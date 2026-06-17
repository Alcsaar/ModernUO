using Server.Custom.Systems.CustomAdmin;

namespace Server.Custom.Systems.CustomFeatureFlags;

/* BEGIN CUSTOM ADMIN HUB: feature flag module registration exposes the existing flag gump from the shared admin hub. */
public static class CustomFeatureFlagCustomAdminModule
{
    public static void Configure()
    {
        CustomAdminRegistry.Register(
            new CustomAdminLinkedModule(
                "custom_feature_flags",
                "Custom Feature Flags",
                "Configuration",
                "Inspect, toggle, and save custom feature flags that gate custom shard systems.",
                AccessLevel.Administrator,
                50,
                from => CustomFeatureFlagAdminGump.DisplayTo(from),
                _ => "Administrator only",
                _ =>
                {
                    var statuses = CustomFeatureFlagManager.GetAllStatuses(includeHidden: true);
                    var enabled = 0;

                    for (var i = 0; i < statuses.Count; i++)
                    {
                        if (statuses[i].EffectiveEnabled)
                        {
                            enabled++;
                        }
                    }

                    return
                    [
                        $"Registered flags: {statuses.Count}",
                        $"Effectively enabled: {enabled}",
                        $"Config: {CustomFeatureFlagManager.ConfigPath}"
                    ];
                }
            )
        );
    }
}
/* END CUSTOM ADMIN HUB */
