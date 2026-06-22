using System;
using Server.Custom.Systems.CustomAdmin;
using Server.Gumps;
using Server.Network;

namespace Server.Custom.Systems.Townships;

public static class TownshipCustomAdminModule
{
    public static void Configure()
    {
        CustomAdminRegistry.Register(
            new CustomAdminLinkedModule(
                "townships",
                "Townships",
                "Gameplay",
                "Configure township costs, borders, upkeep, and review active township state.",
                AccessLevel.Developer,
                70,
                TownshipAdminGump.DisplayTo,
                _ => TownshipService.IsEnabled() ? "Enabled" : "Disabled",
                _ =>
                [
                    $"Active townships: {TownshipService.Townships.Count}",
                    $"Config: {TownshipSettings.ConfigPath}",
                    $"Initial claim: {TownshipSettings.InitialClaimSize}x{TownshipSettings.InitialClaimSize}",
                    $"House buffer: {TownshipSettings.HouseBuffer}",
                    $"Upkeep: {(TownshipSettings.UpkeepEnabled ? "Enabled" : "Disabled")}"
                ]
            )
        );
    }
}

public sealed class TownshipAdminGump : DynamicGump
{
    private const int ButtonSave = 1;
    private const int ButtonRefresh = 2;
    private const int ButtonTeleport = 3;
    private const int ButtonOpenTownship = 4;
    private const int ButtonViewLog = 6;
    private const int ButtonTownshipBase = 100;
    private const int EntryDeedCost = 10;
    private const int EntryInitialSize = 11;
    private const int EntryHouseBuffer = 12;
    private const int EntryEdgeContact = 13;
    private const int EntryTileCost = 14;
    private const int EntryLandUpkeep = 15;
    private const int EntryBorderRenderRange = 17;
    private const int EntryRefundCapPercent = 18;
    private const int EntryVoluntaryRefundPercent = 19;
    private const int EntryDelinquencyRefundPercent = 20;
    private const int EntryPartialVestingDays = 21;
    private const int EntryPartialVestingScalar = 22;
    private const int EntryFullVestingDays = 23;
    private const int EntryDelinquencyGraceDays = 24;
    private const int EntryDelinquencyRemovalIntervalDays = 25;
    private const int EntryBankerPurchaseCost = 26;
    private const int EntryBankerDailyUpkeep = 27;
    private const int EntryMagePurchaseCost = 28;
    private const int EntryMageDailyUpkeep = 29;
    private const int EntryAlchemistPurchaseCost = 30;
    private const int EntryAlchemistDailyUpkeep = 31;
    private const int EntryStablemasterPurchaseCost = 32;
    private const int EntryStablemasterDailyUpkeep = 33;
    private const int EntryInnkeeperPurchaseCost = 34;
    private const int EntryInnkeeperDailyUpkeep = 35;
    private const int EntryGuardedTownPurchaseCost = 36;
    private const int EntryGuardedTownDailyUpkeep = 37;
    private const int EntryHuntingTaxPurchaseCost = 38;
    private const int EntryHuntingTaxDailyUpkeep = 39;
    private const int EntryHuntingContributionPercent = 40;
    private const int EntryPatrolGuardCount = 41;
    private const int EntryVendorRevenueContributionPercent = 42;
    private const int SwitchUpkeepEnabled = 50;
    private const int ButtonConfigGeneral = 60;
    private const int ButtonConfigRefunds = 61;
    private const int ButtonConfigServices = 62;
    private const int ButtonConfigPerks = 63;
    private const int MaxTownshipRows = 10;

    private readonly Mobile _from;
    private readonly int _selectedIndex;
    private readonly int _configPage;

    public override bool Singleton => true;

    private TownshipAdminGump(Mobile from, int selectedIndex, int configPage) : base(70, 50)
    {
        _from = from;
        _selectedIndex = selectedIndex;
        _configPage = Math.Clamp(configPage, 0, 3);
    }

