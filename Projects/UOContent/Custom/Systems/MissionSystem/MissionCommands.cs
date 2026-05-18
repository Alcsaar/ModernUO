using System;
using Server.Commands;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Custom.Systems.MissionSystem;

public static class MissionCommands
{
    public static void Configure()
    {
        CommandSystem.Register("Missions", AccessLevel.Player, Missions_OnCommand);
        CommandSystem.Register("mis", AccessLevel.Player, Missions_OnCommand);
        CommandSystem.Register("MissionStatus", AccessLevel.Player, MissionStatus_OnCommand);
        CommandSystem.Register("misstatus", AccessLevel.Player, MissionStatus_OnCommand);
        CommandSystem.Register("MissionAdmin", AccessLevel.GameMaster, MissionAdmin_OnCommand);
        CommandSystem.Register("misadmin", AccessLevel.GameMaster, MissionAdmin_OnCommand);
        CommandSystem.Register("MissionSeedDefaults", AccessLevel.GameMaster, MissionSeedDefaults_OnCommand);
        CommandSystem.Register("misseed", AccessLevel.GameMaster, MissionSeedDefaults_OnCommand);
        CommandSystem.Register("MissionReseedAll", AccessLevel.GameMaster, MissionReseedAll_OnCommand);
        CommandSystem.Register("misreseedall", AccessLevel.GameMaster, MissionReseedAll_OnCommand);
        CommandSystem.Register("MissionList", AccessLevel.GameMaster, MissionList_OnCommand);
        CommandSystem.Register("mislist", AccessLevel.GameMaster, MissionList_OnCommand);
        CommandSystem.Register("MissionResetDaily", AccessLevel.GameMaster, MissionResetDaily_OnCommand);
        CommandSystem.Register("misresetdaily", AccessLevel.GameMaster, MissionResetDaily_OnCommand);
        CommandSystem.Register("MissionResetWeekly", AccessLevel.GameMaster, MissionResetWeekly_OnCommand);
        CommandSystem.Register("misresetweekly", AccessLevel.GameMaster, MissionResetWeekly_OnCommand);
        CommandSystem.Register("MissionProgress", AccessLevel.GameMaster, MissionProgress_OnCommand);
        CommandSystem.Register("misprogress", AccessLevel.GameMaster, MissionProgress_OnCommand);
        CommandSystem.Register("AddMissionBoard", AccessLevel.GameMaster, AddMissionBoard_OnCommand);
        CommandSystem.Register("misboard", AccessLevel.GameMaster, AddMissionBoard_OnCommand);
    }

