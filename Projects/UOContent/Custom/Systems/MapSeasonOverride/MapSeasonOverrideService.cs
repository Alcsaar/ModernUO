using System.Collections.Generic;
using System.IO;
using Server.Json;
using Server.Network;

namespace Server.Custom.Systems.MapSeasonOverride;

public sealed class MapSeasonOverrideConfig
{
    public Dictionary<string, int> Seasons { get; set; } = new();
}

public static class MapSeasonOverrideService
{
    private const string SystemName = "MapSeasonOverride";

    private static MapSeasonOverrideConfig _config;

    public static string ConfigDirectory => Path.Combine(Core.BaseDirectory, "Configuration", SystemName);

    public static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");

    public static void Configure()
    {
        Load();
        ApplyConfiguredSeasons();
    }

    public static bool TrySetSeason(Map map, int season, out string failureReason)
    {
        failureReason = null;

        if (map == null)
        {
            failureReason = "Unknown map.";
            return false;
        }

        if (!IsValidSeason(season))
        {
            failureReason = "Season must be 0-4: Spring, Summer, Fall, Winter, or Desolation.";
            return false;
        }

        map.Season = season;
        _config.Seasons[map.Name] = season;
        Save();
        RefreshSeasonForPlayers(map);
        return true;
    }

    public static bool TryClearSeason(Map map, out string failureReason)
    {
        failureReason = null;

        if (map == null)
        {
            failureReason = "Unknown map.";
            return false;
        }

        if (!_config.Seasons.Remove(map.Name))
        {
            failureReason = $"{map.Name} does not have a persisted season override.";
            return false;
        }

        Save();
        return true;
    }

    public static bool HasOverride(Map map)
    {
        return map != null && _config.Seasons.ContainsKey(map.Name);
    }

    public static string GetSeasonName(int season)
    {
        return season switch
        {
            0 => "Spring",
            1 => "Summer",
            2 => "Fall",
            3 => "Winter",
            4 => "Desolation",
            _ => "Unknown"
        };
    }

    public static int RefreshSeasonForPlayers(Map map)
    {
        if (map == null || map == Map.Internal)
        {
            return 0;
        }

        var refreshed = 0;

        foreach (var ns in NetState.Instances)
        {
            if (ns.Mobile?.Map != map)
            {
                continue;
            }

            RefreshSeasonFor(ns.Mobile);
            refreshed++;
        }

        return refreshed;
    }

    public static bool TryParseSeason(string value, out int season)
    {
        season = -1;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (int.TryParse(value, out season))
        {
            return IsValidSeason(season);
        }

        season = value.Trim().ToLowerInvariant() switch
        {
            "spring" => 0,
            "summer" => 1,
            "fall" or "autumn" => 2,
            "winter" => 3,
            "desolation" or "desolate" => 4,
            _ => -1
        };

        return IsValidSeason(season);
    }

    private static bool IsValidSeason(int season)
    {
        return season is >= 0 and <= 4;
    }

    private static void Load()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        _config = JsonConfig.Deserialize<MapSeasonOverrideConfig>(ConfigPath) ?? new MapSeasonOverrideConfig();
        _config.Seasons ??= new Dictionary<string, int>();
        Save();
    }

    private static void Save()
    {
        JsonConfig.Serialize(ConfigPath, _config);
    }

    /* BEGIN MAP SEASON OVERRIDES: apply persisted seasons after map definitions have registered Map instances */
    private static void ApplyConfiguredSeasons()
    {
        foreach (var pair in _config.Seasons)
        {
            if (!Map.TryParse(pair.Key, null, out var map) || !IsValidSeason(pair.Value))
            {
                continue;
            }

            map.Season = pair.Value;
        }
    }
    /* END MAP SEASON OVERRIDES */

    /* BEGIN MAP SEASON OVERRIDES: reset movement sequence and push the client season packet without a relog */
    private static void RefreshSeasonFor(Mobile mobile)
    {
        var ns = mobile?.NetState;

        if (ns == null || mobile.Map == null || mobile.Map == Map.Internal)
        {
            return;
        }

        ns.Sequence = 0;
        ns.SendSeasonChange((byte)mobile.GetSeason(), true);
        ns.SendMobileUpdate(mobile);
    }
    /* END MAP SEASON OVERRIDES */
}
