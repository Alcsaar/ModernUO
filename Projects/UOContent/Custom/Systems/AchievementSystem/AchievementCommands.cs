using System;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Custom.Systems.AchievementSystem;

public static class AchievementCommands
{
    public static void Configure()
    {
        CommandSystem.Register("Achievements", AccessLevel.Player, Achievements_OnCommand);
        CommandSystem.Register("ach", AccessLevel.Player, Achievements_OnCommand);
        CommandSystem.Register("AchievementRefresh", AccessLevel.Player, AchievementRefresh_OnCommand);
        CommandSystem.Register("achrefresh", AccessLevel.Player, AchievementRefresh_OnCommand);
        CommandSystem.Register("AchievementReset", AccessLevel.GameMaster, AchievementReset_OnCommand);
        CommandSystem.Register("achreset", AccessLevel.GameMaster, AchievementReset_OnCommand);
        CommandSystem.Register("AchievementRemove", AccessLevel.GameMaster, AchievementRemove_OnCommand);
        CommandSystem.Register("achremove", AccessLevel.GameMaster, AchievementRemove_OnCommand);
        CommandSystem.Register("AchievementAdd", AccessLevel.GameMaster, AchievementAdd_OnCommand);
        CommandSystem.Register("achadd", AccessLevel.GameMaster, AchievementAdd_OnCommand);
        CommandSystem.Register("AchievementAdmin", AccessLevel.GameMaster, AchievementAdmin_OnCommand);
        CommandSystem.Register("achadmin", AccessLevel.GameMaster, AchievementAdmin_OnCommand);
        CommandSystem.Register("AchievementToggle", AccessLevel.Administrator, AchievementToggle_OnCommand);
        CommandSystem.Register("achtoggle", AccessLevel.Administrator, AchievementToggle_OnCommand);
        CommandSystem.Register("AchievementServerFirsts", AccessLevel.Administrator, AchievementServerFirsts_OnCommand);
        CommandSystem.Register("achfirsts", AccessLevel.Administrator, AchievementServerFirsts_OnCommand);
        CommandSystem.Register("AchievementStaffServerFirstTesting", AccessLevel.Administrator, AchievementStaffServerFirstTesting_OnCommand);
        CommandSystem.Register("achstafffirst", AccessLevel.Administrator, AchievementStaffServerFirstTesting_OnCommand);
        CommandSystem.Register("AchievementResetServerFirst", AccessLevel.Administrator, AchievementResetServerFirst_OnCommand);
        CommandSystem.Register("achresetfirst", AccessLevel.Administrator, AchievementResetServerFirst_OnCommand);
        CommandSystem.Register("AchievementResetServerFirstsForTesting", AccessLevel.Administrator, AchievementResetServerFirstsForTesting_OnCommand);
        CommandSystem.Register("achfirstclear", AccessLevel.Administrator, AchievementResetServerFirstsForTesting_OnCommand);
        CommandSystem.Register("AchievementResetAll", AccessLevel.Administrator, AchievementResetAll_OnCommand);
        CommandSystem.Register("achresetall", AccessLevel.Administrator, AchievementResetAll_OnCommand);
    }

