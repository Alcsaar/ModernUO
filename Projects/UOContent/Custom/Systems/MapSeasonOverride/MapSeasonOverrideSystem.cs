namespace Server.Custom.Systems.MapSeasonOverride;

public static class MapSeasonOverrideSystem
{
    public static void Configure()
    {
        MapSeasonOverrideService.Configure();
        MapSeasonOverrideCommands.Configure();
    }
}
