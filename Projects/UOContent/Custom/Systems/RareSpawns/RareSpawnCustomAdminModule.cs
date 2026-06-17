using Server.Custom.Systems.CustomAdmin;

namespace Server.Custom.Systems.RareSpawns;

/* BEGIN CUSTOM ADMIN HUB: rare spawn module registration lets the shared admin gump open existing rare spawn controls. */
public static class RareSpawnCustomAdminModule
{
    public static void Configure()
    {
        CustomAdminRegistry.Register(
            new CustomAdminLinkedModule(
                "rare_spawns",
                "Rare Spawns",
                "World Content",
                "Create, edit, toggle, force-spawn, teleport to, and delete rare spawn points from the existing rare spawn admin surface.",
                AccessLevel.GameMaster,
                200,
                from => RareSpawnAdminGump.DisplayTo(from),
                _ => RareSpawnManager.IsEnabled() ? "Enabled" : "Disabled",
                _ =>
                [
                    $"Spawn points: {RareSpawnManager.GetSpawnPoints().Length}",
                    "Supports create/edit, force spawn, toggle, go, and delete actions",
                    "Export/import remains available through rare spawn admin commands"
                ]
            )
        );
    }
}
/* END CUSTOM ADMIN HUB */
