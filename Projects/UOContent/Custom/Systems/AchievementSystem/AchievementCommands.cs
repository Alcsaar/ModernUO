using System;
using Server;
using Server.Commands;
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
        CommandSystem.Register("AchievementStylePreview", AccessLevel.GameMaster, AchievementStylePreview_OnCommand);
        CommandSystem.Register("achstyle", AccessLevel.GameMaster, AchievementStylePreview_OnCommand);
        CommandSystem.Register("AchievementServerFirsts", AccessLevel.GameMaster, AchievementServerFirsts_OnCommand);
        CommandSystem.Register("achfirsts", AccessLevel.GameMaster, AchievementServerFirsts_OnCommand);
        CommandSystem.Register("AchievementStaffServerFirstTesting", AccessLevel.Administrator, AchievementStaffServerFirstTesting_OnCommand);
        CommandSystem.Register("achstafffirsttest", AccessLevel.Administrator, AchievementStaffServerFirstTesting_OnCommand);
        CommandSystem.Register("AchievementResetServerFirst", AccessLevel.Administrator, AchievementResetServerFirst_OnCommand);
        CommandSystem.Register("achresetfirst", AccessLevel.Administrator, AchievementResetServerFirst_OnCommand);
        CommandSystem.Register("AchievementResetServerFirstsForTesting", AccessLevel.Administrator, AchievementResetServerFirstsForTesting_OnCommand);
        CommandSystem.Register("achresetfirststest", AccessLevel.Administrator, AchievementResetServerFirstsForTesting_OnCommand);
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

    [Usage("AchievementRemove <achievementId>")]
    [Aliases("achremove")]
    [Description("Target a player and remove one achievement.")]
    private static void AchievementRemove_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 1)
        {
            e.Mobile.SendMessage(0x22, "Usage: [AchievementRemove <achievementId>");
            e.Mobile.SendMessage(0x22, "Alias: [achremove <achievementId>");
            return;
        }

        var achievementId = e.GetString(0);

        e.Mobile.SendMessage($"Target a player to remove achievement '{achievementId}'.");
        e.Mobile.Target = new AchievementRemoveTarget(achievementId);
    }

    [Usage("AchievementStylePreview")]
    [Aliases("achstyle")]
    [Description("Opens an in-game preview of achievement journal background styles.")]
    private static void AchievementStylePreview_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            e.Mobile.SendMessage("The preview gump is only available to player characters.");
            return;
        }

        AchievementStylePreviewGump.DisplayTo(player);
    }

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
    [Aliases("achstafffirsttest")]
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
    [Aliases("achresetfirststest")]
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

            if (
                !AchievementService.TryRemoveAchievement(
                    player,
                    _achievementId,
                    out var definition,
                    out var removedUnlock,
                    out var removedProgress
                )
            )
            {
                from.SendMessage(0x22, $"Unknown achievement id '{_achievementId}'.");
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
}
