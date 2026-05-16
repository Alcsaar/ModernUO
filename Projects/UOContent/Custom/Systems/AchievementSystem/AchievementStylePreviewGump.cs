using System;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom.Systems.AchievementSystem;

public sealed class AchievementStylePreviewGump : DynamicGump
{
    private const int HueCodexTitle = 1153;
    private const int HueCodexHeader = 2213;
    private const int HueCodexText = 2101;
    private const int HueCodexMuted = 2401;
    private const int HueCodexReady = 68;

    private const int ButtonPrevious = 1;
    private const int ButtonNext = 2;
    private const int GumpWidth = 760;
    private const int GumpHeight = 540;

    private static readonly PreviewStyle[] Styles =
    {
        new("Classic Paper", 5054, 3000, 0xBBC, "#3F2B12", "#6A4A1C", "#1F1B12"),
        new("Ornate Ledger", 0x13BE, 0xA40, 0xBBC, "#F2D89A", "#C8B27A", "#FFFFFF"),
        new("Dark Stone", 9270, 9250, 9200, "#F2D89A", "#D0D0D0", "#FFFFFF"),
        new("Travel Codex", 9270, 9250, 9200, "#F2D89A", "#D0D0D0", "#FFFFFF", true),
        new("Guild Slate", 0x242C, 0x2486, 0x2430, "#F2D89A", "#D0D0D0", "#FFFFFF"),
        new("Parchment Help", 2600, 3000, 2624, "#3F2B12", "#6A4A1C", "#1F1B12")
    };

    private readonly int _styleIndex;

    public override bool Singleton => true;

    private AchievementStylePreviewGump(int styleIndex) : base(60, 45)
    {
        _styleIndex = Math.Max(0, Math.Min(styleIndex, Styles.Length - 1));
    }

