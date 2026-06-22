using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Items;
using Server.Network;
using Server.Prompts;
using Server.Targeting;

namespace Server.Custom.Systems.Townships;

public sealed class TownshipGump : DynamicGump
{
    private const int ButtonTreasury = 1;
    private const int ButtonDeposit = 2;
    private const int ButtonDepositLog = 3;
    private const int ButtonBorders = 4;
    private const int ButtonFoundingPoint = 5;
    private const int ButtonExpand = 6;
    private const int ButtonUpkeep = 7;
    private const int ButtonActivity = 8;
    private const int ButtonMoveStone = 9;
    private const int ButtonRename = 10;
    private const int ButtonAbolish = 11;
    private const int ButtonOverview = 12;
    private const int ButtonControl = 13;
    private const int ButtonPreviousPage = 14;
    private const int ButtonNextPage = 15;
    private const int ButtonStaffTools = 16;
    private const int ButtonStaffClearTreasury = 17;
    private const int ButtonStaffClearTreasuryLog = 18;
    private const int ButtonStaffSetActivity = 20;
    private const int ButtonStaffAddActivity = 21;
    private const int ButtonStaffRefreshRegion = 22;
    private const int ButtonStaffLog = 23;
    private const int ButtonStaffApplyTreasury = 24;
    private const int ButtonStaffSetNextCharge = 25;
    private const int ButtonStaffClearLifetimeDeposits = 26;
    private const int ButtonDelinquencyDetails = 27;
    private const int ButtonStaffSetDelinquency = 28;
    private const int ButtonServices = 29;
    private const int ButtonStaffAddService = 30;
    private const int ButtonPurchaseBanker = 31;
    private const int ButtonPurchaseMage = 32;
    private const int ButtonPurchaseAlchemist = 33;
    private const int ButtonPurchaseStablemaster = 34;
    private const int ButtonPurchaseInnkeeper = 35;
    private const int ButtonPerks = 36;
    private const int ButtonPurchaseGuardedTown = 37;
    private const int ButtonPurchaseHuntingTax = 38;
    private const int ButtonEnableGuardedTown = 43;
    private const int ButtonDisableGuardedTown = 44;
    private const int ButtonEnableHuntingTax = 45;
    private const int ButtonDisableHuntingTax = 46;
    private const int ButtonRankPermissions = 47;
    private const int ButtonStaffRemoveServiceBase = 300;
    private const int ButtonTreasuryDetailsBase = 5000;
    private const int ButtonRankBase = 7000;
    private const int EntryStaffActivity = 100;
    private const int EntryStaffTreasuryAdjustment = 101;
    private const int EntryStaffTreasuryPlayerNote = 102;
    private const int EntryStaffTreasuryStaffNote = 103;
    private const int EntryStaffServiceName = 104;
    private const int EntryStaffServiceCost = 105;
    private const int EntryStaffServiceUpkeep = 106;
    private const int Width = 760;
    private const int Height = 600;
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueGold = 53;
    private const int HueGood = 68;
    private const int HueWarn = 33;

    private readonly Mobile _from;
    private readonly TownshipState _township;
    private readonly TownshipGumpView _view;
    private readonly int _page;

    public override bool Singleton => true;

    private TownshipGump(Mobile from, TownshipState township, TownshipGumpView view, int page) : base(80, 60)
    {
        _from = from;
        _township = township;
        _view = view;
        _page = page;
    }

    public static void DisplayTo(Mobile from, TownshipState township, TownshipGumpView view = TownshipGumpView.Overview, int page = 0)
    {
        if (from?.NetState == null || township == null)
        {
            return;
        }

        if (!TownshipService.IsGuildMember(from, township.Guild))
        {
            from.CloseGump<TownshipGump>();

            if (township.IsDelinquent)
            {
                TownshipPublicDelinquencyGump.DisplayTo(from, township);
            }
            else
            {
                from.SendMessage(0x22, "Only township guild members may view this township stone.");
            }

            return;
        }

        from.CloseGump<TownshipGump>();
        from.SendGump(new TownshipGump(from, township, view, Math.Max(0, page)));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, Width, Height, 9270);
        builder.AddAlphaRegion(15, 15, Width - 30, Height - 30);
        builder.AddLabel(38, 24, HueTitle, _township.Name ?? "Township");
        builder.AddLabel(560, 24, HueMuted, $"ID: {GetShortId(_township)}");
        DrawRule(ref builder, 34, 54, 692);
        DrawNav(ref builder);
        DrawRule(ref builder, 34, 118, 692);

        switch (_view)
        {
            case TownshipGumpView.Treasury:
                DrawTreasury(ref builder);
                break;
            case TownshipGumpView.DepositLog:
                DrawDepositLog(ref builder);
                break;
            case TownshipGumpView.Upkeep:
                DrawUpkeep(ref builder);
                break;
            case TownshipGumpView.Services:
                DrawServices(ref builder);
                break;
            case TownshipGumpView.Perks:
                DrawPerks(ref builder);
                break;
            case TownshipGumpView.Delinquency:
                DrawDelinquency(ref builder);
                break;
            case TownshipGumpView.Activity:
                DrawActivity(ref builder);
                break;
            case TownshipGumpView.Control:
                DrawControl(ref builder);
                break;
            case TownshipGumpView.StaffTools:
                DrawStaffTools(ref builder);
                break;
            default:
                DrawOverview(ref builder);
                break;
        }

