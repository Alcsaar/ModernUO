using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom.Systems.AchievementSystem;

public sealed class AchievementGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueReady = 68;
    private const int HueProgress = 1153;
    private const int ButtonCategoryBase = 100;
    private const int ButtonPrevPage = 2000;
    private const int ButtonNextPage = 2001;
    private const int ButtonAccountSkillDetails = 2002;
    private const int GumpWidth = 880;
    private const int GumpHeight = 620;
    private const int EntriesPerPage = 4;
    private const int EntryHeight = 94;
    private const int EntryFrameHeight = EntryHeight - 6;
    private const int EntryAccentHeight = 74;
    private const int EntrySpacing = EntryHeight + 4;
    private const int OverviewRowsPerPage = 5;

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
        _definitions = AchievementService.GetJournalDefinitions(from, _selectedView);
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
        builder.AddLabel(385, 18, HueTitle, "Achievement Journal");
        builder.AddLabel(338, 44, HueText, "Feats, milestones, and shard records");
        builder.AddLabel(690, 28, HueText, BuildSummaryText());
        DrawRule(ref builder, 24, 62, 832);
    }

    private void BuildCategoryRail(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(20, 80, 190, 500, 9250);
        builder.AddAlphaRegion(26, 86, 178, 488);
        builder.AddLabel(42, 104, HueHeader, "Categories");

        var y = 142;
        AchievementJournalRailSection? currentSection = null;

        for (var i = 0; i < _views.Length; i++)
        {
            var view = _views[i];
            var selected = view == _selectedView;
            var section = GetRailSection(view);

            if (currentSection != section)
            {
                if (currentSection != null)
                {
                    y += 8;
                    DrawRule(ref builder, 42, y, 144);
                    y += 14;
                }

                if (section == AchievementJournalRailSection.Character)
                {
                    builder.AddLabel(42, y, HueMuted, "Character");
                    y += 24;
                }

                currentSection = section;
            }

            if (selected)
            {
                builder.AddImageTiled(40, y, 4, 20, 9304);
            }

            builder.AddButton(52, y, 4005, 4007, ButtonCategoryBase + i);
            builder.AddLabel(88, y + 2, selected ? HueHeader : HueText, AchievementService.GetJournalViewDisplayName(view));

            y += 36;
        }

        DrawRule(ref builder, 42, 535, 144);
    }

    private void BuildOverview(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(225, 80, 635, 500, 9250);
        builder.AddAlphaRegion(231, 86, 623, 488);
        builder.AddLabel(248, 104, HueHeader, "Overview");
        builder.AddLabel(640, 104, HueText, BuildSummaryText());
        DrawRule(ref builder, 246, 128, 590);

        builder.AddLabel(260, 154, HueReady, $"Unlocked: {AchievementService.GetUnlockedCount(_from)}");
        builder.AddLabel(440, 154, HueText, $"Total: {AchievementService.GetDefinitionCount()}");
        builder.AddLabel(590, 154, HueProgress, $"Completion: {BuildCompletionPercent()}");

        DrawRule(ref builder, 246, 205, 590);
        builder.AddLabel(260, 226, HueHeader, "Category Progress");

        var overviewViews = GetOverviewViews();
        var totalPages = GetTotalPages(overviewViews.Count, OverviewRowsPerPage);
        var pageIndex = ClampPageIndex(_pageIndex, totalPages);
        var start = pageIndex * OverviewRowsPerPage;
        var end = Math.Min(start + OverviewRowsPerPage, overviewViews.Count);
        var y = 270;

        for (var i = start; i < end; i++)
        {
            var view = overviewViews[i];
            var total = AchievementService.GetDefinitionCount(view);
            var unlocked = AchievementService.GetUnlockedCount(_from, view);
            var percent = total > 0 ? unlocked * 100 / total : 0;

            var featsView = view == AchievementJournalView.Feats;
            builder.AddImageTiled(260, y, 4, 34, percent >= 100 && !featsView ? 9304 : 5058);
            builder.AddLabel(276, y, HueText, AchievementService.GetJournalViewDisplayName(view));

            if (featsView)
            {
                builder.AddLabel(460, y, HueMuted, FormatClaimedCount(unlocked));
            }
            else
            {
                builder.AddLabel(460, y, HueMuted, $"{unlocked}/{total}");
                builder.AddLabel(550, y, percent >= 100 ? HueReady : HueProgress, $"{percent}%");
                DrawProgressBar(ref builder, 640, y + 4, 170, 12, unlocked, total);
            }

            y += 54;
        }

        DrawOverviewPaging(ref builder, pageIndex, totalPages);
    }

    private void BuildAchievementList(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(225, 80, 635, 500, 9250);
        builder.AddAlphaRegion(231, 86, 623, 488);
        builder.AddLabel(248, 104, HueHeader, AchievementService.GetJournalViewDisplayName(_selectedView));
        builder.AddLabel(640, 104, HueText, BuildCategorySummaryText());
        DrawRule(ref builder, 246, 128, 590);

        var totalPages = GetTotalPages(_definitions.Count, EntriesPerPage);
        var pageIndex = ClampPageIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;
        var end = Math.Min(start + EntriesPerPage, _definitions.Count);
        var y = 150;

        if (_definitions.Count == 0)
        {
            builder.AddLabel(248, y, HueMuted, "No achievements are registered in this category yet.");
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                var definition = _definitions[i];
                var progress = AchievementService.GetDisplayedProgress(_from, definition);
                var unlocked = AchievementService.IsUnlocked(_from, definition.Id);
                var unlockRecord = AchievementService.GetUnlockRecord(_from, definition.Id);
                var serverFirstRecord = definition.TriggerType == AchievementTriggerType.ServerFirstSkillMilestone
                    ? AchievementService.GetServerFirstRecord(definition.Id)
                    : null;
                var claimedByAnother = serverFirstRecord != null && !unlocked;
                var claimedBySelf = serverFirstRecord != null && unlocked;

                var milestones = AchievementService.GetTierMilestones(definition);
                var isMilestoneChain = milestones.Count > 1;
                var finalThreshold = isMilestoneChain ? milestones[^1].Threshold : definition.Threshold;
                var displayProgress = isMilestoneChain
                    ? AchievementService.GetDisplayedTierProgress(_from, milestones)
                    : progress;

                if (unlocked)
                {
                    builder.AddImageTiled(248, y, 4, EntryAccentHeight, 9304);
                }
                else if (claimedByAnother)
                {
                    builder.AddImageTiled(248, y, 4, EntryAccentHeight, 2624);
                }
                else
                {
                    builder.AddImageTiled(248, y, 4, EntryAccentHeight, 5058);
                }

                var titleHue = unlocked ? HueReady : claimedByAnother ? HueMuted : HueText;
                var textColor = claimedByAnother ? "#909090" : "#D0D0D0";
                var metaHue = claimedByAnother ? HueMuted : HueHeader;
                DrawEntryFrame(ref builder, 246, y - 8, 592, EntryFrameHeight);
                builder.AddLabel(264, y, titleHue, BuildEntryTitle(definition));
                builder.AddLabel(620, y, metaHue, BuildDefinitionMeta(definition, serverFirstRecord, claimedByAnother, claimedBySelf));
                builder.AddHtml(264, y + 24, 440, 24, HtmlColor(definition.Description, textColor));

                if (definition.TriggerType == AchievementTriggerType.AccountUniqueGrandmasterSkills)
                {
                    builder.AddButton(740, y + 42, 4005, 4007, ButtonAccountSkillDetails);
                    builder.AddLabel(774, y + 44, HueText, "Details");
                }

                if (unlocked && unlockRecord != null)
                {
                    builder.AddLabel(264, y + 44, HueReady, BuildUnlockedText(definition, unlockRecord));
                    if (isMilestoneChain)
                    {
                        DrawMilestoneProgressBar(ref builder, milestones, y, displayProgress, finalThreshold, claimedByAnother);
                    }
                    else
                    {
                        DrawProgressBar(ref builder, 500, y + 62, 220, 12, definition.Threshold, definition.Threshold);
                    }
                }
                else if (claimedByAnother)
                {
                    builder.AddLabel(264, y + 44, HueMuted, BuildClaimedByText(serverFirstRecord));
                    if (isMilestoneChain)
                    {
                        DrawMilestoneProgressBar(ref builder, milestones, y, displayProgress, finalThreshold, claimedByAnother);
                    }
                    else
                    {
                        DrawUnavailableBar(ref builder, 500, y + 62, 220, 12);
                    }
                }
                else
                {
                    builder.AddLabel(264, y + 44, HueProgress, $"{Math.Min(displayProgress, definition.Threshold)}/{definition.Threshold}");
                    if (isMilestoneChain)
                    {
                        DrawMilestoneProgressBar(ref builder, milestones, y, displayProgress, finalThreshold, claimedByAnother);
                    }
                    else
                    {
                        DrawProgressBar(ref builder, 500, y + 62, 220, 12, progress, definition.Threshold);
                    }
                }

                y += EntrySpacing;
            }
        }

        DrawRule(ref builder, 246, 535, 590);
        builder.AddLabel(258, 552, HueText, $"Page {pageIndex + 1}/{Math.Max(1, totalPages)}");

        if (pageIndex > 0)
        {
            builder.AddButton(708, 550, 4014, 4016, ButtonPrevPage);
            builder.AddLabel(742, 552, HueText, "Prev");
        }

        if (pageIndex + 1 < totalPages)
        {
            builder.AddButton(788, 550, 4005, 4007, ButtonNextPage);
            builder.AddLabel(822, 552, HueText, "Next");
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
            case ButtonAccountSkillDetails:
                AchievementAccountSkillProgressGump.DisplayTo(from);
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
        if (_selectedView == AchievementJournalView.Feats)
        {
            var claimed = AchievementService.GetUnlockedCount(_from, _selectedView);

            return FormatClaimedCount(claimed);
        }

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

    private static void DrawUnavailableBar(ref DynamicGumpBuilder builder, int x, int y, int width, int height)
    {
        builder.AddImageTiled(x, y, width, height, 2624);
    }

    /* BEGIN ACHIEVEMENT JOURNAL MILESTONES: draw collapsed tier chains with every target visible */
    private static void DrawMilestoneProgressBar(
        ref DynamicGumpBuilder builder,
        IReadOnlyList<AchievementDefinition> milestones,
        int rowY,
        int progress,
        int finalThreshold,
        bool unavailable
    )
    {
        const int barX = 390;
        const int barY = 62;
        const int barWidth = 330;
        const int barHeight = 12;

        if (finalThreshold <= 0)
        {
            return;
        }

        var fillWidth = unavailable ? 0 : GetMilestoneFillWidth(milestones, progress, barWidth);
        builder.AddImageTiled(barX, rowY + barY, barWidth, barHeight, 2624);

        if (fillWidth > 0)
        {
            builder.AddImageTiled(barX, rowY + barY, fillWidth, barHeight, 9304);
        }

        for (var i = 0; i < milestones.Count; i++)
        {
            var milestone = milestones[i];
            var markerX = barX + GetMilestoneMarkerOffset(milestones.Count, i, barWidth);
            var achieved = progress >= milestone.Threshold;
            var labelX = Math.Max(barX, Math.Min(markerX - 16, barX + barWidth - 42));

            builder.AddImageTiled(markerX, rowY + barY - 4, 2, barHeight + 8, achieved ? 9304 : 5058);
            builder.AddLabel(labelX, rowY + 42, achieved ? HueReady : HueText, FormatMilestoneThreshold(milestone.Threshold));
        }
    }

    private static int GetMilestoneFillWidth(IReadOnlyList<AchievementDefinition> milestones, int progress, int barWidth)
    {
        if (milestones.Count == 0 || progress <= 0)
        {
            return 0;
        }

        if (progress >= milestones[^1].Threshold)
        {
            return barWidth;
        }

        var previousThreshold = 0;
        var previousOffset = 0;

        for (var i = 0; i < milestones.Count; i++)
        {
            var milestone = milestones[i];
            var markerOffset = GetMilestoneMarkerOffset(milestones.Count, i, barWidth);

            if (progress <= milestone.Threshold)
            {
                var thresholdSpan = Math.Max(1, milestone.Threshold - previousThreshold);
                var offsetSpan = markerOffset - previousOffset;
                var segmentProgress = Math.Max(0, progress - previousThreshold);

                return previousOffset + offsetSpan * segmentProgress / thresholdSpan;
            }

            previousThreshold = milestone.Threshold;
            previousOffset = markerOffset;
        }

        return barWidth;
    }

    private static int GetMilestoneMarkerOffset(int milestoneCount, int index, int barWidth)
    {
        if (milestoneCount <= 1)
        {
            return barWidth;
        }

        const int edgePadding = 10;
        var usableWidth = Math.Max(1, barWidth - edgePadding * 2);

        return edgePadding + usableWidth * index / (milestoneCount - 1);
    }

    private static string FormatMilestoneThreshold(int threshold)
    {
        if (threshold >= 1000000)
        {
            return threshold % 1000000 == 0 ? $"{threshold / 1000000}M" : $"{threshold / 1000000.0:0.#}M";
        }

        if (threshold >= 1000)
        {
            return threshold % 1000 == 0 ? $"{threshold / 1000}K" : $"{threshold / 1000.0:0.#}K";
        }

        return threshold.ToString();
    }
    /* END ACHIEVEMENT JOURNAL MILESTONES */

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }

    private static void DrawEntryFrame(ref DynamicGumpBuilder builder, int x, int y, int width, int height)
    {
        // Adds a subdued row frame so dense achievement entries separate cleanly without bright panel chrome.
        builder.AddImageTiled(x, y, width, 1, 5058);
        builder.AddImageTiled(x, y + 1, width, 1, 2624);
        builder.AddImageTiled(x, y + height - 2, width, 1, 2624);
        builder.AddImageTiled(x, y + height - 1, width, 1, 5058);
        builder.AddImageTiled(x, y, 1, height, 2624);
        builder.AddImageTiled(x + width - 1, y, 1, height, 2624);
    }

    private static string FormatClaimedCount(int count)
    {
        return count == 1 ? "1 claimed" : $"{count} claimed";
    }

    private List<AchievementJournalView> GetOverviewViews()
    {
        var views = new List<AchievementJournalView>(_views.Length);

        for (var i = 0; i < _views.Length; i++)
        {
            var view = _views[i];

            if (view == AchievementJournalView.Overview || AchievementService.GetDefinitionCount(view) <= 0)
            {
                continue;
            }

            views.Add(view);
        }

        return views;
    }

    /* BEGIN ACHIEVEMENT JOURNAL OVERVIEW: page the category summary list inside the fixed content frame */
    private static void DrawOverviewPaging(ref DynamicGumpBuilder builder, int pageIndex, int totalPages)
    {
        DrawRule(ref builder, 246, 526, 590);
        builder.AddLabel(258, 542, HueText, $"Page {pageIndex + 1}/{Math.Max(1, totalPages)}");

        if (totalPages <= 1)
        {
            return;
        }

        if (pageIndex > 0)
        {
            builder.AddButton(688, 540, 4014, 4016, ButtonPrevPage);
            builder.AddLabel(722, 542, HueText, "Prev");
        }

        if (pageIndex + 1 < totalPages)
        {
            builder.AddButton(776, 540, 4005, 4007, ButtonNextPage);
            builder.AddLabel(810, 542, HueText, "Next");
        }
    }
    /* END ACHIEVEMENT JOURNAL OVERVIEW */

    private static int GetTotalPages(int count, int entriesPerPage)
    {
        return Math.Max(1, (count + entriesPerPage - 1) / entriesPerPage);
    }

    private static int ClampPageIndex(int pageIndex, int totalPages)
    {
        return Math.Max(0, Math.Min(pageIndex, Math.Max(0, totalPages - 1)));
    }

    private static AchievementJournalRailSection GetRailSection(AchievementJournalView view)
    {
        return view switch
        {
            AchievementJournalView.CharacterSkills or
                AchievementJournalView.CharacterHunting or
                AchievementJournalView.CharacterExploration or
                AchievementJournalView.CharacterHarvesting or
                AchievementJournalView.CharacterEconomy or
                AchievementJournalView.CharacterMissions =>
                AchievementJournalRailSection.Character,
            AchievementJournalView.Account => AchievementJournalRailSection.Account,
            AchievementJournalView.Feats or AchievementJournalView.Legacy => AchievementJournalRailSection.Feats,
            _ => AchievementJournalRailSection.General
        };
    }

    private static string BuildDefinitionMeta(
        AchievementDefinition definition,
        AchievementServerFirstRecord serverFirstRecord = null,
        bool claimedByAnother = false,
        bool claimedBySelf = false
    )
    {
        var category = AchievementService.GetCategoryDisplayName(definition.Category);

        if (definition.TriggerType == AchievementTriggerType.ServerFirstSkillMilestone)
        {
            if (claimedBySelf && serverFirstRecord != null)
            {
                return "Your Server First";
            }

            return claimedByAnother && serverFirstRecord != null ? "Claimed" : "Server First";
        }

        if (definition.IsLegacy)
        {
            var legacy = string.IsNullOrWhiteSpace(definition.LegacyLabel) ? "Retired" : definition.LegacyLabel;
            return legacy;
        }

        return category;
    }

    private static string BuildEntryTitle(AchievementDefinition definition)
    {
        if (
            definition.TriggerType == AchievementTriggerType.ServerFirstSkillMilestone &&
            definition.Name.StartsWith("Server First: ", StringComparison.OrdinalIgnoreCase)
        )
        {
            return definition.Name["Server First: ".Length..];
        }

        return definition.Name;
    }

    private static string BuildClaimedByText(AchievementServerFirstRecord record)
    {
        return record == null ? "No longer available" : $"Claimed by {record.PlayerName}";
    }

    private static string BuildUnlockedText(AchievementDefinition definition, AchievementUnlockRecord unlockRecord)
    {
        return definition.TriggerType == AchievementTriggerType.ServerFirstSkillMilestone
            ? $"Claimed {unlockRecord.UnlockedUtc:yyyy-MM-dd}"
            : $"Unlocked {unlockRecord.UnlockedUtc:yyyy-MM-dd}";
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

    private enum AchievementJournalRailSection
    {
        General,
        Character,
        Account,
        Feats
    }
}
