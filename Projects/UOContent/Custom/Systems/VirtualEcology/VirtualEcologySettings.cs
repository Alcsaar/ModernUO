using System;
using System.IO;
using Server.Json;

namespace Server.Custom.Systems.VirtualEcology;

public sealed class VirtualEcologyConfig
{
    public int DefaultLineCount { get; set; } = 12;
    public int MaxLineCount { get; set; } = 30;
    public int AutoTopUpLineCount { get; set; } = 3;
    public TimeSpan AutoTopUpInterval { get; set; } = TimeSpan.FromMinutes(10.0);
    public TimeSpan CatchUpTopUpInterval { get; set; } = TimeSpan.FromMinutes(1.0);
    public int MaxGenerationAttempts { get; set; } = 3;
    public int MaxRejectedLineCount { get; set; } = 30;
    public int MaxCachedDialogueLength { get; set; } = 110;
    public int MaxDynamicDialogueLength { get; set; } = 95;
    public int MaxRecentFactCount { get; set; } = 50;
    public TimeSpan PlayerDeathFactMergeWindow { get; set; } = TimeSpan.FromMinutes(10.0);
    public TimeSpan PlayerDeathFactCooldown { get; set; } = TimeSpan.FromHours(6.0);
    public double MovementFactCommentChance { get; set; } = 0.02;
    public double MovementFlavorCommentChance { get; set; } = 0.005;
    public bool AllowStaffMovementTriggers { get; set; }
    public TimeSpan PlayerLiveCommentCooldown { get; set; } = TimeSpan.FromMinutes(2.0);
    public TimeSpan NpcLiveCommentCooldown { get; set; } = TimeSpan.FromMinutes(3.0);
    public TimeSpan LineReuseCooldown { get; set; } = TimeSpan.FromMinutes(30.0);
    public TimeSpan RecentFactMaxAge { get; set; } = TimeSpan.FromHours(6.0);
    public TimeSpan ServerFirstAnnouncementMaxAge { get; set; } = TimeSpan.FromDays(3.0);
    public TimeSpan ServerFirstFactSyncInterval { get; set; } = TimeSpan.FromMinutes(1.0);
}

public static class VirtualEcologySettings
{
    private const string SystemName = "VirtualEcology";

    private static VirtualEcologyConfig _config;

    public static string ConfigDirectory => Path.Combine(Core.BaseDirectory, "Configuration", SystemName);

    public static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");

    public static int DefaultLineCount => Math.Clamp(_config?.DefaultLineCount ?? 12, 3, 100);

    public static int MaxLineCount => Math.Clamp(_config?.MaxLineCount ?? 30, 3, 200);

    public static int AutoTopUpLineCount => Math.Clamp(_config?.AutoTopUpLineCount ?? 3, 1, MaxLineCount);

    public static TimeSpan AutoTopUpInterval => ClampTimeSpan(
        _config?.AutoTopUpInterval ?? TimeSpan.FromMinutes(10.0),
        TimeSpan.FromMinutes(1.0),
        TimeSpan.FromHours(24.0)
    );

    public static TimeSpan CatchUpTopUpInterval => ClampTimeSpan(
        _config?.CatchUpTopUpInterval ?? TimeSpan.FromMinutes(1.0),
        TimeSpan.FromMinutes(1.0),
        TimeSpan.FromHours(24.0)
    );

    public static int MaxGenerationAttempts => Math.Clamp(_config?.MaxGenerationAttempts ?? 3, 1, 10);

    public static int MaxRejectedLineCount => Math.Clamp(_config?.MaxRejectedLineCount ?? 30, 0, 500);

    public static int MaxCachedDialogueLength => Math.Clamp(_config?.MaxCachedDialogueLength ?? 110, 40, 200);

    public static int MaxDynamicDialogueLength => Math.Clamp(_config?.MaxDynamicDialogueLength ?? 95, 40, 200);

    public static int MaxRecentFactCount => Math.Clamp(_config?.MaxRecentFactCount ?? 50, 1, 500);

    public static TimeSpan PlayerDeathFactMergeWindow => ClampTimeSpan(
        _config?.PlayerDeathFactMergeWindow ?? TimeSpan.FromMinutes(10.0),
        TimeSpan.Zero,
        TimeSpan.FromHours(24.0)
    );

