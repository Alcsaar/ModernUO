using System;
using System.Collections.Generic;
using System.IO;
using Server.Gumps;
using Server.Json;
using Server.Logging;

namespace Server.Spells;

public sealed class TravelRestrictionConfig
{
    public List<TravelRestrictionRuleConfig> Rules { get; set; } = [];
}

public sealed class TravelRestrictionRuleConfig
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool Enabled { get; set; } = true;
    public bool RecallFrom { get; set; }
    public bool RecallTo { get; set; }
    public bool GateFrom { get; set; }
    public bool GateTo { get; set; }
    public bool Mark { get; set; }
    public bool TeleportFrom { get; set; }
    public bool TeleportTo { get; set; }

    public bool Allows(TravelCheckType type) =>
        type switch
        {
            TravelCheckType.RecallFrom   => RecallFrom,
            TravelCheckType.RecallTo     => RecallTo,
            TravelCheckType.GateFrom     => GateFrom,
            TravelCheckType.GateTo       => GateTo,
            TravelCheckType.Mark         => Mark,
            TravelCheckType.TeleportFrom => TeleportFrom,
            TravelCheckType.TeleportTo   => TeleportTo,
            _                            => false
        };

    public void SetAllowed(TravelCheckType type, bool allowed)
    {
        switch (type)
        {
            case TravelCheckType.RecallFrom:
                RecallFrom = allowed;
                break;
            case TravelCheckType.RecallTo:
                RecallTo = allowed;
                break;
            case TravelCheckType.GateFrom:
                GateFrom = allowed;
                break;
            case TravelCheckType.GateTo:
                GateTo = allowed;
                break;
            case TravelCheckType.Mark:
                Mark = allowed;
                break;
            case TravelCheckType.TeleportFrom:
                TeleportFrom = allowed;
                break;
            case TravelCheckType.TeleportTo:
                TeleportTo = allowed;
                break;
        }
    }
}