        if (TownshipService.CanUseStaffTools(_from) && _view != TownshipGumpView.StaffTools)
        {
            DrawButton(ref builder, 610, Height - 56, ButtonStaffTools, "[STAFF]", HueWarn);
        }
    }

    private void DrawNav(ref DynamicGumpBuilder builder)
    {
        DrawButton(ref builder, 42, 66, ButtonOverview, "Overview", _view == TownshipGumpView.Overview ? HueHeader : HueText);
        DrawButton(ref builder, 178, 66, ButtonTreasury, "Treasury", _view == TownshipGumpView.Treasury || _view == TownshipGumpView.DepositLog ? HueHeader : HueText);
        DrawButton(ref builder, 314, 66, ButtonControl, "Control", _view == TownshipGumpView.Control ? HueHeader : HueText);
        DrawButton(ref builder, 450, 66, ButtonServices, "Services", _view == TownshipGumpView.Services ? HueHeader : HueText);
        DrawButton(ref builder, 42, 94, ButtonPerks, "Perks", _view == TownshipGumpView.Perks ? HueHeader : HueText);
        DrawButton(ref builder, 178, 94, ButtonUpkeep, "Upkeep", _view == TownshipGumpView.Upkeep || _view == TownshipGumpView.Delinquency ? HueHeader : HueText);
        DrawButton(ref builder, 314, 94, ButtonActivity, "Activity", _view == TownshipGumpView.Activity ? HueHeader : HueText);
    }

    private void DrawOverview(ref DynamicGumpBuilder builder)
    {
        var guildmaster = _township.Guild?.Leader?.Name ?? "Unknown";

        builder.AddLabel(46, 126, HueHeader, "Township Overview");
        builder.AddLabel(46, 164, HueText, "Town:");
        builder.AddLabel(190, 164, HueTitle, _township.Name ?? "Unknown");
        builder.AddLabel(46, 192, HueText, "Guild:");
        builder.AddLabel(190, 192, HueGood, $"{_township.Guild?.Abbreviation ?? ""} - {_township.Guild?.Name ?? "Unknown"}");
        builder.AddLabel(46, 220, HueText, "Guildmaster:");
        builder.AddLabel(190, 220, HueText, guildmaster);
        builder.AddLabel(46, 248, HueText, "Treasury:");
        builder.AddLabel(190, 248, HueGold, $"{_township.TreasuryBalance:N0} gp");
        builder.AddLabel(46, 276, HueText, "Activity:");
        builder.AddLabel(190, 276, HueTitle, _township.ActivityLevel.ToString());
        builder.AddLabel(46, 304, HueText, "Claimed Tiles:");
        builder.AddLabel(190, 304, HueText, $"{_township.ClaimedTileCount:N0}");
        builder.AddLabel(390, 164, HueText, "Max Border Range:");
        builder.AddLabel(534, 164, HueText, $"{_township.MaxEnvelopeSize}x{_township.MaxEnvelopeSize}");
        builder.AddLabel(390, 192, HueText, "Daily Assessment:");
        DrawGoldChange(ref builder, 534, 192, -TownshipService.GetDailyUpkeep(_township));
        builder.AddLabel(390, 220, HueText, "Founded:");
        builder.AddLabel(534, 220, HueText, $"{_township.FoundedAt:g}");
        builder.AddLabel(390, 248, HueText, "Founding Point:");
        builder.AddLabel(534, 248, HueText, $"{_township.Map?.Name ?? "Internal"} ({_township.FoundingPoint.X}, {_township.FoundingPoint.Y}, {_township.FoundingPoint.Z})");
    }

    private void DrawTreasury(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(46, 126, HueHeader, "Township Treasury");
        builder.AddLabel(46, 164, HueText, "Current Balance:");
        builder.AddLabel(230, 164, HueGold, $"{_township.TreasuryBalance:N0} gp");
        builder.AddLabel(46, 192, HueText, "Lifetime Deposits:");
        builder.AddLabel(230, 192, HueGold, $"{_township.LifetimeDeposits:N0} gp");
        builder.AddLabel(46, 226, HueMuted, "Deposits cannot be withdrawn once made.");

        var buttonY = 272;

        if (_township.IsDelinquent)
        {
            builder.AddLabel(46, 250, HueWarn, $"Delinquent Balance: -{_township.DelinquentBalance:N0} gp");
            DrawButton(ref builder, 46, 276, ButtonDelinquencyDetails, "View Delinquency Details", HueWarn);
            buttonY = 326;
        }

        DrawButton(ref builder, 46, buttonY, ButtonDeposit, _township.IsDelinquent && !TownshipService.IsGuildMember(_from, _township.Guild) ? "Donate Gold to Treasury" : "Deposit Gold to Treasury");
        DrawButton(ref builder, 46, buttonY + 34, ButtonDepositLog, "View Treasury Activity");
    }

    private void DrawDepositLog(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(46, 126, HueHeader, "Treasury Activity Log");

        const int pageSize = 6;
        var total = _township.DepositLog.Count;
        var maxPage = Math.Max(0, (total - 1) / pageSize);
        var page = Math.Clamp(_page, 0, maxPage);
        var start = page * pageSize;
        var max = Math.Min(pageSize, total - start);
        var y = 164;

        if (max == 0)
        {
            builder.AddLabel(46, y, HueMuted, "No treasury activity has been recorded.");
            return;
        }

        for (var i = 0; i < max; i++)
        {
            var entry = _township.DepositLog[start + i];
            var amountHue = entry.Amount < 0 ? HueWarn : HueGold;
            var amountText = FormatSignedGold(entry.Amount);

            builder.AddLabelCropped(46, y, 140, 22, HueText, $"{entry.Timestamp:g} -");
            builder.AddLabelCropped(188, y, 96, 22, amountHue, amountText);
            builder.AddHtml(
                288,
                y,
                entry.AggregateCount > 0 ? 296 : 378,
                34,
                FormatTreasuryActivityDetails(entry),
                "#D8D8D8",
                scrollbar: false
            );

            if (entry.AggregateCount > 0)
            {
                DrawButton(ref builder, 590, y - 2, ButtonTreasuryDetailsBase + start + i, "Details");
            }

            y += 38;
        }

        DrawPageControls(ref builder, 46, 410, page, maxPage, _view);
    }

    private void DrawPageControls(ref DynamicGumpBuilder builder, int x, int y, int page, int maxPage, TownshipGumpView view)
    {
        if (page > 0)
        {
            DrawButton(ref builder, x, y, ButtonPreviousPage, "Previous");
        }

        builder.AddLabel(x + 242, y + 2, HueMuted, $"Page {page + 1:N0} of {maxPage + 1:N0}");

        if (page < maxPage)
        {
            DrawButton(ref builder, x + 420, y, ButtonNextPage, "Next");
        }
    }

    private void DrawUpkeep(ref DynamicGumpBuilder builder)
    {
        var daily = TownshipService.GetDailyUpkeep(_township);
        var dailyLand = TownshipService.GetDailyLandUpkeep(_township);
        var dailyServices = TownshipService.GetDailyServiceUpkeep(_township);
        var projectedWeekly = daily * 7;
        var daysRemaining = TownshipService.GetEstimatedUpkeepDaysRemaining(_township);
        var lastPayment = GetLastUpkeepPayment(_township);

        builder.AddLabel(46, 126, HueHeader, "Upkeep Breakdown");
        builder.AddLabel(46, 164, HueText, "Daily Assessment:");
        DrawGoldChange(ref builder, 230, 164, -daily);
        builder.AddLabel(46, 192, HueText, "Accrued Due:");
        DrawGoldChange(ref builder, 230, 192, -_township.AccruedUpkeepDue);
        builder.AddLabel(46, 220, HueText, $"Next Daily Assessment: {_township.NextUpkeepAssessment:g}");
        builder.AddLabel(46, 248, HueText, $"Next Weekly Payment: {_township.NextWeeklyPayment:g}");
        builder.AddLabel(46, 276, HueText, $"Estimated Days Remaining: {FormatDaysRemaining(daysRemaining)}");
        builder.AddLabel(46, 304, TownshipSettings.UpkeepEnabled ? HueGood : HueWarn, TownshipSettings.UpkeepEnabled ? "Upkeep Enabled" : "Upkeep Disabled");

        builder.AddLabel(46, 350, HueHeader, "Last Payment");
        if (lastPayment == null)
        {
            builder.AddLabel(46, 382, HueMuted, "No upkeep payment has been recorded.");
        }
        else
        {
            builder.AddLabel(46, 382, HueText, $"{lastPayment.Timestamp:g}");
            DrawGoldChange(ref builder, 230, 382, lastPayment.Amount);
        }

        builder.AddLabel(390, 164, HueHeader, "Current Cost Breakdown");
        builder.AddLabel(390, 198, HueText, $"Land: {_township.ClaimedTileCount:N0} tile(s) x {TownshipSettings.DailyLandUpkeepPerTile:N0} gp");
        builder.AddLabel(390, 226, HueText, "Daily Land:");
        DrawGoldChange(ref builder, 560, 226, -dailyLand);
        builder.AddLabel(390, 254, HueText, "Daily Services:");
        DrawGoldChange(ref builder, 560, 254, -dailyServices);
        builder.AddLabel(390, 282, HueText, "Projected 7 Days:");
        DrawGoldChange(ref builder, 560, 282, -projectedWeekly);
        builder.AddLabel(390, 324, HueText, "Due This Week:");
        DrawGoldChange(ref builder, 560, 324, -_township.AccruedUpkeepDue);
        builder.AddHtml(
            390,
            356,
            300,
            54,
            "Upkeep is assessed daily and paid weekly. Land changes affect future daily assessments, not past days.",
            "#D8D8D8",
            scrollbar: false
        );

        if (_township.IsDelinquent)
        {
            builder.AddLabel(390, 416, HueWarn, $"Delinquent: -{_township.DelinquentBalance:N0} gp");
            DrawButton(ref builder, 390, 442, ButtonDelinquencyDetails, "Delinquency Details", HueWarn);
        }
    }

    private void DrawDelinquency(ref DynamicGumpBuilder builder)
    {
        var plan = TownshipService.GetDelinquencyPlan(_township);

        builder.AddLabel(46, 126, HueWarn, "Delinquency Details");

        if (plan == null)
        {
            builder.AddLabel(46, 164, HueGood, "This township is not delinquent.");
            DrawButton(ref builder, 46, 410, ButtonUpkeep, "Back to Upkeep");
            return;
        }

        builder.AddLabel(46, 164, HueText, "Delinquent Balance:");
        DrawGoldChange(ref builder, 230, 164, -plan.DelinquentBalance);
        builder.AddLabel(46, 192, HueText, "Accrued This Week:");
        DrawGoldChange(ref builder, 230, 192, -plan.AccruedUpkeepDue);
        builder.AddLabel(46, 220, HueText, "Delinquent Since:");
        builder.AddLabel(230, 220, HueText, $"{plan.DelinquentSince:g}");
        builder.AddLabel(46, 248, HueText, "Next Removal Check:");
        builder.AddLabel(230, 248, HueWarn, plan.NextRemovalCheck == DateTime.MinValue ? "Not scheduled" : $"{plan.NextRemovalCheck:g}");
        builder.AddLabel(46, 276, plan.ServicesSuspended ? HueWarn : HueGood, plan.ServicesSuspended ? "Paid services are suspended." : "Paid services are active.");
        builder.AddHtml(
            46,
            310,
            300,
            62,
            "Pay the delinquent balance through the treasury to restore paid services. Current-week accrued upkeep remains due at the next weekly payment.",
            "#D8D8D8",
            scrollbar: false
        );

        builder.AddLabel(390, 164, HueHeader, "Removal Order");

        if (plan.RemovalOrder.Count == 0)
        {
            builder.AddHtml(
                390,
                198,
                300,
                72,
                "No removable paid services exist yet. Future services will be listed here in removal order, with land claim removed last.",
                "#D8D8D8",
                scrollbar: false
            );
        }
        else
        {
            var y = 198;

            for (var i = 0; i < plan.RemovalOrder.Count && i < 7; i++)
            {
                var entry = plan.RemovalOrder[i];
                builder.AddLabelCropped(390, y, 310, 22, HueText, $"{i + 1}. {entry.Name} - {entry.DailyUpkeep:N0} gp/day");
                builder.AddLabelCropped(408, y + 22, 292, 22, HueMuted, $"{entry.Status} at {entry.ScheduledRemoval:g}");
                y += 48;
            }
        }

        DrawButton(ref builder, 46, 410, ButtonUpkeep, "Back to Upkeep");
    }

    private void DrawControl(ref DynamicGumpBuilder builder)
    {
        var y = 232;

        builder.AddLabel(46, 126, HueHeader, "Township Control");
        builder.AddLabel(390, 126, HueHeader, "Township Ranks");
        builder.AddLabel(390, 154, HueText, $"Your Rank: {TownshipService.GetRankName(_township, _from)}");
        DrawButton(ref builder, 390, 180, ButtonRankPermissions, "View Rank Permissions");
        DrawButton(ref builder, 46, 164, ButtonBorders, TownshipMarkerService.IsViewingBorders(_from) ? "Hide Borders" : "Show Borders");
        DrawButton(ref builder, 46, 198, ButtonFoundingPoint, "Show Founding Point");

        if (TownshipService.HasPermission(_township, _from, TownshipPermission.ExpandTerritory))
        {
            DrawButton(ref builder, 46, y, ButtonExpand, "Expand Territory");
            y += 34;
        }

        if (TownshipService.HasPermission(_township, _from, TownshipPermission.MoveStone))
        {
            DrawButton(ref builder, 46, y, ButtonMoveStone, "Move Charter Here");
            y += 34;
        }

        if (TownshipService.HasPermission(_township, _from, TownshipPermission.RenameTownship))
        {
            DrawButton(ref builder, 46, y, ButtonRename, "Rename Town");
            y += 34;
        }

        if (TownshipService.HasPermission(_township, _from, TownshipPermission.AbolishTownship))
        {
            DrawButton(ref builder, 46, y + 18, ButtonAbolish, "Abolish Township", HueWarn);
        }

        DrawRankList(ref builder);
    }

    private void DrawRankList(ref DynamicGumpBuilder builder)
    {
        var guild = _township.Guild;

        if (guild == null)
        {
            builder.AddLabel(390, 220, HueMuted, "Guild data is unavailable.");
            return;
        }

        var canManageRanks = TownshipService.HasPermission(_township, _from, TownshipPermission.ManageRanks);
        var y = 220;
        var shown = 0;

        for (var i = 0; i < guild.Members.Count && shown < 9; i++)
        {
            var member = guild.Members[i];

            if (member?.Deleted != false)
            {
                continue;
            }

            var rankName = TownshipService.GetRankName(_township, member);
            var hue = guild.Leader == member ? HueGold :
                TownshipService.GetRank(_township, member) >= TownshipRankLevel.Regent ? HueGood : HueText;

            builder.AddLabelCropped(390, y, 138, 22, HueText, member.Name ?? "Unknown");
            builder.AddLabelCropped(532, y, 92, 22, hue, rankName);

            if (canManageRanks && guild.Leader != member)
            {
                DrawButton(ref builder, 626, y - 2, ButtonRankBase + i, "Change");
            }

            y += 28;
            shown++;
        }

        if (shown == 0)
        {
            builder.AddLabel(390, y, HueMuted, "No guild members are available.");
        }
    }

    private void DrawServices(ref DynamicGumpBuilder builder)
    {
        var active = 0;
        var suspended = 0;
        var removed = 0;

        for (var i = 0; i < _township.Services.Count; i++)
        {
            if (TownshipService.IsPerkService(_township.Services[i].Type))
            {
                continue;
            }

            switch (_township.Services[i].Status)
            {
                case TownshipPaidServiceStatus.Active:
                    active++;
                    break;
                case TownshipPaidServiceStatus.Suspended:
                    suspended++;
                    break;
                case TownshipPaidServiceStatus.Removed:
                    removed++;
                    break;
            }
        }

        builder.AddLabel(46, 126, HueHeader, "Township Services");
        builder.AddLabel(46, 164, HueText, $"Active: {active:N0}");
        builder.AddLabel(170, 164, suspended > 0 ? HueWarn : HueText, $"Suspended: {suspended:N0}");
        builder.AddLabel(336, 164, HueMuted, $"Removed: {removed:N0}");
        builder.AddLabel(46, 192, HueText, "Daily NPC Upkeep:");
        DrawGoldChange(ref builder, 240, 192, -GetDailyNpcServiceUpkeep(_township));

        if (_township.PaidServicesSuspended)
        {
            builder.AddLabel(46, 220, HueWarn, "Paid services are suspended until delinquency is paid.");
        }
        else if (TownshipService.HasPermission(_township, _from, TownshipPermission.PurchaseServices))
        {
            DrawButton(
                ref builder,
                46,
                218,
                ButtonPurchaseBanker,
                $"Purchase Banker ({TownshipSettings.BankerPurchaseCost:N0} gp)",
                HueGold
            );
            DrawButton(
                ref builder,
                336,
                218,
                ButtonPurchaseMage,
                $"Purchase Mage ({TownshipSettings.MagePurchaseCost:N0} gp)",
                HueGold
            );
            DrawButton(
                ref builder,
                46,
                248,
                ButtonPurchaseAlchemist,
                $"Purchase Alchemist ({TownshipSettings.AlchemistPurchaseCost:N0} gp)",
                HueGold
            );
            DrawButton(
                ref builder,
                336,
                248,
                ButtonPurchaseStablemaster,
                $"Purchase Stablemaster ({TownshipSettings.StablemasterPurchaseCost:N0} gp)",
                HueGold
            );
            DrawButton(
                ref builder,
                46,
                278,
                ButtonPurchaseInnkeeper,
                $"Purchase Innkeeper ({TownshipSettings.InnkeeperPurchaseCost:N0} gp)",
                HueGold
            );
        }

        builder.AddLabel(46, 326, HueHeader, "Services");

        var y = 356;
        var shown = 0;

        for (var i = 0; i < _township.Services.Count && shown < 5; i++)
        {
            var service = _township.Services[i];

            if (service.Status == TownshipPaidServiceStatus.Removed)
            {
                continue;
            }

            if (TownshipService.IsPerkService(service.Type))
            {
                continue;
            }

            var refund = TownshipService.CalculateServiceRefund(service, service.Status == TownshipPaidServiceStatus.Removed);
            var hue = service.Status switch
            {
                TownshipPaidServiceStatus.Active    => HueGood,
                TownshipPaidServiceStatus.Suspended => HueWarn,
                _                                   => HueMuted
            };

            builder.AddLabelCropped(46, y, 196, 22, hue, $"{service.Name} ({service.Type})");
            builder.AddLabelCropped(246, y, 80, 22, hue, service.Status.ToString());
            DrawGoldChange(ref builder, 330, y, -service.DailyUpkeep);
            builder.AddLabelCropped(438, y, 82, 22, HueGold, $"Ref {refund.RefundAmount:N0}");

            if (TownshipService.HasPermission(_township, _from, TownshipPermission.RemoveServices) && service.Status != TownshipPaidServiceStatus.Removed)
            {
                DrawButton(ref builder, 660, y - 2, ButtonStaffRemoveServiceBase + i, "Remove", HueWarn);
            }

            y += 28;
            shown++;
        }

        if (shown == 0)
        {
            builder.AddLabel(46, y, HueMuted, "No paid services have been added yet.");
        }

        if (!TownshipService.CanUseStaffTools(_from))
        {
            return;
        }

        builder.AddLabel(46, 514, HueWarn, "[STAFF] Add Placeholder Service");
        builder.AddLabel(46, 546, HueText, "Name:");
        builder.AddBackground(96, 542, 160, 24, 9350);
        builder.AddTextEntry(102, 545, 146, 18, 1153, EntryStaffServiceName, "Test Banker");
        builder.AddLabel(270, 546, HueText, "Cost:");
        builder.AddBackground(320, 542, 90, 24, 9350);
        builder.AddTextEntry(326, 545, 76, 18, 1153, EntryStaffServiceCost, "1000000");
        builder.AddLabel(424, 546, HueText, "Daily:");
        builder.AddBackground(480, 542, 90, 24, 9350);
        builder.AddTextEntry(486, 545, 76, 18, 1153, EntryStaffServiceUpkeep, "50000");
        DrawButton(ref builder, 584, 540, ButtonStaffAddService, "[STAFF] Add", HueWarn);
    }

    private void DrawPerks(ref DynamicGumpBuilder builder)
    {
        var canPurchase = TownshipService.HasPermission(_township, _from, TownshipPermission.PurchasePerks);
        var canToggle = TownshipService.HasPermission(_township, _from, TownshipPermission.TogglePerks);
        var guarded = TownshipService.FindFirstService(_township, TownshipPaidServiceType.GuardedTown);
        var huntingTax = TownshipService.FindFirstService(_township, TownshipPaidServiceType.HuntingTax);
        var guardedActive = TownshipService.HasActivePerk(_township, TownshipPaidServiceType.GuardedTown);
        var huntingTaxActive = TownshipService.HasActivePerk(_township, TownshipPaidServiceType.HuntingTax);

        builder.AddLabel(46, 126, HueHeader, "Township Perks");
        builder.AddLabel(46, 164, HueText, "Town Militia:");
        builder.AddLabel(230, 164, GetServiceStatusHue(guarded), GetPerkStatusText(guarded, guardedActive));
        builder.AddLabel(390, 164, HueText, $"Cost: {TownshipSettings.GuardedTownPurchaseCost:N0} gp");
        builder.AddLabel(540, 164, HueWarn, $"-{TownshipSettings.GuardedTownDailyUpkeep:N0} gp/day");
        builder.AddLabel(46, 194, HueText, $"Patrol Guards: {TownshipSettings.GuardedTownPatrolGuards:N0}");
        builder.AddHtml(
            46,
            222,
            620,
            42,
            "Maintains mounted militia guards who patrol claimed land and pursue criminals or hostile monsters within the town's max border range. The perk is suspended during delinquency.",
            "#D8D8D8",
            scrollbar: false
        );

        if (canPurchase && guarded == null)
        {
            DrawButton(ref builder, 46, 272, ButtonPurchaseGuardedTown, "Purchase Town Militia", HueGold);
        }
        else if (canToggle && guarded?.Status == TownshipPaidServiceStatus.Active)
        {
            DrawButton(ref builder, 46, 272, ButtonDisableGuardedTown, "Disable Town Militia", HueWarn);
        }
        else if (canToggle && guarded?.Status == TownshipPaidServiceStatus.Disabled && !_township.IsDelinquent)
        {
            DrawButton(ref builder, 46, 272, ButtonEnableGuardedTown, "Enable Town Militia", HueGood);
        }

        builder.AddLabel(46, 320, HueText, "Hunting Bonus:");
        builder.AddLabel(230, 320, GetServiceStatusHue(huntingTax), GetPerkStatusText(huntingTax, huntingTaxActive));
        builder.AddLabel(390, 320, HueText, $"Cost: {TownshipSettings.HuntingTaxPurchaseCost:N0} gp");
        builder.AddLabel(540, 320, TownshipSettings.HuntingTaxDailyUpkeep > 0 ? HueWarn : HueGold, $"-{TownshipSettings.HuntingTaxDailyUpkeep:N0} gp/day");
        builder.AddLabel(46, 350, HueText, "Rate:");
        builder.AddLabel(230, 350, HueGold, $"{TownshipSettings.HuntingContributionPercent:N0}%");
        builder.AddLabel(390, 350, HueText, "Source:");
        builder.AddLabel(540, 350, HueTitle, "Generated");
        builder.AddHtml(
            46,
            380,
            620,
            52,
            "Generates bonus treasury gold from eligible guild-member monster kills. This does not remove gold from the corpse or from the hunter.",
            "#D8D8D8",
            scrollbar: false
        );

        if (canPurchase || canToggle)
        {
            if (huntingTax == null)
            {
                if (canPurchase)
                {
                    DrawButton(ref builder, 46, 442, ButtonPurchaseHuntingTax, "Purchase Hunting Bonus", HueGold);
                }
            }
            else
            {
                if (canToggle && huntingTax.Status == TownshipPaidServiceStatus.Active)
                {
                    DrawButton(ref builder, 46, 442, ButtonDisableHuntingTax, "Disable Hunting Bonus", HueWarn);
                }
                else if (canToggle && huntingTax.Status == TownshipPaidServiceStatus.Disabled && !_township.IsDelinquent)
                {
                    DrawButton(ref builder, 46, 442, ButtonEnableHuntingTax, "Enable Hunting Bonus", HueGood);
                }
            }
        }
    }

    private static int GetServiceStatusHue(TownshipPaidServiceRecord service) => service?.Status switch
    {
        TownshipPaidServiceStatus.Active => HueGood,
        TownshipPaidServiceStatus.Suspended => HueWarn,
        TownshipPaidServiceStatus.Disabled => HueMuted,
        _ => HueMuted
    };

    private static string GetPerkStatusText(TownshipPaidServiceRecord service, bool active)
    {
        if (service == null)
        {
            return "Not Purchased";
        }

        return active ? "Active" : service.Status.ToString();
    }

    private void DrawActivity(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(46, 126, HueHeader, "Township Activity");
        builder.AddLabel(46, 164, HueText, "Current Level:");
        builder.AddLabel(190, 164, HueTitle, _township.ActivityLevel.ToString());
        builder.AddLabel(46, 192, HueText, $"Activity Score: {_township.ActivityScore:N0}");
        builder.AddLabel(46, 220, HueMuted, "Guild member movement contributes half as much as visitor movement.");
        builder.AddLabel(46, 248, HueText, $"Last Daily Decay: {_township.LastActivityDecay:g}");
        builder.AddLabel(46, 292, HueHeader, "Recent Activity Gains");

        var y = 322;
        var shown = 0;

        for (var i = 0; i < _township.ActivityLog.Count && shown < 4; i++)
        {
            var entry = _township.ActivityLog[i];

            if (entry.Type != TownshipLogType.ActivityGain)
            {
                continue;
            }

            builder.AddLabelCropped(46, y, 620, 22, HueText, $"{entry.Timestamp:g} - {entry.ActorName}: {entry.Details}");
            y += 24;
            shown++;
        }

        if (shown == 0)
        {
            builder.AddLabel(46, y, HueMuted, "No activity gains have been recorded yet.");
        }
    }

    private void DrawStaffTools(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(46, 126, HueWarn, "[STAFF] Township Tools");

        if (!TownshipService.CanUseStaffTools(_from))
        {
            builder.AddLabel(46, 164, HueMuted, "These controls are staff-only.");
            return;
        }

        builder.AddLabel(46, 164, HueText, $"Treasury: {_township.TreasuryBalance:N0} gp");
        builder.AddLabel(46, 192, HueText, $"Activity: {_township.ActivityLevel} ({_township.ActivityScore:N0})");
        builder.AddLabel(46, 220, HueText, $"Treasury Activity Entries: {_township.DepositLog.Count:N0}");
        builder.AddLabel(46, 248, HueText, $"Staff/Activity Log Entries: {_township.ActivityLog.Count:N0}");
        builder.AddLabel(46, 276, _township.IsDelinquent ? HueWarn : HueGood, $"Delinquency: {_township.DelinquentBalance:N0} gp");
        DrawButton(ref builder, 46, 304, ButtonStaffSetDelinquency, "[STAFF] Set Delinquency", HueWarn);

        builder.AddLabel(390, 164, HueHeader, "Staff Actions");
        DrawButton(ref builder, 390, 194, ButtonStaffClearTreasury, "[STAFF] Clear Treasury", HueWarn);
        DrawButton(ref builder, 390, 228, ButtonStaffClearTreasuryLog, "[STAFF] Clear Treasury Activity", HueWarn);
        DrawButton(ref builder, 390, 262, ButtonStaffRefreshRegion, "[STAFF] Refresh Region/Borders", HueWarn);
        DrawButton(ref builder, 390, 296, ButtonStaffLog, "[STAFF] View Staff Log");
        DrawButton(ref builder, 390, 330, ButtonStaffClearLifetimeDeposits, "[STAFF] Clear Lifetime Deposits", HueWarn);
        DrawButton(ref builder, 390, 364, ButtonStaffSetNextCharge, "[STAFF] Set Next Charge", HueWarn);

        builder.AddLabel(46, 340, HueHeader, "Treasury Adjustment");
        builder.AddLabel(46, 370, HueText, "Amount:");
        builder.AddBackground(150, 366, 90, 24, 9350);
        builder.AddTextEntry(156, 369, 76, 18, 1153, EntryStaffTreasuryAdjustment, "0");
        DrawButton(ref builder, 260, 364, ButtonStaffApplyTreasury, "[STAFF] Apply", HueWarn);
        builder.AddLabel(46, 400, HueText, "Player Note:");
        builder.AddBackground(150, 396, 230, 24, 9350);
        builder.AddTextEntry(156, 399, 216, 18, 1153, EntryStaffTreasuryPlayerNote, "");
        builder.AddLabel(46, 430, HueText, "Staff Note:");
        builder.AddBackground(150, 426, 230, 24, 9350);
        builder.AddTextEntry(156, 429, 216, 18, 1153, EntryStaffTreasuryStaffNote, "");

        builder.AddLabel(390, 404, HueHeader, "Activity Adjustment");
        builder.AddLabel(390, 436, HueText, "Value:");
        builder.AddBackground(460, 432, 80, 24, 9350);
        builder.AddTextEntry(466, 435, 66, 18, 1153, EntryStaffActivity, "0");
        DrawButton(ref builder, 550, 426, ButtonStaffSetActivity, "[STAFF] Set", HueWarn);
        DrawButton(ref builder, 550, 458, ButtonStaffAddActivity, "[STAFF] Add", HueWarn);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || _township?.Guild?.Disbanded != false)
        {
            return;
        }

        if (!TownshipService.IsGuildMember(from, _township.Guild))
        {
            from.CloseGump<TownshipGump>();

            if (_township.IsDelinquent)
            {
                TownshipPublicDelinquencyGump.DisplayTo(from, _township);
            }
            else
            {
                from.SendMessage(0x22, "Only township guild members may view this township stone.");
            }

            return;
        }

        if (info.ButtonID >= ButtonRankBase && info.ButtonID < ButtonRankBase + (_township.Guild?.Members.Count ?? 0))
        {
            var member = _township.Guild.Members[info.ButtonID - ButtonRankBase];
            TownshipRankSelectGump.DisplayTo(from, _township, member, TownshipGumpView.Control);
            return;
        }

        if (info.ButtonID >= ButtonStaffRemoveServiceBase &&
            info.ButtonID < ButtonStaffRemoveServiceBase + _township.Services.Count)
        {
            if (!TownshipService.HasPermission(_township, from, TownshipPermission.RemoveServices))
            {
                from.SendMessage(0x22, "You do not have permission to remove township services.");
                DisplayTo(from, _township, TownshipGumpView.Services);
                return;
            }

            var serviceIndex = info.ButtonID - ButtonStaffRemoveServiceBase;
            var service = _township.Services[serviceIndex];
            from.SendGump(new TownshipServiceRemoveConfirmGump(_township, service.Id));
            return;
        }

        if (info.ButtonID >= ButtonTreasuryDetailsBase &&
            info.ButtonID < ButtonTreasuryDetailsBase + _township.DepositLog.Count)
        {
            var entry = _township.DepositLog[info.ButtonID - ButtonTreasuryDetailsBase];

            if (!string.IsNullOrWhiteSpace(entry.AggregateKey))
            {
                from.SendGump(new TownshipTreasuryContributionGump(_township, entry.AggregateKey, 0));
            }
            else
            {
                DisplayTo(from, _township, TownshipGumpView.DepositLog, _page);
            }

            return;
        }

        switch (info.ButtonID)
        {
            case ButtonOverview:
                DisplayTo(from, _township, TownshipGumpView.Overview);
                break;
            case ButtonTreasury:
                DisplayTo(from, _township, TownshipGumpView.Treasury);
                break;
            case ButtonDeposit:
                BeginDeposit(from, _township);
                break;
            case ButtonDepositLog:
                DisplayTo(from, _township, TownshipGumpView.DepositLog);
                break;
            case ButtonBorders:
                TownshipMarkerService.ToggleBorders(from, _township);
                DisplayTo(from, _township, _view);
                break;
            case ButtonFoundingPoint:
                TownshipMarkerService.ShowFoundingPoint(from, _township);
                DisplayTo(from, _township, _view);
                break;
            case ButtonExpand:
                TownshipExpansionTarget.Begin(from, _township);
                break;
            case ButtonUpkeep:
                DisplayTo(from, _township, TownshipGumpView.Upkeep);
                break;
            case ButtonServices:
                DisplayTo(from, _township, TownshipGumpView.Services);
                break;
            case ButtonPerks:
                DisplayTo(from, _township, TownshipGumpView.Perks);
                break;
            case ButtonDelinquencyDetails:
                DisplayTo(from, _township, TownshipGumpView.Delinquency);
                break;
            case ButtonActivity:
                DisplayTo(from, _township, TownshipGumpView.Activity);
                break;
            case ButtonControl:
                DisplayTo(from, _township, TownshipGumpView.Control);
                break;
            case ButtonStaffTools:
                DisplayTo(from, _township, TownshipGumpView.StaffTools);
                break;
            case ButtonStaffClearTreasury:
                from.SendGump(new TownshipStaffConfirmGump(_township, TownshipStaffAction.ClearTreasury));
                break;
            case ButtonStaffClearTreasuryLog:
                from.SendGump(new TownshipStaffConfirmGump(_township, TownshipStaffAction.ClearTreasuryLog));
                break;
            case ButtonStaffClearLifetimeDeposits:
                from.SendGump(new TownshipStaffConfirmGump(_township, TownshipStaffAction.ClearLifetimeDeposits));
                break;
            case ButtonStaffSetActivity:
                from.SendGump(new TownshipStaffConfirmGump(_township, TownshipStaffAction.SetActivity, ReadInt(info, EntryStaffActivity, 0)));
                break;
            case ButtonStaffAddActivity:
                from.SendGump(new TownshipStaffConfirmGump(_township, TownshipStaffAction.AddActivity, ReadInt(info, EntryStaffActivity, 0)));
                break;
            case ButtonPurchaseBanker:
                TownshipServicePlacementTarget.Begin(from, _township, TownshipPaidServiceType.Banker);
                break;
            case ButtonPurchaseMage:
                TownshipServicePlacementTarget.Begin(from, _township, TownshipPaidServiceType.Mage);
                break;
            case ButtonPurchaseAlchemist:
                TownshipServicePlacementTarget.Begin(from, _township, TownshipPaidServiceType.Alchemist);
                break;
            case ButtonPurchaseStablemaster:
                TownshipServicePlacementTarget.Begin(from, _township, TownshipPaidServiceType.Stablemaster);
                break;
            case ButtonPurchaseInnkeeper:
                TownshipServicePlacementTarget.Begin(from, _township, TownshipPaidServiceType.Innkeeper);
                break;
            case ButtonPurchaseGuardedTown:
                PurchasePerk(from, TownshipPaidServiceType.GuardedTown);
                break;
            case ButtonPurchaseHuntingTax:
                PurchasePerk(from, TownshipPaidServiceType.HuntingTax);
                break;
            case ButtonEnableGuardedTown:
                SetPerkEnabled(from, TownshipPaidServiceType.GuardedTown, true);
                break;
            case ButtonDisableGuardedTown:
                SetPerkEnabled(from, TownshipPaidServiceType.GuardedTown, false);
                break;
            case ButtonEnableHuntingTax:
                SetPerkEnabled(from, TownshipPaidServiceType.HuntingTax, true);
                break;
            case ButtonDisableHuntingTax:
                SetPerkEnabled(from, TownshipPaidServiceType.HuntingTax, false);
                break;
            case ButtonStaffSetNextCharge:
                from.SendMessage(0x35, "Enter next upkeep charge time in server time. Examples: 2026-06-18 18:00, 6/18/2026 6:00 PM, or now.");
                from.Prompt = new TownshipStaffNextChargePrompt(_township);
                break;
            case ButtonStaffSetDelinquency:
                from.SendMessage(0x35, "Enter the delinquent balance in gold. Use 0 to clear delinquency.");
                from.Prompt = new TownshipStaffDelinquencyPrompt(_township);
                break;
            case ButtonStaffAddService:
                if (!TownshipService.AddPaidService(
                    _township,
                    from,
                    TownshipPaidServiceType.Banker,
                    ReadString(info, EntryStaffServiceName),
                    ReadInt(info, EntryStaffServiceCost, 1000000),
                    ReadInt(info, EntryStaffServiceUpkeep, 50000),
                    "Staff placeholder service.",
                    out var addServiceReason
                ))
                {
                    from.SendMessage(0x22, addServiceReason);
                }
                else
                {
                    from.SendMessage(0x35, "Placeholder township service added.");
                }

                DisplayTo(from, _township, TownshipGumpView.Services);
                break;
            case ButtonStaffApplyTreasury:
                from.SendGump(new TownshipStaffTreasuryConfirmGump(
                    _township,
                    ReadInt(info, EntryStaffTreasuryAdjustment, 0),
                    ReadString(info, EntryStaffTreasuryPlayerNote),
                    ReadString(info, EntryStaffTreasuryStaffNote)
                ));
                break;
            case ButtonStaffRefreshRegion:
                if (!TownshipService.StaffRefreshTownship(_township, from, out var refreshReason))
                {
                    from.SendMessage(0x22, refreshReason);
                }
                else
                {
                    from.SendMessage(0x35, "Township region and active border viewers refreshed.");
                }

                DisplayTo(from, _township, TownshipGumpView.StaffTools);
                break;
            case ButtonStaffLog:
                TownshipStaffLogGump.DisplayTo(from, _township);
                break;
            case ButtonRankPermissions:
                from.SendGump(new TownshipRankPermissionsGump(_township, _view));
                break;
            case ButtonPreviousPage:
                DisplayTo(from, _township, _view, Math.Max(0, _page - 1));
                break;
            case ButtonNextPage:
                DisplayTo(from, _township, _view, _page + 1);
                break;
            case ButtonMoveStone:
                TownshipStone.BeginMove(from, _township);
                break;
            case ButtonRename:
                BeginRename(from, _township);
                break;
            case ButtonAbolish:
                from.SendGump(new TownshipAbolishConfirmGump(_township));
                break;
        }
    }

    private void PurchasePerk(Mobile from, TownshipPaidServiceType type)
    {
        if (!TownshipService.PurchasePaidService(_township, from, type, _township.FoundingPoint, _township.Map, out var reason))
        {
            from.SendMessage(0x22, reason);
        }
        else
        {
            from.SendMessage(0x35, $"{TownshipService.GetServiceDisplayName(type, false)} purchased.");
        }

        DisplayTo(from, _township, TownshipGumpView.Perks);
    }

    private void SetPerkEnabled(Mobile from, TownshipPaidServiceType type, bool enabled)
    {
        if (!TownshipService.SetPerkEnabled(_township, from, type, enabled, out var reason))
        {
            from.SendMessage(0x22, reason);
        }
        else
        {
            from.SendMessage(0x35, $"{TownshipService.GetServiceDisplayName(type, false)} {(enabled ? "enabled" : "disabled")}.");
        }

        DisplayTo(from, _township, TownshipGumpView.Perks);
    }

    private static void BeginRename(Mobile from, TownshipState township)
    {
        if (!TownshipService.HasPermission(township, from, TownshipPermission.RenameTownship))
        {
            from.SendMessage(0x22, "You do not have permission to rename this township.");
            return;
        }

        from.SendMessage(0x35, "Enter a new unique township name.");
        from.Prompt = new TownshipRenamePrompt(township);
    }

    public static void BeginDeposit(Mobile from, TownshipState township)
    {
        if (!TownshipService.IsGuildMember(from, township.Guild) && !township.IsDelinquent)
        {
            from.SendMessage(0x22, "Only guild members may deposit unless the township is delinquent.");
            return;
        }

        if (!TownshipService.IsGuildMember(from, township.Guild) && township.DelinquentBalance <= 0)
        {
            from.SendMessage(0x22, "This township no longer has a delinquent balance to donate toward.");
            return;
        }

        from.SendMessage(0x35, "Target gold or a bank check in your backpack to deposit it into the township treasury.");
        from.Target = new DepositTarget(township);
    }

    private sealed class DepositTarget : Target
    {
        private readonly TownshipState _township;

        public DepositTarget(TownshipState township) : base(-1, false, TargetFlags.None)
        {
            _township = township;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Item item || !item.IsChildOf(from.Backpack))
            {
                from.SendMessage(0x22, "You must target gold or a bank check in your backpack.");
                return;
            }

            var amount = item switch
            {
                Gold gold       => gold.Amount,
                BankCheck check => check.Worth,
                _               => 0
            };

            if (amount <= 0)
            {
                from.SendMessage(0x22, "That cannot be deposited into the township treasury.");
                return;
            }

            if (!TownshipService.IsGuildMember(from, _township.Guild))
            {
                if (!_township.IsDelinquent || _township.DelinquentBalance <= 0)
                {
                    from.SendMessage(0x22, "This township no longer has a delinquent balance to donate toward.");
                    return;
                }

                amount = Math.Min(amount, _township.DelinquentBalance);
            }

            from.SendGump(new TownshipDepositConfirmGump(_township, item, amount));
        }
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label)
    {
        DrawButton(ref builder, x, y, buttonId, label, HueText);
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

    private static void DrawGoldChange(ref DynamicGumpBuilder builder, int x, int y, int amount)
    {
        var hue = amount < 0 ? HueWarn : HueGold;
        var sign = amount < 0 ? "-" : amount > 0 ? "+" : "";
        builder.AddLabel(x, y, hue, $"{sign}{Math.Abs(amount):N0} gp");
    }

    private static string FormatDaysRemaining(double days)
    {
        if (double.IsPositiveInfinity(days))
        {
            return "No upkeep due";
        }

        return days < 1.0 ? "Less than 1 day" : $"{days:0.0} days";
    }

    private static TownshipDepositLogEntry GetLastUpkeepPayment(TownshipState township)
    {
        if (township == null)
        {
            return null;
        }

        for (var i = 0; i < township.DepositLog.Count; i++)
        {
            var entry = township.DepositLog[i];

            if (entry.Source == TownshipDepositSource.UpkeepPayment && entry.Amount < 0)
            {
                return entry;
            }
        }

        return null;
    }

    private static int GetDailyNpcServiceUpkeep(TownshipState township)
    {
        if (township == null)
        {
            return 0;
        }

        var total = 0;

        for (var i = 0; i < township.Services.Count; i++)
        {
            var service = township.Services[i];

            if (!TownshipService.IsPerkService(service.Type) &&
                service.Status is TownshipPaidServiceStatus.Active or TownshipPaidServiceStatus.Suspended)
            {
                total += Math.Max(0, service.DailyUpkeep);
            }
        }

        return total;
    }

    private static string FormatTreasuryActivityDetails(TownshipDepositLogEntry entry)
    {
        if (entry.AggregateCount > 0)
        {
            return $"{GetTreasuryActivitySourceName(entry.Source)} - {entry.AggregateCount:N0} contribution{(entry.AggregateCount == 1 ? "" : "s")}{GetTreasuryActivityNote(entry)}";
        }

        var source = GetTreasuryActivitySourceName(entry.Source);
        var note = GetTreasuryActivityNote(entry);
        var actor = entry.Source == TownshipDepositSource.StaffAdjustment || IsStaffForcedUpkeep(entry)
            ? "Staff member"
            : EscapeHtml(entry.PlayerName ?? "System");
        var action = entry.Source switch
        {
            TownshipDepositSource.StaffAdjustment => "adjusted treasury",
            TownshipDepositSource.UpkeepPayment => "paid upkeep",
            _ when entry.Amount < 0 => "withdrew",
            _ => "deposited"
        };

        return $"{actor} {action} - {source}{note}";
    }

    private static string GetTreasuryActivitySourceName(TownshipDepositSource source) => source switch
    {
        TownshipDepositSource.PlayerDeposit => "Manual gold deposit",
        TownshipDepositSource.EscortRevenue => "Escort revenue",
        TownshipDepositSource.VendorRevenue => "NPC revenue",
        TownshipDepositSource.StaffAdjustment => "Staff adjustment",
        TownshipDepositSource.UpkeepPayment => "Upkeep payment",
        TownshipDepositSource.ServiceRefund => "Service refund",
        TownshipDepositSource.ServicePurchase => "Service purchase",
        TownshipDepositSource.HuntingTax => "Hunting bonus",
        _ => "Treasury activity"
    };

    private static string GetTreasuryActivityNote(TownshipDepositLogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Note) || IsDefaultTreasuryActivityNote(entry.Source, entry.Note))
        {
            return "";
        }

        return $" - {EscapeHtml(entry.Note)}";
    }

    private static bool IsDefaultTreasuryActivityNote(TownshipDepositSource source, string note)
    {
        note = note.Trim();

        return source switch
        {
            TownshipDepositSource.PlayerDeposit => note.Equals("Player treasury deposit.", StringComparison.OrdinalIgnoreCase),
            TownshipDepositSource.UpkeepPayment =>
                note.Equals("Weekly upkeep payment.", StringComparison.OrdinalIgnoreCase) ||
                note.Equals("Weekly accrued upkeep payment.", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool IsStaffForcedUpkeep(TownshipDepositLogEntry entry) =>
        entry.Source == TownshipDepositSource.UpkeepPayment &&
        entry.Note?.StartsWith("Staff forced", StringComparison.OrdinalIgnoreCase) == true;

    private static string FormatSignedGold(int amount)
    {
        var sign = amount < 0 ? "-" : "+";
        return $"{sign}{Math.Abs(amount):N0} gp";
    }

    private static string EscapeHtml(string value) =>
        string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string GetShortId(TownshipState township) =>
        string.IsNullOrWhiteSpace(township?.Id) || township.Id.Length < 8 ? township?.Id ?? "unknown" : township.Id[..8];

    private static int ReadInt(RelayInfo info, int entryId, int fallback)
    {
        var text = info.GetTextEntry(entryId);
        return int.TryParse(text, out var value) ? value : fallback;
    }

    private static string ReadString(RelayInfo info, int entryId) => info.GetTextEntry(entryId)?.Trim() ?? "";
}

public sealed class TownshipFoundedAnnouncementGump : DynamicGump
{
    private const int Width = 420;
    private const int Height = 210;
    private const int HueTitle = 1153;
    private const int HueText = 2101;
    private const int HueGood = 68;

    private readonly TownshipState _township;

    public TownshipFoundedAnnouncementGump(TownshipState township) : base(180, 160)
    {
        _township = township;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, Width, Height, 9270);
        builder.AddAlphaRegion(12, 12, Width - 24, Height - 24);
        builder.AddLabel(28, 24, HueTitle, "A New Township Has Been Founded.");

        if (_township == null)
        {
            builder.AddHtml(28, 62, 360, 80, "The details have already faded from the town crier's notes.", "#D8D8D8");
            return;
        }

        var guild = _township.Guild;
        var guildName = guild?.Name ?? "Unknown";
        var guildmaster = guild?.Leader?.Name ?? "Unknown";
        var location = TownshipService.GetLocationDescription(_township);

        builder.AddLabel(28, 66, HueText, "Name:");
        builder.AddLabel(118, 66, HueGood, _township.Name ?? "Unknown");
        builder.AddLabel(28, 92, HueText, "Governor:");
        builder.AddLabel(118, 92, HueText, guildmaster);
        builder.AddLabel(28, 118, HueText, "Guild:");
        builder.AddLabel(118, 118, HueText, guildName);
        builder.AddLabel(28, 144, HueText, "Location:");
        builder.AddHtml(118, 144, 260, 42, location, "#D8D8D8");
    }
}

public sealed class TownshipTreasuryContributionGump : DynamicGump
{
    private const int ButtonBack = 1;
    private const int ButtonPrevious = 2;
    private const int ButtonNext = 3;
    private const int ButtonDetailBase = 1000;
    private const int Width = 640;
    private const int Height = 430;
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueGold = 53;

    private readonly TownshipState _township;
    private readonly string _aggregateKey;
    private readonly int _page;

    public override bool Singleton => true;

    public TownshipTreasuryContributionGump(TownshipState township, string aggregateKey, int page) : base(120, 90)
    {
        _township = township;
        _aggregateKey = aggregateKey;
        _page = Math.Max(0, page);
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, Width, Height, 9270);
        builder.AddAlphaRegion(15, 15, Width - 30, Height - 30);
        builder.AddLabel(36, 24, HueTitle, "Treasury Contribution Details");
        builder.AddLabel(36, 56, HueMuted, _township?.Name ?? "Township");
        DrawRule(ref builder, 32, 84, Width - 64);

        if (_township == null || string.IsNullOrWhiteSpace(_aggregateKey))
        {
            builder.AddLabel(36, 112, HueMuted, "No contribution details are available.");
            return;
        }

        const int pageSize = 5;
        var total = CountEntries();
        var maxPage = Math.Max(0, (total - 1) / pageSize);
        var page = Math.Clamp(_page, 0, maxPage);
        var start = page * pageSize;
        var y = 112;
        var visible = 0;

        if (total == 0)
        {
            builder.AddLabel(36, y, HueMuted, "No contribution details are available.");
        }
        else
        {
            for (var i = 0; i < _township.TreasuryContributions.Count && visible < pageSize; i++)
            {
                var entry = _township.TreasuryContributions[i];

                if (entry.AggregateKey != _aggregateKey)
                {
                    continue;
                }

                if (start > 0)
                {
                    start--;
                    continue;
                }

                builder.AddLabelCropped(36, y, 118, 22, HueText, $"{entry.Timestamp:g}");
                builder.AddLabelCropped(158, y, 86, 22, HueGold, $"+{entry.Amount:N0} gp");
                builder.AddHtml(
                    250,
                    y,
                    260,
                    34,
                    FormatContributionSummary(entry),
                    "#D8D8D8"
                );
                DrawButton(ref builder, 530, y, ButtonDetailBase + i, "View");

                y += 48;
                visible++;
            }

            DrawPageControls(ref builder, page, maxPage);
        }

        DrawButton(ref builder, 36, Height - 48, ButtonBack, "Back");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || _township == null)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case ButtonPrevious:
                from.SendGump(new TownshipTreasuryContributionGump(_township, _aggregateKey, Math.Max(0, _page - 1)));
                break;
            case ButtonNext:
                from.SendGump(new TownshipTreasuryContributionGump(_township, _aggregateKey, _page + 1));
                break;
            default:
                if (info.ButtonID >= ButtonDetailBase)
                {
                    var entryIndex = info.ButtonID - ButtonDetailBase;

                    if (entryIndex >= 0 && entryIndex < _township.TreasuryContributions.Count)
                    {
                        from.SendGump(new TownshipTreasuryContributionDetailGump(_township, _aggregateKey, _page, entryIndex));
                        break;
                    }
                }

                TownshipGump.DisplayTo(from, _township, TownshipGumpView.DepositLog);
                break;
        }
    }

    internal static string FormatContributionSummary(TownshipTreasuryContributionEntry entry) =>
        $"{EscapeHtml(entry.PlayerName ?? "System")} - {EscapeHtml(entry.Note)}";

    internal static string FormatContributionFullDetails(TownshipTreasuryContributionEntry entry)
    {
        var details = string.IsNullOrWhiteSpace(entry.Details) ? entry.Note : entry.Details;
        return EscapeHtml(details);
    }

    private int CountEntries()
    {
        var count = 0;

        for (var i = 0; i < _township.TreasuryContributions.Count; i++)
        {
            if (_township.TreasuryContributions[i].AggregateKey == _aggregateKey)
            {
                count++;
            }
        }

        return count;
    }

    private static void DrawPageControls(ref DynamicGumpBuilder builder, int page, int maxPage)
    {
        if (page > 0)
        {
            DrawButton(ref builder, 36, 352, ButtonPrevious, "Previous");
        }

        builder.AddLabel(270, 354, HueMuted, $"Page {page + 1:N0} of {maxPage + 1:N0}");

        if (page < maxPage)
        {
            DrawButton(ref builder, 456, 352, ButtonNext, "Next");
        }
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 32, y + 2, HueText, label);
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 9107);
    }

    private static string EscapeHtml(string value) =>
        string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
}

