using System;
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
