using System;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom.Engines.ActivityTracking;

public sealed class ActivityTrackingStatusGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueGood = 68;
    private const int HueWarn = 33;

    private const int GumpWidth = 560;
    private const int GumpHeight = 440;

    private TimerExecutionToken _refreshToken;

    public override bool Singleton => true;

    private ActivityTrackingStatusGump() : base(50, 50)
    {
    }

    public static void DisplayTo(Mobile from)
    {
        if (from == null || from.NetState == null)
        {
            return;
        }

        from.CloseGump<ActivityTrackingStatusGump>();

        var gump = new ActivityTrackingStatusGump();
        from.SendGump(gump);
        gump.StartAutoRefresh(from);
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);

        builder.AddLabel(180, 18, HueTitle, "Activity Tracking Status");

        BuildRuntimeSection(ref builder, 30, 55);
        BuildTrackingSection(ref builder, 295, 55);
        BuildGoldSection(ref builder, 30, 185);
        BuildMemorySection(ref builder, 295, 185);
        BuildEconomySection(ref builder, 30, 330);

        builder.AddLabel(30, 405, HueMuted, "Auto refreshes every 5 seconds");

        builder.AddButton(470, 403, 4017, 4019, 0);
        builder.AddLabel(505, 405, HueText, "Close");
    }

    private static void BuildRuntimeSection(ref DynamicGumpBuilder builder, int x, int y)
    {
        builder.AddBackground(x, y, 235, 125, 9250);
        builder.AddLabel(x + 15, y + 12, HueHeader, "Runtime");

        AddMetric(ref builder, x, y, 38, "Debug", ActivityTrackingService.DebugEnabled ? "ON" : "OFF", ActivityTrackingService.DebugEnabled ? HueGood : HueMuted);
        AddMetric(ref builder, x, y, 60, "Staff Tracking", ActivityTrackingService.IncludeStaffMembers ? "ENABLED" : "DISABLED", ActivityTrackingService.IncludeStaffMembers ? HueWarn : HueMuted);
        AddMetric(ref builder, x, y, 82, "Tracked Players", ActivityTrackingService.PlayerCount.ToString("N0"), HueText);
        AddMetric(ref builder, x, y, 104, "Accounts", ActivityTrackingService.AccountBalanceCount.ToString("N0"), HueText);
    }

    private static void BuildTrackingSection(ref DynamicGumpBuilder builder, int x, int y)
    {
        builder.AddBackground(x, y, 235, 125, 9250);
        builder.AddLabel(x + 15, y + 12, HueHeader, "Activity");

        AddMetric(ref builder, x, y, 38, "Recent Kills", ActivityTrackingService.RecentKillCount.ToString("N0"), HueText);
        AddMetric(ref builder, x, y, 60, "Deaths", ActivityTrackingService.TotalDeathCount.ToString("N0"), HueText);
        AddMetric(ref builder, x, y, 82, "Gold Corpses", ActivityTrackingService.MonsterCorpseGoldRecordCount.ToString("N0"), HueText);
        AddMetric(ref builder, x, y, 104, "Regions", ActivityTrackingService.RegionCount.ToString("N0"), HueText);
    }

    private static void BuildGoldSection(ref DynamicGumpBuilder builder, int x, int y)
    {
        builder.AddBackground(x, y, 235, 130, 9250);
        builder.AddLabel(x + 15, y + 12, HueHeader, "Gold Earned");

        AddMetric(ref builder, x, y, 38, "Total", ActivityTrackingService.TotalGoldEarned.ToString("N0"), HueGood);
        AddMetric(ref builder, x, y, 60, "Monster Loot", ActivityTrackingService.TotalMonsterGoldLooted.ToString("N0"), HueText);
        AddMetric(ref builder, x, y, 82, "NPC Vendors", ActivityTrackingService.TotalNpcVendorGoldEarned.ToString("N0"), HueText);
    }

    private static void BuildEconomySection(ref DynamicGumpBuilder builder, int x, int y)
    {
        builder.AddBackground(x, y, 500, 65, 9250);
        builder.AddLabel(x + 15, y + 12, HueHeader, "Economy");

        AddMetric(ref builder, x, y, 38, "Bank Known", ActivityTrackingService.TotalKnownBankBalance.ToString("N0"), HueGood);
        AddMetric(ref builder, x + 250, y, 38, "Gold Leaving", ActivityTrackingService.TotalGoldLeavingEconomy.ToString("N0"), HueWarn);
        AddMetric(ref builder, x, y, 60, "NPC Bought", ActivityTrackingService.TotalNpcVendorGoldSpent.ToString("N0"), HueText);
        AddMetric(ref builder, x + 250, y, 60, "PV Sales", ActivityTrackingService.TotalPlayerVendorSales.ToString("N0"), HueText);
    }

    private static void BuildMemorySection(ref DynamicGumpBuilder builder, int x, int y)
    {
        var memoryBytes = ActivityTrackingService.GetEstimatedMemoryUsage();
        var memoryKb = memoryBytes / 1024;

        builder.AddBackground(x, y, 235, 130, 9250);
        builder.AddLabel(x + 15, y + 12, HueHeader, "Memory");

        AddMetric(ref builder, x, y, 38, "Estimated", $"{memoryKb:N0} KB", HueText);
        AddMetric(ref builder, x, y, 60, "Bytes", memoryBytes.ToString("N0"), HueMuted);
        AddMetric(ref builder, x, y, 82, "Commissions", ActivityTrackingService.TotalPlayerVendorCommissions.ToString("N0"), HueWarn);
        AddMetric(ref builder, x, y, 104, "Decayed", ActivityTrackingService.TotalGoldDecayed.ToString("N0"), HueWarn);
    }

    private static void AddMetric(
        ref DynamicGumpBuilder builder,
        int x,
        int y,
        int offsetY,
        string label,
        string value,
        int valueHue
    )
    {
        builder.AddLabel(x + 15, y + offsetY, HueText, label);
        builder.AddLabelCropped(x + 130, y + offsetY, 90, 20, valueHue, value);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        StopAutoRefresh();
    }

    public override void OnServerClose(NetState owner)
    {
        base.OnServerClose(owner);

        StopAutoRefresh();
    }

    private void StartAutoRefresh(Mobile from)
    {
        Timer.StartTimer(TimeSpan.FromSeconds(5.0), () => Refresh(from), out _refreshToken);
    }

    private void StopAutoRefresh()
    {
        _refreshToken.Cancel();
    }

    private void Refresh(Mobile from)
    {
        if (from?.NetState == null)
        {
            return;
        }

        if (from.FindGump<ActivityTrackingStatusGump>() == this)
        {
            DisplayTo(from);
        }
    }
}