public sealed class TownshipTreasuryContributionDetailGump : DynamicGump
{
    private const int ButtonBack = 1;
    private const int Width = 640;
    private const int Height = 430;
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueGold = 53;

    private readonly TownshipState _township;
    private readonly string _aggregateKey;
    private readonly int _returnPage;
    private readonly int _entryIndex;

    public override bool Singleton => true;

    public TownshipTreasuryContributionDetailGump(
        TownshipState township,
        string aggregateKey,
        int returnPage,
        int entryIndex
    ) : base(120, 90)
    {
        _township = township;
        _aggregateKey = aggregateKey;
        _returnPage = Math.Max(0, returnPage);
        _entryIndex = entryIndex;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, Width, Height, 9270);
        builder.AddAlphaRegion(15, 15, Width - 30, Height - 30);
        builder.AddLabel(36, 24, HueTitle, "Treasury Contribution");
        builder.AddLabel(36, 56, HueMuted, _township?.Name ?? "Township");
        DrawRule(ref builder, 32, 84, Width - 64);

        var entry = GetEntry();

        if (entry == null)
        {
            builder.AddLabel(36, 112, HueMuted, "This contribution detail is no longer available.");
            DrawButton(ref builder, 36, Height - 48, ButtonBack, "Back");
            return;
        }

