using System;
using System.IO;
using Server.Json;

namespace Server.Custom.Systems.Townships;

public sealed class TownshipConfig
{
    public bool Enabled { get; set; } = true;
    public int DeedCost { get; set; } = 150000;
    public int InitialClaimSize { get; set; } = 50;
    public int HouseBuffer { get; set; } = 5;
    public int EdgeContactRequired { get; set; } = 5;
    public int TileCost { get; set; }
    public int MaxLogEntries { get; set; } = 500;
    public int MaxDepositLogEntries { get; set; } = 500;
    public int MarkerDurationSeconds { get; set; } = 30;
    public int PreviewMarkerDurationSeconds { get; set; } = 5;
    public int BorderRefreshSeconds { get; set; } = 25;
    public int BorderRenderRange { get; set; } = 64;
    public int ClaimedBorderHue { get; set; } = 1151;
    public int EnvelopeBorderHue { get; set; } = 1152;
    public int PreviewValidHue { get; set; } = 68;
    public int PreviewInvalidHue { get; set; } = 33;
    public int BorderEffectItemId { get; set; } = 0x1F14;
    public int EnvelopeEffectItemId { get; set; } = 0x1F14;
    public int InvalidEffectItemId { get; set; } = 0x1165;
    public bool FeluccaOnly { get; set; } = true;
    public bool UpkeepEnabled { get; set; }
    public int UpkeepGraceDays { get; set; } = 3;
    public int DailyLandUpkeepPerTile { get; set; }
    public int ActivityLogMergeHours { get; set; } = 24;
    public int DelinquencyGraceDays { get; set; } = 7;
    public int DelinquencyRemovalIntervalDays { get; set; } = 3;
    public int MaxServiceRefundPercent { get; set; } = 50;
    public int DefaultVoluntaryServiceRefundPercent { get; set; } = 50;
    public int DefaultDelinquencyServiceRefundPercent { get; set; } = 40;
    public int ServiceRefundPartialVestingDays { get; set; } = 8;
    public int ServiceRefundPartialVestingScalarPercent { get; set; } = 50;
    public int ServiceRefundFullVestingDays { get; set; } = 31;
    public int BankerPurchaseCost { get; set; } = 1000000;
    public int BankerDailyUpkeep { get; set; } = 50000;
    public int MagePurchaseCost { get; set; } = 500000;
    public int MageDailyUpkeep { get; set; } = 25000;
    public int AlchemistPurchaseCost { get; set; } = 500000;
    public int AlchemistDailyUpkeep { get; set; } = 25000;
    public int StablemasterPurchaseCost { get; set; } = 750000;
    public int StablemasterDailyUpkeep { get; set; } = 40000;
    public int InnkeeperPurchaseCost { get; set; } = 750000;
    public int InnkeeperDailyUpkeep { get; set; } = 40000;
    public int GuardedTownPurchaseCost { get; set; } = 100000;
    public int GuardedTownDailyUpkeep { get; set; } = 10000;
    public int HuntingTaxPurchaseCost { get; set; }
    public int HuntingTaxDailyUpkeep { get; set; }
    public int HuntingContributionPercent { get; set; } = 2;
    public int MaxHuntingTaxPercent { get; set; } = 10;
    public int VendorRevenueContributionPercent { get; set; } = 10;
    public int GuardedTownPatrolGuards { get; set; } = 3;
    public bool AmbientTownsfolkEnabled { get; set; } = true;
    public int MaxAmbientTownsfolk { get; set; } = 5;
    public int AmbientTownsfolkSpawnIntervalMinutes { get; set; } = 30;
    public int AmbientTownsfolkSpawnChancePercent { get; set; } = 35;
    public int AmbientTownsfolkRoamRange { get; set; } = 10;
    public int[] EnvelopeSizes { get; set; } = [50, 75, 100, 125];
}

public static class TownshipSettings
{
    private const string SystemName = "Townships";
    private static TownshipConfig _config;

    public static string ConfigDirectory => Path.Combine(Core.BaseDirectory, "Configuration", SystemName);
    public static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");

