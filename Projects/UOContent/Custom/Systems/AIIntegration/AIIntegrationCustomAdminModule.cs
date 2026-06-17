using Server.Custom.Systems.CustomAdmin;
using Server.Custom.Systems.CustomFeatureFlags;

namespace Server.Custom.Systems.AIIntegration;

/* BEGIN CUSTOM ADMIN HUB: AI module registration gives the shared admin gump a status panel for AI integration settings. */
public static class AIIntegrationCustomAdminModule
{
    public static void Configure()
    {
        CustomAdminRegistry.Register(
            new CustomAdminLinkedModule(
                "ai_integration",
                "AI Integration",
                "Infrastructure",
                "Review AI integration state and configured Ollama model settings used by staff tools and virtual ecology features.",
                AccessLevel.GameMaster,
                250,
                null,
                _ => AIIntegrationService.IsEnabled ? "Enabled" : "Disabled",
                _ =>
                [
                    $"Flag: {(CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.AIIntegration) ? "On" : "Off")}",
                    $"Endpoint: {AIIntegrationSettings.OllamaEndpoint}",
                    $"Staff model: {AIIntegrationSettings.StaffModel}",
                    $"Chatter model: {AIIntegrationSettings.ChatterModel}",
                    $"Timeout: {AIIntegrationSettings.RequestTimeout.TotalSeconds:0} second(s)"
                ]
            )
        );
    }
}
/* END CUSTOM ADMIN HUB */
