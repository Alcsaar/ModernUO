using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom.Systems.VirtueAlignment;

public enum VirtueAlignmentGumpMode
{
    Overview,
    Detail,
    ChoosePrimaryAspiration,
    ChooseSecondaryAspiration,
    Confirm
}

public sealed class VirtueAlignmentGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueVirtue = 68;
    private const int HueVice = 33;
    private const int HueWarn = 33;
    private const string HtmlText = "#D8D8D8";
    private const string HtmlVirtue = "#66FF66";
    private const string HtmlVice = "#FF5555";
    private const string HtmlTitle = "#F0E6C8";
    private const string HtmlMuted = "#BEBEBE";

    private const int ButtonOverview = 10;
    private const int ButtonChoosePrimary = 11;
    private const int ButtonBackToPrimary = 12;
    private const int ButtonConfirm = 13;
    private const int ButtonClear = 14;
    private const int ButtonReturnToSelection = 15;
    private const int ButtonPreviousDetail = 16;
    private const int ButtonNextDetail = 17;
    private const int ButtonPathBase = 100;
    private const int ButtonDetailBase = 200;

    private readonly PlayerMobile _from;
    private readonly VirtueAlignmentGumpMode _mode;
    private readonly VirtueAlignmentGumpMode _returnMode;
    private readonly VirtueAlignmentPath _primary;
    private readonly VirtueAlignmentPath _secondary;
    private readonly VirtueAlignmentPath _detailPath;

    private static readonly VirtueAlignmentPath[] _allPaths =
    [
        VirtueAlignmentPath.Compassion,
        VirtueAlignmentPath.Cruelty,
        VirtueAlignmentPath.Justice,
        VirtueAlignmentPath.Vengeance,
        VirtueAlignmentPath.Honesty,
        VirtueAlignmentPath.Deceit,
        VirtueAlignmentPath.Honor,
        VirtueAlignmentPath.Treachery,
        VirtueAlignmentPath.Spirituality,
        VirtueAlignmentPath.Corruption,
        VirtueAlignmentPath.Valor,
        VirtueAlignmentPath.Cowardice,
        VirtueAlignmentPath.Sacrifice,
        VirtueAlignmentPath.Greed,
        VirtueAlignmentPath.Humility,
        VirtueAlignmentPath.Pride
    ];

    public override bool Singleton => true;

    private VirtueAlignmentGump(
        PlayerMobile from,
        VirtueAlignmentGumpMode mode,
        VirtueAlignmentGumpMode returnMode,
        VirtueAlignmentPath primary,
        VirtueAlignmentPath secondary,
        VirtueAlignmentPath detailPath
    ) : base(55, 45)
    {
        _from = from;
        _mode = mode;
        _returnMode = returnMode;
        _primary = primary;
        _secondary = secondary;
        _detailPath = detailPath;
    }

    public static void DisplayTo(PlayerMobile from)
    {
        var profile = VirtueAlignmentService.GetProfile(from);
        var primary = profile?.PrimaryAspiration ?? VirtueAlignmentPath.None;
        var secondary = profile?.SecondaryAspiration ?? VirtueAlignmentPath.None;

        if (VirtueAlignmentService.IsValidPair(primary, secondary))
        {
            DisplayTo(from, VirtueAlignmentGumpMode.Confirm, primary, secondary, VirtueAlignmentPath.None);
            return;
        }

        DisplayTo(from, VirtueAlignmentGumpMode.Overview, primary, secondary, GetInitialOverviewPath(primary));
    }

    private static void DisplayTo(
        PlayerMobile from,
        VirtueAlignmentGumpMode mode,
        VirtueAlignmentPath primary,
        VirtueAlignmentPath secondary,
        VirtueAlignmentPath detailPath
    )
    {
        from.CloseGump<VirtueAlignmentGump>();
        from.SendGump(new VirtueAlignmentGump(from, mode, VirtueAlignmentGumpMode.Overview, primary, secondary, detailPath));
    }

    private static void DisplayTo(
        PlayerMobile from,
        VirtueAlignmentGumpMode mode,
        VirtueAlignmentGumpMode returnMode,
        VirtueAlignmentPath primary,
        VirtueAlignmentPath secondary,
        VirtueAlignmentPath detailPath
    )
    {
        from.CloseGump<VirtueAlignmentGump>();
        from.SendGump(new VirtueAlignmentGump(from, mode, returnMode, primary, secondary, detailPath));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 690, 520, 9270);
        builder.AddAlphaRegion(18, 18, 654, 484);

        if (_mode != VirtueAlignmentGumpMode.Confirm)
        {
            builder.AddLabel(38, 28, HueTitle, "Virtue Alignment");
        }

        switch (_mode)
        {
            case VirtueAlignmentGumpMode.Detail:
                DrawDetail(ref builder);
                break;
            case VirtueAlignmentGumpMode.ChoosePrimaryAspiration:
                DrawPrimaryChoice(ref builder);
                break;
            case VirtueAlignmentGumpMode.ChooseSecondaryAspiration:
                DrawSecondaryChoice(ref builder);
                break;
            case VirtueAlignmentGumpMode.Confirm:
                DrawConfirm(ref builder);
                break;
            default:
                DrawOverview(ref builder);
                break;
        }
    }

    private void DrawOverview(ref DynamicGumpBuilder builder)
    {
        var profile = VirtueAlignmentService.GetProfile(_from);
        var current = profile?.HasAspirations == true
            ? VirtueAlignmentService.GetSummary(_from)
            : "No aspirations declared. Your deeds can still begin shaping an expressed alignment.";

        builder.AddHtml(
            38,
            62,
            604,
            64,
            "Declare the Virtue or Vice your character aspires toward, then choose a counterweight from the opposite side. Aspirations guide the story, but your deeds ultimately reveal your expressed alignment.",
            "#D8D8D8",
            scrollbar: false
        );

        builder.AddLabel(38, 132, HueMuted, "Current:");
        builder.AddHtml(116, 132, 520, 24, current, "#D8D8D8", scrollbar: false);
        DrawConvictionStatus(ref builder, 38, 154, 604);

        /*
         * The overview is a tutorial carousel instead of a dense list. It keeps
         * the system rules visible while letting players read each path in place.
         */
        builder.AddImageTiled(38, 182, 604, 2, 9107);
        DrawPathSpotlight(ref builder, GetOverviewPath(), 38, 202, 604, 206);

        DrawCarouselControls(
            ref builder,
            chooseLabel: profile?.HasAspirations == true ? "New Aspiration" : "Aspire Toward"
        );

        if (_from.AccessLevel >= AccessLevel.GameMaster && profile?.HasAspirations == true)
        {
            builder.AddButton(540, 466, 4017, 4019, ButtonClear);
            builder.AddLabel(574, 468, HueWarn, "Clear");
        }
    }

    private void DrawDetail(ref DynamicGumpBuilder builder)
    {
        DrawPathSpotlight(ref builder, _detailPath, 38, 78, 604, 322);

        builder.AddButton(246, 436, 4014, 4016, ButtonReturnToSelection);
        builder.AddLabel(280, 438, HueText, GetReturnLabel());
        builder.AddButton(430, 436, 4005, 4007, ButtonChoosePrimary);
        builder.AddLabel(464, 438, HueText, GetChooseDisplayedPathLabel());
    }

    private void DrawPrimaryChoice(ref DynamicGumpBuilder builder)
    {
        builder.AddHtml(38, 62, 604, 44, "First choose the path your character aspires toward. This is intent, not proof; future deeds can affirm or contradict it.", "#D8D8D8", scrollbar: false);
        DrawCurrentDraft(ref builder, 38, 112);

        builder.AddLabel(38, 154, HueTitle, "Aspire Toward Virtue");
        DrawChoiceList(ref builder, VirtueAlignmentService.Virtues, 38, 186, primaryChoice: true);

        builder.AddLabel(360, 154, HueTitle, "Aspire Toward Vice");
        DrawChoiceList(ref builder, VirtueAlignmentService.Vices, 360, 186, primaryChoice: true);

        builder.AddButton(252, 466, 4014, 4016, ButtonOverview);
        builder.AddLabel(286, 468, HueText, "Back");
    }

    private void DrawSecondaryChoice(ref DynamicGumpBuilder builder)
    {
        var primarySide = VirtueAlignmentService.GetSide(_primary);
        var secondaryPath = GetSecondaryPath();

        builder.AddHtml(
            38,
            62,
            604,
            64,
            "Now choose the counterweight your character recognizes. Only paths from the opposite side are available, and the direct counterpart to your primary aspiration is excluded.",
            "#D8D8D8",
            scrollbar: false
        );
        DrawCurrentDraft(ref builder, 38, 132);

        builder.AddImageTiled(38, 164, 604, 2, 9107);
        builder.AddLabel(38, 174, HueTitle, primarySide == VirtueAlignmentSide.Virtue ? "Choose Counterweight Vice" : "Choose Counterweight Virtue");
        DrawPathSpotlight(ref builder, secondaryPath, 38, 204, 604, 204);

        DrawCarouselControls(ref builder, "Choose Counterweight");

        builder.AddButton(90, 466, 4014, 4016, ButtonBackToPrimary);
        builder.AddLabel(124, 468, HueText, "Change Aspiration");
    }

    private void DrawConfirm(ref DynamicGumpBuilder builder)
    {
        var savedSelection = IsSavedSelection();

        if (savedSelection)
        {
            DrawSavedStatus(ref builder);
            return;
        }

        DrawConfirmColumn(ref builder, _primary, "Aspiration", 54, 76, 264);
        builder.AddImageTiled(344, 74, 2, 328, 9105);
        DrawConfirmColumn(ref builder, _secondary, "Counterweight", 374, 76, 264);
        DrawConvictionStatus(ref builder, 54, 404, 584);

        builder.AddButton(166, 430, 4014, 4016, ButtonBackToPrimary);
        builder.AddLabel(200, 432, HueText, "Rechoose");

        builder.AddButton(358, 430, 4005, 4007, ButtonConfirm);
        builder.AddLabel(392, 432, HueText, "Confirm Aspirations");
    }

    private void DrawChoiceList(
        ref DynamicGumpBuilder builder,
        IReadOnlyList<VirtueAlignmentPath> paths,
        int x,
        int y,
        bool primaryChoice,
        VirtueAlignmentPath excludedPath = VirtueAlignmentPath.None
    )
    {
        for (var i = 0; i < paths.Count; i++)
        {
            var path = paths[i];

            if (path == excludedPath)
            {
                continue;
            }

            var side = VirtueAlignmentService.GetSide(path);
            var hue = side == VirtueAlignmentSide.Virtue ? HueVirtue : HueVice;
            var selected = primaryChoice ? _primary == path : _secondary == path;

            builder.AddButton(x, y, 4005, 4007, ButtonPathBase + (int)path);
            DrawPathIcon(ref builder, x + 34, y - 3, path);
            builder.AddLabel(x + 72, y + 2, selected ? hue : HueText, VirtueAlignmentService.GetDisplayName(path));
            builder.AddButton(x + 178, y, 4011, 4013, ButtonDetailBase + (int)path);
            builder.AddLabel(x + 212, y + 2, HueMuted, "Details");

            y += 28;
        }
    }

    private void DrawCurrentDraft(ref DynamicGumpBuilder builder, int x, int y)
    {
        builder.AddLabel(x, y, HueMuted, $"Aspiration: {VirtueAlignmentService.GetDisplayName(_primary)}");
        builder.AddLabel(x + 322, y, HueMuted, $"Counterweight: {VirtueAlignmentService.GetDisplayName(_secondary)}");
    }

    private void DrawConvictionStatus(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        var conviction = VirtueAlignmentService.GetConviction(_from);
        var rank = VirtueAlignmentService.GetConvictionRank(_from);
        var next = VirtueAlignmentService.GetNextConvictionThreshold(_from);
        var nextText = next > 0 ? $"Next: {next}" : "Highest rank";

        builder.AddLabel(x, y, HueMuted, "Conviction:");
        builder.AddHtml(
            x + 92,
            y,
            width - 92,
            24,
            $"{conviction} - {VirtueAlignmentService.GetConvictionRankName(rank)} ({nextText})",
            "#D8D8D8",
            scrollbar: false
        );
    }

    private void DrawSavedStatus(ref DynamicGumpBuilder builder)
    {
        AddCenteredHtml(ref builder, 38, 28, 604, 24, "Virtue Alignment", HtmlTitle);
        AddCenteredRawHtml(ref builder, 38, 56, 604, 22, GetAspirationSummaryHtml());
        AddCenteredRawHtml(ref builder, 38, 78, 604, 22, GetExpressionSummaryHtml());
        AddCenteredHtml(ref builder, 38, 108, 604, 22, GetConvictionStatusText(), HtmlText);
        DrawAlignmentBalances(ref builder, 68, 146, 554);

        builder.AddButton(244, 466, 4014, 4016, ButtonBackToPrimary);
        builder.AddLabel(278, 468, HueText, "Rechoose");

        if (_from.AccessLevel >= AccessLevel.GameMaster)
        {
            builder.AddButton(514, 466, 4017, 4019, ButtonClear);
            builder.AddLabel(548, 468, HueWarn, "Clear");
        }
    }

    /*
     * Shows deed-earned pressure between each classic virtue and its vice. The
     * marker drifts left for virtue pressure, right for vice pressure, and stays
     * centered when both sides are even or unearned.
     */
    private void DrawAlignmentBalances(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        AddCenteredHtml(ref builder, x, y, width, 22, "Deed Balance", HtmlTitle);

        var rowY = y + 28;

        for (var i = 0; i < VirtueAlignmentService.Virtues.Count; i++)
        {
            var virtue = VirtueAlignmentService.Virtues[i];
            DrawAlignmentBalanceRow(ref builder, virtue, VirtueAlignmentService.GetCounterpart(virtue), x, rowY, width);
            rowY += 35;
        }
    }

    private void DrawAlignmentBalanceRow(
        ref DynamicGumpBuilder builder,
        VirtueAlignmentPath virtue,
        VirtueAlignmentPath vice,
        int x,
        int y,
        int width
    )
    {
        var virtueScore = VirtueAlignmentService.GetTendency(_from, virtue);
        var viceScore = VirtueAlignmentService.GetTendency(_from, vice);
        const int labelWidth = 118;
        const int sideGap = 14;
        var barX = x + labelWidth + sideGap;
        var barWidth = width - labelWidth * 2 - sideGap * 2;
        var centerX = barX + barWidth / 2;
        var markerX = GetBalanceMarkerX(centerX, barWidth / 2, virtueScore, viceScore);
        var netScore = virtueScore - viceScore;
        var netColor = netScore > 0 ? HtmlVirtue : netScore < 0 ? HtmlVice : HtmlMuted;

        builder.AddHtml(x, y, labelWidth, 22, $"<BASEFONT COLOR={HtmlVirtue}>{VirtueAlignmentService.GetDisplayName(virtue)}</BASEFONT>", scrollbar: false);
        builder.AddHtml(x + width - labelWidth, y, labelWidth, 22, $"<BASEFONT COLOR={HtmlVice}>{VirtueAlignmentService.GetDisplayName(vice)}</BASEFONT>", scrollbar: false);
        builder.AddImageTiled(barX, y + 12, barWidth, 2, 9107);
        builder.AddImageTiled(centerX, y + 6, 2, 14, 9105);
        builder.AddImageTiled(markerX - 2, y + 4, 5, 18, 9157);
        builder.AddHtml(
            barX,
            y + 18,
            barWidth,
            18,
            $"<CENTER><BASEFONT COLOR={netColor}>{Math.Abs(netScore)}</BASEFONT></CENTER>",
            scrollbar: false
        );
    }

    private string GetConvictionStatusText()
    {
        var conviction = VirtueAlignmentService.GetConviction(_from);
        var rank = VirtueAlignmentService.GetConvictionRank(_from);
        var next = VirtueAlignmentService.GetNextConvictionThreshold(_from);
        var nextText = next > 0 ? $"Next: {next}" : "Highest rank";

        return $"Conviction: {conviction} - {VirtueAlignmentService.GetConvictionRankName(rank)} ({nextText})";
    }

    private string GetAspirationSummaryHtml()
    {
        var primary = VirtueAlignmentService.GetPrimaryAspiration(_from);
        var secondary = VirtueAlignmentService.GetSecondaryAspiration(_from);

        if (!VirtueAlignmentService.IsValidPair(primary, secondary))
        {
            return $"<CENTER><BASEFONT COLOR={HtmlText}>No aspirations declared.</BASEFONT></CENTER>";
        }

        return $"<CENTER><BASEFONT COLOR={HtmlText}>Aspires toward {GetColoredPathName(primary)}, tempered by {GetColoredPathName(secondary)}.</BASEFONT></CENTER>";
    }

    private string GetExpressionSummaryHtml()
    {
        var virtue = VirtueAlignmentService.GetExpressedVirtue(_from);
        var vice = VirtueAlignmentService.GetExpressedVice(_from);

        if (virtue != VirtueAlignmentPath.None && vice != VirtueAlignmentPath.None)
        {
            return $"<CENTER><BASEFONT COLOR={HtmlText}>Expresses {GetColoredPathName(virtue)} and {GetColoredPathName(vice)}.</BASEFONT></CENTER>";
        }

        if (virtue != VirtueAlignmentPath.None)
        {
            return $"<CENTER><BASEFONT COLOR={HtmlText}>Expresses {GetColoredPathName(virtue)}; no Vice has emerged.</BASEFONT></CENTER>";
        }

        if (vice != VirtueAlignmentPath.None)
        {
            return $"<CENTER><BASEFONT COLOR={HtmlText}>Expresses {GetColoredPathName(vice)}; no Virtue has emerged.</BASEFONT></CENTER>";
        }

        return $"<CENTER><BASEFONT COLOR={HtmlText}>No clear Virtue or Vice has emerged.</BASEFONT></CENTER>";
    }

    private static string GetColoredPathName(VirtueAlignmentPath path)
    {
        var color = VirtueAlignmentService.GetSide(path) == VirtueAlignmentSide.Vice ? HtmlVice : HtmlVirtue;
        return $"<BASEFONT COLOR={color}>{VirtueAlignmentService.GetDisplayName(path)}</BASEFONT><BASEFONT COLOR={HtmlText}>";
    }

    private static int GetBalanceMarkerX(int centerX, int halfWidth, int virtueScore, int viceScore)
    {
        var difference = viceScore - virtueScore;
        var total = Math.Max(100, virtueScore + viceScore);
        var offset = Math.Clamp(difference * halfWidth / total, -halfWidth, halfWidth);

        return centerX + offset;
    }

    private static void DrawCarouselControls(ref DynamicGumpBuilder builder, string chooseLabel)
    {
        builder.AddButton(128, 426, 4014, 4016, ButtonPreviousDetail);
        builder.AddLabel(162, 428, HueText, "Previous");
        builder.AddButton(506, 426, 4005, 4007, ButtonNextDetail);
        builder.AddLabel(540, 428, HueText, "Next");

        builder.AddButton(288, 466, 4005, 4007, ButtonChoosePrimary);
        builder.AddLabel(322, 468, HueText, chooseLabel);
    }

    private static void DrawConfirmColumn(
        ref DynamicGumpBuilder builder,
        VirtueAlignmentPath path,
        string label,
        int x,
        int y,
        int width
    )
    {
        var side = VirtueAlignmentService.GetSide(path);
        var hue = side == VirtueAlignmentSide.Virtue ? HueVirtue : HueVice;
        var iconX = x + width / 2 - 24;

        DrawPathIcon(ref builder, iconX, y + 18, path);
        AddCenteredHtml(
            ref builder,
            x,
            y + 78,
            width,
            24,
            $"{label} {side}: {VirtueAlignmentService.GetDisplayName(path)}",
            side == VirtueAlignmentSide.Virtue ? "#66FF66" : "#FF5555"
        );
        builder.AddHtml(
            x,
            y + 116,
            width,
            108,
            VirtueAlignmentService.GetLongDescription(path),
            "#D8D8D8",
            scrollbar: false
        );
        builder.AddHtml(
            x,
            y + 248,
            width,
            44,
            "[Future missions and perk flavor will be shaped by this path.]",
            "#BEBEBE",
            scrollbar: false
        );
    }

    private static void AddCenteredHtml(
        ref DynamicGumpBuilder builder,
        int x,
        int y,
        int width,
        int height,
        string text,
        string color
    )
    {
        builder.AddHtml(x, y, width, height, $"<CENTER><BASEFONT COLOR={color}>{text}</BASEFONT></CENTER>", scrollbar: false);
    }

    private static void AddCenteredRawHtml(
        ref DynamicGumpBuilder builder,
        int x,
        int y,
        int width,
        int height,
        string html
    )
    {
        builder.AddHtml(x, y, width, height, html, scrollbar: false);
    }

    private void DrawPathSpotlight(
        ref DynamicGumpBuilder builder,
        VirtueAlignmentPath path,
        int x,
        int y,
        int width,
        int height
    )
    {
        var side = VirtueAlignmentService.GetSide(path);
        var hue = side == VirtueAlignmentSide.Virtue ? HueVirtue : HueVice;
        var textX = x + 90;

        DrawPathIcon(ref builder, x + 20, y + 12, path);
        builder.AddLabel(textX, y + 4, hue, VirtueAlignmentService.GetDisplayName(path));
        builder.AddLabel(textX, y + 34, HueMuted, side == VirtueAlignmentSide.Virtue ? "Virtue" : "Vice");
        builder.AddHtml(
            textX,
            y + 74,
            width - 116,
            height - 140,
            VirtueAlignmentService.GetLongDescription(path),
            "#D8D8D8",
            scrollbar: false
        );
        builder.AddHtml(
            textX,
            y + height - 34,
            width - 116,
            28,
            $"Tendency: {VirtueAlignmentService.GetTendency(_from, path)}",
            "#BEBEBE",
            scrollbar: false
        );
    }

    /*
     * Draws the stock virtue symbol used to visually identify both virtues and
     * their paired vices. The server gump API supports hue changes but not rotation.
     */
    private static void DrawPathIcon(ref DynamicGumpBuilder builder, int x, int y, VirtueAlignmentPath path)
    {
        var icon = VirtueAlignmentService.GetIconGumpId(path);

        if (icon == 0)
        {
            return;
        }

        builder.AddImage(x, y, icon, VirtueAlignmentService.GetIconHue(path));
    }

    private bool IsSavedSelection()
    {
        var profile = VirtueAlignmentService.GetProfile(_from);

        return profile?.HasAspirations == true &&
               profile.PrimaryAspiration == _primary &&
               profile.SecondaryAspiration == _secondary;
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile from || info.ButtonID == 0)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case ButtonOverview:
                DisplayTo(from, VirtueAlignmentGumpMode.Overview, _primary, _secondary, VirtueAlignmentPath.None);
                return;
            case ButtonChoosePrimary:
                ChooseDisplayedPathAsPrimary(from);
                return;
            case ButtonBackToPrimary:
                DisplayTo(from, VirtueAlignmentGumpMode.Overview, _primary, VirtueAlignmentPath.None, GetInitialOverviewPath(_primary));
                return;
            case ButtonReturnToSelection:
                ReturnToSelection(from);
                return;
            case ButtonPreviousDetail:
                PageDisplayedPath(from, -1);
                return;
            case ButtonNextDetail:
                PageDisplayedPath(from, 1);
                return;
            case ButtonConfirm:
                ConfirmSelection(from);
                return;
            case ButtonClear when from.AccessLevel >= AccessLevel.GameMaster:
                ClearSelection(from);
                return;
        }

        if (info.ButtonID >= ButtonDetailBase)
        {
            var detailPath = (VirtueAlignmentPath)(info.ButtonID - ButtonDetailBase);
            DisplayTo(from, VirtueAlignmentGumpMode.Detail, _mode, _primary, _secondary, detailPath);
            return;
        }

        if (info.ButtonID >= ButtonPathBase)
        {
            var path = (VirtueAlignmentPath)(info.ButtonID - ButtonPathBase);
            SelectPath(from, path);
        }
    }

    private string GetReturnLabel() =>
        _returnMode switch
        {
            VirtueAlignmentGumpMode.ChooseSecondaryAspiration => "Back to Counterweight",
            VirtueAlignmentGumpMode.ChoosePrimaryAspiration => "Back to Aspiration",
            _ => "Back"
        };

    private string GetChooseDisplayedPathLabel() =>
        _returnMode == VirtueAlignmentGumpMode.ChooseSecondaryAspiration ? "Choose Counterweight" : "Aspire Toward";

    private static VirtueAlignmentPath GetInitialOverviewPath(VirtueAlignmentPath primary) =>
        primary == VirtueAlignmentPath.None ? VirtueAlignmentPath.Compassion : primary;

    private VirtueAlignmentPath GetOverviewPath() =>
        GetPathIndex(_detailPath) >= 0 ? _detailPath : GetInitialOverviewPath(_primary);

    private VirtueAlignmentPath GetPagedPath(int direction)
    {
        var index = GetPathIndex(GetOverviewPath());

        if (index < 0)
        {
            index = 0;
        }

        index = (index + direction + _allPaths.Length) % _allPaths.Length;
        return _allPaths[index];
    }

    private static int GetPathIndex(VirtueAlignmentPath path)
    {
        for (var i = 0; i < _allPaths.Length; i++)
        {
            if (_allPaths[i] == path)
            {
                return i;
            }
        }

        return -1;
    }

    private VirtueAlignmentPath GetSecondaryPath()
    {
        if (IsEligibleSecondaryPath(_detailPath))
        {
            return _detailPath;
        }

        return GetInitialSecondaryPath(_primary);
    }

    private VirtueAlignmentPath GetPagedSecondaryPath(int direction)
    {
        var paths = GetSecondaryPaths(_primary);

        if (paths == null)
        {
            return VirtueAlignmentPath.None;
        }

        var current = GetSecondaryPath();
        var index = GetPathIndex(paths, current);

        if (index < 0)
        {
            index = 0;
        }

        for (var i = 0; i < paths.Count; i++)
        {
            index = (index + direction + paths.Count) % paths.Count;

            if (IsEligibleSecondaryPath(_primary, paths[index]))
            {
                return paths[index];
            }
        }

        return VirtueAlignmentPath.None;
    }

    private VirtueAlignmentPath GetInitialSecondaryPath(VirtueAlignmentPath primary)
    {
        var paths = GetSecondaryPaths(primary);

        if (paths == null)
        {
            return VirtueAlignmentPath.None;
        }

        for (var i = 0; i < paths.Count; i++)
        {
            if (IsEligibleSecondaryPath(primary, paths[i]))
            {
                return paths[i];
            }
        }

        return VirtueAlignmentPath.None;
    }

    private bool IsEligibleSecondaryPath(VirtueAlignmentPath path) =>
        IsEligibleSecondaryPath(_primary, path);

    private static bool IsEligibleSecondaryPath(VirtueAlignmentPath primary, VirtueAlignmentPath secondary) =>
        VirtueAlignmentService.IsValidPair(primary, secondary);

    private static IReadOnlyList<VirtueAlignmentPath> GetSecondaryPaths(VirtueAlignmentPath primary)
    {
        var primarySide = VirtueAlignmentService.GetSide(primary);

        return primarySide switch
        {
            VirtueAlignmentSide.Virtue => VirtueAlignmentService.Vices,
            VirtueAlignmentSide.Vice => VirtueAlignmentService.Virtues,
            _ => null
        };
    }

    private static int GetPathIndex(IReadOnlyList<VirtueAlignmentPath> paths, VirtueAlignmentPath path)
    {
        for (var i = 0; i < paths.Count; i++)
        {
            if (paths[i] == path)
            {
                return i;
            }
        }

        return -1;
    }

    private void PageDisplayedPath(PlayerMobile from, int direction)
    {
        if (_mode == VirtueAlignmentGumpMode.ChooseSecondaryAspiration)
        {
            DisplayTo(
                from,
                VirtueAlignmentGumpMode.ChooseSecondaryAspiration,
                _primary,
                VirtueAlignmentPath.None,
                GetPagedSecondaryPath(direction)
            );
            return;
        }

        DisplayTo(from, VirtueAlignmentGumpMode.Overview, _primary, _secondary, GetPagedPath(direction));
    }

    private void ReturnToSelection(PlayerMobile from)
    {
        var returnMode = _returnMode is VirtueAlignmentGumpMode.ChoosePrimaryAspiration or VirtueAlignmentGumpMode.ChooseSecondaryAspiration
            ? _returnMode
            : VirtueAlignmentGumpMode.Overview;

        DisplayTo(from, returnMode, _primary, _secondary, VirtueAlignmentPath.None);
    }

    private void ChooseDisplayedPathAsPrimary(PlayerMobile from)
    {
        var path = _mode switch
        {
            VirtueAlignmentGumpMode.ChooseSecondaryAspiration => GetSecondaryPath(),
            VirtueAlignmentGumpMode.Detail => _detailPath,
            _ => GetOverviewPath()
        };

        if (VirtueAlignmentService.GetSide(path) == VirtueAlignmentSide.None)
        {
            DisplayTo(from);
            return;
        }

        if (_mode == VirtueAlignmentGumpMode.ChooseSecondaryAspiration ||
            _mode == VirtueAlignmentGumpMode.Detail && _returnMode == VirtueAlignmentGumpMode.ChooseSecondaryAspiration)
        {
            if (!VirtueAlignmentService.IsValidPair(_primary, path))
            {
                DisplayTo(
                    from,
                    VirtueAlignmentGumpMode.ChooseSecondaryAspiration,
                    _primary,
                    VirtueAlignmentPath.None,
                    GetInitialSecondaryPath(_primary)
                );
                return;
            }

            DisplayTo(from, VirtueAlignmentGumpMode.Confirm, _primary, path, VirtueAlignmentPath.None);
            return;
        }

        SelectPath(from, path);
    }

    private void SelectPath(PlayerMobile from, VirtueAlignmentPath path)
    {
        if (_mode is VirtueAlignmentGumpMode.Overview or VirtueAlignmentGumpMode.Detail or VirtueAlignmentGumpMode.ChoosePrimaryAspiration)
        {
            DisplayTo(
                from,
                VirtueAlignmentGumpMode.ChooseSecondaryAspiration,
                path,
                VirtueAlignmentPath.None,
                GetInitialSecondaryPath(path)
            );
            return;
        }

        if (_mode == VirtueAlignmentGumpMode.ChooseSecondaryAspiration)
        {
            DisplayTo(from, VirtueAlignmentGumpMode.Confirm, _primary, path, VirtueAlignmentPath.None);
        }
    }

    private void ConfirmSelection(PlayerMobile from)
    {
        if (!VirtueAlignmentService.TrySetAspirations(from, _primary, _secondary, from, false, out var reason))
        {
            from.SendMessage(0x22, reason);
            DisplayTo(from, VirtueAlignmentGumpMode.Confirm, _primary, _secondary, VirtueAlignmentPath.None);
            return;
        }

        from.SendMessage(0x35, $"Virtue Alignment aspirations chosen: {VirtueAlignmentService.GetAspirationSummary(from)}");
        DisplayTo(from);
    }

    private void ClearSelection(PlayerMobile from)
    {
        if (!VirtueAlignmentService.ClearAspirations(from, from, out var reason))
        {
            from.SendMessage(0x22, reason);
        }
        else
        {
            from.SendMessage(0x35, "Virtue Alignment aspirations cleared.");
        }

        DisplayTo(from);
    }
}