        builder.AddLabel(36, 112, HueHeader, $"{entry.Timestamp:g}");
        builder.AddLabel(180, 112, HueGold, $"+{entry.Amount:N0} gp");
        builder.AddLabel(36, 140, HueText, entry.PlayerName ?? "System");
        builder.AddHtml(
            36,
            176,
            Width - 72,
            182,
            TownshipTreasuryContributionGump.FormatContributionFullDetails(entry),
            "#D8D8D8",
            scrollbar: true
        );

        DrawButton(ref builder, 36, Height - 48, ButtonBack, "Back");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from != null && _township != null)
        {
            from.SendGump(new TownshipTreasuryContributionGump(_township, _aggregateKey, _returnPage));
        }
    }

    private TownshipTreasuryContributionEntry GetEntry()
    {
        if (_township == null || _entryIndex < 0 || _entryIndex >= _township.TreasuryContributions.Count)
        {
            return null;
        }

        var entry = _township.TreasuryContributions[_entryIndex];
        return entry.AggregateKey == _aggregateKey ? entry : null;
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 32, y + 2, HueText, label);
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 9107);
    }
}

public sealed class TownshipRankPermissionsGump : DynamicGump
{
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueGold = 53;

    private readonly TownshipState _township;
    private readonly TownshipGumpView _returnView;