    [Usage("Achievements")]
    [Aliases("ach")]
    [Description("Opens the achievements journal.")]
    private static void Achievements_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            e.Mobile.SendMessage("Achievements are only available to player characters.");
            return;
        }

        AchievementService.DisplayAchievementGump(player);
    }

    [Usage("AchievementRefresh")]
    [Aliases("achrefresh")]
    [Description("Re-evaluates your achievements from current live data.")]
    private static void AchievementRefresh_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            e.Mobile.SendMessage("This command requires a player character.");
            return;
        }

        if (!AchievementService.IsSystemEnabled())
        {
            e.Mobile.SendMessage(0x22, "Achievement system is disabled.");
            return;
        }

        AchievementService.EvaluatePlayer(player);
        e.Mobile.SendMessage(0x35, "Achievement progress refreshed.");
    }

    [Usage("AchievementReset")]
    [Aliases("achreset")]
    [Description("Target a player and reset achievement state.")]
    private static void AchievementReset_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target a player to reset achievements.");
        e.Mobile.Target = new AchievementResetTarget();
    }

    [Usage("AchievementRemove [achievementId]")]
    [Aliases("achremove")]
    [Description("Target a player and remove one achievement, or pick from that player's earned list.")]
    private static void AchievementRemove_OnCommand(CommandEventArgs e)
    {
        if (!AchievementService.IsSystemEnabled())
        {
            e.Mobile.SendMessage(0x22, "Achievement system is disabled.");
            return;
        }

        if (e.Length < 1)
        {
            e.Mobile.SendMessage("Target a player to choose an earned achievement to remove.");
            e.Mobile.Target = new AchievementRemoveTarget(null);
            return;
        }

        var achievementId = e.GetString(0);

        e.Mobile.SendMessage($"Target a player to remove achievement '{achievementId}'.");
        e.Mobile.Target = new AchievementRemoveTarget(achievementId);
    }

    [Usage("AchievementAdd [achievementId]")]
    [Aliases("achadd")]
    [Description("Target a player and grant one achievement, or pick from that player's unearned list.")]
    private static void AchievementAdd_OnCommand(CommandEventArgs e)
    {
        if (!AchievementService.IsSystemEnabled())
        {
            e.Mobile.SendMessage(0x22, "Achievement system is disabled.");
            return;
        }

        if (e.Length < 1)
        {
            e.Mobile.SendMessage("Target a player to choose an unearned achievement to grant.");
            e.Mobile.Target = new AchievementGrantTarget(null);
            return;
        }

        var achievementId = e.GetString(0);

        e.Mobile.SendMessage($"Target a player to grant achievement '{achievementId}'.");
        e.Mobile.Target = new AchievementGrantTarget(achievementId);
    }

    [Usage("AchievementAdmin")]
    [Aliases("achadmin")]
    [Description("Opens the achievement staff control panel.")]
    private static void AchievementAdmin_OnCommand(CommandEventArgs e)
    {
        AchievementAdminGump.DisplayTo(e.Mobile);
    }

    /* BEGIN ACHIEVEMENT FEATURE FLAG: administrator command controls the custom feature flag in game */
    [Usage("AchievementToggle [on|off|toggle|status]")]
    [Aliases("achtoggle")]
    [Description("Gets or changes the achievement system custom feature flag.")]
    private static void AchievementToggle_OnCommand(CommandEventArgs e)
    {
        var action = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "toggle";
        var changed = false;

        switch (action)
        {
            case "on":
            case "enable":
            case "enabled":
            case "true":
            case "1":
                changed = AchievementService.TrySetSystemEnabled(true, e.Mobile, out var enableReason);
                SendFeatureFlagResult(e.Mobile, changed, enableReason);
                return;
            case "off":
            case "disable":
            case "disabled":
            case "false":
            case "0":
                changed = AchievementService.TrySetSystemEnabled(false, e.Mobile, out var disableReason);
                SendFeatureFlagResult(e.Mobile, changed, disableReason);
                return;
            case "toggle":
                changed = AchievementService.TryToggleSystemEnabled(e.Mobile, out var toggleReason);
                SendFeatureFlagResult(e.Mobile, changed, toggleReason);
                return;
            case "status":
            case "info":
                SendFeatureFlagStatus(e.Mobile);
                return;
            default:
                e.Mobile.SendMessage(0x22, "Usage: [AchievementToggle [on|off|toggle|status]");
                e.Mobile.SendMessage(0x22, "Alias: [achtoggle [on|off|toggle|status]");
                return;
        }
    }

    private static void SendFeatureFlagResult(Mobile from, bool changed, string failureReason)
    {
        if (!changed)
        {
            from.SendMessage(0x22, failureReason ?? "Unable to update achievement system flag.");
            return;
        }

        SendFeatureFlagStatus(from);
    }

    private static void SendFeatureFlagStatus(Mobile from)
    {
        var status = AchievementService.GetSystemFlagStatus();

        if (status == null)
        {
            from.SendMessage(0x22, "Achievement system flag is not registered.");
            return;
        }

        from.SendMessage(
            status.EffectiveEnabled ? 0x35 : 0x22,
            $"Achievement system is {(status.EffectiveEnabled ? "enabled" : "disabled")}."
        );
        from.SendMessage($"Stored: {(status.StoredEnabled ? "ON" : "OFF")}; Default: {(status.DefaultEnabled ? "ON" : "OFF")}.");
    }
    /* END ACHIEVEMENT FEATURE FLAG */

    /* BEGIN ACHIEVEMENT SERVER FIRSTS: staff commands inspect and correct shard-first claim state */
    [Usage("AchievementServerFirsts")]
    [Aliases("achfirsts")]
    [Description("Lists claimed server-first achievements.")]
    private static void AchievementServerFirsts_OnCommand(CommandEventArgs e)
    {
        var records = AchievementService.GetServerFirstRecords();

        if (records.Count == 0)
        {
            e.Mobile.SendMessage(0x35, "No server-first achievements have been claimed.");
            return;
        }

        e.Mobile.SendMessage(0x35, $"Server-first claims: {records.Count}");

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            e.Mobile.SendMessage(
                0x35,
                $"{record.AchievementId}: {record.PlayerName} - {record.SkillDisplayName} - {record.AchievedUtc:yyyy-MM-dd HH:mm} UTC"
            );
        }
    }

    [Usage("AchievementStaffServerFirstTesting [true|false]")]
    [Aliases("achstafffirst")]
    [Description("Gets or sets whether staff accounts can earn server-first achievements for testing.")]
    private static void AchievementStaffServerFirstTesting_OnCommand(CommandEventArgs e)
    {
        if (e.Length > 0)
        {
            AchievementService.AllowStaffServerFirstsForTesting = e.GetBoolean(0);
        }

        e.Mobile.SendMessage(
            0x35,
            $"Staff server-first testing is {(AchievementService.AllowStaffServerFirstsForTesting ? "enabled" : "disabled")}."
        );
    }

    [Usage("AchievementResetServerFirst <skillName|achievementId>")]
    [Aliases("achresetfirst")]
    [Description("Resets one claimed server-first achievement.")]
    private static void AchievementResetServerFirst_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 1)
        {
            e.Mobile.SendMessage(0x22, "Usage: [AchievementResetServerFirst <skillName|achievementId>");
            e.Mobile.SendMessage(0x22, "Alias: [achresetfirst <skillName|achievementId>");
            return;
        }

        var input = e.GetString(0);
        AchievementServerFirstRecord removed = null;
        AchievementServerFirstRecord promoted = null;

        if (Enum.TryParse(input, true, out SkillName skill))
        {
            AchievementService.TryResetServerFirstSkill(skill, out removed, out promoted);
        }
        else
        {
            AchievementService.TryResetServerFirst(input, out removed, out promoted);
        }

        if (removed == null)
        {
            e.Mobile.SendMessage(0x22, $"No server-first claim found for '{input}'.");
            return;
        }

        e.Mobile.SendMessage(
            0x35,
            $"Reset {removed.AchievementId}, previously claimed by {removed.PlayerName}."
        );

        if (promoted != null)
        {
            e.Mobile.SendMessage(
                0x35,
                $"Promoted next eligible claim: {promoted.PlayerName} - {promoted.SkillDisplayName} - {promoted.AchievedUtc:yyyy-MM-dd HH:mm} UTC."
            );
        }
        else
        {
            e.Mobile.SendMessage(0x35, "No eligible next claim was found.");
        }
    }

    [Usage("AchievementResetServerFirstsForTesting")]
    [Aliases("achfirstclear")]
    [Description("Clears all server-first claims and candidate history without disqualifying anyone.")]
    private static void AchievementResetServerFirstsForTesting_OnCommand(CommandEventArgs e)
    {
        AchievementService.ResetServerFirstsForTesting(
            out var clearedClaims,
            out var clearedCandidateGroups,
            out var clearedPlayerEntries
        );

        e.Mobile.SendMessage(
            0x35,
            $"Cleared {clearedClaims} server-first claims, {clearedCandidateGroups} candidate groups, and {clearedPlayerEntries} player unlock/progress entries without disqualifying anyone."
        );
    }
    /* END ACHIEVEMENT SERVER FIRSTS */

    [Usage("AchievementResetAll")]
    [Aliases("achresetall")]
    [Description("Resets achievement state for all players.")]
    private static void AchievementResetAll_OnCommand(CommandEventArgs e)
    {
        AchievementService.ResetAllPlayers();
        e.Mobile.SendMessage(0x35, "Achievement state has been reset for all players.");
    }

    private sealed class AchievementResetTarget : Target
    {
        public AchievementResetTarget() : base(-1, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not PlayerMobile player)
            {
                from.SendMessage(0x22, "That is not a player.");
                return;
            }

            AchievementService.ResetPlayer(player);

            from.SendMessage(0x35, $"Reset achievements for {player.Name}.");
            player.SendMessage(0x35, "Your achievements were reset by staff.");
        }
    }

    /* BEGIN ACHIEVEMENT SYSTEM CUSTOMIZATION: staff command target for pruning one achievement from a character */
    private sealed class AchievementRemoveTarget : Target
    {
        private readonly string _achievementId;

        public AchievementRemoveTarget(string achievementId) : base(-1, false, TargetFlags.None)
        {
            _achievementId = achievementId;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not PlayerMobile player)
            {
                from.SendMessage(0x22, "That is not a player.");
                return;
            }

            if (!AchievementService.IsSystemEnabled())
            {
                from.SendMessage(0x22, "Achievement system is disabled.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_achievementId))
            {
                AchievementAdminAchievementListGump.DisplayTo(from, player, AchievementAdminAchievementMode.Remove, 0);
                return;
            }

            if (
                !AchievementService.TryRemoveAchievement(
                    from,
                    player,
                    _achievementId,
                    out var definition,
                    out var removedUnlock,
                    out var removedProgress,
                    out var failureReason
                )
            )
            {
                from.SendMessage(0x22, failureReason);
                return;
            }

            var changedText = removedUnlock || removedProgress
                ? "Removed saved unlock/progress"
                : "No saved unlock/progress existed";

            from.SendMessage(
                0x35,
                $"{changedText} for {definition.Id} ({definition.Name}) on {player.Name}."
            );
            player.SendMessage(0x35, $"Achievement '{definition.Name}' was removed by staff.");
        }
    }
    /* END ACHIEVEMENT SYSTEM CUSTOMIZATION */

    /* BEGIN ACHIEVEMENT ADMIN CONTROLS: target helper for granting one achievement or opening the grant picker */
    private sealed class AchievementGrantTarget : Target
    {
        private readonly string _achievementId;

        public AchievementGrantTarget(string achievementId) : base(-1, false, TargetFlags.None)
        {
            _achievementId = achievementId;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not PlayerMobile player)
            {
                from.SendMessage(0x22, "That is not a player.");
                return;
            }

            if (!AchievementService.IsSystemEnabled())
            {
                from.SendMessage(0x22, "Achievement system is disabled.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_achievementId))
            {
                AchievementAdminAchievementListGump.DisplayTo(from, player, AchievementAdminAchievementMode.Grant, 0);
                return;
            }

            if (!AchievementService.TryGrantAchievement(from, player, _achievementId, out var definition, out var failureReason))
            {
                from.SendMessage(0x22, failureReason);
                return;
            }

            from.SendMessage(0x35, $"Granted {definition.Id} ({definition.Name}) to {player.Name}.");
            player.SendMessage(0x35, $"Achievement '{definition.Name}' was granted by staff.");
        }
    }
    /* END ACHIEVEMENT ADMIN CONTROLS */
}
