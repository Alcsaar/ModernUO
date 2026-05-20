using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Mobiles;

namespace Server.Custom.Systems.MissionSystem;

public sealed class MissionStatusGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueReady = 68;
    private const int HueProgress = 1153;
    private const int GumpWidth = 560;
    private const int GumpHeight = 450;
    private const int MaxVisibleEntries = 6;
    private const int EntryStartY = 92;
    private const int EntrySpacing = 42;
    private const int FooterRuleY = EntryStartY + MaxVisibleEntries * EntrySpacing + 10;

    private readonly List<PlayerMissionInstance> _active;

    public override bool Singleton => true;

    private MissionStatusGump(PlayerMobile from) : base(80, 80)
    {
        var profile = MissionSystemService.GetOrCreateProfile(from);
        _active = MissionSystemService.GetActiveMissions(profile);
    }

    public static void DisplayTo(PlayerMobile from)
    {
        if (from?.NetState == null)
        {
            return;
        }

        if (!MissionSystemService.IsSystemEnabled())
        {
            from.SendMessage(0x22, "The mission board is not available right now.");
            return;
        }

        MissionSystemService.EnsureProfileOffers(from);
        from.CloseGump<MissionStatusGump>();
        from.SendGump(new MissionStatusGump(from));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);
        builder.AddLabel(220, 18, HueTitle, "Mission Status");
        builder.AddLabel(168, 44, HueText, "Active Daily Missives and Weekly Contracts");
        DrawRule(ref builder, 26, 66, 508);

        if (_active.Count == 0)
        {
            builder.AddLabel(44, 100, HueMuted, "No active Daily Missives or Weekly Contracts.");
            return;
        }

        var y = EntryStartY;

        for (var i = 0; i < _active.Count && i < MaxVisibleEntries; i++)
        {
            DrawEntry(ref builder, _active[i], y);
            y += EntrySpacing;
        }

        // Keep the footer below the sixth active mission so full daily and weekly sets remain visible.
        DrawRule(ref builder, 26, FooterRuleY, 508);
    }

    private static void DrawEntry(ref DynamicGumpBuilder builder, PlayerMissionInstance instance, int y)
    {
        var definition = MissionSystemService.GetDefinition(instance.DefinitionId);
        var title = definition?.Title ?? "Unknown mission";
        var cadence = MissionSystemService.GetCadenceName(instance.Cadence);
        var hue = instance.Completed ? HueReady : HueText;

        builder.AddImageTiled(42, y + 4, 4, 28, instance.Completed ? 9304 : 5058);
        builder.AddLabel(58, y, hue, title);
        builder.AddLabel(350, y, HueHeader, cadence);
        builder.AddLabel(58, y + 20, instance.Completed ? HueReady : HueProgress, BuildProgressText(instance));
        DrawProgressBar(ref builder, 170, y + 25, 170, 10, instance.CurrentProgress, instance.RequiredProgress);
    }

    private static string BuildProgressText(PlayerMissionInstance instance)
    {
        if (instance.Completed)
        {
            return "Complete";
        }

        return $"{Math.Min(instance.CurrentProgress, instance.RequiredProgress)}/{instance.RequiredProgress}";
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
}
