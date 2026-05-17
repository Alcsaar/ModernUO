using System;
using System.Collections.Generic;
using Server.Custom.Systems.CustomFeatureFlags;
using Server.Logging;

namespace Server.Custom.Systems.RareSpawns;

public static class RareSpawnManager
{
    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(RareSpawnManager));
    private static readonly HashSet<RareSpawnPoint> _spawnPoints = new();
    private static TimerExecutionToken _checkToken;

    public static void Configure()
    {
        RareSpawnCommands.Configure();
    }

    public static void Initialize()
    {
        EnsureFlagRegistered();
        StartTimer();
    }

    public static bool IsEnabled()
    {
        return CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.RareSpawns);
    }

    public static void EnsureFlagRegistered()
    {
        if (CustomFeatureFlagManager.IsRegistered(CustomFeatureFlagKeys.RareSpawns))
        {
            return;
        }

        CustomFeatureFlagManager.Register(
            CustomFeatureFlagKeys.RareSpawns,
            "Rare Spawns",
            "Controls timed rare item spawns in the world",
            "Custom Systems",
            defaultEnabled: true
        );
    }

    public static void Register(RareSpawnPoint point)
    {
        if (point == null)
        {
            return;
        }

        _spawnPoints.Add(point);
    }

    public static void Unregister(RareSpawnPoint point)
    {
        if (point == null)
        {
            return;
        }

        _spawnPoints.Remove(point);
    }

    public static RareSpawnPoint[] GetSpawnPoints()
    {
        var list = new List<RareSpawnPoint>();

        foreach (var point in _spawnPoints)
        {
            if (point == null || point.Deleted)
            {
                continue;
            }

            list.Add(point);
        }

        list.Sort(static (a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return list.ToArray();
    }

    public static bool TryResolveItemType(string typeName, out Type type)
    {
        type = null;

        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        type = AssemblyHandler.FindTypeByName(typeName.Trim());
        return type != null && typeof(Item).IsAssignableFrom(type);
    }

    public static bool TryResolveAnyItemType(string[] typeNames, out Type type)
    {
        type = null;

        if (typeNames == null)
        {
            return false;
        }

        for (var i = 0; i < typeNames.Length; i++)
        {
            if (TryResolveItemType(typeNames[i], out type))
            {
                return true;
            }
        }

        return false;
    }

    public static void CheckAll()
    {
        if (!IsEnabled())
        {
            return;
        }

        foreach (var point in _spawnPoints)
        {
            if (point == null || point.Deleted)
            {
                continue;
            }

            try
            {
                point.CheckRespawn();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Rare spawn point check failed for {Serial}", point.Serial);
            }
        }
    }

    private static void StartTimer()
    {
        /* BEGIN RARE SPAWN CUSTOMIZATION: one lightweight polling timer services all registered rare points. */
        _checkToken.Cancel();
        Timer.StartTimer(TimeSpan.FromMinutes(1.0), TimeSpan.FromMinutes(5.0), CheckAll, out _checkToken);
        Timer.StartTimer(CheckAll);
        /* END RARE SPAWN CUSTOMIZATION */
    }
}
