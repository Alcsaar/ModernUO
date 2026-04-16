using System;
using System.Collections.Generic;
using System.IO;
using Server.Accounting;
using Server.Json;
using Server.Mobiles;
using Server.Systems.FeatureFlags;

namespace Server.Custom.Systems.TemplateSaver;

public static class TemplateSaverManager
{
    public const string FeatureFlagKey = "template_saves";
    public const int DefaultTemplateSlots = 2;
    public const int MaxDeletedHistoryPerCharacter = 100;
    public const int MaxDeletedArchiveEntries = 5000;

    private static readonly string SavePath =
        Path.Combine(Core.BaseDirectory, "Configuration", "TemplateSaver", "template-saver.json");

    private static readonly string DeletedArchivePath =
        Path.Combine(Core.BaseDirectory, "Configuration", "TemplateSaver", "template-saver-deleted.json");

    private static TemplateSaverState _state;
    private static DeletedTemplateArchiveState _deletedArchive;
    private static bool _initialized;

    public static void Configure()
    {
        EnsureInitialized();
    }

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _state = JsonConfig.Deserialize<TemplateSaverState>(SavePath) ?? new TemplateSaverState();
        _deletedArchive = JsonConfig.Deserialize<DeletedTemplateArchiveState>(DeletedArchivePath) ?? new DeletedTemplateArchiveState();

        FeatureFlagManager.CreateOrUpdateFlag(
            FeatureFlagKey,
            "Char Template Saving",
            "Player Systems",
            true,
            "System"
        );

