using System;
using System.Collections.Generic;

namespace Server.Custom.Systems.TemplateSaver;

public sealed class TemplateSaverPersistence : GenericPersistence
{
    private static TemplateSaverPersistence _instance;

    public static void Configure()
    {
        _instance ??= new TemplateSaverPersistence();
    }

    private TemplateSaverPersistence() : base("TemplateSaver", 3)
    {
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version
        TemplateSaverManager.SerializePersistence(writer);
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        switch (version)
        {
            case 0:
                {
                    TemplateSaverManager.DeserializePersistence(reader);
                    break;
                }
        }
    }
}

public static class TemplateSaverPersistenceSerializer
{
    public static void WriteState(IGenericWriter writer, TemplateSaverState state)
    {
        WriteCharacterStores(writer, state?.Characters);
    }

    public static TemplateSaverState ReadState(IGenericReader reader)
    {
        return new TemplateSaverState
        {
            Characters = ReadCharacterStores(reader)
        };
    }

    public static void WriteDeletedArchive(IGenericWriter writer, DeletedTemplateArchiveState state)
    {
        var entries = state?.Entries;
        writer.WriteEncodedInt(entries?.Count ?? 0);

        if (entries == null)
        {
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            WriteDeletedArchiveEntry(writer, entries[i]);
        }
    }

    public static DeletedTemplateArchiveState ReadDeletedArchive(IGenericReader reader)
    {
        var state = new DeletedTemplateArchiveState();
        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            state.Entries.Add(ReadDeletedArchiveEntry(reader));
        }