    public static TimeSpan PlayerDeathFactCooldown => ClampTimeSpan(
        _config?.PlayerDeathFactCooldown ?? TimeSpan.FromHours(6.0),
        TimeSpan.Zero,
        TimeSpan.FromDays(7.0)
    );

    public static double MovementFactCommentChance => Math.Clamp(_config?.MovementFactCommentChance ?? 0.02, 0.0, 1.0);

    public static double MovementFlavorCommentChance => Math.Clamp(_config?.MovementFlavorCommentChance ?? 0.005, 0.0, 1.0);

    public static bool AllowStaffMovementTriggers => _config?.AllowStaffMovementTriggers == true;

    public static TimeSpan PlayerLiveCommentCooldown => ClampTimeSpan(
        _config?.PlayerLiveCommentCooldown ?? TimeSpan.FromMinutes(2.0),
        TimeSpan.Zero,
        TimeSpan.FromHours(24.0)
    );

    public static TimeSpan NpcLiveCommentCooldown => ClampTimeSpan(
        _config?.NpcLiveCommentCooldown ?? TimeSpan.FromMinutes(3.0),
        TimeSpan.Zero,
        TimeSpan.FromHours(24.0)
    );

    public static TimeSpan LineReuseCooldown => ClampTimeSpan(
        _config?.LineReuseCooldown ?? TimeSpan.FromMinutes(30.0),
        TimeSpan.Zero,
        TimeSpan.FromDays(7.0)
    );

    public static TimeSpan RecentFactMaxAge => ClampTimeSpan(
        _config?.RecentFactMaxAge ?? TimeSpan.FromHours(6.0),
        TimeSpan.FromMinutes(1.0),
        TimeSpan.FromDays(30.0)
    );

    public static TimeSpan ServerFirstAnnouncementMaxAge => ClampTimeSpan(
        _config?.ServerFirstAnnouncementMaxAge ?? TimeSpan.FromDays(3.0),
        TimeSpan.FromMinutes(1.0),
        TimeSpan.FromDays(30.0)
    );

    public static TimeSpan ServerFirstFactSyncInterval => ClampTimeSpan(
        _config?.ServerFirstFactSyncInterval ?? TimeSpan.FromMinutes(1.0),
        TimeSpan.FromSeconds(10.0),
        TimeSpan.FromHours(24.0)
    );

    public static VirtualEcologyConfig Snapshot() => new()
    {
        DefaultLineCount = DefaultLineCount,
        MaxLineCount = MaxLineCount,
        AutoTopUpLineCount = AutoTopUpLineCount,
        AutoTopUpInterval = AutoTopUpInterval,
        CatchUpTopUpInterval = CatchUpTopUpInterval,
        MaxGenerationAttempts = MaxGenerationAttempts,
        MaxRejectedLineCount = MaxRejectedLineCount,
        MaxCachedDialogueLength = MaxCachedDialogueLength,
        MaxDynamicDialogueLength = MaxDynamicDialogueLength,
        MaxRecentFactCount = MaxRecentFactCount,
        PlayerDeathFactMergeWindow = PlayerDeathFactMergeWindow,
        PlayerDeathFactCooldown = PlayerDeathFactCooldown,
        MovementFactCommentChance = MovementFactCommentChance,
        MovementFlavorCommentChance = MovementFlavorCommentChance,
        AllowStaffMovementTriggers = AllowStaffMovementTriggers,
        PlayerLiveCommentCooldown = PlayerLiveCommentCooldown,
        NpcLiveCommentCooldown = NpcLiveCommentCooldown,
        LineReuseCooldown = LineReuseCooldown,
        RecentFactMaxAge = RecentFactMaxAge,
        ServerFirstAnnouncementMaxAge = ServerFirstAnnouncementMaxAge,
        ServerFirstFactSyncInterval = ServerFirstFactSyncInterval
    };

    public static void Configure()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        _config = JsonConfig.Deserialize<VirtualEcologyConfig>(ConfigPath) ?? new VirtualEcologyConfig();
        Save();
    }

    public static void Save(VirtualEcologyConfig config)
    {
        _config = config ?? new VirtualEcologyConfig();
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

    private static TimeSpan ClampTimeSpan(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
