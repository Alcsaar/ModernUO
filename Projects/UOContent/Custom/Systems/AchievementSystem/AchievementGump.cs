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
    private const int EntriesPerPage = 5;

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

        var y = 270;

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
    }

    private void BuildAchievementList(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(225, 80, 635, 500, 9250);
        builder.AddAlphaRegion(231, 86, 623, 488);
        builder.AddLabel(248, 104, HueHeader, AchievementService.GetJournalViewDisplayName(_selectedView));
        builder.AddLabel(640, 104, HueText, BuildCategorySummaryText());
        DrawRule(ref builder, 246, 128, 590);

        var totalPages = GetTotalPages(_definitions.Count);
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

                if (unlocked)
                {
                    builder.AddImageTiled(248, y, 4, 56, 9304);
                }
                else if (claimedByAnother)
                {
                    builder.AddImageTiled(248, y, 4, 56, 2624);
                }
                else
                {
                    builder.AddImageTiled(248, y, 4, 56, 5058);
                }

                var titleHue = unlocked ? HueReady : claimedByAnother ? HueMuted : HueText;
                var textColor = claimedByAnother ? "#909090" : "#D0D0D0";
                var metaHue = claimedByAnother ? HueMuted : HueHeader;
                DrawEntryFrame(ref builder, 246, y - 8, 592, 72);
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
                    DrawProgressBar(ref builder, 500, y + 46, 220, 12, definition.Threshold, definition.Threshold);
                }
                else if (claimedByAnother)
                {
                    builder.AddLabel(264, y + 44, HueMuted, BuildClaimedByText(serverFirstRecord));
                    DrawUnavailableBar(ref builder, 500, y + 46, 220, 12);
                }
                else
                {
                    builder.AddLabel(264, y + 44, HueProgress, $"{Math.Min(progress, definition.Threshold)}/{definition.Threshold}");
                    DrawProgressBar(ref builder, 500, y + 46, 220, 12, progress, definition.Threshold);
                }

                y += 78;
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

    private static int GetTotalPages(int count)
    {
        return Math.Max(1, (count + EntriesPerPage - 1) / EntriesPerPage);
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
                AchievementJournalView.CharacterHarvesting or
                AchievementJournalView.CharacterEconomy =>
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
