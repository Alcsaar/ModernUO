using System;
using System.IO;
using Server.Json;

namespace Server.Custom.Systems.TravelCodex;

public sealed class TravelCodexConfig
{
    public int MaxCharges { get; set; } = 20;
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromMinutes(0.0);
    public TimeSpan CastDelay { get; set; } = TimeSpan.FromSeconds(4.0);
    public int DefaultDiscoverRange { get; set; } = 2;
    public int DiscoveryMessageHue { get; set; } = 0x59;
    public int CategoryButtonHue { get; set; } = 0x35;
}

public static class TravelCodexSettings
{
    private const string SystemName = "TravelCodex";

    private static TravelCodexConfig _config;

    public static string ConfigDirectory => Path.Combine(Core.BaseDirectory, "Configuration", SystemName);

    public static string PlayerDataPath => Path.Combine(ConfigDirectory, "player-discoveries.json");

    public static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");

    public static int MaxCharges => _config?.MaxCharges ?? 20;

    public static TimeSpan Cooldown => _config?.Cooldown ?? TimeSpan.FromMinutes(0.0);

    public static TimeSpan CastDelay => _config?.CastDelay ?? TimeSpan.FromSeconds(4.0);

    public static int DefaultDiscoverRange => _config?.DefaultDiscoverRange ?? 2;

    public static int DiscoveryMessageHue => _config?.DiscoveryMessageHue ?? 0x59;

    public static int CategoryButtonHue => _config?.CategoryButtonHue ?? 0x35;

    public static void Configure()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        _config = JsonConfig.Deserialize<TravelCodexConfig>(ConfigPath) ?? new TravelCodexConfig();
        JsonConfig.Serialize(ConfigPath, _config);
    }
}
