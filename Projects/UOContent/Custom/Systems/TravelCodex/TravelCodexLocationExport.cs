using System;
using System.Collections.Generic;
using System.IO;
using Server.Json;

namespace Server.Custom.Systems.TravelCodex;

public sealed class TravelCodexLocationExportFile
{
    public int Version { get; set; } = 1;
    public List<TravelCodexLocationRecord> Locations { get; set; } = new();
}

public sealed class TravelCodexLocationRecord
{
    public string DestinationKey { get; set; }
    public string DisplayName { get; set; }
    public TravelCategory TravelCategory { get; set; }
    public int DiscoverRange { get; set; }
    public bool Enabled { get; set; }
    public string StoneMap { get; set; }
    public int StoneX { get; set; }
    public int StoneY { get; set; }
    public int StoneZ { get; set; }
    public string TravelMap { get; set; }
    public int TravelX { get; set; }
    public int TravelY { get; set; }
    public int TravelZ { get; set; }
}

public static class TravelCodexLocationExport
{
    private const string SystemName = "TravelCodex";
    private const string DefaultFileName = "locations.json";

    public static string ConfigDirectory => Path.Combine(Core.BaseDirectory, "Configuration", SystemName);

    public static string DefaultPath => Path.Combine(ConfigDirectory, DefaultFileName);

    public static string ResolveDisplayPath(string path) => ResolvePath(path);

    public static int Export(string path)
    {
        path = ResolvePath(path);
        PathUtility.EnsureDirectory(Path.GetDirectoryName(path));

        var file = new TravelCodexLocationExportFile();

        foreach (var item in World.Items.Values)
        {
            if (item is not TravelDiscoveryStone stone || stone.Deleted || stone.Map == null || stone.Map == Map.Internal)
            {
                continue;
            }

            file.Locations.Add(
                new TravelCodexLocationRecord
                {
                    DestinationKey = stone.DestinationKey,
                    DisplayName = stone.DisplayName,
                    TravelCategory = stone.TravelCategory,
                    DiscoverRange = stone.DiscoverRange,
                    Enabled = stone.Enabled,
                    StoneMap = stone.Map.Name,
                    StoneX = stone.X,
                    StoneY = stone.Y,
                    StoneZ = stone.Z,
                    TravelMap = stone.TravelMap?.Name,
                    TravelX = stone.TravelPoint.X,
                    TravelY = stone.TravelPoint.Y,
                    TravelZ = stone.TravelPoint.Z
                }
            );
        }

        file.Locations.Sort(static (left, right) => string.Compare(left.DestinationKey, right.DestinationKey, StringComparison.OrdinalIgnoreCase));
        JsonConfig.Serialize(path, file);
        return file.Locations.Count;
    }

    public static bool Import(string path, out int created, out int updated, out string failureReason)
    {
        created = 0;
        updated = 0;
        failureReason = null;
        path = ResolvePath(path);

        var file = JsonConfig.Deserialize<TravelCodexLocationExportFile>(path);
        if (file?.Locations == null)
        {
            failureReason = $"No travel locations found in {path}.";
            return false;
        }

        for (var i = 0; i < file.Locations.Count; i++)
        {
            var record = file.Locations[i];
            if (!TryImportRecord(record, out var wasCreated, out failureReason))
            {
                return false;
            }

            if (wasCreated)
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        return true;
    }

    private static bool TryImportRecord(TravelCodexLocationRecord record, out bool created, out string failureReason)
    {
        created = false;
        failureReason = null;

        var key = TravelCodexManager.NormalizeKey(record?.DestinationKey);
        if (key == null)
        {
            failureReason = "A travel location record is missing DestinationKey.";
            return false;
        }

        if (!TryParseMap(record.StoneMap, out var stoneMap) || !TryParseMap(record.TravelMap, out var travelMap))
        {
            failureReason = $"Travel location '{key}' has an invalid map.";
            return false;
        }

        var stone = FindStone(key);
        if (stone == null)
        {
            stone = new TravelDiscoveryStone();
            created = true;
        }

        stone.Enabled = false;
        stone.DestinationKey = key;
        stone.DisplayName = record.DisplayName;
        stone.TravelCategory = record.TravelCategory;
        stone.DiscoverRange = record.DiscoverRange;
        stone.TravelMap = travelMap;
        stone.TravelPoint = new Point3D(record.TravelX, record.TravelY, record.TravelZ);
        stone.MoveToWorld(new Point3D(record.StoneX, record.StoneY, record.StoneZ), stoneMap);
        stone.Enabled = record.Enabled;
        return true;
    }

    private static TravelDiscoveryStone FindStone(string destinationKey)
    {
        foreach (var item in World.Items.Values)
        {
            if (
                item is TravelDiscoveryStone stone &&
                !stone.Deleted &&
                string.Equals(stone.DestinationKey, destinationKey, StringComparison.OrdinalIgnoreCase)
            )
            {
                return stone;
            }
        }

        return null;
    }

    private static bool TryParseMap(string mapName, out Map map)
    {
        map = null;
        return !string.IsNullOrWhiteSpace(mapName) && Map.TryParse(mapName, null, out map) && map != Map.Internal;
    }

    private static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return DefaultPath;
        }

        return Path.IsPathRooted(path) ? path : Path.Combine(Core.BaseDirectory, path);
    }
}