    public static void DisplayTo(Mobile from) => DisplayTo(from, 0);

    public static void DisplayTo(Mobile from, int selectedIndex, int configPage = 0)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel.Developer)
        {
            from?.SendMessage(0x22, "Township configuration requires Developer access.");
            return;
        }

        from.CloseGump<TownshipAdminGump>();
        from.SendGump(new TownshipAdminGump(from, selectedIndex, configPage));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 760, 650, 9270);
        builder.AddAlphaRegion(15, 15, 730, 620);
        builder.AddLabel(300, 24, 1153, "Township Admin");
        DrawRule(ref builder, 34, 54, 692);

        builder.AddLabel(46, 78, 2213, "Configuration");
        DrawConfigTabs(ref builder);
        DrawConfigPage(ref builder);

        builder.AddLabel(420, 78, 2213, "Active Townships");
        var y = 112;
        var townships = TownshipService.Townships;
        var selected = GetSelectedTownship();

        if (townships.Count == 0)
        {
            builder.AddLabel(420, y, 2401, "No active townships.");
        }
        else
        {
            var rowCount = Math.Min(MaxTownshipRows, townships.Count);

            for (var i = 0; i < rowCount; i++)
            {
                var t = townships[i];
                var hue = t == selected ? 68 : 2101;
                builder.AddButton(420, y - 2, 4005, 4007, ButtonTownshipBase + i);
                builder.AddLabelCropped(454, y, 250, 22, hue, $"{t.Name}: {t.ClaimedTileCount:N0} tiles, {t.TreasuryBalance:N0} gold");
                y += 26;
            }

            if (townships.Count > MaxTownshipRows)
            {
                builder.AddLabel(454, y, 2401, $"Showing first {MaxTownshipRows:N0} of {townships.Count:N0}.");
            }
        }

        DrawSelectedTownship(ref builder, selected);

        DrawRule(ref builder, 34, 592, 692);
        DrawButton(ref builder, 46, 606, ButtonSave, "Save Config");
        DrawButton(ref builder, 210, 606, ButtonRefresh, "Refresh");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || from.AccessLevel < AccessLevel.Developer)
        {
            return;
        }

        if (info.ButtonID == ButtonRefresh)
        {
            DisplayTo(from, _selectedIndex, _configPage);
            return;
        }

        if (info.ButtonID is ButtonConfigGeneral or ButtonConfigRefunds or ButtonConfigServices or ButtonConfigPerks)
        {
            DisplayTo(from, _selectedIndex, info.ButtonID - ButtonConfigGeneral);
            return;
        }

        if (info.ButtonID >= ButtonTownshipBase && info.ButtonID < ButtonTownshipBase + MaxTownshipRows)
        {
            DisplayTo(from, info.ButtonID - ButtonTownshipBase, _configPage);
            return;
        }

        var selected = GetSelectedTownship();

        if (info.ButtonID == ButtonTeleport)
        {
            TeleportToTownship(from, selected);
            DisplayTo(from, _selectedIndex, _configPage);
            return;
        }

        if (info.ButtonID == ButtonOpenTownship)
        {
            if (selected == null)
            {
                from.SendMessage(0x22, "No township is selected.");
                DisplayTo(from, _selectedIndex, _configPage);
                return;
            }

            TownshipGump.DisplayTo(from, selected);
            return;
        }

        if (info.ButtonID == ButtonViewLog)
        {
            TownshipAdminLogGump.DisplayTo(from, selected, _selectedIndex);
            return;
        }

        if (info.ButtonID != ButtonSave)
        {
            return;
        }

        var config = TownshipSettings.Snapshot();
        config.DeedCost = ReadInt(info, EntryDeedCost, config.DeedCost);
        config.InitialClaimSize = ReadInt(info, EntryInitialSize, config.InitialClaimSize);
        config.HouseBuffer = ReadInt(info, EntryHouseBuffer, config.HouseBuffer);
        config.EdgeContactRequired = ReadInt(info, EntryEdgeContact, config.EdgeContactRequired);
        config.TileCost = ReadInt(info, EntryTileCost, config.TileCost);
        config.DailyLandUpkeepPerTile = ReadInt(info, EntryLandUpkeep, config.DailyLandUpkeepPerTile);
        config.BorderRenderRange = ReadInt(info, EntryBorderRenderRange, config.BorderRenderRange);
        config.UpkeepEnabled = _configPage == 0 ? info.IsSwitched(SwitchUpkeepEnabled) : config.UpkeepEnabled;
        config.MaxServiceRefundPercent = ReadInt(info, EntryRefundCapPercent, config.MaxServiceRefundPercent);
        config.DefaultVoluntaryServiceRefundPercent = ReadInt(info, EntryVoluntaryRefundPercent, config.DefaultVoluntaryServiceRefundPercent);
        config.DefaultDelinquencyServiceRefundPercent = ReadInt(info, EntryDelinquencyRefundPercent, config.DefaultDelinquencyServiceRefundPercent);
        config.ServiceRefundPartialVestingDays = ReadInt(info, EntryPartialVestingDays, config.ServiceRefundPartialVestingDays);
        config.ServiceRefundPartialVestingScalarPercent = ReadInt(info, EntryPartialVestingScalar, config.ServiceRefundPartialVestingScalarPercent);
        config.ServiceRefundFullVestingDays = ReadInt(info, EntryFullVestingDays, config.ServiceRefundFullVestingDays);
        config.DelinquencyGraceDays = ReadInt(info, EntryDelinquencyGraceDays, config.DelinquencyGraceDays);
        config.DelinquencyRemovalIntervalDays = ReadInt(info, EntryDelinquencyRemovalIntervalDays, config.DelinquencyRemovalIntervalDays);
        config.BankerPurchaseCost = ReadInt(info, EntryBankerPurchaseCost, config.BankerPurchaseCost);
        config.BankerDailyUpkeep = ReadInt(info, EntryBankerDailyUpkeep, config.BankerDailyUpkeep);
        config.MagePurchaseCost = ReadInt(info, EntryMagePurchaseCost, config.MagePurchaseCost);
        config.MageDailyUpkeep = ReadInt(info, EntryMageDailyUpkeep, config.MageDailyUpkeep);
        config.AlchemistPurchaseCost = ReadInt(info, EntryAlchemistPurchaseCost, config.AlchemistPurchaseCost);
        config.AlchemistDailyUpkeep = ReadInt(info, EntryAlchemistDailyUpkeep, config.AlchemistDailyUpkeep);
        config.StablemasterPurchaseCost = ReadInt(info, EntryStablemasterPurchaseCost, config.StablemasterPurchaseCost);
        config.StablemasterDailyUpkeep = ReadInt(info, EntryStablemasterDailyUpkeep, config.StablemasterDailyUpkeep);
        config.InnkeeperPurchaseCost = ReadInt(info, EntryInnkeeperPurchaseCost, config.InnkeeperPurchaseCost);
        config.InnkeeperDailyUpkeep = ReadInt(info, EntryInnkeeperDailyUpkeep, config.InnkeeperDailyUpkeep);
        config.GuardedTownPurchaseCost = ReadInt(info, EntryGuardedTownPurchaseCost, config.GuardedTownPurchaseCost);
        config.GuardedTownDailyUpkeep = ReadInt(info, EntryGuardedTownDailyUpkeep, config.GuardedTownDailyUpkeep);
        config.HuntingTaxPurchaseCost = ReadInt(info, EntryHuntingTaxPurchaseCost, config.HuntingTaxPurchaseCost);
        config.HuntingTaxDailyUpkeep = ReadInt(info, EntryHuntingTaxDailyUpkeep, config.HuntingTaxDailyUpkeep);
        config.HuntingContributionPercent = ReadInt(info, EntryHuntingContributionPercent, config.HuntingContributionPercent);
        config.GuardedTownPatrolGuards = ReadInt(info, EntryPatrolGuardCount, config.GuardedTownPatrolGuards);
        config.VendorRevenueContributionPercent = ReadInt(
            info,
            EntryVendorRevenueContributionPercent,
            config.VendorRevenueContributionPercent
        );

        TownshipSettings.Save(config);
        from.SendMessage(0x35, "Township configuration saved.");

        for (var i = 0; i < TownshipService.Townships.Count; i++)
        {
            TownshipService.AddLog(TownshipService.Townships[i], TownshipLogType.ConfigChanged, from, "Township config updated from CAdmin.");
        }

        DisplayTo(from, _selectedIndex, _configPage);
    }

    private TownshipState GetSelectedTownship()
    {
        var townships = TownshipService.Townships;

        if (townships.Count == 0)
        {
            return null;
        }

        var index = Math.Clamp(_selectedIndex, 0, townships.Count - 1);
        return townships[index];
    }

    private void DrawConfigTabs(ref DynamicGumpBuilder builder)
    {
        DrawButton(ref builder, 46, 106, ButtonConfigGeneral, "General", _configPage == 0 ? 2213 : 2101);
        DrawButton(ref builder, 176, 106, ButtonConfigRefunds, "Refunds", _configPage == 1 ? 2213 : 2101);
        DrawButton(ref builder, 46, 136, ButtonConfigServices, "Services", _configPage == 2 ? 2213 : 2101);
        DrawButton(ref builder, 176, 136, ButtonConfigPerks, "Perks", _configPage == 3 ? 2213 : 2101);
    }

    private void DrawConfigPage(ref DynamicGumpBuilder builder)
    {
        switch (_configPage)
        {
            case 1:
                DrawRefundConfig(ref builder);
                break;
            case 2:
                DrawServiceConfig(ref builder);
                break;
            case 3:
                DrawPerkConfig(ref builder);
                break;
            default:
                DrawGeneralConfig(ref builder);
                break;
        }
    }

    private static void DrawGeneralConfig(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(46, 184, 2213, "General");
        DrawIntEntry(ref builder, 46, 216, "Deed Cost", EntryDeedCost, TownshipSettings.DeedCost);
        DrawIntEntry(ref builder, 46, 252, "Initial Claim Size", EntryInitialSize, TownshipSettings.InitialClaimSize);
        DrawIntEntry(ref builder, 46, 288, "House Buffer", EntryHouseBuffer, TownshipSettings.HouseBuffer);
        DrawIntEntry(ref builder, 46, 324, "Required Edge Contact", EntryEdgeContact, TownshipSettings.EdgeContactRequired);
        DrawIntEntry(ref builder, 46, 360, "Tile Cost", EntryTileCost, TownshipSettings.TileCost);
        DrawIntEntry(ref builder, 46, 396, "Daily Land Upkeep / Tile", EntryLandUpkeep, TownshipSettings.DailyLandUpkeepPerTile);
        DrawIntEntry(ref builder, 46, 432, "Border Render Range", EntryBorderRenderRange, TownshipSettings.BorderRenderRange);
        DrawIntEntry(ref builder, 46, 468, "Delinq. Grace Days", EntryDelinquencyGraceDays, (int)TownshipSettings.DelinquencyGracePeriod.TotalDays);
        DrawIntEntry(ref builder, 46, 504, "Removal Interval Days", EntryDelinquencyRemovalIntervalDays, (int)TownshipSettings.DelinquencyRemovalInterval.TotalDays);
        builder.AddCheckbox(46, 544, 0xD2, 0xD3, TownshipSettings.UpkeepEnabled, SwitchUpkeepEnabled);
        builder.AddLabel(76, 542, TownshipSettings.UpkeepEnabled ? 68 : 33, "Upkeep Enabled");
    }

    private static void DrawRefundConfig(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(46, 184, 2213, "Service Refunds");
        DrawIntEntry(ref builder, 46, 216, "Refund Cap %", EntryRefundCapPercent, TownshipSettings.MaxServiceRefundPercent);
        DrawIntEntry(ref builder, 46, 252, "Voluntary Refund %", EntryVoluntaryRefundPercent, TownshipSettings.DefaultVoluntaryServiceRefundPercent);
        DrawIntEntry(ref builder, 46, 288, "Delinquency Refund %", EntryDelinquencyRefundPercent, TownshipSettings.DefaultDelinquencyServiceRefundPercent);
        DrawIntEntry(ref builder, 46, 324, "Partial Vest Days", EntryPartialVestingDays, TownshipSettings.ServiceRefundPartialVestingDays);
        DrawIntEntry(ref builder, 46, 360, "Partial Vest %", EntryPartialVestingScalar, TownshipSettings.ServiceRefundPartialVestingScalarPercent);
        DrawIntEntry(ref builder, 46, 396, "Full Vest Days", EntryFullVestingDays, TownshipSettings.ServiceRefundFullVestingDays);
    }

    private static void DrawServiceConfig(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(46, 184, 2213, "NPC Services");
        DrawIntEntry(ref builder, 46, 216, "Banker Cost", EntryBankerPurchaseCost, TownshipSettings.BankerPurchaseCost);
        DrawIntEntry(ref builder, 46, 252, "Banker Daily", EntryBankerDailyUpkeep, TownshipSettings.BankerDailyUpkeep);
        DrawIntEntry(ref builder, 46, 288, "Mage Cost", EntryMagePurchaseCost, TownshipSettings.MagePurchaseCost);
        DrawIntEntry(ref builder, 46, 324, "Mage Daily", EntryMageDailyUpkeep, TownshipSettings.MageDailyUpkeep);
        DrawIntEntry(ref builder, 46, 360, "Alchemist Cost", EntryAlchemistPurchaseCost, TownshipSettings.AlchemistPurchaseCost);
        DrawIntEntry(ref builder, 46, 396, "Alchemist Daily", EntryAlchemistDailyUpkeep, TownshipSettings.AlchemistDailyUpkeep);
        DrawIntEntry(ref builder, 46, 432, "Stablemaster Cost", EntryStablemasterPurchaseCost, TownshipSettings.StablemasterPurchaseCost);
        DrawIntEntry(ref builder, 46, 468, "Stablemaster Daily", EntryStablemasterDailyUpkeep, TownshipSettings.StablemasterDailyUpkeep);
        DrawIntEntry(ref builder, 46, 504, "Innkeeper Cost", EntryInnkeeperPurchaseCost, TownshipSettings.InnkeeperPurchaseCost);
        DrawIntEntry(ref builder, 46, 540, "Innkeeper Daily", EntryInnkeeperDailyUpkeep, TownshipSettings.InnkeeperDailyUpkeep);
    }

    private static void DrawPerkConfig(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(46, 184, 2213, "Perks");
        DrawIntEntry(ref builder, 46, 216, "Militia Cost", EntryGuardedTownPurchaseCost, TownshipSettings.GuardedTownPurchaseCost);
        DrawIntEntry(ref builder, 46, 252, "Militia Daily", EntryGuardedTownDailyUpkeep, TownshipSettings.GuardedTownDailyUpkeep);
        DrawIntEntry(ref builder, 46, 288, "Militia Guards", EntryPatrolGuardCount, TownshipSettings.GuardedTownPatrolGuards);
        DrawIntEntry(ref builder, 46, 324, "Hunt Bonus Cost", EntryHuntingTaxPurchaseCost, TownshipSettings.HuntingTaxPurchaseCost);
        DrawIntEntry(ref builder, 46, 360, "Hunt Bonus Daily", EntryHuntingTaxDailyUpkeep, TownshipSettings.HuntingTaxDailyUpkeep);
        DrawIntEntry(ref builder, 46, 396, "Hunt Bonus %", EntryHuntingContributionPercent, TownshipSettings.HuntingContributionPercent);
        DrawIntEntry(ref builder, 46, 432, "Vendor Rev %", EntryVendorRevenueContributionPercent, TownshipSettings.VendorRevenueContributionPercent);
    }

    private static void DrawSelectedTownship(ref DynamicGumpBuilder builder, TownshipState township)
    {
        builder.AddLabel(420, 226, 2213, "Selected Township");

        if (township == null)
        {
            builder.AddLabel(420, 260, 2401, "Select an active township.");
            return;
        }

        var guild = township.Guild;
        var leader = guild?.Leader?.Name ?? "Unknown";
        var regionStatus = township.Region == null ? "No region" : "Region active";

        builder.AddLabelCropped(420, 258, 290, 22, 1153, township.Name ?? "Unknown");
        builder.AddLabelCropped(420, 284, 290, 22, 2101, $"ID: {ShortId(township)}");
        builder.AddLabelCropped(420, 310, 290, 22, 68, $"Guild: {guild?.Abbreviation ?? ""} - {guild?.Name ?? "Unknown"}");
        builder.AddLabelCropped(420, 336, 290, 22, 2101, $"Guildmaster: {leader}");
        builder.AddLabelCropped(420, 362, 290, 22, 2101, $"Location: {township.Map?.Name ?? "Internal"} ({township.FoundingPoint.X}, {township.FoundingPoint.Y}, {township.FoundingPoint.Z})");
        builder.AddLabelCropped(420, 388, 290, 22, 2101, $"Activity: {township.ActivityLevel} | {regionStatus}");
        builder.AddLabelCropped(420, 414, 290, 22, 53, $"Treasury: {township.TreasuryBalance:N0} gp");
        builder.AddLabelCropped(420, 440, 290, 22, township.IsDelinquent ? 33 : 68, township.IsDelinquent ? $"Delinquent: {township.DelinquentBalance:N0} gp" : "Financial Status: Healthy");

        DrawButton(ref builder, 420, 470, ButtonTeleport, "Teleport");
        DrawButton(ref builder, 540, 470, ButtonOpenTownship, "Open Town Gump");
        DrawButton(ref builder, 420, 520, ButtonViewLog, "Staff Log");
    }

    private static void TeleportToTownship(Mobile from, TownshipState township)
    {
        if (township == null)
        {
            from.SendMessage(0x22, "No township is selected.");
            return;
        }

        var hasStone = township.Stone?.Deleted == false;
        var map = hasStone ? township.Stone.Map : township.Map;
        var location = hasStone ? township.Stone.Location : township.FoundingPoint;

        if (map == null || map == Map.Internal)
        {
            from.SendMessage(0x22, "That township does not have a valid map.");
            return;
        }

        from.MoveToWorld(location, map);
        TownshipService.AddLog(township, TownshipLogType.StaffAction, from, $"Teleported to township admin target at {map.Name} ({location.X}, {location.Y}, {location.Z}).");
    }

    private static int ReadInt(RelayInfo info, int entryId, int fallback)
    {
        var text = info.GetTextEntry(entryId);
        return int.TryParse(text, out var value) ? value : fallback;
    }

    private static void DrawIntEntry(ref DynamicGumpBuilder builder, int x, int y, string label, int entryId, int value)
    {
        builder.AddLabel(x, y, 2101, label);
        builder.AddBackground(x + 190, y - 4, 110, 28, 9350);
        builder.AddTextEntry(x + 196, y, 96, 20, 1153, entryId, value.ToString());
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label)
    {
        DrawButton(ref builder, x, y, buttonId, label, 2101);
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label, int hue)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 34, y + 2, hue, label);
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }

    private static string ShortId(TownshipState township) =>
        string.IsNullOrWhiteSpace(township?.Id) || township.Id.Length < 8 ? township?.Id ?? "unknown" : township.Id[..8];
}