        return state;
    }

    private static void WriteCharacterStores(IGenericWriter writer, List<CharacterTemplateStore> stores)
    {
        writer.WriteEncodedInt(stores?.Count ?? 0);

        if (stores == null)
        {
            return;
        }

        for (var i = 0; i < stores.Count; i++)
        {
            WriteCharacterStore(writer, stores[i]);
        }
    }

    private static List<CharacterTemplateStore> ReadCharacterStores(IGenericReader reader)
    {
        var count = reader.ReadEncodedInt();
        var stores = new List<CharacterTemplateStore>(count);

        for (var i = 0; i < count; i++)
        {
            stores.Add(ReadCharacterStore(reader));
        }

        return stores;
    }

    private static void WriteCharacterStore(IGenericWriter writer, CharacterTemplateStore store)
    {
        writer.Write(store?.OwnerSerial ?? 0u);
        writer.Write(store?.OwnerName);
        writer.WriteEncodedInt(store?.ExtraSlots ?? 0);
        WriteTemplates(writer, store?.Templates);
        WriteDeletedTemplates(writer, store?.DeletedTemplates);
    }

    private static CharacterTemplateStore ReadCharacterStore(IGenericReader reader)
    {
        return new CharacterTemplateStore
        {
            OwnerSerial = reader.ReadUInt(),
            OwnerName = reader.ReadString(),
            ExtraSlots = reader.ReadEncodedInt(),
            Templates = ReadTemplates(reader),
            DeletedTemplates = ReadDeletedTemplates(reader)
        };
    }

    private static void WriteTemplates(IGenericWriter writer, List<CharacterTemplateEntry> templates)
    {
        writer.WriteEncodedInt(templates?.Count ?? 0);

        if (templates == null)
        {
            return;
        }

        for (var i = 0; i < templates.Count; i++)
        {
            WriteTemplate(writer, templates[i]);
        }
    }

    private static List<CharacterTemplateEntry> ReadTemplates(IGenericReader reader)
    {
        var count = reader.ReadEncodedInt();
        var templates = new List<CharacterTemplateEntry>(count);

        for (var i = 0; i < count; i++)
        {
            templates.Add(ReadTemplate(reader));
        }

        return templates;
    }

    private static void WriteDeletedTemplates(IGenericWriter writer, List<DeletedCharacterTemplateEntry> entries)
    {
        writer.WriteEncodedInt(entries?.Count ?? 0);

        if (entries == null)
        {
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            WriteDeletedTemplate(writer, entries[i]);
        }
    }

    private static List<DeletedCharacterTemplateEntry> ReadDeletedTemplates(IGenericReader reader)
    {
        var count = reader.ReadEncodedInt();
        var entries = new List<DeletedCharacterTemplateEntry>(count);

        for (var i = 0; i < count; i++)
        {
            entries.Add(ReadDeletedTemplate(reader));
        }

        return entries;
    }

    private static void WriteTemplate(IGenericWriter writer, CharacterTemplateEntry template)
    {
        writer.Write(template?.Id ?? Guid.Empty);
        writer.Write(template?.OwnerSerial ?? 0u);
        writer.Write(template?.OwnerName);
        writer.Write(template?.Name);
        writer.Write(template?.CreatedAt ?? DateTime.MinValue);
        writer.Write(template?.UpdatedAt ?? DateTime.MinValue);
        WriteStats(writer, template?.Stats);
        WriteSkills(writer, template?.Skills);
    }

    private static CharacterTemplateEntry ReadTemplate(IGenericReader reader)
    {
        return new CharacterTemplateEntry
        {
            Id = reader.ReadGuid(),
            OwnerSerial = reader.ReadUInt(),
            OwnerName = reader.ReadString(),
            Name = reader.ReadString(),
            CreatedAt = reader.ReadDateTime(),
            UpdatedAt = reader.ReadDateTime(),
            Stats = ReadStats(reader),
            Skills = ReadSkills(reader)
        };
    }

    private static void WriteDeletedTemplate(IGenericWriter writer, DeletedCharacterTemplateEntry entry)
    {
        writer.Write(entry?.DeletedId ?? Guid.Empty);
        WriteTemplate(writer, entry?.Template);
        writer.Write(entry?.DeletedAt ?? DateTime.MinValue);
        writer.Write(entry?.DeletedBy);
    }

    private static DeletedCharacterTemplateEntry ReadDeletedTemplate(IGenericReader reader)
    {
        return new DeletedCharacterTemplateEntry
        {
            DeletedId = reader.ReadGuid(),
            Template = ReadTemplate(reader),
            DeletedAt = reader.ReadDateTime(),
            DeletedBy = reader.ReadString()
        };
    }

    private static void WriteDeletedArchiveEntry(IGenericWriter writer, DeletedTemplateArchiveEntry entry)
    {
        writer.Write(entry?.DeletedId ?? Guid.Empty);
        writer.Write(entry?.OwnerSerial ?? 0u);
        writer.Write(entry?.OwnerName);
        WriteTemplate(writer, entry?.Template);
        writer.Write(entry?.DeletedAt ?? DateTime.MinValue);
        writer.Write(entry?.DeletedBy);
    }

    private static DeletedTemplateArchiveEntry ReadDeletedArchiveEntry(IGenericReader reader)
    {
        return new DeletedTemplateArchiveEntry
        {
            DeletedId = reader.ReadGuid(),
            OwnerSerial = reader.ReadUInt(),
            OwnerName = reader.ReadString(),
            Template = ReadTemplate(reader),
            DeletedAt = reader.ReadDateTime(),
            DeletedBy = reader.ReadString()
        };
    }

    private static void WriteStats(IGenericWriter writer, StatTemplateSnapshot stats)
    {
        writer.WriteEncodedInt(stats?.Str ?? 0);
        writer.WriteEncodedInt(stats?.Dex ?? 0);
        writer.WriteEncodedInt(stats?.Int ?? 0);
        writer.WriteEnum(stats?.StrLock ?? StatLockType.Up);
        writer.WriteEnum(stats?.DexLock ?? StatLockType.Up);
        writer.WriteEnum(stats?.IntLock ?? StatLockType.Up);
    }

    private static StatTemplateSnapshot ReadStats(IGenericReader reader)
    {
        return new StatTemplateSnapshot
        {
            Str = reader.ReadEncodedInt(),
            Dex = reader.ReadEncodedInt(),
            Int = reader.ReadEncodedInt(),
            StrLock = reader.ReadEnum<StatLockType>(),
            DexLock = reader.ReadEnum<StatLockType>(),
            IntLock = reader.ReadEnum<StatLockType>()
        };
    }

    private static void WriteSkills(IGenericWriter writer, List<SkillTemplateSnapshot> skills)
    {
        writer.WriteEncodedInt(skills?.Count ?? 0);

        if (skills == null)
        {
            return;
        }

        for (var i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            writer.WriteEncodedInt(skill?.SkillIndex ?? -1);
            writer.Write(skill?.SkillName);
            writer.Write(skill?.Base ?? 0.0);
            writer.WriteEnum(skill?.Lock ?? SkillLock.Up);
        }
    }

    private static List<SkillTemplateSnapshot> ReadSkills(IGenericReader reader)
    {
        var count = reader.ReadEncodedInt();
        var skills = new List<SkillTemplateSnapshot>(count);

        for (var i = 0; i < count; i++)
        {
            skills.Add(
                new SkillTemplateSnapshot
                {
                    SkillIndex = reader.ReadEncodedInt(),
                    SkillName = reader.ReadString(),
                    Base = reader.ReadDouble(),
                    Lock = reader.ReadEnum<SkillLock>()
                }
            );
        }

        return skills;
    }
}
