using Server.Commands;
using Server.Mobiles;

namespace Server.Custom.Systems.LaunchAudit;

public static class LaunchAuditCommands
{
    public static void Configure()
    {
        CommandSystem.Register("LaunchAudit", AccessLevel.Administrator, LaunchAudit_OnCommand);
        CommandSystem.Register("LAudit", AccessLevel.Administrator, LaunchAudit_OnCommand);
    }

    [Usage("LaunchAudit")]
    [Aliases("LAudit")]
    [Description("Opens the launch wipe audit gump.")]
    private static void LaunchAudit_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            e.Mobile.SendMessage("This command requires a player character.");
            return;
        }

        LaunchAuditGump.DisplayTo(player);
    }
}
