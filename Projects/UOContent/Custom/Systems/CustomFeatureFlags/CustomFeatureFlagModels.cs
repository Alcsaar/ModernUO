using System;
using System.Collections.Generic;

namespace Server.Custom.Systems.CustomFeatureFlags;

public sealed class CustomFeatureFlagDefinition
{
    public string Key { get; init; }
    public string DisplayName { get; init; }
    public string Description { get; init; }
    public string Category { get; init; }
    public bool DefaultEnabled { get; init; }
    public bool Hidden { get; init; }
    public string[] Dependencies { get; init; } = Array.Empty<string>();
}

public sealed class CustomFeatureFlagState
{
    public string Key { get; set; }
    public bool Enabled { get; set; }
    public DateTime LastModified { get; set; }
    public string LastModifiedBy { get; set; }
}

public sealed class CustomFeatureFlagConfig
{
    public List<CustomFeatureFlagState> Flags { get; set; } = new();
}

public sealed class CustomFeatureFlagStatus
{
    public string Key { get; init; }
    public string DisplayName { get; init; }
    public string Description { get; init; }
    public string Category { get; init; }
    public bool Hidden { get; init; }
    public bool DefaultEnabled { get; init; }
    public bool StoredEnabled { get; init; }
    public bool EffectiveEnabled { get; init; }
    public bool DependencyFailure { get; init; }
    public string[] Dependencies { get; init; } = Array.Empty<string>();
    public string[] BlockingDependencies { get; init; } = Array.Empty<string>();
    public DateTime LastModified { get; init; }
    public string LastModifiedBy { get; init; }
}
