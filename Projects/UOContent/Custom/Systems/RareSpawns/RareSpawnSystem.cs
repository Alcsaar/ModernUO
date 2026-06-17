namespace Server.Custom.Systems.RareSpawns;

public static class RareSpawnSystem
{
    public static void Configure()
    {
        RareSpawnManager.Configure();
        RareSpawnCustomAdminModule.Configure();
    }

    public static void Initialize()
    {
        RareSpawnManager.Initialize();
    }
}
