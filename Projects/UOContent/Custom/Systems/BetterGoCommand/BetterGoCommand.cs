using Server.Commands;
using Server.Commands.Generic;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Custom.Systems.BetterGoCommand;

public static class BetterGoCommand
{
    public static void Configure()
    {
        CommandSystem.Register("Go", AccessLevel.Counselor, Go_OnCommand);
        CommandSystem.Register("BetterGo", AccessLevel.Counselor, Go_OnCommand);
        CommandSystem.Register("BGo", AccessLevel.Counselor, Go_OnCommand);
    }

    [Usage("Go [target | serial | map | region name | x y [z] [map] | map x y [z] | deg min N/S deg min E/W]")]
    [Description("Moves staff to a targeted object, serial, map, region, coordinate, or sextant coordinate.")]
    private static void Go_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        /*
         * Preserve the familiar no-argument Go menu and add an explicit target mode.
         * Target mode is the safest option when the destination is visible but awkward to describe.
         */
        if (e.Length == 0)
        {
            BetterGoGump.DisplayTo(from);
            return;
        }

        if (IsTargetRequest(e.GetString(0)))
        {
            from.SendMessage("Select a mobile, item, or location to go to.");
            from.Target = new BetterGoTarget();
            return;
        }

        if (TryGoToSextant(from, e))
        {
            return;
        }

        if (TryGoToCoordinates(from, e))
        {
            return;
        }

        if (TryGoToSerial(from, e.GetString(0)))
        {
            return;
        }

        var destinationName = GetDestinationName(e);

        if (TryGoToMap(from, destinationName))
        {
            return;
        }

        if (TryGoToRegion(from, destinationName))
        {
            return;
        }