    public TownshipRankPermissionsGump(TownshipState township, TownshipGumpView returnView) : base(160, 110)
    {
        _township = township;
        _returnView = returnView;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 560, 430, 9270);
        builder.AddAlphaRegion(15, 15, 530, 400);
        builder.AddLabel(36, 28, HueHeader, "Township Rank Permissions");
        builder.AddHtml(
            36,
            60,
            480,
            46,
            "All township guild members can view township information, treasury details, logs, upkeep, activity, services, perks, borders, founding point, and deposit to the treasury.",
            "#D8D8D8"
        );

        var y = 124;
        DrawRank(ref builder, y, "Citizen", TownshipService.GetRankPermissionSummary(TownshipRankLevel.Citizen), HueText);
        y += 42;
        DrawRank(ref builder, y, "Aide", TownshipService.GetRankPermissionSummary(TownshipRankLevel.Aide), HueText);
        y += 42;
        DrawRank(ref builder, y, "Officer", TownshipService.GetRankPermissionSummary(TownshipRankLevel.Officer), HueText);
        y += 42;
        DrawRank(ref builder, y, "Councilor", TownshipService.GetRankPermissionSummary(TownshipRankLevel.Councilor), HueText);
        y += 42;
        DrawRank(ref builder, y, "Regent", TownshipService.GetRankPermissionSummary(TownshipRankLevel.Regent), HueGold);
        y += 42;
        DrawRank(ref builder, y, "Governor", "Guildmaster-only. Full township control, including abolishing the township.", HueGold);

        builder.AddButton(36, 378, 4005, 4007, 1);
        builder.AddLabel(70, 380, HueText, "Back");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != null)
        {
            TownshipGump.DisplayTo(sender.Mobile, _township, _returnView);
        }
    }

    private static void DrawRank(ref DynamicGumpBuilder builder, int y, string rank, string summary, int hue)
    {
        builder.AddLabel(36, y, hue, rank);
        builder.AddHtml(136, y, 370, 38, summary, "#D8D8D8");
    }
}

public sealed class TownshipRankSelectGump : DynamicGump
{
    private const int ButtonRankBase = 100;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueGold = 53;

    private readonly TownshipState _township;
    private readonly Mobile _target;
    private readonly TownshipGumpView _returnView;

    private TownshipRankSelectGump(TownshipState township, Mobile target, TownshipGumpView returnView) : base(180, 130)
    {
        _township = township;
        _target = target;
        _returnView = returnView;
    }

    public static void DisplayTo(Mobile from, TownshipState township, Mobile target, TownshipGumpView returnView)
    {
        if (from?.NetState == null || township == null || target == null)
        {
            return;
        }

        if (!TownshipService.HasPermission(township, from, TownshipPermission.ManageRanks) &&
            !TownshipService.CanUseStaffTools(from))
        {
            from.SendMessage(0x22, "You do not have permission to manage township ranks.");
            TownshipGump.DisplayTo(from, township, returnView);
            return;
        }

        from.SendGump(new TownshipRankSelectGump(township, target, returnView));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 430, 310, 9270);
        builder.AddAlphaRegion(15, 15, 400, 280);
        builder.AddLabel(36, 28, HueHeader, "Set Township Rank");

        if (_target == null)
        {
            builder.AddLabel(36, 70, HueMuted, "That player is no longer available.");
            return;
        }

        builder.AddLabel(36, 66, HueText, "Player:");
        builder.AddLabel(130, 66, HueGold, _target.Name ?? "Unknown");
        builder.AddLabel(36, 92, HueText, "Current:");
        builder.AddLabel(130, 92, HueText, TownshipService.GetRankName(_township, _target));

        var y = 130;
        DrawRankButton(ref builder, y, TownshipRankLevel.Citizen);
        y += 28;
        DrawRankButton(ref builder, y, TownshipRankLevel.Aide);
        y += 28;
        DrawRankButton(ref builder, y, TownshipRankLevel.Officer);
        y += 28;
        DrawRankButton(ref builder, y, TownshipRankLevel.Councilor);
        y += 28;
        DrawRankButton(ref builder, y, TownshipRankLevel.Regent);

        builder.AddButton(248, 258, 4017, 4019, 0);
        builder.AddLabel(282, 260, HueText, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || _township == null)
        {
            return;
        }

        if (info.ButtonID < ButtonRankBase || info.ButtonID > ButtonRankBase + (int)TownshipRankLevel.Regent)
        {
            TownshipGump.DisplayTo(from, _township, _returnView);
            return;
        }

        var rank = (TownshipRankLevel)(info.ButtonID - ButtonRankBase);
        from.SendGump(new TownshipRankConfirmGump(_township, _target, rank, _returnView));
    }

    private static void DrawRankButton(ref DynamicGumpBuilder builder, int y, TownshipRankLevel rank)
    {
        builder.AddButton(42, y, 4005, 4007, ButtonRankBase + (int)rank);
        builder.AddLabel(76, y + 2, rank == TownshipRankLevel.Regent ? HueGold : HueText, rank.ToString());
    }
}

public sealed class TownshipRankConfirmGump : DynamicGump
{
    private const int ButtonConfirm = 1;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueWarn = 33;

    private readonly TownshipState _township;
    private readonly Mobile _target;
    private readonly TownshipRankLevel _rank;
    private readonly TownshipGumpView _returnView;

    public TownshipRankConfirmGump(TownshipState township, Mobile target, TownshipRankLevel rank, TownshipGumpView returnView) : base(190, 160)
    {
        _township = township;
        _target = target;
        _rank = rank;
        _returnView = returnView;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 430, 210, 9270);
        builder.AddAlphaRegion(15, 15, 400, 180);
        builder.AddLabel(36, 28, HueHeader, "Confirm Rank Change");
        builder.AddHtml(
            36,
            66,
            352,
            74,
            $"Set {_target?.Name ?? "Unknown"}'s township rank to {_rank}?<br>This changes their township management permissions.",
            "#D8D8D8"
        );
        builder.AddButton(72, 154, 4005, 4007, ButtonConfirm);
        builder.AddLabel(106, 156, HueWarn, "Confirm");
        builder.AddButton(248, 154, 4017, 4019, 0);
        builder.AddLabel(282, 156, HueText, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || _township == null)
        {
            return;
        }

        if (info.ButtonID == ButtonConfirm)
        {
            if (!TownshipService.SetRank(_township, from, _target, _rank, out var reason))
            {
                from.SendMessage(0x22, reason);
            }
            else
            {
                from.SendMessage(0x35, $"{_target.Name}'s township rank is now {_rank}.");
            }
        }

        TownshipGump.DisplayTo(from, _township, _returnView);
    }
}

public enum TownshipGumpView
{
    Overview,
    Treasury,
    DepositLog,
    Upkeep,
    Activity,
    Control,
    StaffTools,
    Delinquency,
    Services,
    Perks
}

public sealed class TownshipPublicDelinquencyGump : DynamicGump
{
    private const int ButtonDeposit = 1;

    private readonly Mobile _from;
    private readonly TownshipState _township;

    public override bool Singleton => true;

    private TownshipPublicDelinquencyGump(Mobile from, TownshipState township) : base(120, 90)
    {
        _from = from;
        _township = township;
    }

