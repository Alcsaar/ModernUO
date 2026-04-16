using System;
using System.Collections.Generic;
using System.IO;
using Server.Json;
using Server.Logging;

namespace Server.Custom.Systems.CustomFeatureFlags;

public static class CustomFeatureFlagManager
{
    private const string SystemName = "CustomFeatureFlags";

    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(CustomFeatureFlagManager));

    private static readonly Dictionary<string, CustomFeatureFlagDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, CustomFeatureFlagState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    private static bool _configured;

    public static string ConfigDirectory => Path.Combine(Core.BaseDirectory, "Configuration", SystemName);
    public static string ConfigPath => Path.Combine(ConfigDirectory, "flags.json");

    public static void Configure()
    {
        if (_configured)
        {
            return;
        }

        EnsureDirectory();
        Load();
        CustomFeatureFlagCommands.Configure();

        _configured = true;
    }

    public static bool Register(
        string key,
        string displayName,
        string description,
        string category,
        bool defaultEnabled = true,
        string[] dependencies = null,
        bool hidden = false
    )
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalizedKey = NormalizeKey(key);

        _definitions[normalizedKey] = new CustomFeatureFlagDefinition
        {
            Key = normalizedKey,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedKey : displayName,
            Description = description ?? string.Empty,
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category,
            DefaultEnabled = defaultEnabled,
            Hidden = hidden,
            Dependencies = NormalizeDependencies(dependencies)
        };

        if (!_states.ContainsKey(normalizedKey))
        {
            _states[normalizedKey] = new CustomFeatureFlagState
            {
                Key = normalizedKey,
                Enabled = defaultEnabled,
                LastModified = Core.Now,
                LastModifiedBy = "Default"
            };

            Save();
        }

        return true;
    }

    public static bool IsRegistered(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return _definitions.ContainsKey(NormalizeKey(key));
    }

    public static bool IsEnabled(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return EvaluateEnabled(NormalizeKey(key), new HashSet<string>(StringComparer.OrdinalIgnoreCase), out _);
    }

    public static bool SetEnabled(string key, bool enabled, string modifiedBy, out string reason)
    {
        reason = null;

        if (string.IsNullOrWhiteSpace(key))
        {
            reason = "Invalid flag key.";
            return false;
        }

        var normalizedKey = NormalizeKey(key);

        if (!_definitions.TryGetValue(normalizedKey, out var definition))
        {
            reason = $"Unknown flag '{normalizedKey}'.";
            return false;
        }

        var state = GetOrCreateState(definition);

        if (enabled)
        {
            for (var i = 0; i < definition.Dependencies.Length; i++)
            {
                var dependency = definition.Dependencies[i];

                if (!_definitions.ContainsKey(dependency))
                {
                    reason = $"Missing dependency '{dependency}'.";
                    return false;
                }

                if (!IsEnabled(dependency))
                {
                    reason = $"Dependency '{dependency}' is currently unavailable.";
                    return false;
                }
            }
        }

        var previous = state.Enabled;

        state.Enabled = enabled;
        state.LastModified = Core.Now;
        state.LastModifiedBy = string.IsNullOrWhiteSpace(modifiedBy) ? "System" : modifiedBy;

        Save();

        Logger.Information(
            "Custom feature flag '{FlagKey}' changed from {PreviousState} to {NewState} by {ModifiedBy}",
            normalizedKey,
            previous,
            enabled,
            state.LastModifiedBy
        );

        return true;
    }

    public static bool Toggle(string key, string modifiedBy, out string reason)
    {
        reason = null;

        var status = GetStatus(key);
        if (status == null)
        {
            reason = $"Unknown flag '{key}'.";
            return false;
        }

        return SetEnabled(status.Key, !status.StoredEnabled, modifiedBy, out reason);
    }

    public static CustomFeatureFlagStatus GetStatus(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalizedKey = NormalizeKey(key);

        if (!_definitions.TryGetValue(normalizedKey, out var definition))
        {
            return null;
        }

        var state = GetOrCreateState(definition);
        var effectiveEnabled = EvaluateEnabled(
            normalizedKey,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            out var blockingDependencies
        );

        return new CustomFeatureFlagStatus
        {
            Key = definition.Key,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            Category = definition.Category,
            Hidden = definition.Hidden,
            DefaultEnabled = definition.DefaultEnabled,
            StoredEnabled = state.Enabled,
            EffectiveEnabled = effectiveEnabled,
            DependencyFailure = blockingDependencies.Length > 0,
            Dependencies = definition.Dependencies,
            BlockingDependencies = blockingDependencies,
            LastModified = state.LastModified,
            LastModifiedBy = state.LastModifiedBy
        };
    }

    public static List<CustomFeatureFlagStatus> GetAllStatuses(bool includeHidden = true)
    {
        var list = new List<CustomFeatureFlagStatus>(_definitions.Count);

        foreach (var pair in _definitions)
        {
            var status = GetStatus(pair.Key);
            if (status == null)
            {
                continue;
            }

            if (!includeHidden && status.Hidden)
            {
                continue;
            }

            list.Add(status);
        }

        list.Sort(static (a, b) =>
        {
            var categoryCompare = string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase);
            if (categoryCompare != 0)
            {
                return categoryCompare;
            }

            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        return list;
    }

    public static void Save()
    {
        try
        {
            EnsureDirectory();

            var config = new CustomFeatureFlagConfig();

            foreach (var state in _states.Values)
            {
                config.Flags.Add(new CustomFeatureFlagState
                {
                    Key = state.Key,
                    Enabled = state.Enabled,
                    LastModified = state.LastModified,
                    LastModifiedBy = state.LastModifiedBy
                });
            }

            JsonConfig.Serialize(ConfigPath, config);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save custom feature flag config");
        }
    }

    public static void Load()
    {
        try
        {
            EnsureDirectory();
            _states.Clear();

            var config = JsonConfig.Deserialize<CustomFeatureFlagConfig>(ConfigPath);
            if (config?.Flags == null)
            {
                return;
            }

            for (var i = 0; i < config.Flags.Count; i++)
            {
                var state = config.Flags[i];

                if (state == null || string.IsNullOrWhiteSpace(state.Key))
                {
                    continue;
                }

                state.Key = NormalizeKey(state.Key);
                _states[state.Key] = state;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load custom feature flag config");
        }
    }

    private static bool EvaluateEnabled(
        string key,
        HashSet<string> visited,
        out string[] blockingDependencies
    )
    {
        blockingDependencies = Array.Empty<string>();

        if (!_definitions.TryGetValue(key, out var definition))
        {
            blockingDependencies = new[] { $"missing definition: {key}" };
            return false;
        }

        if (!visited.Add(key))
        {
            blockingDependencies = new[] { $"circular dependency: {key}" };
            return false;
        }

        var state = GetOrCreateState(definition);

        if (!state.Enabled)
        {
            visited.Remove(key);
            return false;
        }

        if (definition.Dependencies.Length == 0)
        {
            visited.Remove(key);
            return true;
        }

        var blockedBy = new List<string>();

        for (var i = 0; i < definition.Dependencies.Length; i++)
        {
            var dependency = definition.Dependencies[i];

            if (!EvaluateEnabled(dependency, visited, out _))
            {
                blockedBy.Add(dependency);
            }
        }

        visited.Remove(key);

        if (blockedBy.Count > 0)
        {
            blockingDependencies = blockedBy.ToArray();
            return false;
        }

        return true;
    }

    private static CustomFeatureFlagState GetOrCreateState(CustomFeatureFlagDefinition definition)
    {
        if (_states.TryGetValue(definition.Key, out var state))
        {
            return state;
        }

        state = new CustomFeatureFlagState
        {
            Key = definition.Key,
            Enabled = definition.DefaultEnabled,
            LastModified = Core.Now,
            LastModifiedBy = "Default"
        };

        _states[definition.Key] = state;
        return state;
    }

    private static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();

    private static string[] NormalizeDependencies(string[] dependencies)
    {
        if (dependencies == null || dependencies.Length == 0)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>(dependencies.Length);

        for (var i = 0; i < dependencies.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(dependencies[i]))
            {
                continue;
            }

            list.Add(NormalizeKey(dependencies[i]));
        }

        return list.ToArray();
    }

    private static void EnsureDirectory()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }
    }
}