        from.SendMessage("Format: Go [target | serial | map | region name | x y [z] [map] | map x y [z] | deg min N/S deg min E/W]");
    }

    private static bool IsTargetRequest(string value) =>
        value.InsensitiveEquals("target") || value.InsensitiveEquals("t");

    private static string GetDestinationName(CommandEventArgs e) => string.Join(' ', e.Arguments);

    private static bool TryGoToSerial(Mobile from, string value)
    {
        if (!Serial.TryParse(value, null, out var serial) || serial == Serial.Zero)
        {
            return false;
        }

        var entity = World.FindEntity(serial);

        switch (entity)
        {
            case Item item:
                return TryMoveToItem(from, item);
            case Mobile mobile:
                return TryMoveToMobile(from, mobile);
            default:
                from.SendMessage("No object with that serial was found.");
                return true;
        }
    }

    private static bool TryGoToCoordinates(Mobile from, CommandEventArgs e)
    {
        var start = 0;
        var map = from.Map;

        /*
         * Accept both coordinate-first and map-first forms:
         * [Go 512 512 Felucca] and [Go Felucca 512 512 0].
         */
        if (e.Length >= 3 && TryParseUsableMap(e.GetString(0), out var parsedMap))
        {
            map = parsedMap;
            start = 1;
        }

        var remaining = e.Length - start;

        if (remaining is < 2 or > 4 ||
            !int.TryParse(e.GetString(start), out var x) ||
            !int.TryParse(e.GetString(start + 1), out var y))
        {
            return false;
        }

        var z = map?.GetAverageZ(x, y) ?? 0;

        if (remaining >= 3 && !int.TryParse(e.GetString(start + 2), out z))
        {
            if (!TryParseUsableMap(e.GetString(start + 2), out parsedMap))
            {
                return false;
            }

            map = parsedMap;
            z = map.GetAverageZ(x, y);
        }

        if (remaining == 4)
        {
            if (start != 0 || !TryParseUsableMap(e.GetString(start + 3), out parsedMap))
            {
                return false;
            }

            map = parsedMap;
        }

        if (map == null || map == Map.Internal)
        {
            from.SendMessage("You must specify a usable map.");
            return true;
        }

        MoveToWorld(from, new Point3D(x, y, z), map);
        return true;
    }

    private static bool TryGoToSextant(Mobile from, CommandEventArgs e)
    {
        if (e.Length != 6)
        {
            return false;
        }

        var map = from.Map;

        if (map == null || map == Map.Internal)
        {
            from.SendMessage("You must be on a usable map for sextant lookup.");
            return true;
        }

        var point = Sextant.ReverseLookup(
            map,
            e.GetInt32(3),
            e.GetInt32(0),
            e.GetInt32(4),
            e.GetInt32(1),
            e.GetString(5).InsensitiveEquals("E"),
            e.GetString(2).InsensitiveEquals("S")
        );

        if (point == Point3D.Zero)
        {
            from.SendMessage("Sextant reverse lookup failed.");
            return true;
        }

        MoveToWorld(from, point, map);
        return true;
    }

    private static bool TryGoToMap(Mobile from, string value)
    {
        if (!TryParseUsableMap(value, out var map))
        {
            return false;
        }

        MoveToWorld(from, from.Location, map);
        return true;
    }

    private static bool TryGoToRegion(Mobile from, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var currentMap = from.Map;

        if (currentMap != null && currentMap != Map.Internal && TryFindRegion(currentMap, value, out var region))
        {
            MoveToWorld(from, region.GoLocation, currentMap);
            return true;
        }

        for (var i = 0; i < Map.AllMaps.Count; i++)
        {
            var map = Map.AllMaps[i];

            if (!IsUsableMap(map) || map == currentMap)
            {
                continue;
            }

            if (TryFindRegion(map, value, out region))
            {
                MoveToWorld(from, region.GoLocation, map);
                return true;
            }
        }

        return false;
    }

    private static bool TryFindRegion(Map map, string name, out Region region)
    {
        foreach (var kvp in map.Regions)
        {
            var candidate = kvp.Value;

            if (candidate.Name.InsensitiveEquals(name))
            {
                region = candidate;
                return true;
            }
        }

        region = null;
        return false;
    }

    private static bool TryMoveToItem(Mobile from, Item item)
    {
        var map = item.Map;
        var location = item.GetWorldLocation();
        var owner = item.RootParent as Mobile;

        if (owner?.Map != null &&
            owner.Map != Map.Internal &&
            !BaseCommand.IsAccessible(from, owner))
        {
            from.SendMessage("You can not go to what you can not see.");
            return true;
        }

        if (owner != null &&
            (owner.Map == null || owner.Map == Map.Internal) &&
            owner.Hidden &&
            owner.AccessLevel >= from.AccessLevel)
        {
            from.SendMessage("You can not go to what you can not see.");
            return true;
        }

        if (!FixMap(ref map, ref location, item))
        {
            from.SendMessage("That is an internal item and you cannot go to it.");
            return true;
        }

        MoveToWorld(from, location, map);
        return true;
    }

    private static bool TryMoveToMobile(Mobile from, Mobile mobile)
    {
        var map = mobile.Map;
        var location = mobile.Location;

        if (mobile.Map != null &&
            mobile.Map != Map.Internal &&
            !BaseCommand.IsAccessible(from, mobile))
        {
            from.SendMessage("You can not go to what you can not see.");
            return true;
        }

        if ((mobile.Map == null || mobile.Map == Map.Internal) &&
            mobile.Hidden &&
            mobile.AccessLevel >= from.AccessLevel)
        {
            from.SendMessage("You can not go to what you can not see.");
            return true;
        }

        if (!FixMap(ref map, ref location, mobile))
        {
            from.SendMessage("That is an internal mobile and you cannot go to it.");
            return true;
        }

        MoveToWorld(from, location, map);
        return true;
    }

    private static bool FixMap(ref Map map, ref Point3D location, Item item) =>
        map != null && map != Map.Internal ||
        item.RootParent is Mobile mobile && FixMap(ref map, ref location, mobile);

    private static bool FixMap(ref Map map, ref Point3D location, Mobile mobile)
    {
        var validMap = map != null && map != Map.Internal;

        if (!validMap)
        {
            map = mobile.LogoutMap;
            location = mobile.LogoutLocation;
        }

        return IsUsableMap(map);
    }

    private static bool TryParseUsableMap(string value, out Map map)
    {
        if (Map.TryParse(value, null, out map) && IsUsableMap(map))
        {
            return true;
        }

        map = null;
        return false;
    }

    internal static bool IsUsableMap(Map map) =>
        map != null && map != Map.Internal && map.MapIndex is not 0x7F and not 0xFF;

    internal static void MoveToWorld(Mobile from, Point3D location, Map map)
    {
        if (!IsUsableMap(map))
        {
            from.SendMessage("That destination does not have a usable map.");
            return;
        }

        from.MoveToWorld(location, map);
        from.SendMessage($"Moved to {location.X} {location.Y} {location.Z} in {map.Name}.");
    }

    private class BetterGoTarget : Target
    {
        public BetterGoTarget() : base(-1, true, TargetFlags.None)
        {
            CheckLOS = false;
            AllowNonlocal = true;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            switch (targeted)
            {
                case Item item:
                    TryMoveToItem(from, item);
                    break;
                case Mobile mobile:
                    TryMoveToMobile(from, mobile);
                    break;
                case LandTarget land:
                    MoveToWorld(from, land.Location, from.Map);
                    break;
                case StaticTarget stat:
                    MoveToWorld(from, stat.Location, from.Map);
                    break;
                case IPoint3D point:
                    MoveToWorld(from, new Point3D(point), from.Map);
                    break;
                default:
                    from.SendMessage("That target does not have a world location.");
                    break;
            }
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            if (cancelType == TargetCancelType.Canceled)
            {
                from.SendMessage("Go target cancelled.");
            }
        }
    }
}
