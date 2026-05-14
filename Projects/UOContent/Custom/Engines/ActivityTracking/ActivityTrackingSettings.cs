namespace Server.Custom.Engines.ActivityTracking;

public static class ActivityTrackingSettings
{
    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: keep activity tracking runtime-only with no config persistence */
    public static void Configure()
    {
        // Activity tracking is intentionally in-memory only.
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */
}
