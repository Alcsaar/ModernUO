using System;
using System.Collections.Generic;
using Server.Guilds;

namespace Server.Custom.Systems.Townships;

public enum TownshipActivityLevel
{
    Camp,
    Hamlet,
    Village,
    Town,
    City
}

public enum TownshipLogType
{
    Founded,
    Disbanded,
    StoneMoved,
    ExpansionPreview,
    ExpansionPurchased,
    TreasuryDeposit,
    UpkeepAssessed,
    UpkeepPaid,
    UpkeepFailed,
    ConfigChanged,
    MarkerViewed,
    StaffAction,
    ActivityGain
}

public enum TownshipDepositSource
{
    PlayerDeposit,
    VendorRevenue,
    EscortRevenue,
    StaffAdjustment,
    UpkeepPayment,
    ServiceRefund,
    ServicePurchase,
    HuntingTax
}

public enum TownshipFinancialStatus
{
    Healthy,
    Delinquent
}

public enum TownshipPaidServiceType
{
    Banker,
    Vendor,
    Stablemaster,
    Moongate,
    Guard,
    Perk,
    Mage,
    Alchemist,
    Innkeeper,
    GuardedTown,
    HuntingTax,
    Other
}

public enum TownshipHuntingTaxMode
{
    OptIn,
    Required
}

public enum TownshipPaidServiceStatus
{
    Active,
    Suspended,
    Removed,
    Disabled
}

public enum TownshipRankLevel
{
    Citizen,
    Aide,
    Officer,
    Councilor,
    Regent
}

[Flags]
public enum TownshipPermission
{
    None = 0,
    ManageServiceNpcs = 1 << 0,
    PurchaseServices = 1 << 1,
    RemoveServices = 1 << 2,
    PurchasePerks = 1 << 3,
    TogglePerks = 1 << 4,
    ExpandTerritory = 1 << 5,
    MoveStone = 1 << 6,
    RenameTownship = 1 << 7,
    ManageRanks = 1 << 8,
    AbolishTownship = 1 << 9
}

public sealed class TownshipClaimRange
{
    public int Y { get; set; }
    public int StartX { get; set; }
    public int EndX { get; set; }

    public int TileCount => Math.Max(0, EndX - StartX + 1);

    public bool Contains(int x, int y) => Y == y && StartX <= x && x <= EndX;
}

public sealed class TownshipDepositLogEntry
{
    public DateTime Timestamp { get; set; }
    public Serial PlayerSerial { get; set; }
    public string PlayerName { get; set; }
    public TownshipDepositSource Source { get; set; }
    public int Amount { get; set; }
    public string Note { get; set; }
    public string AggregateKey { get; set; }
    public int AggregateCount { get; set; }
}

public sealed class TownshipTreasuryContributionEntry
{
    public DateTime Timestamp { get; set; }
    public Serial PlayerSerial { get; set; }
    public string PlayerName { get; set; }
    public TownshipDepositSource Source { get; set; }
    public int Amount { get; set; }
    public string Note { get; set; }
    public string Details { get; set; }
    public string AggregateKey { get; set; }
}

public sealed class TownshipActivityLogEntry
{
    public DateTime Timestamp { get; set; }
    public TownshipLogType Type { get; set; }
    public Serial ActorSerial { get; set; }
    public string ActorName { get; set; }
    public string Details { get; set; }
    public int ActivityAmount { get; set; }
    public int ActivityTriggerCount { get; set; }
}

public sealed class TownshipHouseRecord
{
    public Serial HouseSerial { get; set; }
    public Serial OwnerSerial { get; set; }
    public string OwnerName { get; set; }
    public bool WasGuildMemberAtClaim { get; set; }
    public bool ResidentLease { get; set; }
}

public sealed class TownshipServiceRefundPreview
{
    public int PurchaseCost { get; set; }
    public int RequestedRefundPercent { get; set; }
    public int CappedRefundPercent { get; set; }
    public int VestingScalarPercent { get; set; }
    public int RefundAmount { get; set; }
    public TimeSpan ServiceAge { get; set; }
}

public sealed class TownshipDelinquencyRemovalEntry
{
    public string ServiceId { get; set; }
    public string Name { get; set; }
    public int DailyUpkeep { get; set; }
    public int RefundAmount { get; set; }
    public DateTime ScheduledRemoval { get; set; }
    public string Status { get; set; }
}

