namespace Server.Custom.Systems.CustomFeatureFlags;

public static class CustomFeatureFlagBootstrap
{
    public static void Configure()
    {
        CustomFeatureFlagManager.Configure();

        CustomFeatureFlagManager.Register(
            CustomFeatureFlagKeys.TemplateSaver,
            "Template Saver",
            "Skill Stat Templates",
            "Custom Systems",
            defaultEnabled: true
        );

        CustomFeatureFlagManager.Register(
            CustomFeatureFlagKeys.RelativeThreat,
            "Relative Threat",
            "Overhead Threat Display",
            "Custom Systems",
            defaultEnabled: true
        );

        CustomFeatureFlagManager.Register(
            CustomFeatureFlagKeys.CreatureAutoDispel,
            "Creature Auto Dispel",
            "Auto Dispel",
            "Combat AI",
            defaultEnabled: false

        );
        CustomFeatureFlagManager.Register(
            CustomFeatureFlagKeys.TravelCodex,
            "Travel Codex",
            "Non-magery codex travel system",
            "Custom Systems",
            defaultEnabled: true
);
    }
}
