using System;
using System.IO;
using Server.Commands;
using Server.Custom.Engines.CreatureDifficultySystem;
using Server.Custom.Systems.CustomFeatureFlags;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Engines.RelativeThreatSystem;

public static class RelativeThreatCommands
{
    private static readonly double[] _calibrationSkillValues =
    {
        50.0,
        70.0,
        90.0,
        100.0
    };

    private static readonly RelativeThreatPlayerStyle[] _calibrationStyles =
    {
        RelativeThreatPlayerStyle.Warrior,
        RelativeThreatPlayerStyle.Archer,
        RelativeThreatPlayerStyle.Mage,
        RelativeThreatPlayerStyle.Tamer,
        RelativeThreatPlayerStyle.BardMage,
        RelativeThreatPlayerStyle.BardDexxer,
        RelativeThreatPlayerStyle.Paladin
    };

    public static void Configure()
    {
        CommandSystem.Register("GetThreat", AccessLevel.Player, GetThreat_OnCommand);
        CommandSystem.Register("ExportThreatCalibration", AccessLevel.GameMaster, ExportThreatCalibration_OnCommand);
    }

    [Usage("GetThreat")]
    [Description("Targets a creature and displays its relative threat compared to you.")]
    public static void GetThreat_OnCommand(CommandEventArgs e)
    {
        if (!CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.RelativeThreat))
        {
            e.Mobile.SendMessage("Relative Threat is currently disabled.");
            return;
        }

