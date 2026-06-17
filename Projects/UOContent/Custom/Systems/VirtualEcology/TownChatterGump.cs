using System;
using Server.Custom.Systems.AIIntegration;
using Server.Gumps;
using Server.Network;

namespace Server.Custom.Systems.VirtualEcology;

public sealed class TownChatterGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueReady = 68;
    private const int HueWarn = 33;
    private const int ButtonRefresh = 1;
    private const int ButtonGenerate = 2;
    private const int ButtonRegenerate = 3;
    private const int ButtonClear = 4;
    private const int ButtonRegenerateAll = 5;
    private const int ButtonAcceptedTab = 6;
    private const int ButtonRejectedTab = 7;
    private const int ButtonPreviousPage = 8;
    private const int ButtonNextPage = 9;
    private const int ButtonTownBase = 100;
    private const int ButtonDeleteBase = 1000;
    private const int GumpWidth = 1000;
    private const int GumpHeight = 760;
    private const int MaxVisibleLines = 10;
    private const int MaxVisibleRejectedLines = 6;

    private readonly string _selectedTown;
    private readonly bool _showRejected;
    private readonly int _page;

    public override bool Singleton => true;

    private TownChatterGump(string selectedTown, bool showRejected, int page) : base(70, 55)
    {
        _selectedTown = TownChatterService.NormalizeTown(selectedTown);
        _showRejected = showRejected;
        _page = Math.Max(0, page);
    }

    public static void DisplayTo(Mobile from, string selectedTown = null, bool showRejected = false, int page = 0)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        from.CloseGump<TownChatterGump>();
        from.SendGump(new TownChatterGump(selectedTown, showRejected, page));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);

        builder.AddLabel(30, 22, HueTitle, "Town Chatter");
        builder.AddLabel(30, 50, AIIntegrationService.IsEnabled ? HueReady : HueWarn,
            AIIntegrationService.IsEnabled ? "AI integration enabled" : "AI integration disabled");
        DrawButton(ref builder, 770, 22, ButtonRegenerateAll, "Regenerate All");
        DrawButton(ref builder, 770, 52, ButtonRefresh, "Refresh");
        DrawRule(ref builder, 30, 82, 940);

        DrawTownList(ref builder);
        DrawSelectedTown(ref builder);
    }

    private void DrawTownList(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(36, 102, HueHeader, "Towns");

        var y = 134;
        for (var i = 0; i < TownChatterService.DefaultTowns.Length; i++)
        {
            var town = TownChatterService.DefaultTowns[i];
            var selected = string.Equals(town, _selectedTown, StringComparison.OrdinalIgnoreCase);
            var displayName = ToDisplayName(town);
            var hasCache = TownChatterService.TryGetCache(town, out var cache);

            builder.AddButton(38, y - 2, selected ? 4017 : 4005, selected ? 4019 : 4007, ButtonTownBase + i);
            builder.AddLabel(72, y, selected ? HueTitle : HueText, displayName);
            builder.AddLabel(160, y, hasCache ? HueReady : HueMuted, hasCache ? $"{cache.Lines.Count}" : "-");
            y += 28;
        }
    }

    private void DrawSelectedTown(ref DynamicGumpBuilder builder)
    {
        var displayName = ToDisplayName(_selectedTown);
        var hasCache = TownChatterService.TryGetCache(_selectedTown, out var cache);

        builder.AddLabel(230, 102, HueHeader, displayName);
        builder.AddLabel(330, 102, HueMuted, hasCache ? $"Generated {cache.GeneratedAt:g}" : "No generated chatter");

        if (hasCache)
        {
            DrawButton(ref builder, 230, 132, ButtonRegenerate, "Regenerate");
            DrawButton(ref builder, 370, 132, ButtonClear, "Clear");
        }
        else
        {
            DrawButton(ref builder, 230, 132, ButtonGenerate, "Generate");
        }

        DrawRule(ref builder, 230, 174, 730);

        if (!hasCache)
        {
            builder.AddLabel(240, 198, HueMuted, "No cached chatter for this town.");
            builder.AddLabel(240, 224, HueMuted, $"Use Generate to create a {TownChatterService.DefaultLineCount}-line pool.");
            return;
        }

        DrawButton(ref builder, 240, 194, ButtonAcceptedTab, $"Accepted ({cache.Lines.Count}/{TownChatterService.MaxLineCount})");
        DrawButton(ref builder, 430, 194, ButtonRejectedTab, $"Rejected ({cache.RejectedLines.Count})");
        builder.AddLabel(
            650,
            198,
            HueMuted,
            $"Auto adds {TownChatterService.AutoTopUpLineCount} every {TownChatterService.AutoTopUpInterval.TotalMinutes:0} min"
        );

        if (_showRejected)
        {
            DrawRejectedLines(ref builder, cache);
            return;
        }

        DrawAcceptedLines(ref builder, cache);
    }

    private void DrawAcceptedLines(ref DynamicGumpBuilder builder, TownChatterCache cache)
    {
        var page = Math.Min(_page, GetMaxPage(cache.Lines.Count, MaxVisibleLines));
        var offset = page * MaxVisibleLines;
        var remaining = Math.Max(0, cache.Lines.Count - offset);
        var visibleCount = Math.Min(remaining, MaxVisibleLines);

        builder.AddLabel(240, 228, HueHeader, $"Newest accepted lines - page {page + 1}");
        DrawPageButtons(ref builder, page, cache.Lines.Count, MaxVisibleLines);

        var y = 258;
        for (var i = 0; i < visibleCount; i++)
        {
            var sourceIndex = cache.Lines.Count - 1 - offset - i;
            builder.AddButton(238, y - 2, 4017, 4019, ButtonDeleteBase + sourceIndex);
            builder.AddLabel(272, y, HueMuted, $"{sourceIndex + 1}.");
            builder.AddHtml(304, y - 2, 635, 38, ColorHtml(cache.Lines[sourceIndex], "#F2E6B8"));
            y += 42;
        }

        if (cache.Lines.Count == 0)
        {
            builder.AddLabel(240, y, HueMuted, "No accepted chatter lines are cached.");
            y += 32;
        }

        if (cache.Lines.Count > MaxVisibleLines)
        {
            var firstShown = cache.Lines.Count - offset;
            var lastShown = cache.Lines.Count - offset - visibleCount + 1;
            builder.AddLabel(240, y, HueMuted, $"Showing lines {firstShown}-{lastShown} of {cache.Lines.Count}.");
        }
    }

    private void DrawRejectedLines(ref DynamicGumpBuilder builder, TownChatterCache cache)
    {
        var page = Math.Min(_page, GetMaxPage(cache.RejectedLines.Count, MaxVisibleRejectedLines));
        var offset = page * MaxVisibleRejectedLines;
        var remaining = Math.Max(0, cache.RejectedLines.Count - offset);
        var visibleCount = Math.Min(remaining, MaxVisibleRejectedLines);

        builder.AddLabel(240, 228, HueWarn, $"Rejected lines and reasons - page {page + 1}");
        DrawPageButtons(ref builder, page, cache.RejectedLines.Count, MaxVisibleRejectedLines);

        var y = 258;
        if (cache.RejectedLines.Count == 0)
        {
            builder.AddLabel(240, y, HueReady, "No rejected lines.");
            return;
        }

        for (var i = 0; i < visibleCount; i++)
        {
            var sourceIndex = cache.RejectedLines.Count - 1 - offset - i;
            builder.AddLabel(240, y, HueMuted, $"{sourceIndex + 1}.");
            builder.AddHtml(280, y - 2, 660, 58, ColorHtml(cache.RejectedLines[sourceIndex], "#FFCC66"));
            y += 64;
        }

        if (cache.RejectedLines.Count > MaxVisibleRejectedLines)
        {
            var firstShown = cache.RejectedLines.Count - offset;
            var lastShown = cache.RejectedLines.Count - offset - visibleCount + 1;
            builder.AddLabel(240, y, HueMuted, $"Showing rejected lines {firstShown}-{lastShown} of {cache.RejectedLines.Count}.");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case 0:
                return;
            case ButtonRefresh:
                DisplayTo(from, _selectedTown, _showRejected, _page);
                return;
            case ButtonAcceptedTab:
                DisplayTo(from, _selectedTown);
                return;
            case ButtonRejectedTab:
                DisplayTo(from, _selectedTown, true);
                return;
            case ButtonPreviousPage:
                DisplayTo(from, _selectedTown, _showRejected, Math.Max(0, _page - 1));
                return;
            case ButtonNextPage:
                DisplayTo(from, _selectedTown, _showRejected, _page + 1);
                return;
            case ButtonGenerate:
                BeginGenerate(from, _selectedTown, false);
                return;
            case ButtonRegenerate:
                BeginGenerate(from, _selectedTown, true);
                return;
            case ButtonClear:
                TownChatterService.Clear(_selectedTown);
                DisplayTo(from, _selectedTown, _showRejected);
                return;
            case ButtonRegenerateAll:
                BeginRegenerateAll(from);
                return;
        }

        if (info.ButtonID >= ButtonTownBase && info.ButtonID < ButtonTownBase + TownChatterService.DefaultTowns.Length)
        {
            var index = info.ButtonID - ButtonTownBase;
            DisplayTo(from, TownChatterService.DefaultTowns[index], _showRejected);
            return;
        }

        if (info.ButtonID >= ButtonDeleteBase)
        {
            var index = info.ButtonID - ButtonDeleteBase + 1;
            TownChatterService.DeleteLine(_selectedTown, index, out _);
            DisplayTo(from, _selectedTown, _showRejected, _page);
        }
    }

    private static async void BeginGenerate(Mobile from, string town, bool regenerate)
    {
        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage(0x22, "AI integration is disabled. Enable the ai_integration feature flag first.");
            DisplayTo(from, town);
            return;
        }

        from.SendMessage(0x35, $"{(regenerate ? "Regenerating" : "Generating")} town chatter for {ToDisplayName(town)}...");
        var cache = regenerate
            ? await TownChatterService.RegenerateAsync(town)
            : await TownChatterService.GenerateAsync(town);

        PostToGameLoop(() =>
        {
            if (from?.Deleted != false)
            {
                return;
            }

            from.SendMessage(0x35, $"{(regenerate ? "Regenerated" : "Generated")} {cache.Lines.Count} line(s) for {cache.Town}; {cache.RejectedLines.Count} rejected.");
            DisplayTo(from, town);
        });
    }

    private static async void BeginRegenerateAll(Mobile from)
    {
        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage(0x22, "AI integration is disabled. Enable the ai_integration feature flag first.");
            DisplayTo(from);
            return;
        }

        from.SendMessage(0x35, "Regenerating town chatter for all default towns...");
        var caches = await TownChatterService.RegenerateAllAsync();

        PostToGameLoop(() =>
        {
            if (from?.Deleted != false)
            {
                return;
            }

            from.SendMessage(0x35, $"Regenerated {caches.Count} town chatter cache(s).");
            DisplayTo(from);
        });
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 34, y + 2, HueText, label);
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }

    private static void DrawPageButtons(ref DynamicGumpBuilder builder, int page, int itemCount, int pageSize)
    {
        var maxPage = GetMaxPage(itemCount, pageSize);

        if (page > 0)
        {
            DrawButton(ref builder, 675, 224, ButtonPreviousPage, "Previous");
        }

        if (page < maxPage)
        {
            DrawButton(ref builder, 800, 224, ButtonNextPage, "Next");
        }
    }

    private static int GetMaxPage(int itemCount, int pageSize)
    {
        if (itemCount <= 0)
        {
            return 0;
        }

        return (itemCount - 1) / pageSize;
    }

    private static void PostToGameLoop(Action callback)
    {
        if (Core.LoopContext != null)
        {
            Core.LoopContext.Post(callback);
            return;
        }

        callback();
    }

    private static string ToDisplayName(string town)
    {
        town = TownChatterService.NormalizeTown(town);
        return char.ToUpperInvariant(town[0]) + town[1..];
    }

    private static string EscapeHtml(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string ColorHtml(string value, string color)
    {
        return $"<BASEFONT COLOR={color}>{EscapeHtml(value)}</BASEFONT>";
    }
}
