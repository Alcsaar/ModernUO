using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server;
using Server.Accounting;
using Server.Custom.Systems.CustomFeatureFlags;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Regions;

namespace Server.Custom.Systems.AchievementSystem;

public static class AchievementService
{
    private const string AccountAllGrandmasterSkillsAchievementId = "account_all_grandmaster_skills";
    private const string AccountGrandmasterSkillProgressPrefix = "account_gm_skill_";
    private const string WorldExplorerAchievementId = "world_explorer";

    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(AchievementService));
    private static readonly Dictionary<string, AchievementDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint, AchievementPlayerState> _players = new();
    private static readonly Dictionary<string, AchievementAccountState> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint, Queue<AchievementNotificationRecord>> _pendingNotifications = new();
    private static readonly Dictionary<uint, AchievementNotificationRecord> _activeNotifications = new();
    private static readonly Dictionary<string, AchievementServerFirstRecord> _serverFirsts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<AchievementServerFirstCandidateRecord>> _serverFirstCandidates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint, string> _lastExplorationRegion = new();
    private static bool _allowStaffServerFirstsForTesting;
    private static bool _configured;
    private static bool _initialized;

    private static readonly AchievementJournalView[] _journalViewOrder =
    {
        AchievementJournalView.Overview,
        AchievementJournalView.CharacterSkills,
        AchievementJournalView.CharacterHunting,
        AchievementJournalView.CharacterExploration,
        AchievementJournalView.CharacterHarvesting,
        AchievementJournalView.CharacterEconomy,
        AchievementJournalView.Account,
        AchievementJournalView.Feats
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

    private static readonly int[] _harvestTierThresholds = { 500, 2500, 5000, 10000 };
    private static readonly int[] _monsterGoldTierThresholds = { 50000, 250000, 1000000, 5000000, 10000000 };
    private static readonly int[] _treasureMapGoldTierThresholds = { 25000, 100000, 500000, 1000000, 2500000 };
    private static readonly int[] _dungeonChestGoldTierThresholds = { 10000, 50000, 250000, 500000, 1000000 };
    private static readonly int[] _vendorSaleGoldTierThresholds = { 10000, 50000, 250000, 500000, 1000000 };
    private static readonly int[] _creatureFamilyKillTierThresholds = { 500, 2500, 5000, 10000 };
    private static readonly int[] _creatureSpecificKillTierThresholds = { 100, 500, 1000, 2500 };
    private static readonly int[] _creatureEliteKillTierThresholds = { 25, 100, 250, 500 };
    private static readonly int[] _treasureMapCompletionTierThresholds = { 1, 10, 50, 100 };

    /* BEGIN ACHIEVEMENT EXPLORATION: named region visit achievements for towns, dungeons, and shrines */
    private static readonly ExplorationVisitDefinition[] _townVisitDefinitions =
    {
        new("town_britain", "Britain"),
        new("town_trinsic", "Trinsic"),
        new("town_minoc", "Minoc"),
        new("town_moonglow", "Moonglow"),
        new("town_yew", "Yew"),
        new("town_skara_brae", "Skara Brae"),
        new("town_new_haven", "New Haven"),
        new("town_magincia", "Magincia"),
        new("town_vesper", "Vesper"),
        new("town_jhelom", "Jhelom"),
        new("town_nujelm", "Nujel'm"),
        new("town_serpents_hold", "Serpent's Hold")
    };

    private static readonly ExplorationVisitDefinition[] _dungeonVisitDefinitions =
    {
        new("dungeon_covetous", "Covetous"),
        new("dungeon_deceit", "Deceit"),
        new("dungeon_despise", "Despise"),
        new("dungeon_destard", "Destard"),
        new("dungeon_wrong", "Wrong"),
        new("dungeon_shame", "Shame"),
        new("dungeon_hythloth", "Hythloth"),
        new("dungeon_doom", "Doom")
    };

    private static readonly ExplorationVisitDefinition[] _shrineVisitDefinitions =
    {
        new("shrine_chaos", "Chaos Shrine", new Point3D(1470, 843, 0), 12),
        new("shrine_compassion", "Compassion Shrine", new Point3D(1857, 865, -1), 12),
        new("shrine_honesty", "Honesty Shrine", new Point3D(4220, 563, 36), 12),
        new("shrine_honor", "Honor Shrine", new Point3D(1732, 3528, 0), 12),
        new("shrine_humility", "Humility Shrine", new Point3D(4264, 3707, 0), 12),
        new("shrine_justice", "Justice Shrine", new Point3D(1300, 644, 8), 12),
        new("shrine_sacrifice", "Sacrifice Shrine", new Point3D(3355, 302, 9), 12),
        new("shrine_spirituality", "Spirituality Shrine", new Point3D(1606, 2490, 5), 12),
        new("shrine_valor", "Valor Shrine", new Point3D(2500, 3931, 3), 12)
    };
    /* END ACHIEVEMENT EXPLORATION */

    /* BEGIN ACHIEVEMENT CREATURE KILLS: concrete class-name groups avoid reflection in the death hot path */
    private static readonly string[] _ratmanCreatureTypes =
    {
        "Ratman",
        "RatmanArcher",
        "RatmanMage"
    };

    private static readonly string[] _lizardmanCreatureTypes =
    {
        "Lizardman"
    };

    private static readonly string[] _reptileCreatureTypes =
    {
        "Lizardman",
        "Drake",
        "Dragon",
        "WhiteWyrm",
        "AncientWyrm",
        "ShadowWyrm",
        "SerpentineDragon",
        "SkeletalDragon",
        "GreaterDragon",
        "Wyvern",
        "SeaSerpent",
        "DeepSeaSerpent",
        "Kraken",
        "Leviathan",
        "OphidianWarrior",
        "OphidianKnight",
        "OphidianMage",
        "OphidianMatriarch",
        "OphidianArchmage",
        "Hydra"
    };

    private static readonly string[] _dragonCreatureTypes =
    {
        "Dragon",
        "WhiteWyrm",
        "AncientWyrm",
        "ShadowWyrm",
        "SerpentineDragon",
        "SkeletalDragon",
        "GreaterDragon"
    };

    private static readonly string[] _drakeCreatureTypes =
    {
        "Drake"
    };

    private static readonly string[] _lichCreatureTypes =
    {
        "Lich"
    };

    private static readonly string[] _lichLordCreatureTypes =
    {
        "LichLord"
    };

    private static readonly string[] _ancientLichCreatureTypes =
    {
        "AncientLich"
    };

    private static readonly string[] _undeadCreatureTypes =
    {
        "Skeleton",
        "SkeletalKnight",
        "SkeletalMage",
        "BoneKnight",
        "BoneMagi",
        "Zombie",
        "Ghoul",
        "Mummy",
        "Shade",
        "Spectre",
        "Wraith",
        "Lich",
        "LichLord",
        "AncientLich",
        "RottingCorpse",
        "SkeletalDragon",
        "Bogle",
        "WailingBanshee",
        "RestlessSoul",
        "KhaldunRevenant",
        "SpectralArmour",
        "VampireBat"
    };

    private static readonly string[] _daemonCreatureTypes =
    {
        "Daemon",
        "Balron",
        "ChaosDaemon",
        "Moloch"
    };

    private static readonly string[] _elementalCreatureTypes =
    {
        "EarthElemental",
        "AirElemental",
        "FireElemental",
        "WaterElemental",
        "PoisonElemental",
        "BloodElemental",
        "SnowElemental",
        "IceElemental",
        "AcidElemental",
        "Efreet"
    };

    private static readonly string[] _orcCreatureTypes =
    {
        "Orc",
        "OrcCaptain",
        "OrcishLord",
        "OrcBomber"
    };

    private static readonly string[] _ogreTrollCreatureTypes =
    {
        "Ogre",
        "OgreLord",
        "ArcticOgreLord",
        "Troll",
        "FrostTroll",
        "Ettin"
    };

    private static readonly string[] _arachnidCreatureTypes =
    {
        "GiantSpider",
        "GiantBlackWidow",
        "FrostSpider",
        "DreadSpider",
        "TerathanDrone",
        "TerathanWarrior",
        "TerathanAvenger",
        "TerathanMatriarch"
    };
    /* END ACHIEVEMENT CREATURE KILLS */

    public static void Configure()
    {
        if (_configured)
        {
            return;
        }

        _configured = true;

        AchievementPersistence.Configure();
        AchievementCommands.Configure();
        EventSink.Movement += OnMovement;
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

    /* BEGIN ACHIEVEMENT FEATURE FLAG: centralize custom feature flag writes for staff controls */
    public static CustomFeatureFlagStatus GetSystemFlagStatus()
    {
        EnsureFlagRegistered();
        return CustomFeatureFlagManager.GetStatus(CustomFeatureFlagKeys.AchievementSystem);
    }

    public static bool TrySetSystemEnabled(bool enabled, Mobile modifiedBy, out string failureReason)
    {
        EnsureFlagRegistered();
        return CustomFeatureFlagManager.SetEnabled(
            CustomFeatureFlagKeys.AchievementSystem,
            enabled,
            modifiedBy?.Name,
            out failureReason
        );
    }

    public static bool TryToggleSystemEnabled(Mobile modifiedBy, out string failureReason)
    {
        EnsureFlagRegistered();
        return CustomFeatureFlagManager.Toggle(
            CustomFeatureFlagKeys.AchievementSystem,
            modifiedBy?.Name,
            out failureReason
        );
    }
    /* END ACHIEVEMENT FEATURE FLAG */

    public static bool AllowStaffServerFirstsForTesting
    {
        get => _allowStaffServerFirstsForTesting;
        set => _allowStaffServerFirstsForTesting = value;
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

        RefreshAccountGrandmasterSkills(player);

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

        /* BEGIN ACHIEVEMENT SERVER FIRSTS: live skill crossing records candidates and claims the current first */
        RecordServerFirstSkillMilestone(player, skill, oldBase, newBase);
        /* END ACHIEVEMENT SERVER FIRSTS */

        /* BEGIN ACHIEVEMENT ACCOUNT SKILL MASTERY: account-wide unique GM skill tracking */
        RecordAccountGrandmasterSkill(player, skill, oldBase, newBase);
        /* END ACHIEVEMENT ACCOUNT SKILL MASTERY */

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
                !MatchesCreatureKillDefinition(definition, creatureTypeName)
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

    public static void RecordTreasureMapCompleted(Mobile from)
    {
        if (from is not PlayerMobile player || !ShouldTrackPlayer(player) || !IsSystemEnabled())
        {
            return;
        }

        foreach (var definition in _definitions.Values)
        {
            if (
                definition.TriggerType != AchievementTriggerType.TreasureMapCompletionCount ||
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
        _serverFirsts.Clear();
        _serverFirstCandidates.Clear();
    }

    /* BEGIN ACHIEVEMENT SYSTEM CUSTOMIZATION: staff pruning removes one achievement from achievement-owned state */
    public static bool TryRemoveAchievement(
        Mobile staff,
        PlayerMobile player,
        string achievementId,
        out AchievementDefinition definition,
        out bool removedUnlock,
        out bool removedProgress,
        out string failureReason
    )
    {
        definition = null;
        removedUnlock = false;
        removedProgress = false;
        failureReason = null;

        if (player == null || string.IsNullOrWhiteSpace(achievementId))
        {
            failureReason = "A player and achievement id are required.";
            return false;
        }

        if (!_definitions.TryGetValue(achievementId, out definition))
        {
            failureReason = $"Unknown achievement id '{achievementId}'.";
            return false;
        }

        if (
            definition.TriggerType == AchievementTriggerType.ServerFirstSkillMilestone &&
            (staff == null || staff.AccessLevel < AccessLevel.Administrator)
        )
        {
            failureReason = "Only administrators can remove server-first achievements.";
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

        if (
            removedUnlock &&
            _serverFirsts.TryGetValue(definition.Id, out var serverFirst) &&
            serverFirst.PlayerSerial == player.Serial.Value
        )
        {
            _serverFirsts.Remove(definition.Id);
            DisqualifyServerFirstCandidate(serverFirst);
            TryPromoteNextServerFirst(definition.Id, out _);
        }

        return true;
    }

    /* BEGIN ACHIEVEMENT ADMIN CONTROLS: staff grant support centralizes manual unlock state changes */
    public static bool TryGrantAchievement(
        Mobile staff,
        PlayerMobile player,
        string achievementId,
        out AchievementDefinition definition,
        out string failureReason
    )
    {
        definition = null;
        failureReason = null;

        if (!IsSystemEnabled())
        {
            failureReason = "Achievement system is disabled.";
            return false;
        }

        if (player == null || string.IsNullOrWhiteSpace(achievementId))
        {
            failureReason = "A player and achievement id are required.";
            return false;
        }

        if (!_definitions.TryGetValue(achievementId, out definition))
        {
            failureReason = $"Unknown achievement id '{achievementId}'.";
            return false;
        }

        if (
            definition.TriggerType == AchievementTriggerType.ServerFirstSkillMilestone &&
            (staff == null || staff.AccessLevel < AccessLevel.Administrator)
        )
        {
            failureReason = "Only administrators can grant server-first achievements.";
            return false;
        }

        if (IsUnlocked(player, definition.Id))
        {
            failureReason = $"{player.Name} already has {definition.Id} ({definition.Name}).";
            return false;
        }

        if (definition.TriggerType == AchievementTriggerType.ServerFirstSkillMilestone)
        {
            if (_serverFirsts.TryGetValue(definition.Id, out var existingRecord))
            {
                failureReason = $"{definition.Id} is already claimed by {existingRecord.PlayerName}.";
                return false;
            }

            var timestamp = DateTime.UtcNow;
            var record = new AchievementServerFirstRecord
            {
                AchievementId = definition.Id,
                PlayerSerial = player.Serial.Value,
                PlayerName = player.Name,
                AccountName = player.Account?.Username ?? string.Empty,
                Skill = definition.Skill,
                SkillDisplayName = GetSkillDisplayName(definition.Skill),
                AchievedUtc = timestamp
            };

            _serverFirsts[definition.Id] = record;
            AddServerFirstCandidate(record, disqualified: false);
            UnlockServerFirstRecord(record, definition);
            TryDisplayNextNotification(player);
            return true;
        }

        var state = GetOrCreateProgressState(player, definition);
        UpdateProgressValue(state, definition.Id, definition.Threshold);
        UnlockAchievement(player, state, definition);

        if (definition.TriggerType == AchievementTriggerType.ExplorationRegionVisit)
        {
            TryUnlockWorldExplorer(player);
        }

        TryDisplayNextNotification(player);
        return true;
    }
    /* END ACHIEVEMENT ADMIN CONTROLS */
    /* END ACHIEVEMENT SYSTEM CUSTOMIZATION */

    /* BEGIN ACHIEVEMENT SERVER FIRSTS: staff controls and read models for shard-wide first records */
    public static bool TryResetServerFirst(string achievementId, out AchievementServerFirstRecord removedRecord)
    {
        return TryResetServerFirst(achievementId, out removedRecord, out _);
    }

    public static bool TryResetServerFirst(
        string achievementId,
        out AchievementServerFirstRecord removedRecord,
        out AchievementServerFirstRecord promotedRecord
    )
    {
        removedRecord = null;
        promotedRecord = null;

        if (string.IsNullOrWhiteSpace(achievementId))
        {
            return false;
        }

        if (!_serverFirsts.TryGetValue(achievementId, out removedRecord))
        {
            return false;
        }

        _serverFirsts.Remove(achievementId);
        DisqualifyServerFirstCandidate(removedRecord);
        ClearServerFirstWinnerUnlock(removedRecord);
        TryPromoteNextServerFirst(achievementId, out promotedRecord);
        return true;
    }

    public static bool TryResetServerFirstSkill(SkillName skill, out AchievementServerFirstRecord removedRecord)
    {
        return TryResetServerFirst(GetServerFirstSkillAchievementId(skill), out removedRecord);
    }

    public static bool TryResetServerFirstSkill(
        SkillName skill,
        out AchievementServerFirstRecord removedRecord,
        out AchievementServerFirstRecord promotedRecord
    )
    {
        return TryResetServerFirst(GetServerFirstSkillAchievementId(skill), out removedRecord, out promotedRecord);
    }

    public static List<AchievementServerFirstRecord> GetServerFirstRecords()
    {
        var records = new List<AchievementServerFirstRecord>(_serverFirsts.Count);

        foreach (var pair in _serverFirsts)
        {
            records.Add(pair.Value);
        }

        records.Sort(static (a, b) => a.AchievedUtc.CompareTo(b.AchievedUtc));
        return records;
    }

    public static AchievementServerFirstRecord GetServerFirstRecord(string achievementId)
    {
        return !string.IsNullOrWhiteSpace(achievementId) && _serverFirsts.TryGetValue(achievementId, out var record)
            ? record
            : null;
    }

    public static void ResetServerFirstsForTesting(
        out int clearedClaims,
        out int clearedCandidateGroups,
        out int clearedPlayerEntries
    )
    {
        clearedClaims = _serverFirsts.Count;
        clearedCandidateGroups = _serverFirstCandidates.Count;
        clearedPlayerEntries = 0;

        _serverFirsts.Clear();
        _serverFirstCandidates.Clear();

        foreach (var pair in _players)
        {
            var state = pair.Value;

            foreach (var definition in _definitions.Values)
            {
                if (definition.TriggerType != AchievementTriggerType.ServerFirstSkillMilestone)
                {
                    continue;
                }

                var removedUnlock = state.UnlockedAchievements.Remove(definition.Id);
                var removedProgress = state.ProgressValues.Remove(definition.Id);

                if (removedUnlock || removedProgress)
                {
                    clearedPlayerEntries++;
                    state.LastUpdatedUtc = DateTime.UtcNow;
                }
            }
        }

        _pendingNotifications.Clear();
        _activeNotifications.Clear();
    }
    /* END ACHIEVEMENT SERVER FIRSTS */

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
            DisplayAchievementGump(player, GetJournalView(active));
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
            AchievementJournalView.CharacterSkills => "Skills",
            AchievementJournalView.CharacterHunting => "Hunting",
            AchievementJournalView.CharacterExploration => "Exploration",
            AchievementJournalView.CharacterHarvesting => "Harvesting",
            AchievementJournalView.CharacterEconomy => "Economy",
            AchievementJournalView.Account => "Account",
            AchievementJournalView.Legacy => "Legacy",
            AchievementJournalView.Feats => "Feats",
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

    public static List<AchievementDefinition> GetJournalDefinitions(PlayerMobile player, AchievementJournalView view)
    {
        var definitions = GetDefinitions(view);

        if (player == null || definitions.Count <= 1)
        {
            return definitions;
        }

        CollapseTierDefinitions(player, definitions);
        return definitions;
    }

    public static List<AchievementDefinition> GetTierMilestones(AchievementDefinition definition)
    {
        var milestones = new List<AchievementDefinition>();

        if (definition == null || string.IsNullOrWhiteSpace(definition.TierGroupId))
        {
            if (definition != null)
            {
                milestones.Add(definition);
            }

            return milestones;
        }

        foreach (var candidate in _definitions.Values)
        {
            if (string.Equals(candidate.TierGroupId, definition.TierGroupId, StringComparison.OrdinalIgnoreCase))
            {
                milestones.Add(candidate);
            }
        }

        milestones.Sort(static (a, b) =>
        {
            var thresholdCompare = a.Threshold.CompareTo(b.Threshold);

            if (thresholdCompare != 0)
            {
                return thresholdCompare;
            }

            return a.SortOrder.CompareTo(b.SortOrder);
        });

        return milestones;
    }

    private static void CollapseTierDefinitions(PlayerMobile player, List<AchievementDefinition> definitions)
    {
        var tierIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];

            if (string.IsNullOrWhiteSpace(definition.TierGroupId))
            {
                continue;
            }

            if (!tierIndexes.TryGetValue(definition.TierGroupId, out var selectedIndex))
            {
                tierIndexes[definition.TierGroupId] = i;
                continue;
            }

            var selected = definitions[selectedIndex];
            var selectedUnlocked = IsUnlocked(player, selected.Id);
            var currentUnlocked = IsUnlocked(player, definition.Id);

            if (selectedUnlocked || currentUnlocked && definition.Threshold > selected.Threshold)
            {
                definitions[selectedIndex] = definition;
            }

            definitions.RemoveAt(i);
            i--;
        }
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

    public static List<AchievementAccountSkillProgressEntry> GetAccountGrandmasterSkillProgress(PlayerMobile player)
    {
        if (!ShouldTrackPlayer(player))
        {
            return new List<AchievementAccountSkillProgressEntry>();
        }

        RefreshAccountGrandmasterSkills(player);

        var state = GetOrCreateAccountState(player);
        var entries = new List<AchievementAccountSkillProgressEntry>(_uorSkills.Length);

        for (var i = 0; i < _uorSkills.Length; i++)
        {
            var skill = _uorSkills[i];
            entries.Add(
                new AchievementAccountSkillProgressEntry
                {
                    Skill = skill,
                    SkillDisplayName = GetSkillDisplayName(skill),
                    Completed = state.ProgressValues.ContainsKey(GetAccountGrandmasterSkillProgressId(skill))
                }
            );
        }

        entries.Sort(static (a, b) => string.Compare(a.SkillDisplayName, b.SkillDisplayName, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    public static void RecordEconomyGoldEarned(PlayerMobile player, AchievementEconomyGoldSource source, int amount)
    {
        if (!ShouldTrackPlayer(player) || amount <= 0 || !IsSystemEnabled())
        {
            return;
        }

        var state = GetOrCreatePlayerState(player);
        var progressId = GetEconomyGoldProgressId(source);
        var total = GetProgressValue(state, progressId) + amount;

        UpdateProgressValue(state, progressId, total);

        foreach (var definition in _definitions.Values)
        {
            if (
                definition.TriggerType != AchievementTriggerType.EconomyGoldEarned ||
                definition.EconomyGoldSource != source ||
                !IsAchievementEarnable(definition)
            )
            {
                continue;
            }

            UpdateProgressValue(state, definition.Id, total);

            if (total >= definition.Threshold)
            {
                UnlockAchievement(player, state, definition);
            }
        }
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

        if (definition.TriggerType == AchievementTriggerType.ServerFirstSkillMilestone)
        {
            return GetServerFirstSkillProgress(player, definition);
        }

        if (definition.TriggerType == AchievementTriggerType.AccountUniqueGrandmasterSkills)
        {
            return CountAccountGrandmasterSkills(GetOrCreateAccountState(player));
        }

        if (definition.TriggerType == AchievementTriggerType.ExplorationRegionVisitAll)
        {
            return CountUnlockedExplorationVisits(player);
        }

        if (definition.TriggerType == AchievementTriggerType.EconomyGoldEarned)
        {
            return GetProgressValue(state, GetEconomyGoldProgressId(definition.EconomyGoldSource));
        }

        return 0;
    }

    public static int GetDisplayedTierProgress(PlayerMobile player, IReadOnlyList<AchievementDefinition> milestones)
    {
        if (player == null || milestones == null || milestones.Count == 0)
        {
            return 0;
        }

        var progress = 0;

        for (var i = 0; i < milestones.Count; i++)
        {
            var milestone = milestones[i];
            var milestoneProgress = GetDisplayedProgress(player, milestone);

            if (IsUnlocked(player, milestone.Id) && milestone.Threshold > milestoneProgress)
            {
                milestoneProgress = milestone.Threshold;
            }

            if (milestoneProgress > progress)
            {
                progress = milestoneProgress;
            }
        }

        return progress;
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

        /* BEGIN ACHIEVEMENT SERVER FIRSTS: persist global shard-first claims after player/account state */
        writer.WriteEncodedInt(_serverFirsts.Count);

        foreach (var pair in _serverFirsts)
        {
            var record = pair.Value;

            writer.Write(pair.Key);
            writer.Write(record.AchievementId);
            writer.Write(record.AchievementName);
            writer.Write(record.PlayerSerial);
            writer.Write(record.PlayerName);
            writer.Write(record.AccountName);
            writer.Write((int)record.Skill);
            writer.Write(record.SkillDisplayName);
            writer.Write(record.AchievedUtc);
        }

        writer.WriteEncodedInt(_serverFirstCandidates.Count);

        foreach (var pair in _serverFirstCandidates)
        {
            writer.Write(pair.Key);
            writer.WriteEncodedInt(pair.Value.Count);

            for (var i = 0; i < pair.Value.Count; i++)
            {
                var candidate = pair.Value[i];

                writer.Write(candidate.AchievementId);
                writer.Write(candidate.AchievementName);
                writer.Write(candidate.PlayerSerial);
                writer.Write(candidate.PlayerName);
                writer.Write(candidate.AccountName);
                writer.Write((int)candidate.Skill);
                writer.Write(candidate.SkillDisplayName);
                writer.Write(candidate.AchievedUtc);
                writer.Write(candidate.Disqualified);
            }
        }
        /* END ACHIEVEMENT SERVER FIRSTS */
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
        _serverFirsts.Clear();
        _serverFirstCandidates.Clear();

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

        if (version <= 1)
        {
            return;
        }

        var serverFirstCount = reader.ReadEncodedInt();

        for (var i = 0; i < serverFirstCount; i++)
        {
            var key = reader.ReadString();
            var record = new AchievementServerFirstRecord
            {
                AchievementId = reader.ReadString(),
                AchievementName = reader.ReadString(),
                PlayerSerial = reader.ReadUInt(),
                PlayerName = reader.ReadString(),
                AccountName = reader.ReadString(),
                Skill = (SkillName)reader.ReadInt(),
                SkillDisplayName = reader.ReadString(),
                AchievedUtc = reader.ReadDateTime()
            };

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(record.AchievementId))
            {
                _serverFirsts[key] = record;
            }
        }

        if (version <= 2)
        {
            foreach (var pair in _serverFirsts)
            {
                AddServerFirstCandidate(pair.Value, disqualified: false);
            }

            return;
        }

        var candidateGroupCount = reader.ReadEncodedInt();

        for (var i = 0; i < candidateGroupCount; i++)
        {
            var achievementId = reader.ReadString();
            var candidateCount = reader.ReadEncodedInt();

            if (string.IsNullOrWhiteSpace(achievementId))
            {
                for (var j = 0; j < candidateCount; j++)
                {
                    ReadServerFirstCandidate(reader);
                }

                continue;
            }

            for (var j = 0; j < candidateCount; j++)
            {
                AddServerFirstCandidate(ReadServerFirstCandidate(reader));
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
                    Id = GetSkillAchievementId(skill),
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

            /* BEGIN ACHIEVEMENT SERVER FIRSTS: companion shard-first GM skill achievements */
            RegisterDefinition(
                new AchievementDefinition
                {
                    Id = GetServerFirstSkillAchievementId(skill),
                    Name = $"Server First: Grandmaster {skillDisplayName}",
                    Description = $"Be the first player to reach 100.0 in {skillDisplayName}.",
                    Category = AchievementCategory.Skills,
                    TriggerType = AchievementTriggerType.ServerFirstSkillMilestone,
                    Scope = AchievementScope.Character,
                    IsLegacy = false,
                    Skill = skill,
                    Threshold = 100,
                    SortOrder = 5000 + i
                }
            );
            /* END ACHIEVEMENT SERVER FIRSTS */
        }

        /* BEGIN ACHIEVEMENT ACCOUNT SKILL MASTERY: account-wide all-GM skill achievement */
        RegisterDefinition(
            new AchievementDefinition
            {
                Id = AccountAllGrandmasterSkillsAchievementId,
                Name = "Mastery Across Many Lives",
                Description = "Reach Grandmaster in every unique skill across your account.",
                Category = AchievementCategory.Skills,
                TriggerType = AchievementTriggerType.AccountUniqueGrandmasterSkills,
                Scope = AchievementScope.Account,
                IsLegacy = false,
                Threshold = _uorSkills.Length,
                SortOrder = 10
            }
        );
        /* END ACHIEVEMENT ACCOUNT SKILL MASTERY */

        RegisterCreatureKillDefinitions();
        RegisterExplorationDefinitions();
        RegisterHarvestDefinitions();
        RegisterFishingDefinitions();
        RegisterEconomyDefinitions();
    }

    /* BEGIN ACHIEVEMENT EXPLORATION: first-visit and treasure-map completion definitions */
    private static void RegisterExplorationDefinitions()
    {
        var sortOrder = 900;

        RegisterExplorationVisitDefinitions(
            _townVisitDefinitions,
            "First Visit: {0}",
            "Visit {0} for the first time.",
            ref sortOrder
        );

        RegisterExplorationVisitDefinitions(
            _dungeonVisitDefinitions,
            "First Descent: {0}",
            "Enter {0} for the first time.",
            ref sortOrder
        );

        RegisterExplorationVisitDefinitions(
            _shrineVisitDefinitions,
            "Pilgrimage: {0}",
            "Visit the {0} for the first time.",
            ref sortOrder
        );

        RegisterDefinition(
            new AchievementDefinition
            {
                Id = WorldExplorerAchievementId,
                Name = "World Explorer",
                Description = "Visit every tracked town, dungeon, and shrine.",
                Category = AchievementCategory.Exploration,
                TriggerType = AchievementTriggerType.ExplorationRegionVisitAll,
                Scope = AchievementScope.Character,
                Threshold = GetExplorationVisitDefinitionCount(),
                SortOrder = sortOrder++
            }
        );

        RegisterTreasureMapCompletionDefinitions(ref sortOrder);
    }

    private static void RegisterExplorationVisitDefinitions(
        IReadOnlyList<ExplorationVisitDefinition> visits,
        string nameFormat,
        string descriptionFormat,
        ref int sortOrder
    )
    {
        for (var i = 0; i < visits.Count; i++)
        {
            var visit = visits[i];

            RegisterDefinition(
                new AchievementDefinition
                {
                    Id = $"visit_{visit.IdSuffix}",
                    Name = string.Format(nameFormat, visit.RegionName),
                    Description = string.Format(descriptionFormat, visit.RegionName),
                    Category = AchievementCategory.Exploration,
                    TriggerType = AchievementTriggerType.ExplorationRegionVisit,
                    Scope = AchievementScope.Character,
                    ExplorationRegionName = visit.RegionName,
                    Threshold = 1,
                    SortOrder = sortOrder++
                }
            );
        }
    }

    private static void RegisterTreasureMapCompletionDefinitions(ref int sortOrder)
    {
        for (var i = 0; i < _treasureMapCompletionTierThresholds.Length; i++)
        {
            var threshold = _treasureMapCompletionTierThresholds[i];

            RegisterDefinition(
                new AchievementDefinition
                {
                    Id = $"complete_treasure_maps_{threshold}",
                    Name = $"Treasure Trail {RomanizeTier(i + 1)}",
                    Description = $"Complete {threshold:N0} treasure map{(threshold == 1 ? string.Empty : "s")}.",
                    Category = AchievementCategory.Exploration,
                    TriggerType = AchievementTriggerType.TreasureMapCompletionCount,
                    Scope = AchievementScope.Character,
                    TierGroupId = "complete_treasure_maps",
                    Threshold = threshold,
                    SortOrder = sortOrder++
                }
            );
        }
    }
    /* END ACHIEVEMENT EXPLORATION */

    /* BEGIN ACHIEVEMENT CREATURE KILLS: character kill tier definitions for major monster families */
    private static void RegisterCreatureKillDefinitions()
    {
        var sortOrder = 800;

        RegisterCreatureKillTierDefinitions(
            "kill_ratmen",
            "Rat Catcher",
            "Defeat {0:N0} ratmen.",
            _ratmanCreatureTypes,
            _creatureFamilyKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_lizardmen",
            "Scale Breaker",
            "Defeat {0:N0} lizardmen.",
            _lizardmanCreatureTypes,
            _creatureFamilyKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_reptiles",
            "Reptile Hunter",
            "Defeat {0:N0} reptilian monsters.",
            _reptileCreatureTypes,
            _creatureFamilyKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_undead",
            "Grave Warden",
            "Defeat {0:N0} undead.",
            _undeadCreatureTypes,
            _creatureFamilyKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_dragons",
            "Dragon Slayer",
            "Defeat {0:N0} dragons or wyrms.",
            _dragonCreatureTypes,
            _creatureSpecificKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_drakes",
            "Drake Hunter",
            "Defeat {0:N0} drakes.",
            _drakeCreatureTypes,
            _creatureSpecificKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_liches",
            "Lich Hunter",
            "Defeat {0:N0} liches.",
            _lichCreatureTypes,
            _creatureSpecificKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_lich_lords",
            "Lord of Dust",
            "Defeat {0:N0} lich lords.",
            _lichLordCreatureTypes,
            _creatureSpecificKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_ancient_liches",
            "Ancient Silence",
            "Defeat {0:N0} ancient liches.",
            _ancientLichCreatureTypes,
            _creatureEliteKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_daemons",
            "Daemon Bane",
            "Defeat {0:N0} daemons.",
            _daemonCreatureTypes,
            _creatureSpecificKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_elementals",
            "Elemental Breaker",
            "Defeat {0:N0} elementals.",
            _elementalCreatureTypes,
            _creatureFamilyKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_orcs",
            "Orc Foe",
            "Defeat {0:N0} orcs.",
            _orcCreatureTypes,
            _creatureFamilyKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_ogres_trolls",
            "Brute Breaker",
            "Defeat {0:N0} ogres, trolls, or ettins.",
            _ogreTrollCreatureTypes,
            _creatureFamilyKillTierThresholds,
            ref sortOrder
        );

        RegisterCreatureKillTierDefinitions(
            "kill_arachnids",
            "Web Cutter",
            "Defeat {0:N0} spiders or terathans.",
            _arachnidCreatureTypes,
            _creatureFamilyKillTierThresholds,
            ref sortOrder
        );
    }

    private static void RegisterCreatureKillTierDefinitions(
        string idPrefix,
        string namePrefix,
        string descriptionFormat,
        string[] creatureTypeNames,
        IReadOnlyList<int> thresholds,
        ref int sortOrder
    )
    {
        for (var i = 0; i < thresholds.Count; i++)
        {
            var threshold = thresholds[i];

            RegisterDefinition(
                new AchievementDefinition
                {
                    Id = $"{idPrefix}_{threshold}",
                    Name = $"{namePrefix} {RomanizeTier(i + 1)}",
                    Description = string.Format(descriptionFormat, threshold),
                    Category = AchievementCategory.Hunting,
                    TriggerType = AchievementTriggerType.CreatureKillCount,
                    Scope = AchievementScope.Character,
                    TierGroupId = idPrefix,
                    CreatureTypeNames = creatureTypeNames,
                    Threshold = threshold,
                    SortOrder = sortOrder++
                }
            );
        }
    }
    /* END ACHIEVEMENT CREATURE KILLS */

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
                        TierGroupId = $"mine_{ore.Resource.ToString().ToLowerInvariant()}",
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
                    TierGroupId = "chop_regular_logs",
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
                    TierGroupId = "catch_normal_fish",
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

    /* BEGIN ACHIEVEMENT ECONOMY: character gold-earned tier definitions */
    private static void RegisterEconomyDefinitions()
    {
        var sortOrder = 1200;

        RegisterEconomyGoldDefinitions(
            AchievementEconomyGoldSource.MonsterLoot,
            "Monster Spoils",
            "Loot {0:N0} gold from monster corpses.",
            _monsterGoldTierThresholds,
            ref sortOrder
        );

        RegisterEconomyGoldDefinitions(
            AchievementEconomyGoldSource.TreasureMapChest,
            "Cartographer's Cut",
            "Loot {0:N0} gold from treasure map chests.",
            _treasureMapGoldTierThresholds,
            ref sortOrder
        );

        RegisterEconomyGoldDefinitions(
            AchievementEconomyGoldSource.DungeonChest,
            "Dungeon Cache",
            "Loot {0:N0} gold from dungeon treasure chests.",
            _dungeonChestGoldTierThresholds,
            ref sortOrder
        );

        RegisterEconomyGoldDefinitions(
            AchievementEconomyGoldSource.VendorSale,
            "Market Returns",
            "Earn {0:N0} gold by selling items to NPC vendors.",
            _vendorSaleGoldTierThresholds,
            ref sortOrder
        );
    }

    private static void RegisterEconomyGoldDefinitions(
        AchievementEconomyGoldSource source,
        string namePrefix,
        string descriptionFormat,
        IReadOnlyList<int> thresholds,
        ref int sortOrder
    )
    {
        for (var i = 0; i < thresholds.Count; i++)
        {
            var threshold = thresholds[i];

            RegisterDefinition(
                new AchievementDefinition
                {
                    Id = GetEconomyGoldAchievementId(source, threshold),
                    Name = $"{namePrefix} {RomanizeTier(i + 1)}",
                    Description = string.Format(descriptionFormat, threshold),
                    Category = AchievementCategory.Economy,
                    TriggerType = AchievementTriggerType.EconomyGoldEarned,
                    Scope = AchievementScope.Character,
                    TierGroupId = GetEconomyGoldProgressId(source),
                    EconomyGoldSource = source,
                    Threshold = threshold,
                    SortOrder = sortOrder++
                }
            );
        }
    }
    /* END ACHIEVEMENT ECONOMY */

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
            AchievementTriggerType.ServerFirstSkillMilestone => GetServerFirstSkillProgress(player, definition),
            AchievementTriggerType.AccountUniqueGrandmasterSkills => CountAccountGrandmasterSkills(GetOrCreateAccountState(player)),
            AchievementTriggerType.EconomyGoldEarned => GetProgressValue(state, GetEconomyGoldProgressId(definition.EconomyGoldSource)),
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

    /* BEGIN ACHIEVEMENT SERVER FIRSTS: claim, unlock, and announce shard-first achievements */
    private static int GetServerFirstSkillProgress(PlayerMobile player, AchievementDefinition definition)
    {
        if (HasServerFirstClaim(definition.Id))
        {
            return IsServerFirstWinner(player, definition.Id) ? definition.Threshold : 0;
        }

        return Math.Min((int)player.Skills[definition.Skill].Base, definition.Threshold - 1);
    }

    private static void RecordServerFirstSkillMilestone(PlayerMobile player, SkillName skill, double oldBase, double newBase)
    {
        if (!IsServerFirstEligiblePlayer(player) || oldBase >= 100.0 || newBase < 100.0)
        {
            return;
        }

        var achievementId = GetServerFirstSkillAchievementId(skill);

        if (!_definitions.TryGetValue(achievementId, out var definition))
        {
            return;
        }

        var timestamp = DateTime.UtcNow;
        var candidate = new AchievementServerFirstCandidateRecord
        {
            AchievementId = definition.Id,
            AchievementName = definition.Name,
            PlayerSerial = player.Serial.Value,
            PlayerName = player.Name,
            AccountName = player.Account?.Username ?? string.Empty,
            Skill = skill,
            SkillDisplayName = GetSkillDisplayName(skill),
            AchievedUtc = timestamp
        };

        AddServerFirstCandidate(candidate);

        if (!HasServerFirstClaim(achievementId))
        {
            TryPromoteNextServerFirst(achievementId, out _);
        }
    }

    private static bool TryPromoteNextServerFirst(string achievementId, out AchievementServerFirstRecord promotedRecord)
    {
        promotedRecord = null;

        if (string.IsNullOrWhiteSpace(achievementId) || HasServerFirstClaim(achievementId))
        {
            return false;
        }

        if (!_definitions.TryGetValue(achievementId, out var definition))
        {
            return false;
        }

        SeedServerFirstCandidatesFromAchievementState(definition);

        if (!_serverFirstCandidates.TryGetValue(achievementId, out var candidates) || candidates.Count == 0)
        {
            return false;
        }

        candidates.Sort(static (a, b) =>
        {
            var timeCompare = a.AchievedUtc.CompareTo(b.AchievedUtc);
            return timeCompare != 0 ? timeCompare : a.PlayerSerial.CompareTo(b.PlayerSerial);
        });

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];

            if (candidate.Disqualified || !IsServerFirstEligibleCandidate(candidate))
            {
                continue;
            }

            promotedRecord = new AchievementServerFirstRecord
            {
                AchievementId = candidate.AchievementId,
                AchievementName = candidate.AchievementName,
                PlayerSerial = candidate.PlayerSerial,
                PlayerName = candidate.PlayerName,
                AccountName = candidate.AccountName,
                Skill = candidate.Skill,
                SkillDisplayName = candidate.SkillDisplayName,
                AchievedUtc = candidate.AchievedUtc
            };

            _serverFirsts[achievementId] = promotedRecord;
            UnlockServerFirstRecord(promotedRecord, definition);
            AnnounceServerFirst(promotedRecord);
            return true;
        }

        return false;
    }

    private static void UnlockServerFirstRecord(AchievementServerFirstRecord record, AchievementDefinition definition)
    {
        if (!_players.TryGetValue(record.PlayerSerial, out var state))
        {
            state = new AchievementPlayerState
            {
                PlayerSerial = record.PlayerSerial,
                PlayerName = record.PlayerName,
                AccountName = record.AccountName,
                FirstSeenUtc = record.AchievedUtc,
                LastUpdatedUtc = record.AchievedUtc
            };

            _players[record.PlayerSerial] = state;
        }

        state.PlayerName = record.PlayerName;
        state.AccountName = record.AccountName;
        UpdateProgressValue(state, definition.Id, definition.Threshold);

        if (state.UnlockedAchievements.ContainsKey(definition.Id))
        {
            return;
        }

        state.UnlockedAchievements[definition.Id] = new AchievementUnlockRecord { UnlockedUtc = record.AchievedUtc };
        state.LastUpdatedUtc = DateTime.UtcNow;

        if (World.FindMobile((Serial)record.PlayerSerial) is PlayerMobile player)
        {
            QueueUnlockNotification(player, definition);
            PlayUnlockEffects(player);
        }

        Logger.Information(
            "[AchievementSystem] {PlayerName}/{AccountName} unlocked {AchievementId} at {TimestampUtc:O}.",
            record.PlayerName,
            record.AccountName,
            definition.Id,
            record.AchievedUtc
        );
    }

    private static void AddServerFirstCandidate(AchievementServerFirstRecord record, bool disqualified)
    {
        if (record == null)
        {
            return;
        }

        AddServerFirstCandidate(
            new AchievementServerFirstCandidateRecord
            {
                AchievementId = record.AchievementId,
                AchievementName = record.AchievementName,
                PlayerSerial = record.PlayerSerial,
                PlayerName = record.PlayerName,
                AccountName = record.AccountName,
                Skill = record.Skill,
                SkillDisplayName = record.SkillDisplayName,
                AchievedUtc = record.AchievedUtc,
                Disqualified = disqualified
            }
        );
    }

    private static void AddServerFirstCandidate(AchievementServerFirstCandidateRecord candidate)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(candidate.AchievementId))
        {
            return;
        }

        if (!_serverFirstCandidates.TryGetValue(candidate.AchievementId, out var candidates))
        {
            candidates = new List<AchievementServerFirstCandidateRecord>();
            _serverFirstCandidates[candidate.AchievementId] = candidates;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].PlayerSerial != candidate.PlayerSerial)
            {
                continue;
            }

            candidates[i].PlayerName = candidate.PlayerName;
            candidates[i].AccountName = candidate.AccountName;
            candidates[i].SkillDisplayName = candidate.SkillDisplayName;
            candidates[i].AchievedUtc = candidates[i].AchievedUtc <= candidate.AchievedUtc
                ? candidates[i].AchievedUtc
                : candidate.AchievedUtc;
            return;
        }

        candidates.Add(candidate);
    }

    private static void DisqualifyServerFirstCandidate(AchievementServerFirstRecord record)
    {
        if (record == null)
        {
            return;
        }

        AddServerFirstCandidate(record, disqualified: true);

        if (!_serverFirstCandidates.TryGetValue(record.AchievementId, out var candidates))
        {
            return;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].PlayerSerial == record.PlayerSerial)
            {
                candidates[i].Disqualified = true;
                return;
            }
        }
    }

    private static void SeedServerFirstCandidatesFromAchievementState(AchievementDefinition definition)
    {
        var normalSkillAchievementId = GetSkillAchievementId(definition.Skill);

        foreach (var pair in _players)
        {
            var state = pair.Value;

            if (!state.UnlockedAchievements.TryGetValue(normalSkillAchievementId, out var unlockRecord))
            {
                continue;
            }

            if (
                World.FindMobile((Serial)state.PlayerSerial) is PlayerMobile player &&
                !IsServerFirstEligiblePlayer(player)
            )
            {
                continue;
            }

            AddServerFirstCandidate(
                new AchievementServerFirstCandidateRecord
                {
                    AchievementId = definition.Id,
                    AchievementName = definition.Name,
                    PlayerSerial = state.PlayerSerial,
                    PlayerName = state.PlayerName,
                    AccountName = state.AccountName,
                    Skill = definition.Skill,
                    SkillDisplayName = GetSkillDisplayName(definition.Skill),
                    AchievedUtc = unlockRecord.UnlockedUtc
                }
            );
        }
    }

    private static AchievementServerFirstCandidateRecord ReadServerFirstCandidate(IGenericReader reader)
    {
        return new AchievementServerFirstCandidateRecord
        {
            AchievementId = reader.ReadString(),
            AchievementName = reader.ReadString(),
            PlayerSerial = reader.ReadUInt(),
            PlayerName = reader.ReadString(),
            AccountName = reader.ReadString(),
            Skill = (SkillName)reader.ReadInt(),
            SkillDisplayName = reader.ReadString(),
            AchievedUtc = reader.ReadDateTime(),
            Disqualified = reader.ReadBool()
        };
    }

    private static void AnnounceServerFirst(AchievementServerFirstRecord record)
    {
        World.Broadcast(
            0x35,
            true,
            $"{record.PlayerName} is the first to reach Grandmaster in {record.SkillDisplayName}!"
        );

        foreach (var state in NetState.Instances)
        {
            if (state?.Mobile is PlayerMobile player && player.NetState != null)
            {
                AchievementServerFirstGump.DisplayTo(player, record);
            }
        }
    }

    private static bool HasServerFirstClaim(string achievementId)
    {
        return !string.IsNullOrWhiteSpace(achievementId) && _serverFirsts.ContainsKey(achievementId);
    }

    private static bool IsServerFirstWinner(PlayerMobile player, string achievementId)
    {
        return player != null &&
            !string.IsNullOrWhiteSpace(achievementId) &&
            _serverFirsts.TryGetValue(achievementId, out var record) &&
            record.PlayerSerial == player.Serial.Value;
    }

    private static void ClearServerFirstWinnerUnlock(AchievementServerFirstRecord record)
    {
        if (
            record == null ||
            string.IsNullOrWhiteSpace(record.AchievementId) ||
            World.FindMobile((Serial)record.PlayerSerial) is not PlayerMobile player
        )
        {
            return;
        }

        var state = GetOrCreatePlayerState(player);

        if (state.UnlockedAchievements.Remove(record.AchievementId) | state.ProgressValues.Remove(record.AchievementId))
        {
            state.LastUpdatedUtc = DateTime.UtcNow;
        }

        ClearQueuedNotification(player, record.AchievementId);
        player.CloseGump<AchievementUnlockGump>();
        player.CloseGump<AchievementGump>();
    }

    private static string GetServerFirstSkillAchievementId(SkillName skill)
    {
        return $"server_first_gm_{skill.ToString().ToLowerInvariant()}";
    }

    private static string GetSkillAchievementId(SkillName skill)
    {
        return $"gm_{skill.ToString().ToLowerInvariant()}";
    }
    /* END ACHIEVEMENT SERVER FIRSTS */

    /* BEGIN ACHIEVEMENT ACCOUNT SKILL MASTERY: maintain unique GM skill markers per account */
    private static void RefreshAccountGrandmasterSkills(PlayerMobile player)
    {
        if (!ShouldTrackPlayer(player))
        {
            return;
        }

        for (var i = 0; i < _uorSkills.Length; i++)
        {
            var skill = _uorSkills[i];

            if (player.Skills[skill].Base >= 100.0)
            {
                RecordAccountGrandmasterSkill(player, skill, 99.9, player.Skills[skill].Base);
            }
        }
    }

    private static void RecordAccountGrandmasterSkill(PlayerMobile player, SkillName skill, double oldBase, double newBase)
    {
        if (oldBase >= 100.0 || newBase < 100.0)
        {
            return;
        }

        if (!_definitions.TryGetValue(AccountAllGrandmasterSkillsAchievementId, out var definition))
        {
            return;
        }

        var state = GetOrCreateAccountState(player);
        var markerId = GetAccountGrandmasterSkillProgressId(skill);

        if (!state.ProgressValues.ContainsKey(markerId))
        {
            state.ProgressValues[markerId] = 1;
            state.LastUpdatedUtc = DateTime.UtcNow;
        }

        var progress = CountAccountGrandmasterSkills(state);
        UpdateProgressValue(state, definition.Id, progress);

        if (progress >= definition.Threshold)
        {
            UnlockAchievement(player, state, definition);
        }
    }

    private static int CountAccountGrandmasterSkills(AchievementAccountState state)
    {
        if (state == null)
        {
            return 0;
        }

        var count = 0;

        for (var i = 0; i < _uorSkills.Length; i++)
        {
            if (state.ProgressValues.ContainsKey(GetAccountGrandmasterSkillProgressId(_uorSkills[i])))
            {
                count++;
            }
        }

        return count;
    }

    private static string GetAccountGrandmasterSkillProgressId(SkillName skill)
    {
        return $"{AccountGrandmasterSkillProgressPrefix}{skill.ToString().ToLowerInvariant()}";
    }
    /* END ACHIEVEMENT ACCOUNT SKILL MASTERY */

    /* BEGIN ACHIEVEMENT ECONOMY: shared progress keys for source totals */
    private static string GetEconomyGoldAchievementId(AchievementEconomyGoldSource source, int threshold)
    {
        return $"economy_{source.ToString().ToLowerInvariant()}_{threshold}";
    }

    private static string GetEconomyGoldProgressId(AchievementEconomyGoldSource source)
    {
        return $"economy_{source.ToString().ToLowerInvariant()}_total";
    }
    /* END ACHIEVEMENT ECONOMY */

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

    private static bool IsServerFirstEligiblePlayer(PlayerMobile player)
    {
        if (!ShouldTrackPlayer(player))
        {
            return false;
        }

        if (_allowStaffServerFirstsForTesting)
        {
            return true;
        }

        return player.AccessLevel == AccessLevel.Player && GetAccountAccessLevel(player) == AccessLevel.Player;
    }

    private static bool IsServerFirstEligibleCandidate(AchievementServerFirstCandidateRecord candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (_allowStaffServerFirstsForTesting)
        {
            return true;
        }

        if (World.FindMobile((Serial)candidate.PlayerSerial) is PlayerMobile player)
        {
            return IsServerFirstEligiblePlayer(player);
        }

        var account = Accounts.GetAccount(candidate.AccountName);
        return account == null || account.AccessLevel == AccessLevel.Player;
    }

    private static AccessLevel GetAccountAccessLevel(PlayerMobile player)
    {
        return player?.Account?.AccessLevel ?? AccessLevel.Player;
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
                !definition.IsLegacy &&
                definition.TriggerType != AchievementTriggerType.ServerFirstSkillMilestone,
            AchievementJournalView.CharacterHunting =>
                definition.Scope == AchievementScope.Character &&
                definition.Category == AchievementCategory.Hunting &&
                !definition.IsLegacy,
            AchievementJournalView.CharacterExploration =>
                definition.Scope == AchievementScope.Character &&
                definition.Category == AchievementCategory.Exploration &&
                !definition.IsLegacy,
            AchievementJournalView.CharacterHarvesting =>
                definition.Scope == AchievementScope.Character &&
                definition.Category == AchievementCategory.Harvesting &&
                !definition.IsLegacy,
            AchievementJournalView.CharacterEconomy =>
                definition.Scope == AchievementScope.Character &&
                definition.Category == AchievementCategory.Economy &&
                !definition.IsLegacy,
            AchievementJournalView.Account => definition.Scope == AchievementScope.Account && !definition.IsLegacy,
            AchievementJournalView.Legacy => definition.IsLegacy,
            AchievementJournalView.Feats => definition.IsLegacy ||
                definition.TriggerType == AchievementTriggerType.ServerFirstSkillMilestone,
            _ => false
        };
    }

    /* BEGIN ACHIEVEMENT CREATURE KILLS: match exact configured creature class names without runtime type hierarchy scans */
    private static bool MatchesCreatureKillDefinition(AchievementDefinition definition, string creatureTypeName)
    {
        if (definition == null || string.IsNullOrWhiteSpace(creatureTypeName))
        {
            return false;
        }

        if (string.Equals(definition.CreatureTypeName, creatureTypeName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var creatureTypeNames = definition.CreatureTypeNames;

        if (creatureTypeNames == null)
        {
            return false;
        }

        for (var i = 0; i < creatureTypeNames.Length; i++)
        {
            if (string.Equals(creatureTypeNames[i], creatureTypeName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    /* END ACHIEVEMENT CREATURE KILLS */

    /* BEGIN ACHIEVEMENT EXPLORATION: movement hook records first visits to configured major regions */
    private static void OnMovement(MovementEventArgs args)
    {
        if (args.Mobile is not PlayerMobile player || !ShouldTrackPlayer(player) || !IsSystemEnabled())
        {
            return;
        }

        if (!TryGetExplorationRegion(player, out var regionName))
        {
            return;
        }

        var serial = player.Serial.Value;

        if (
            _lastExplorationRegion.TryGetValue(serial, out var lastRegion) &&
            string.Equals(lastRegion, regionName, StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        _lastExplorationRegion[serial] = regionName;
        RecordExplorationRegionVisit(player, regionName);
    }

    private static bool TryGetExplorationRegion(PlayerMobile player, out string regionName)
    {
        regionName = null;

        if (player == null)
        {
            return false;
        }

        return TryGetExplorationRegion(player.Region, _townVisitDefinitions, out regionName) ||
            TryGetExplorationRegion(player.Region, _dungeonVisitDefinitions, out regionName) ||
            TryGetShrineExplorationRegion(player, out regionName);
    }

    private static bool TryGetExplorationRegion(
        Region region,
        IReadOnlyList<ExplorationVisitDefinition> visits,
        out string regionName
    )
    {
        regionName = null;

        if (region == null)
        {
            return false;
        }

        for (var i = 0; i < visits.Count; i++)
        {
            var visit = visits[i];

            if (region.IsPartOf(visit.RegionName))
            {
                regionName = visit.RegionName;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetShrineExplorationRegion(PlayerMobile player, out string regionName)
    {
        regionName = null;

        if (player?.Map != Map.Felucca && player?.Map != Map.Trammel)
        {
            return false;
        }

        for (var i = 0; i < _shrineVisitDefinitions.Length; i++)
        {
            var visit = _shrineVisitDefinitions[i];

            if (visit.Location != Point3D.Zero && player.InRange(visit.Location, visit.Range))
            {
                regionName = visit.RegionName;
                return true;
            }
        }

        return false;
    }

    private static void RecordExplorationRegionVisit(PlayerMobile player, string regionName)
    {
        if (player == null || string.IsNullOrWhiteSpace(regionName))
        {
            return;
        }

        foreach (var definition in _definitions.Values)
        {
            if (
                definition.TriggerType != AchievementTriggerType.ExplorationRegionVisit ||
                !string.Equals(definition.ExplorationRegionName, regionName, StringComparison.OrdinalIgnoreCase) ||
                !IsAchievementEarnable(definition)
            )
            {
                continue;
            }

            var state = GetOrCreateProgressState(player, definition);

            if (state.UnlockedAchievements.ContainsKey(definition.Id))
            {
                return;
            }

            UpdateProgressValue(state, definition.Id, definition.Threshold);
            UnlockAchievement(player, state, definition);
            TryUnlockWorldExplorer(player);
            return;
        }
    }

    private static void TryUnlockWorldExplorer(PlayerMobile player)
    {
        if (player == null || !_definitions.TryGetValue(WorldExplorerAchievementId, out var definition))
        {
            return;
        }

        var state = GetOrCreateProgressState(player, definition);

        if (state.UnlockedAchievements.ContainsKey(definition.Id))
        {
            return;
        }

        var progress = CountUnlockedExplorationVisits(player);
        UpdateProgressValue(state, definition.Id, progress);

        if (progress >= definition.Threshold)
        {
            UnlockAchievement(player, state, definition);
        }
    }

    private static int CountUnlockedExplorationVisits(PlayerMobile player)
    {
        if (player == null)
        {
            return 0;
        }

        var count = 0;

        foreach (var definition in _definitions.Values)
        {
            if (definition.TriggerType != AchievementTriggerType.ExplorationRegionVisit)
            {
                continue;
            }

            if (IsUnlocked(player, definition.Id))
            {
                count++;
            }
        }

        return count;
    }

    private static int GetExplorationVisitDefinitionCount()
    {
        return _townVisitDefinitions.Length + _dungeonVisitDefinitions.Length + _shrineVisitDefinitions.Length;
    }
    /* END ACHIEVEMENT EXPLORATION */

    private static AchievementJournalView GetJournalView(AchievementScope scope, AchievementCategory category, bool isLegacy)
    {
        if (isLegacy)
        {
            return AchievementJournalView.Feats;
        }

        if (scope == AchievementScope.Account)
        {
            return AchievementJournalView.Account;
        }

        return category switch
        {
            AchievementCategory.Hunting => AchievementJournalView.CharacterHunting,
            AchievementCategory.Exploration => AchievementJournalView.CharacterExploration,
            AchievementCategory.Harvesting => AchievementJournalView.CharacterHarvesting,
            AchievementCategory.Economy => AchievementJournalView.CharacterEconomy,
            _ => AchievementJournalView.CharacterSkills
        };
    }

    private static AchievementJournalView GetJournalView(AchievementNotificationRecord notification)
    {
        if (notification == null)
        {
            return AchievementJournalView.Overview;
        }

        var definition = GetDefinition(notification?.AchievementId);

        if (definition?.TriggerType == AchievementTriggerType.ServerFirstSkillMilestone)
        {
            return AchievementJournalView.Feats;
        }

        return GetJournalView(notification.Scope, notification.Category, notification.IsLegacy);
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
            6 => "VI",
            7 => "VII",
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
    CharacterHunting,
    CharacterExploration,
    CharacterHarvesting,
    CharacterEconomy,
    Account,
    Legacy,
    Feats
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
    ServerFirstSkillMilestone,
    AccountUniqueGrandmasterSkills,
    EconomyGoldEarned,
    ExplorationRegionVisit,
    ExplorationRegionVisitAll,
    TreasureMapCompletionCount,
    CreatureKillCount,
    HarvestResourceCount,
    FishingCatchCount
}

public enum AchievementEconomyGoldSource
{
    MonsterLoot,
    TreasureMapChest,
    DungeonChest,
    VendorSale
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
    public string TierGroupId { get; set; }
    public DateTime? EarnableFromUtc { get; set; }
    public DateTime? EarnableUntilUtc { get; set; }
    public AchievementHarvestKind HarvestKind { get; set; }
    public AchievementFishingCatchKind FishingCatchKind { get; set; }
    public AchievementEconomyGoldSource EconomyGoldSource { get; set; }
    public CraftResource Resource { get; set; }
    public SkillName Skill { get; set; }
    public string ExplorationRegionName { get; set; }
    public string CreatureTypeName { get; set; }
    public string[] CreatureTypeNames { get; set; }
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

public sealed class AchievementAccountSkillProgressEntry
{
    public SkillName Skill { get; set; }
    public string SkillDisplayName { get; set; }
    public bool Completed { get; set; }
}

public sealed class AchievementServerFirstRecord
{
    public string AchievementId { get; set; }
    public string AchievementName { get; set; }
    public uint PlayerSerial { get; set; }
    public string PlayerName { get; set; }
    public string AccountName { get; set; }
    public SkillName Skill { get; set; }
    public string SkillDisplayName { get; set; }
    public DateTime AchievedUtc { get; set; }
}

public sealed class AchievementServerFirstCandidateRecord
{
    public string AchievementId { get; set; }
    public string AchievementName { get; set; }
    public uint PlayerSerial { get; set; }
    public string PlayerName { get; set; }
    public string AccountName { get; set; }
    public SkillName Skill { get; set; }
    public string SkillDisplayName { get; set; }
    public DateTime AchievedUtc { get; set; }
    public bool Disqualified { get; set; }
}

public readonly record struct ExplorationVisitDefinition(
    string IdSuffix,
    string RegionName,
    Point3D Location = default,
    int Range = 0
);
