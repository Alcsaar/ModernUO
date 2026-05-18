namespace Server.Custom.Systems.CustomFeatureFlags;

public static class CustomFeatureFlagBootstrap
{
    public static void Configure()
    {
        CustomFeatureFlagManager.Configure();

        CustomFeatureFlagManager.Register(
            CustomFeatureFlagKeys.AchievementSystem,
            "Achievement System",
            "Tracks and unlocks player achievements",
            "Custom Systems",
            defaultEnabled: true
        );

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
            CustomFeatureFlagKeys.HarvestingAutomation,
            "Harvesting Automation",
            "Automatically repeats mining, lumberjacking, and fishing on the same harvest node",
            "Custom Systems",
            defaultEnabled: true
        );

        CustomFeatureFlagManager.Register(
            CustomFeatureFlagKeys.TravelCodex,
            "Travel Codex",
            "Non-magery codex travel system",
            "Custom Systems",
            defaultEnabled: true
        );

        CustomFeatureFlagManager.Register(
            CustomFeatureFlagKeys.SupplyStones,
            "Supply Stones",
            "Player access to supply stones that generate resource bags",
            "Special Systems",
            defaultEnabled: false
        );

        CustomFeatureFlagManager.Register(
            CustomFeatureFlagKeys.RareSpawns,
            "Rare Spawns",
            "Controls timed rare item spawns in the world",
            "Custom Systems",
            defaultEnabled: true
        );
    }
}
