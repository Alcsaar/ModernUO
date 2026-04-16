using Server.Commands;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Engines.RelativeThreatSystem;

public static class RelativeThreatCommands
{
    public static void Configure()
    {
        CommandSystem.Register("GetThreat", AccessLevel.Player, GetThreat_OnCommand);
    }

    [Usage("GetThreat")]
    [Description("Targets a creature and displays its relative threat compared to you.")]
    public static void GetThreat_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target a creature to evaluate its threat.");
        e.Mobile.Target = new GetThreatTarget();
    }

    private sealed class GetThreatTarget : Target
    {
        public GetThreatTarget() : base(12, false, TargetFlags.None)
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

            var playerPower = PlayerCombatPowerEvaluator.Evaluate(from);
            var threat = RelativeThreatService.GetThreat(from, creature);

            from.SendMessage($"Threat: {threat.ThreatLabel}");
            from.SendMessage($"Ratio: {threat.Ratio:0.00}");
            from.SendMessage($"Player Score: {threat.PlayerScore:0.0}");
            from.SendMessage($"Creature Score: {threat.CreatureScore:0.0}");
            from.SendMessage($"Primary: {playerPower.PrimaryStyle}  Secondary: {playerPower.SecondaryStyle}");
            from.SendMessage($"Melee: {playerPower.MeleeScore:0.0}  Archer: {playerPower.ArcherScore:0.0}  Mage: {playerPower.MageScore:0.0}");
            from.SendMessage($"Tamer: {playerPower.TamerScore:0.0}  Bard: {playerPower.BardScore:0.0}");
            from.SendMessage($"Healing: {playerPower.HealingScore:0.0}  Resist: {playerPower.MagicResistScore:0.0}");
        }
    }
}
