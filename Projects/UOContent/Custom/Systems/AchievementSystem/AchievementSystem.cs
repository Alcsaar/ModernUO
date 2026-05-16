namespace Server.Custom.Systems.AchievementSystem;

public static class AchievementSystem
{
    public static void Configure()
    {
        AchievementService.Configure();
    }

    public static void Initialize()
    {
        AchievementService.Initialize();
    }
}
