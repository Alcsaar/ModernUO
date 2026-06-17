using System;
using System.Collections.Generic;
using System.IO;
using Server.Json;

namespace Server.Custom.Systems.MissionSystem;

public sealed class MissionBoardExportFile
{
    public int Version { get; set; } = 1;
    public List<MissionBoardRecord> Boards { get; set; } = new();
}

public sealed class MissionBoardRecord
{
    public string Map { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}

public static class MissionBoardExport
{
    private const string SystemName = "MissionSystem";
    private const string DefaultFileName = "mission-boards.json";

    public static string ConfigDirectory => Path.Combine(Core.BaseDirectory, "Configuration", SystemName);

    public static string DefaultPath => Path.Combine(ConfigDirectory, DefaultFileName);

    public static string ResolveDisplayPath(string path) => ResolvePath(path);

    public static int Export(string path)
    {
        path = ResolvePath(path);
        PathUtility.EnsureDirectory(Path.GetDirectoryName(path));

        var file = new MissionBoardExportFile();

        foreach (var item in World.Items.Values)
        {
            if (item is not MissionBoardItem board || board.Deleted || board.Map == null || board.Map == Map.Internal)
            {
                continue;
            }

            file.Boards.Add(
                new MissionBoardRecord
                {
                    Map = board.Map.Name,
                    X = board.X,
                    Y = board.Y,
                    Z = board.Z
                }
            );
        }

        file.Boards.Sort(static (left, right) =>
        {
            var map = string.Compare(left.Map, right.Map, StringComparison.OrdinalIgnoreCase);
            if (map != 0)
            {
                return map;
            }

            var x = left.X.CompareTo(right.X);
            return x != 0 ? x : left.Y.CompareTo(right.Y);
        });

        JsonConfig.Serialize(path, file);
        return file.Boards.Count;
    }

    public static bool Import(string path, out int created, out int skipped, out string failureReason)
    {
        created = 0;
        skipped = 0;
        failureReason = null;
        path = ResolvePath(path);

        var file = JsonConfig.Deserialize<MissionBoardExportFile>(path);
        if (file?.Boards == null)
        {
            failureReason = $"No mission board records found in {path}.";
            return false;
        }

        for (var i = 0; i < file.Boards.Count; i++)
        {
            var record = file.Boards[i];
            if (record == null || !Map.TryParse(record.Map, null, out var map) || map == Map.Internal)
            {
                failureReason = "A mission board record has an invalid map.";
                return false;
            }

            var location = new Point3D(record.X, record.Y, record.Z);
            if (FindBoard(map, location) != null)
            {
                skipped++;
                continue;
            }

            new MissionBoardItem().MoveToWorld(location, map);
            created++;
        }

        return true;
    }

    private static MissionBoardItem FindBoard(Map map, Point3D location)
    {
        foreach (var item in World.Items.Values)
        {
            if (item is MissionBoardItem board && !board.Deleted && board.Map == map && board.Location == location)
            {
                return board;
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
