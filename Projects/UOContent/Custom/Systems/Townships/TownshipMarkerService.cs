using System;
using System.Collections.Generic;
using Server.Network;

namespace Server.Custom.Systems.Townships;

public enum TownshipMarkerKind
{
    ClaimedBorder,
    MaxEnvelopeBorder,
    SelectedExpansionBorder,
    ValidExpansionBorder,
    InvalidExpansionBorder,
    FoundingPoint
}

public static class TownshipMarkerService
{
    private sealed class BorderView
    {
        public Mobile Viewer { get; init; }
        public TownshipState Township { get; init; }
        public DateTime LastRefresh { get; set; }
    }

    private sealed class ExpansionView
    {
        public Mobile Viewer { get; init; }
        public TownshipState Township { get; init; }
        public TownshipExpansionPreview Preview { get; set; }
        public DateTime LastRefresh { get; set; }
    }

    private static readonly Dictionary<Serial, BorderView> _borderViews = new();
    private static readonly Dictionary<Serial, ExpansionView> _expansionViews = new();

    public static void Configure()
    {
        EventSink.Movement += OnMovement;
        Timer.DelayCall(TownshipSettings.BorderRefreshInterval, TownshipSettings.BorderRefreshInterval, RefreshAll);
    }

    public static bool IsViewingBorders(Mobile from) => from != null && _borderViews.ContainsKey(from.Serial);

    public static void ToggleBorders(Mobile from, TownshipState township)
    {
        if (from?.NetState == null || township == null)
        {
            return;
        }

        if (_borderViews.Remove(from.Serial))
        {
            from.SendMessage(0x35, "Township border visualization disabled.");
            return;
        }

        _borderViews[from.Serial] = new BorderView { Viewer = from, Township = township, LastRefresh = Core.Now };
        from.SendMessage(0x35, "Township border visualization enabled.");
        SendTownshipBorders(from, township);
        TownshipService.AddLog(township, TownshipLogType.MarkerViewed, from, "Enabled town border visualization.");
    }

    public static void ShowFoundingPoint(Mobile from, TownshipState township)
    {
        if (from?.NetState == null || township == null)
        {
            return;
        }

        from.SendMessage(
            0x35,
            $"Original founding point: {township.Map?.Name ?? "Internal"} ({township.FoundingPoint.X}, {township.FoundingPoint.Y}, {township.FoundingPoint.Z})"
        );
        SendEffect(from, township.FoundingPoint, TownshipSettings.BorderEffectItemId, TownshipSettings.EnvelopeBorderHue, TownshipSettings.MarkerDuration);
        TownshipService.AddLog(township, TownshipLogType.MarkerViewed, from, "Viewed founding point marker.");
    }

    public static void ShowExpansionPreview(Mobile from, TownshipState township, TownshipExpansionPreview preview)
    {
        if (from?.NetState == null || township == null || preview == null)
        {
            return;
        }

        _expansionViews[from.Serial] = new ExpansionView
        {
            Viewer = from,
            Township = township,
            Preview = preview,
            LastRefresh = Core.Now
        };

        SendExpansionPreview(from, township, preview);
        TownshipService.AddLog(township, TownshipLogType.ExpansionPreview, from, $"Previewed {preview.RequestedArea}.");
    }

    public static void ClearExpansionPreview(Mobile from)
    {
        if (from != null)
        {
            _expansionViews.Remove(from.Serial);
        }
    }

    public static void RefreshTownship(TownshipState township)
    {
        foreach (var view in _borderViews.Values)
        {
            if (view.Township == township)
            {
                SendTownshipBorders(view.Viewer, township);
            }
        }
    }

    public static void ClearTownship(TownshipState township)
    {
        foreach (var key in new List<Serial>(_borderViews.Keys))
        {
            if (_borderViews[key].Township == township)
            {
                _borderViews.Remove(key);
            }
        }

        foreach (var key in new List<Serial>(_expansionViews.Keys))
        {
            if (_expansionViews[key].Township == township)
            {
                _expansionViews.Remove(key);
            }
        }
    }

    private static void RefreshAll()
    {
        foreach (var key in new List<Serial>(_borderViews.Keys))
        {
            var view = _borderViews[key];

            if (view.Viewer?.NetState == null || view.Viewer.Deleted || view.Township?.Guild?.Disbanded != false)
            {
                _borderViews.Remove(key);
                continue;
            }

            SendTownshipBorders(view.Viewer, view.Township);
            view.LastRefresh = Core.Now;
        }

        foreach (var key in new List<Serial>(_expansionViews.Keys))
        {
            var view = _expansionViews[key];

            if (view.Viewer?.NetState == null || view.Viewer.Deleted || view.Township?.Guild?.Disbanded != false)
            {
                _expansionViews.Remove(key);
                continue;
            }

            SendExpansionPreview(view.Viewer, view.Township, view.Preview);
            view.LastRefresh = Core.Now;
        }
    }

    private static void OnMovement(MovementEventArgs args)
    {
        var from = args.Mobile;

        if (from?.NetState == null)
        {
            return;
        }

        if (_borderViews.TryGetValue(from.Serial, out var borderView) &&
            Core.Now >= borderView.LastRefresh + TimeSpan.FromSeconds(1.0))
        {
            SendTownshipBorders(from, borderView.Township);
            borderView.LastRefresh = Core.Now;
        }

        if (_expansionViews.TryGetValue(from.Serial, out var expansionView) &&
            Core.Now >= expansionView.LastRefresh + TimeSpan.FromSeconds(1.0))
        {
            SendExpansionPreview(from, expansionView.Township, expansionView.Preview);
            expansionView.LastRefresh = Core.Now;
        }
    }

