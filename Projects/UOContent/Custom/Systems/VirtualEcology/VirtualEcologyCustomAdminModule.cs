using Server.Custom.Systems.AIIntegration;
using Server.Custom.Systems.CustomAdmin;

namespace Server.Custom.Systems.VirtualEcology;

/* BEGIN CUSTOM ADMIN HUB: virtual ecology module registration exposes town chatter admin controls from the shared hub. */
public static class VirtualEcologyCustomAdminModule
{
    public static void Configure()
    {
        CustomAdminRegistry.Register(
            new CustomAdminLinkedModule(
                "virtual_ecology",
                "Virtual Ecology",
                "AI World Flavor",
                "Open virtual ecology chatter controls for generated area dialogue caches, rejected lines, recent facts, and AI-backed regeneration actions.",
                AccessLevel.GameMaster,
                300,
                from => TownChatterGump.DisplayTo(from),
                _ => AIIntegrationService.IsEnabled ? "AI enabled" : "AI disabled",
                _ =>
                [
                    $"Cached areas: {TownChatterService.Caches.Count}",
                    $"Default areas: {TownChatterService.DefaultAreas.Length}",
                    $"Recent facts: {TownChatterService.RecentFacts.Count}",
                    $"Auto top-up interval: {TownChatterService.AutoTopUpInterval.TotalMinutes:0} minute(s)"
                ]
            )
        );
    }
}
/* END CUSTOM ADMIN HUB */
