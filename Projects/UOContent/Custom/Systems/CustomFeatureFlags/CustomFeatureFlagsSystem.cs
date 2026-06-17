namespace Server.Custom.Systems.CustomFeatureFlags;

public static class CustomFeatureFlagsSystem
{
    public static void Configure()
    {
        CustomFeatureFlagBootstrap.Configure();
        CustomFeatureFlagCustomAdminModule.Configure();
    }

    public static void Initialize()
    {
    }
}
