using Server.Commands;
using Server.Mobiles;

namespace Server.Custom.Systems.MapSeasonOverride;

public static class MapSeasonOverrideCommands
{
    public static void Configure()
    {
        CommandSystem.Register("Season", AccessLevel.Administrator, Season_OnCommand);
        CommandSystem.Register("SeasonRefresh", AccessLevel.Administrator, SeasonRefresh_OnCommand);
        CommandSystem.Register("srefresh", AccessLevel.Administrator, SeasonRefresh_OnCommand);
    }

    [Usage("Season")]
    [Description("Opens the map season override gump.")]
    private static void Season_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            e.Mobile.SendMessage("This command requires a player character.");
            return;
        }

        MapSeasonOverrideGump.DisplayTo(player);
    }

    [Usage("SeasonRefresh")]
    [Aliases("srefresh")]
    [Description("Forces connected clients on your current map to refresh visible season terrain.")]
    private static void SeasonRefresh_OnCommand(CommandEventArgs e)
    {
        var map = e.Mobile?.Map;

        if (map == null || map == Map.Internal)
        {
            e.Mobile?.SendMessage(0x22, "You are not on a valid map.");
            return;
        }

        var count = MapSeasonOverrideService.RefreshSeasonForPlayers(map);
        e.Mobile.SendMessage(0x35, $"Season refresh sent to {count:N0} client{(count == 1 ? string.Empty : "s")} on {map.Name}.");
    }
}
