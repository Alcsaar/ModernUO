using System;
using Server.Targeting;

namespace Server.Custom.Systems.RareSpawns;

public static class RareSpawnCommands
{
    public static void Configure()
    {
        CommandSystem.Register("RareSpawnAdd", AccessLevel.GameMaster, RareSpawnAdd_OnCommand);
        CommandSystem.Register("rsadd", AccessLevel.GameMaster, RareSpawnAdd_OnCommand);
        CommandSystem.Register("RareSpawnCheck", AccessLevel.GameMaster, RareSpawnCheck_OnCommand);
        CommandSystem.Register("rscheck", AccessLevel.GameMaster, RareSpawnCheck_OnCommand);
        CommandSystem.Register("RareSpawnForce", AccessLevel.GameMaster, RareSpawnForce_OnCommand);
        CommandSystem.Register("rsforce", AccessLevel.GameMaster, RareSpawnForce_OnCommand);
        CommandSystem.Register("RareSpawnReset", AccessLevel.GameMaster, RareSpawnReset_OnCommand);
        CommandSystem.Register("rsreset", AccessLevel.GameMaster, RareSpawnReset_OnCommand);
        CommandSystem.Register("RareSpawnStatus", AccessLevel.GameMaster, RareSpawnStatus_OnCommand);
        CommandSystem.Register("rsstatus", AccessLevel.GameMaster, RareSpawnStatus_OnCommand);
        CommandSystem.Register("RareSpawnToggle", AccessLevel.GameMaster, RareSpawnToggle_OnCommand);
        CommandSystem.Register("rstoggle", AccessLevel.GameMaster, RareSpawnToggle_OnCommand);
    }

    [Usage("RareSpawnAdd <itemType> <profile> <display name>")]
    [Aliases("rsadd")]
    [Description("Create a hidden rare spawn point at a targeted location.")]
    private static void RareSpawnAdd_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (e.Length < 3)
        {
            from.SendMessage("Usage: [RareSpawnAdd <itemType> <ServerBirth|Daily|Weekly|Monthly|Custom> <display name>");
            return;
        }

        var typeName = e.GetString(0);
        if (!RareSpawnManager.TryResolveItemType(typeName, out _))
        {
            from.SendMessage(0x22, $"'{typeName}' is not a valid item type.");
            return;
        }

        if (!Enum.TryParse<RareRespawnProfile>(e.GetString(1), true, out var profile))
        {
            from.SendMessage(0x22, "Unknown profile. Valid: ServerBirth, Daily, Weekly, Monthly, Custom.");
            return;
        }

        var displayName = string.Join(" ", e.Arguments, 2, e.Arguments.Length - 2).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            from.SendMessage(0x22, "Display name is required.");
            return;
        }

        from.SendMessage("Target the world location where this rare should spawn.");
        from.Target = new AddRareSpawnTarget(typeName, profile, displayName);
    }

    private sealed class AddRareSpawnTarget : Target
    {
        private readonly string _typeName;
        private readonly RareRespawnProfile _profile;
        private readonly string _displayName;

        public AddRareSpawnTarget(string typeName, RareRespawnProfile profile, string displayName)
            : base(-1, true, TargetFlags.None)
        {
            _typeName = typeName;
            _profile = profile;
            _displayName = displayName;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (!TryGetTargetLocation(from, targeted, out var point, out var map))
            {
                from.SendMessage(0x22, "That is not a valid world location.");
                return;
            }

            var spawnPoint = new RareSpawnPoint
            {
                SpawnTypeName = _typeName,
                DisplayName = _displayName,
                RespawnProfile = _profile
            };

            spawnPoint.MoveToWorld(point, map);
            spawnPoint.Enabled = true;
            spawnPoint.ScheduleNextSpawn(true);
            spawnPoint.CheckRespawn();
            from.SendMessage(0x35, $"Rare spawn point '{_displayName}' created and enabled.");
        }
    }

    [Usage("RareSpawnCheck")]
    [Aliases("rscheck")]
    [Description("Run the rare spawn controller check immediately.")]
    private static void RareSpawnCheck_OnCommand(CommandEventArgs e)
    {
        RareSpawnManager.CheckAll();
        e.Mobile.SendMessage(0x35, "Rare spawn check complete.");
    }

    [Usage("RareSpawnForce")]
    [Aliases("rsforce")]
    [Description("Target a rare spawn point and force its rare to spawn now.")]
    private static void RareSpawnForce_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target a rare spawn point to force.");
        e.Mobile.Target = new RareSpawnPointTarget((from, point) =>
        {
            if (point.ForceRespawn(out var reason))
            {
                from.SendMessage(0x35, "Rare force-respawned.");
            }
            else
            {
                from.SendMessage(0x22, reason ?? "Rare could not be spawned.");
            }
        });
    }

    [Usage("RareSpawnReset")]
    [Aliases("rsreset")]
    [Description("Target a rare spawn point and reset its schedule. ServerBirth points are made available again.")]
    private static void RareSpawnReset_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target a rare spawn point to reset.");
        e.Mobile.Target = new RareSpawnPointTarget((from, point) =>
        {
            point.ResetSchedule(true);
            from.SendMessage(0x35, "Rare spawn schedule reset.");
        });
    }

    [Usage("RareSpawnStatus")]
    [Aliases("rsstatus")]
    [Description("Show all registered rare spawn points.")]
    private static void RareSpawnStatus_OnCommand(CommandEventArgs e)
    {
        RareSpawnAdminGump.DisplayTo(e.Mobile);
    }

    [Usage("RareSpawnToggle")]
    [Aliases("rstoggle")]
    [Description("Target a rare spawn point and toggle it enabled/disabled.")]
    private static void RareSpawnToggle_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target a rare spawn point to toggle.");
        e.Mobile.Target = new RareSpawnPointTarget((from, point) =>
        {
            point.Enabled = !point.Enabled;
            from.SendMessage(0x35, $"Rare spawn point enabled: {point.Enabled}");
        });
    }

    private sealed class RareSpawnPointTarget : Target
    {
        private readonly Action<Mobile, RareSpawnPoint> _action;

        public RareSpawnPointTarget(Action<Mobile, RareSpawnPoint> action) : base(-1, false, TargetFlags.None)
        {
            _action = action;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not RareSpawnPoint point)
            {
                from.SendMessage(0x22, "That is not a rare spawn point.");
                return;
            }

            _action(from, point);
        }
    }

    private static bool TryGetTargetLocation(Mobile from, object targeted, out Point3D point, out Map map)
    {
        point = Point3D.Zero;
        map = null;

        if (targeted is not IPoint3D ip)
        {
            return false;
        }

        point = new Point3D(ip);
        map = from.Map;

        if (targeted is Item item)
        {
            point = item.GetWorldLocation();
            map = item.Map;
        }
        else if (targeted is Mobile mobile)
        {
            point = mobile.Location;
            map = mobile.Map;
        }
        else if (targeted is LandTarget land)
        {
            point = land.Location;
        }
        else if (targeted is StaticTarget st)
        {
            point = st.Location;
        }

        return map != null && map != Map.Internal;
    }
}