    public static void DisplayTo(Mobile from, TownshipState township)
    {
        if (from?.NetState == null || township?.IsDelinquent != true)
        {
            return;
        }

        from.CloseGump<TownshipPublicDelinquencyGump>();
        from.SendGump(new TownshipPublicDelinquencyGump(from, township));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var guild = _township.Guild;
        var guildmaster = guild?.Leader?.Name ?? "Unknown";

        builder.AddPage();
        builder.AddBackground(0, 0, 500, 300, 9270);
        builder.AddAlphaRegion(15, 15, 470, 270);
        builder.AddLabel(40, 28, 1153, _township.Name ?? "Township");
        builder.AddLabel(40, 70, 2101, "Guild:");
        builder.AddLabel(160, 70, 68, $"{guild?.Abbreviation ?? ""} - {guild?.Name ?? "Unknown"}");
        builder.AddLabel(40, 100, 2101, "Guildmaster:");
        builder.AddLabel(160, 100, 2101, guildmaster);
        builder.AddLabel(40, 144, 33, "Delinquent Balance:");
        builder.AddLabel(220, 144, 33, $"-{_township.DelinquentBalance:N0} gp");
        builder.AddHtml(
            40,
            176,
            400,
            42,
            "This township is delinquent. Public donations are capped to the remaining delinquent balance.",
            "#D8D8D8",
            scrollbar: false
        );
        DrawButton(ref builder, 40, 238, ButtonDeposit, "Donate Gold to Treasury", 33);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || info.ButtonID != ButtonDeposit)
        {
            return;
        }

        TownshipGump.BeginDeposit(from, _township);
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label, int hue)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 34, y + 2, hue, label);
    }
}

public enum TownshipStaffAction
{
    ClearTreasury,
    ClearTreasuryLog,
    ClearLifetimeDeposits,
    ClearActivityLog,
    SetActivity,
    AddActivity,
    SetDelinquency
}

public sealed class TownshipStaffConfirmGump : DynamicGump
{
    private const int ButtonConfirm = 1;

    private readonly TownshipState _township;
    private readonly TownshipStaffAction _action;
    private readonly int _value;
    private readonly bool _returnToStaffLog;
    private readonly int _returnPage;

    public TownshipStaffConfirmGump(
        TownshipState township,
        TownshipStaffAction action,
        int value = 0,
        bool returnToStaffLog = false,
        int returnPage = 0
    ) : base(180, 150)
    {
        _township = township;
        _action = action;
        _value = value;
        _returnToStaffLog = returnToStaffLog;
        _returnPage = Math.Max(0, returnPage);
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 480, 240, 9270);
        builder.AddAlphaRegion(15, 15, 450, 210);
        builder.AddLabel(40, 28, 33, "[STAFF] Confirm Township Action");
        builder.AddHtml(40, 62, 390, 92, GetMessage(), "#D8D8D8", scrollbar: false);
        builder.AddButton(80, 184, 4005, 4007, ButtonConfirm);
        builder.AddLabel(114, 186, 33, "Confirm");
        builder.AddButton(300, 184, 4017, 4019, 0);
        builder.AddLabel(334, 186, 2101, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null)
        {
            return;
        }

        if (info.ButtonID != ButtonConfirm)
        {
            ReturnToSource(from);
            return;
        }

        string reason;
        bool success;

        switch (_action)
        {
            case TownshipStaffAction.ClearTreasury:
                success = TownshipService.ClearTreasury(_township, from, out reason);
                break;
            case TownshipStaffAction.ClearTreasuryLog:
                success = TownshipService.ClearTreasuryLog(_township, from, out reason);
                break;
            case TownshipStaffAction.ClearLifetimeDeposits:
                success = TownshipService.ClearLifetimeDeposits(_township, from, out reason);
                break;
            case TownshipStaffAction.ClearActivityLog:
                success = TownshipService.ClearActivityLog(_township, from, out reason);
                break;
            case TownshipStaffAction.SetActivity:
                success = TownshipService.AdjustActivity(_township, from, _value, setValue: true, out reason);
                break;
            case TownshipStaffAction.AddActivity:
                success = TownshipService.AdjustActivity(_township, from, _value, setValue: false, out reason);
                break;
            case TownshipStaffAction.SetDelinquency:
                success = TownshipService.SetDelinquentBalance(_township, from, _value, out reason);
                break;
            default:
                success = Fail(out reason);
                break;
        }

        if (!success)
        {
            from.SendMessage(0x22, reason);
        }
        else
        {
            from.SendMessage(0x35, "Staff township action completed.");
        }

        ReturnToSource(from);
    }

    private void ReturnToSource(Mobile from)
    {
        if (_returnToStaffLog)
        {
            TownshipStaffLogGump.DisplayTo(from, _township, _returnPage);
            return;
        }

        TownshipGump.DisplayTo(from, _township, TownshipGumpView.StaffTools);
    }

    private string GetMessage() => _action switch
    {
        TownshipStaffAction.ClearTreasury =>
            $"Clear {_township.Name}'s treasury balance to 0 gp?<br>This is staff-only and will be logged.",
        TownshipStaffAction.ClearTreasuryLog =>
            $"Clear {_township.Name}'s player-facing treasury activity log?<br>This is staff-only and will be logged.",
        TownshipStaffAction.ClearLifetimeDeposits =>
            $"Clear {_township.Name}'s lifetime deposits total?<br>This does not remove treasury gold or treasury activity entries.",
        TownshipStaffAction.ClearActivityLog =>
            $"Clear {_township.Name}'s staff/activity log?<br>Only Owners can do this. A new staff log entry will remain documenting this action.",
        TownshipStaffAction.SetActivity =>
            $"Set {_township.Name}'s activity score to {Math.Clamp(_value, 0, 1000):N0}?<br>This is staff-only and will be logged.",
        TownshipStaffAction.AddActivity =>
            $"Add {_value:N0} activity to {_township.Name}?<br>The final score will be clamped between 0 and 1,000.",
        TownshipStaffAction.SetDelinquency =>
            _value <= 0
                ? $"Clear {_township.Name}'s delinquent balance and restore paid services?<br>This is staff-only and will be logged."
                : $"Set {_township.Name}'s delinquent balance to {_value:N0} gp?<br>Paid services will be suspended until this amount is paid.",
        _ => "Confirm this staff-only township action?"
    };

    private static bool Fail(out string reason)
    {
        reason = "Unknown staff action.";
        return false;
    }
}

public sealed class TownshipUpkeepWarningGump : DynamicGump
{
    private readonly TownshipState _township;

    public TownshipUpkeepWarningGump(TownshipState township) : base(190, 140)
    {
        _township = township;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var daily = TownshipService.GetDailyUpkeep(_township);
        var days = TownshipService.GetEstimatedUpkeepDaysRemaining(_township);

        builder.AddPage();
        builder.AddBackground(0, 0, 440, 290, 9270);
        builder.AddAlphaRegion(15, 15, 410, 260);
        builder.AddLabel(40, 28, 33, "Township Upkeep Warning");
        builder.AddHtml(40, 62, 360, 156,
            $"{_township.Name} has less than 3 days of upkeep remaining.<br>Treasury: {_township.TreasuryBalance:N0} gp<br>Accrued due: -{_township.AccruedUpkeepDue:N0} gp<br>Daily assessment: -{daily:N0} gp<br>Estimated remaining: {FormatDaysRemaining(days)}<br>Next weekly payment: {_township.NextWeeklyPayment:g}<br>Service removals may begin if upkeep cannot be paid.",
            "#D8D8D8",
            scrollbar: false
        );
        builder.AddButton(178, 234, 4017, 4019, 0);
        builder.AddLabel(212, 236, 2101, "Close");
    }

    private static string FormatDaysRemaining(double days)
    {
        if (double.IsPositiveInfinity(days))
        {
            return "No upkeep due";
        }

        return days < 1.0 ? "Less than 1 day" : $"{days:0.0} days";
    }
}

public sealed class TownshipDelinquencyWarningGump : DynamicGump
{
    private readonly TownshipState _township;

    public TownshipDelinquencyWarningGump(TownshipState township) : base(190, 140)
    {
        _township = township;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var plan = TownshipService.GetDelinquencyPlan(_township);

        builder.AddPage();
        builder.AddBackground(0, 0, 460, 290, 9270);
        builder.AddAlphaRegion(15, 15, 430, 260);
        builder.AddLabel(40, 28, 33, "Township Delinquency Warning");

        if (plan == null)
        {
            builder.AddHtml(40, 62, 380, 110, "This township is no longer delinquent.", "#D8D8D8", scrollbar: false);
        }
        else
        {
            builder.AddHtml(
                40,
                62,
                380,
                140,
                $"{_township.Name} is delinquent.<br>Paid services are suspended.<br>Delinquent balance: -{plan.DelinquentBalance:N0} gp<br>Accrued this week: -{plan.AccruedUpkeepDue:N0} gp<br>Removal checks begin: {(plan.NextRemovalCheck == DateTime.MinValue ? "Not scheduled" : plan.NextRemovalCheck.ToString("g"))}<br>Deposit at the townstone treasury to restore services.",
                "#D8D8D8",
                scrollbar: false
            );
        }

        builder.AddButton(188, 234, 4017, 4019, 0);
        builder.AddLabel(222, 236, 2101, "Close");
    }
}

public sealed class TownshipStaffTreasuryConfirmGump : DynamicGump
{
    private const int ButtonConfirm = 1;

    private readonly TownshipState _township;
    private readonly int _delta;
    private readonly string _playerNote;
    private readonly string _staffNote;

    public TownshipStaffTreasuryConfirmGump(TownshipState township, int delta, string playerNote, string staffNote) : base(180, 150)
    {
        _township = township;
        _delta = delta;
        _playerNote = playerNote;
        _staffNote = staffNote;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 520, 260, 9270);
        builder.AddAlphaRegion(15, 15, 490, 230);
        builder.AddLabel(40, 28, 33, "[STAFF] Confirm Treasury Adjustment");
        builder.AddHtml(
            40,
            62,
            430,
            116,
            $"Adjust {_township.Name}'s treasury by {_delta:N0} gp?<br>Current Balance: {_township.TreasuryBalance:N0} gp<br>New Balance: {Math.Max(0, _township.TreasuryBalance + _delta):N0} gp<br>Player Note: {SafeNote(_playerNote)}<br>Staff Note: {SafeNote(_staffNote)}",
            "#D8D8D8",
            scrollbar: false
        );
        builder.AddButton(92, 204, 4005, 4007, ButtonConfirm);
        builder.AddLabel(126, 206, 33, "Confirm");
        builder.AddButton(320, 204, 4017, 4019, 0);
        builder.AddLabel(354, 206, 2101, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null)
        {
            return;
        }

        if (info.ButtonID != ButtonConfirm)
        {
            TownshipGump.DisplayTo(from, _township, TownshipGumpView.StaffTools);
            return;
        }

        if (!TownshipService.AdjustTreasury(_township, from, _delta, _playerNote, _staffNote, out var reason))
        {
            from.SendMessage(0x22, reason);
        }
        else
        {
            from.SendMessage(0x35, "Treasury adjusted.");
        }

        TownshipGump.DisplayTo(from, _township, TownshipGumpView.StaffTools);
    }

    private static string SafeNote(string note) => string.IsNullOrWhiteSpace(note) ? "No note provided." : note.Trim();
}

public sealed class TownshipServiceRemoveConfirmGump : DynamicGump
{
    private const int ButtonConfirm = 1;

    private readonly TownshipState _township;
    private readonly string _serviceId;

    public TownshipServiceRemoveConfirmGump(TownshipState township, string serviceId) : base(180, 150)
    {
        _township = township;
        _serviceId = serviceId;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var service = TownshipService.FindPaidService(_township, _serviceId);

        builder.AddPage();
        builder.AddBackground(0, 0, 540, 300, 9270);
        builder.AddAlphaRegion(15, 15, 510, 270);
        builder.AddLabel(40, 28, 33, "Remove Township Service");

        if (service == null)
        {
            builder.AddHtml(40, 64, 440, 90, "That township service no longer exists.", "#D8D8D8", scrollbar: false);
            builder.AddButton(214, 238, 4017, 4019, 0);
            builder.AddLabel(248, 240, 2101, "Close");
            return;
        }

        if (service.Status == TownshipPaidServiceStatus.Removed)
        {
            builder.AddHtml(40, 64, 440, 90, "That township service has already been removed.", "#D8D8D8", scrollbar: false);
            builder.AddButton(214, 238, 4017, 4019, 0);
            builder.AddLabel(248, 240, 2101, "Close");
            return;
        }

        var refund = TownshipService.CalculateServiceRefund(service, delinquencyRemoval: false);
        builder.AddHtml(
            40,
            64,
            440,
            130,
            $"Remove {EscapeHtml(service.Name)} ({service.Type})?<br>Status: {service.Status}<br>Purchase Cost: {service.PurchaseCost:N0} gp<br>Daily Upkeep Removed: -{service.DailyUpkeep:N0} gp<br>Voluntary Refund: +{refund.RefundAmount:N0} gp<br>The refund is deposited into the township treasury.",
            "#D8D8D8",
            scrollbar: false
        );
        builder.AddButton(88, 238, 4005, 4007, ButtonConfirm);
        builder.AddLabel(122, 240, 33, "Remove");
        builder.AddButton(316, 238, 4017, 4019, 0);
        builder.AddLabel(350, 240, 2101, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null)
        {
            return;
        }

        if (info.ButtonID != ButtonConfirm)
        {
            TownshipGump.DisplayTo(from, _township, TownshipGumpView.Services);
            return;
        }

        if (!TownshipService.RemovePaidService(_township, from, _serviceId, out var refund, out var reason))
        {
            from.SendMessage(0x22, reason);
        }
        else
        {
            from.SendMessage(0x35, $"Township service removed. Refund applied: {refund.RefundAmount:N0} gp.");
        }

        TownshipGump.DisplayTo(from, _township, TownshipGumpView.Services);
    }

