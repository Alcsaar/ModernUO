using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Custom.Systems.CustomFeatureFlags;
using Server.Custom.Utilities;
using Server.Gumps;
using Server.Guilds;
using Server.Items;
using Server.Logging;
using Server.Misc;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Regions;
using Server.Text;

namespace Server.Custom.Systems.Townships;

public static class TownshipService
{
    private static readonly TimeSpan UpkeepChargeTime = TimeSpan.FromHours(18);
    public const int MaxTownshipNpcRoamRange = 12;
    public const string FeatureFlagKey = "townships";
    private const int MilitiaPatrolHomeRange = 12;

    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(TownshipService));
    private static readonly List<TownshipState> _townships = new();
    private static readonly Dictionary<Serial, TownshipState> _byGuild = new();
    private static readonly Dictionary<string, TownshipState> _byId = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Serial, string> _lastTownByPlayer = new();

    public static IReadOnlyList<TownshipState> Townships => _townships;

    public static void Configure()
    {
        TownshipSettings.Configure();
        TownshipPersistence.Configure();
        TownshipCommands.Configure();
        TownshipCustomAdminModule.Configure();
        TownshipMarkerService.Configure();

        CustomFeatureFlagManager.Configure();
        CustomFeatureFlagManager.Register(
            FeatureFlagKey,
            "Townships",
            "Guild-owned flexible township territory system.",
            "Custom Systems",
            defaultEnabled: true
        );

        EventSink.Movement += OnMovement;
        Timer.DelayCall(TimeSpan.FromMinutes(1.0), TimeSpan.FromMinutes(5.0), Tick);
    }

    public static bool IsEnabled() => TownshipSettings.Enabled && CustomFeatureFlagManager.IsEnabled(FeatureFlagKey);

    public static TownshipState FindById(string id) =>
        !string.IsNullOrWhiteSpace(id) && _byId.TryGetValue(id, out var township) ? township : null;

    public static TownshipState FindByGuild(Guild guild) =>
        guild != null && _byGuild.TryGetValue(guild.Serial, out var township) ? township : null;

    public static TownshipState FindAt(Point3D point, Map map)
    {
        if (map == null || map == Map.Internal)
        {
            return null;
        }

        for (var i = 0; i < _townships.Count; i++)
        {
            var township = _townships[i];

            if (township.Map == map && Contains(township, point.X, point.Y))
            {
                return township;
            }
        }

        return null;
    }

    public static bool Contains(TownshipState township, int x, int y)
    {
        if (township == null)
        {
            return false;
        }

        for (var i = 0; i < township.Claims.Count; i++)
        {
            if (township.Claims[i].Contains(x, y))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsGuildmaster(Mobile from, Guild guild) =>
        from != null && guild?.Disbanded == false &&
        (from.AccessLevel >= AccessLevel.GameMaster || guild.Leader == from);

    public static bool IsGuildMember(Mobile from, Guild guild) =>
        from != null && guild?.Disbanded == false &&
        (from.AccessLevel >= AccessLevel.GameMaster || guild.IsMember(from));

    public static bool CanAccessTownship(TownshipState township, Mobile from) =>
        township?.Guild?.Disbanded == false && IsGuildMember(from, township.Guild);

    public static TownshipRankLevel GetRank(TownshipState township, Mobile from)
    {
        if (township?.Guild?.Disbanded != false || from == null)
        {
            return TownshipRankLevel.Citizen;
        }

        if (township.Guild.Leader == from || from.AccessLevel >= AccessLevel.GameMaster)
        {
            return TownshipRankLevel.Regent;
        }

        for (var i = 0; i < township.RankAssignments.Count; i++)
        {
            var assignment = township.RankAssignments[i];

            if (assignment.PlayerSerial == from.Serial)
            {
                return assignment.Rank;
            }
        }

        return TownshipRankLevel.Citizen;
    }

    public static string GetRankName(TownshipState township, Mobile from)
    {
        if (township?.Guild?.Leader == from)
        {
            return "Governor";
        }

        return GetRank(township, from).ToString();
    }

    public static bool HasPermission(TownshipState township, Mobile from, TownshipPermission permission)
    {
        if (permission == TownshipPermission.None)
        {
            return true;
        }

        if (township?.Guild?.Disbanded != false || from == null)
        {
            return false;
        }

        if (from.AccessLevel >= AccessLevel.GameMaster || township.Guild.Leader == from)
        {
            return true;
        }

        if (!township.Guild.IsMember(from))
        {
            return false;
        }

        return (GetPermissions(GetRank(township, from)) & permission) == permission;
    }

    public static TownshipPermission GetPermissions(TownshipRankLevel rank) => rank switch
    {
        TownshipRankLevel.Regent => TownshipPermission.ManageServiceNpcs |
                                    TownshipPermission.PurchaseServices |
                                    TownshipPermission.RemoveServices |
                                    TownshipPermission.PurchasePerks |
                                    TownshipPermission.TogglePerks |
                                    TownshipPermission.ExpandTerritory |
                                    TownshipPermission.MoveStone |
                                    TownshipPermission.RenameTownship |
                                    TownshipPermission.ManageRanks,
        TownshipRankLevel.Councilor => TownshipPermission.ManageServiceNpcs |
                                       TownshipPermission.PurchaseServices |
                                       TownshipPermission.RemoveServices |
                                       TownshipPermission.PurchasePerks |
                                       TownshipPermission.ExpandTerritory |
                                       TownshipPermission.MoveStone,
        TownshipRankLevel.Officer => TownshipPermission.ManageServiceNpcs,
        TownshipRankLevel.Aide => TownshipPermission.ManageServiceNpcs,
        _ => TownshipPermission.None
    };

    public static string GetRankPermissionSummary(TownshipRankLevel rank) => rank switch
    {
        TownshipRankLevel.Regent => "All township controls except abolishing the township.",
        TownshipRankLevel.Councilor => "Purchase/remove services, purchase perks, expand territory, move the charter, and manage service NPCs.",
        TownshipRankLevel.Officer => "Manage township service NPCs.",
        TownshipRankLevel.Aide => "Manage township service NPCs.",
        _ => "View township information, logs, upkeep, activity, services, perks, borders, and deposit to the treasury."
    };

    public static bool SetRank(TownshipState township, Mobile from, Mobile target, TownshipRankLevel rank, out string reason)
    {
        reason = null;

        if (!HasPermission(township, from, TownshipPermission.ManageRanks))
        {
            reason = "You do not have permission to manage township ranks.";
            return false;
        }

        if (target == null || township?.Guild?.IsMember(target) != true)
        {
            reason = "Township ranks may only be assigned to guild members.";
            return false;
        }

        if (township.Guild.Leader == target)
        {
            reason = "The guildmaster is always the township Governor.";
            return false;
        }

        if (rank < TownshipRankLevel.Citizen || rank > TownshipRankLevel.Regent)
        {
            rank = TownshipRankLevel.Citizen;
        }

        var oldRank = GetRank(township, target);
        var assignment = FindRankAssignment(township, target.Serial);

        if (rank == TownshipRankLevel.Citizen)
        {
            if (assignment != null)
            {
                township.RankAssignments.Remove(assignment);
            }
        }
        else if (assignment == null)
        {
            township.RankAssignments.Add(new TownshipRankAssignment
            {
                PlayerSerial = target.Serial,
                PlayerName = target.Name,
                Rank = rank
            });
        }
        else
        {
            assignment.PlayerName = target.Name;
            assignment.Rank = rank;
        }

        AddLog(
            township,
            TownshipLogType.StaffAction,
            from,
            $"Set {target.Name}'s township rank from {oldRank} to {rank}."
        );
        return true;
    }

    private static TownshipRankAssignment FindRankAssignment(TownshipState township, Serial serial)
    {
        if (township == null)
        {
            return null;
        }

        for (var i = 0; i < township.RankAssignments.Count; i++)
        {
            if (township.RankAssignments[i].PlayerSerial == serial)
            {
                return township.RankAssignments[i];
            }
        }

        return null;
    }

    public static bool TryFoundTownship(
        Mobile from,
        Point3D location,
        Map map,
        string requestedName,
        out TownshipState township,
        out string reason
    )
    {
        township = null;
        reason = null;

        if (!IsEnabled())
        {
            reason = "The township system is disabled.";
            return false;
        }

        if (from?.Deleted != false || map == null || map == Map.Internal)
        {
            reason = "Invalid founding location.";
            return false;
        }

        if (TownshipSettings.FeluccaOnly && map != Map.Felucca)
        {
            reason = "Townships may only be founded in Felucca.";
            return false;
        }

        if (from.Guild is not Guild guild || guild.Disbanded)
        {
            reason = "You must be in a guild to found a township.";
            return false;
        }

        if (!ValidateTownshipName(requestedName, out var townshipName, out reason, township))
        {
            return false;
        }

        if (!IsGuildmaster(from, guild))
        {
            reason = "Only the guildmaster may found a township.";
            return false;
        }

        if (FindByGuild(guild) != null)
        {
            reason = "Your guild already controls a township.";
            return false;
        }

        var house = BaseHouse.FindHouseAt(location, map, 16);

        if (house?.IsOwner(from) != true)
        {
            reason = "The township deed must be placed inside a house owned by the guildmaster.";
            return false;
        }

        var initial = CenteredRect(location, TownshipSettings.InitialClaimSize);

        if (!ValidateRegionRules(guild, initial, map, out reason))
        {
            return false;
        }

        if (IntersectsExistingTownship(null, initial, map))
        {
            reason = "The selected area overlaps an existing township.";
            return false;
        }

        if (HasBlockingHouse(guild, initial, map, allowGrandfathered: false, out reason))
        {
            return false;
        }

        var sizes = TownshipSettings.EnvelopeSizes;

        township = new TownshipState
        {
            Name = townshipName,
            Guild = guild,
            GuildSerial = guild.Serial,
            FoundingPoint = location,
            Map = map,
            FoundedAt = Core.Now,
            MaxEnvelopeSize = GetInitialEnvelopeSize(sizes),
            LastActivityDecay = Core.Now,
            NextUpkeepAssessment = GetNextUpkeepChargeTime(Core.Now),
            NextWeeklyPayment = GetNextUpkeepChargeTime(Core.Now.AddDays(7))
        };

        AddRectangleClaim(township, initial);
        RecordCompatibleHouses(township, initial);
        Register(township);
        AddLog(
            township,
            TownshipLogType.Founded,
            from,
            $"Founded at {FormatLocation(location, map)} ({GetLocationDescription(township)})."
        );

        return true;
    }

    public static string GetLocationDescription(TownshipState township) =>
        township == null
            ? "an unknown location"
            : WorldLocationDescription.Describe(township.FoundingPoint, township.Map);

    public static void BroadcastFounded(TownshipState township)
    {
        if (township == null)
        {
            return;
        }

        foreach (var state in NetState.Instances)
        {
            var mobile = state.Mobile;

            if (mobile?.Deleted == false)
            {
                mobile.SendGump(new TownshipFoundedAnnouncementGump(township));
            }
        }
    }

    public static bool ValidateTownshipName(
        string requestedName,
        out string townshipName,
        out string reason,
        TownshipState ignoreTownship = null
    )
    {
        townshipName = requestedName?.Trim();
        reason = null;

        if (string.IsNullOrWhiteSpace(townshipName))
        {
            reason = "You must enter a township name.";
            return false;
        }

        if (townshipName.Length > 40)
        {
            townshipName = townshipName[..40].Trim();
        }

        if (!Server.Guilds.BaseGuildGump.CheckProfanity(townshipName, 40))
        {
            reason = "That township name is not allowed.";
            return false;
        }

        var existing = FindByName(townshipName);

        if (existing != null && existing != ignoreTownship)
        {
            reason = "That township name is already in use.";
            return false;
        }

        return true;
    }

    public static TownshipState FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();

        for (var i = 0; i < _townships.Count; i++)
        {
            if (string.Equals(_townships[i].Name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return _townships[i];
            }
        }

        return null;
    }

    public static bool RenameTownship(TownshipState township, Mobile from, string requestedName, out string reason)
    {
        if (!HasPermission(township, from, TownshipPermission.RenameTownship))
        {
            reason = "You do not have permission to rename this township.";
            return false;
        }

        if (!ValidateTownshipName(requestedName, out var townshipName, out reason, township))
        {
            return false;
        }

        var oldName = township.Name;
        township.Name = townshipName;
        RebuildRegion(township);
        AddLog(township, TownshipLogType.StaffAction, from, $"Renamed township from '{oldName}' to '{townshipName}'.");
        return true;
    }

    public static bool AbolishTownship(TownshipState township, Mobile from, out string reason)
    {
        if (!HasPermission(township, from, TownshipPermission.AbolishTownship))
        {
            reason = "You do not have permission to abolish this township.";
            return false;
        }

        reason = null;
        Disband(township, from, "Township abolished from the township stone.");
        return true;
    }

    public static bool MoveStone(TownshipState township, Mobile from, Point3D location, Map map, out string reason)
    {
        reason = null;

        if (township?.Stone?.Deleted != false || from == null)
        {
            reason = "The township stone is unavailable.";
            return false;
        }

        if (!HasPermission(township, from, TownshipPermission.MoveStone))
        {
            reason = "You do not have permission to move this township stone.";
            return false;
        }

        if (map != township.Map || !Contains(township, location.X, location.Y))
        {
            reason = "The township stone can only be moved within claimed township land.";
            return false;
        }

        township.Stone.MoveToWorld(location, map);
        AddLog(township, TownshipLogType.StoneMoved, from, $"Moved stone to {FormatLocation(location, map)}.");
        return true;
    }

    public static bool CanManageTownship(TownshipState township, Mobile from) =>
        HasPermission(township, from, TownshipPermission.ManageServiceNpcs);

    public static bool CanUseStaffTools(Mobile from) => from?.AccessLevel >= AccessLevel.GameMaster;

    public static bool ClearTreasury(TownshipState township, Mobile from, out string reason)
    {
        if (!ValidateStaffAction(township, from, out reason))
        {
            return false;
        }

        var oldBalance = township.TreasuryBalance;
        township.TreasuryBalance = 0;
        AddLog(township, TownshipLogType.StaffAction, from, $"[STAFF] Cleared treasury balance. Previous balance: {oldBalance:N0} gp. {FormatStaffActor(from)}");
        return true;
    }

    public static bool ClearTreasuryLog(TownshipState township, Mobile from, out string reason)
    {
        if (!ValidateStaffAction(township, from, out reason))
        {
            return false;
        }

        var oldCount = township.DepositLog.Count;
        var oldContributionCount = township.TreasuryContributions.Count;
        township.DepositLog.Clear();
        township.TreasuryContributions.Clear();
        AddLog(township, TownshipLogType.StaffAction, from, $"[STAFF] Cleared treasury activity log. Removed {oldCount:N0} entr{(oldCount == 1 ? "y" : "ies")} and {oldContributionCount:N0} contribution detail{(oldContributionCount == 1 ? "" : "s")}. {FormatStaffActor(from)}");
        return true;
    }

    public static bool ClearLifetimeDeposits(TownshipState township, Mobile from, out string reason)
    {
        if (!ValidateStaffAction(township, from, out reason))
        {
            return false;
        }

        var oldTotal = township.LifetimeDeposits;
        township.LifetimeDeposits = 0;
        AddLog(township, TownshipLogType.StaffAction, from, $"[STAFF] Cleared lifetime deposits total. Previous total: {oldTotal:N0} gp. {FormatStaffActor(from)}");
        return true;
    }

    public static bool ClearActivityLog(TownshipState township, Mobile from, out string reason)
    {
        if (!ValidateStaffAction(township, from, out reason))
        {
            return false;
        }

        if (from.AccessLevel < AccessLevel.Owner)
        {
            reason = "Clearing the township staff/activity log requires Owner access.";
            return false;
        }

        var oldCount = township.ActivityLog.Count;
        township.ActivityLog.Clear();
        AddLog(township, TownshipLogType.StaffAction, from, $"[STAFF] Cleared staff/activity log. Removed {oldCount:N0} entr{(oldCount == 1 ? "y" : "ies")}. {FormatStaffActor(from)}");
        return true;
    }

    public static bool AdjustActivity(TownshipState township, Mobile from, int value, bool setValue, out string reason)
    {
        if (!ValidateStaffAction(township, from, out reason))
        {
            return false;
        }

        var oldScore = township.ActivityScore;
        township.ActivityScore = setValue ? Math.Clamp(value, 0, 1000) : Math.Clamp(township.ActivityScore + value, 0, 1000);
        var action = setValue ? "set" : "adjusted";
        AddLog(township, TownshipLogType.StaffAction, from, $"[STAFF] Activity score {action}. Previous: {oldScore:N0}. Current: {township.ActivityScore:N0}. {FormatStaffActor(from)}");
        return true;
    }

    public static bool SetNextUpkeepCharge(TownshipState township, Mobile from, DateTime nextCharge, out string reason)
    {
        if (!ValidateStaffAction(township, from, out reason))
        {
            return false;
        }

        if (nextCharge < Core.Now.AddYears(-1) || nextCharge > Core.Now.AddYears(1))
        {
            reason = "Enter a charge time within one year of now.";
            return false;
        }

        var oldAssessment = township.NextUpkeepAssessment;
        var oldPayment = township.NextWeeklyPayment;
        township.NextUpkeepAssessment = nextCharge;
        township.NextWeeklyPayment = nextCharge;
        AddLog(
            township,
            TownshipLogType.StaffAction,
            from,
            $"[STAFF] Set next upkeep charge to {nextCharge:g}. Previous assessment: {oldAssessment:g}. Previous charge: {oldPayment:g}. {FormatStaffActor(from)}"
        );

        if (nextCharge <= Core.Now)
        {
            AssessUpkeep(township);
        }

        reason = null;
        return true;
    }

    public static bool SetDelinquentBalance(TownshipState township, Mobile from, int amount, out string reason)
    {
        if (!ValidateStaffAction(township, from, out reason))
        {
            return false;
        }

        amount = Math.Max(0, amount);
        var oldAmount = township.DelinquentBalance;

        if (amount == 0)
        {
            ClearDelinquency(township, from, "Staff cleared delinquency balance.");
        }
        else
        {
            EnterDelinquency(township, amount, from, replaceBalance: true);
        }

        AddLog(
            township,
            TownshipLogType.StaffAction,
            from,
            $"[STAFF] Set delinquent balance from {oldAmount:N0} gp to {amount:N0} gp. {FormatStaffActor(from)}"
        );
        reason = null;
        return true;
    }

    public static bool StaffRefreshTownship(TownshipState township, Mobile from, out string reason)
    {
        if (!ValidateStaffAction(township, from, out reason))
        {
            return false;
        }

        RebuildRegion(township);
        TownshipMarkerService.RefreshTownship(township);
        AddLog(township, TownshipLogType.StaffAction, from, $"[STAFF] Rebuilt township region and refreshed active border viewers. {FormatStaffActor(from)}");
        return true;
    }

    public static Rectangle2D GetEnvelope(TownshipState township) =>
        CenteredRect(township.FoundingPoint, Math.Max(TownshipSettings.InitialClaimSize, township.MaxEnvelopeSize));

    public static TownshipExpansionPreview PreviewExpansion(TownshipState township, Rectangle2D requested)
    {
        var preview = new TownshipExpansionPreview
        {
            RequestedArea = requested,
            InsideEnvelope = RectangleContains(GetEnvelope(township), requested)
        };

        var rowStart = int.MinValue;
        var rowEnd = int.MinValue;
        var rowY = 0;
        var invalidStart = int.MinValue;
        var invalidEnd = int.MinValue;
        var invalidY = 0;

        for (var y = requested.Y; y < requested.Y + requested.Height; y++)
        {
            FlushRange(preview.ValidClaims, ref rowStart, ref rowEnd, ref rowY);
            FlushRange(preview.InvalidClaims, ref invalidStart, ref invalidEnd, ref invalidY);

            for (var x = requested.X; x < requested.X + requested.Width; x++)
            {
                var valid = IsTileClaimable(township, x, y, out var reason);

                if (valid)
                {
                    preview.ValidTiles++;
                    AddToPendingRange(ref rowStart, ref rowEnd, ref rowY, x, y, preview.ValidClaims);
                }
                else
                {
                    preview.InvalidTiles++;
                    AddInvalidReason(preview, reason);
                    AddToPendingRange(ref invalidStart, ref invalidEnd, ref invalidY, x, y, preview.InvalidClaims);
                }
            }
        }

        FlushRange(preview.ValidClaims, ref rowStart, ref rowEnd, ref rowY);
        FlushRange(preview.InvalidClaims, ref invalidStart, ref invalidEnd, ref invalidY);

        preview.SharedEdgeTiles = CountSharedEdges(township, preview.ValidClaims);
        preview.MeetsEdgeRequirement = preview.SharedEdgeTiles >= TownshipSettings.EdgeContactRequired;
        preview.Cost = preview.ValidTiles * TownshipSettings.TileCost;

        return preview;
    }

    public static bool ApplyExpansion(TownshipState township, Mobile from, TownshipExpansionPreview preview, out string reason)
    {
        reason = null;

        if (township == null || preview == null)
        {
            reason = "Expansion data is unavailable.";
            return false;
        }

        if (!HasPermission(township, from, TownshipPermission.ExpandTerritory))
        {
            reason = "You do not have permission to expand this township.";
            return false;
        }

        if (!preview.InsideEnvelope)
        {
            reason = "The selected area extends outside the current township max border range.";
            return false;
        }

        if (!preview.MeetsEdgeRequirement)
        {
            reason = $"Expansion must share at least {TownshipSettings.EdgeContactRequired} edge tiles with existing territory.";
            return false;
        }

        if (preview.ValidTiles <= 0)
        {
            reason = "There are no valid tiles to claim.";
            return false;
        }

        if (preview.Cost > 0)
        {
            if (township.TreasuryBalance < preview.Cost)
            {
                reason = "The township treasury does not contain enough gold for this expansion.";
                return false;
            }

            township.TreasuryBalance -= preview.Cost;
        }

        for (var i = 0; i < preview.ValidClaims.Count; i++)
        {
            township.Claims.Add(new TownshipClaimRange
            {
                Y = preview.ValidClaims[i].Y,
                StartX = preview.ValidClaims[i].StartX,
                EndX = preview.ValidClaims[i].EndX
            });
        }

        MergeClaims(township);
        RecordCompatibleHouses(township, preview.RequestedArea);
        AddLog(
            township,
            TownshipLogType.ExpansionPurchased,
            from,
            $"Claimed {preview.ValidTiles} tile(s), skipped {preview.InvalidTiles}, cost {preview.Cost:N0}."
        );

        RebuildRegion(township);
        TownshipMarkerService.RefreshTownship(township);
        return true;
    }

    public static void RebuildRegion(TownshipState township)
    {
        if (township == null || township.Map == null || township.Map == Map.Internal || township.Claims.Count == 0)
        {
            return;
        }

        township.Region?.Unregister();
        township.Region = new TownshipRegion(township, BuildRegionRectangles(township));
        township.Region.Register();
    }

    public static int GetDailyLandUpkeep(TownshipState township) =>
        township == null ? 0 : township.ClaimedTileCount * TownshipSettings.DailyLandUpkeepPerTile;

    public static int GetDailyServiceUpkeep(TownshipState township)
    {
        if (township == null)
        {
            return 0;
        }

        var total = 0;

        for (var i = 0; i < township.Services.Count; i++)
        {
            var service = township.Services[i];

            if (service.Status is TownshipPaidServiceStatus.Active or TownshipPaidServiceStatus.Suspended)
            {
                total += Math.Max(0, service.DailyUpkeep);
            }
        }

        return total;
    }

    public static int GetDailyUpkeep(TownshipState township) =>
        GetDailyLandUpkeep(township) + GetDailyServiceUpkeep(township);

    public static bool HasActivePerk(TownshipState township, TownshipPaidServiceType type)
    {
        if (township?.IsDelinquent == true || township == null)
        {
            return false;
        }

        for (var i = 0; i < township.Services.Count; i++)
        {
            var service = township.Services[i];

            if (service.Type == type && service.Status == TownshipPaidServiceStatus.Active)
            {
                return true;
            }
        }

        return false;
    }

    public static TownshipPaidServiceRecord FindFirstService(TownshipState township, TownshipPaidServiceType type)
    {
        if (township == null)
        {
            return null;
        }

        for (var i = 0; i < township.Services.Count; i++)
        {
            var service = township.Services[i];

            if (service.Type == type && service.Status != TownshipPaidServiceStatus.Removed)
            {
                return service;
            }
        }

        return null;
    }

    public static void RemovePatrolGuardSerial(string townshipId, Serial serial)
    {
        var township = FindById(townshipId);

        if (township == null || serial == Serial.Zero)
        {
            return;
        }

        for (var i = township.PatrolGuardSerials.Count - 1; i >= 0; i--)
        {
            if (township.PatrolGuardSerials[i] == serial)
            {
                township.PatrolGuardSerials.RemoveAt(i);
            }
        }
    }

    public static bool TryMovePatrolGuardToTownship(TownshipPatrolGuard guard, TownshipState township)
    {
        if (guard == null || township == null || !TryGetSpreadPatrolLocation(township, out var location))
        {
            return false;
        }

        guard.Home = location;
        guard.RangeHome = MilitiaPatrolHomeRange;
        guard.MoveToWorld(location, township.Map);
        return true;
    }

    public static bool IsInsideTownshipEnvelope(TownshipState township, Mobile mobile) =>
        mobile != null && township?.Map == mobile.Map && IsInsideTownshipEnvelope(township, mobile.X, mobile.Y);

    public static bool IsInsideTownshipEnvelope(TownshipState township, int x, int y) =>
        township != null && GetEnvelope(township).Contains(x, y);

    public static void AlertTownMilitia(TownshipState township, Mobile threat)
    {
        if (township == null ||
            threat?.Deleted != false ||
            !threat.Alive ||
            !HasActivePerk(township, TownshipPaidServiceType.GuardedTown) ||
            !IsInsideTownshipEnvelope(township, threat))
        {
            return;
        }

        for (var i = township.PatrolGuardSerials.Count - 1; i >= 0; i--)
        {
            if (World.FindMobile(township.PatrolGuardSerials[i]) is not TownshipPatrolGuard guard ||
                guard.Deleted ||
                guard.Map != township.Map ||
                !guard.Alive ||
                !IsInsideTownshipEnvelope(township, guard))
            {
                continue;
            }

            var distance = guard.GetDistanceToSqrt(threat);

            if (distance > TownshipPatrolGuard.AlertRange || !guard.CanSee(threat) || !guard.InLOS(threat))
            {
                continue;
            }

            guard.Combatant = threat;
            guard.SetCurrentSpeedToActive();
        }
    }

    private static void MaintainPatrolGuards(TownshipState township)
    {
        if (township == null)
        {
            return;
        }

        if (!HasActivePerk(township, TownshipPaidServiceType.GuardedTown))
        {
            DeletePatrolGuards(township);
            return;
        }

        for (var i = township.PatrolGuardSerials.Count - 1; i >= 0; i--)
        {
            var guard = World.FindMobile(township.PatrolGuardSerials[i]) as TownshipPatrolGuard;

            if (guard == null ||
                guard.Deleted ||
                guard.Map != township.Map ||
                !Contains(township, guard.X, guard.Y))
            {
                township.PatrolGuardSerials.RemoveAt(i);

                if (guard?.Deleted == false)
                {
                    guard.Delete();
                }
            }
        }

        var targetCount = TownshipSettings.GuardedTownPatrolGuards;

        while (township.PatrolGuardSerials.Count > targetCount)
        {
            var index = township.PatrolGuardSerials.Count - 1;
            var serial = township.PatrolGuardSerials[index];
            township.PatrolGuardSerials.RemoveAt(index);

            if (World.FindMobile(serial) is TownshipPatrolGuard guard && !guard.Deleted)
            {
                guard.Delete();
            }
        }

        while (township.PatrolGuardSerials.Count < targetCount)
        {
            if (!TryGetSpreadPatrolLocation(township, out var location))
            {
                break;
            }

            var guard = new TownshipPatrolGuard(township.Id)
            {
                Home = location,
                RangeHome = MilitiaPatrolHomeRange
            };

            guard.MoveToWorld(location, township.Map);
            township.PatrolGuardSerials.Add(guard.Serial);
        }
    }

    private static void DeletePatrolGuards(TownshipState township)
    {
        if (township == null)
        {
            return;
        }

        for (var i = township.PatrolGuardSerials.Count - 1; i >= 0; i--)
        {
            var serial = township.PatrolGuardSerials[i];

            if (World.FindMobile(serial) is TownshipPatrolGuard guard && !guard.Deleted)
            {
                guard.Delete();
            }
        }

        township.PatrolGuardSerials.Clear();
    }

    private static bool TryGetSpreadPatrolLocation(TownshipState township, out Point3D location)
    {
        location = Point3D.Zero;

        if (township?.Map == null || township.Map == Map.Internal || township.Claims.Count == 0)
        {
            return false;
        }

        var bestScore = double.MinValue;

        for (var i = 0; i < 80; i++)
        {
            if (!TryGetRandomPatrolLocation(township, out var candidate))
            {
                continue;
            }

            var score = ScorePatrolLocation(township, candidate);

            if (score > bestScore)
            {
                location = candidate;
                bestScore = score;
            }
        }

        return bestScore > double.MinValue;
    }

    private static double ScorePatrolLocation(TownshipState township, Point3D candidate)
    {
        var closest = double.MaxValue;

        for (var i = township.PatrolGuardSerials.Count - 1; i >= 0; i--)
        {
            if (World.FindMobile(township.PatrolGuardSerials[i]) is not TownshipPatrolGuard guard ||
                guard.Deleted ||
                guard.Map != township.Map)
            {
                continue;
            }

            closest = Math.Min(closest, GetDistance(candidate, guard.Location));

            if (guard.Home != Point3D.Zero)
            {
                closest = Math.Min(closest, GetDistance(candidate, guard.Home));
            }
        }

        if (closest == double.MaxValue)
        {
            closest = TownshipSettings.InitialClaimSize;
        }

        return closest + Utility.RandomDouble();
    }

    private static double GetDistance(Point3D a, Point3D b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        return Math.Sqrt(x * x + y * y);
    }

    private static bool TryGetRandomPatrolLocation(TownshipState township, out Point3D location)
    {
        location = Point3D.Zero;

        if (township?.Map == null || township.Map == Map.Internal || township.Claims.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < 50; i++)
        {
            var claim = township.Claims[Utility.Random(township.Claims.Count)];
            var x = Utility.RandomMinMax(claim.StartX, claim.EndX);
            var y = claim.Y;
            var z = township.Map.GetAverageZ(x, y);

            if (township.Map.CanSpawnMobile(x, y, z))
            {
                location = new Point3D(x, y, z);
                return true;
            }
        }

        return false;
    }

    public static bool IsHuntingTaxActive(TownshipState township) =>
        TownshipSettings.HuntingContributionPercent > 0 && HasActivePerk(township, TownshipPaidServiceType.HuntingTax);

    public static bool IsHuntingTaxAppliedTo(TownshipState township, Mobile from)
    {
        if (!IsHuntingTaxActive(township) || !IsHuntingTaxGuildMember(township, from))
        {
            return false;
        }

        return true;
    }

    private static bool IsHuntingTaxGuildMember(TownshipState township, Mobile from) =>
        from != null && township?.Guild?.Disbanded == false && township.Guild.IsMember(from);

    public static bool SetPerkEnabled(
        TownshipState township,
        Mobile from,
        TownshipPaidServiceType type,
        bool enabled,
        out string reason
    )
    {
        if (!IsPerkService(type))
        {
            reason = "Only township perks can be toggled.";
            return false;
        }

        if (!HasPermission(township, from, TownshipPermission.TogglePerks))
        {
            reason = "You do not have permission to configure township perks.";
            return false;
        }

        if (township.IsDelinquent)
        {
            reason = "Perks cannot be toggled while the township is delinquent.";
            return false;
        }

        var service = FindFirstService(township, type);

        if (service == null)
        {
            reason = "That perk has not been purchased.";
            return false;
        }

        var wasActive = HasActivePerk(township, type);
        service.Status = enabled ? TownshipPaidServiceStatus.Active : TownshipPaidServiceStatus.Disabled;
        service.SuspendedAt = DateTime.MinValue;

        if (type == TownshipPaidServiceType.GuardedTown)
        {
            RebuildRegion(township);

            if (enabled)
            {
                MaintainPatrolGuards(township);
            }
            else
            {
                DeletePatrolGuards(township);
            }
        }
        AddLog(
            township,
            TownshipLogType.StaffAction,
            from,
            $"{GetServiceDisplayName(type, false)} perk {(enabled ? "enabled" : "disabled")}."
        );

        reason = null;
        return true;
    }

    public static void ApplyHuntingTax(BaseCreature creature, Container corpse, List<DamageStore> rights)
    {
        if (creature == null || corpse is not Corpse || rights == null || rights.Count == 0 ||
            creature.Summoned || creature.Controlled || creature.Owners.Count > 0)
        {
            return;
        }

        var totalGold = CountGold(corpse);

        if (totalGold <= 0)
        {
            return;
        }

        for (var i = 0; i < rights.Count; i++)
        {
            var ds = rights[i];

            if (ds?.m_Mobile is not PlayerMobile player || !ds.m_HasRight)
            {
                continue;
            }

            var township = FindHuntingTaxTownship(player, creature);

            if (!IsHuntingTaxAppliedTo(township, player))
            {
                continue;
            }

            var taxableGold = CalculateTaxableGold(totalGold, rights, ds);
            var tax = (int)Math.Clamp(taxableGold * TownshipSettings.HuntingContributionPercent / 100, 0, int.MaxValue);

            if (tax <= 0 && taxableGold > 0 && TownshipSettings.HuntingContributionPercent > 0)
            {
                tax = 1;
            }

            if (tax <= 0)
            {
                continue;
            }

            AddAutomatedTreasuryRevenue(
                township,
                player,
                tax,
                TownshipDepositSource.HuntingTax,
                $"Hunting contribution generated from {GetCreatureTypeName(creature)}."
            );
            player.SendMessage(
                0x35,
                $"{township.Name} generated {tax:N0} gp for its treasury from this kill."
            );
        }
    }

    private static TownshipState FindHuntingTaxTownship(PlayerMobile player, BaseCreature creature)
    {
        var township = FindByGuild(player?.Guild as Guild);

        if (township != null || player?.AccessLevel < AccessLevel.GameMaster)
        {
            return township;
        }

        return FindAt(creature.Location, creature.Map);
    }

    private static long CalculateTaxableGold(int totalGold, List<DamageStore> rights, DamageStore damage)
    {
        var participants = 0;
        var totalDamage = 0;

        for (var i = 0; i < rights.Count; i++)
        {
            if (rights[i]?.m_Mobile is PlayerMobile && rights[i].m_HasRight)
            {
                participants++;
                totalDamage += Math.Max(0, rights[i].m_Damage);
            }
        }

        if (participants <= 1)
        {
            return totalGold;
        }

        if (totalDamage <= 0)
        {
            return totalGold / participants;
        }

        var playerDamage = Math.Max(0, damage.m_Damage);
        return Math.Clamp((long)totalGold * playerDamage / totalDamage, 0, totalGold);
    }

    public static void RecordNpcVendorPurchaseRevenue(BaseVendor vendor, Mobile buyer, int saleTotal, string purchaseDetails)
    {
        if (vendor?.Deleted != false ||
            buyer == null ||
            buyer.AccessLevel >= AccessLevel.GameMaster ||
            saleTotal <= 0 ||
            TownshipSettings.VendorRevenueContributionPercent <= 0 ||
            vendor is not ITownshipServiceNpc serviceNpc ||
            !serviceNpc.ServiceActive)
        {
            return;
        }

        var township = serviceNpc.Township;

        if (township == null ||
            township.Map != vendor.Map ||
            !Contains(township, vendor.X, vendor.Y))
        {
            return;
        }

        var contribution = (int)Math.Clamp(
            (long)saleTotal * TownshipSettings.VendorRevenueContributionPercent / 100,
            0,
            int.MaxValue
        );

        if (contribution <= 0)
        {
            contribution = 1;
        }

        var detail = FormatVendorRevenueNote(vendor, saleTotal, purchaseDetails);

        AddAutomatedTreasuryRevenue(
            township,
            buyer,
            contribution,
            TownshipDepositSource.VendorRevenue,
            detail,
            detail
        );
    }

    private static string FormatVendorRevenueNote(BaseVendor vendor, int saleTotal, string purchaseDetails)
    {
        var vendorName = string.IsNullOrWhiteSpace(vendor?.Name) ? vendor?.GetType().Name ?? "vendor" : vendor.Name.Trim();
        purchaseDetails = string.IsNullOrWhiteSpace(purchaseDetails) ? "purchase details unavailable" : purchaseDetails.Trim();

        return $"{vendorName}: {purchaseDetails}. Sale total: {saleTotal:N0} gp.";
    }

    public static double GetEstimatedUpkeepDaysRemaining(TownshipState township)
    {
        var daily = GetDailyUpkeep(township);

        if (!TownshipSettings.UpkeepEnabled || daily <= 0)
        {
            return double.PositiveInfinity;
        }

        return township == null
            ? 0.0
            : Math.Max(0, township.TreasuryBalance - township.DelinquentBalance - township.AccruedUpkeepDue) / (double)daily;
    }

    public static TownshipDelinquencyPlan GetDelinquencyPlan(TownshipState township)
    {
        if (township?.IsDelinquent != true)
        {
            return null;
        }

        var plan = new TownshipDelinquencyPlan
        {
            DelinquentBalance = township.DelinquentBalance,
            AccruedUpkeepDue = township.AccruedUpkeepDue,
            DelinquentSince = township.DelinquentSince,
            NextRemovalCheck = township.NextDelinquencyRemoval,
            ServicesSuspended = township.PaidServicesSuspended
        };

        var scheduled = township.NextDelinquencyRemoval == DateTime.MinValue
            ? Core.Now + TownshipSettings.DelinquencyGracePeriod
            : township.NextDelinquencyRemoval;

        for (var i = 0; i < township.Services.Count; i++)
        {
            var service = GetDelinquencyRemovalServiceAt(township, i);

            if (service == null)
            {
                break;
            }

            var refund = CalculateServiceRefund(service, delinquencyRemoval: true);

            plan.RemovalOrder.Add(new TownshipDelinquencyRemovalEntry
            {
                ServiceId = service.Id,
                Name = service.Name,
                DailyUpkeep = service.DailyUpkeep,
                RefundAmount = refund.RefundAmount,
                ScheduledRemoval = scheduled,
                Status = service.Status.ToString()
            });

            scheduled += TownshipSettings.DelinquencyRemovalInterval;
        }

        return plan;
    }

    public static TownshipServiceRefundPreview CalculateServiceRefund(TownshipPaidServiceRecord service, bool delinquencyRemoval) =>
        service == null
            ? CalculateServiceRefund(0, Core.Now, delinquencyRemoval)
            : CalculateServiceRefund(service.PurchaseCost, service.PurchasedAt, delinquencyRemoval);

    public static bool AddPaidService(
        TownshipState township,
        Mobile from,
        TownshipPaidServiceType type,
        string name,
        int purchaseCost,
        int dailyUpkeep,
        string notes,
        out string reason
    )
    {
        if (!ValidateStaffAction(township, from, out reason))
        {
            return false;
        }

        name = string.IsNullOrWhiteSpace(name) ? type.ToString() : name.Trim();
        purchaseCost = Math.Clamp(purchaseCost, 0, 100000000);
        dailyUpkeep = Math.Clamp(dailyUpkeep, 0, 10000000);

        var service = new TownshipPaidServiceRecord
        {
            Type = type,
            Name = name.Length <= 40 ? name : name[..40],
            PurchaseCost = purchaseCost,
            DailyUpkeep = dailyUpkeep,
            PurchasedAt = Core.Now,
            Status = township.IsDelinquent ? TownshipPaidServiceStatus.Suspended : TownshipPaidServiceStatus.Active,
            SuspendedAt = township.IsDelinquent ? Core.Now : DateTime.MinValue,
            Notes = CleanNote(notes)
        };

        township.Services.Add(service);
        AddLog(
            township,
            TownshipLogType.StaffAction,
            from,
            $"[STAFF] Added paid service '{service.Name}' ({service.Type}). Purchase cost: {service.PurchaseCost:N0} gp. Daily upkeep: {service.DailyUpkeep:N0} gp. {FormatStaffActor(from)}"
        );
        reason = null;
        return true;
    }

    public static bool PurchaseBankerService(
        TownshipState township,
        Mobile from,
        Point3D location,
        Map map,
        out string reason
    ) => PurchasePaidService(township, from, TownshipPaidServiceType.Banker, location, map, out reason);

    public static bool PurchasePaidService(
        TownshipState township,
        Mobile from,
        TownshipPaidServiceType type,
        Point3D location,
        Map map,
        out string reason
    )
    {
        reason = null;

        if (!HasPermission(township, from, IsPerkService(type) ? TownshipPermission.PurchasePerks : TownshipPermission.PurchaseServices))
        {
            reason = "You do not have permission to purchase township services.";
            return false;
        }

        if (township.IsDelinquent)
        {
            reason = "Township services cannot be purchased while the township is delinquent.";
            return false;
        }

        var perk = IsPerkService(type);

        if (!perk && (map != township.Map || !Contains(township, location.X, location.Y)))
        {
            reason = "Township services must be placed within claimed township land.";
            return false;
        }

        BaseHouse house = null;

        if (!perk)
        {
            house = BaseHouse.FindHouseAt(location, map, 16);

            if (house == null)
            {
                reason = $"{GetServiceDisplayName(type, plural: true)} must be placed inside a compatible house within the township.";
                return false;
            }

            if (!IsCompatibleHouse(township.Guild, house))
            {
                reason = $"{GetServiceDisplayName(type, plural: true)} must be placed inside a guild-member or grandfathered township house.";
                return false;
            }
        }
        else if (HasActiveOrSuspendedService(township, type))
        {
            reason = $"{GetServiceDisplayName(type, plural: false)} is already enabled for this township.";
            return false;
        }

        var purchaseCost = GetServicePurchaseCost(type);
        var dailyUpkeep = GetServiceDailyUpkeep(type);
        var displayName = GetServiceDisplayName(type, plural: false);

        if (!perk && !IsImplementedServiceNpc(type))
        {
            /* Validate the NPC implementation before debiting the treasury so unsupported
             * service types cannot consume funds and then fail placement.
             */
            reason = "That township service is not implemented yet.";
            return false;
        }

        if (township.TreasuryBalance < purchaseCost)
        {
            reason = $"The township treasury needs {purchaseCost:N0} gp to purchase a {displayName} contract.";
            return false;
        }

        township.TreasuryBalance -= purchaseCost;
        AddTreasuryActivity(township, from, -purchaseCost, TownshipDepositSource.ServicePurchase, $"{displayName} contract purchase.");

        var service = new TownshipPaidServiceRecord
        {
            Type = type,
            Name = $"Township {displayName}",
            PurchaseCost = purchaseCost,
            DailyUpkeep = dailyUpkeep,
            PurchasedAt = Core.Now,
            Status = TownshipPaidServiceStatus.Active,
            CreatedObjectSerial = Serial.Zero,
            AnchorHouseSerial = house?.Serial ?? Serial.Zero,
            HomeLocation = location,
            RoamRange = 0,
            Notes = $"Purchased {displayName} service."
        };

        if (!perk)
        {
            var mobile = CreatePaidServiceNpc(type, township.Id, service.Id);

            if (mobile == null)
            {
                reason = "That township service is not implemented yet.";
                return false;
            }

            mobile.MoveToWorld(location, map);

            if (mobile is ITownshipServiceNpc npc)
            {
                npc.ApplyServiceMovement();
            }

            service.CreatedObjectSerial = mobile.Serial;
        }

        township.Services.Add(service);
        AddLog(
            township,
            TownshipLogType.StaffAction,
            from,
            perk
                ? $"Purchased {displayName} perk. Cost: {purchaseCost:N0} gp. Daily upkeep: {dailyUpkeep:N0} gp."
                : $"Purchased {displayName} service at {FormatLocation(location, map)}. Cost: {purchaseCost:N0} gp. Daily upkeep: {dailyUpkeep:N0} gp."
        );

        if (type == TownshipPaidServiceType.GuardedTown)
        {
            RebuildRegion(township);
            MaintainPatrolGuards(township);
        }

        return true;
    }

    public static bool IsPerkService(TownshipPaidServiceType type) =>
        type is TownshipPaidServiceType.GuardedTown or TownshipPaidServiceType.HuntingTax;

    private static bool HasActiveOrSuspendedService(TownshipState township, TownshipPaidServiceType type)
    {
        if (township == null)
        {
            return false;
        }

        for (var i = 0; i < township.Services.Count; i++)
        {
            var service = township.Services[i];

            if (service.Type == type && service.Status != TownshipPaidServiceStatus.Removed)
            {
                return true;
            }
        }

        return false;
    }

    private static Mobile CreatePaidServiceNpc(TownshipPaidServiceType type, string townshipId, string serviceId) =>
        type switch
        {
            TownshipPaidServiceType.Banker       => new TownshipBanker(townshipId, serviceId),
            TownshipPaidServiceType.Mage         => new TownshipMage(townshipId, serviceId),
            TownshipPaidServiceType.Alchemist    => new TownshipAlchemist(townshipId, serviceId),
            TownshipPaidServiceType.Stablemaster => new TownshipStablemaster(townshipId, serviceId),
            TownshipPaidServiceType.Innkeeper    => new TownshipInnKeeper(townshipId, serviceId),
            _                                    => null
        };

    private static bool IsImplementedServiceNpc(TownshipPaidServiceType type) =>
        type is TownshipPaidServiceType.Banker or
            TownshipPaidServiceType.Mage or
            TownshipPaidServiceType.Alchemist or
            TownshipPaidServiceType.Stablemaster or
            TownshipPaidServiceType.Innkeeper;

    public static int GetServicePurchaseCost(TownshipPaidServiceType type) =>
        type switch
        {
            TownshipPaidServiceType.Banker       => TownshipSettings.BankerPurchaseCost,
            TownshipPaidServiceType.Mage         => TownshipSettings.MagePurchaseCost,
            TownshipPaidServiceType.Alchemist    => TownshipSettings.AlchemistPurchaseCost,
            TownshipPaidServiceType.Stablemaster => TownshipSettings.StablemasterPurchaseCost,
            TownshipPaidServiceType.Innkeeper    => TownshipSettings.InnkeeperPurchaseCost,
            TownshipPaidServiceType.GuardedTown  => TownshipSettings.GuardedTownPurchaseCost,
            TownshipPaidServiceType.HuntingTax   => TownshipSettings.HuntingTaxPurchaseCost,
            _                                    => 0
        };

    public static int GetServiceDailyUpkeep(TownshipPaidServiceType type) =>
        type switch
        {
            TownshipPaidServiceType.Banker       => TownshipSettings.BankerDailyUpkeep,
            TownshipPaidServiceType.Mage         => TownshipSettings.MageDailyUpkeep,
            TownshipPaidServiceType.Alchemist    => TownshipSettings.AlchemistDailyUpkeep,
            TownshipPaidServiceType.Stablemaster => TownshipSettings.StablemasterDailyUpkeep,
            TownshipPaidServiceType.Innkeeper    => TownshipSettings.InnkeeperDailyUpkeep,
            TownshipPaidServiceType.GuardedTown  => TownshipSettings.GuardedTownDailyUpkeep,
            TownshipPaidServiceType.HuntingTax   => TownshipSettings.HuntingTaxDailyUpkeep,
            _                                    => 0
        };

    public static string GetServiceDisplayName(TownshipPaidServiceType type, bool plural) =>
        type switch
        {
            TownshipPaidServiceType.Banker       => plural ? "Township bankers" : "Banker",
            TownshipPaidServiceType.Mage         => plural ? "Township mages" : "Mage",
            TownshipPaidServiceType.Alchemist    => plural ? "Township alchemists" : "Alchemist",
            TownshipPaidServiceType.Stablemaster => plural ? "Township stablemasters" : "Stablemaster",
            TownshipPaidServiceType.Innkeeper    => plural ? "Township innkeepers" : "Innkeeper",
            TownshipPaidServiceType.GuardedTown  => plural ? "Town militia perks" : "Town Militia",
            TownshipPaidServiceType.HuntingTax   => plural ? "Hunting bonus perks" : "Hunting Bonus",
            _                                    => plural ? "Township services" : "Service"
        };

    public static bool HasActiveInnkeeperForHouse(BaseHouse house)
    {
        if (house?.Deleted != false)
        {
            return false;
        }

        var houseSerial = house.Serial;

        for (var i = 0; i < _townships.Count; i++)
        {
            var township = _townships[i];

            if (township?.IsDelinquent == true)
            {
                continue;
            }

            for (var j = 0; j < township.Services.Count; j++)
            {
                var service = township.Services[j];

                if (service.Type != TownshipPaidServiceType.Innkeeper ||
                    service.Status != TownshipPaidServiceStatus.Active ||
                    service.AnchorHouseSerial != houseSerial)
                {
                    continue;
                }

                if (service.CreatedObjectSerial == Serial.Zero ||
                    World.FindMobile(service.CreatedObjectSerial) is not ITownshipServiceNpc npc ||
                    npc is not Mobile mobile ||
                    mobile.Deleted)
                {
                    MarkServiceObjectMissing(township, service, "Township innkeeper NPC no longer exists.");
                    continue;
                }

                return npc.ServiceActive;
            }
        }

        return false;
    }

    public static bool SetServiceRoamRange(
        TownshipState township,
        Mobile from,
        string serviceId,
        int roamRange,
        out string reason
    )
    {
        if (!HasPermission(township, from, TownshipPermission.ManageServiceNpcs))
        {
            reason = "You do not have permission to modify township services.";
            return false;
        }

        var service = FindPaidService(township, serviceId);

        if (service == null || service.Status == TownshipPaidServiceStatus.Removed)
        {
            reason = "That township service no longer exists.";
            return false;
        }

        if (service.CreatedObjectSerial == Serial.Zero ||
            World.FindMobile(service.CreatedObjectSerial) is not ITownshipServiceNpc npc ||
            npc is not Mobile mobile ||
            mobile.Deleted)
        {
            MarkServiceObjectMissing(township, service, "Township service NPC no longer exists.");
            reason = "That service NPC no longer exists and the service was removed.";
            return false;
        }

        roamRange = Math.Clamp(roamRange, 0, MaxTownshipNpcRoamRange);
        service.RoamRange = roamRange;
        service.HomeLocation = mobile.Location;
        service.AnchorHouseSerial = BaseHouse.FindHouseAt(mobile)?.Serial ?? service.AnchorHouseSerial;
        npc.ApplyServiceMovement();

        AddLog(
            township,
            TownshipLogType.StaffAction,
            from,
            $"Set paid service '{service.Name}' roam range to {roamRange:N0} tile{(roamRange == 1 ? "" : "s")}."
        );

        reason = null;
        return true;
    }

    public static bool RelocatePaidServiceNpc(
        TownshipState township,
        Mobile from,
        string serviceId,
        Point3D location,
        Map map,
        out string reason
    )
    {
        if (!HasPermission(township, from, TownshipPermission.ManageServiceNpcs))
        {
            reason = "You do not have permission to modify township services.";
            return false;
        }

        var service = FindPaidService(township, serviceId);

        if (service == null || service.Status == TownshipPaidServiceStatus.Removed)
        {
            reason = "That township service no longer exists.";
            return false;
        }

        if (map != township.Map || !Contains(township, location.X, location.Y))
        {
            reason = "Township service NPCs must be moved within claimed township land.";
            return false;
        }

        var house = BaseHouse.FindHouseAt(location, map, 16);

        if (house == null)
        {
            reason = "Township service NPCs must be moved inside a compatible house within the township.";
            return false;
        }

        if (!IsCompatibleHouse(township.Guild, house))
        {
            reason = "Township service NPCs must be moved inside a guild-member or grandfathered township house.";
            return false;
        }

        if (service.CreatedObjectSerial == Serial.Zero ||
            World.FindMobile(service.CreatedObjectSerial) is not Mobile mobile ||
            mobile.Deleted)
        {
            MarkServiceObjectMissing(township, service, "Township service NPC no longer exists.");
            reason = "That service NPC no longer exists and the service was removed.";
            return false;
        }

        mobile.MoveToWorld(location, map);
        service.HomeLocation = location;
        service.AnchorHouseSerial = house.Serial;

        if (mobile is ITownshipServiceNpc npc)
        {
            npc.ApplyServiceMovement();
        }

        AddLog(
            township,
            TownshipLogType.StaffAction,
            from,
            $"Moved paid service '{service.Name}' ({service.Type}) to {FormatLocation(location, map)}."
        );

        reason = null;
        return true;
    }

    public static bool RenamePaidServiceNpc(
        TownshipState township,
        Mobile from,
        string serviceId,
        string requestedName,
        out string reason
    )
    {
        if (!HasPermission(township, from, TownshipPermission.ManageServiceNpcs))
        {
            reason = "You do not have permission to modify township services.";
            return false;
        }

        var service = FindPaidService(township, serviceId);

        if (service == null || service.Status == TownshipPaidServiceStatus.Removed)
        {
            reason = "That township service no longer exists.";
            return false;
        }

        var name = requestedName.AsSpan().Trim();

        if (!NameVerification.ValidateVendorName(name))
        {
            reason = "That NPC name is not allowed.";
            return false;
        }

        if (service.CreatedObjectSerial == Serial.Zero ||
            World.FindMobile(service.CreatedObjectSerial) is not Mobile mobile ||
            mobile.Deleted)
        {
            MarkServiceObjectMissing(township, service, "Township service NPC no longer exists.");
            reason = "That service NPC no longer exists and the service was removed.";
            return false;
        }

        var cleanName = name.FixHtml();
        service.Name = cleanName;
        mobile.Name = cleanName;
        mobile.InvalidateProperties();

        AddLog(
            township,
            TownshipLogType.StaffAction,
            from,
            $"Renamed paid service NPC '{service.Type}' to '{cleanName}'."
        );

        reason = null;
        return true;
    }

    public static bool SetPaidServiceNpcGender(
        TownshipState township,
        Mobile from,
        string serviceId,
        bool female,
        out string reason
    )
    {
        if (!HasPermission(township, from, TownshipPermission.ManageServiceNpcs))
        {
            reason = "You do not have permission to modify township services.";
            return false;
        }

        var service = FindPaidService(township, serviceId);

        if (service == null || service.Status == TownshipPaidServiceStatus.Removed)
        {
            reason = "That township service no longer exists.";
            return false;
        }

        if (service.CreatedObjectSerial == Serial.Zero ||
            World.FindMobile(service.CreatedObjectSerial) is not Mobile mobile ||
            mobile.Deleted)
        {
            MarkServiceObjectMissing(township, service, "Township service NPC no longer exists.");
            reason = "That service NPC no longer exists and the service was removed.";
            return false;
        }

        if (mobile is ITownshipServiceNpc npc)
        {
            npc.ApplyServiceGender(female);
        }
        else
        {
            if (mobile.IsBodyMod)
            {
                mobile.BodyMod = 0;
            }

            mobile.Body = female ? 0x191 : 0x190;
            mobile.Female = female;

            /* Township service NPCs are human-bodied after this change, so validate
             * gender-specific hair/beard IDs and force a full mobile redraw for clients.
             */
            if (!Race.Human.ValidateHair(female, mobile.HairItemID))
            {
                mobile.HairItemID = 0;
            }

            if (!Race.Human.ValidateFacialHair(female, mobile.FacialHairItemID))
            {
                mobile.FacialHairItemID = 0;
            }

            mobile.ProcessDelta();
            mobile.SendIncomingPacket();
            mobile.InvalidateProperties();
        }

        AddLog(
            township,
            TownshipLogType.StaffAction,
            from,
            $"Changed paid service NPC '{service.Name}' gender to {(female ? "female" : "male")}."
        );

        reason = null;
        return true;
    }

    public static TownshipPaidServiceRecord FindPaidService(TownshipState township, string serviceId)
    {
        if (township == null || string.IsNullOrWhiteSpace(serviceId))
        {
            return null;
        }

        for (var i = 0; i < township.Services.Count; i++)
        {
            if (string.Equals(township.Services[i].Id, serviceId, StringComparison.OrdinalIgnoreCase))
            {
                return township.Services[i];
            }
        }

        return null;
    }

    public static bool RemovePaidService(
        TownshipState township,
        Mobile from,
        string serviceId,
        out TownshipServiceRefundPreview refund,
        out string reason
    )
    {
        refund = null;

        if (!HasPermission(township, from, TownshipPermission.RemoveServices))
        {
            reason = "You do not have permission to remove township services.";
            return false;
        }

        var service = FindPaidService(township, serviceId);

        if (service == null)
        {
            reason = "That township service no longer exists.";
            return false;
        }

        if (service.Status == TownshipPaidServiceStatus.Removed)
        {
            reason = "That township service has already been removed.";
            return false;
        }

        refund = RemovePaidServiceInternal(township, service, from, delinquencyRemoval: false);
        TryPayDelinquentBalance(township, from);
        reason = null;
        return true;
    }

    public static TownshipServiceRefundPreview CalculateServiceRefund(
        int purchaseCost,
        DateTime purchasedAt,
        bool delinquencyRemoval,
        int? requestedRefundPercent = null
    )
    {
        purchaseCost = Math.Max(0, purchaseCost);
        var configuredPercent = requestedRefundPercent ??
            (delinquencyRemoval
                ? TownshipSettings.DefaultDelinquencyServiceRefundPercent
                : TownshipSettings.DefaultVoluntaryServiceRefundPercent);
        var cappedPercent = Math.Clamp(configuredPercent, 0, TownshipSettings.MaxServiceRefundPercent);
        var age = Core.Now >= purchasedAt ? Core.Now - purchasedAt : TimeSpan.Zero;
        var vestingScalar = GetServiceRefundVestingScalar(age);
        var refund = (int)Math.Min(
            int.MaxValue,
            (long)purchaseCost * cappedPercent * vestingScalar / 10000
        );

        return new TownshipServiceRefundPreview
        {
            PurchaseCost = purchaseCost,
            RequestedRefundPercent = configuredPercent,
            CappedRefundPercent = cappedPercent,
            VestingScalarPercent = vestingScalar,
            RefundAmount = refund,
            ServiceAge = age
        };
    }

    private static int GetServiceRefundVestingScalar(TimeSpan age)
    {
        var days = age.TotalDays;

        if (days >= TownshipSettings.ServiceRefundFullVestingDays)
        {
            return 100;
        }

        if (days >= TownshipSettings.ServiceRefundPartialVestingDays)
        {
            return TownshipSettings.ServiceRefundPartialVestingScalarPercent;
        }

        return 0;
    }

    public static void Deposit(TownshipState township, Mobile from, int amount, TownshipDepositSource source, string note)
    {
        if (township == null || amount <= 0)
        {
            return;
        }

        township.TreasuryBalance = AddClamped(township.TreasuryBalance, amount);
        township.LifetimeDeposits = AddClamped(township.LifetimeDeposits, amount);

        township.DepositLog.Insert(0, new TownshipDepositLogEntry
        {
            Timestamp = Core.Now,
            PlayerSerial = from?.Serial ?? Serial.Zero,
            PlayerName = from?.Name ?? "System",
            Source = source,
            Amount = amount,
            Note = note
        });

        Trim(township.DepositLog, TownshipSettings.MaxDepositLogEntries);
        AddLog(township, TownshipLogType.TreasuryDeposit, from, $"{source}: {amount:N0} gold. {note}");
        TryPayDelinquentBalance(township, from);
    }

    public static bool AdjustTreasury(
        TownshipState township,
        Mobile from,
        int delta,
        string playerNote,
        string staffNote,
        out string reason
    )
    {
        reason = null;

        if (township == null)
        {
            reason = "No township is selected.";
            return false;
        }

        if (from?.AccessLevel < AccessLevel.Developer)
        {
            reason = "Treasury adjustments require Developer access.";
            return false;
        }

        if (delta == 0)
        {
            reason = "Enter a non-zero adjustment.";
            return false;
        }

        var nextBalance = (long)township.TreasuryBalance + delta;

        if (nextBalance < 0)
        {
            reason = "That adjustment would make the treasury negative.";
            return false;
        }

        township.TreasuryBalance = (int)Math.Min(int.MaxValue, nextBalance);
        playerNote = CleanNote(playerNote);
        staffNote = CleanNote(staffNote);
        var accountName = from?.Account?.Username ?? "Unknown";

        township.DepositLog.Insert(0, new TownshipDepositLogEntry
        {
            Timestamp = Core.Now,
            PlayerSerial = Serial.Zero,
            PlayerName = "Staff member",
            Source = TownshipDepositSource.StaffAdjustment,
            Amount = delta,
            Note = playerNote
        });

        Trim(township.DepositLog, TownshipSettings.MaxDepositLogEntries);
        AddLog(
            township,
            TownshipLogType.StaffAction,
            from,
            $"Treasury adjusted by {delta:N0} gold. New balance: {township.TreasuryBalance:N0}. Staff: {from?.Name ?? "Unknown"} ({accountName}). Player note: {playerNote}. Staff note: {staffNote}"
        );
        if (delta > 0)
        {
            TryPayDelinquentBalance(township, from);
        }

        return true;
    }

    public static void AddLog(TownshipState township, TownshipLogType type, Mobile actor, string details)
    {
        if (township == null)
        {
            return;
        }

        township.ActivityLog.Insert(0, new TownshipActivityLogEntry
        {
            Timestamp = Core.Now,
            Type = type,
            ActorSerial = actor?.Serial ?? Serial.Zero,
            ActorName = actor?.Name ?? "System",
            Details = details ?? string.Empty
        });

        Trim(township.ActivityLog, TownshipSettings.MaxLogEntries);
        Logger.Information("Township {Township} {Type} by {Actor}: {Details}", township.Name, type, actor?.Name ?? "System", details ?? "");
    }

    private static void AddActivityGainLog(TownshipState township, Mobile actor, int amount)
    {
        if (township == null || actor == null || amount <= 0)
        {
            return;
        }

        var since = Core.Now - TownshipSettings.ActivityLogMergeWindow;

        for (var i = 0; i < township.ActivityLog.Count; i++)
        {
            var entry = township.ActivityLog[i];

            if (entry.Type != TownshipLogType.ActivityGain || entry.ActorSerial != actor.Serial || entry.Timestamp < since)
            {
                continue;
            }

            entry.Timestamp = Core.Now;
            entry.ActorName = actor.Name;
            entry.ActivityAmount = Math.Max(0, entry.ActivityAmount) + amount;
            entry.ActivityTriggerCount = Math.Max(1, entry.ActivityTriggerCount) + 1;
            entry.Details = FormatActivityGainDetails(entry.ActivityTriggerCount, entry.ActivityAmount);

            if (i > 0)
            {
                township.ActivityLog.RemoveAt(i);
                township.ActivityLog.Insert(0, entry);
            }

            Logger.Information("Township {Township} {Type} by {Actor}: {Details}", township.Name, TownshipLogType.ActivityGain, actor.Name, entry.Details);
            return;
        }

        var newEntry = new TownshipActivityLogEntry
        {
            Timestamp = Core.Now,
            Type = TownshipLogType.ActivityGain,
            ActorSerial = actor.Serial,
            ActorName = actor.Name,
            ActivityAmount = amount,
            ActivityTriggerCount = 1,
            Details = FormatActivityGainDetails(1, amount)
        };

        township.ActivityLog.Insert(0, newEntry);
        Trim(township.ActivityLog, TownshipSettings.MaxLogEntries);
        Logger.Information("Township {Township} {Type} by {Actor}: {Details}", township.Name, TownshipLogType.ActivityGain, actor.Name, newEntry.Details);
    }

    private static string FormatActivityGainDetails(int triggerCount, int amount) =>
        triggerCount <= 1
            ? $"Entered township land and added {amount:N0} activity."
            : $"Entered township land {triggerCount:N0} times within the merge window and added {amount:N0} activity.";

    private static bool ValidateStaffAction(TownshipState township, Mobile from, out string reason)
    {
        if (township == null)
        {
            reason = "No township is selected.";
            return false;
        }

        if (!CanUseStaffTools(from))
        {
            reason = "This township action is staff-only.";
            return false;
        }

        reason = null;
        return true;
    }

    public static bool BlocksHousePlacement(Mobile from, Rectangle2D houseFootprint, Map map)
    {
        if (from == null || map == null || map == Map.Internal || from.AccessLevel >= AccessLevel.GameMaster)
        {
            return false;
        }

        var buffered = Expand(houseFootprint, TownshipSettings.HouseBuffer);

        for (var x = buffered.X; x < buffered.X + buffered.Width; x++)
        {
            for (var y = buffered.Y; y < buffered.Y + buffered.Height; y++)
            {
                var township = FindAt(new Point3D(x, y, map.GetAverageZ(x, y)), map);

                if (township != null && !IsGuildMember(from, township.Guild))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(_townships.Count);

        for (var i = 0; i < _townships.Count; i++)
        {
            WriteTownship(writer, _townships[i]);
        }
    }

    public static void Deserialize(IGenericReader reader, int version)
    {
        _townships.Clear();
        _byGuild.Clear();
        _byId.Clear();

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var township = ReadTownship(reader, version);

            if (township?.Guild?.Disbanded == false)
            {
                Register(township);
            }
        }
    }

    private static void Register(TownshipState township)
    {
        NormalizeEnvelopeSize(township);
        NormalizeUpkeepSchedule(township);

        _townships.Add(township);
        _byId[township.Id] = township;

        if (township.Guild != null)
        {
            _byGuild[township.Guild.Serial] = township;
        }

        RebuildRegion(township);
    }

    private static void NormalizeEnvelopeSize(TownshipState township)
    {
        if (township != null && township.MaxEnvelopeSize <= TownshipSettings.InitialClaimSize)
        {
            township.MaxEnvelopeSize = GetInitialEnvelopeSize(TownshipSettings.EnvelopeSizes);
        }
    }

    private static int GetInitialEnvelopeSize(int[] sizes)
    {
        for (var i = 0; i < sizes.Length; i++)
        {
            if (sizes[i] > TownshipSettings.InitialClaimSize)
            {
                return sizes[i];
            }
        }

        return sizes.Length > 0 ? sizes[0] : TownshipSettings.InitialClaimSize;
    }

    private static void Tick()
    {
        for (var i = _townships.Count - 1; i >= 0; i--)
        {
            var township = _townships[i];

            if (township.Guild?.Disbanded != false)
            {
                Disband(township, null, "Owning guild disbanded.");
                continue;
            }

            DecayActivity(township);
            ReconcileServiceObjects(township);
            AssessUpkeep(township);
            ProcessDelinquency(township);
            MaintainPatrolGuards(township);
        }
    }

    private static void Disband(TownshipState township, Mobile actor, string reason)
    {
        AddLog(township, TownshipLogType.Disbanded, actor, reason);
        CleanupTownshipObjects(township);
        TownshipMarkerService.ClearTownship(township);
        township.Region?.Unregister();
        township.Region = null;
        township.Stone?.Delete();
        _townships.Remove(township);
        _byId.Remove(township.Id);
        _byGuild.Remove(township.GuildSerial);
    }

    private static void CleanupTownshipObjects(TownshipState township)
    {
        /* BEGIN CUSTOM TOWNSHIPS: future township-created objects implement ITownshipOwnedObject for centralized cleanup. */
        if (township?.Stone is ITownshipOwnedObject ownedStone)
        {
            ownedStone.OnTownshipDeleted(township);
        }

        if (township != null)
        {
            for (var i = 0; i < township.Services.Count; i++)
            {
                DeleteServiceObject(township.Services[i]);
            }

            DeletePatrolGuards(township);
        }
        /* END CUSTOM TOWNSHIPS */
    }

    private static void OnMovement(MovementEventArgs args)
    {
        var m = args.Mobile;

        if (m?.Player != true || m.Map == null || m.Map == Map.Internal)
        {
            return;
        }

        var township = FindAt(m.Location, m.Map);
        var currentId = township?.Id;
        _lastTownByPlayer.TryGetValue(m.Serial, out var previousId);

        if (string.Equals(currentId, previousId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (previousId != null && FindById(previousId) is { } previous)
        {
            m.SendMessage(0x35, $"You have left {previous.Name}.");
        }

        if (township != null)
        {
            _lastTownByPlayer[m.Serial] = township.Id;
            var guildName = township.Guild?.Name ?? "Unknown";
            var guildAbbrev = township.Guild?.Abbreviation ?? "";
            m.SendMessage(0x35, $"You have entered the {township.ActivityLevel} of {township.Name} ran by the guild {guildAbbrev} - {guildName}.");

            if (township.IsDelinquent)
            {
                if (m.Guild == township.Guild)
                {
                    m.SendMessage(0x22, $"{township.Name} is delinquent. Paid services are suspended until {township.DelinquentBalance:N0} gp is paid.");
                }
                else
                {
                    m.SendMessage(0x35, $"{township.Name} is delinquent. You may donate at the townstone to help restore its services.");
                }
            }

            var delta = m.Guild == township.Guild ? 1 : 2;
            township.ActivityScore = Math.Min(1000, township.ActivityScore + delta);
            AddActivityGainLog(township, m, delta);
        }
        else
        {
            _lastTownByPlayer.Remove(m.Serial);
        }
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnPlayerLogin(PlayerMobile from)
    {
        if (from?.NetState == null || from.Guild is not Guild guild)
        {
            return;
        }

        var township = FindByGuild(guild);

        if (township == null || !guild.IsMember(from))
        {
            return;
        }

        if (township.IsDelinquent)
        {
            Timer.DelayCall(TimeSpan.FromSeconds(2.0), () =>
            {
                if (from.NetState != null && !from.Deleted && township.Guild?.Disbanded == false)
                {
                    from.SendGump(new TownshipDelinquencyWarningGump(township));
                }
            });

            return;
        }

        var daysRemaining = GetEstimatedUpkeepDaysRemaining(township);

        if (daysRemaining >= 3.0)
        {
            return;
        }

        Timer.DelayCall(TimeSpan.FromSeconds(2.0), () =>
        {
            if (from.NetState != null && !from.Deleted && township.Guild?.Disbanded == false)
            {
                from.SendGump(new TownshipUpkeepWarningGump(township));
            }
        });
    }

    private static void DecayActivity(TownshipState township)
    {
        if (Core.Now < township.LastActivityDecay.AddDays(1))
        {
            return;
        }

        township.ActivityScore = Math.Max(0, township.ActivityScore - 25);
        township.LastActivityDecay = Core.Now;
    }

    private static void AssessUpkeep(TownshipState township)
    {
        if (!TownshipSettings.UpkeepEnabled || Core.Now < township.NextUpkeepAssessment)
        {
            return;
        }

        var assessed = 0;
        var safety = 0;

        while (Core.Now >= township.NextUpkeepAssessment && safety++ < 30)
        {
            var amount = GetDailyUpkeep(township);
            township.AccruedUpkeepDue += amount;
            assessed += amount;
            AddLog(township, TownshipLogType.UpkeepAssessed, null, $"Assessed {amount:N0} gold daily upkeep. Accrued due: {township.AccruedUpkeepDue:N0} gp.");
            township.NextUpkeepAssessment = GetNextUpkeepChargeTime(township.NextUpkeepAssessment);
        }

        if (assessed > 0)
        {
            Logger.Information("Township {Township} accrued {Amount} gold upkeep.", township.Name, assessed);
        }

        if (Core.Now >= township.NextWeeklyPayment)
        {
            TryPayAccruedUpkeep(township, null, forced: false, out _);
        }
    }

    private static bool TryPayAccruedUpkeep(TownshipState township, Mobile actor, bool forced, out string reason)
    {
        var due = township.AccruedUpkeepDue;

        if (due <= 0)
        {
            township.NextWeeklyPayment = GetNextUpkeepChargeTime(Core.Now.AddDays(7));
            reason = "This township has no upkeep due.";
            return false;
        }

        if (township.TreasuryBalance < due)
        {
            reason = $"The township treasury cannot cover {due:N0} gp in accrued upkeep.";
            EnterDelinquency(township, due, actor, replaceBalance: false);
            township.AccruedUpkeepDue = 0;
            township.NextWeeklyPayment = GetNextUpkeepChargeTime(Core.Now.AddDays(7));
            AddLog(township, TownshipLogType.UpkeepFailed, actor, $"{(forced ? "[STAFF] Forced upkeep payment failed." : "Unable to pay")} {due:N0} gold accrued upkeep. Township is delinquent.");
            return false;
        }

        township.TreasuryBalance -= due;
        township.AccruedUpkeepDue = 0;
        township.NextWeeklyPayment = GetNextUpkeepChargeTime(Core.Now.AddDays(7));
        AddTreasuryActivity(township, actor, -due, TownshipDepositSource.UpkeepPayment, forced ? "Staff forced current upkeep dues." : "Weekly accrued upkeep payment.");
        AddLog(township, TownshipLogType.UpkeepPaid, actor, $"{(forced ? "[STAFF] Forced payment of" : "Paid")} {due:N0} gold accrued upkeep.");
        reason = null;
        return true;
    }

    private static void EnterDelinquency(TownshipState township, int amount, Mobile actor, bool replaceBalance)
    {
        if (township == null || amount <= 0)
        {
            return;
        }

        var wasDelinquent = township.IsDelinquent;
        township.FinancialStatus = TownshipFinancialStatus.Delinquent;
        township.DelinquentBalance = replaceBalance ? amount : township.DelinquentBalance + amount;
        township.PaidServicesSuspended = true;
        SuspendPaidServices(township);

        if (!wasDelinquent || township.DelinquentSince == DateTime.MinValue)
        {
            township.DelinquentSince = Core.Now;
            township.NextDelinquencyRemoval = Core.Now + TownshipSettings.DelinquencyGracePeriod;
        }

        AddLog(
            township,
            TownshipLogType.UpkeepFailed,
            actor,
            $"Township entered delinquency. Balance due: {township.DelinquentBalance:N0} gp. Paid services suspended until delinquency is paid."
        );
    }

    private static bool TryPayDelinquentBalance(TownshipState township, Mobile actor)
    {
        if (township?.IsDelinquent != true || township.DelinquentBalance <= 0 || township.TreasuryBalance < township.DelinquentBalance)
        {
            return false;
        }

        var amount = township.DelinquentBalance;
        township.TreasuryBalance -= amount;
        AddTreasuryActivity(township, actor, -amount, TownshipDepositSource.UpkeepPayment, "Delinquent upkeep payment.");
        ClearDelinquency(township, actor, $"Paid delinquent balance of {amount:N0} gp. Paid services restored.");
        return true;
    }

    private static void ClearDelinquency(TownshipState township, Mobile actor, string details)
    {
        if (township == null)
        {
            return;
        }

        township.FinancialStatus = TownshipFinancialStatus.Healthy;
        township.DelinquentBalance = 0;
        township.DelinquentSince = DateTime.MinValue;
        township.NextDelinquencyRemoval = DateTime.MinValue;
        township.PaidServicesSuspended = false;
        RestorePaidServices(township);
        AddLog(township, TownshipLogType.UpkeepPaid, actor, details);
    }

    private static void ProcessDelinquency(TownshipState township)
    {
        if (township?.IsDelinquent != true)
        {
            return;
        }

        SuspendPaidServices(township);

        if (township.NextDelinquencyRemoval == DateTime.MinValue)
        {
            township.NextDelinquencyRemoval = Core.Now + TownshipSettings.DelinquencyGracePeriod;
            return;
        }

        var safety = 0;

        while (Core.Now >= township.NextDelinquencyRemoval && safety++ < 10)
        {
            var service = GetNextDelinquencyRemovalService(township);

            if (service == null)
            {
                return;
            }

            RemovePaidServiceInternal(township, service, null, delinquencyRemoval: true);

            if (TryPayDelinquentBalance(township, null))
            {
                return;
            }

            township.NextDelinquencyRemoval += TownshipSettings.DelinquencyRemovalInterval;
        }
    }

    private static void SuspendPaidServices(TownshipState township)
    {
        DeletePatrolGuards(township);

        for (var i = 0; i < township.Services.Count; i++)
        {
            var service = township.Services[i];

            if (service.Status == TownshipPaidServiceStatus.Active)
            {
                service.Status = TownshipPaidServiceStatus.Suspended;
                service.SuspendedAt = Core.Now;
            }
        }
    }

    private static void RestorePaidServices(TownshipState township)
    {
        for (var i = 0; i < township.Services.Count; i++)
        {
            var service = township.Services[i];

            if (service.Status == TownshipPaidServiceStatus.Suspended)
            {
                service.Status = TownshipPaidServiceStatus.Active;
                service.SuspendedAt = DateTime.MinValue;
            }
        }

        MaintainPatrolGuards(township);
    }

    private static TownshipPaidServiceRecord GetNextDelinquencyRemovalService(TownshipState township) =>
        GetDelinquencyRemovalServiceAt(township, 0);

    private static TownshipPaidServiceRecord GetDelinquencyRemovalServiceAt(TownshipState township, int index)
    {
        TownshipPaidServiceRecord selected = null;

        for (var selectedCount = 0; selectedCount <= index; selectedCount++)
        {
            selected = null;

            for (var i = 0; i < township.Services.Count; i++)
            {
                var service = township.Services[i];

                if (service.Status is TownshipPaidServiceStatus.Removed or TownshipPaidServiceStatus.Disabled ||
                    IsEarlierDelinquencyRemovalChoice(township, service, selectedCount))
                {
                    continue;
                }

                if (selected == null ||
                    service.DailyUpkeep > selected.DailyUpkeep ||
                    service.DailyUpkeep == selected.DailyUpkeep && service.PurchasedAt < selected.PurchasedAt)
                {
                    selected = service;
                }
            }

            if (selected == null)
            {
                return null;
            }
        }

        return selected;
    }

    private static bool IsEarlierDelinquencyRemovalChoice(TownshipState township, TownshipPaidServiceRecord service, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (ReferenceEquals(GetDelinquencyRemovalServiceAt(township, i), service))
            {
                return true;
            }
        }

        return false;
    }

    private static TownshipServiceRefundPreview RemovePaidServiceInternal(
        TownshipState township,
        TownshipPaidServiceRecord service,
        Mobile actor,
        bool delinquencyRemoval
    )
    {
        if (service == null || service.Status == TownshipPaidServiceStatus.Removed)
        {
            return CalculateServiceRefund(0, Core.Now, delinquencyRemoval);
        }

        service.Status = TownshipPaidServiceStatus.Removed;
        service.RemovedAt = Core.Now;
        DeleteServiceObject(service);
        var refund = CalculateServiceRefund(service, delinquencyRemoval);

        if (refund.RefundAmount > 0)
        {
            township.TreasuryBalance = AddClamped(township.TreasuryBalance, refund.RefundAmount);
            AddTreasuryActivity(
                township,
                actor,
                refund.RefundAmount,
                TownshipDepositSource.ServiceRefund,
                $"{service.Name} service refund."
            );
        }

        AddLog(
            township,
            delinquencyRemoval ? TownshipLogType.UpkeepFailed : TownshipLogType.StaffAction,
            actor,
            $"{GetServiceRemovalPrefix(actor, delinquencyRemoval)} paid service '{service.Name}' ({service.Type}). Refund: {refund.RefundAmount:N0} gp."
        );

        if (service.Type == TownshipPaidServiceType.GuardedTown)
        {
            RebuildRegion(township);
            DeletePatrolGuards(township);
        }

        return refund;
    }

    private static string GetServiceRemovalPrefix(Mobile actor, bool delinquencyRemoval)
    {
        if (delinquencyRemoval)
        {
            return "Delinquency removed";
        }

        if (actor?.AccessLevel >= AccessLevel.GameMaster)
        {
            return $"[STAFF] Removed ({FormatStaffActor(actor)})";
        }

        return $"Township manager {actor?.Name ?? "Unknown"} removed";
    }

    private static void DeleteServiceObject(TownshipPaidServiceRecord service)
    {
        if (service?.CreatedObjectSerial == Serial.Zero)
        {
            return;
        }

        var mobile = World.FindMobile(service.CreatedObjectSerial);

        if (mobile?.Deleted == false)
        {
            mobile.Delete();
        }

        service.CreatedObjectSerial = Serial.Zero;
    }

    public static void MarkServiceObjectMissing(string townshipId, string serviceId, string reason)
    {
        var township = FindById(townshipId);
        MarkServiceObjectMissing(township, FindPaidService(township, serviceId), reason);
    }

    private static void MarkServiceObjectMissing(
        TownshipState township,
        TownshipPaidServiceRecord service,
        string reason
    )
    {
        if (township == null || service == null || service.Status == TownshipPaidServiceStatus.Removed)
        {
            return;
        }

        service.Status = TownshipPaidServiceStatus.Removed;
        service.RemovedAt = Core.Now;
        service.CreatedObjectSerial = Serial.Zero;

        AddLog(
            township,
            TownshipLogType.StaffAction,
            null,
            $"Paid service '{service.Name}' ({service.Type}) was removed because its NPC is missing. {reason}"
        );
    }

    private static void ReconcileServiceObjects(TownshipState township)
    {
        if (township == null)
        {
            return;
        }

        for (var i = 0; i < township.Services.Count; i++)
        {
            var service = township.Services[i];

            if (service.Status == TownshipPaidServiceStatus.Removed || service.CreatedObjectSerial == Serial.Zero)
            {
                continue;
            }

            var mobile = World.FindMobile(service.CreatedObjectSerial);

            if (mobile?.Deleted != false)
            {
                MarkServiceObjectMissing(township, service, "Periodic reconciliation could not find the NPC.");
            }
        }
    }

    private static void AddTreasuryActivity(TownshipState township, Mobile from, int amount, TownshipDepositSource source, string note)
    {
        township.DepositLog.Insert(0, new TownshipDepositLogEntry
        {
            Timestamp = Core.Now,
            PlayerSerial = from?.Serial ?? Serial.Zero,
            PlayerName = from?.Name ?? "System",
            Source = source,
            Amount = amount,
            Note = note,
            AggregateCount = 0
        });

        Trim(township.DepositLog, TownshipSettings.MaxDepositLogEntries);
    }

    public static void AddAutomatedTreasuryRevenue(
        TownshipState township,
        Mobile from,
        int amount,
        TownshipDepositSource source,
        string note,
        string details = null
    )
    {
        if (township == null || amount <= 0)
        {
            return;
        }

        township.TreasuryBalance = AddClamped(township.TreasuryBalance, amount);
        township.LifetimeDeposits = AddClamped(township.LifetimeDeposits, amount);

        var key = GetTreasuryAggregateKey(source, Core.Now);
        var entry = FindTreasuryAggregate(township, key);

        if (entry == null)
        {
            entry = new TownshipDepositLogEntry
            {
                Timestamp = Core.Now,
                PlayerSerial = Serial.Zero,
                PlayerName = "System",
                Source = source,
                Amount = 0,
                Note = GetTreasuryAggregateNote(source),
                AggregateKey = key,
                AggregateCount = 0
            };

            township.DepositLog.Insert(0, entry);
            Trim(township.DepositLog, TownshipSettings.MaxDepositLogEntries);
        }

        entry.Timestamp = Core.Now;
        entry.Amount = AddClamped(entry.Amount, amount);
        entry.AggregateCount = AddClamped(entry.AggregateCount, 1);

        township.TreasuryContributions.Insert(0, new TownshipTreasuryContributionEntry
        {
            Timestamp = Core.Now,
            PlayerSerial = from?.Serial ?? Serial.Zero,
            PlayerName = from?.Name ?? "System",
            Source = source,
            Amount = amount,
            Note = CleanNote(note),
            Details = CleanDetails(details),
            AggregateKey = key
        });

        Trim(township.TreasuryContributions, TownshipSettings.MaxDepositLogEntries);
    }

    private static TownshipDepositLogEntry FindTreasuryAggregate(TownshipState township, string key)
    {
        for (var i = 0; i < township.DepositLog.Count; i++)
        {
            var entry = township.DepositLog[i];

            if (entry.AggregateKey == key)
            {
                return entry;
            }
        }

        return null;
    }

    private static string GetTreasuryAggregateKey(TownshipDepositSource source, DateTime timestamp) =>
        $"{source}:{timestamp:yyyyMMdd}";

    private static string GetTreasuryAggregateNote(TownshipDepositSource source) => source switch
    {
        TownshipDepositSource.HuntingTax => "Daily hunting bonus total.",
        TownshipDepositSource.EscortRevenue => "Daily escort revenue total.",
        TownshipDepositSource.VendorRevenue => "Daily township NPC vendor revenue total.",
        _ => "Daily treasury revenue total."
    };

    private static int AddClamped(int value, int amount) =>
        amount <= 0 ? value : (int)Math.Min(int.MaxValue, (long)Math.Max(0, value) + amount);

    private static string GetCreatureTypeName(BaseCreature creature)
    {
        if (!string.IsNullOrWhiteSpace(creature?.DefaultName))
        {
            return StripArticle(creature.DefaultName.Trim());
        }

        var typeName = creature?.GetType().Name ?? "creature";
        using var builder = ValueStringBuilder.Create(typeName.Length + 8);

        for (var i = 0; i < typeName.Length; i++)
        {
            var c = typeName[i];

            if (i > 0 && char.IsUpper(c) && (char.IsLower(typeName[i - 1]) || i + 1 < typeName.Length && char.IsLower(typeName[i + 1])))
            {
                builder.Append(' ');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    private static string StripArticle(string value)
    {
        if (value.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
        {
            return value[2..];
        }

        if (value.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            return value[3..];
        }

        if (value.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            return value[4..];
        }

        return value;
    }

    private static void NormalizeUpkeepSchedule(TownshipState township)
    {
        if (township == null)
        {
            return;
        }

        township.NextUpkeepAssessment = NormalizeUpkeepChargeTime(township.NextUpkeepAssessment);
        township.NextWeeklyPayment = NormalizeUpkeepChargeTime(township.NextWeeklyPayment);
    }

    private static DateTime NormalizeUpkeepChargeTime(DateTime value)
    {
        if (value.TimeOfDay == UpkeepChargeTime)
        {
            return value;
        }

        if (value == DateTime.MinValue)
        {
            return GetNextUpkeepChargeTime(Core.Now);
        }

        return value.Date + UpkeepChargeTime;
    }

    private static DateTime GetNextUpkeepChargeTime(DateTime from)
    {
        var next = from.Date + UpkeepChargeTime;

        if (next <= from)
        {
            next = next.AddDays(1);
        }

        return next;
    }

    private static bool ValidateRegionRules(Guild guild, Rectangle2D area, Map map, out string reason)
    {
        reason = null;

        for (var x = area.X; x < area.X + area.Width; x++)
        {
            for (var y = area.Y; y < area.Y + area.Height; y++)
            {
                var point = new Point3D(x, y, map.GetAverageZ(x, y));
                var region = Region.Find(point, map);

                if (!AllowsTownshipClaim(region, guild, point, map, out var blockReason))
                {
                    reason = $"The selected area crosses a blocked region at {map.Name} ({x}, {y}): {blockReason}.";
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasBlockingHouse(Guild guild, Rectangle2D area, Map map, bool allowGrandfathered, out string reason)
    {
        reason = null;
        var buffered = Expand(area, TownshipSettings.HouseBuffer);

        foreach (var house in map.GetMultisInBounds<BaseHouse>(buffered, true))
        {
            if (house?.Deleted != false)
            {
                continue;
            }

            if (IsCompatibleHouse(guild, house))
            {
                continue;
            }

            if (HouseIntersects(house, area, TownshipSettings.HouseBuffer))
            {
                reason = "A non-guild house blocks the selected area or its township safety buffer.";
                return true;
            }
        }

        return false;
    }

    private static bool IsCompatibleHouse(Guild guild, BaseHouse house) =>
        guild?.Disbanded == false && house?.Owner?.Guild == guild;

    public static bool IsTownshipNpcInTownshipHouse(Mobile mobile, BaseHouse house)
    {
        if (mobile?.Deleted != false ||
            mobile is not ITownshipOwnedObject owned ||
            house?.Deleted != false)
        {
            return false;
        }

        var township = FindById(owned.TownshipId);

        return township?.Map == house.Map && IsTownshipHouse(township, house);
    }

    private static bool IsTownshipHouse(TownshipState township, BaseHouse house)
    {
        if (township == null || house?.Deleted != false)
        {
            return false;
        }

        for (var i = 0; i < township.Houses.Count; i++)
        {
            if (township.Houses[i].HouseSerial == house.Serial)
            {
                return true;
            }
        }

        return IsCompatibleHouse(township.Guild, house) && HouseIntersectsClaimedLand(township, house);
    }

    private static bool HouseIntersectsClaimedLand(TownshipState township, BaseHouse house)
    {
        var houseArea = house.Area;

        for (var i = 0; i < houseArea.Length; i++)
        {
            var houseRect = new Rectangle2D(
                house.X + houseArea[i].X,
                house.Y + houseArea[i].Y,
                houseArea[i].Width,
                houseArea[i].Height
            );

            for (var j = 0; j < township.Claims.Count; j++)
            {
                var claim = township.Claims[j];

                if (claim.Y >= houseRect.Y &&
                    claim.Y < houseRect.Y + houseRect.Height &&
                    claim.StartX < houseRect.X + houseRect.Width &&
                    claim.EndX >= houseRect.X)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AllowsTownshipClaim(Region region, Guild guild, Point3D point, Map map, out string reason)
    {
        reason = null;

        if (region == null)
        {
            return true;
        }

        var houseRegion = region.GetRegion<HouseRegion>();

        if (houseRegion != null)
        {
            if (IsCompatibleHouse(guild, houseRegion.House))
            {
                return true;
            }

            reason = $"non-guild house region ({region.GetType().Name})";
            return false;
        }

        if (region.IsPartOf<GuardedRegion, DungeonRegion>() ||
            region.IsPartOf<NoHousingRegion, NoHousingGuardedRegion>() ||
            region.IsPartOf<TempNoHousingRegion>() ||
            region.IsPartOf<HouseRaffleRegion>())
        {
            reason = region.GetType().Name;
            return false;
        }

        return true;
    }

    private static bool IsTileClaimable(TownshipState township, int x, int y, out string reason)
    {
        reason = null;

        if (Contains(township, x, y))
        {
            reason = "Already claimed";
            return false;
        }

        var existingTownship = FindAt(new Point3D(x, y, township.Map.GetAverageZ(x, y)), township.Map);

        if (existingTownship != null && existingTownship != township)
        {
            reason = "Other township";
            return false;
        }

        if (!GetEnvelope(township).Contains(x, y))
        {
            reason = "Outside max border range";
            return false;
        }

        var point = new Point3D(x, y, township.Map.GetAverageZ(x, y));

        if (!AllowsTownshipClaim(Region.Find(point, township.Map), township.Guild, point, township.Map, out var blockReason))
        {
            reason = blockReason ?? "Blocked region";
            return false;
        }

        if (HasBlockingHouse(township.Guild, new Rectangle2D(x, y, 1, 1), township.Map, allowGrandfathered: true, out _))
        {
            reason = "Private house buffer";
            return false;
        }

        reason = "Valid";
        return true;
    }

    private static int CountSharedEdges(TownshipState township, List<TownshipClaimRange> ranges)
    {
        var count = 0;

        for (var i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i];

            for (var x = range.StartX; x <= range.EndX; x++)
            {
                if (Contains(township, x - 1, range.Y))
                {
                    count++;
                }

                if (Contains(township, x + 1, range.Y))
                {
                    count++;
                }

                if (Contains(township, x, range.Y - 1))
                {
                    count++;
                }

                if (Contains(township, x, range.Y + 1))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static void AddRectangleClaim(TownshipState township, Rectangle2D rect)
    {
        for (var y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            township.Claims.Add(new TownshipClaimRange
            {
                Y = y,
                StartX = rect.X,
                EndX = rect.X + rect.Width - 1
            });
        }

        MergeClaims(township);
    }

    private static void MergeClaims(TownshipState township)
    {
        township.Claims.Sort(static (a, b) =>
        {
            var y = a.Y.CompareTo(b.Y);
            return y != 0 ? y : a.StartX.CompareTo(b.StartX);
        });

        for (var i = township.Claims.Count - 2; i >= 0; i--)
        {
            var current = township.Claims[i];
            var next = township.Claims[i + 1];

            if (current.Y == next.Y && current.EndX + 1 >= next.StartX)
            {
                current.EndX = Math.Max(current.EndX, next.EndX);
                township.Claims.RemoveAt(i + 1);
            }
        }
    }

    private static void RecordCompatibleHouses(TownshipState township, Rectangle2D area)
    {
        foreach (var house in township.Map.GetMultisInBounds<BaseHouse>(Expand(area, TownshipSettings.HouseBuffer), true))
        {
            if (house?.Deleted != false || house.Owner == null || !IsCompatibleHouse(township.Guild, house))
            {
                continue;
            }

            var exists = false;

            for (var i = 0; i < township.Houses.Count; i++)
            {
                if (township.Houses[i].HouseSerial == house.Serial)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                township.Houses.Add(new TownshipHouseRecord
                {
                    HouseSerial = house.Serial,
                    OwnerSerial = house.Owner.Serial,
                    OwnerName = house.Owner.Name,
                    WasGuildMemberAtClaim = true
                });
            }
        }
    }

    private static bool HouseIntersects(BaseHouse house, Rectangle2D rect, int buffer)
    {
        var houseArea = house.Area;

        for (var i = 0; i < houseArea.Length; i++)
        {
            var houseRect = new Rectangle2D(
                house.X + houseArea[i].X - buffer,
                house.Y + houseArea[i].Y - buffer,
                houseArea[i].Width + buffer * 2,
                houseArea[i].Height + buffer * 2
            );

            if (Intersects(houseRect, rect))
            {
                return true;
            }
        }

        return false;
    }

    private static Rectangle2D CenteredRect(Point3D center, int size)
    {
        var half = size / 2;
        return new Rectangle2D(center.X - half, center.Y - half, size, size);
    }

    private static Rectangle2D Expand(Rectangle2D rect, int amount) =>
        new(rect.X - amount, rect.Y - amount, rect.Width + amount * 2, rect.Height + amount * 2);

    private static bool Intersects(Rectangle2D a, Rectangle2D b) =>
        a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;

    private static bool RectangleContains(Rectangle2D outer, Rectangle2D inner) =>
        outer.Contains(inner.X, inner.Y) &&
        outer.Contains(inner.X + inner.Width - 1, inner.Y + inner.Height - 1);

    private static bool IntersectsExistingTownship(TownshipState source, Rectangle2D rect, Map map)
    {
        for (var x = rect.X; x < rect.X + rect.Width; x++)
        {
            for (var y = rect.Y; y < rect.Y + rect.Height; y++)
            {
                var township = FindAt(new Point3D(x, y, map.GetAverageZ(x, y)), map);

                if (township != null && township != source)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AddInvalidReason(TownshipExpansionPreview preview, string reason)
    {
        reason ??= "Unknown";
        preview.InvalidReasons.TryGetValue(reason, out var count);
        preview.InvalidReasons[reason] = count + 1;
    }

    private static void AddToPendingRange(
        ref int start,
        ref int end,
        ref int rowY,
        int x,
        int y,
        List<TownshipClaimRange> ranges
    )
    {
        if (start == int.MinValue)
        {
            start = x;
            end = x;
            rowY = y;
            return;
        }

        if (rowY == y && end + 1 == x)
        {
            end = x;
            return;
        }

        FlushRange(ranges, ref start, ref end, ref rowY);
        start = x;
        end = x;
        rowY = y;
    }

    private static void FlushRange(List<TownshipClaimRange> ranges, ref int start, ref int end, ref int y)
    {
        if (start == int.MinValue)
        {
            return;
        }

        ranges.Add(new TownshipClaimRange { Y = y, StartX = start, EndX = end });
        start = int.MinValue;
        end = int.MinValue;
    }

    private static Rectangle2D[] BuildRegionRectangles(TownshipState township)
    {
        var rects = new List<Rectangle2D>();
        var claims = township.Claims;

        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i];
            var height = 1;

            while (i + 1 < claims.Count &&
                   claims[i + 1].Y == claim.Y + height &&
                   claims[i + 1].StartX == claim.StartX &&
                   claims[i + 1].EndX == claim.EndX)
            {
                height++;
                i++;
            }

            rects.Add(new Rectangle2D(claim.StartX, claim.Y, claim.TileCount, height));
        }

        return rects.ToArray();
    }

    private static void Trim<T>(List<T> list, int max)
    {
        while (list.Count > max)
        {
            list.RemoveAt(list.Count - 1);
        }
    }

    private static int CountGold(Container container)
    {
        if (container == null)
        {
            return 0;
        }

        var total = 0;

        for (var i = 0; i < container.Items.Count; i++)
        {
            var item = container.Items[i];

            if (item is Gold gold)
            {
                total += gold.Amount;
            }
            else if (item is Container child)
            {
                total += CountGold(child);
            }
        }

        return total;
    }

    private static int ConsumeGold(Container container, int amount)
    {
        if (container == null || amount <= 0)
        {
            return 0;
        }

        var removed = 0;

        for (var i = container.Items.Count - 1; i >= 0 && removed < amount; i--)
        {
            var item = container.Items[i];

            if (item is Gold gold)
            {
                var take = Math.Min(gold.Amount, amount - removed);
                gold.Consume(take);
                removed += take;
            }
            else if (item is Container child)
            {
                removed += ConsumeGold(child, amount - removed);
            }
        }

        return removed;
    }

    private static string CleanNote(string note)
    {
        note = note?.Trim();

        if (string.IsNullOrWhiteSpace(note))
        {
            return "No note provided.";
        }

        return note.Length <= 160 ? note : note[..160];
    }

    private static string CleanDetails(string details)
    {
        details = details?.Trim();

        if (string.IsNullOrWhiteSpace(details))
        {
            return null;
        }

        return details.Length <= 2000 ? details : details[..2000];
    }

    private static string FormatStaffActor(Mobile from) =>
        $"Staff: {from?.Name ?? "Unknown"} ({from?.Account?.Username ?? "Unknown"}).";

    private static string FormatLocation(Point3D p, Map map) => $"{map?.Name ?? "Internal"} ({p.X}, {p.Y}, {p.Z})";

    private static void WriteTownship(IGenericWriter writer, TownshipState t)
    {
        writer.Write(t.Id);
        writer.Write(t.Name);
        writer.Write(t.Guild);
        writer.Write(t.GuildSerial);
        writer.Write(t.FoundingPoint);
        writer.Write(t.Map);
        writer.Write(t.FoundedAt);
        writer.Write(t.Stone);
        writer.WriteEncodedInt(t.MaxEnvelopeSize);
        writer.WriteEncodedInt(t.TreasuryBalance);
        writer.WriteEncodedInt(t.LifetimeDeposits);
        writer.WriteEncodedInt(t.ActivityScore);
        writer.Write(t.LastActivityDecay);
        writer.Write(t.NextUpkeepAssessment);
        writer.Write(t.NextWeeklyPayment);
        writer.WriteEncodedInt(t.AccruedUpkeepDue);
        writer.WriteEncodedInt((int)t.FinancialStatus);
        writer.WriteEncodedInt(t.DelinquentBalance);
        writer.Write(t.DelinquentSince);
        writer.Write(t.NextDelinquencyRemoval);
        writer.Write(t.PaidServicesSuspended);
        writer.WriteEncodedInt((int)t.HuntingTaxMode);
        writer.WriteEncodedInt(t.HuntingTaxPercent);

        writer.WriteEncodedInt(t.PatrolGuardSerials.Count);
        for (var i = 0; i < t.PatrolGuardSerials.Count; i++)
        {
            writer.Write(t.PatrolGuardSerials[i]);
        }

        writer.WriteEncodedInt(t.Claims.Count);
        for (var i = 0; i < t.Claims.Count; i++)
        {
            writer.WriteEncodedInt(t.Claims[i].Y);
            writer.WriteEncodedInt(t.Claims[i].StartX);
            writer.WriteEncodedInt(t.Claims[i].EndX);
        }

        writer.WriteEncodedInt(t.Houses.Count);
        for (var i = 0; i < t.Houses.Count; i++)
        {
            writer.Write(t.Houses[i].HouseSerial);
            writer.Write(t.Houses[i].OwnerSerial);
            writer.Write(t.Houses[i].OwnerName);
            writer.Write(t.Houses[i].WasGuildMemberAtClaim);
            writer.Write(t.Houses[i].ResidentLease);
        }

        writer.WriteEncodedInt(t.Services.Count);
        for (var i = 0; i < t.Services.Count; i++)
        {
            writer.Write(t.Services[i].Id);
            writer.WriteEncodedInt((int)t.Services[i].Type);
            writer.WriteEncodedInt((int)t.Services[i].Status);
            writer.Write(t.Services[i].Name);
            writer.WriteEncodedInt(t.Services[i].PurchaseCost);
            writer.WriteEncodedInt(t.Services[i].DailyUpkeep);
            writer.Write(t.Services[i].PurchasedAt);
            writer.Write(t.Services[i].SuspendedAt);
            writer.Write(t.Services[i].RemovedAt);
            writer.Write(t.Services[i].CreatedObjectSerial);
            writer.Write(t.Services[i].AnchorHouseSerial);
            writer.Write(t.Services[i].HomeLocation);
            writer.WriteEncodedInt(t.Services[i].RoamRange);
            writer.Write(t.Services[i].Notes);
        }

        writer.WriteEncodedInt(t.HuntingTaxPreferences.Count);
        for (var i = 0; i < t.HuntingTaxPreferences.Count; i++)
        {
            writer.Write(t.HuntingTaxPreferences[i].PlayerSerial);
            writer.Write(t.HuntingTaxPreferences[i].OptedIn);
            writer.Write(t.HuntingTaxPreferences[i].Prompted);
        }

        writer.WriteEncodedInt(t.RankAssignments.Count);
        for (var i = 0; i < t.RankAssignments.Count; i++)
        {
            writer.Write(t.RankAssignments[i].PlayerSerial);
            writer.Write(t.RankAssignments[i].PlayerName);
            writer.WriteEncodedInt((int)t.RankAssignments[i].Rank);
        }

        writer.WriteEncodedInt(t.DepositLog.Count);
        for (var i = 0; i < t.DepositLog.Count; i++)
        {
            writer.Write(t.DepositLog[i].Timestamp);
            writer.Write(t.DepositLog[i].PlayerSerial);
            writer.Write(t.DepositLog[i].PlayerName);
            writer.WriteEncodedInt((int)t.DepositLog[i].Source);
            writer.WriteEncodedInt(t.DepositLog[i].Amount);
            writer.Write(t.DepositLog[i].Note);
            writer.Write(t.DepositLog[i].AggregateKey);
            writer.WriteEncodedInt(t.DepositLog[i].AggregateCount);
        }

        writer.WriteEncodedInt(t.TreasuryContributions.Count);
        for (var i = 0; i < t.TreasuryContributions.Count; i++)
        {
            writer.Write(t.TreasuryContributions[i].Timestamp);
            writer.Write(t.TreasuryContributions[i].PlayerSerial);
            writer.Write(t.TreasuryContributions[i].PlayerName);
            writer.WriteEncodedInt((int)t.TreasuryContributions[i].Source);
            writer.WriteEncodedInt(t.TreasuryContributions[i].Amount);
            writer.Write(t.TreasuryContributions[i].Note);
            writer.Write(t.TreasuryContributions[i].Details);
            writer.Write(t.TreasuryContributions[i].AggregateKey);
        }

        writer.WriteEncodedInt(t.ActivityLog.Count);
        for (var i = 0; i < t.ActivityLog.Count; i++)
        {
            writer.Write(t.ActivityLog[i].Timestamp);
            writer.WriteEncodedInt((int)t.ActivityLog[i].Type);
            writer.Write(t.ActivityLog[i].ActorSerial);
            writer.Write(t.ActivityLog[i].ActorName);
            writer.Write(t.ActivityLog[i].Details);
            writer.WriteEncodedInt(t.ActivityLog[i].ActivityAmount);
            writer.WriteEncodedInt(t.ActivityLog[i].ActivityTriggerCount);
        }
    }

    private static TownshipState ReadTownship(IGenericReader reader, int version)
    {
        var t = new TownshipState
        {
            Id = reader.ReadString(),
            Name = reader.ReadString(),
            Guild = reader.ReadEntity<Guild>(),
            GuildSerial = reader.ReadSerial(),
            FoundingPoint = reader.ReadPoint3D(),
            Map = reader.ReadMap(),
            FoundedAt = reader.ReadDateTime(),
            Stone = reader.ReadEntity<TownshipStone>(),
            MaxEnvelopeSize = reader.ReadEncodedInt(),
            TreasuryBalance = reader.ReadEncodedInt(),
            LifetimeDeposits = reader.ReadEncodedInt(),
            ActivityScore = reader.ReadEncodedInt(),
            LastActivityDecay = reader.ReadDateTime(),
            NextUpkeepAssessment = reader.ReadDateTime(),
            NextWeeklyPayment = reader.ReadDateTime(),
            AccruedUpkeepDue = version >= 2 ? reader.ReadEncodedInt() : 0,
            FinancialStatus = version >= 3 ? (TownshipFinancialStatus)reader.ReadEncodedInt() : TownshipFinancialStatus.Healthy,
            DelinquentBalance = version >= 3 ? reader.ReadEncodedInt() : 0,
            DelinquentSince = version >= 3 ? reader.ReadDateTime() : DateTime.MinValue,
            NextDelinquencyRemoval = version >= 3 ? reader.ReadDateTime() : DateTime.MinValue,
            PaidServicesSuspended = version >= 3 && reader.ReadBool(),
            HuntingTaxMode = version >= 6 ? (TownshipHuntingTaxMode)reader.ReadEncodedInt() : TownshipHuntingTaxMode.OptIn,
            HuntingTaxPercent = version >= 6 ? reader.ReadEncodedInt() : 0
        };

        if (version >= 7)
        {
            var guardCount = reader.ReadEncodedInt();

            for (var i = 0; i < guardCount; i++)
            {
                t.PatrolGuardSerials.Add(reader.ReadSerial());
            }
        }

        var claimCount = reader.ReadEncodedInt();
        for (var i = 0; i < claimCount; i++)
        {
            t.Claims.Add(new TownshipClaimRange
            {
                Y = reader.ReadEncodedInt(),
                StartX = reader.ReadEncodedInt(),
                EndX = reader.ReadEncodedInt()
            });
        }

        var houseCount = reader.ReadEncodedInt();
        for (var i = 0; i < houseCount; i++)
        {
            t.Houses.Add(new TownshipHouseRecord
            {
                HouseSerial = reader.ReadSerial(),
                OwnerSerial = reader.ReadSerial(),
                OwnerName = reader.ReadString(),
                WasGuildMemberAtClaim = reader.ReadBool(),
                ResidentLease = reader.ReadBool()
            });
        }

        if (version >= 4)
        {
            var serviceCount = reader.ReadEncodedInt();
            for (var i = 0; i < serviceCount; i++)
            {
                var service = new TownshipPaidServiceRecord
                {
                    Id = reader.ReadString(),
                    Type = (TownshipPaidServiceType)reader.ReadEncodedInt(),
                    Status = (TownshipPaidServiceStatus)reader.ReadEncodedInt(),
                    Name = reader.ReadString(),
                    PurchaseCost = reader.ReadEncodedInt(),
                    DailyUpkeep = reader.ReadEncodedInt(),
                    PurchasedAt = reader.ReadDateTime(),
                    SuspendedAt = reader.ReadDateTime(),
                    RemovedAt = reader.ReadDateTime(),
                    CreatedObjectSerial = reader.ReadSerial(),
                    AnchorHouseSerial = version >= 5 ? reader.ReadSerial() : Serial.Zero,
                    HomeLocation = version >= 5 ? reader.ReadPoint3D() : Point3D.Zero,
                    RoamRange = version >= 5 ? reader.ReadEncodedInt() : 0,
                    Notes = reader.ReadString()
                };

                t.Services.Add(service);
            }

            if (version >= 6)
            {
                var preferenceCount = reader.ReadEncodedInt();

                for (var i = 0; i < preferenceCount; i++)
                {
                    t.HuntingTaxPreferences.Add(new TownshipHuntingTaxPreference
                    {
                        PlayerSerial = reader.ReadSerial(),
                        OptedIn = reader.ReadBool(),
                        Prompted = reader.ReadBool()
                    });
                }
            }
        }

        if (version >= 9)
        {
            var rankCount = reader.ReadEncodedInt();

            for (var i = 0; i < rankCount; i++)
            {
                t.RankAssignments.Add(new TownshipRankAssignment
                {
                    PlayerSerial = reader.ReadSerial(),
                    PlayerName = reader.ReadString(),
                    Rank = (TownshipRankLevel)reader.ReadEncodedInt()
                });
            }
        }

        var depositCount = reader.ReadEncodedInt();
        for (var i = 0; i < depositCount; i++)
        {
            t.DepositLog.Add(new TownshipDepositLogEntry
            {
                Timestamp = reader.ReadDateTime(),
                PlayerSerial = reader.ReadSerial(),
                PlayerName = reader.ReadString(),
                Source = (TownshipDepositSource)reader.ReadEncodedInt(),
                Amount = reader.ReadEncodedInt(),
                Note = reader.ReadString(),
                AggregateKey = version >= 8 ? reader.ReadString() : null,
                AggregateCount = version >= 8 ? reader.ReadEncodedInt() : 0
            });
        }

        if (version >= 8)
        {
            var contributionCount = reader.ReadEncodedInt();
            for (var i = 0; i < contributionCount; i++)
            {
                t.TreasuryContributions.Add(new TownshipTreasuryContributionEntry
                {
                    Timestamp = reader.ReadDateTime(),
                    PlayerSerial = reader.ReadSerial(),
                    PlayerName = reader.ReadString(),
                    Source = (TownshipDepositSource)reader.ReadEncodedInt(),
                    Amount = reader.ReadEncodedInt(),
                    Note = reader.ReadString(),
                    Details = version >= 10 ? reader.ReadString() : null,
                    AggregateKey = reader.ReadString()
                });
            }
        }

        var logCount = reader.ReadEncodedInt();
        for (var i = 0; i < logCount; i++)
        {
            t.ActivityLog.Add(new TownshipActivityLogEntry
            {
                Timestamp = reader.ReadDateTime(),
                Type = (TownshipLogType)reader.ReadEncodedInt(),
                ActorSerial = reader.ReadSerial(),
                ActorName = reader.ReadString(),
                Details = reader.ReadString(),
                ActivityAmount = version >= 1 ? reader.ReadEncodedInt() : 0,
                ActivityTriggerCount = version >= 1 ? reader.ReadEncodedInt() : 0
            });
        }

        return t;
    }
}