        _initialized = true;
    }

    public static bool CanUse(Mobile from, out string message)
    {
        EnsureInitialized();

        if (from == null)
        {
            message = "Invalid user.";
            return false;
        }

        if (from.AccessLevel > AccessLevel.Player)
        {
            message = null;
            return true;
        }

        if (!FeatureFlagManager.IsEnabled(FeatureFlagKey))
        {
            message = "This system is currently disabled.";
            return false;
        }

        if (from.Account is not Account)
        {
            message = "Only player characters may use this system.";
            return false;
        }

        message = null;
        return true;
    }

    public static CharacterTemplateStore GetOrCreateStore(Mobile owner)
    {
        EnsureInitialized();

        var store = GetStore(owner.Serial.Value);
        if (store != null)
        {
            UpdateOwnerDisplayName(store, owner);
            return store;
        }

        store = new CharacterTemplateStore
        {
            OwnerSerial = owner.Serial.Value,
            OwnerName = owner.Name,
            ExtraSlots = 0
        };

        _state.Characters.Add(store);
        Save();

        return store;
    }

    public static CharacterTemplateStore GetStore(uint ownerSerial)
    {
        EnsureInitialized();

        for (var i = 0; i < _state.Characters.Count; i++)
        {
            var store = _state.Characters[i];
            if (store.OwnerSerial == ownerSerial)
            {
                return store;
            }
        }

        return null;
    }

    public static IReadOnlyList<CharacterTemplateStore> GetAllStores()
    {
        EnsureInitialized();
        return _state.Characters;
    }

    public static IReadOnlyList<DeletedTemplateArchiveEntry> GetDeletedArchiveEntries()
    {
        EnsureInitialized();
        return _deletedArchive.Entries;
    }

    public static List<DeletedTemplateArchiveEntry> GetDeletedArchiveEntriesForOwner(uint ownerSerial)
    {
        EnsureInitialized();

        var list = new List<DeletedTemplateArchiveEntry>();

        for (var i = 0; i < _deletedArchive.Entries.Count; i++)
        {
            var entry = _deletedArchive.Entries[i];
            if (entry.OwnerSerial == ownerSerial)
            {
                list.Add(entry);
            }
        }

        return list;
    }

    public static int GetTemplateSlotLimit(uint ownerSerial)
    {
        var store = GetStore(ownerSerial);
        return DefaultTemplateSlots + (store?.ExtraSlots ?? 0);
    }

    public static int GetTemplateSlotLimit(Mobile owner) => GetTemplateSlotLimit(owner.Serial.Value);

    public static IReadOnlyList<CharacterTemplateEntry> GetTemplates(uint ownerSerial)
    {
        var store = GetStore(ownerSerial);

        if (store != null)
        {
            return store.Templates;
        }

        return Array.Empty<CharacterTemplateEntry>();
    }

    public static IReadOnlyList<CharacterTemplateEntry> GetTemplates(Mobile owner) => GetTemplates(owner.Serial.Value);

    public static IReadOnlyList<DeletedCharacterTemplateEntry> GetDeletedTemplates(uint ownerSerial)
    {
        var store = GetStore(ownerSerial);

        if (store != null)
        {
            return store.DeletedTemplates;
        }

        return Array.Empty<DeletedCharacterTemplateEntry>();
    }

    public static IReadOnlyList<DeletedCharacterTemplateEntry> GetDeletedTemplates(Mobile owner) =>
        GetDeletedTemplates(owner.Serial.Value);

    public static bool SaveNewTemplate(Mobile from, string templateName, out string message)
    {
        EnsureInitialized();

        if (!CanUse(from, out message))
        {
            return false;
        }

        templateName = NormalizeTemplateName(templateName);

        if (string.IsNullOrWhiteSpace(templateName))
        {
            message = "You must provide a template name.";
            return false;
        }

        var store = GetOrCreateStore(from);

        if (store.Templates.Count >= GetTemplateSlotLimit(from))
        {
            message = $"You have reached your template limit ({GetTemplateSlotLimit(from)}).";
            return false;
        }

        for (var i = 0; i < store.Templates.Count; i++)
        {
            if (store.Templates[i].Name.Equals(templateName, StringComparison.OrdinalIgnoreCase))
            {
                message = "A template with that name already exists.";
                return false;
            }
        }

        var entry = BuildTemplateSnapshot(from, templateName);
        store.Templates.Add(entry);
        Save();

        message = $"Template '{entry.Name}' has been saved.";
        return true;
    }

    public static bool LoadTemplate(Mobile target, Guid templateId, out string message)
    {
        EnsureInitialized();

        if (!CanUse(target, out message))
        {
            return false;
        }

        var store = GetStore(target.Serial.Value);
        if (store == null)
        {
            message = "No templates were found for this character.";
            return false;
        }

        var entry = FindTemplate(store, templateId);
        if (entry == null)
        {
            message = "That template could not be found.";
            return false;
        }

        ApplyTemplateToMobile(target, entry);
        message = $"Template '{entry.Name}' has been loaded.";
        return true;
    }

    public static bool LoadTemplateAsStaff(Mobile actor, Mobile target, uint ownerSerial, Guid templateId, out string message)
    {
        EnsureInitialized();

        if (actor == null || actor.AccessLevel <= AccessLevel.Player)
        {
            message = "You do not have access to that action.";
            return false;
        }

        if (target == null || target.Deleted)
        {
            message = "That target is invalid.";
            return false;
        }

        var store = GetStore(ownerSerial);
        if (store == null)
        {
            message = "No templates were found for that character.";
            return false;
        }

        var entry = FindTemplate(store, templateId);
        if (entry == null)
        {
            message = "That template could not be found.";
            return false;
        }

        ApplyTemplateToMobile(target, entry);
        message = $"Template '{entry.Name}' has been loaded onto {target.Name}.";
        return true;
    }

    public static bool LoadTemplateToStaff(Mobile actor, uint ownerSerial, Guid templateId, out string message)
    {
        EnsureInitialized();

        if (actor == null || actor.AccessLevel <= AccessLevel.Player)
        {
            message = "You do not have access to that action.";
            return false;
        }

        var store = GetStore(ownerSerial);
        if (store == null)
        {
            message = "No templates were found for that character.";
            return false;
        }

        var entry = FindTemplate(store, templateId);
        if (entry == null)
        {
            message = "That template could not be found.";
            return false;
        }

        ApplyTemplateToMobile(actor, entry);
        message = $"Template '{entry.Name}' has been loaded onto {actor.Name}.";
        return true;
    }

    public static bool DeleteTemplate(Mobile actor, uint ownerSerial, Guid templateId, out string message)
    {
        EnsureInitialized();

        if (actor == null)
        {
            message = "Invalid user.";
            return false;
        }

        var selfDelete = actor.Serial.Value == ownerSerial;
        if (!selfDelete && actor.AccessLevel <= AccessLevel.Player)
        {
            message = "You do not have access to that action.";
            return false;
        }

        if (selfDelete && !CanUse(actor, out message))
        {
            return false;
        }

        var store = GetStore(ownerSerial);
        if (store == null)
        {
            message = "No templates were found for that character.";
            return false;
        }

        var entry = FindTemplate(store, templateId);
        if (entry == null)
        {
            message = "That template could not be found.";
            return false;
        }

        store.Templates.Remove(entry);

        var deletedEntry = new DeletedCharacterTemplateEntry
        {
            DeletedId = Guid.NewGuid(),
            Template = CloneTemplate(entry),
            DeletedAt = Core.Now,
            DeletedBy = actor.Name
        };

        store.DeletedTemplates.Insert(0, deletedEntry);

        while (store.DeletedTemplates.Count > MaxDeletedHistoryPerCharacter)
        {
            store.DeletedTemplates.RemoveAt(store.DeletedTemplates.Count - 1);
        }

        ArchiveDeletedTemplate(ownerSerial, store.OwnerName, deletedEntry);
        Save();

        message = $"Template '{entry.Name}' has been deleted.";
        return true;
    }

    public static bool DeleteTemplateAsStaff(Mobile actor, uint ownerSerial, Guid templateId, out string message)
    {
        EnsureInitialized();

        if (actor == null || actor.AccessLevel <= AccessLevel.Player)
        {
            message = "You do not have access to that action.";
            return false;
        }

        var store = GetStore(ownerSerial);
        if (store == null)
        {
            message = "No templates were found for that character.";
            return false;
        }

        var entry = FindTemplate(store, templateId);
        if (entry == null)
        {
            message = "That template could not be found.";
            return false;
        }

        store.Templates.Remove(entry);

        var deletedEntry = new DeletedCharacterTemplateEntry
        {
            DeletedId = Guid.NewGuid(),
            Template = CloneTemplate(entry),
            DeletedAt = Core.Now,
            DeletedBy = actor.Name
        };

        store.DeletedTemplates.Insert(0, deletedEntry);

        while (store.DeletedTemplates.Count > MaxDeletedHistoryPerCharacter)
        {
            store.DeletedTemplates.RemoveAt(store.DeletedTemplates.Count - 1);
        }

        ArchiveDeletedTemplate(ownerSerial, store.OwnerName, deletedEntry);
        Save();

        message = $"Template '{entry.Name}' has been removed from {store.OwnerName}.";
        return true;
    }

    public static bool RestoreMostRecentDeleted(Mobile from, out string message)
    {
        EnsureInitialized();

        if (!CanUse(from, out message))
        {
            return false;
        }

        var store = GetStore(from.Serial.Value);
        if (store == null || store.DeletedTemplates.Count == 0)
        {
            message = "There is no deleted template to restore.";
            return false;
        }

        if (store.Templates.Count >= GetTemplateSlotLimit(from))
        {
            message = $"You have reached your template limit ({GetTemplateSlotLimit(from)}).";
            return false;
        }

        var deleted = store.DeletedTemplates[0];
        var restored = CloneTemplate(deleted.Template);

        restored.Id = Guid.NewGuid();
        restored.OwnerSerial = from.Serial.Value;
        restored.OwnerName = from.Name;
        restored.UpdatedAt = Core.Now;

        store.Templates.Add(restored);
        store.DeletedTemplates.RemoveAt(0);
        Save();

        message = $"Template '{restored.Name}' has been restored.";
        return true;
    }

    public static bool RestoreArchivedDeletedAsStaff(
        Mobile actor,
        Guid deletedId,
        Mobile restoreTarget,
        bool removeFromArchive,
        out string message
    )
    {
        EnsureInitialized();

        if (actor == null || actor.AccessLevel <= AccessLevel.Player)
        {
            message = "You do not have access to that action.";
            return false;
        }

        if (restoreTarget == null || restoreTarget.Deleted)
        {
            message = "That restore target is invalid.";
            return false;
        }

        var deleted = FindArchivedDeletedTemplate(deletedId);
        if (deleted == null)
        {
            message = "That deleted template could not be found.";
            return false;
        }

        ApplyTemplateToMobile(restoreTarget, deleted.Template);

        if (removeFromArchive)
        {
            _deletedArchive.Entries.Remove(deleted);
            SaveDeletedArchive();
        }

        message = $"Deleted template '{deleted.Template.Name}' has been restored onto {restoreTarget.Name}.";
        return true;
    }

    public static bool RestoreArchivedDeletedToOwnerAsStaff(
        Mobile actor,
        Guid deletedId,
        bool removeFromArchive,
        out string message
    )
    {
        EnsureInitialized();

        if (actor == null || actor.AccessLevel <= AccessLevel.Player)
        {
            message = "You do not have access to that action.";
            return false;
        }

        var deleted = FindArchivedDeletedTemplate(deletedId);
        if (deleted == null)
        {
            message = "That deleted template could not be found.";
            return false;
        }

        var owner = World.FindMobile((Serial)deleted.OwnerSerial);
        if (owner == null || owner.Deleted)
        {
            message = "The original character could not be found online.";
            return false;
        }

        return RestoreArchivedDeletedAsStaff(actor, deletedId, owner, removeFromArchive, out message);
    }

    public static bool PurgeArchivedDeletedTemplate(Mobile actor, Guid deletedId, out string message)
    {
        EnsureInitialized();

        if (actor == null || actor.AccessLevel <= AccessLevel.Player)
        {
            message = "You do not have access to that action.";
            return false;
        }

        var deleted = FindArchivedDeletedTemplate(deletedId);
        if (deleted == null)
        {
            message = "That deleted template could not be found.";
            return false;
        }

        _deletedArchive.Entries.Remove(deleted);
        SaveDeletedArchive();

        message = $"Deleted template '{deleted.Template.Name}' has been purged.";
        return true;
    }

    public static bool GrabTargetTemplate(Mobile actor, Mobile target, string templateName, out string message)
    {
        EnsureInitialized();

        if (actor == null || actor.AccessLevel <= AccessLevel.Player)
        {
            message = "You do not have access to that action.";
            return false;
        }

        if (target == null || target.Deleted)
        {
            message = "That target is invalid.";
            return false;
        }

        templateName = NormalizeTemplateName(templateName);
        if (string.IsNullOrWhiteSpace(templateName))
        {
            templateName = $"{target.Name} Snapshot";
        }

        var store = GetOrCreateStore(actor);

        if (store.Templates.Count >= GetTemplateSlotLimit(actor))
        {
            message = $"You have reached your template limit ({GetTemplateSlotLimit(actor)}).";
            return false;
        }

        for (var i = 0; i < store.Templates.Count; i++)
        {
            if (store.Templates[i].Name.Equals(templateName, StringComparison.OrdinalIgnoreCase))
            {
                message = "A template with that name already exists.";
                return false;
            }
        }

        var entry = BuildTemplateSnapshot(target, templateName);
        entry.Id = Guid.NewGuid();
        entry.OwnerSerial = actor.Serial.Value;
        entry.OwnerName = actor.Name;
        entry.UpdatedAt = Core.Now;

        store.Templates.Add(entry);
        Save();

        message = $"Grabbed template '{entry.Name}' from {target.Name}.";
        return true;
    }

    public static bool SetExtraSlots(Mobile actor, uint ownerSerial, int amount, out string message)
    {
        EnsureInitialized();

        if (actor == null || actor.AccessLevel <= AccessLevel.Player)
        {
            message = "You do not have access to that action.";
            return false;
        }

        if (amount < 0)
        {
            message = "Extra slots cannot be negative.";
            return false;
        }

        var store = GetStore(ownerSerial);
        if (store == null)
        {
            var owner = World.FindMobile((Serial)ownerSerial);
            if (owner == null)
            {
                store = new CharacterTemplateStore
                {
                    OwnerSerial = ownerSerial,
                    OwnerName = $"0x{ownerSerial:X8}",
                    ExtraSlots = amount
                };

                _state.Characters.Add(store);
            }
            else
            {
                store = GetOrCreateStore(owner);
                store.ExtraSlots = amount;
            }
        }
        else
        {
            store.ExtraSlots = amount;
        }

        Save();

        message = $"Extra template slots set to {amount}. Total slots: {DefaultTemplateSlots + amount}.";
        return true;
    }

    public static int GetExtraSlots(uint ownerSerial)
    {
        var store = GetStore(ownerSerial);
        return store?.ExtraSlots ?? 0;
    }

    public static int GetExtraSlots(Mobile owner) => GetExtraSlots(owner.Serial.Value);

    public static void Save()
    {
        EnsureInitialized();
        JsonConfig.Serialize(SavePath, _state);
        SaveDeletedArchive();
    }

    private static void SaveDeletedArchive()
    {
        EnsureInitialized();
        JsonConfig.Serialize(DeletedArchivePath, _deletedArchive);
    }

    private static void UpdateOwnerDisplayName(CharacterTemplateStore store, Mobile owner)
    {
        if (store == null || owner == null)
        {
            return;
        }

        if (!string.Equals(store.OwnerName, owner.Name, StringComparison.Ordinal))
        {
            store.OwnerName = owner.Name;
        }
    }

    private static string NormalizeTemplateName(string templateName)
    {
        if (templateName == null)
        {
            return null;
        }

        templateName = templateName.Trim();

        if (templateName.Length > 40)
        {
            templateName = templateName[..40];
        }

        return templateName;
    }

    private static CharacterTemplateEntry BuildTemplateSnapshot(Mobile source, string templateName)
    {
        var entry = new CharacterTemplateEntry
        {
            Id = Guid.NewGuid(),
            OwnerSerial = source.Serial.Value,
            OwnerName = source.Name,
            Name = templateName,
            CreatedAt = Core.Now,
            UpdatedAt = Core.Now,
            Stats = new StatTemplateSnapshot
            {
                Str = source.RawStr,
                Dex = source.RawDex,
                Int = source.RawInt,
                StrLock = source.StrLock,
                DexLock = source.DexLock,
                IntLock = source.IntLock
            },
            Skills = new List<SkillTemplateSnapshot>()
        };

        for (var i = 0; i < source.Skills.Length; i++)
        {
            var skill = source.Skills[i];

            if (skill == null || skill.Base <= 0.0)
            {
                continue;
            }

            entry.Skills.Add(
                new SkillTemplateSnapshot
                {
                    SkillIndex = i,
                    SkillName = skill.Info?.Name ?? ((SkillName)i).ToString(),
                    Base = skill.Base,
                    Lock = skill.Lock
                }
            );
        }

        return entry;
    }

    private static CharacterTemplateEntry CloneTemplate(CharacterTemplateEntry source)
    {
        var clone = new CharacterTemplateEntry
        {
            Id = Guid.NewGuid(),
            OwnerSerial = source.OwnerSerial,
            OwnerName = source.OwnerName,
            Name = source.Name,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            Stats = new StatTemplateSnapshot
            {
                Str = source.Stats.Str,
                Dex = source.Stats.Dex,
                Int = source.Stats.Int,
                StrLock = source.Stats.StrLock,
                DexLock = source.Stats.DexLock,
                IntLock = source.Stats.IntLock
            },
            Skills = new List<SkillTemplateSnapshot>()
        };

        for (var i = 0; i < source.Skills.Count; i++)
        {
            var skill = source.Skills[i];

            clone.Skills.Add(
                new SkillTemplateSnapshot
                {
                    SkillIndex = skill.SkillIndex,
                    SkillName = skill.SkillName,
                    Base = skill.Base,
                    Lock = skill.Lock
                }
            );
        }

        return clone;
    }

    private static CharacterTemplateEntry FindTemplate(CharacterTemplateStore store, Guid templateId)
    {
        for (var i = 0; i < store.Templates.Count; i++)
        {
            if (store.Templates[i].Id == templateId)
            {
                return store.Templates[i];
            }
        }

        return null;
    }

    private static DeletedCharacterTemplateEntry FindDeletedTemplate(CharacterTemplateStore store, Guid deletedId)
    {
        for (var i = 0; i < store.DeletedTemplates.Count; i++)
        {
            if (store.DeletedTemplates[i].DeletedId == deletedId)
            {
                return store.DeletedTemplates[i];
            }
        }

        return null;
    }

    private static DeletedTemplateArchiveEntry FindArchivedDeletedTemplate(Guid deletedId)
    {
        for (var i = 0; i < _deletedArchive.Entries.Count; i++)
        {
            if (_deletedArchive.Entries[i].DeletedId == deletedId)
            {
                return _deletedArchive.Entries[i];
            }
        }

        return null;
    }

    private static void ArchiveDeletedTemplate(uint ownerSerial, string ownerName, DeletedCharacterTemplateEntry deletedEntry)
    {
        var archiveEntry = new DeletedTemplateArchiveEntry
        {
            DeletedId = deletedEntry.DeletedId,
            OwnerSerial = ownerSerial,
            OwnerName = ownerName,
            Template = CloneTemplate(deletedEntry.Template),
            DeletedAt = deletedEntry.DeletedAt,
            DeletedBy = deletedEntry.DeletedBy
        };

        _deletedArchive.Entries.Insert(0, archiveEntry);

        while (_deletedArchive.Entries.Count > MaxDeletedArchiveEntries)
        {
            _deletedArchive.Entries.RemoveAt(_deletedArchive.Entries.Count - 1);
        }
    }

    private static void ApplyTemplateToMobile(Mobile target, CharacterTemplateEntry entry)
    {
        if (target == null || entry == null)
        {
            return;
        }

        target.RawStr = entry.Stats.Str;
        target.RawDex = entry.Stats.Dex;
        target.RawInt = entry.Stats.Int;

        target.StrLock = entry.Stats.StrLock;
        target.DexLock = entry.Stats.DexLock;
        target.IntLock = entry.Stats.IntLock;

        for (var i = 0; i < target.Skills.Length; i++)
        {
            var skill = target.Skills[i];

            if (skill == null)
            {
                continue;
            }

            skill.Base = 0.0;
            skill.SetLockNoRelay(SkillLock.Up);
        }

        for (var i = 0; i < entry.Skills.Count; i++)
        {
            var snapshot = entry.Skills[i];

            if (snapshot.SkillIndex < 0 || snapshot.SkillIndex >= target.Skills.Length)
            {
                continue;
            }

            var skill = target.Skills[snapshot.SkillIndex];
            if (skill == null)
            {
                continue;
            }

            skill.Base = snapshot.Base;
            skill.SetLockNoRelay(snapshot.Lock);
        }

        target.Hits = target.HitsMax;
        target.Stam = target.StamMax;
        target.Mana = target.ManaMax;
    }
}
