using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom.Systems.AchievementSystem;

public sealed class AchievementGump : DynamicGump
{
    private const int ButtonCategoryBase = 100;
    private const int ButtonPrevPage = 2000;
    private const int ButtonNextPage = 2001;
    private const int GumpWidth = 880;
    private const int GumpHeight = 620;
    private const int EntriesPerPage = 6;

    private readonly PlayerMobile _from;
    private readonly AchievementJournalView _selectedView;
    private readonly int _pageIndex;
    private readonly AchievementJournalView[] _views;
    private readonly List<AchievementDefinition> _definitions;

    public override bool Singleton => true;

    private AchievementGump(PlayerMobile from, AchievementJournalView selectedView, int pageIndex) : base(30, 30)
    {
        _from = from;
        _views = AchievementService.GetAvailableJournalViews();
        _selectedView = NormalizeJournalView(selectedView, _views);
        _definitions = AchievementService.GetDefinitions(_selectedView);
        _pageIndex = Math.Max(0, pageIndex);
    }

    public static void DisplayTo(PlayerMobile from, AchievementJournalView selectedView, int pageIndex = 0)
    {
        if (from?.NetState == null)
        {
            return;
        }

        from.CloseGump<AchievementGump>();
        from.SendGump(new AchievementGump(from, selectedView, pageIndex));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);

        BuildHeader(ref builder);
        BuildCategoryRail(ref builder);