    public static bool Enabled => _config?.Enabled != false;
    public static int DeedCost => Math.Clamp(_config?.DeedCost ?? 150000, 0, 100000000);
    public static int InitialClaimSize => Math.Clamp(_config?.InitialClaimSize ?? 50, 10, 300);
    public static int HouseBuffer => Math.Clamp(_config?.HouseBuffer ?? 5, 0, 50);
    public static int EdgeContactRequired => Math.Clamp(_config?.EdgeContactRequired ?? 5, 1, 100);
    public static int TileCost => Math.Clamp(_config?.TileCost ?? 0, 0, 1000000);
    public static int MaxLogEntries => Math.Clamp(_config?.MaxLogEntries ?? 500, 50, 5000);
    public static int MaxDepositLogEntries => Math.Clamp(_config?.MaxDepositLogEntries ?? 500, 50, 5000);
    public static TimeSpan MarkerDuration => TimeSpan.FromSeconds(Math.Clamp(_config?.MarkerDurationSeconds ?? 30, 5, 120));
    public static TimeSpan PreviewMarkerDuration => TimeSpan.FromSeconds(Math.Clamp(_config?.PreviewMarkerDurationSeconds ?? 5, 2, 30));
    public static TimeSpan BorderRefreshInterval => TimeSpan.FromSeconds(Math.Clamp(_config?.BorderRefreshSeconds ?? 25, 5, 120));
    public static int BorderRenderRange => Math.Clamp(_config?.BorderRenderRange ?? 64, 18, 160);
    public static int ClaimedBorderHue => Math.Clamp(_config?.ClaimedBorderHue ?? 1151, 0, 3000);
    public static int EnvelopeBorderHue => Math.Clamp(_config?.EnvelopeBorderHue ?? 1152, 0, 3000);
    public static int PreviewValidHue => Math.Clamp(_config?.PreviewValidHue ?? 68, 0, 3000);
    public static int PreviewInvalidHue => Math.Clamp(_config?.PreviewInvalidHue ?? 33, 0, 3000);
    public static int BorderEffectItemId => Math.Clamp(_config?.BorderEffectItemId ?? 0x1F14, 1, 0x3FFF);
    public static int EnvelopeEffectItemId => Math.Clamp(_config?.EnvelopeEffectItemId ?? 0x1F14, 1, 0x3FFF);
    public static int InvalidEffectItemId => Math.Clamp(_config?.InvalidEffectItemId ?? 0x1165, 1, 0x3FFF);
    public static bool FeluccaOnly => _config?.FeluccaOnly != false;
    public static bool UpkeepEnabled => _config?.UpkeepEnabled == true;
    public static int UpkeepGraceDays => Math.Clamp(_config?.UpkeepGraceDays ?? 3, 1, 30);
    public static int DailyLandUpkeepPerTile => Math.Clamp(_config?.DailyLandUpkeepPerTile ?? 0, 0, 1000000);
    public static TimeSpan ActivityLogMergeWindow => TimeSpan.FromHours(Math.Clamp(_config?.ActivityLogMergeHours ?? 24, 1, 168));
    public static TimeSpan DelinquencyGracePeriod => TimeSpan.FromDays(Math.Clamp(_config?.DelinquencyGraceDays ?? 7, 1, 60));
    public static TimeSpan DelinquencyRemovalInterval => TimeSpan.FromDays(Math.Clamp(_config?.DelinquencyRemovalIntervalDays ?? 3, 1, 30));
    public static int MaxServiceRefundPercent => Math.Clamp(_config?.MaxServiceRefundPercent ?? 50, 0, 50);
    public static int DefaultVoluntaryServiceRefundPercent => Math.Clamp(_config?.DefaultVoluntaryServiceRefundPercent ?? 50, 0, MaxServiceRefundPercent);
    public static int DefaultDelinquencyServiceRefundPercent => Math.Clamp(_config?.DefaultDelinquencyServiceRefundPercent ?? 40, 0, MaxServiceRefundPercent);
    public static int ServiceRefundPartialVestingDays => Math.Clamp(_config?.ServiceRefundPartialVestingDays ?? 8, 0, 365);
    public static int ServiceRefundPartialVestingScalarPercent => Math.Clamp(_config?.ServiceRefundPartialVestingScalarPercent ?? 50, 0, 100);
    public static int ServiceRefundFullVestingDays => Math.Clamp(
        Math.Max(_config?.ServiceRefundFullVestingDays ?? 31, ServiceRefundPartialVestingDays),
        0,
        3650
    );
    public static int BankerPurchaseCost => Math.Clamp(_config?.BankerPurchaseCost ?? 1000000, 0, 100000000);
    public static int BankerDailyUpkeep => Math.Clamp(_config?.BankerDailyUpkeep ?? 50000, 0, 10000000);
    public static int MagePurchaseCost => Math.Clamp(_config?.MagePurchaseCost ?? 500000, 0, 100000000);
    public static int MageDailyUpkeep => Math.Clamp(_config?.MageDailyUpkeep ?? 25000, 0, 10000000);
    public static int AlchemistPurchaseCost => Math.Clamp(_config?.AlchemistPurchaseCost ?? 500000, 0, 100000000);
    public static int AlchemistDailyUpkeep => Math.Clamp(_config?.AlchemistDailyUpkeep ?? 25000, 0, 10000000);
    public static int StablemasterPurchaseCost => Math.Clamp(_config?.StablemasterPurchaseCost ?? 750000, 0, 100000000);
    public static int StablemasterDailyUpkeep => Math.Clamp(_config?.StablemasterDailyUpkeep ?? 40000, 0, 10000000);
    public static int InnkeeperPurchaseCost => Math.Clamp(_config?.InnkeeperPurchaseCost ?? 750000, 0, 100000000);
    public static int InnkeeperDailyUpkeep => Math.Clamp(_config?.InnkeeperDailyUpkeep ?? 40000, 0, 10000000);
    public static int GuardedTownPurchaseCost => Math.Clamp(_config?.GuardedTownPurchaseCost ?? 100000, 0, 100000000);
    public static int GuardedTownDailyUpkeep => Math.Clamp(_config?.GuardedTownDailyUpkeep ?? 10000, 0, 10000000);
    public static int HuntingTaxPurchaseCost => Math.Clamp(_config?.HuntingTaxPurchaseCost ?? 0, 0, 100000000);
    public static int HuntingTaxDailyUpkeep => Math.Clamp(_config?.HuntingTaxDailyUpkeep ?? 0, 0, 10000000);
    public static int HuntingContributionPercent => Math.Clamp(_config?.HuntingContributionPercent ?? 2, 0, 10);
    public static int MaxHuntingTaxPercent => Math.Clamp(_config?.MaxHuntingTaxPercent ?? 10, 0, 25);
    public static int VendorRevenueContributionPercent => Math.Clamp(_config?.VendorRevenueContributionPercent ?? 10, 0, 25);
    public static int GuardedTownPatrolGuards => Math.Clamp(_config?.GuardedTownPatrolGuards ?? 3, 0, 10);
    public static bool AmbientTownsfolkEnabled => _config?.AmbientTownsfolkEnabled != false;
    public static int MaxAmbientTownsfolk => Math.Clamp(_config?.MaxAmbientTownsfolk ?? 5, 0, 25);
    public static TimeSpan AmbientTownsfolkSpawnInterval =>
        TimeSpan.FromMinutes(Math.Clamp(_config?.AmbientTownsfolkSpawnIntervalMinutes ?? 30, 5, 1440));
    public static int AmbientTownsfolkSpawnChancePercent =>
        Math.Clamp(_config?.AmbientTownsfolkSpawnChancePercent ?? 35, 0, 100);
    public static int AmbientTownsfolkRoamRange => Math.Clamp(_config?.AmbientTownsfolkRoamRange ?? 10, 0, 25);