public sealed class TownshipDelinquencyPlan
{
    public int DelinquentBalance { get; set; }
    public int AccruedUpkeepDue { get; set; }
    public DateTime DelinquentSince { get; set; }
    public DateTime NextRemovalCheck { get; set; }
    public bool ServicesSuspended { get; set; }
    public List<TownshipDelinquencyRemovalEntry> RemovalOrder { get; } = new();
}

public sealed class TownshipPaidServiceRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TownshipPaidServiceType Type { get; set; }
    public TownshipPaidServiceStatus Status { get; set; }
    public string Name { get; set; }
    public int PurchaseCost { get; set; }
    public int DailyUpkeep { get; set; }
    public DateTime PurchasedAt { get; set; }
    public DateTime SuspendedAt { get; set; }
    public DateTime RemovedAt { get; set; }
    public Serial CreatedObjectSerial { get; set; }
    public Serial AnchorHouseSerial { get; set; }
    public Point3D HomeLocation { get; set; }
    public int RoamRange { get; set; }
    public string Notes { get; set; }
}

public sealed class TownshipHuntingTaxPreference
{
    public Serial PlayerSerial { get; set; }
    public bool OptedIn { get; set; }
    public bool Prompted { get; set; }
}

public sealed class TownshipRankAssignment
{
    public Serial PlayerSerial { get; set; }
    public string PlayerName { get; set; }
    public TownshipRankLevel Rank { get; set; }
}

public sealed class TownshipState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; }
    public Guild Guild { get; set; }
    public Serial GuildSerial { get; set; }
    public Point3D FoundingPoint { get; set; }
    public Map Map { get; set; }
    public DateTime FoundedAt { get; set; }
    public TownshipStone Stone { get; set; }
    public TownshipRegion Region { get; set; }
    public int MaxEnvelopeSize { get; set; }
    public int TreasuryBalance { get; set; }
    public int LifetimeDeposits { get; set; }
    public int ActivityScore { get; set; }
    public DateTime LastActivityDecay { get; set; }
    public DateTime NextUpkeepAssessment { get; set; }
    public DateTime NextWeeklyPayment { get; set; }
    public int AccruedUpkeepDue { get; set; }
    public TownshipFinancialStatus FinancialStatus { get; set; }
    public int DelinquentBalance { get; set; }
    public DateTime DelinquentSince { get; set; }
    public DateTime NextDelinquencyRemoval { get; set; }
    public bool PaidServicesSuspended { get; set; }
    public TownshipHuntingTaxMode HuntingTaxMode { get; set; }
    public int HuntingTaxPercent { get; set; }
    public List<Serial> PatrolGuardSerials { get; } = new();
    public List<TownshipClaimRange> Claims { get; } = new();
    public List<TownshipHouseRecord> Houses { get; } = new();
    public List<TownshipPaidServiceRecord> Services { get; } = new();
    public List<TownshipHuntingTaxPreference> HuntingTaxPreferences { get; } = new();
    public List<TownshipRankAssignment> RankAssignments { get; } = new();
    public List<TownshipDepositLogEntry> DepositLog { get; } = new();
    public List<TownshipTreasuryContributionEntry> TreasuryContributions { get; } = new();
    public List<TownshipActivityLogEntry> ActivityLog { get; } = new();

    public int ClaimedTileCount
    {
        get
        {
            var count = 0;

            for (var i = 0; i < Claims.Count; i++)
            {
                count += Claims[i].TileCount;
            }

            return count;
        }
    }

    public TownshipActivityLevel ActivityLevel => ActivityScore switch
    {
        >= 400 => TownshipActivityLevel.City,
        >= 250 => TownshipActivityLevel.Town,
        >= 125 => TownshipActivityLevel.Village,
        >= 50  => TownshipActivityLevel.Hamlet,
        _      => TownshipActivityLevel.Camp
    };

    public bool IsDelinquent => FinancialStatus == TownshipFinancialStatus.Delinquent && DelinquentBalance > 0;
}

public interface ITownshipOwnedObject
{
    string TownshipId { get; }
    void OnTownshipDeleted(TownshipState township);
}

public sealed class TownshipExpansionPreview
{
    public Rectangle2D RequestedArea { get; set; }
    public int ValidTiles { get; set; }
    public int InvalidTiles { get; set; }
    public int Cost { get; set; }
    public int SharedEdgeTiles { get; set; }
    public bool MeetsEdgeRequirement { get; set; }
    public bool InsideEnvelope { get; set; }
    public Dictionary<string, int> InvalidReasons { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<TownshipClaimRange> ValidClaims { get; } = new();
    public List<TownshipClaimRange> InvalidClaims { get; } = new();
}