        if (_selectedView == AchievementJournalView.Overview)
        {
            BuildOverview(ref builder);
        }
        else
        {
            BuildAchievementList(ref builder);
        }
    }

    private void BuildHeader(ref DynamicGumpBuilder builder)
    {
        builder.AddHtml(24, 18, 320, 24, HtmlColor("Achievement Journal", "#00FFFF"));
        builder.AddHtml(585, 18, 170, 20, HtmlColor(BuildSummaryText(), "#FFFFFF"));
        builder.AddImageTiled(20, 48, 840, 2, 5058);
        builder.AddImageTiled(20, 50, 840, 2, 2624);
    }

    private void BuildCategoryRail(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(20, 62, 190, 538, 9250);
        builder.AddHtml(36, 78, 120, 20, HtmlColor("Categories", "#FFFFFF"));

        var y = 112;

        for (var i = 0; i < _views.Length; i++)
        {
            var view = _views[i];
            var selected = view == _selectedView;

            builder.AddBackground(34, y - 4, 160, 28, selected ? 9300 : 9250);
            builder.AddButton(42, y, 4005, 4007, ButtonCategoryBase + i);
            builder.AddHtml(
                78,
                y + 1,
                100,
                20,
                HtmlColor(AchievementService.GetJournalViewDisplayName(view), selected ? "#00FF99" : "#D0D0D0")
            );

            y += 36;
        }

        builder.AddImageTiled(34, 536, 160, 2, 5058);
        builder.AddImageTiled(34, 538, 160, 2, 2624);
    }

    private void BuildOverview(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(225, 62, 635, 120, 9250);
        builder.AddHtml(248, 80, 180, 20, HtmlColor("Overview", "#FFFFFF"));
        builder.AddHtml(248, 112, 160, 20, HtmlColor($"Unlocked: {AchievementService.GetUnlockedCount(_from)}", "#66FF66"));
        builder.AddHtml(440, 112, 120, 20, HtmlColor($"Total: {AchievementService.GetDefinitionCount()}", "#FFFFFF"));
        builder.AddHtml(610, 112, 170, 20, HtmlColor($"Completion: {BuildCompletionPercent()}", "#66B2FF"));

        builder.AddBackground(225, 196, 635, 404, 9250);
        builder.AddHtml(248, 214, 180, 20, HtmlColor("Category Progress", "#FFFFFF"));

        var y = 250;

        foreach (var view in _views)
        {
            if (view == AchievementJournalView.Overview)
            {
                continue;
            }

            var total = AchievementService.GetDefinitionCount(view);
            if (total <= 0)
            {
                continue;
            }

            var unlocked = AchievementService.GetUnlockedCount(_from, view);
            var percent = total > 0 ? unlocked * 100 / total : 0;

            builder.AddBackground(246, y - 8, 592, 44, 9200);
            builder.AddHtml(262, y, 180, 20, HtmlColor(AchievementService.GetJournalViewDisplayName(view), "#FFFFFF"));
            builder.AddHtml(450, y, 120, 20, HtmlColor($"{unlocked}/{total}", "#C0C0C0"));
            builder.AddHtml(560, y, 60, 20, HtmlColor($"{percent}%", percent >= 100 ? "#66FF66" : "#66B2FF"));
            DrawProgressBar(ref builder, 640, y + 2, 170, 14, unlocked, total);

            y += 54;
        }
    }

    private void BuildAchievementList(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(225, 62, 635, 538, 9250);
        builder.AddHtml(248, 78, 180, 20, HtmlColor(AchievementService.GetJournalViewDisplayName(_selectedView), "#FFFFFF"));
        builder.AddHtml(628, 78, 190, 20, HtmlColor(BuildCategorySummaryText(), "#C0C0C0"));

        var totalPages = GetTotalPages(_definitions.Count);
        var pageIndex = ClampPageIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;
        var end = Math.Min(start + EntriesPerPage, _definitions.Count);
        var y = 112;

        if (_definitions.Count == 0)
        {
            builder.AddHtml(248, y, 360, 20, HtmlColor("No achievements are registered in this category yet.", "#C0C0C0"));
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                var definition = _definitions[i];
                var progress = AchievementService.GetDisplayedProgress(_from, definition);
                var unlocked = AchievementService.IsUnlocked(_from, definition.Id);
                var unlockRecord = AchievementService.GetUnlockRecord(_from, definition.Id);

                builder.AddBackground(244, y, 596, 66, 9200);
                builder.AddHtml(260, y + 8, 250, 20, HtmlColor(definition.Name, unlocked ? "#66FF66" : "#FFFFFF"));
                builder.AddHtml(520, y + 8, 300, 20, HtmlColor(definition.Description, "#C0C0C0"));
                builder.AddHtml(260, y + 52, 210, 16, HtmlColor(BuildDefinitionMeta(definition), "#909090"));

                if (unlocked && unlockRecord != null)
                {
                    builder.AddHtml(260, y + 34, 190, 20, HtmlColor($"Unlocked {unlockRecord.UnlockedUtc:yyyy-MM-dd}", "#66FF66"));
                    DrawProgressBar(ref builder, 500, y + 38, 300, 12, definition.Threshold, definition.Threshold);
                }
                else
                {
                    builder.AddHtml(260, y + 34, 90, 20, HtmlColor($"{Math.Min(progress, definition.Threshold)}/{definition.Threshold}", "#66B2FF"));
                    DrawProgressBar(ref builder, 360, y + 38, 440, 12, progress, definition.Threshold);
                }

                y += 76;
            }
        }

        builder.AddImageTiled(244, 548, 596, 2, 5058);
        builder.AddImageTiled(244, 550, 596, 2, 2624);
        builder.AddHtml(258, 562, 120, 20, HtmlColor($"Page {pageIndex + 1}/{Math.Max(1, totalPages)}", "#FFFFFF"));

        if (pageIndex > 0)
        {
            builder.AddButton(698, 560, 4014, 4016, ButtonPrevPage);
            builder.AddHtml(733, 562, 45, 20, HtmlColor("Prev", "#FFFFFF"));
        }

        if (pageIndex + 1 < totalPages)
        {
            builder.AddButton(778, 560, 4005, 4007, ButtonNextPage);
            builder.AddHtml(813, 562, 45, 20, HtmlColor("Next", "#FFFFFF"));
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile as PlayerMobile;

        if (from == null)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case 0:
                return;
            case ButtonPrevPage:
                DisplayTo(from, _selectedView, Math.Max(0, _pageIndex - 1));
                return;
            case ButtonNextPage:
                DisplayTo(from, _selectedView, _pageIndex + 1);
                return;
        }

        if (info.ButtonID >= ButtonCategoryBase && info.ButtonID < ButtonCategoryBase + _views.Length)
        {
            DisplayTo(from, _views[info.ButtonID - ButtonCategoryBase], 0);
        }
    }

    private string BuildSummaryText()
    {
        return $"{AchievementService.GetUnlockedCount(_from)}/{AchievementService.GetDefinitionCount()} unlocked";
    }

    private string BuildCategorySummaryText()
    {
        return $"{AchievementService.GetUnlockedCount(_from, _selectedView)}/{AchievementService.GetDefinitionCount(_selectedView)} unlocked";
    }

    private string BuildCompletionPercent()
    {
        var total = AchievementService.GetDefinitionCount();
        var unlocked = AchievementService.GetUnlockedCount(_from);

        return total > 0 ? $"{unlocked * 100 / total}%" : "0%";
    }

    private static void DrawProgressBar(
        ref DynamicGumpBuilder builder,
        int x,
        int y,
        int width,
        int height,
        int progress,
        int threshold
    )
    {
        builder.AddImageTiled(x, y, width, height, 2624);

        if (threshold <= 0)
        {
            return;
        }

        var normalized = Math.Max(0, Math.Min(progress, threshold));
        var fillWidth = width * normalized / threshold;

        if (fillWidth > 0)
        {
            builder.AddImageTiled(x, y, fillWidth, height, 9304);
        }
    }

    private static int GetTotalPages(int count)
    {
        return Math.Max(1, (count + EntriesPerPage - 1) / EntriesPerPage);
    }

    private static int ClampPageIndex(int pageIndex, int totalPages)
    {
        return Math.Max(0, Math.Min(pageIndex, Math.Max(0, totalPages - 1)));
    }

    private static string BuildDefinitionMeta(AchievementDefinition definition)
    {
        var category = AchievementService.GetCategoryDisplayName(definition.Category);
        var scope = AchievementService.GetScopeDisplayName(definition.Scope);
        var legacy = string.IsNullOrWhiteSpace(definition.LegacyLabel) ? "Legacy" : definition.LegacyLabel;
        return definition.IsLegacy ? $"{scope} / {legacy} / {category}" : $"{scope} / {category}";
    }

    private static AchievementJournalView NormalizeJournalView(AchievementJournalView view, IReadOnlyList<AchievementJournalView> views)
    {
        for (var i = 0; i < views.Count; i++)
        {
            if (views[i] == view)
            {
                return view;
            }
        }

        return AchievementJournalView.Overview;
    }

    private static string HtmlColor(string text, string hex)
    {
        return $"<BASEFONT COLOR={hex}>{text}</BASEFONT>";
    }
}