    private static string EscapeHtml(string value) =>
        string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
}

public sealed class TownshipStaffLogGump : DynamicGump
{
    private const int ButtonBack = 1;
    private const int ButtonPreviousPage = 2;
    private const int ButtonNextPage = 3;
    private const int ButtonClearStaffLog = 4;
    private const int PageSize = 6;

    private readonly Mobile _from;
    private readonly TownshipState _township;
    private readonly int _page;

    public override bool Singleton => true;

    private TownshipStaffLogGump(Mobile from, TownshipState township, int page) : base(90, 70)
    {
        _from = from;
        _township = township;
        _page = page;
    }

    public static void DisplayTo(Mobile from, TownshipState township, int page = 0)
    {
        if (from?.NetState == null || township == null)
        {
            return;
        }

        if (!TownshipService.CanUseStaffTools(from))
        {
            from.SendMessage(0x22, "This township log is staff-only.");
            return;
        }

        from.CloseGump<TownshipStaffLogGump>();
        from.SendGump(new TownshipStaffLogGump(from, township, Math.Max(0, page)));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 760, 620, 9270);
        builder.AddAlphaRegion(15, 15, 730, 590);
        builder.AddLabel(300, 24, 33, "[STAFF] Township Log");
        DrawRule(ref builder, 34, 54, 692);
        builder.AddLabelCropped(46, 82, 620, 22, 2213, $"{_township.Name} ({GetShortId(_township)})");

        var total = _township.ActivityLog.Count;
        var maxPage = Math.Max(0, (total - 1) / PageSize);
        var page = Math.Clamp(_page, 0, maxPage);
        var start = page * PageSize;
        var max = Math.Min(PageSize, total - start);
        var y = 120;

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

        DrawRule(ref builder, 34, 520, 692);
        DrawButton(ref builder, 46, 544, ButtonBack, "Back to Staff Tools");

        if (_from.AccessLevel >= AccessLevel.Owner)
        {
            DrawButton(ref builder, 230, 544, ButtonClearStaffLog, "[OWNER] Clear Staff Log", 33);
        }

        if (page > 0)
        {
            DrawButton(ref builder, 420, 544, ButtonPreviousPage, "Previous");
        }

        builder.AddLabel(540, 546, 2401, $"Page {page + 1:N0} of {maxPage + 1:N0}");

        if (page < maxPage)
        {
            DrawButton(ref builder, 650, 544, ButtonNextPage, "Next");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || !TownshipService.CanUseStaffTools(from))
        {
            return;
        }

        switch (info.ButtonID)
        {
            case ButtonBack:
                TownshipGump.DisplayTo(from, _township, TownshipGumpView.StaffTools);
                break;
            case ButtonPreviousPage:
                DisplayTo(from, _township, Math.Max(0, _page - 1));
                break;
            case ButtonNextPage:
                DisplayTo(from, _township, _page + 1);
                break;
            case ButtonClearStaffLog:
                from.SendGump(new TownshipStaffConfirmGump(
                    _township,
                    TownshipStaffAction.ClearActivityLog,
                    returnToStaffLog: true,
                    returnPage: _page
                ));
                break;
        }
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label, int hue = 2101)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 34, y + 2, hue, label);
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }

    private static string GetShortId(TownshipState township) =>
        string.IsNullOrWhiteSpace(township?.Id) || township.Id.Length < 8 ? township?.Id ?? "unknown" : township.Id[..8];
}

public sealed class TownshipRenamePrompt : Prompt
{
    private readonly TownshipState _township;

    public TownshipRenamePrompt(TownshipState township)
    {
        _township = township;
    }

    public override void OnResponse(Mobile from, string text)
    {
        if (!TownshipService.RenameTownship(_township, from, text, out var reason))
        {
            from.SendMessage(0x22, reason);
        }
        else
        {
            from.SendMessage(0x35, $"The township is now named {_township.Name}.");
        }

        TownshipGump.DisplayTo(from, _township);
    }

    public override void OnCancel(Mobile from)
    {
        TownshipGump.DisplayTo(from, _township);
    }
}

public sealed class TownshipStaffNextChargePrompt : Prompt
{
    private readonly TownshipState _township;

    public TownshipStaffNextChargePrompt(TownshipState township)
    {
        _township = township;
    }

    public override void OnResponse(Mobile from, string text)
    {
        if (from == null)
        {
            return;
        }

        if (!TryParseChargeTime(text, out var nextCharge))
        {
            from.SendMessage(0x22, "Enter a valid server date/time, such as 2026-06-18 18:00, 6/18/2026 6:00 PM, or now.");
            TownshipGump.DisplayTo(from, _township, TownshipGumpView.StaffTools);
            return;
        }

        if (!TownshipService.SetNextUpkeepCharge(_township, from, nextCharge, out var reason))
        {
            from.SendMessage(0x22, reason);
        }
        else
        {
            from.SendMessage(0x35, $"Next township upkeep charge set to {nextCharge:g}.");
        }

        TownshipGump.DisplayTo(from, _township, TownshipGumpView.StaffTools);
    }

    public override void OnCancel(Mobile from)
    {
        if (from?.NetState != null)
        {
            TownshipGump.DisplayTo(from, _township, TownshipGumpView.StaffTools);
        }
    }

    private static bool TryParseChargeTime(string text, out DateTime nextCharge)
    {
        text = text?.Trim();

        if (text?.Equals("now", StringComparison.OrdinalIgnoreCase) == true)
        {
            nextCharge = Core.Now;
            return true;
        }

        return DateTime.TryParse(text, out nextCharge);
    }
}

public sealed class TownshipStaffDelinquencyPrompt : Prompt
{
    private readonly TownshipState _township;

    public TownshipStaffDelinquencyPrompt(TownshipState township)
    {
        _township = township;
    }

    public override void OnResponse(Mobile from, string text)
    {
        if (from == null)
        {
            return;
        }

        if (!int.TryParse(text?.Trim(), out var amount) || amount < 0)
        {
            from.SendMessage(0x22, "Enter a whole gold amount of 0 or greater.");
            TownshipGump.DisplayTo(from, _township, TownshipGumpView.StaffTools);
            return;
        }

        from.SendGump(new TownshipStaffConfirmGump(_township, TownshipStaffAction.SetDelinquency, amount));
    }

    public override void OnCancel(Mobile from)
    {
        if (from?.NetState != null)
        {
            TownshipGump.DisplayTo(from, _township, TownshipGumpView.StaffTools);
        }
    }
}

public sealed class TownshipNpcManagementGump : DynamicGump
{
    private const int ButtonRename = 1;
    private const int ButtonRoam = 2;
    private const int ButtonMove = 3;
    private const int ButtonGender = 4;
    private const int ButtonCustomize = 5;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueGood = 68;
    private const int HueWarn = 33;

    private readonly ITownshipServiceNpc _npc;

    public TownshipNpcManagementGump(ITownshipServiceNpc npc) : base(180, 140)
    {
        _npc = npc;
    }

    public static void DisplayTo(Mobile from, TownshipState township, string serviceId)
    {
        var service = TownshipService.FindPaidService(township, serviceId);

        if (service?.CreatedObjectSerial != Serial.Zero &&
            World.FindMobile(service.CreatedObjectSerial) is ITownshipServiceNpc npc &&
            npc is Mobile mobile &&
            !mobile.Deleted)
        {
            from.SendGump(new TownshipNpcManagementGump(npc));
            return;
        }

        from.SendMessage(0x22, "That township service NPC is no longer available.");
        TownshipGump.DisplayTo(from, township, TownshipGumpView.Services);
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 460, 332, 9270);
        builder.AddAlphaRegion(15, 15, 430, 302);

        var mobile = _npc as Mobile;
        var township = _npc?.Township;
        var service = _npc?.Service;

        builder.AddLabel(36, 28, HueHeader, "Township NPC Customization");

        if (mobile?.Deleted != false || service == null || township == null)
        {
            builder.AddHtml(36, 68, 380, 60, "This township NPC is no longer available.", "#D8D8D8", scrollbar: false);
            return;
        }

        builder.AddLabel(36, 66, HueText, "Name:");
        builder.AddLabelCropped(138, 66, 240, 22, HueGood, mobile.Name ?? service.Name ?? "Township NPC");
        builder.AddLabel(36, 94, HueText, "Service:");
        builder.AddLabel(138, 94, HueText, $"{service.Type} ({service.Status})");
        builder.AddLabel(36, 122, HueText, "Roam Range:");
        builder.AddLabel(138, 122, HueText, $"{service.RoamRange:N0}");
        builder.AddLabel(36, 150, HueText, "Gender:");
        builder.AddLabel(138, 150, HueText, mobile.Female ? "Female" : "Male");
        builder.AddLabel(36, 178, HueText, "Anchor House:");
        builder.AddLabel(138, 178, HueMuted, service.AnchorHouseSerial == Serial.Zero ? "None" : service.AnchorHouseSerial.ToString());

        DrawButton(ref builder, 42, 224, ButtonRename, "Rename NPC");
        DrawButton(ref builder, 156, 224, ButtonRoam, "Set Roam");
        DrawButton(ref builder, 284, 224, ButtonMove, "Move");
        DrawButton(ref builder, 42, 256, ButtonGender, mobile.Female ? "Set Male" : "Set Female", HueWarn);
        DrawButton(ref builder, 156, 256, ButtonCustomize, "Customize Appearance");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || _npc is not Mobile mobile || mobile.Deleted)
        {
            return;
        }

        var township = _npc.Township;
        var service = _npc.Service;

        if (!TownshipService.CanManageTownship(township, from) || service == null)
        {
            from.SendMessage(0x22, "You do not have permission to modify this township NPC.");
            return;
        }

        switch (info.ButtonID)
        {
            case ButtonRename:
                from.SendMessage(0x35, "Enter a new name for this township NPC.");
                from.Prompt = new TownshipNpcRenamePrompt(township, service.Id);
                break;
            case ButtonRoam:
                from.SendMessage(0x35, $"Enter roam range from 0 to {TownshipService.MaxTownshipNpcRoamRange}. Use 0 to keep the NPC stationary.");
                from.Prompt = new TownshipNpcRoamPrompt(township, service.Id);
                break;
            case ButtonMove:
                TownshipServiceRelocationTarget.Begin(from, township, service.Id);
                break;
            case ButtonGender:
                if (!TownshipService.SetPaidServiceNpcGender(township, from, service.Id, !mobile.Female, out var reason))
                {
                    from.SendMessage(0x22, reason);
                }
                else
                {
                    from.SendMessage(0x35, "Township NPC gender updated.");
                }

                DisplayTo(from, township, service.Id);
                break;
            case ButtonCustomize:
                PlayerVendorCustomizeGump.DisplayTo(from, mobile);
                break;
        }
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label, int hue = HueText)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 34, y + 2, hue, label);
    }
}

public sealed class TownshipNpcRenamePrompt : Prompt
{
    private readonly TownshipState _township;
    private readonly string _serviceId;

    public TownshipNpcRenamePrompt(TownshipState township, string serviceId)
    {
        _township = township;
        _serviceId = serviceId;
    }

    public override void OnResponse(Mobile from, string text)
    {
        if (from == null)
        {
            return;
        }

        if (!TownshipService.RenamePaidServiceNpc(_township, from, _serviceId, text, out var reason))
        {
            from.SendMessage(0x22, reason);
        }
        else
        {
            from.SendMessage(0x35, "Township NPC renamed.");
        }

        TownshipNpcManagementGump.DisplayTo(from, _township, _serviceId);
    }

    public override void OnCancel(Mobile from)
    {
        if (from?.NetState != null)
        {
            TownshipNpcManagementGump.DisplayTo(from, _township, _serviceId);
        }
    }
}

public sealed class TownshipNpcRoamPrompt : Prompt
{
    private readonly TownshipState _township;
    private readonly string _serviceId;

    public TownshipNpcRoamPrompt(TownshipState township, string serviceId)
    {
        _township = township;
        _serviceId = serviceId;
    }

    public override void OnResponse(Mobile from, string text)
    {
        if (from == null)
        {
            return;
        }

        if (!int.TryParse(text?.Trim(), out var range))
        {
            from.SendMessage(0x22, $"Enter a number from 0 to {TownshipService.MaxTownshipNpcRoamRange}.");
            TownshipNpcManagementGump.DisplayTo(from, _township, _serviceId);
            return;
        }

        if (!TownshipService.SetServiceRoamRange(_township, from, _serviceId, range, out var reason))
        {
            from.SendMessage(0x22, reason);
        }
        else
        {
            from.SendMessage(0x35, $"Township NPC roam range set to {Math.Clamp(range, 0, TownshipService.MaxTownshipNpcRoamRange):N0}.");
        }

        TownshipNpcManagementGump.DisplayTo(from, _township, _serviceId);
    }

    public override void OnCancel(Mobile from)
    {
        if (from?.NetState != null)
        {
            TownshipNpcManagementGump.DisplayTo(from, _township, _serviceId);
        }
    }
}

public sealed class TownshipAbolishConfirmGump : DynamicGump
{
    private readonly TownshipState _township;

