using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server;
using Server.Custom.Systems.CustomFeatureFlags;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Server.Custom.Systems.AchievementSystem;

public static class AchievementService
{
    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(AchievementService));
    private static readonly Dictionary<string, AchievementDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint, AchievementPlayerState> _players = new();
    private static readonly Dictionary<string, AchievementAccountState> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint, Queue<AchievementNotificationRecord>> _pendingNotifications = new();
    private static readonly Dictionary<uint, AchievementNotificationRecord> _activeNotifications = new();
    private static bool _configured;
    private static bool _initialized;

    private static readonly AchievementJournalView[] _journalViewOrder =
    {
        AchievementJournalView.Overview,
        AchievementJournalView.CharacterSkills,
        AchievementJournalView.CharacterHarvesting,
        AchievementJournalView.Account,
        AchievementJournalView.Legacy
    };

    private static readonly Dictionary<SkillName, string> _skillDisplayOverrides = new()
    {
        { SkillName.AnimalLore, "Animal Lore" },
        { SkillName.AnimalTaming, "Animal Taming" },
        { SkillName.ArmsLore, "Arms Lore" },
        { SkillName.DetectHidden, "Detect Hidden" },
        { SkillName.EvalInt, "Evaluating Intelligence" },
        { SkillName.ItemID, "Item Identification" },
        { SkillName.MagicResist, "Magic Resist" },
        { SkillName.RemoveTrap, "Remove Trap" },
        { SkillName.SpiritSpeak, "Spirit Speak" },
        { SkillName.TasteID, "Taste Identification" }
    };

    private static readonly SkillName[] _uorSkills =
    {
        SkillName.Alchemy,
        SkillName.Anatomy,
        SkillName.AnimalLore,
        SkillName.ItemID,
        SkillName.ArmsLore,
        SkillName.Parry,
        SkillName.Begging,
        SkillName.Blacksmith,
        SkillName.Fletching,
        SkillName.Peacemaking,
        SkillName.Camping,
        SkillName.Carpentry,
        SkillName.Cartography,
        SkillName.Cooking,
        SkillName.DetectHidden,
        SkillName.Discordance,
        SkillName.EvalInt,
        SkillName.Healing,
        SkillName.Fishing,
        SkillName.Forensics,
        SkillName.Herding,
        SkillName.Hiding,
        SkillName.Provocation,
        SkillName.Inscribe,
        SkillName.Lockpicking,
        SkillName.Magery,
        SkillName.MagicResist,
        SkillName.Tactics,
        SkillName.Snooping,
        SkillName.Musicianship,
        SkillName.Poisoning,
        SkillName.Archery,
        SkillName.SpiritSpeak,
        SkillName.Stealing,
        SkillName.Tailoring,
        SkillName.AnimalTaming,
        SkillName.TasteID,
        SkillName.Tinkering,
        SkillName.Tracking,
        SkillName.Veterinary,
        SkillName.Swords,
        SkillName.Macing,
        SkillName.Fencing,
        SkillName.Wrestling,
        SkillName.Lumberjacking,
        SkillName.Mining,
        SkillName.Meditation,
        SkillName.Stealth,
        SkillName.RemoveTrap
    };

    private static readonly (CraftResource Resource, string DisplayName)[] _oreAchievements =
    {
        (CraftResource.Iron, "Iron"),
        (CraftResource.DullCopper, "Dull Copper"),
        (CraftResource.ShadowIron, "Shadow Iron"),
        (CraftResource.Copper, "Copper"),
        (CraftResource.Bronze, "Bronze"),
        (CraftResource.Gold, "Gold"),
        (CraftResource.Agapite, "Agapite"),
        (CraftResource.Verite, "Verite"),
        (CraftResource.Valorite, "Valorite")
    };

    private static readonly int[] _harvestTierThresholds = { 500, 1000, 5000 };

    public static void Configure()
    {
        if (_configured)
        {
            return;
        }

        _configured = true;

        AchievementPersistence.Configure();
        AchievementCommands.Configure();
        EnsureFlagRegistered();
        RegisterDefinitions();
    }

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        EnsureFlagRegistered();
        RegisterDefinitions();
    }

    public static bool IsSystemEnabled()
    {
        return CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.AchievementSystem);
    }

    public static void DisplayAchievementGump(PlayerMobile player, AchievementJournalView view = AchievementJournalView.Overview, int pageIndex = 0)
    {
        if (player?.NetState == null)
        {
            return;
        }

        if (!IsSystemEnabled())
        {
            player.SendMessage(0x22, "Achievement system is disabled.");
            return;
        }

        EvaluatePlayer(player);

        AchievementGump.DisplayTo(player, NormalizeJournalView(view), pageIndex);
    }

    public static void EvaluatePlayer(PlayerMobile player)
    {
        if (!ShouldTrackPlayer(player) || !IsSystemEnabled())
        {
            return;
        }

        foreach (var definition in _definitions.Values)
        {
            var state = GetOrCreateProgressState(player, definition);
            EvaluateDefinition(player, state, definition);
        }
    }

    /* BEGIN ACHIEVEMENT SYSTEM CUSTOMIZATION: achievement-owned live progress tracking from gameplay hooks */
    public static void RecordSkillChange(PlayerMobile player, SkillName skill, double oldBase)
    {
        if (!ShouldTrackPlayer(player) || !IsSystemEnabled())
        {
            return;
        }

        var newBase = player.Skills[skill].Base;

        if (newBase <= oldBase)
        {
            return;
        }

        var progress = (int)newBase;

        foreach (var definition in _definitions.Values)
        {
            if (definition.TriggerType != AchievementTriggerType.SkillMilestone || definition.Skill != skill)
            {
                continue;
            }

            if (!IsAchievementEarnable(definition))
            {
                continue;
            }

            var state = GetOrCreateProgressState(player, definition);
            UpdateProgressValue(state, definition.Id, progress);

            if (progress >= definition.Threshold)
            {
                UnlockAchievement(player, state, definition);
            }
        }
    }

    public static void RecordCreatureKill(BaseCreature creature, List<DamageStore> rights)
    {
        if (creature == null || rights == null || rights.Count == 0 || !IsSystemEnabled())
        {
            return;
        }

        var creatureTypeName = creature.GetType().Name;

        foreach (var definition in _definitions.Values)
        {
            if (
                definition.TriggerType != AchievementTriggerType.CreatureKillCount ||
                !string.Equals(definition.CreatureTypeName, creatureTypeName, StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            if (!IsAchievementEarnable(definition))
            {
                continue;
            }

            for (var i = 0; i < rights.Count; i++)
            {
                var damageStore = rights[i];

                if (damageStore?.m_Mobile is not PlayerMobile player || !damageStore.m_HasRight || !ShouldTrackPlayer(player))
                {
                    continue;
                }

                var state = GetOrCreateProgressState(player, definition);
                var progress = GetProgressValue(state, definition.Id) + 1;
                UpdateProgressValue(state, definition.Id, progress);

                if (progress >= definition.Threshold)
                {
                    UnlockAchievement(player, state, definition);
                }
            }
        }
    }

    public static void RecordHarvestYield(Mobile from, Item item, AchievementHarvestKind harvestKind)
    {
        if (from is not PlayerMobile player || item == null || !ShouldTrackPlayer(player) || !IsSystemEnabled())
        {
            return;
        }

        var resource = item switch
        {
            BaseOre ore => ore.Resource,
            Log log     => log.Resource,
            _           => CraftResource.None
        };

        if (resource == CraftResource.None)
        {
            return;
        }

        var quantity = item.Amount > 0 ? item.Amount : 1;

        foreach (var definition in _definitions.Values)
        {
            if (
                definition.TriggerType != AchievementTriggerType.HarvestResourceCount ||
                definition.HarvestKind != harvestKind ||
                definition.Resource != resource ||
                !IsAchievementEarnable(definition)
            )
            {
                continue;
            }

            var state = GetOrCreateProgressState(player, definition);
            var progress = GetProgressValue(state, definition.Id) + quantity;
            UpdateProgressValue(state, definition.Id, progress);

            if (progress >= definition.Threshold)
            {
                UnlockAchievement(player, state, definition);
            }
        }
    }

    public static void RecordFishingCatch(Mobile from, Item item)
    {
        if (from is not PlayerMobile player || item == null || !ShouldTrackPlayer(player) || !IsSystemEnabled())
        {
            return;
        }

        var catchKind = GetFishingCatchKind(item);

        if (catchKind == AchievementFishingCatchKind.None)
        {
            return;
        }

        var quantity = item is Fish && item.Amount > 0 ? item.Amount : 1;

        foreach (var definition in _definitions.Values)
        {
            if (
                definition.TriggerType != AchievementTriggerType.FishingCatchCount ||
                definition.FishingCatchKind != catchKind ||
                !IsAchievementEarnable(definition)
            )
            {
                continue;
            }

            var state = GetOrCreateProgressState(player, definition);
            var progress = GetProgressValue(state, definition.Id) + quantity;
            UpdateProgressValue(state, definition.Id, progress);

            if (progress >= definition.Threshold)
            {
                UnlockAchievement(player, state, definition);
            }
        }
    }

    public static void RecordFishingSerpentEncounter(Mobile from)
    {
        if (from is not PlayerMobile player || !ShouldTrackPlayer(player) || !IsSystemEnabled())
        {
            return;
        }

        foreach (var definition in _definitions.Values)
        {
            if (
                definition.TriggerType != AchievementTriggerType.FishingCatchCount ||
                definition.FishingCatchKind != AchievementFishingCatchKind.SeaSerpent ||
                !IsAchievementEarnable(definition)
            )
            {
                continue;
            }

            var state = GetOrCreateProgressState(player, definition);
            var progress = GetProgressValue(state, definition.Id) + 1;
            UpdateProgressValue(state, definition.Id, progress);

            if (progress >= definition.Threshold)
            {
                UnlockAchievement(player, state, definition);
            }
        }
    }
    /* END ACHIEVEMENT SYSTEM CUSTOMIZATION */

    public static void ResetPlayer(PlayerMobile player)
    {
        if (player == null)
        {
            return;
        }

        var serial = player.Serial.Value;
        _players.Remove(serial);
        RemoveAccountState(player);
        _pendingNotifications.Remove(serial);
        _activeNotifications.Remove(serial);
        player.CloseGump<AchievementUnlockGump>();
        player.CloseGump<AchievementGump>();
    }

    public static void ResetAllPlayers()
    {
        _players.Clear();
        _accounts.Clear();
        _pendingNotifications.Clear();
        _activeNotifications.Clear();
    }

    /* BEGIN ACHIEVEMENT SYSTEM CUSTOMIZATION: staff pruning removes one achievement from achievement-owned state */
    public static bool TryRemoveAchievement(
        PlayerMobile player,
        string achievementId,
        out AchievementDefinition definition,
        out bool removedUnlock,
        out bool removedProgress
    )
    {
        definition = null;
        removedUnlock = false;
        removedProgress = false;

        if (player == null || string.IsNullOrWhiteSpace(achievementId))
        {
            return false;
        }

        if (!_definitions.TryGetValue(achievementId, out definition))
        {
            return false;
        }

        var state = GetOrCreateProgressState(player, definition);
        removedUnlock = state.UnlockedAchievements.Remove(definition.Id);
        removedProgress = state.ProgressValues.Remove(definition.Id);

        if (removedUnlock || removedProgress)
        {
            state.LastUpdatedUtc = DateTime.UtcNow;
        }

        ClearQueuedNotification(player, definition.Id);
        player.CloseGump<AchievementUnlockGump>();
        player.CloseGump<AchievementGump>();

        return true;
    }
    /* END ACHIEVEMENT SYSTEM CUSTOMIZATION */

    public static void CompleteActiveNotification(PlayerMobile player, string achievementId, bool openJournal)
    {
        if (player == null)
        {
            return;
        }

        var serial = player.Serial.Value;

        if (
            !_activeNotifications.TryGetValue(serial, out var active) ||
            !string.Equals(active.AchievementId, achievementId, StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        _activeNotifications.Remove(serial);

        if (openJournal)
        {
            player.CloseGump<AchievementUnlockGump>();
            DisplayAchievementGump(player, GetJournalView(active.Scope, active.Category, active.IsLegacy));
            return;
        }

        TryDisplayNextNotification(player);
    }

    public static AchievementJournalView[] GetAvailableJournalViews()
    {
        var views = new List<AchievementJournalView> { AchievementJournalView.Overview };

        foreach (var view in _journalViewOrder)
        {
            if (view == AchievementJournalView.Overview)
            {
                continue;
            }

            foreach (var definition in _definitions.Values)
            {
                if (MatchesJournalView(definition, view))
                {
                    views.Add(view);
                    break;
                }
            }
        }

        return views.ToArray();
    }

    public static string GetJournalViewDisplayName(AchievementJournalView view)
    {
        return view switch
        {
            AchievementJournalView.Overview => "Overview",
            AchievementJournalView.CharacterSkills => "Character: Skills",
            AchievementJournalView.CharacterHarvesting => "Character: Harvesting",
            AchievementJournalView.Account => "Account",
            AchievementJournalView.Legacy => "Legacy",
            _ => "Overview"
        };
    }

    public static string GetCategoryDisplayName(AchievementCategory category)
    {
        return category switch
        {
            AchievementCategory.Skills => "Skills",
            AchievementCategory.Hunting => "Hunting",
            AchievementCategory.Harvesting => "Harvesting",
            AchievementCategory.Crafting => "Crafting",
            AchievementCategory.Exploration => "Exploration",
            AchievementCategory.Economy => "Economy",
            _ => "General"
        };
    }

    public static string GetScopeDisplayName(AchievementScope scope)
    {
        return scope switch
        {
            AchievementScope.Character => "Character",
            AchievementScope.Account => "Account",
            _ => "Character"
        };
    }

    public static List<AchievementDefinition> GetDefinitions(AchievementJournalView view)
    {
        var definitions = new List<AchievementDefinition>();

        foreach (var definition in _definitions.Values)
        {
            if (!MatchesJournalView(definition, view))
            {
                continue;
            }

            definitions.Add(definition);
        }

        definitions.Sort(static (a, b) =>
        {
            var categoryCompare = a.Category.CompareTo(b.Category);
            if (categoryCompare != 0)
            {
                return categoryCompare;
            }

            var sortCompare = a.SortOrder.CompareTo(b.SortOrder);
            if (sortCompare != 0)
            {
                return sortCompare;
            }

            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return definitions;
    }

    public static int GetDefinitionCount(AchievementJournalView view = AchievementJournalView.Overview)
    {
        return GetDefinitions(view).Count;
    }

    public static int GetUnlockedCount(PlayerMobile player, AchievementJournalView view = AchievementJournalView.Overview)
    {
        if (player == null)
        {
            return 0;
        }

        var count = 0;

        foreach (var definition in GetDefinitions(view))
        {
            var state = GetOrCreateProgressState(player, definition);

            if (state.UnlockedAchievements.ContainsKey(definition.Id))
            {
                count++;
            }
        }

        return count;
    }

    internal static AchievementPlayerState GetOrCreatePlayerState(PlayerMobile player)
    {
        var serial = player.Serial.Value;

        if (!_players.TryGetValue(serial, out var state))
        {
            state = new AchievementPlayerState
            {
                PlayerSerial = serial,
                PlayerName = player.Name,
                AccountName = player.Account?.Username ?? string.Empty,
                FirstSeenUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            };

            _players[serial] = state;
        }
        else
        {
            state.PlayerName = player.Name;
            state.AccountName = player.Account?.Username ?? string.Empty;
        }

        return state;
    }

    internal static AchievementAccountState GetOrCreateAccountState(PlayerMobile player)
    {
        var accountKey = GetAccountKey(player);

        if (!_accounts.TryGetValue(accountKey, out var state))
        {
            state = new AchievementAccountState
            {
                AccountKey = accountKey,
                AccountName = player.Account?.Username ?? string.Empty,
                FirstSeenUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            };

            _accounts[accountKey] = state;
        }
        else
        {
            state.AccountName = player.Account?.Username ?? string.Empty;
        }

        return state;
    }

    internal static AchievementProgressState GetOrCreateProgressState(PlayerMobile player, AchievementDefinition definition)
    {
        return definition.Scope == AchievementScope.Account
            ? GetOrCreateAccountState(player)
            : GetOrCreatePlayerState(player);
    }

    internal static int GetProgressValue(AchievementProgressState state, string achievementId)
    {
        return state.ProgressValues.TryGetValue(achievementId, out var value) ? value : 0;
    }

    internal static int GetDisplayedProgress(PlayerMobile player, AchievementDefinition definition)
    {
        if (player == null || definition == null)
        {
            return 0;
        }

        var state = GetOrCreateProgressState(player, definition);
        var progress = GetProgressValue(state, definition.Id);

        if (progress > 0)
        {
            return progress;
        }

        if (definition.TriggerType == AchievementTriggerType.SkillMilestone)
        {
            return (int)player.Skills[definition.Skill].Base;
        }

        return 0;
    }

    internal static bool IsUnlocked(PlayerMobile player, string achievementId)
    {
        if (player == null || string.IsNullOrWhiteSpace(achievementId))
        {
            return false;
        }

        var definition = GetDefinition(achievementId);

        if (definition == null)
        {
            return false;
        }

        var state = GetOrCreateProgressState(player, definition);
        return state.UnlockedAchievements.ContainsKey(achievementId);
    }

    internal static AchievementUnlockRecord GetUnlockRecord(PlayerMobile player, string achievementId)
    {
        if (player == null || string.IsNullOrWhiteSpace(achievementId))
        {
            return null;
        }

        var definition = GetDefinition(achievementId);

        if (definition == null)
        {
            return null;
        }

        var state = GetOrCreateProgressState(player, definition);
        return state.UnlockedAchievements.TryGetValue(achievementId, out var record) ? record : null;
    }

    internal static void SerializePersistence(IGenericWriter writer)
    {
        writer.WriteEncodedInt(_players.Count);

        foreach (var pair in _players)
        {
            var state = pair.Value;

            writer.Write(pair.Key);
            writer.Write(state.PlayerName);
            writer.Write(state.AccountName);
            writer.Write(state.FirstSeenUtc);
            writer.Write(state.LastUpdatedUtc);

            writer.WriteEncodedInt(state.ProgressValues.Count);

            foreach (var progressPair in state.ProgressValues)
            {
                writer.Write(progressPair.Key);
                writer.WriteEncodedInt(progressPair.Value);
            }

            writer.WriteEncodedInt(state.UnlockedAchievements.Count);

            foreach (var unlockPair in state.UnlockedAchievements)
            {
                writer.Write(unlockPair.Key);
                writer.Write(unlockPair.Value.UnlockedUtc);
            }

        }

        writer.WriteEncodedInt(_accounts.Count);

        foreach (var pair in _accounts)
        {
            var state = pair.Value;

            writer.Write(pair.Key);
            writer.Write(state.AccountName);
            writer.Write(state.FirstSeenUtc);
            writer.Write(state.LastUpdatedUtc);

            WriteProgressState(writer, state);
        }
    }

    internal static void DeserializePersistence(IGenericReader reader)
    {
        DeserializePersistence(reader, 1);
    }

    internal static void DeserializePersistence(IGenericReader reader, int version)
    {
        _players.Clear();
        _accounts.Clear();
        _pendingNotifications.Clear();
        _activeNotifications.Clear();

        var playerCount = reader.ReadEncodedInt();

        for (var i = 0; i < playerCount; i++)
        {
            var serial = reader.ReadUInt();
            var state = new AchievementPlayerState
            {
                PlayerSerial = serial,
                PlayerName = reader.ReadString(),
                AccountName = reader.ReadString(),
                FirstSeenUtc = reader.ReadDateTime(),
                LastUpdatedUtc = reader.ReadDateTime()
            };

            var progressCount = reader.ReadEncodedInt();

            for (var j = 0; j < progressCount; j++)
            {
                var key = reader.ReadString();
                var value = reader.ReadEncodedInt();

                if (!string.IsNullOrWhiteSpace(key))
                {
                    state.ProgressValues[key] = value;
                }
            }

            var unlockCount = reader.ReadEncodedInt();

            for (var j = 0; j < unlockCount; j++)
            {
                var key = reader.ReadString();
                var unlockedUtc = reader.ReadDateTime();

                if (!string.IsNullOrWhiteSpace(key))
                {
                    state.UnlockedAchievements[key] = new AchievementUnlockRecord { UnlockedUtc = unlockedUtc };
                }
            }

            _players[serial] = state;
        }

        if (version <= 0)
        {
            return;
        }

        var accountCount = reader.ReadEncodedInt();

        for (var i = 0; i < accountCount; i++)
        {
            var accountKey = reader.ReadString();
            var state = new AchievementAccountState
            {
                AccountKey = accountKey,
                AccountName = reader.ReadString(),
                FirstSeenUtc = reader.ReadDateTime(),
                LastUpdatedUtc = reader.ReadDateTime()
            };

            ReadProgressState(reader, state);

            if (!string.IsNullOrWhiteSpace(accountKey))
            {
                _accounts[accountKey] = state;
            }
        }
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnPlayerLogin(PlayerMobile player)
    {
        EvaluatePlayer(player);
    }

    private static void EnsureFlagRegistered()
    {
        if (CustomFeatureFlagManager.IsRegistered(CustomFeatureFlagKeys.AchievementSystem))
        {
            return;
        }

        CustomFeatureFlagManager.Register(
            CustomFeatureFlagKeys.AchievementSystem,
            "Achievement System",
            "Tracks and unlocks player achievements",
            "Custom Systems",
            defaultEnabled: true
        );
    }

    private static void RegisterDefinitions()
    {
        if (_definitions.Count > 0)
        {
            return;
        }

        var skillDefinitions = new List<(SkillName Skill, string DisplayName)>(_uorSkills.Length);

        for (var i = 0; i < _uorSkills.Length; i++)
        {
            var skill = _uorSkills[i];
            skillDefinitions.Add((skill, GetSkillDisplayName(skill)));
        }

        skillDefinitions.Sort(static (a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

        for (var i = 0; i < skillDefinitions.Count; i++)
        {
            var skill = skillDefinitions[i].Skill;
            var skillDisplayName = skillDefinitions[i].DisplayName;

            RegisterDefinition(
                new AchievementDefinition
                {
                    Id = $"gm_{skill.ToString().ToLowerInvariant()}",
                    Name = $"Grandmaster {skillDisplayName}",
                    Description = $"Reach 100.0 in {skillDisplayName}.",
                    Category = AchievementCategory.Skills,
                    TriggerType = AchievementTriggerType.SkillMilestone,
                    Scope = AchievementScope.Character,
                    IsLegacy = false,
                    Skill = skill,
                    Threshold = 100,
                    SortOrder = i
                }
            );
        }

        RegisterHarvestDefinitions();
        RegisterFishingDefinitions();
    }

    private static void RegisterHarvestDefinitions()
    {
        var sortOrder = 1000;

        for (var i = 0; i < _oreAchievements.Length; i++)
        {
            var ore = _oreAchievements[i];

            for (var tierIndex = 0; tierIndex < _harvestTierThresholds.Length; tierIndex++)
            {
                var threshold = _harvestTierThresholds[tierIndex];

                RegisterDefinition(
                    new AchievementDefinition
                    {
                        Id = $"mine_{ore.Resource.ToString().ToLowerInvariant()}_{threshold}",
                        Name = $"{ore.DisplayName} Prospector {RomanizeTier(tierIndex + 1)}",
                        Description = $"Mine {threshold:N0} {ore.DisplayName.ToLowerInvariant()} ore.",
                        Category = AchievementCategory.Harvesting,
                        TriggerType = AchievementTriggerType.HarvestResourceCount,
                        Scope = AchievementScope.Character,
                        HarvestKind = AchievementHarvestKind.Mining,
                        Resource = ore.Resource,
                        Threshold = threshold,
                        SortOrder = sortOrder++
                    }
                );
            }
        }

        for (var tierIndex = 0; tierIndex < _harvestTierThresholds.Length; tierIndex++)
        {
            var threshold = _harvestTierThresholds[tierIndex];

            RegisterDefinition(
                new AchievementDefinition
                {
                    Id = $"chop_regular_logs_{threshold}",
                    Name = $"Lumber Stockpile {RomanizeTier(tierIndex + 1)}",
                    Description = $"Chop {threshold:N0} logs.",
                    Category = AchievementCategory.Harvesting,
                    TriggerType = AchievementTriggerType.HarvestResourceCount,
                    Scope = AchievementScope.Character,
                    HarvestKind = AchievementHarvestKind.Lumberjacking,
                    Resource = CraftResource.RegularWood,
                    Threshold = threshold,
                    SortOrder = sortOrder++
                }
            );
        }
    }

    private static void RegisterFishingDefinitions()
    {
        var sortOrder = 1100;

        for (var tierIndex = 0; tierIndex < _harvestTierThresholds.Length; tierIndex++)
        {
            var threshold = _harvestTierThresholds[tierIndex];

            RegisterDefinition(
                new AchievementDefinition
                {
                    Id = $"catch_normal_fish_{threshold}",
                    Name = $"Netful of Fish {RomanizeTier(tierIndex + 1)}",
                    Description = $"Catch {threshold:N0} fish.",
                    Category = AchievementCategory.Harvesting,
                    TriggerType = AchievementTriggerType.FishingCatchCount,
                    Scope = AchievementScope.Character,
                    FishingCatchKind = AchievementFishingCatchKind.NormalFish,
                    Threshold = threshold,
                    SortOrder = sortOrder++
                }
            );
        }

        RegisterDefinition(
            new AchievementDefinition
            {
                Id = "catch_big_fish",
                Name = "Big Catch",
                Description = "Catch a big fish.",
                Category = AchievementCategory.Harvesting,
                TriggerType = AchievementTriggerType.FishingCatchCount,
                Scope = AchievementScope.Character,
                FishingCatchKind = AchievementFishingCatchKind.BigFish,
                Threshold = 1,
                SortOrder = sortOrder++
            }
        );

        RegisterDefinition(
            new AchievementDefinition
            {
                Id = "catch_magic_fish",
                Name = "Strange Scales",
                Description = "Catch a magic fish.",
                Category = AchievementCategory.Harvesting,
                TriggerType = AchievementTriggerType.FishingCatchCount,
                Scope = AchievementScope.Character,
                FishingCatchKind = AchievementFishingCatchKind.MagicFish,
                Threshold = 1,
                SortOrder = sortOrder++
            }
        );

        RegisterDefinition(
            new AchievementDefinition
            {
                Id = "fish_message_in_a_bottle",
                Name = "Message in a Bottle",
                Description = "Fish up a message in a bottle.",
                Category = AchievementCategory.Harvesting,
                TriggerType = AchievementTriggerType.FishingCatchCount,
                Scope = AchievementScope.Character,
                FishingCatchKind = AchievementFishingCatchKind.MessageInABottle,
                Threshold = 1,
                SortOrder = sortOrder++
            }
        );

        RegisterDefinition(
            new AchievementDefinition
            {
                Id = "fish_special_net",
                Name = "Net Gain",
                Description = "Fish up a special fishing net.",
                Category = AchievementCategory.Harvesting,
                TriggerType = AchievementTriggerType.FishingCatchCount,
                Scope = AchievementScope.Character,
                FishingCatchKind = AchievementFishingCatchKind.SpecialFishingNet,
                Threshold = 1,
                SortOrder = sortOrder++
            }
        );

        RegisterDefinition(
            new AchievementDefinition
            {
                Id = "fish_sea_serpent",
                Name = "That Is Not a Fish",
                Description = "Stir up a sea serpent while fishing.",
                Category = AchievementCategory.Harvesting,
                TriggerType = AchievementTriggerType.FishingCatchCount,
                Scope = AchievementScope.Character,
                FishingCatchKind = AchievementFishingCatchKind.SeaSerpent,
                Threshold = 1,
                SortOrder = sortOrder
            }
        );
    }

    private static void RegisterDefinition(AchievementDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
        {
            return;
        }

        _definitions[definition.Id] = definition;
    }

    private static AchievementDefinition GetDefinition(string achievementId)
    {
        if (string.IsNullOrWhiteSpace(achievementId))
        {
            return null;
        }

        return _definitions.TryGetValue(achievementId, out var definition) ? definition : null;
    }

    /* BEGIN ACHIEVEMENT SYSTEM CUSTOMIZATION: targeted prune support clears queued one-time notifications */
    private static void ClearQueuedNotification(PlayerMobile player, string achievementId)
    {
        if (player == null || string.IsNullOrWhiteSpace(achievementId))
        {
            return;
        }

        var serial = player.Serial.Value;

        if (
            _activeNotifications.TryGetValue(serial, out var active) &&
            string.Equals(active.AchievementId, achievementId, StringComparison.OrdinalIgnoreCase)
        )
        {
            _activeNotifications.Remove(serial);
        }

        if (!_pendingNotifications.TryGetValue(serial, out var queue) || queue.Count == 0)
        {
            return;
        }

        var filteredQueue = new Queue<AchievementNotificationRecord>();

        while (queue.Count > 0)
        {
            var notification = queue.Dequeue();

            if (string.Equals(notification.AchievementId, achievementId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            filteredQueue.Enqueue(notification);
        }

        if (filteredQueue.Count > 0)
        {
            _pendingNotifications[serial] = filteredQueue;
        }
        else
        {
            _pendingNotifications.Remove(serial);
        }
    }
    /* END ACHIEVEMENT SYSTEM CUSTOMIZATION */

    private static void EvaluateDefinition(
        PlayerMobile player,
        AchievementProgressState state,
        AchievementDefinition definition
    )
    {
        if (!IsAchievementEarnable(definition))
        {
            return;
        }

        if (state.UnlockedAchievements.ContainsKey(definition.Id))
        {
            return;
        }

        var progress = definition.TriggerType switch
        {
            AchievementTriggerType.SkillMilestone => GetSkillProgress(player, definition),
            AchievementTriggerType.CreatureKillCount => GetProgressValue(state, definition.Id),
            AchievementTriggerType.HarvestResourceCount => GetProgressValue(state, definition.Id),
            AchievementTriggerType.FishingCatchCount => GetProgressValue(state, definition.Id),
            _ => 0
        };

        UpdateProgressValue(state, definition.Id, progress);

        if (progress >= definition.Threshold)
        {
            UnlockAchievement(player, state, definition);
        }
    }

    private static int GetSkillProgress(PlayerMobile player, AchievementDefinition definition)
    {
        return (int)player.Skills[definition.Skill].Base;
    }

    private static void UpdateProgressValue(AchievementProgressState state, string achievementId, int progress)
    {
        if (string.IsNullOrWhiteSpace(achievementId) || progress <= GetProgressValue(state, achievementId))
        {
            return;
        }

        state.ProgressValues[achievementId] = progress;
        state.LastUpdatedUtc = DateTime.UtcNow;
    }

    private static void UnlockAchievement(
        PlayerMobile player,
        AchievementProgressState state,
        AchievementDefinition definition
    )
    {
        if (state.UnlockedAchievements.ContainsKey(definition.Id))
        {
            return;
        }

        var timestamp = DateTime.UtcNow;
        state.UnlockedAchievements[definition.Id] = new AchievementUnlockRecord { UnlockedUtc = timestamp };
        state.LastUpdatedUtc = timestamp;

        QueueUnlockNotification(player, definition);
        PlayUnlockEffects(player);

        Logger.Information(
            "[AchievementSystem] {PlayerName}/{AccountName} unlocked {AchievementId} at {TimestampUtc:O}.",
            player.Name,
            player.Account?.Username ?? string.Empty,
            definition.Id,
            timestamp
        );
    }

    private static void QueueUnlockNotification(PlayerMobile player, AchievementDefinition definition)
    {
        if (player?.NetState == null || definition == null)
        {
            return;
        }

        var serial = player.Serial.Value;

        if (!_pendingNotifications.TryGetValue(serial, out var queue))
        {
            queue = new Queue<AchievementNotificationRecord>();
            _pendingNotifications[serial] = queue;
        }

        queue.Enqueue(
            new AchievementNotificationRecord
            {
                AchievementId = definition.Id,
                Name = definition.Name,
                Description = definition.Description,
                Category = definition.Category,
                Scope = definition.Scope,
                IsLegacy = definition.IsLegacy,
                LegacyLabel = definition.LegacyLabel
            }
        );

        TryDisplayNextNotification(player);
    }

    private static void TryDisplayNextNotification(PlayerMobile player)
    {
        if (player?.NetState == null)
        {
            return;
        }

        var serial = player.Serial.Value;

        if (_activeNotifications.ContainsKey(serial))
        {
            return;
        }

        if (!_pendingNotifications.TryGetValue(serial, out var queue) || queue.Count == 0)
        {
            return;
        }

        var notification = queue.Dequeue();
        _activeNotifications[serial] = notification;

        if (queue.Count == 0)
        {
            _pendingNotifications.Remove(serial);
        }

        AchievementUnlockGump.DisplayTo(player, notification);
    }

    private static void PlayUnlockEffects(PlayerMobile player)
    {
        if (player == null)
        {
            return;
        }

        player.FixedEffect(0x373A, 10, 15, 1153, 0);
        player.PlaySound(0x1F5);
    }

    private static string GetSkillDisplayName(SkillName skill)
    {
        if (_skillDisplayOverrides.TryGetValue(skill, out var overrideName))
        {
            return overrideName;
        }

        var index = (int)skill;

        if (SkillInfo.Table != null && index >= 0 && index < SkillInfo.Table.Length && SkillInfo.Table[index] != null)
        {
            var tableName = SkillInfo.Table[index].Name;
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                return tableName;
            }
        }

        return HumanizeIdentifier(skill.ToString());
    }

    private static string HumanizeIdentifier(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var chars = new List<char>(raw.Length * 2);

        for (var i = 0; i < raw.Length; i++)
        {
            var current = raw[i];
            var previous = i > 0 ? raw[i - 1] : '\0';
            var next = i + 1 < raw.Length ? raw[i + 1] : '\0';

            if (
                i > 0 &&
                char.IsUpper(current) &&
                (char.IsLower(previous) || (char.IsUpper(previous) && next != '\0' && char.IsLower(next)))
            )
            {
                chars.Add(' ');
            }

            chars.Add(current);
        }

        return new string(chars.ToArray());
    }

    private static bool ShouldTrackPlayer(PlayerMobile player)
    {
        return player is { Deleted: false };
    }

    private static bool MatchesJournalView(AchievementDefinition definition, AchievementJournalView view)
    {
        if (definition == null)
        {
            return false;
        }

        return view switch
        {
            AchievementJournalView.Overview => true,
            AchievementJournalView.CharacterSkills =>
                definition.Scope == AchievementScope.Character &&
                definition.Category == AchievementCategory.Skills &&
                !definition.IsLegacy,
            AchievementJournalView.CharacterHarvesting =>
                definition.Scope == AchievementScope.Character &&
                definition.Category == AchievementCategory.Harvesting &&
                !definition.IsLegacy,
            AchievementJournalView.Account => definition.Scope == AchievementScope.Account && !definition.IsLegacy,
            AchievementJournalView.Legacy => definition.IsLegacy,
            _ => false
        };
    }

    private static AchievementJournalView GetJournalView(AchievementScope scope, AchievementCategory category, bool isLegacy)
    {
        if (isLegacy)
        {
            return AchievementJournalView.Legacy;
        }

        if (scope == AchievementScope.Account)
        {
            return AchievementJournalView.Account;
        }

        return category == AchievementCategory.Harvesting
            ? AchievementJournalView.CharacterHarvesting
            : AchievementJournalView.CharacterSkills;
    }

    private static bool IsAchievementEarnable(AchievementDefinition definition)
    {
        return definition is { IsLegacy: false };
    }

    private static AchievementFishingCatchKind GetFishingCatchKind(Item item)
    {
        return item switch
        {
            Fish => AchievementFishingCatchKind.NormalFish,
            BigFish => AchievementFishingCatchKind.BigFish,
            BaseMagicFish => AchievementFishingCatchKind.MagicFish,
            MessageInABottle => AchievementFishingCatchKind.MessageInABottle,
            SpecialFishingNet => AchievementFishingCatchKind.SpecialFishingNet,
            _ => AchievementFishingCatchKind.None
        };
    }

    private static string RomanizeTier(int tier)
    {
        return tier switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            _ => tier.ToString()
        };
    }

    private static AchievementJournalView NormalizeJournalView(AchievementJournalView view)
    {
        foreach (var availableView in GetAvailableJournalViews())
        {
            if (availableView == view)
            {
                return view;
            }
        }

        return AchievementJournalView.Overview;
    }

    private static string GetAccountKey(PlayerMobile player)
    {
        var accountName = player?.Account?.Username;
        return string.IsNullOrWhiteSpace(accountName) ? $"serial:{player?.Serial.Value ?? 0}" : accountName.Trim();
    }

    private static void RemoveAccountState(PlayerMobile player)
    {
        if (player == null)
        {
            return;
        }

        _accounts.Remove(GetAccountKey(player));
    }

    private static void WriteProgressState(IGenericWriter writer, AchievementProgressState state)
    {
        writer.WriteEncodedInt(state.ProgressValues.Count);

        foreach (var progressPair in state.ProgressValues)
        {
            writer.Write(progressPair.Key);
            writer.WriteEncodedInt(progressPair.Value);
        }

        writer.WriteEncodedInt(state.UnlockedAchievements.Count);

        foreach (var unlockPair in state.UnlockedAchievements)
        {
            writer.Write(unlockPair.Key);
            writer.Write(unlockPair.Value.UnlockedUtc);
        }
    }

    private static void ReadProgressState(IGenericReader reader, AchievementProgressState state)
    {
        var progressCount = reader.ReadEncodedInt();

        for (var j = 0; j < progressCount; j++)
        {
            var key = reader.ReadString();
            var value = reader.ReadEncodedInt();

            if (!string.IsNullOrWhiteSpace(key))
            {
                state.ProgressValues[key] = value;
            }
        }

        var unlockCount = reader.ReadEncodedInt();

        for (var j = 0; j < unlockCount; j++)
        {
            var key = reader.ReadString();
            var unlockedUtc = reader.ReadDateTime();

            if (!string.IsNullOrWhiteSpace(key))
            {
                state.UnlockedAchievements[key] = new AchievementUnlockRecord { UnlockedUtc = unlockedUtc };
            }
        }
    }
}

public enum AchievementJournalView
{
    Overview,
    CharacterSkills,
    CharacterHarvesting,
    Account,
    Legacy
}

public enum AchievementCategory
{
    Skills,
    Hunting,
    Harvesting,
    Crafting,
    Exploration,
    Economy
}

public enum AchievementTriggerType
{
    SkillMilestone,
    CreatureKillCount,
    HarvestResourceCount,
    FishingCatchCount
}

public enum AchievementScope
{
    Character,
    Account
}

public enum AchievementHarvestKind
{
    None,
    Mining,
    Lumberjacking
}

public enum AchievementFishingCatchKind
{
    None,
    NormalFish,
    BigFish,
    MagicFish,
    MessageInABottle,
    SpecialFishingNet,
    SeaSerpent
}

public sealed class AchievementDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public AchievementCategory Category { get; set; }
    public AchievementTriggerType TriggerType { get; set; }
    // New definitions must choose scope and legacy/time-limited status intentionally.
    public AchievementScope Scope { get; set; }
    public bool IsLegacy { get; set; }
    public string LegacyLabel { get; set; }
    public DateTime? EarnableFromUtc { get; set; }
    public DateTime? EarnableUntilUtc { get; set; }
    public AchievementHarvestKind HarvestKind { get; set; }
    public AchievementFishingCatchKind FishingCatchKind { get; set; }
    public CraftResource Resource { get; set; }
    public SkillName Skill { get; set; }
    public string CreatureTypeName { get; set; }
    public int Threshold { get; set; }
    public int SortOrder { get; set; }
}

public class AchievementProgressState
{
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public Dictionary<string, int> ProgressValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, AchievementUnlockRecord> UnlockedAchievements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AchievementPlayerState : AchievementProgressState
{
    public uint PlayerSerial { get; set; }
    public string PlayerName { get; set; }
    public string AccountName { get; set; }
}

public sealed class AchievementAccountState : AchievementProgressState
{
    public string AccountKey { get; set; }
    public string AccountName { get; set; }
}

public sealed class AchievementUnlockRecord
{
    public DateTime UnlockedUtc { get; set; }
}

public sealed class AchievementNotificationRecord
{
    public string AchievementId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public AchievementCategory Category { get; set; }
    public AchievementScope Scope { get; set; }
    public bool IsLegacy { get; set; }
    public string LegacyLabel { get; set; }
}
