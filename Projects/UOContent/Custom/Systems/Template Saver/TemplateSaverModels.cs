using System;
using System.Collections.Generic;

namespace Server.Custom.Systems.TemplateSaver;

public sealed class TemplateSaverState
{
    public List<CharacterTemplateStore> Characters { get; set; } = new();
}

public sealed class DeletedTemplateArchiveState
{
    public List<DeletedTemplateArchiveEntry> Entries { get; set; } = new();
}

public sealed class CharacterTemplateStore
{
    public uint OwnerSerial { get; set; }
    public string OwnerName { get; set; }
    public int ExtraSlots { get; set; } = 0;
    public List<CharacterTemplateEntry> Templates { get; set; } = new();
    public List<DeletedCharacterTemplateEntry> DeletedTemplates { get; set; } = new();
}

public sealed class CharacterTemplateEntry
{
    public Guid Id { get; set; }
    public uint OwnerSerial { get; set; }
    public string OwnerName { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public StatTemplateSnapshot Stats { get; set; } = new();
    public List<SkillTemplateSnapshot> Skills { get; set; } = new();
}

public sealed class DeletedCharacterTemplateEntry
{
    public Guid DeletedId { get; set; }
    public CharacterTemplateEntry Template { get; set; }
    public DateTime DeletedAt { get; set; }
    public string DeletedBy { get; set; }
}

public sealed class DeletedTemplateArchiveEntry
{
    public Guid DeletedId { get; set; }
    public uint OwnerSerial { get; set; }
    public string OwnerName { get; set; }
    public CharacterTemplateEntry Template { get; set; }
    public DateTime DeletedAt { get; set; }
    public string DeletedBy { get; set; }
}

public sealed class StatTemplateSnapshot
{
    public int Str { get; set; }
    public int Dex { get; set; }
    public int Int { get; set; }
    public StatLockType StrLock { get; set; }
    public StatLockType DexLock { get; set; }
    public StatLockType IntLock { get; set; }
}

public sealed class SkillTemplateSnapshot
{
    public int SkillIndex { get; set; }
    public string SkillName { get; set; }
    public double Base { get; set; }
    public SkillLock Lock { get; set; }
}
