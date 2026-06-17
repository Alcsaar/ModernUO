namespace Server.Custom.Systems.VirtualEcology;

public static class VirtualEcologySystem
{
    public static void Configure()
    {
        TownChatterCommands.Configure();
        TownChatterService.Configure();
        TownChatterPersistence.Configure();
    }
}
