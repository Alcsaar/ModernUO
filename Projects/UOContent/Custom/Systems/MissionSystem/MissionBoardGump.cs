using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom.Systems.MissionSystem;

public sealed class MissionBoardGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueReady = 68;
    private const int HueProgress = 1153;
    private const int ButtonViewBase = 100;
    private const int ButtonAcceptBase = 1000;
    private const int ButtonClaimBase = 2000;
    private const int ButtonCancelBase = 2500;
    private const int ButtonPrevPage = 3000;
    private const int ButtonNextPage = 3001;
    private const int GumpWidth = 880;
    private const int GumpHeight = 620;
    private const int EntriesPerPage = 4;
    private const int EntryHeight = 96;
    private const int EntryFrameHeight = EntryHeight - 6;
    private const int EntryAccentHeight = 74;
    private const int EntrySpacing = EntryHeight + 4;

    private readonly PlayerMobile _from;
    private readonly MissionBoardView _selectedView;
    private readonly int _pageIndex;
    private readonly bool _staffBypass;
    private readonly PlayerMissionProfile _profile;
    private readonly List<PlayerMissionInstance> _entries;

    public override bool Singleton => true;

    private MissionBoardGump(PlayerMobile from, MissionBoardView selectedView, int pageIndex, bool staffBypass) : base(30, 30)
    {
        _from = from;
        _selectedView = selectedView;
        _pageIndex = Math.Max(0, pageIndex);
        _staffBypass = staffBypass;
        _profile = MissionSystemService.GetOrCreateProfile(from);
        _entries = BuildEntries(_profile, selectedView);
    }

    public static void DisplayTo(PlayerMobile from, MissionBoardView selectedView, int pageIndex = 0, bool staffBypass = false)
    {
        if (from?.NetState == null)
        {
            return;
        }

        if (!staffBypass && !MissionSystemService.IsSystemEnabled())
        {
            from.SendMessage(0x22, "The mission board is not available right now.");
            return;
        }

        from.CloseGump<MissionBoardGump>();
        from.SendGump(new MissionBoardGump(from, selectedView, pageIndex, staffBypass));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);

        BuildHeader(ref builder);
        BuildViewRail(ref builder);
        BuildMissionList(ref builder);
    }

    private void BuildHeader(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(390, 18, HueTitle, "Mission Board");
        builder.AddLabel(330, 44, HueText, "Daily Missives and Weekly Contracts");
        builder.AddLabel(660, 28, HueText, BuildSummaryText());
        DrawRule(ref builder, 24, 62, 832);
    }

    private void BuildViewRail(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(20, 80, 190, 500, 9250);
        builder.AddAlphaRegion(26, 86, 178, 488);
        builder.AddLabel(42, 104, HueHeader, "Boards");

        AddRailButton(ref builder, MissionBoardView.DailyMissives, 0, 144, "Daily Missives");
        AddRailButton(ref builder, MissionBoardView.WeeklyContracts, 1, 184, "Weekly Contracts");
        AddRailButton(ref builder, MissionBoardView.Completed, 2, 224, "Completed");

        DrawRule(ref builder, 42, 284, 144);
        builder.AddLabel(42, 308, HueMuted, "Remaining");
        builder.AddLabel(42, 336, HueText, $"Daily: {MissionSystemService.GetRemainingCompletions(_profile, MissionCadence.DailyMissive)}");
        builder.AddLabel(42, 364, HueText, $"Weekly: {MissionSystemService.GetRemainingCompletions(_profile, MissionCadence.WeeklyContract)}");
    }

    private void AddRailButton(ref DynamicGumpBuilder builder, MissionBoardView view, int index, int y, string label)
    {
        var selected = view == _selectedView;

        if (selected)
        {
            builder.AddImageTiled(40, y, 4, 20, 9304);
        }

        builder.AddButton(52, y, 4005, 4007, ButtonViewBase + index);
        builder.AddLabel(88, y + 2, selected ? HueHeader : HueText, label);
    }

    private void BuildMissionList(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(225, 80, 635, 500, 9250);
        builder.AddAlphaRegion(231, 86, 623, 488);
        builder.AddLabel(248, 104, HueHeader, GetViewTitle(_selectedView));
        builder.AddLabel(642, 104, HueText, BuildViewSummaryText());
        DrawRule(ref builder, 246, 128, 590);

        var totalPages = GetTotalPages(_entries.Count, EntriesPerPage);
        var pageIndex = ClampPageIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;
        var end = Math.Min(start + EntriesPerPage, _entries.Count);
        var y = 150;

        if (_entries.Count == 0)
        {
            builder.AddLabel(248, y, HueMuted, "No missions are available in this section.");
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                DrawMissionEntry(ref builder, _entries[i], i, y);
                y += EntrySpacing;
            }
        }

        DrawPaging(ref builder, pageIndex, totalPages);
    }

    private void DrawMissionEntry(ref DynamicGumpBuilder builder, PlayerMissionInstance instance, int index, int y)
    {
        var definition = MissionSystemService.GetDefinition(instance.DefinitionId);
        var completed = instance.Completed;
        var claimed = instance.Claimed;
        var accepted = instance.Accepted;
        var titleHue = claimed ? HueMuted : completed ? HueReady : accepted ? HueProgress : HueText;

        builder.AddImageTiled(248, y, 4, EntryAccentHeight, completed && !claimed ? 9304 : accepted ? 5058 : 2624);
        DrawEntryFrame(ref builder, 246, y - 8, 592, EntryFrameHeight);

        if (definition == null)
        {
            builder.AddLabel(264, y, HueMuted, "Unavailable mission");
            return;
        }

        builder.AddLabel(264, y, titleHue, definition.Title);
        builder.AddLabel(622, y, HueHeader, FormatObjective(definition.Objective));
        builder.AddHtml(264, y + 24, 430, 24, HtmlColor(definition.Description, "#D0D0D0"));
        builder.AddLabel(264, y + 48, HueProgress, $"{Math.Min(instance.CurrentProgress, instance.RequiredProgress)}/{instance.RequiredProgress}");
        DrawProgressBar(ref builder, 340, y + 54, 210, 12, instance.CurrentProgress, instance.RequiredProgress);
        builder.AddLabel(570, y + 48, HueText, $"{definition.Reward?.Gold ?? 0} gold");

        // Hide accept actions once the player has reserved every completion slot for this cadence.
        if (!accepted && !completed && !claimed && MissionSystemService.GetRemainingCompletions(_profile, instance.Cadence) > 0)
        {
            builder.AddButton(742, y + 42, 4005, 4007, ButtonAcceptBase + index);
            builder.AddLabel(776, y + 44, HueText, "Accept");
        }
        else if (completed && !claimed)
        {
            builder.AddButton(748, y + 42, 4005, 4007, ButtonClaimBase + index);
            builder.AddLabel(782, y + 44, HueReady, "Claim");
        }
        else if (claimed)
        {
            builder.AddLabel(744, y + 44, HueMuted, "Claimed");
        }
        else if (accepted)
        {
            builder.AddButton(740, y + 42, 4017, 4019, ButtonCancelBase + index);
            builder.AddLabel(774, y + 44, HueProgress, "Cancel");
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
                DisplayTo(from, _selectedView, Math.Max(0, _pageIndex - 1), _staffBypass);
                return;
            case ButtonNextPage:
                DisplayTo(from, _selectedView, _pageIndex + 1, _staffBypass);
                return;
        }

        if (info.ButtonID >= ButtonViewBase && info.ButtonID < ButtonViewBase + 3)
        {
            DisplayTo(from, (MissionBoardView)(info.ButtonID - ButtonViewBase), 0, _staffBypass);
            return;
        }

        if (info.ButtonID >= ButtonAcceptBase && info.ButtonID < ButtonAcceptBase + _entries.Count)
        {
            var entry = _entries[info.ButtonID - ButtonAcceptBase];
            MissionSystemService.AcceptMission(from, entry.InstanceId);
            DisplayTo(from, _selectedView, _pageIndex, _staffBypass);
            return;
        }

        if (info.ButtonID >= ButtonCancelBase && info.ButtonID < ButtonCancelBase + _entries.Count)
        {
            var entry = _entries[info.ButtonID - ButtonCancelBase];
            MissionSystemService.CancelMission(from, entry.InstanceId);
            DisplayTo(from, _selectedView, _pageIndex, _staffBypass);
            return;
        }

        if (info.ButtonID >= ButtonClaimBase && info.ButtonID < ButtonClaimBase + _entries.Count)
        {
            var entry = _entries[info.ButtonID - ButtonClaimBase];
            MissionSystemService.ClaimMission(from, entry.InstanceId);
            DisplayTo(from, _selectedView, _pageIndex, _staffBypass);
        }
    }

    private static List<PlayerMissionInstance> BuildEntries(PlayerMissionProfile profile, MissionBoardView view)
    {
        if (view == MissionBoardView.Completed)
        {
            return MissionSystemService.GetCompletedUnclaimed(profile);
        }

        var cadence = view == MissionBoardView.DailyMissives ? MissionCadence.DailyMissive : MissionCadence.WeeklyContract;
        var source = MissionSystemService.GetInstances(profile, cadence);
        var entries = new List<PlayerMissionInstance>(source.Count);

        for (var i = 0; i < source.Count; i++)
        {
            entries.Add(source[i]);
        }

        return entries;
    }

    private string BuildSummaryText()
    {
        return $"{MissionSystemService.Definitions.Count} definitions";
    }

    private string BuildViewSummaryText()
    {
        return _selectedView == MissionBoardView.Completed ? $"{_entries.Count} ready" : $"{_entries.Count} offered";
    }

    private static string GetViewTitle(MissionBoardView view)
    {
        return view switch
        {
            MissionBoardView.WeeklyContracts => "Weekly Contracts",
            MissionBoardView.Completed => "Completed",
            _ => "Daily Missives"
        };
    }

    private static string FormatObjective(MissionObjective objective)
    {
        return objective?.Kind switch
        {
            MissionObjectiveKind.KillCreature => "Specific",
            MissionObjectiveKind.KillCreatureFamily => "Family",
            MissionObjectiveKind.KillRegion => "Region",
            _ => "Mission"
        };
    }

    private static void DrawProgressBar(ref DynamicGumpBuilder builder, int x, int y, int width, int height, int progress, int required)
    {
        builder.AddImageTiled(x, y, width, height, 2624);

        if (required <= 0)
        {
            return;
        }

        var fill = width * Math.Max(0, Math.Min(progress, required)) / required;

        if (fill > 0)
        {
            builder.AddImageTiled(x, y, fill, height, 9304);
        }
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }

    private static void DrawEntryFrame(ref DynamicGumpBuilder builder, int x, int y, int width, int height)
    {
        // Mirrors the subdued Achievement Journal row framing for dense mission entries.
        builder.AddImageTiled(x, y, width, 1, 5058);
        builder.AddImageTiled(x, y + 1, width, 1, 2624);
        builder.AddImageTiled(x, y + height - 2, width, 1, 2624);
        builder.AddImageTiled(x, y + height - 1, width, 1, 5058);
        builder.AddImageTiled(x, y, 1, height, 2624);
        builder.AddImageTiled(x + width - 1, y, 1, height, 2624);
    }

    private static void DrawPaging(ref DynamicGumpBuilder builder, int pageIndex, int totalPages)
    {
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

    private static int GetTotalPages(int count, int entriesPerPage)
    {
        return Math.Max(1, (count + entriesPerPage - 1) / entriesPerPage);
    }

    private static int ClampPageIndex(int pageIndex, int totalPages)
    {
        return Math.Max(0, Math.Min(pageIndex, Math.Max(0, totalPages - 1)));
    }

    private static string HtmlColor(string text, string hex)
    {
        return $"<BASEFONT COLOR={hex}>{text}</BASEFONT>";
    }
}
