using Server.Commands;

namespace Server.Custom.Systems.CustomAdmin;

/* BEGIN CUSTOM ADMIN HUB: staff command opens the shared modular admin gump. */
public static class CustomAdminCommands
{
    public static void Configure()
    {
        CommandSystem.Register("CustomAdmin", AccessLevel.GameMaster, CustomAdmin_OnCommand);
        CommandSystem.Register("CAdmin", AccessLevel.GameMaster, CustomAdmin_OnCommand);
        CommandSystem.Register("AdminUI", AccessLevel.GameMaster, CustomAdmin_OnCommand);
    }

    [Usage("CustomAdmin [module]")]
    [Aliases("CAdmin", "AdminUI")]
    [Description("Open the modular custom systems admin gump.")]
    private static void CustomAdmin_OnCommand(CommandEventArgs e)
    {
        CustomAdminGump.DisplayTo(e.Mobile, e.ArgString?.Trim());
    }
}
/* END CUSTOM ADMIN HUB */