        e.Mobile.SendMessage("Target a creature to evaluate its threat.");
        e.Mobile.Target = new GetThreatTarget();
    }

    [Usage("ExportThreatCalibration")]
    [Description("Targets a creature and exports relative threat calibration rows for standard player templates.")]
    public static void ExportThreatCalibration_OnCommand(CommandEventArgs e)
    {
        if (!CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.RelativeThreat))
        {
            e.Mobile.SendMessage("Relative Threat is currently disabled.");
            return;
        }

        e.Mobile.SendMessage("Target a creature to export threat calibration rows.");
        e.Mobile.Target = new ExportThreatCalibrationTarget();
    }

    private sealed class GetThreatTarget : Target
    {
        public GetThreatTarget() : base(12, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (!CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.RelativeThreat))
            {
                from.SendMessage("Relative Threat is currently disabled.");
                return;
            }

            if (from == null || from.Deleted)
            {
                return;
            }

            if (targeted is not BaseCreature creature)
            {
                from.SendMessage("That is not a creature.");
                return;
            }

            var playerPower = PlayerCombatPowerEvaluator.Evaluate(from);
            var threat = RelativeThreatService.GetThreat(from, creature, playerPower);

            from.SendMessage($"Threat: {threat.ThreatLabel}");
            from.SendMessage($"Ratio: {threat.Ratio:0.00}");
            from.SendMessage($"Player Score: {threat.PlayerScore:0.0}");
            from.SendMessage($"Creature Threat Score: {threat.CreatureScore:0.0}");
            from.SendMessage($"Primary: {playerPower.PrimaryStyle}  Secondary: {playerPower.SecondaryStyle}");
            from.SendMessage($"Melee: {playerPower.MeleeScore:0.0}  Archer: {playerPower.ArcherScore:0.0}  Mage: {playerPower.MageScore:0.0}");
            from.SendMessage($"Tamer: {playerPower.TamerScore:0.0}  Bard: {playerPower.BardScore:0.0}");
            from.SendMessage($"Healing: {playerPower.HealingScore:0.0}  Resist: {playerPower.MagicResistScore:0.0}");
        }
    }

    private sealed class ExportThreatCalibrationTarget : Target
    {
        public ExportThreatCalibrationTarget() : base(12, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from == null || from.Deleted)
            {
                return;
            }

            if (targeted is not BaseCreature creature)
            {
                from.SendMessage("That is not a creature.");
                return;
            }

            try
            {
                var path = ExportCalibrationRows(creature);
                from.SendMessage("Threat calibration export complete.");
                from.SendMessage(path);
            }
            catch (Exception ex)
            {
                from.SendMessage($"Threat calibration export failed: {ex.Message}");
            }
        }
    }

    private static string ExportCalibrationRows(BaseCreature creature)
    {
        var directory = Path.Combine(Core.BaseDirectory, "Configuration", "RelativeThreatSystem");
        Directory.CreateDirectory(directory);

        var timestamp = Core.Now.ToString("yyyyMMdd_HHmmss");
        var typeName = creature.GetType().Name;
        var path = Path.Combine(directory, $"RelativeThreatCalibration_{typeName}_{timestamp}.csv");
        var difficulty = CreatureDifficultyService.EvaluateCurrent(creature);

        using var writer = new StreamWriter(path, false);

        writer.WriteLine("CreatureType,CreatureName,DifficultyScore,CreatureThreatScore,DurabilityScore,Template,SkillValue,Healing,MagicResist,PlayerScore,Ratio,ThreatLabel,Primary,Secondary,Melee,Archer,Mage,Tamer,Bard");

        for (var styleIndex = 0; styleIndex < _calibrationStyles.Length; styleIndex++)
        {
            var style = _calibrationStyles[styleIndex];

            for (var skillIndex = 0; skillIndex < _calibrationSkillValues.Length; skillIndex++)
            {
                var skillValue = _calibrationSkillValues[skillIndex];
                var template = new RelativeThreatPlayerTemplate(
                    style,
                    skillValue,
                    ShouldTemplateUseHealing(style),
                    ShouldTemplateUseMagicResist(style)
                );
                var playerPower = PlayerCombatPowerEvaluator.EvaluateTemplate(template);
                var threat = RelativeThreatService.GetThreatForTemplate(template, creature);

                WriteCsvField(writer, creature.GetType().FullName ?? creature.GetType().Name); writer.Write(',');
                WriteCsvField(writer, string.IsNullOrWhiteSpace(creature.DefaultName) ? creature.GetType().Name : creature.DefaultName); writer.Write(',');
                writer.Write(difficulty.Score.ToString("F2")); writer.Write(',');
                writer.Write(difficulty.ThreatScore.ToString("F2")); writer.Write(',');
                writer.Write(difficulty.DurabilityScore.ToString("F2")); writer.Write(',');
                WriteCsvField(writer, template.StyleName); writer.Write(',');
                writer.Write(skillValue.ToString("F1")); writer.Write(',');
                writer.Write(template.HasHealing ? "True" : "False"); writer.Write(',');
                writer.Write(template.HasMagicResist ? "True" : "False"); writer.Write(',');
                writer.Write(threat.PlayerScore.ToString("F2")); writer.Write(',');
                writer.Write(threat.Ratio.ToString("F2")); writer.Write(',');
                WriteCsvField(writer, threat.ThreatLabel); writer.Write(',');
                WriteCsvField(writer, playerPower.PrimaryStyle); writer.Write(',');
                WriteCsvField(writer, playerPower.SecondaryStyle); writer.Write(',');
                writer.Write(playerPower.MeleeScore.ToString("F2")); writer.Write(',');
                writer.Write(playerPower.ArcherScore.ToString("F2")); writer.Write(',');
                writer.Write(playerPower.MageScore.ToString("F2")); writer.Write(',');
                writer.Write(playerPower.TamerScore.ToString("F2")); writer.Write(',');
                writer.Write(playerPower.BardScore.ToString("F2"));
                writer.WriteLine();
            }
        }

        return path;
    }

    private static bool ShouldTemplateUseHealing(RelativeThreatPlayerStyle style)
        => style is RelativeThreatPlayerStyle.Warrior or
            RelativeThreatPlayerStyle.Archer or
            RelativeThreatPlayerStyle.BardDexxer or
            RelativeThreatPlayerStyle.Paladin;

    private static bool ShouldTemplateUseMagicResist(RelativeThreatPlayerStyle style)
        => style is not RelativeThreatPlayerStyle.Tamer;

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
}
