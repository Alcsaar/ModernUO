using System;
using System.IO;
using Server.Json;

namespace Server.Custom.Systems.AIIntegration;

public sealed class AIIntegrationConfig
{
    public string OllamaEndpoint { get; set; } = "http://192.168.0.238:11434";
    public string StaffModel { get; set; } = "llama3.1:8b";
    public string ChatterModel { get; set; } = "llama3.1:8b";
    public string KeepAlive { get; set; } = "30m";
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30.0);
    public int MaxPromptLength { get; set; } = 500;
    public int StaffMaxResponseLength { get; set; } = 1600;
    public int ChatterMaxResponseLength { get; set; } = 500;
    public int StaffMaxGeneratedTokens { get; set; } = 180;
    public int ChatterMaxGeneratedTokens { get; set; } = 60;
    public double StaffTemperature { get; set; } = 0.45;
    public double ChatterTemperature { get; set; } = 0.75;
    public bool AllowPlayerUse { get; set; }
}

public enum AIRequestProfile
{
    Staff,
    Chatter
}

public static class AIIntegrationSettings
{
    private const string SystemName = "AIIntegration";

    private static AIIntegrationConfig _config;

    public static string ConfigDirectory => Path.Combine(Core.BaseDirectory, "Configuration", SystemName);

    public static string ConfigPath => Path.Combine(ConfigDirectory, "settings.json");

    public static string OllamaEndpoint => _config?.OllamaEndpoint ?? "http://192.168.0.238:11434";

    public static string StaffModel => _config?.StaffModel ?? "llama3.1:8b";

    public static string ChatterModel => _config?.ChatterModel ?? "llama3.1:8b";

    public static string KeepAlive => string.IsNullOrWhiteSpace(_config?.KeepAlive) ? "30m" : _config.KeepAlive;

    public static TimeSpan RequestTimeout => _config?.RequestTimeout ?? TimeSpan.FromSeconds(30.0);

    public static int MaxPromptLength => Math.Clamp(_config?.MaxPromptLength ?? 500, 50, 4000);

    public static int StaffMaxResponseLength => Math.Clamp(_config?.StaffMaxResponseLength ?? 1600, 100, 8000);

    public static int ChatterMaxResponseLength => Math.Clamp(_config?.ChatterMaxResponseLength ?? 500, 80, 2000);

    public static int StaffMaxGeneratedTokens => Math.Clamp(_config?.StaffMaxGeneratedTokens ?? 180, 20, 1000);

    public static int ChatterMaxGeneratedTokens => Math.Clamp(_config?.ChatterMaxGeneratedTokens ?? 60, 10, 300);

    public static double StaffTemperature => Math.Clamp(_config?.StaffTemperature ?? 0.45, 0.0, 2.0);

    public static double ChatterTemperature => Math.Clamp(_config?.ChatterTemperature ?? 0.75, 0.0, 2.0);

    public static bool AllowPlayerUse => _config?.AllowPlayerUse == true;

    public static string GetModel(AIRequestProfile profile) => profile switch
    {
        AIRequestProfile.Chatter => ChatterModel,
        _                        => StaffModel
    };

    public static int GetMaxResponseLength(AIRequestProfile profile) => profile switch
    {
        AIRequestProfile.Chatter => ChatterMaxResponseLength,
        _                        => StaffMaxResponseLength
    };

    public static int GetMaxGeneratedTokens(AIRequestProfile profile) => profile switch
    {
        AIRequestProfile.Chatter => ChatterMaxGeneratedTokens,
        _                        => StaffMaxGeneratedTokens
    };

    public static double GetTemperature(AIRequestProfile profile) => profile switch
    {
        AIRequestProfile.Chatter => ChatterTemperature,
        _                        => StaffTemperature
    };

    public static void Configure()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        _config = JsonConfig.Deserialize<AIIntegrationConfig>(ConfigPath) ?? new AIIntegrationConfig();
        JsonConfig.Serialize(ConfigPath, _config);
    }
}
