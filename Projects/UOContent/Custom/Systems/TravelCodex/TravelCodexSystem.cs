namespace Server.Custom.Systems.TravelCodex;

public static class TravelCodexSystem
{
    public static void Configure()
    {
        TravelCodexManager.Configure();
    }

    public static void Initialize()
    {
        TravelCodexManager.Initialize();
    }
}