    public TownshipAbolishConfirmGump(TownshipState township) : base(180, 150)
    {
        _township = township;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 430, 210, 9270);
        builder.AddAlphaRegion(15, 15, 400, 180);
        builder.AddLabel(40, 28, 33, "Abolish Township");
        builder.AddHtml(40, 62, 350, 78,
            $"Abolish {_township.Name}?<br>This removes the township, its claims, border views, and township-created objects.",
            "#D8D8D8",
            scrollbar: false
        );
        builder.AddButton(72, 160, 4005, 4007, 1);
        builder.AddLabel(106, 162, 33, "Abolish");
        builder.AddButton(250, 160, 4017, 4019, 0);
        builder.AddLabel(284, 162, 2101, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || info.ButtonID != 1)
        {
            return;
        }

        if (!TownshipService.AbolishTownship(_township, from, out var reason))
        {
            from.SendMessage(0x22, reason);
            TownshipGump.DisplayTo(from, _township);
            return;
        }

        from.SendMessage(0x35, "The township has been abolished.");
    }
}

public sealed class TownshipDepositConfirmGump : DynamicGump
{
    private readonly TownshipState _township;
    private readonly Item _item;
    private readonly int _amount;

    public TownshipDepositConfirmGump(TownshipState township, Item item, int amount) : base(180, 160)
    {
        _township = township;
        _item = item;
        _amount = amount;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 420, 190, 9270);
        builder.AddAlphaRegion(15, 15, 390, 160);
        builder.AddLabel(40, 28, 1153, "Confirm Treasury Deposit");
        builder.AddHtml(40, 62, 340, 70,
            $"Deposit {_amount:N0} gold into {_township.Name}?<br>Deposits cannot be withdrawn.",
            "#D8D8D8",
            scrollbar: false
        );
        builder.AddButton(72, 142, 4005, 4007, 1);
        builder.AddLabel(106, 144, 2101, "Confirm");
        builder.AddButton(240, 142, 4017, 4019, 0);
        builder.AddLabel(274, 144, 2101, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (info.ButtonID != 1 || from == null || _item?.Deleted != false || !_item.IsChildOf(from.Backpack))
        {
            return;
        }

        var currentAmount = _item switch
        {
            Gold gold       => gold.Amount,
            BankCheck check => check.Worth,
            _               => 0
        };

        var depositAmount = _amount;

        if (!TownshipService.IsGuildMember(from, _township.Guild))
        {
            if (!_township.IsDelinquent || _township.DelinquentBalance <= 0)
            {
                from.SendMessage(0x22, "This township no longer has a delinquent balance to donate toward.");
                TownshipPublicDelinquencyGump.DisplayTo(from, _township);
                return;
            }

            depositAmount = Math.Min(depositAmount, _township.DelinquentBalance);
        }

        if (currentAmount < depositAmount)
        {
            from.SendMessage(0x22, "That deposit amount has changed. Please start the deposit again.");
            DisplayAfterDeposit(from, _township);
            return;
        }

        if (_item is Gold depositGold)
        {
            depositGold.Consume(depositAmount);
        }
        else if (_item is BankCheck check)
        {
            if (check.Worth <= depositAmount)
            {
                check.Delete();
            }
            else
            {
                check.Worth -= depositAmount;
            }
        }
        else
        {
            from.SendMessage(0x22, "That item can no longer be deposited.");
            return;
        }

        TownshipService.Deposit(_township, from, depositAmount, TownshipDepositSource.PlayerDeposit, "Player treasury deposit.");
        from.SendMessage(0x35, $"You deposit {depositAmount:N0} gold into {_township.Name}'s treasury.");
        DisplayAfterDeposit(from, _township);
    }

    private static void DisplayAfterDeposit(Mobile from, TownshipState township)
    {
        if (TownshipService.IsGuildMember(from, township.Guild))
        {
            TownshipGump.DisplayTo(from, township, TownshipGumpView.Treasury);
            return;
        }

        TownshipPublicDelinquencyGump.DisplayTo(from, township);
    }
}

public sealed class TownshipServicePlacementTarget : Target
{
    private readonly TownshipState _township;
    private readonly TownshipPaidServiceType _type;

    private TownshipServicePlacementTarget(TownshipState township, TownshipPaidServiceType type) : base(-1, true, TargetFlags.None)
    {
        _township = township;
        _type = type;
    }

    public static void Begin(Mobile from, TownshipState township, TownshipPaidServiceType type)
    {
        var permission = TownshipService.IsPerkService(type)
            ? TownshipPermission.PurchasePerks
            : TownshipPermission.PurchaseServices;

        if (!TownshipService.HasPermission(township, from, permission))
        {
            from.SendMessage(0x22, "You do not have permission to purchase township services.");
            return;
        }

        from.SendMessage(0x35, $"Target a location inside claimed township land for the {TownshipService.GetServiceDisplayName(type, false)}.");
        from.Target = new TownshipServicePlacementTarget(township, type);
    }

    protected override void OnTarget(Mobile from, object targeted)
    {
        if (targeted is not IPoint3D point)
        {
            from.SendMessage(0x22, "That is not a valid service placement point.");
            TownshipGump.DisplayTo(from, _township, TownshipGumpView.Services);
            return;
        }

        var location = new Point3D(point);

        if (!from.InRange(location, 3))
        {
            from.SendLocalizedMessage(500446);
            TownshipGump.DisplayTo(from, _township, TownshipGumpView.Services);
            return;
        }

        var map = from.Map;
        string reason;
        var success = TownshipService.PurchasePaidService(_township, from, _type, location, map, out reason);

        if (!success)
        {
            from.SendMessage(0x22, reason);
        }
        else
        {
            from.SendMessage(0x35, "Township service purchased and placed.");
        }

        TownshipGump.DisplayTo(from, _township, TownshipGumpView.Services);
    }

    protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
    {
        TownshipGump.DisplayTo(from, _township, TownshipGumpView.Services);
    }
}

public sealed class TownshipServiceRelocationTarget : Target
{
    private readonly TownshipState _township;
    private readonly string _serviceId;

    private TownshipServiceRelocationTarget(TownshipState township, string serviceId) : base(-1, true, TargetFlags.None)
    {
        _township = township;
        _serviceId = serviceId;
    }

    public static void Begin(Mobile from, TownshipState township, string serviceId)
    {
        if (!TownshipService.CanManageTownship(township, from))
        {
            from.SendMessage(0x22, "You do not have permission to modify township services.");
            return;
        }

        from.SendMessage(0x35, "Target the new location inside a compatible township house.");
        from.Target = new TownshipServiceRelocationTarget(township, serviceId);
    }

    protected override void OnTarget(Mobile from, object targeted)
    {
        if (targeted is not IPoint3D point)
        {
            from.SendMessage(0x22, "That is not a valid service relocation point.");
            TownshipNpcManagementGump.DisplayTo(from, _township, _serviceId);
            return;
        }

        var location = new Point3D(point);

        if (!from.InRange(location, 3))
        {
            from.SendLocalizedMessage(500446);
            TownshipNpcManagementGump.DisplayTo(from, _township, _serviceId);
            return;
        }

        if (!TownshipService.RelocatePaidServiceNpc(_township, from, _serviceId, location, from.Map, out var reason))
        {
            from.SendMessage(0x22, reason);
        }
        else
        {
            from.SendMessage(0x35, "Township service NPC moved.");
        }

        TownshipNpcManagementGump.DisplayTo(from, _township, _serviceId);
    }

    protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
    {
        TownshipNpcManagementGump.DisplayTo(from, _township, _serviceId);
    }
}

public sealed class TownshipExpansionTarget : Target
{
    private readonly TownshipState _township;
    private readonly Point3D? _first;

    private TownshipExpansionTarget(TownshipState township, Point3D? first) : base(-1, true, TargetFlags.None)
    {
        _township = township;
        _first = first;
    }

    public static void Begin(Mobile from, TownshipState township)
    {
        if (!TownshipService.HasPermission(township, from, TownshipPermission.ExpandTerritory))
        {
            from.SendMessage(0x22, "You do not have permission to expand this township.");
            return;
        }

        TownshipMarkerService.ShowExpansionPreview(from, township, new TownshipExpansionPreview
        {
            RequestedArea = TownshipService.GetEnvelope(township),
            InsideEnvelope = true
        });
        from.SendMessage(0x35, "Target the first corner of the expansion rectangle.");
        from.Target = new TownshipExpansionTarget(township, null);
    }

    protected override void OnTarget(Mobile from, object targeted)
    {
        if (targeted is not IPoint3D point)
        {
            from.SendMessage(0x22, "That is not a valid point.");
            TownshipMarkerService.ClearExpansionPreview(from);
            TownshipGump.DisplayTo(from, _township);
            return;
        }

        var p = new Point3D(point);

        if (_first == null)
        {
            from.SendMessage(0x35, "Target the opposite corner of the expansion rectangle.");
            from.Target = new TownshipExpansionTarget(_township, p);
            return;
        }

        var first = _first.Value;
        var x = Math.Min(first.X, p.X);
        var y = Math.Min(first.Y, p.Y);
        var width = Math.Abs(first.X - p.X) + 1;
        var height = Math.Abs(first.Y - p.Y) + 1;
        var rect = new Rectangle2D(x, y, width, height);
        var preview = TownshipService.PreviewExpansion(_township, rect);

        TownshipMarkerService.ShowExpansionPreview(from, _township, preview);

        if (preview.ValidTiles > 0 && preview.InsideEnvelope && !preview.MeetsEdgeRequirement)
        {
            from.SendGump(new TownshipExpansionRetryGump(_township, preview));
            return;
        }

        from.SendGump(new TownshipExpansionConfirmGump(_township, preview));
    }

    protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
    {
        TownshipMarkerService.ClearExpansionPreview(from);

        if (cancelType == TargetCancelType.Canceled)
        {
            TownshipGump.DisplayTo(from, _township);
        }
    }
}

public sealed class TownshipExpansionRetryGump : DynamicGump
{
    private const int ButtonRetry = 1;

    private readonly TownshipState _township;
    private readonly TownshipExpansionPreview _preview;

    public TownshipExpansionRetryGump(TownshipState township, TownshipExpansionPreview preview) : base(180, 140)
    {
        _township = township;
        _preview = preview;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 460, 230, 9270);
        builder.AddAlphaRegion(15, 15, 430, 200);
        builder.AddLabel(40, 28, 33, "Expansion Requires Border Contact");
        builder.AddHtml(
            40,
            62,
            380,
            82,
            $"Expansions must border at least {TownshipSettings.EdgeContactRequired:N0} existing township border tiles.<br>Your selection bordered {_preview.SharedEdgeTiles:N0}.",
            "#D8D8D8",
            scrollbar: false
        );
        builder.AddButton(80, 174, 4005, 4007, ButtonRetry);
        builder.AddLabel(114, 176, 2101, "Retry Targeting");
        builder.AddButton(280, 174, 4017, 4019, 0);
        builder.AddLabel(314, 176, 2101, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null)
        {
            return;
        }

        if (info.ButtonID == ButtonRetry)
        {
            TownshipExpansionTarget.Begin(from, _township);
            return;
        }

        TownshipMarkerService.ClearExpansionPreview(from);
        TownshipGump.DisplayTo(from, _township);
    }
}

public sealed class TownshipExpansionConfirmGump : DynamicGump
{
    private readonly TownshipState _township;
    private readonly TownshipExpansionPreview _preview;

    public TownshipExpansionConfirmGump(TownshipState township, TownshipExpansionPreview preview) : base(150, 120)
    {
        _township = township;
        _preview = preview;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 520, 360, 9270);
        builder.AddAlphaRegion(15, 15, 490, 330);
        builder.AddLabel(42, 28, 1153, "Confirm Township Expansion");
        builder.AddLabel(42, 66, 2101, $"Selected Area: {_preview.RequestedArea.Width}x{_preview.RequestedArea.Height}");
        builder.AddLabel(42, 94, 2101, $"Valid Tiles: {_preview.ValidTiles:N0}");
        builder.AddLabel(42, 122, _preview.InvalidTiles > 0 ? 33 : 68, $"Skipped Tiles: {_preview.InvalidTiles:N0}");
        builder.AddLabel(42, 150, _preview.MeetsEdgeRequirement ? 68 : 33, $"Shared Edge Tiles: {_preview.SharedEdgeTiles:N0}");
        builder.AddLabel(42, 178, _preview.InsideEnvelope ? 68 : 33, _preview.InsideEnvelope ? "Inside max border range" : "Outside max border range");
        builder.AddLabel(42, 206, 2101, $"Cost: {_preview.Cost:N0} gold");

        var y = 236;
        foreach (var pair in _preview.InvalidReasons)
        {
            builder.AddLabelCropped(42, y, 420, 22, 2401, $"{pair.Key}: {pair.Value:N0}");
            y += 22;

            if (y > 286)
            {
                break;
            }
        }

        if (_preview.ValidTiles > 0 && _preview.MeetsEdgeRequirement && _preview.InsideEnvelope)
        {
            builder.AddButton(70, 314, 4005, 4007, 1);
            builder.AddLabel(104, 316, 2101, "Purchase Valid Tiles");
        }

        builder.AddButton(330, 314, 4017, 4019, 0);
        builder.AddLabel(364, 316, 2101, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null)
        {
            return;
        }

        if (info.ButtonID != 1)
        {
            TownshipMarkerService.ClearExpansionPreview(from);
            TownshipGump.DisplayTo(from, _township);
            return;
        }

        if (!TownshipService.ApplyExpansion(_township, from, _preview, out var reason))
        {
            TownshipMarkerService.ClearExpansionPreview(from);
            from.SendMessage(0x22, reason);
            TownshipGump.DisplayTo(from, _township);
            return;
        }

        TownshipMarkerService.ClearExpansionPreview(from);
        from.SendMessage(0x35, $"The township claimed {_preview.ValidTiles:N0} new tile(s).");
        TownshipGump.DisplayTo(from, _township);
    }
}
