namespace Server.Custom.Systems.RareSpawns;

public static class RareSpawnSystem
{
    public static void Configure()
    {
        RareSpawnManager.Configure();
    }

    public static void Initialize()
    {
        RareSpawnManager.Initialize();
    }
}