public sealed class TownshipAdminLogGump : DynamicGump
{
    private const int ButtonBack = 1;
    private const int ButtonPreviousPage = 2;
    private const int ButtonNextPage = 3;
    private const int PageSize = 6;

    private readonly Mobile _from;
    private readonly TownshipState _township;
    private readonly int _selectedIndex;
    private readonly int _page;

    public override bool Singleton => true;

    private TownshipAdminLogGump(Mobile from, TownshipState township, int selectedIndex, int page) : base(90, 70)
    {
        _from = from;
        _township = township;
        _selectedIndex = selectedIndex;
        _page = page;
    }

    public static void DisplayTo(Mobile from, TownshipState township, int selectedIndex, int page = 0)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel.Developer)
        {
            from?.SendMessage(0x22, "Township logs require Developer access.");
            return;
        }

        from.CloseGump<TownshipAdminLogGump>();
        from.SendGump(new TownshipAdminLogGump(from, township, selectedIndex, Math.Max(0, page)));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 760, 620, 9270);
        builder.AddAlphaRegion(15, 15, 730, 590);
        builder.AddLabel(312, 24, 1153, "Township Staff Log");
        DrawRule(ref builder, 34, 54, 652);

        if (_township == null)
        {
            builder.AddLabel(46, 82, 2401, "No township is selected.");
            DrawButton(ref builder, 46, 560, ButtonBack, "Back");
            return;
        }

        builder.AddLabelCropped(46, 82, 600, 22, 2213, $"{_township.Name} ({ShortId(_township)})");
        builder.AddLabel(46, 112, 2213, "Major Activity");

        var y = 142;
        var total = _township.ActivityLog.Count;
        var maxPage = Math.Max(0, (total - 1) / PageSize);
        var page = Math.Clamp(_page, 0, maxPage);
        var start = page * PageSize;
        var max = Math.Min(PageSize, total - start);

        if (max == 0)
        {
            builder.AddLabel(46, y, 2401, "No activity has been logged.");
        }
        else
        {
            for (var i = 0; i < max; i++)
            {
                var entry = _township.ActivityLog[start + i];
                builder.AddHtml(
                    46,
                    y,
                    660,
                    56,
                    $"{entry.Timestamp:g} - {entry.Type} - {entry.ActorName}: {entry.Details}",
                    entry.Type == TownshipLogType.StaffAction ? "#FF4C7A" : "#D8D8D8",
                    scrollbar: false
                );
                y += 62;
            }
        }

        DrawRule(ref builder, 34, 520, 652);
        DrawButton(ref builder, 46, 544, ButtonBack, "Back to Admin");

        if (page > 0)
        {
            DrawButton(ref builder, 248, 544, ButtonPreviousPage, "Previous");
        }

        builder.AddLabel(398, 546, 2401, $"Page {page + 1:N0} of {maxPage + 1:N0}");

        if (page < maxPage)
        {
            DrawButton(ref builder, 548, 544, ButtonNextPage, "Next");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || from.AccessLevel < AccessLevel.Developer)
        {
            return;
        }

        if (info.ButtonID == ButtonBack)
        {
            TownshipAdminGump.DisplayTo(from, _selectedIndex);
        }
        else if (info.ButtonID == ButtonPreviousPage)
        {
            DisplayTo(from, _township, _selectedIndex, Math.Max(0, _page - 1));
        }
        else if (info.ButtonID == ButtonNextPage)
        {
            DisplayTo(from, _township, _selectedIndex, _page + 1);
        }
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 34, y + 2, 2101, label);
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }

    private static string ShortId(TownshipState township) =>
        string.IsNullOrWhiteSpace(township?.Id) || township.Id.Length < 8 ? township?.Id ?? "unknown" : township.Id[..8];
}