public static class TravelRestrictionSystem
{
    public const string ConfigurationPath = "Configuration/travelrestrictions.json";

    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(TravelRestrictionSystem));

    private static readonly TravelCheckType[] _travelTypes =
    [
        TravelCheckType.RecallFrom,
        TravelCheckType.RecallTo,
        TravelCheckType.GateFrom,
        TravelCheckType.GateTo,
        TravelCheckType.Mark,
        TravelCheckType.TeleportFrom,
        TravelCheckType.TeleportTo
    ];

    private static readonly TravelRestrictionDefinition[] _definitions =
    [
        new("felucca_t2a", "T2A (Felucca)", SpellHelper.IsFeluccaT2A, false, false, false, false, false, true, true),
        new("khaldun", "Khaldun", SpellHelper.IsKhaldun, false, false, false, false, false, true, true),
        new("ilshenar", "Ilshenar", SpellHelper.IsIlshenar, true, false, false, false, false, true, true),
        new("trammel_wind", "Wind (Trammel)", SpellHelper.IsTrammelWind, true, false, false, false, false, true, true),
        new("felucca_wind", "Wind (Felucca)", SpellHelper.IsFeluccaWind, false, false, false, false, false, true, true),
        new("felucca_dungeon", "Dungeons (Felucca)", SpellHelper.IsFeluccaDungeon, false, false, false, false, false, true, true),
        new("trammel_solen_hive", "Solen Hive (Trammel)", SpellHelper.IsTrammelSolenHive, true, false, false, false, false, true, true),
        new("felucca_solen_hive", "Solen Hive (Felucca)", SpellHelper.IsFeluccaSolenHive, false, false, false, false, false, true, true),
        new("crystal_cave", "Crystal Cave", SpellHelper.IsCrystalCave, false, false, false, false, false, false, false),
        new("doom_gauntlet", "Doom Gauntlet", SpellHelper.IsDoomGauntlet, false, false, false, false, false, true, true),
        new("doom_ferry", "Doom Ferry", SpellHelper.IsDoomFerry, false, false, false, false, false, true, false),
        new("safe_zone", "Safe Zone", SpellHelper.IsSafeZone, true, false, false, false, false, true, false),
        new("faction_stronghold", "Faction Stronghold", SpellHelper.IsFactionStronghold, true, false, false, false, false, false, false),
        new("champion_spawn", "Champion Spawn", SpellHelper.IsChampionSpawn, false, false, false, false, false, true, true),
        new("tokuno_dungeon", "Dungeons (Tokuno/Malas)", SpellHelper.IsTokunoDungeon, true, false, false, false, false, true, true),
        new("doom_lamp_room", "Doom Lamp Room", SpellHelper.IsLampRoom, false, false, false, false, false, true, true),
        new("doom_guardian_room", "Doom Guardian Room", SpellHelper.IsGuardianRoom, false, false, false, false, false, true, true),
        new("heartwood", "Heartwood", SpellHelper.IsHeartwood, false, false, false, false, false, false, false),
        new("ml_dungeon", "ML Dungeons", SpellHelper.IsMLDungeon, false, false, false, false, false, true, false)
    ];

    private static TravelRestrictionConfig _config;
    private static TravelRestrictionRule[] _rules = [];

    public static ReadOnlySpan<TravelCheckType> TravelTypes => _travelTypes;

    public static IReadOnlyList<TravelRestrictionRuleConfig> Rules => _config?.Rules ?? [];

    public static void Configure()
    {
        CommandSystem.Register("TravelRestrictions", AccessLevel.Administrator, TravelRestrictions_OnCommand);
        CommandSystem.Register("TravelRestrictionsReload", AccessLevel.Administrator, TravelRestrictionsReload_OnCommand);
        CommandSystem.Register("TravelRestrictionsReset", AccessLevel.Administrator, TravelRestrictionsReset_OnCommand);

        Load();
    }

    public static bool CheckTravel(Map map, Point3D loc, TravelCheckType type)
    {
        var rules = _rules;

        for (var i = 0; i < rules.Length; i++)
        {
            ref readonly var rule = ref rules[i];

            if (rule.Enabled && !rule.Allows(type) && rule.Validator(map, loc))
            {
                return false;
            }
        }

        return true;
    }

    public static void Load()
    {
        var path = Path.Combine(Core.BaseDirectory, ConfigurationPath);

        try
        {
            _config = File.Exists(path)
                ? JsonConfig.Deserialize<TravelRestrictionConfig>(path) ?? CreateDefaultConfig()
                : CreateDefaultConfig();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load travel restriction configuration. Using default travel restrictions.");
            _config = CreateDefaultConfig();
        }

        NormalizeConfig();
        RebuildRules();
        Save();
    }

    public static void Save()
    {
        var path = Path.Combine(Core.BaseDirectory, ConfigurationPath);
        JsonConfig.Serialize(path, _config);
    }

    public static void ResetToDefaults()
    {
        _config = CreateDefaultConfig();
        RebuildRules();
        Save();
    }

    public static bool ToggleRule(int ruleIndex, TravelCheckType type)
    {
        if (_config?.Rules == null || ruleIndex < 0 || ruleIndex >= _config.Rules.Count)
        {
            return false;
        }

        var rule = _config.Rules[ruleIndex];
        rule.SetAllowed(type, !rule.Allows(type));
        RebuildRules();
        Save();

        return true;
    }

    public static bool ToggleRuleEnabled(int ruleIndex)
    {
        if (_config?.Rules == null || ruleIndex < 0 || ruleIndex >= _config.Rules.Count)
        {
            return false;
        }

        var rule = _config.Rules[ruleIndex];
        rule.Enabled = !rule.Enabled;
        RebuildRules();
        Save();

        return true;
    }

    public static string GetLabel(TravelCheckType type) =>
        type switch
        {
            TravelCheckType.RecallFrom   => "Recall From",
            TravelCheckType.RecallTo     => "Recall To",
            TravelCheckType.GateFrom     => "Gate From",
            TravelCheckType.GateTo       => "Gate To",
            TravelCheckType.Mark         => "Mark",
            TravelCheckType.TeleportFrom => "Tele From",
            TravelCheckType.TeleportTo   => "Tele To",
            _                            => type.ToString()
        };

    private static void TravelRestrictions_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendGump(new TravelRestrictionsGump());
    }

    private static void TravelRestrictionsReload_OnCommand(CommandEventArgs e)
    {
        Load();
        e.Mobile.SendMessage("Travel restrictions reloaded.");
        e.Mobile.SendGump(new TravelRestrictionsGump());
    }

    private static void TravelRestrictionsReset_OnCommand(CommandEventArgs e)
    {
        ResetToDefaults();
        e.Mobile.SendMessage("Travel restrictions reset to defaults.");
        e.Mobile.SendGump(new TravelRestrictionsGump());
    }

    private static TravelRestrictionConfig CreateDefaultConfig()
    {
        var config = new TravelRestrictionConfig();

        for (var i = 0; i < _definitions.Length; i++)
        {
            config.Rules.Add(_definitions[i].CreateConfig());
        }

        return config;
    }

    private static void NormalizeConfig()
    {
        var source = _config.Rules ?? [];
        var normalized = new List<TravelRestrictionRuleConfig>(_definitions.Length);

        for (var i = 0; i < _definitions.Length; i++)
        {
            var definition = _definitions[i];
            var rule = FindRule(source, definition.Id) ?? definition.CreateConfig();

            rule.Id = definition.Id;
            rule.Name = string.IsNullOrWhiteSpace(rule.Name) ? definition.Name : rule.Name;

            normalized.Add(rule);
        }

        _config.Rules = normalized;
    }

    private static TravelRestrictionRuleConfig FindRule(List<TravelRestrictionRuleConfig> rules, string id)
    {
        for (var i = 0; i < rules.Count; i++)
        {
            if (rules[i].Id.InsensitiveEquals(id))
            {
                return rules[i];
            }
        }

        return null;
    }

    private static void RebuildRules()
    {
        var rules = new TravelRestrictionRule[_definitions.Length];

        for (var i = 0; i < _definitions.Length; i++)
        {
            rules[i] = new TravelRestrictionRule(_config.Rules[i], _definitions[i].Validator);
        }

        _rules = rules;
    }

    private readonly record struct TravelRestrictionRule(
        string Id,
        string Name,
        bool Enabled,
        bool RecallFrom,
        bool RecallTo,
        bool GateFrom,
        bool GateTo,
        bool Mark,
        bool TeleportFrom,
        bool TeleportTo,
        TravelRestrictionValidator Validator
    )
    {
        public TravelRestrictionRule(TravelRestrictionRuleConfig config, TravelRestrictionValidator validator)
            : this(
                config.Id,
                config.Name,
                config.Enabled,
                config.RecallFrom,
                config.RecallTo,
                config.GateFrom,
                config.GateTo,
                config.Mark,
                config.TeleportFrom,
                config.TeleportTo,
                validator
            )
        {
        }

        public bool Allows(TravelCheckType type) =>
            type switch
            {
                TravelCheckType.RecallFrom   => RecallFrom,
                TravelCheckType.RecallTo     => RecallTo,
                TravelCheckType.GateFrom     => GateFrom,
                TravelCheckType.GateTo       => GateTo,
                TravelCheckType.Mark         => Mark,
                TravelCheckType.TeleportFrom => TeleportFrom,
                TravelCheckType.TeleportTo   => TeleportTo,
                _                            => false
            };
    }

    private readonly record struct TravelRestrictionDefinition(
        string Id,
        string Name,
        TravelRestrictionValidator Validator,
        bool RecallFrom,
        bool RecallTo,
        bool GateFrom,
        bool GateTo,
        bool Mark,
        bool TeleportFrom,
        bool TeleportTo
    )
    {
        public TravelRestrictionRuleConfig CreateConfig() =>
            new()
            {
                Id = Id,
                Name = Name,
                RecallFrom = RecallFrom,
                RecallTo = RecallTo,
                GateFrom = GateFrom,
                GateTo = GateTo,
                Mark = Mark,
                TeleportFrom = TeleportFrom,
                TeleportTo = TeleportTo
            };
    }

    private delegate bool TravelRestrictionValidator(Map map, Point3D loc);
}
