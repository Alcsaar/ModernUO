using System.Collections.Generic;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom.Systems.MapSeasonOverride;

public sealed class MapSeasonOverrideGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueReady = 68;
    private const int ButtonRefreshCurrentMap = 1;
    private const int ButtonSeasonBase = 1000;
    private const int SeasonButtonStride = 10;

    private static readonly string[] _seasonLabels =
    {
        "Spring",
        "Summer",
        "Fall",
        "Winter",
        "Desolation"
    };

    private readonly Map[] _maps;

    public override bool Singleton => true;

    private MapSeasonOverrideGump() : base(185, 90)
    {
        _maps = BuildMaps();
    }

    public static void DisplayTo(PlayerMobile from)
    {
        if (from?.NetState == null)
        {
            return;
        }

        from.CloseGump<MapSeasonOverrideGump>();
        from.SendGump(new MapSeasonOverrideGump());
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var height = 122 + _maps.Length * 42;

        builder.AddPage();
        builder.AddBackground(0, 0, 700, height, 9270);
        builder.AddAlphaRegion(15, 15, 670, height - 30);

        builder.AddLabel(28, 22, HueTitle, "Map Seasons");
        builder.AddLabel(28, 50, HueMuted, "Season changes are persisted and pushed to players on the selected map.");
        builder.AddButton(560, 22, 4005, 4007, ButtonRefreshCurrentMap);
        builder.AddLabel(594, 24, HueText, "Refresh Here");
        DrawRule(ref builder, 28, 78, 640);

        var y = 98;

        for (var i = 0; i < _maps.Length; i++)
        {
            var map = _maps[i];
            var overrideText = MapSeasonOverrideService.HasOverride(map) ? "persisted" : "default";

            builder.AddLabel(34, y, HueHeader, map.Name);
            builder.AddLabel(132, y, HueText, $"{MapSeasonOverrideService.GetSeasonName(map.Season)} ({overrideText})");

            for (var season = 0; season < _seasonLabels.Length; season++)
            {
                var x = 258 + season * 80;
                var hue = map.Season == season ? HueReady : HueText;

                builder.AddButton(x, y - 2, 4005, 4007, ButtonSeasonBase + i * SeasonButtonStride + season);
                builder.AddLabel(x + 34, y, hue, GetShortSeasonName(season));
            }

            y += 42;
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile player)
        {
            return;
        }

        if (info.ButtonID == ButtonRefreshCurrentMap)
        {
            var count = MapSeasonOverrideService.RefreshSeasonForPlayers(player.Map);
            player.SendMessage(0x35, $"Season refresh sent to {count:N0} client{(count == 1 ? string.Empty : "s")} on {player.Map?.Name ?? "this map"}.");
            DisplayTo(player);
            return;
        }

        if (info.ButtonID < ButtonSeasonBase)
        {
            return;
        }

        var value = info.ButtonID - ButtonSeasonBase;
        var mapIndex = value / SeasonButtonStride;
        var season = value % SeasonButtonStride;

        if (mapIndex < 0 || mapIndex >= _maps.Length || season < 0 || season >= _seasonLabels.Length)
        {
            return;
        }

        var map = _maps[mapIndex];

        if (MapSeasonOverrideService.TrySetSeason(map, season, out var failureReason))
        {
            player.SendMessage(0x35, $"{map.Name} season set to {MapSeasonOverrideService.GetSeasonName(season)}.");
        }
        else
        {
            player.SendMessage(0x22, failureReason ?? "Unable to set map season.");
        }

        DisplayTo(player);
    }

    private static Map[] BuildMaps()
    {
        var maps = new List<Map>(Map.AllMaps.Count);

        for (var i = 0; i < Map.AllMaps.Count; i++)
        {
            var map = Map.AllMaps[i];

            if (map != null && map != Map.Internal)
            {
                maps.Add(map);
            }
        }

        maps.Sort(static (a, b) => a.MapIndex.CompareTo(b.MapIndex));
        return maps.ToArray();
    }

    private static string GetShortSeasonName(int season)
    {
        return season switch
        {
            0 => "Spr",
            1 => "Sum",
            2 => "Fall",
            3 => "Win",
            4 => "Des",
            _ => "?"
        };
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }
}
