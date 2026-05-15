using System;
using System.IO;
using System.Reflection;
using Server.Commands;
using Server.Logging;
using Server.Mobiles;
using Server.Targeting;
using Server.Text;

namespace Server.Custom.Engines.CreatureDifficultySystem;

public static class CreatureDifficultySystemCommands
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(CreatureDifficultySystemCommands));

    private static readonly string[] _excludedNamespaceMarkersForBasic =
    {
        ".Factions.",
        ".Engines.Factions.",
        ".Quests.",
        ".Escortable",
        ".Vendors.",
        ".Towns.",
        ".Test.",
        ".Tests."
    };

    private static readonly string[] _excludedTypeMarkersForBasic =
    {
        "Faction",
        "Vendor",
        "Escort",
        "Escortable",
        "TownCrier",
        "Auctioneer",
        "Banker",
        "InnKeeper",
        "Stablemaster",
        "AnimalTrainer",
        "Provisioner",
        "Blacksmith",
        "Bowyer",
        "Mage",
        "Healer",
        "BrideGroom",
        "Waiter",
        "Waitress",
        "Barkeeper",
        "TavernKeeper",
        "GameMaster",
        "Counselor",
        "Test",

        // New pass exclusions
        "Familiar",
        "Steed",
        "Mount",
        "Summoned",
        "Spawned"
    };

    private static readonly string[] _excludedExactTypeNamesForBasic =
    {
        "Barracoon",
        "Neira",
        "Rikktor",
        "Mephitis",
        "Semidar",
        "Silvani",
        "LordOaks",
        "GrimmochDrummel",
        "LadyJennifyr",
        "LadyMarai",
        "LadyLissith",
        "LadySabrix"
    };

    public static void Configure()
    {
        CommandSystem.Register("GetDifficulty", AccessLevel.GameMaster, GetDifficulty_OnCommand);
        CommandSystem.Register("ExportCreatureDifficulties", AccessLevel.Administrator, ExportCreatureDifficulties_OnCommand);
    }

    [Usage("GetDifficulty")]
    [Description("Targets a creature and displays its difficulty breakdown.")]
    public static void GetDifficulty_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target a creature to evaluate difficulty.");
        e.Mobile.Target = new DifficultyTarget();
    }

    [Usage("ExportCreatureDifficulties [basic|full]")]
    [Description("Exports creature difficulty data. Default is basic filtered export.")]
    public static void ExportCreatureDifficulties_OnCommand(CommandEventArgs e)
    {
        var mode = ExportMode.Basic;

        if (e.Length > 0)
        {
            var arg = e.GetString(0);

            if (InsensitiveEquals(arg, "full"))
            {
                mode = ExportMode.Full;
            }
            else if (InsensitiveEquals(arg, "basic"))
            {
                mode = ExportMode.Basic;
            }
            else
            {
                e.Mobile.SendMessage("Usage: [ExportCreatureDifficulties [basic|full]");
                return;
            }
        }

        RunExport(e.Mobile, mode);
    }

    private static void RunExport(Mobile from, ExportMode mode)
    {
        try
        {
            var directory = Path.Combine(Core.BaseDirectory, "Configuration", "CreatureDifficultySystem");
            Directory.CreateDirectory(directory);

            var timestamp = Core.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = mode == ExportMode.Full
                ? $"CreatureDifficultyExport_Full_{timestamp}.csv"
                : $"CreatureDifficultyExport_Basic_{timestamp}.csv";

            var path = Path.Combine(directory, fileName);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var scannedCount = 0;
            var exportedCount = 0;
            var excludedCount = 0;
            var failedCount = 0;

            using var writer = new StreamWriter(path, false);

            writer.WriteLine("TypeName,DisplayName,Tier,Score,Offense,Defense,Magic,Special,AutoDispel,BardImmune,AbilityCount,Abilities,ExportMode");

            for (var assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                var assembly = assemblies[assemblyIndex];
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null)
                {
                    continue;
                }

                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    var type = types[typeIndex];

                    if (type == null || !typeof(BaseCreature).IsAssignableFrom(type) || type.IsAbstract)
                    {
                        continue;
                    }

                    var ctor = type.GetConstructor(Type.EmptyTypes);

                    if (ctor == null)
                    {
                        continue;
                    }

                    scannedCount++;

                    if (mode == ExportMode.Basic && !ShouldIncludeInBasicExport(type))
                    {
                        excludedCount++;
                        continue;
                    }

                    BaseCreature creature = null;

                    try
                    {
                        creature = ctor.Invoke(null) as BaseCreature;

                        if (creature == null)
                        {
                            failedCount++;
                            continue;
                        }

                        var result = CreatureDifficultyService.GetDifficulty(creature);
                        var abilities = creature.GetMonsterAbilities();
                        var abilityCount = abilities?.Length ?? 0;
                        var displayName = GetDisplayName(creature, type);
                        var abilitiesText = BuildAbilityList(abilities);

                        WriteCsvField(writer, type.FullName ?? type.Name); writer.Write(',');
                        WriteCsvField(writer, displayName); writer.Write(',');
                        writer.Write(result.Tier); writer.Write(',');
                        writer.Write(result.Score.ToString("F2")); writer.Write(',');
                        writer.Write(result.OffenseScore.ToString("F2")); writer.Write(',');
                        writer.Write(result.DefenseScore.ToString("F2")); writer.Write(',');
                        writer.Write(result.MagicScore.ToString("F2")); writer.Write(',');
                        writer.Write(result.SpecialScore.ToString("F2")); writer.Write(',');
                        writer.Write(creature.AutoDispel ? "True" : "False"); writer.Write(',');
                        writer.Write(creature.BardImmune ? "True" : "False"); writer.Write(',');
                        writer.Write(abilityCount); writer.Write(',');
                        WriteCsvField(writer, abilitiesText); writer.Write(',');
                        writer.Write(mode == ExportMode.Full ? "Full" : "Basic");
                        writer.WriteLine();

                        exportedCount++;
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        _logger.Warning(ex, $"Creature difficulty export failed for type {type.FullName}");
                    }
                    finally
                    {
                        if (creature != null && !creature.Deleted)
                        {
                            creature.Delete();
                        }
                    }
                }
            }

            from.SendMessage($"Export complete ({mode}). Scanned: {scannedCount}, Exported: {exportedCount}, Excluded: {excludedCount}, Failed: {failedCount}");
            from.SendMessage(path);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Export failed.");
            from.SendMessage("Export failed. Check logs.");
        }
    }

    private static bool ShouldIncludeInBasicExport(Type type)
    {
        var fullName = type.FullName ?? string.Empty;
        var name = type.Name;

        for (var i = 0; i < _excludedNamespaceMarkersForBasic.Length; i++)
        {
            if (ContainsInsensitive(fullName, _excludedNamespaceMarkersForBasic[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < _excludedTypeMarkersForBasic.Length; i++)
        {
            if (ContainsInsensitive(name, _excludedTypeMarkersForBasic[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < _excludedExactTypeNamesForBasic.Length; i++)
        {
            if (InsensitiveEquals(name, _excludedExactTypeNamesForBasic[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetDisplayName(BaseCreature creature, Type type)
    {
        return string.IsNullOrWhiteSpace(creature.DefaultName)
            ? type.Name
            : creature.DefaultName;
    }

    private static string BuildAbilityList(MonsterAbility[] abilities)
    {
        if (abilities == null || abilities.Length == 0)
        {
            return string.Empty;
        }

        using var sb = ValueStringBuilder.Create(128);

        for (var i = 0; i < abilities.Length; i++)
        {
            if (abilities[i] == null)
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append('|');
            }

            sb.Append(abilities[i].GetType().Name);
        }

        return sb.ToString();
    }

    private static void WriteCsvField(TextWriter writer, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var needsQuotes = false;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c == ',' || c == '"' || c == '\r' || c == '\n')
            {
                needsQuotes = true;
                break;
            }
        }

        if (!needsQuotes)
        {
            writer.Write(value);
            return;
        }

        writer.Write('"');

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c == '"')
            {
                writer.Write("\"\"");
            }
            else
            {
                writer.Write(c);
            }
        }

        writer.Write('"');
    }

    private static bool InsensitiveEquals(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsInsensitive(string value, string pattern)
        => value?.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;

    private sealed class DifficultyTarget : Target
    {
        public DifficultyTarget() : base(-1, false, TargetFlags.None) { }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not BaseCreature creature)
            {
                from.SendMessage("Not a creature.");
                return;
            }

            var result = CreatureDifficultyService.GetDifficulty(creature);

            from.SendMessage($"Creature: {creature.GetType().Name}");
            from.SendMessage($"Tier: {result.Tier} | Score: {result.Score:F2}");
            from.SendMessage($"Offense: {result.OffenseScore:F2} Defense: {result.DefenseScore:F2}");
            from.SendMessage($"Magic: {result.MagicScore:F2} Special: {result.SpecialScore:F2}");
        }
    }

    private enum ExportMode
    {
        Basic,
        Full
    }
}
