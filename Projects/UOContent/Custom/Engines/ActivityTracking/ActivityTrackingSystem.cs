namespace Server.Custom.Engines.ActivityTracking;

public static class ActivityTrackingSystem
{
    public static void Configure()
    {
        ActivityTrackingService.Configure();
        ActivityTrackingCommand.Configure();
    }

    public static void Initialize()
    {
        ActivityTrackingService.Initialize();
    }
}
