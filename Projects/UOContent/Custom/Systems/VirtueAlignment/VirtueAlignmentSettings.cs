using System;
using System.IO;
using Server.Json;

namespace Server.Custom.Systems.VirtueAlignment;

public sealed class VirtueAlignmentConfig
{
    public bool Enabled { get; set; } = true;
    public bool StockVirtuesEnabled { get; set; }
    public bool AllowPlayerReselection { get; set; }
}

public static class VirtueAlignmentSettings
{
    private const string SystemName = "VirtueAlignment";
    private static VirtueAlignmentConfig _config;

    public static string ConfigDirectory => Path.Combine(Core.BaseDirectory, "Configuration", SystemName);
    public static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");

    public static bool Enabled => _config?.Enabled != false;
    public static bool StockVirtuesEnabled => _config?.StockVirtuesEnabled == true;
    public static bool AllowPlayerReselection => _config?.AllowPlayerReselection == true;

    public static VirtueAlignmentConfig Snapshot() => new()
    {
        Enabled = Enabled,
        StockVirtuesEnabled = StockVirtuesEnabled,
        AllowPlayerReselection = AllowPlayerReselection
    };

    public static void Configure()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        _config = JsonConfig.Deserialize<VirtueAlignmentConfig>(ConfigPath) ?? new VirtueAlignmentConfig();
        Save();
    }

    public static void Save(VirtueAlignmentConfig config)
    {
        _config = config ?? new VirtueAlignmentConfig();
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
