using System;
using System.Collections.Generic;
using System.IO;
using Server.Json;

namespace Server.Custom.Systems.RareSpawns;

public sealed class RareSpawnExportFile
{
    public int Version { get; set; } = 1;
    public List<RareSpawnRecord> SpawnPoints { get; set; } = new();
}

public sealed class RareSpawnRecord
{
    public string DisplayName { get; set; }
    public string SpawnTypeName { get; set; }
    public string PossibleSpawnTypeNames { get; set; }
    public RareRespawnProfile RespawnProfile { get; set; }
    public int MinRespawnMinutes { get; set; }
    public int MaxRespawnMinutes { get; set; }
    public bool Enabled { get; set; }
    public string Map { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}

public static class RareSpawnExport
{
    private const string SystemName = "RareSpawns";
    private const string DefaultFileName = "spawn-points.json";

    public static string ConfigDirectory => Path.Combine(Core.BaseDirectory, "Configuration", SystemName);

    public static string DefaultPath => Path.Combine(ConfigDirectory, DefaultFileName);

    public static string ResolveDisplayPath(string path) => ResolvePath(path);

    public static int Export(string path)
    {
        path = ResolvePath(path);
        PathUtility.EnsureDirectory(Path.GetDirectoryName(path));

        var file = new RareSpawnExportFile();
        var points = RareSpawnManager.GetSpawnPoints();

        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            if (point.Map == null || point.Map == Map.Internal)
            {
                continue;
            }

            file.SpawnPoints.Add(
                new RareSpawnRecord
                {
                    DisplayName = point.DisplayName,
                    SpawnTypeName = point.SpawnTypeName,
                    PossibleSpawnTypeNames = point.PossibleSpawnTypeNames,
                    RespawnProfile = point.RespawnProfile,
                    MinRespawnMinutes = point.MinRespawnMinutes,
                    MaxRespawnMinutes = point.MaxRespawnMinutes,
                    Enabled = point.Enabled,
                    Map = point.Map.Name,
                    X = point.X,
                    Y = point.Y,
                    Z = point.Z
                }
            );
        }

        file.SpawnPoints.Sort(static (left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
        JsonConfig.Serialize(path, file);
        return file.SpawnPoints.Count;
    }

    public static bool Import(string path, out int created, out int updated, out string failureReason)
    {
        created = 0;
        updated = 0;
        failureReason = null;
        path = ResolvePath(path);

        var file = JsonConfig.Deserialize<RareSpawnExportFile>(path);
        if (file?.SpawnPoints == null)
        {
            failureReason = $"No rare spawn points found in {path}.";
            return false;
        }

        for (var i = 0; i < file.SpawnPoints.Count; i++)
        {
            var record = file.SpawnPoints[i];
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

        RareSpawnManager.CheckAll();
        return true;
    }

    private static bool TryImportRecord(RareSpawnRecord record, out bool created, out string failureReason)
    {
        created = false;
        failureReason = null;

        if (record == null || !Map.TryParse(record.Map, null, out var map) || map == Map.Internal)
        {
            failureReason = "A rare spawn point record has an invalid map.";
            return false;
        }

        var location = new Point3D(record.X, record.Y, record.Z);
        var point = FindPoint(record, map, location);

        if (point == null)
        {
            point = new RareSpawnPoint();
            created = true;
        }

        point.Enabled = false;
        point.SpawnTypeName = record.SpawnTypeName;
        point.PossibleSpawnTypeNames = record.PossibleSpawnTypeNames;
        point.DisplayName = record.DisplayName;
        point.RespawnProfile = record.RespawnProfile;
        point.MoveToWorld(location, map);
        point.ResetSchedule(clearServerBirth: true);
        point.MinRespawnMinutes = record.MinRespawnMinutes;
        point.MaxRespawnMinutes = record.MaxRespawnMinutes;
        point.Enabled = record.Enabled;
        return true;
    }

    private static RareSpawnPoint FindPoint(RareSpawnRecord record, Map map, Point3D location)
    {
        var points = RareSpawnManager.GetSpawnPoints();

        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            if (
                point.Map == map &&
                point.Location == location &&
                string.Equals(point.DisplayName, record.DisplayName, StringComparison.OrdinalIgnoreCase)
            )
            {
                return point;
            }
        }

        return null;
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