    public static void DisplayTo(PlayerMobile from, int styleIndex = 0)
    {
        if (from?.NetState == null)
        {
            return;
        }

        from.CloseGump<AchievementStylePreviewGump>();
        from.SendGump(new AchievementStylePreviewGump(styleIndex));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var style = Styles[_styleIndex];

        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, style.OuterGumpId);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);

        if (style.UseCodexLayout)
        {
            DrawTravelCodexPreview(ref builder);
            return;
        }

        builder.AddHtml(28, 18, 300, 24, HtmlColor($"Achievement Journal - {style.Name}", style.TitleColor));
        builder.AddHtml(560, 18, 120, 20, HtmlColor($"{_styleIndex + 1}/{Styles.Length}", style.TextColor));
        DrawRule(ref builder, 24, 48, 710);

        DrawNavigation(ref builder, style);
        DrawSidebar(ref builder, style);
        DrawContent(ref builder, style);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var player = sender.Mobile as PlayerMobile;

        if (player == null)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case ButtonPrevious:
                DisplayTo(player, _styleIndex <= 0 ? Styles.Length - 1 : _styleIndex - 1);
                return;
            case ButtonNext:
                DisplayTo(player, _styleIndex + 1 >= Styles.Length ? 0 : _styleIndex + 1);
                return;
        }
    }

    private static void DrawSidebar(ref DynamicGumpBuilder builder, PreviewStyle style)
    {
        builder.AddBackground(24, 62, 170, 448, style.PanelGumpId);
        builder.AddHtml(42, 82, 110, 20, HtmlColor("Categories", style.TextColor));

        DrawCategory(ref builder, style, 44, 120, "Overview", false);
        DrawCategory(ref builder, style, 44, 156, "Skills", false);
        DrawCategory(ref builder, style, 44, 192, "Harvesting", false);
        DrawCategory(ref builder, style, 44, 228, "Feats", true);

        DrawRule(ref builder, 40, 462, 138);
    }

    private static void DrawCategory(ref DynamicGumpBuilder builder, PreviewStyle style, int x, int y, string label, bool selected)
    {
        if (selected)
        {
            builder.AddBackground(x - 8, y - 4, 136, 28, style.RowGumpId);
            builder.AddImageTiled(x - 3, y, 4, 20, 9304);
        }

        builder.AddButton(x + 6, y, 4005, 4007, 0);
        builder.AddHtml(x + 42, y + 1, 80, 20, HtmlColor(label, selected ? style.TitleColor : style.TextColor));
    }

    private static void DrawContent(ref DynamicGumpBuilder builder, PreviewStyle style)
    {
        builder.AddBackground(210, 62, 525, 448, style.PanelGumpId);
        builder.AddHtml(232, 82, 160, 20, HtmlColor("Feats", style.TitleColor));
        builder.AddHtml(590, 82, 120, 20, HtmlColor("1/49 unlocked", style.TextColor));
        DrawRule(ref builder, 230, 106, 482);

        DrawAchievementRow(
            ref builder,
            style,
            232,
            120,
            "Grandmaster Alchemy",
            "Be the first player to reach 100.0 in Alchemy.",
            "Your Server First",
            "Claimed 2026-05-16",
            unlocked: true,
            unavailable: false
        );

        DrawAchievementRow(
            ref builder,
            style,
            232,
            210,
            "Grandmaster Anatomy",
            "Be the first player to reach 100.0 in Anatomy.",
            "Claimed",
            "Claimed by Alessar",
            unlocked: false,
            unavailable: true
        );

        DrawAchievementRow(
            ref builder,
            style,
            232,
            300,
            "Grandmaster Animal Lore",
            "Be the first player to reach 100.0 in Animal Lore.",
            "Server First",
            "0/100",
            unlocked: false,
            unavailable: false
        );

        DrawRule(ref builder, 230, 466, 482);
        builder.AddHtml(246, 482, 90, 20, HtmlColor("Page 1/10", style.TextColor));
    }

    private static void DrawAchievementRow(
        ref DynamicGumpBuilder builder,
        PreviewStyle style,
        int x,
        int y,
        string title,
        string description,
        string meta,
        string status,
        bool unlocked,
        bool unavailable
    )
    {
        var titleColor = unlocked ? "#B8F2B8" : unavailable ? "#A0A0A0" : style.StrongTextColor;
        var bodyColor = unavailable ? "#909090" : style.TextColor;
        var accentGump = unlocked ? 9304 : unavailable ? 2624 : 5058;

        builder.AddBackground(x, y, 480, 78, style.RowGumpId);
        builder.AddImageTiled(x + 8, y + 8, 4, 62, accentGump);
        builder.AddHtml(x + 20, y + 8, 250, 20, HtmlColor(title, titleColor));
        builder.AddHtml(x + 322, y + 8, 130, 20, HtmlColor(meta, unavailable ? "#808080" : style.TextColor));
        builder.AddHtml(x + 20, y + 30, 420, 24, HtmlColor(description, bodyColor));
        builder.AddHtml(x + 20, y + 56, 155, 20, HtmlColor(status, unlocked ? "#B8F2B8" : unavailable ? "#A0A0A0" : "#78BFFF"));
        builder.AddImageTiled(x + 190, y + 60, 230, 12, 2624);

        if (unlocked)
        {
            builder.AddImageTiled(x + 190, y + 60, 230, 12, 9304);
        }
        else if (!unavailable)
        {
            builder.AddImageTiled(x + 190, y + 60, 18, 12, 9304);
        }
    }

    private static void DrawNavigation(ref DynamicGumpBuilder builder, PreviewStyle style)
    {
        builder.AddBackground(596, 472, 134, 40, 9200);
        builder.AddButton(610, 480, 4014, 4016, ButtonPrevious);
        builder.AddHtml(644, 482, 35, 20, HtmlColor("Prev", style.TextColor));
        builder.AddButton(680, 480, 4005, 4007, ButtonNext);
        builder.AddHtml(714, 482, 35, 20, HtmlColor("Next", style.TextColor));
    }

    private static void DrawTravelCodexPreview(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(286, 18, HueCodexTitle, "Achievement Journal");
        builder.AddLabel(292, 44, HueCodexText, "Feats, milestones, and shard records");
        builder.AddLabel(570, 28, HueCodexText, "4/6");

        DrawRule(ref builder, 24, 62, 710);

        builder.AddBackground(24, 84, 170, 373, 9250);
        builder.AddAlphaRegion(30, 90, 158, 361);
        builder.AddLabel(42, 104, HueCodexHeader, "Categories");
        DrawCodexCategory(ref builder, 42, 142, "Overview", false);
        DrawCodexCategory(ref builder, 42, 178, "Skills", false);
        DrawCodexCategory(ref builder, 42, 214, "Harvesting", false);
        DrawCodexCategory(ref builder, 42, 250, "Feats", true);
        DrawRule(ref builder, 42, 420, 132);

        builder.AddBackground(210, 84, 525, 373, 9250);
        builder.AddAlphaRegion(216, 90, 513, 361);
        builder.AddLabel(230, 104, HueCodexHeader, "Feats");
        builder.AddLabel(610, 104, HueCodexText, "1/49 unlocked");
        DrawRule(ref builder, 228, 128, 482);

        DrawCodexRow(
            ref builder,
            230,
            152,
            "Grandmaster Alchemy",
            "Your Server First",
            "Claimed 2026-05-16",
            HueCodexReady,
            fill: 1.0
        );

        DrawCodexRow(
            ref builder,
            230,
            224,
            "Grandmaster Anatomy",
            "Claimed",
            "Claimed by Alessar",
            HueCodexMuted,
            fill: 0.0
        );

        DrawCodexRow(
            ref builder,
            230,
            296,
            "Grandmaster Animal Lore",
            "Server First",
            "0/100",
            HueCodexText,
            fill: 0.08
        );

        DrawRule(ref builder, 228, 420, 482);
        builder.AddLabel(234, 432, HueCodexText, "Page 1/10");
        DrawNavigation(ref builder, new PreviewStyle("Travel Codex", 9270, 9250, 9200, "#F2D89A", "#D0D0D0", "#FFFFFF", true));
    }

    private static void DrawCodexCategory(ref DynamicGumpBuilder builder, int x, int y, string label, bool selected)
    {
        if (selected)
        {
            builder.AddBackground(x - 8, y - 4, 130, 28, 9200);
            builder.AddImageTiled(x - 2, y, 4, 20, 9304);
        }

        builder.AddButton(x + 6, y, 4005, 4007, 0);
        builder.AddLabel(x + 42, y + 2, selected ? HueCodexHeader : HueCodexText, label);
    }

    private static void DrawCodexRow(
        ref DynamicGumpBuilder builder,
        int x,
        int y,
        string title,
        string meta,
        string status,
        int titleHue,
        double fill
    )
    {
        builder.AddImageTiled(x, y, 4, 56, fill >= 1.0 ? 9304 : fill <= 0 ? 2624 : 5058);
        builder.AddLabel(x + 16, y, titleHue, title);
        builder.AddLabel(x + 330, y, titleHue == HueCodexMuted ? HueCodexMuted : HueCodexHeader, meta);
        builder.AddLabel(x + 16, y + 24, titleHue == HueCodexMuted ? HueCodexMuted : HueCodexText, "Be the first player to reach 100.0 in this skill.");
        builder.AddLabel(x + 16, y + 48, titleHue, status);
        builder.AddImageTiled(x + 300, y + 48, 190, 12, 2624);

        var fillWidth = (int)(190 * Math.Max(0.0, Math.Min(1.0, fill)));

        if (fillWidth > 0)
        {
            builder.AddImageTiled(x + 300, y + 48, fillWidth, 12, 9304);
        }
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }

    private static string HtmlColor(string text, string hex)
    {
        return $"<BASEFONT COLOR={hex}>{text}</BASEFONT>";
    }

    private readonly record struct PreviewStyle(
        string Name,
        int OuterGumpId,
        int PanelGumpId,
        int RowGumpId,
        string TitleColor,
        string TextColor,
        string StrongTextColor,
        bool UseCodexLayout = false
    );
}