    private static void SendTownshipBorders(Mobile from, TownshipState township)
    {
        SendRangesPerimeter(from, township.Map, township.Claims, TownshipSettings.BorderEffectItemId, TownshipSettings.ClaimedBorderHue, TownshipSettings.MarkerDuration);
        SendRectanglePerimeter(from, township.Map, TownshipService.GetEnvelope(township), TownshipSettings.EnvelopeEffectItemId, TownshipSettings.EnvelopeBorderHue, TownshipSettings.MarkerDuration);
    }

    private static void SendExpansionPreview(Mobile from, TownshipState township, TownshipExpansionPreview preview)
    {
        var duration = TownshipSettings.PreviewMarkerDuration;
        SendRangesPerimeter(from, township.Map, township.Claims, TownshipSettings.BorderEffectItemId, TownshipSettings.ClaimedBorderHue, duration);
        SendRectanglePerimeter(from, township.Map, TownshipService.GetEnvelope(township), TownshipSettings.EnvelopeEffectItemId, TownshipSettings.EnvelopeBorderHue, duration);
        SendRectanglePerimeter(from, township.Map, preview.RequestedArea, TownshipSettings.BorderEffectItemId, TownshipSettings.PreviewValidHue, duration);
        SendRangesPerimeter(from, township.Map, preview.InvalidClaims, TownshipSettings.InvalidEffectItemId, TownshipSettings.PreviewInvalidHue, duration);
        SendRangesPerimeter(from, township.Map, preview.ValidClaims, TownshipSettings.BorderEffectItemId, TownshipSettings.PreviewValidHue, duration);
    }

    private static void SendRangesPerimeter(
        Mobile from,
        Map map,
        List<TownshipClaimRange> ranges,
        int itemId,
        int hue,
        TimeSpan duration
    )
    {
        if (from == null || map != from.Map)
        {
            return;
        }

        var rangeLimit = TownshipSettings.BorderRenderRange;
        var minX = from.X - rangeLimit;
        var maxX = from.X + rangeLimit;
        var minY = from.Y - rangeLimit;
        var maxY = from.Y + rangeLimit;

        for (var i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i];

            if (range.Y < minY || range.Y > maxY || range.EndX < minX || range.StartX > maxX)
            {
                continue;
            }

            var startX = Math.Max(range.StartX, minX);
            var endX = Math.Min(range.EndX, maxX);

            for (var x = startX; x <= endX; x++)
            {
                if (Contains(ranges, x - 1, range.Y) &&
                    Contains(ranges, x + 1, range.Y) &&
                    Contains(ranges, x, range.Y - 1) &&
                    Contains(ranges, x, range.Y + 1))
                {
                    continue;
                }

                SendEffect(from, new Point3D(x, range.Y, map.GetAverageZ(x, range.Y)), itemId, hue, duration);
            }
        }
    }

    private static void SendRectanglePerimeter(Mobile from, Map map, Rectangle2D rect, int itemId, int hue, TimeSpan duration)
    {
        if (from == null || map != from.Map)
        {
            return;
        }

        var rangeLimit = TownshipSettings.BorderRenderRange;
        var minX = from.X - rangeLimit;
        var maxX = from.X + rangeLimit;
        var minY = from.Y - rangeLimit;
        var maxY = from.Y + rangeLimit;
        var rectRight = rect.X + rect.Width - 1;
        var rectBottom = rect.Y + rect.Height - 1;

        var startX = Math.Max(rect.X, minX);
        var endX = Math.Min(rectRight, maxX);

        if (rect.Y >= minY && rect.Y <= maxY)
        {
            for (var x = startX; x <= endX; x++)
            {
                SendEffect(from, new Point3D(x, rect.Y, map.GetAverageZ(x, rect.Y)), itemId, hue, duration);
            }
        }

        if (rectBottom != rect.Y && rectBottom >= minY && rectBottom <= maxY)
        {
            for (var x = startX; x <= endX; x++)
            {
                SendEffect(from, new Point3D(x, rectBottom, map.GetAverageZ(x, rectBottom)), itemId, hue, duration);
            }
        }

        var startY = Math.Max(rect.Y + 1, minY);
        var endY = Math.Min(rectBottom - 1, maxY);

        for (var y = startY; y <= endY; y++)
        {
            if (rect.X >= minX && rect.X <= maxX)
            {
                SendEffect(from, new Point3D(rect.X, y, map.GetAverageZ(rect.X, y)), itemId, hue, duration);
            }

            if (rectRight != rect.X && rectRight >= minX && rectRight <= maxX)
            {
                SendEffect(from, new Point3D(rectRight, y, map.GetAverageZ(rectRight, y)), itemId, hue, duration);
            }
        }
    }

    private static void SendEffect(Mobile from, Point3D p, int itemId, int hue, TimeSpan duration)
    {
        var ns = from?.NetState;

        if (ns == null || !IsInRenderRange(from, p))
        {
            return;
        }

        var effect = stackalloc byte[OutgoingEffectPackets.HuedEffectLength].InitializePacket();
        OutgoingEffectPackets.CreateLocationHuedEffect(
            effect,
            p,
            itemId,
            10,
            Math.Max(1, (int)duration.TotalSeconds * 4),
            hue,
            0
        );
        ns.Send(effect);
    }

    private static bool IsInRenderRange(Mobile from, Point3D p)
    {
        var range = TownshipSettings.BorderRenderRange;
        return Math.Abs(from.X - p.X) <= range && Math.Abs(from.Y - p.Y) <= range;
    }

    private static bool Contains(List<TownshipClaimRange> ranges, int x, int y)
    {
        for (var i = 0; i < ranges.Count; i++)
        {
            if (ranges[i].Contains(x, y))
            {
                return true;
            }
        }

        return false;
    }
}
