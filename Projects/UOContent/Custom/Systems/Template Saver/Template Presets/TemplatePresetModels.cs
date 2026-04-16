using System;
using System.Collections.Generic;

namespace Server.Custom.Systems.TemplateSaver;

public sealed class TemplatePresetLibraryState
{
    public List<TemplatePresetDefinition> Presets { get; set; } = new();
}

public sealed class TemplatePresetDefinition
{
    public string Name { get; set; }
    public List<TemplatePresetTierDefinition> Tiers { get; set; } = new();
}

public sealed class TemplatePresetTierDefinition
{
    public Guid Id { get; set; }
    public string PresetName { get; set; }
    public string Tier { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
    public StatTemplateSnapshot Stats { get; set; } = new();
    public List<SkillTemplateSnapshot> Skills { get; set; } = new();
}
