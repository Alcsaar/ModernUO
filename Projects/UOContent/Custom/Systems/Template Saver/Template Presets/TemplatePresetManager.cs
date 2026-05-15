using System;
using System.Collections.Generic;
using System.IO;
using Server.Json;

namespace Server.Custom.Systems.TemplateSaver;

public static class TemplatePresetManager
{
    public static readonly string[] TierOrder =
    {
        "Apprentice",
        "Journeyman",
        "Expert",
        "Adept",
        "Master",
        "Grandmaster"
    };

    private static readonly string SavePath =
        Path.Combine(Core.BaseDirectory, "Configuration", "TemplateSaver", "template-presets.json");

    private static TemplatePresetLibraryState _state;
    private static bool _initialized;

    public static void Configure()
    {
        EnsureInitialized();
    }

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _state = JsonConfig.Deserialize<TemplatePresetLibraryState>(SavePath) ?? new TemplatePresetLibraryState();

        if (_state.Presets == null)
        {
            _state.Presets = new List<TemplatePresetDefinition>();
        }

        var fileExists = File.Exists(SavePath);

        if (_state.Presets.Count == 0 && !fileExists)
        {
            SeedDefaults();
            NormalizeLoadedState();

            _initialized = true;
            Save();
            return;
        }

        NormalizeLoadedState();
        _initialized = true;
    }

    public static bool CanUse(Mobile from, out string message)
    {
        EnsureInitialized();

        if (from == null || from.Deleted)
        {
            message = "Invalid user.";
            return false;
        }

        if (from.AccessLevel <= AccessLevel.Player)
        {
            message = "You do not have access to template presets.";
            return false;
        }

        message = null;
        return true;
    }

    public static bool CanModify(Mobile from, out string message)
    {
        if (!CanUse(from, out message))
        {
            return false;
        }

        if (from.AccessLevel < AccessLevel.Owner)
        {
            message = "Only the Owner may modify template presets.";
            return false;
        }

        message = null;
        return true;
    }

    public static IReadOnlyList<TemplatePresetDefinition> GetPresets()
    {
        EnsureInitialized();
        return _state.Presets;
    }

    public static List<TemplatePresetTierDefinition> GetAllPresetTiers()
    {
        EnsureInitialized();

        var list = new List<TemplatePresetTierDefinition>();

        for (var i = 0; i < _state.Presets.Count; i++)
        {
            var preset = _state.Presets[i];
            if (preset?.Tiers == null)
            {
                continue;
            }

            for (var j = 0; j < preset.Tiers.Count; j++)
            {
                var tier = preset.Tiers[j];
                if (tier != null)
                {
                    list.Add(tier);
                }
            }
        }

        list.Sort(ComparePresetTiers);
        return list;
    }

    public static TemplatePresetTierDefinition FindTier(Guid id)
    {
        EnsureInitialized();

        for (var i = 0; i < _state.Presets.Count; i++)
        {
            var preset = _state.Presets[i];
            if (preset?.Tiers == null)
            {
                continue;
            }

            for (var j = 0; j < preset.Tiers.Count; j++)
            {
                var tier = preset.Tiers[j];
                if (tier != null && tier.Id == id)
                {
                    return tier;
                }
            }
        }

        return null;
    }

    public static TemplatePresetTierDefinition FindTier(string presetName, string tierName)
    {
        EnsureInitialized();

        var normalizedPresetName = NormalizePresetName(presetName);
        var normalizedTierName = NormalizeTierName(tierName);

        if (string.IsNullOrWhiteSpace(normalizedPresetName) || string.IsNullOrWhiteSpace(normalizedTierName))
        {
            return null;
        }

        for (var i = 0; i < _state.Presets.Count; i++)
        {
            var preset = _state.Presets[i];
            if (preset == null || !string.Equals(preset.Name, normalizedPresetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (var j = 0; j < preset.Tiers.Count; j++)
            {
                var tier = preset.Tiers[j];
                if (tier != null && string.Equals(tier.Tier, normalizedTierName, StringComparison.OrdinalIgnoreCase))
                {
                    return tier;
                }
            }

            return null;
        }

        return null;
    }

    public static bool LoadPresetOntoSelf(Mobile from, Guid presetTierId, out string message)
    {
        EnsureInitialized();

        if (!CanUse(from, out message))
        {
            return false;
        }

        var tier = FindTier(presetTierId);
        if (tier == null)
        {
            message = "That preset tier could not be found.";
            return false;
        }

        ApplyPresetToMobile(from, tier);
        message = $"Preset '{tier.PresetName} [{tier.Tier}]' loaded onto {from.Name}.";
        return true;
    }

    public static bool LoadPresetOntoSelf(Mobile from, string presetName, string tierName, out string message)
    {
        var tier = FindTier(presetName, tierName);
        if (tier == null)
        {
            message = "That preset tier could not be found.";
            return false;
        }

        return LoadPresetOntoSelf(from, tier.Id, out message);
    }

    public static bool CreateOrUpdateFromSelf(Mobile from, string presetName, string tierName, out string message)
    {
        EnsureInitialized();

        if (!CanModify(from, out message))
        {
            return false;
        }

        var normalizedPresetName = NormalizePresetName(presetName);
        var normalizedTierName = NormalizeTierName(tierName);

        if (string.IsNullOrWhiteSpace(normalizedPresetName))
        {
            message = "You must provide a preset name.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedTierName))
        {
            message = $"Tier must be one of: {GetTierListText()}.";
            return false;
        }

        var preset = GetOrCreatePreset(normalizedPresetName);
        var existing = FindTier(normalizedPresetName, normalizedTierName);
        var snapshot = BuildPresetSnapshot(from, normalizedPresetName, normalizedTierName, from.Name);

        if (existing == null)
        {
            preset.Tiers.Add(snapshot);
            preset.Tiers.Sort(ComparePresetTiersWithinPreset);
            Save();
            message = $"Added preset '{normalizedPresetName} [{normalizedTierName}]'.";
            return true;
        }

        existing.UpdatedAt = snapshot.UpdatedAt;
        existing.Stats = snapshot.Stats;
        existing.Skills = snapshot.Skills;

        Save();
        message = $"Updated preset '{normalizedPresetName} [{normalizedTierName}]'.";
        return true;
    }

    public static bool DeletePresetTier(Mobile from, string presetName, string tierName, out string message)
    {
        EnsureInitialized();

        if (!CanModify(from, out message))
        {
            return false;
        }

        var normalizedPresetName = NormalizePresetName(presetName);
        var normalizedTierName = NormalizeTierName(tierName);

        if (string.IsNullOrWhiteSpace(normalizedPresetName) || string.IsNullOrWhiteSpace(normalizedTierName))
        {
            message = "You must provide a preset name and tier.";
            return false;
        }

        for (var i = 0; i < _state.Presets.Count; i++)
        {
            var preset = _state.Presets[i];
            if (preset == null || !string.Equals(preset.Name, normalizedPresetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (var j = 0; j < preset.Tiers.Count; j++)
            {
                var tier = preset.Tiers[j];
                if (tier != null && string.Equals(tier.Tier, normalizedTierName, StringComparison.OrdinalIgnoreCase))
                {
                    preset.Tiers.RemoveAt(j);

                    if (preset.Tiers.Count == 0)
                    {
                        _state.Presets.RemoveAt(i);
                    }

                    Save();
                    message = $"Deleted preset '{normalizedPresetName} [{normalizedTierName}]'.";
                    return true;
                }
            }

            break;
        }

        message = "That preset tier could not be found.";
        return false;
    }

    public static void Save()
    {
        if (!_initialized)
        {
            return;
        }

        JsonConfig.Serialize(SavePath, _state);
    }

    private static void NormalizeLoadedState()
    {
        for (var i = _state.Presets.Count - 1; i >= 0; i--)
        {
            var preset = _state.Presets[i];
            if (preset == null)
            {
                _state.Presets.RemoveAt(i);
                continue;
            }

            preset.Name = NormalizePresetName(preset.Name);
            preset.Tiers ??= new List<TemplatePresetTierDefinition>();

            for (var j = preset.Tiers.Count - 1; j >= 0; j--)
            {
                var tier = preset.Tiers[j];
                if (tier == null)
                {
                    preset.Tiers.RemoveAt(j);
                    continue;
                }

                if (tier.Id == Guid.Empty)
                {
                    tier.Id = Guid.NewGuid();
                }

                tier.PresetName = NormalizePresetName(tier.PresetName);
                if (string.IsNullOrWhiteSpace(tier.PresetName))
                {
                    tier.PresetName = preset.Name;
                }

                tier.Tier = NormalizeTierName(tier.Tier);
                tier.Stats ??= new StatTemplateSnapshot();
                tier.Skills ??= new List<SkillTemplateSnapshot>();

                for (var k = tier.Skills.Count - 1; k >= 0; k--)
                {
                    var skill = tier.Skills[k];
                    if (skill == null || skill.Base <= 0.0)
                    {
                        tier.Skills.RemoveAt(k);
                    }
                }
            }

            preset.Tiers.Sort(ComparePresetTiersWithinPreset);

            if (string.IsNullOrWhiteSpace(preset.Name) || preset.Tiers.Count == 0)
            {
                _state.Presets.RemoveAt(i);
            }
        }

        _state.Presets.Sort(ComparePresets);
    }

    private static TemplatePresetDefinition GetOrCreatePreset(string presetName)
    {
        for (var i = 0; i < _state.Presets.Count; i++)
        {
            var preset = _state.Presets[i];
            if (preset != null && string.Equals(preset.Name, presetName, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }

        var created = new TemplatePresetDefinition
        {
            Name = presetName,
            Tiers = new List<TemplatePresetTierDefinition>()
        };

        _state.Presets.Add(created);
        _state.Presets.Sort(ComparePresets);
        return created;
    }

    private static TemplatePresetTierDefinition BuildPresetSnapshot(
        Mobile source,
        string presetName,
        string tierName,
        string createdBy
    )
    {
        var snapshot = new TemplatePresetTierDefinition
        {
            Id = Guid.NewGuid(),
            PresetName = presetName,
            Tier = tierName,
            CreatedAt = Core.Now,
            UpdatedAt = Core.Now,
            CreatedBy = createdBy,
            Stats = new StatTemplateSnapshot
            {
                Str = source.RawStr,
                Dex = source.RawDex,
                Int = source.RawInt,
                StrLock = source.StrLock,
                DexLock = source.DexLock,
                IntLock = source.IntLock
            },
            Skills = new List<SkillTemplateSnapshot>()
        };

        for (var i = 0; i < source.Skills.Length; i++)
        {
            var skill = source.Skills[i];
            if (skill == null || skill.Base <= 0.0)
            {
                continue;
            }

            snapshot.Skills.Add(
                new SkillTemplateSnapshot
                {
                    SkillIndex = i,
                    SkillName = skill.Info?.Name ?? ((SkillName)i).ToString(),
                    Base = skill.Base,
                    Lock = skill.Lock
                }
            );
        }

        return snapshot;
    }

    private static void ApplyPresetToMobile(Mobile target, TemplatePresetTierDefinition tier)
    {
        if (target == null || tier == null)
        {
            return;
        }

        target.RawStr = tier.Stats.Str;
        target.RawDex = tier.Stats.Dex;
        target.RawInt = tier.Stats.Int;

        target.StrLock = tier.Stats.StrLock;
        target.DexLock = tier.Stats.DexLock;
        target.IntLock = tier.Stats.IntLock;

        for (var i = 0; i < target.Skills.Length; i++)
        {
            var skill = target.Skills[i];
            if (skill == null)
            {
                continue;
            }

            skill.Base = 0.0;
            skill.SetLockNoRelay(SkillLock.Up);
        }

        for (var i = 0; i < tier.Skills.Count; i++)
        {
            var snapshot = tier.Skills[i];
            if (snapshot == null || snapshot.SkillIndex < 0 || snapshot.SkillIndex >= target.Skills.Length)
            {
                continue;
            }

            var skill = target.Skills[snapshot.SkillIndex];
            if (skill == null)
            {
                continue;
            }

            skill.Base = snapshot.Base;
            skill.SetLockNoRelay(snapshot.Lock);
        }

        target.Hits = target.HitsMax;
        target.Stam = target.StamMax;
        target.Mana = target.ManaMax;
    }

    private static string NormalizePresetName(string presetName)
    {
        if (presetName == null)
        {
            return null;
        }

        var trimmed = presetName.Trim();
        if (trimmed.Length > 40)
        {
            trimmed = trimmed[..40];
        }

        return trimmed;
    }

    private static string NormalizeTierName(string tierName)
    {
        if (string.IsNullOrWhiteSpace(tierName))
        {
            return null;
        }

        for (var i = 0; i < TierOrder.Length; i++)
        {
            if (string.Equals(TierOrder[i], tierName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return TierOrder[i];
            }
        }

        return null;
    }

    private static string GetTierListText()
    {
        return string.Join(", ", TierOrder);
    }

    private static int ComparePresets(TemplatePresetDefinition a, TemplatePresetDefinition b)
    {
        return string.Compare(a?.Name, b?.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static int ComparePresetTiers(TemplatePresetTierDefinition a, TemplatePresetTierDefinition b)
    {
        var presetCompare = string.Compare(a?.PresetName, b?.PresetName, StringComparison.OrdinalIgnoreCase);
        if (presetCompare != 0)
        {
            return presetCompare;
        }

        return ComparePresetTiersWithinPreset(a, b);
    }

    private static int ComparePresetTiersWithinPreset(TemplatePresetTierDefinition a, TemplatePresetTierDefinition b)
    {
        return GetTierSortIndex(a?.Tier).CompareTo(GetTierSortIndex(b?.Tier));
    }

    private static int GetTierSortIndex(string tierName)
    {
        if (string.IsNullOrWhiteSpace(tierName))
        {
            return int.MaxValue;
        }

        for (var i = 0; i < TierOrder.Length; i++)
        {
            if (string.Equals(TierOrder[i], tierName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static void SeedDefaults()
    {
        SeedBard();
        SeedBardDexxer();
        SeedTamer();
        SeedWarrior();
        SeedCrafter();
    }

    private static void SeedBard()
    {
        AddSeedPreset(
            "Bard",
            "Apprentice",
            50,
            40,
            35,
            (SkillName.Musicianship, 55.0),
            (SkillName.Provocation, 55.0),
            (SkillName.Discordance, 55.0),
            (SkillName.Peacemaking, 55.0),
            (SkillName.Magery, 55.0),
            (SkillName.Meditation, 55.0)
        );

        AddSeedPreset(
            "Bard",
            "Journeyman",
            70,
            55,
            50,
            (SkillName.Musicianship, 65.0),
            (SkillName.Provocation, 65.0),
            (SkillName.Discordance, 65.0),
            (SkillName.Peacemaking, 65.0),
            (SkillName.Magery, 65.0),
            (SkillName.Meditation, 65.0)
        );

        AddSeedPreset(
            "Bard",
            "Expert",
            85,
            70,
            65,
            (SkillName.Musicianship, 75.0),
            (SkillName.Provocation, 75.0),
            (SkillName.Discordance, 75.0),
            (SkillName.Peacemaking, 75.0),
            (SkillName.Magery, 75.0),
            (SkillName.Meditation, 75.0)
        );

        AddSeedPreset(
            "Bard",
            "Adept",
            95,
            80,
            80,
            (SkillName.Musicianship, 85.0),
            (SkillName.Provocation, 85.0),
            (SkillName.Discordance, 85.0),
            (SkillName.Peacemaking, 85.0),
            (SkillName.Magery, 85.0),
            (SkillName.Meditation, 85.0)
        );

        AddSeedPreset(
            "Bard",
            "Master",
            100,
            90,
            90,
            (SkillName.Musicianship, 95.0),
            (SkillName.Provocation, 95.0),
            (SkillName.Discordance, 95.0),
            (SkillName.Peacemaking, 95.0),
            (SkillName.Magery, 95.0),
            (SkillName.Meditation, 95.0)
        );

        AddSeedPreset(
            "Bard",
            "Grandmaster",
            100,
            100,
            100,
            (SkillName.Musicianship, 100.0),
            (SkillName.Provocation, 100.0),
            (SkillName.Discordance, 100.0),
            (SkillName.Peacemaking, 100.0),
            (SkillName.Magery, 100.0),
            (SkillName.Meditation, 100.0)
        );
    }

    private static void SeedBardDexxer()
    {
        AddSeedPreset(
            "BardDexxer",
            "Apprentice",
            70,
            50,
            25,
            (SkillName.Peacemaking, 55.0),
            (SkillName.Musicianship, 55.0),
            (SkillName.Swords, 55.0),
            (SkillName.Tactics, 55.0),
            (SkillName.Anatomy, 55.0),
            (SkillName.Healing, 55.0),
            (SkillName.MagicResist, 55.0)
        );

        AddSeedPreset(
            "BardDexxer",
            "Journeyman",
            80,
            65,
            25,
            (SkillName.Peacemaking, 65.0),
            (SkillName.Musicianship, 65.0),
            (SkillName.Swords, 65.0),
            (SkillName.Tactics, 65.0),
            (SkillName.Anatomy, 65.0),
            (SkillName.Healing, 65.0),
            (SkillName.MagicResist, 65.0)
        );

        AddSeedPreset(
            "BardDexxer",
            "Expert",
            90,
            80,
            25,
            (SkillName.Peacemaking, 75.0),
            (SkillName.Musicianship, 75.0),
            (SkillName.Swords, 75.0),
            (SkillName.Tactics, 75.0),
            (SkillName.Anatomy, 75.0),
            (SkillName.Healing, 75.0),
            (SkillName.MagicResist, 75.0)
        );

        AddSeedPreset(
            "BardDexxer",
            "Adept",
            100,
            90,
            25,
            (SkillName.Peacemaking, 85.0),
            (SkillName.Musicianship, 85.0),
            (SkillName.Swords, 85.0),
            (SkillName.Tactics, 85.0),
            (SkillName.Anatomy, 85.0),
            (SkillName.Healing, 85.0),
            (SkillName.MagicResist, 85.0)
        );

        AddSeedPreset(
            "BardDexxer",
            "Master",
            100,
            100,
            25,
            (SkillName.Peacemaking, 95.0),
            (SkillName.Musicianship, 95.0),
            (SkillName.Swords, 95.0),
            (SkillName.Tactics, 95.0),
            (SkillName.Anatomy, 95.0),
            (SkillName.Healing, 95.0),
            (SkillName.MagicResist, 95.0)
        );

        AddSeedPreset(
            "BardDexxer",
            "Grandmaster",
            100,
            100,
            25,
            (SkillName.Peacemaking, 100.0),
            (SkillName.Musicianship, 100.0),
            (SkillName.Swords, 100.0),
            (SkillName.Tactics, 100.0),
            (SkillName.Anatomy, 100.0),
            (SkillName.Healing, 100.0),
            (SkillName.MagicResist, 100.0)
        );
    }

    private static void SeedTamer()
    {
        AddSeedPreset(
            "Tamer",
            "Apprentice",
            60,
            35,
            55,
            (SkillName.AnimalTaming, 55.0),
            (SkillName.AnimalLore, 55.0),
            (SkillName.Veterinary, 55.0),
            (SkillName.Magery, 55.0),
            (SkillName.Meditation, 55.0),
            (SkillName.MagicResist, 55.0)
        );

        AddSeedPreset(
            "Tamer",
            "Journeyman",
            75,
            45,
            70,
            (SkillName.AnimalTaming, 65.0),
            (SkillName.AnimalLore, 65.0),
            (SkillName.Veterinary, 65.0),
            (SkillName.Magery, 65.0),
            (SkillName.Meditation, 65.0),
            (SkillName.MagicResist, 65.0)
        );

        AddSeedPreset(
            "Tamer",
            "Expert",
            90,
            55,
            85,
            (SkillName.AnimalTaming, 75.0),
            (SkillName.AnimalLore, 75.0),
            (SkillName.Veterinary, 75.0),
            (SkillName.Magery, 75.0),
            (SkillName.Meditation, 75.0),
            (SkillName.MagicResist, 75.0)
        );

        AddSeedPreset(
            "Tamer",
            "Adept",
            100,
            70,
            90,
            (SkillName.AnimalTaming, 85.0),
            (SkillName.AnimalLore, 85.0),
            (SkillName.Veterinary, 85.0),
            (SkillName.Magery, 85.0),
            (SkillName.Meditation, 85.0),
            (SkillName.MagicResist, 85.0)
        );

        AddSeedPreset(
            "Tamer",
            "Master",
            100,
            90,
            100,
            (SkillName.AnimalTaming, 95.0),
            (SkillName.AnimalLore, 95.0),
            (SkillName.Veterinary, 95.0),
            (SkillName.Magery, 95.0),
            (SkillName.Meditation, 95.0),
            (SkillName.MagicResist, 95.0)
        );

        AddSeedPreset(
            "Tamer",
            "Grandmaster",
            100,
            100,
            100,
            (SkillName.AnimalTaming, 100.0),
            (SkillName.AnimalLore, 100.0),
            (SkillName.Veterinary, 100.0),
            (SkillName.Magery, 100.0),
            (SkillName.Meditation, 100.0),
            (SkillName.MagicResist, 100.0)
        );
    }

    private static void SeedWarrior()
    {
        AddSeedPreset(
            "Warrior",
            "Apprentice",
            60,
            55,
            20,
            (SkillName.Swords, 55.0),
            (SkillName.Tactics, 55.0),
            (SkillName.Anatomy, 55.0),
            (SkillName.Healing, 55.0),
            (SkillName.Parry, 55.0),
            (SkillName.MagicResist, 55.0)
        );

        AddSeedPreset(
            "Warrior",
            "Journeyman",
            75,
            70,
            25,
            (SkillName.Swords, 65.0),
            (SkillName.Tactics, 65.0),
            (SkillName.Anatomy, 65.0),
            (SkillName.Healing, 65.0),
            (SkillName.Parry, 65.0),
            (SkillName.MagicResist, 65.0)
        );

        AddSeedPreset(
            "Warrior",
            "Expert",
            90,
            85,
            25,
            (SkillName.Swords, 75.0),
            (SkillName.Tactics, 75.0),
            (SkillName.Anatomy, 75.0),
            (SkillName.Healing, 75.0),
            (SkillName.Parry, 75.0),
            (SkillName.MagicResist, 75.0)
        );

        AddSeedPreset(
            "Warrior",
            "Adept",
            100,
            95,
            25,
            (SkillName.Swords, 85.0),
            (SkillName.Tactics, 85.0),
            (SkillName.Anatomy, 85.0),
            (SkillName.Healing, 85.0),
            (SkillName.Parry, 85.0),
            (SkillName.MagicResist, 85.0)
        );

        AddSeedPreset(
            "Warrior",
            "Master",
            100,
            100,
            25,
            (SkillName.Swords, 95.0),
            (SkillName.Tactics, 95.0),
            (SkillName.Anatomy, 95.0),
            (SkillName.Healing, 95.0),
            (SkillName.Parry, 95.0),
            (SkillName.MagicResist, 95.0)
        );

        AddSeedPreset(
            "Warrior",
            "Grandmaster",
            100,
            100,
            25,
            (SkillName.Swords, 100.0),
            (SkillName.Tactics, 100.0),
            (SkillName.Anatomy, 100.0),
            (SkillName.Healing, 100.0),
            (SkillName.Parry, 100.0),
            (SkillName.MagicResist, 100.0)
        );
    }

    private static void SeedCrafter()
    {
        AddSeedPreset(
            "Crafter",
            "Apprentice",
            40,
            40,
            60,
            (SkillName.Blacksmith, 55.0),
            (SkillName.Tinkering, 55.0),
            (SkillName.Tailoring, 55.0),
            (SkillName.Carpentry, 55.0),
            (SkillName.Alchemy, 55.0),
            (SkillName.Inscribe, 55.0)
        );

        AddSeedPreset(
            "Crafter",
            "Journeyman",
            50,
            50,
            75,
            (SkillName.Blacksmith, 65.0),
            (SkillName.Tinkering, 65.0),
            (SkillName.Tailoring, 65.0),
            (SkillName.Carpentry, 65.0),
            (SkillName.Alchemy, 65.0),
            (SkillName.Inscribe, 65.0)
        );

        AddSeedPreset(
            "Crafter",
            "Expert",
            60,
            60,
            90,
            (SkillName.Blacksmith, 75.0),
            (SkillName.Tinkering, 75.0),
            (SkillName.Tailoring, 75.0),
            (SkillName.Carpentry, 75.0),
            (SkillName.Alchemy, 75.0),
            (SkillName.Inscribe, 75.0)
        );

        AddSeedPreset(
            "Crafter",
            "Adept",
            70,
            70,
            100,
            (SkillName.Blacksmith, 85.0),
            (SkillName.Tinkering, 85.0),
            (SkillName.Tailoring, 85.0),
            (SkillName.Carpentry, 85.0),
            (SkillName.Alchemy, 85.0),
            (SkillName.Inscribe, 85.0)
        );

        AddSeedPreset(
            "Crafter",
            "Master",
            80,
            80,
            100,
            (SkillName.Blacksmith, 95.0),
            (SkillName.Tinkering, 95.0),
            (SkillName.Tailoring, 95.0),
            (SkillName.Carpentry, 95.0),
            (SkillName.Alchemy, 95.0),
            (SkillName.Inscribe, 95.0)
        );

        AddSeedPreset(
            "Crafter",
            "Grandmaster",
            90,
            90,
            100,
            (SkillName.Blacksmith, 100.0),
            (SkillName.Tinkering, 100.0),
            (SkillName.Tailoring, 100.0),
            (SkillName.Carpentry, 100.0),
            (SkillName.Alchemy, 100.0),
            (SkillName.Inscribe, 100.0)
        );
    }

    private static void AddSeedPreset(
        string presetName,
        string tierName,
        int str,
        int dex,
        int intel,
        params (SkillName skill, double value)[] skills
    )
    {
        var preset = GetOrCreatePreset(presetName);

        var tier = new TemplatePresetTierDefinition
        {
            Id = Guid.NewGuid(),
            PresetName = presetName,
            Tier = tierName,
            CreatedAt = Core.Now,
            UpdatedAt = Core.Now,
            CreatedBy = "System",
            Stats = new StatTemplateSnapshot
            {
                Str = str,
                Dex = dex,
                Int = intel,
                StrLock = StatLockType.Up,
                DexLock = StatLockType.Up,
                IntLock = StatLockType.Up
            },
            Skills = new List<SkillTemplateSnapshot>()
        };

        for (var i = 0; i < skills.Length; i++)
        {
            var (skillName, value) = skills[i];
            if (value <= 0.0)
            {
                continue;
            }

            tier.Skills.Add(
                new SkillTemplateSnapshot
                {
                    SkillIndex = (int)skillName,
                    SkillName = skillName.ToString(),
                    Base = value,
                    Lock = SkillLock.Up
                }
            );
        }

        preset.Tiers.Add(tier);
        preset.Tiers.Sort(ComparePresetTiersWithinPreset);
    }
}
