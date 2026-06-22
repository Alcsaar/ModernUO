using System;

namespace Server.Custom.Utilities;

public static class WorldLocationDescription
{
    private const int NearDistance = 55;

    private static readonly Landmark[] FeluccaLandmarks =
    [
        new("Britain", 1495, 1629),
        new("Skara Brae", 618, 2234),
        new("Yew", 634, 849),
        new("Minoc", 2477, 407),
        new("Vesper", 2899, 676),
        new("Cove", 2247, 1192),
        new("Moonglow", 4442, 1134),
        new("Magincia", 3714, 2224),
        new("Trinsic", 1828, 2821),
        new("Jhelom", 1419, 3821),
        new("Nujel'm", 3768, 1308),
        new("Serpent's Hold", 3000, 3410),
        new("Buccaneer's Den", 2714, 2162),
        new("Destard", 1176, 2637),
        new("Despise", 1299, 1080),
        new("Shame", 512, 1564),
        new("Wrong", 2043, 238),
        new("Covetous", 2499, 916),
        new("Deceit", 4111, 432),
        new("Hythloth", 4721, 3824),
        new("Fire Dungeon", 2923, 3408),
        new("Ice Dungeon", 1997, 81),
        new("Wind", 1361, 895)
    ];

    public static string Describe(Point3D location, Map map, bool includeMapFallback = true)
    {
        var landmarks = GetLandmarks(map);

        if (landmarks.Length == 0)
        {
            return includeMapFallback
                ? $"{map?.Name ?? "Unknown lands"} ({location.X}, {location.Y})"
                : $"({location.X}, {location.Y})";
        }

        FindNearest(location, landmarks, out var primary, out var secondary);

        if (primary == null)
        {
            return includeMapFallback
                ? $"{map?.Name ?? "Unknown lands"} ({location.X}, {location.Y})"
                : $"({location.X}, {location.Y})";
        }

        var primaryText = FormatRelative(location, primary);

        if (secondary == null)
        {
            return primaryText;
        }

        return $"{primaryText}, {FormatRelative(location, secondary)}";
    }

    private static ReadOnlySpan<Landmark> GetLandmarks(Map map)
    {
        if (map == Map.Felucca || map == Map.Trammel)
        {
            return FeluccaLandmarks;
        }

        return [];
    }

    private static void FindNearest(Point3D location, ReadOnlySpan<Landmark> landmarks, out Landmark primary, out Landmark secondary)
    {
        primary = null;
        secondary = null;

        for (var i = 0; i < landmarks.Length; i++)
        {
            var landmark = landmarks[i];
            var dx = location.X - landmark.X;
            var dy = location.Y - landmark.Y;
            landmark.DistanceSquared = dx * dx + dy * dy;

            if (primary == null || landmark.DistanceSquared < primary.DistanceSquared)
            {
                secondary = primary;
                primary = landmark;
            }
            else if (secondary == null || landmark.DistanceSquared < secondary.DistanceSquared)
            {
                secondary = landmark;
            }
        }
    }

    private static string FormatRelative(Point3D location, Landmark landmark)
    {
        var dx = location.X - landmark.X;
        var dy = location.Y - landmark.Y;

        if (Math.Abs(dx) <= NearDistance && Math.Abs(dy) <= NearDistance)
        {
            return $"near {landmark.Name}";
        }

        return $"{GetDirection(dx, dy)} of {landmark.Name}";
    }

    private static string GetDirection(int dx, int dy)
    {
        var absX = Math.Abs(dx);
        var absY = Math.Abs(dy);

        if (absX <= NearDistance)
        {
            return dy < 0 ? "north" : "south";
        }

        if (absY <= NearDistance)
        {
            return dx < 0 ? "west" : "east";
        }

        var northSouth = dy < 0 ? "north" : "south";
        var eastWest = dx < 0 ? "west" : "east";

        return $"{northSouth}{eastWest}";
    }

    private sealed class Landmark
    {
        public Landmark(string name, int x, int y)
        {
            Name = name;
            X = x;
            Y = y;
        }

        public string Name { get; }
        public int X { get; }
        public int Y { get; }
        public int DistanceSquared { get; set; }
    }
}