    [Usage("Missions")]
    [Aliases("mis")]
    [Description("Opens your Daily Missives and Weekly Contracts.")]
    private static void Missions_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            e.Mobile.SendMessage("Missions are only available to player characters.");
            return;
        }

        MissionSystemService.DisplayBoard(player, MissionBoardView.DailyMissives);
    }

    [Usage("MissionStatus")]
    [Aliases("misstatus")]
    [Description("Shows active Daily Missive and Weekly Contract progress.")]
    private static void MissionStatus_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            e.Mobile.SendMessage("Missions are only available to player characters.");
            return;
        }

        MissionStatusGump.DisplayTo(player);
    }

    [Usage("MissionAdmin")]
    [Aliases("misadmin")]
    [Description("Opens the mission board with staff feature-flag bypass.")]
    private static void MissionAdmin_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            e.Mobile.SendMessage("This command requires a player character.");
            return;
        }

        MissionSystemService.DisplayBoard(player, MissionBoardView.DailyMissives, 0, true);
    }

    [Usage("MissionSeedDefaults")]
    [Aliases("misseed")]
    [Description("Seeds default Daily Missive and Weekly Contract definitions.")]
    private static void MissionSeedDefaults_OnCommand(CommandEventArgs e)
    {
        var added = MissionSystemService.SeedDefaults();
        e.Mobile.SendMessage(0x35, $"Seeded {added} new mission definitions.");
    }

    [Usage("MissionReseedAll")]
    [Aliases("misreseedall")]
    [Description("Reseeds default definitions and refreshes Daily Missives and Weekly Contracts for all known players.")]
    private static void MissionReseedAll_OnCommand(CommandEventArgs e)
    {
        var changed = MissionSystemService.ReseedAndResetAll();
        e.Mobile.SendMessage(0x35, $"Mission definitions reseeded and all known player offers refreshed. Definitions changed: {changed}.");
    }

    [Usage("MissionList")]
    [Aliases("mislist")]
    [Description("Lists registered mission definitions.")]
    private static void MissionList_OnCommand(CommandEventArgs e)
    {
        var definitions = MissionSystemService.Definitions;

        if (definitions.Count == 0)
        {
            e.Mobile.SendMessage("No mission definitions are registered.");
            return;
        }

        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            e.Mobile.SendMessage($"{definition.Id}: {definition.Title} ({MissionSystemService.GetCadenceName(definition.Cadence)}, {definition.Objective?.Kind}, {definition.Reward?.Gold ?? 0} gold)");
        }
    }

    [Usage("MissionResetDaily <target/self/all>")]
    [Aliases("misresetdaily")]
    [Description("Refreshes Daily Missives for yourself, a target, or all known profiles.")]
    private static void MissionResetDaily_OnCommand(CommandEventArgs e)
    {
        HandleReset(e, MissionCadence.DailyMissive);
    }

    [Usage("MissionResetWeekly <target/self/all>")]
    [Aliases("misresetweekly")]
    [Description("Refreshes Weekly Contracts for yourself, a target, or all known profiles.")]
    private static void MissionResetWeekly_OnCommand(CommandEventArgs e)
    {
        HandleReset(e, MissionCadence.WeeklyContract);
    }

    [Usage("MissionProgress <amount>")]
    [Aliases("misprogress")]
    [Description("Adds test progress to accepted active missions on yourself.")]
    private static void MissionProgress_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            e.Mobile.SendMessage("This command requires a player character.");
            return;
        }

        var amount = e.Length > 0 ? e.GetInt32(0) : 1;
        var updated = MissionSystemService.AddProgress(player, Math.Max(1, amount), true);

        e.Mobile.SendMessage(0x35, $"Added test progress to {updated} active missions.");
        MissionSystemService.DisplayBoard(player, MissionBoardView.DailyMissives, 0, true);
    }

    [Usage("AddMissionBoard")]
    [Aliases("misboard")]
    [Description("Places a mission board at your location.")]
    private static void AddMissionBoard_OnCommand(CommandEventArgs e)
    {
        var board = new MissionBoardItem();
        board.MoveToWorld(e.Mobile.Location, e.Mobile.Map);
        e.Mobile.SendMessage(0x35, "Mission board placed.");
    }

    private static void HandleReset(CommandEventArgs e, MissionCadence cadence)
    {
        var scope = e.Length > 0 ? e.GetString(0) : "self";

        if (scope.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (cadence == MissionCadence.DailyMissive)
            {
                MissionSystemService.ResetAllDaily();
            }
            else
            {
                MissionSystemService.ResetAllWeekly();
            }

            e.Mobile.SendMessage(0x35, $"{MissionSystemService.GetCadenceName(cadence)} profiles refreshed.");
            return;
        }

        if (scope.Equals("target", StringComparison.OrdinalIgnoreCase))
        {
            e.Mobile.SendMessage($"Target a player to refresh {MissionSystemService.GetCadenceName(cadence)} offers.");
            e.Mobile.Target = new MissionResetTarget(cadence);
            return;
        }

        if (e.Mobile is PlayerMobile player)
        {
            ResetPlayer(player, cadence);
            e.Mobile.SendMessage(0x35, $"{MissionSystemService.GetCadenceName(cadence)} offers refreshed.");
        }
    }

    private static void ResetPlayer(PlayerMobile player, MissionCadence cadence)
    {
        if (cadence == MissionCadence.DailyMissive)
        {
            MissionSystemService.ResetDaily(player);
        }
        else
        {
            MissionSystemService.ResetWeekly(player);
        }
    }

    private sealed class MissionResetTarget : Target
    {
        private readonly MissionCadence _cadence;

        public MissionResetTarget(MissionCadence cadence) : base(-1, false, TargetFlags.None)
        {
            _cadence = cadence;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not PlayerMobile player)
            {
                from.SendMessage("That is not a player character.");
                return;
            }

            ResetPlayer(player, _cadence);
            from.SendMessage(0x35, $"{MissionSystemService.GetCadenceName(_cadence)} offers refreshed.");
        }
    }
}
