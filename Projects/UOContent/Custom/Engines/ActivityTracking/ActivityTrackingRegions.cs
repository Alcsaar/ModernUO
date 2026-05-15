using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Regions;

namespace Server.Custom.Engines.ActivityTracking;

public static class ActivityTrackingRegions
{
    // Define major regions to track (dungeons, towns, POIs)
    private static readonly HashSet<string> _majorRegions = new()
    {
        // Towns
        "Britain", "Trinsic", "Minoc", "Moonglow", "Yew", "Skara Brae", "New Haven", "Magincia", "Vesper", "Jhelom", "Nujel'm", "Serpent's Hold",
        // Dungeons
        "Covetous", "Deceit", "Despise", "Destard", "Wrong", "Shame", "Hythloth", "Doom",
        // POIs/Shrines
        "Chaos Shrine", "Compassion Shrine", "Honesty Shrine", "Honor Shrine", "Humility Shrine", "Justice Shrine", "Sacrifice Shrine", "Spirituality Shrine", "Valor Shrine",
        // Other major areas
        "Trammel", "Felucca", "Ilshenar", "Tokuno", "Ter Mur"
    };

    public static void RecordLocationExplored(PlayerMobile player, string locationKey, string regionName)
    {
        if (player == null || string.IsNullOrWhiteSpace(locationKey) || string.IsNullOrWhiteSpace(regionName))
        {
            return;
        }

        var data = ActivityTrackingService.GetOrCreatePlayerData(player);

        if (data.ExploredRegionNames.Add(regionName))
        {
            var timestamp = DateTime.UtcNow;

            data.ExploredRegions[regionName] = new RegionEntryRecord
            {
                RegionName = regionName,
                FirstEnteredUtc = timestamp,
                Map = player.Map?.ToString() ?? "Unknown",
                Location = player.Location
            };

            data.LastUpdatedUtc = timestamp;
            ActivityTrackingService.SavePlayerData();
        }
    }

    public static void OnMovement(MovementEventArgs args)
    {
        if (args.Mobile is not PlayerMobile player || !ActivityTrackingService.ShouldTrackPlayer(player))
        {
            return;
        }

        var regionName = player.Region?.Name;

        if (string.IsNullOrWhiteSpace(regionName) || !_majorRegions.Contains(regionName))
        {
            return;
        }

        var serial = player.Serial.Value;

        // Only record if this is the first time entering this major region
        if (!ActivityTrackingService.LastRecordedRegion.TryGetValue(serial, out var lastRegion) || lastRegion != regionName)
        {
            ActivityTrackingService.LastRecordedRegion[serial] = regionName;

            RecordLocationExplored(player, regionName, regionName);
        }
    }
}
