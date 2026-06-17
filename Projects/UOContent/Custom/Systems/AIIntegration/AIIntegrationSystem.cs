namespace Server.Custom.Systems.AIIntegration;

public static class AIIntegrationSystem
{
    public static void Configure()
    {
        AIIntegrationService.Configure();
        AIIntegrationCustomAdminModule.Configure();
    }
}