    public static int[] EnvelopeSizes
    {
        get
        {
            var configured = _config?.EnvelopeSizes;

            if (configured == null || configured.Length == 0)
            {
                return [50, 75, 100, 125];
            }

            var sizes = new int[configured.Length];

            for (var i = 0; i < configured.Length; i++)
            {
                sizes[i] = Math.Clamp(configured[i], InitialClaimSize, 500);
            }

            Array.Sort(sizes);
            return sizes;
        }
    }

    public static TownshipConfig Snapshot() => new()
    {
        Enabled = Enabled,
        DeedCost = DeedCost,
        InitialClaimSize = InitialClaimSize,
        HouseBuffer = HouseBuffer,
        EdgeContactRequired = EdgeContactRequired,
        TileCost = TileCost,
        MaxLogEntries = MaxLogEntries,
        MaxDepositLogEntries = MaxDepositLogEntries,
        MarkerDurationSeconds = (int)MarkerDuration.TotalSeconds,
        PreviewMarkerDurationSeconds = (int)PreviewMarkerDuration.TotalSeconds,
        BorderRefreshSeconds = (int)BorderRefreshInterval.TotalSeconds,
        BorderRenderRange = BorderRenderRange,
        ClaimedBorderHue = ClaimedBorderHue,
        EnvelopeBorderHue = EnvelopeBorderHue,
        PreviewValidHue = PreviewValidHue,
        PreviewInvalidHue = PreviewInvalidHue,
        BorderEffectItemId = BorderEffectItemId,
        EnvelopeEffectItemId = EnvelopeEffectItemId,
        InvalidEffectItemId = InvalidEffectItemId,
        FeluccaOnly = FeluccaOnly,
        UpkeepEnabled = UpkeepEnabled,
        UpkeepGraceDays = UpkeepGraceDays,
        DailyLandUpkeepPerTile = DailyLandUpkeepPerTile,
        ActivityLogMergeHours = (int)ActivityLogMergeWindow.TotalHours,
        DelinquencyGraceDays = (int)DelinquencyGracePeriod.TotalDays,
        DelinquencyRemovalIntervalDays = (int)DelinquencyRemovalInterval.TotalDays,
        MaxServiceRefundPercent = MaxServiceRefundPercent,
        DefaultVoluntaryServiceRefundPercent = DefaultVoluntaryServiceRefundPercent,
        DefaultDelinquencyServiceRefundPercent = DefaultDelinquencyServiceRefundPercent,
        ServiceRefundPartialVestingDays = ServiceRefundPartialVestingDays,
        ServiceRefundPartialVestingScalarPercent = ServiceRefundPartialVestingScalarPercent,
        ServiceRefundFullVestingDays = ServiceRefundFullVestingDays,
        BankerPurchaseCost = BankerPurchaseCost,
        BankerDailyUpkeep = BankerDailyUpkeep,
        MagePurchaseCost = MagePurchaseCost,
        MageDailyUpkeep = MageDailyUpkeep,
        AlchemistPurchaseCost = AlchemistPurchaseCost,
        AlchemistDailyUpkeep = AlchemistDailyUpkeep,
        StablemasterPurchaseCost = StablemasterPurchaseCost,
        StablemasterDailyUpkeep = StablemasterDailyUpkeep,
        InnkeeperPurchaseCost = InnkeeperPurchaseCost,
        InnkeeperDailyUpkeep = InnkeeperDailyUpkeep,
        GuardedTownPurchaseCost = GuardedTownPurchaseCost,
        GuardedTownDailyUpkeep = GuardedTownDailyUpkeep,
        HuntingTaxPurchaseCost = HuntingTaxPurchaseCost,
        HuntingTaxDailyUpkeep = HuntingTaxDailyUpkeep,
        HuntingContributionPercent = HuntingContributionPercent,
        MaxHuntingTaxPercent = MaxHuntingTaxPercent,
        VendorRevenueContributionPercent = VendorRevenueContributionPercent,
        GuardedTownPatrolGuards = GuardedTownPatrolGuards,
        AmbientTownsfolkEnabled = AmbientTownsfolkEnabled,
        MaxAmbientTownsfolk = MaxAmbientTownsfolk,
        AmbientTownsfolkSpawnIntervalMinutes = (int)AmbientTownsfolkSpawnInterval.TotalMinutes,
        AmbientTownsfolkSpawnChancePercent = AmbientTownsfolkSpawnChancePercent,
        AmbientTownsfolkRoamRange = AmbientTownsfolkRoamRange,
        EnvelopeSizes = EnvelopeSizes
    };

    public static void Configure()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        _config = JsonConfig.Deserialize<TownshipConfig>(ConfigPath) ?? new TownshipConfig();
        Save();
    }

    public static void Save(TownshipConfig config)
    {
        _config = config ?? new TownshipConfig();
        Save();
    }

    public static void Save()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        JsonConfig.Serialize(ConfigPath, Snapshot());
    }
}
