using Server.Custom.Systems.CustomAdmin;

namespace Server.Custom.Systems.AchievementSystem;

/* BEGIN CUSTOM ADMIN HUB: achievement module registration lets the shared admin gump open existing achievement controls. */
public static class AchievementCustomAdminModule
{
    public static void Configure()
    {
        CustomAdminRegistry.Register(
            new CustomAdminLinkedModule(
                "achievements",
                "Achievements",
                "Player Progression",
                "Review and operate achievement staff controls, including player resets, manual grants, removals, server-first claims, and system toggles.",
                AccessLevel.GameMaster,
                100,
                AchievementAdminGump.DisplayTo,
                _ => AchievementService.IsSystemEnabled() ? "Enabled" : "Disabled",
                from =>
                [
                    $"Definitions: {AchievementService.GetDefinitionCount()}",
                    $"Server first claims: {AchievementService.GetServerFirstRecords().Count}",
                    $"Staff first testing: {(AchievementService.AllowStaffServerFirstsForTesting ? "On" : "Off")}",
                    from.AccessLevel >= AccessLevel.Administrator ? "Administrator actions available" : "GameMaster actions available"
                ]
            )
        );
    }
}
/* END CUSTOM ADMIN HUB */
