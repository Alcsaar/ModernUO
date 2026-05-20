using System;
using System.Collections.Generic;

namespace Server.Custom.Systems.MissionSystem;

public sealed class MissionSystemPersistence : GenericPersistence
{
    private static MissionSystemPersistence _instance;

    public static void Configure()
    {
        _instance ??= new MissionSystemPersistence();
    }

    private MissionSystemPersistence() : base("MissionSystem", 3)
    {
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(1); // version
        MissionSystemService.SerializePersistence(writer);
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        switch (version)
        {
            case 0:
                {
                    MissionSystemService.DeserializePersistence(reader, version);
                    break;
                }
            case 1:
                {
                    MissionSystemService.DeserializePersistence(reader, version);
                    break;
                }
        }
    }
}

public static class MissionPersistenceSerializer
{
    public static void WriteDefinitions(IGenericWriter writer, List<MissionDefinition> definitions)
    {
        writer.WriteEncodedInt(definitions?.Count ?? 0);

        if (definitions == null)
        {
            return;
        }

        for (var i = 0; i < definitions.Count; i++)
        {
            WriteDefinition(writer, definitions[i]);
        }
    }

    public static List<MissionDefinition> ReadDefinitions(IGenericReader reader)
    {
        var count = reader.ReadEncodedInt();
        var definitions = new List<MissionDefinition>(count);

        for (var i = 0; i < count; i++)
        {
            definitions.Add(ReadDefinition(reader));
        }

        return definitions;
    }

    public static void WriteProfiles(IGenericWriter writer, Dictionary<Serial, PlayerMissionProfile> profiles)
    {
        writer.WriteEncodedInt(profiles?.Count ?? 0);

        if (profiles == null)
        {
            return;
        }

        foreach (var profile in profiles.Values)
        {
            WriteProfile(writer, profile);
        }
    }

    public static Dictionary<Serial, PlayerMissionProfile> ReadProfiles(IGenericReader reader, int version)
    {
        var count = reader.ReadEncodedInt();
        var profiles = new Dictionary<Serial, PlayerMissionProfile>(count);

        for (var i = 0; i < count; i++)
        {
            var profile = ReadProfile(reader, version);
            profiles[profile.PlayerSerial] = profile;
        }

        return profiles;
    }

    private static void WriteDefinition(IGenericWriter writer, MissionDefinition definition)
    {
        writer.Write(definition?.Id);
        writer.Write(definition?.Title);
        writer.Write(definition?.Description);
        writer.WriteEnum(definition?.Cadence ?? MissionCadence.DailyMissive);
        writer.WriteEnum(definition?.Difficulty ?? MissionDifficulty.Common);
        WriteObjective(writer, definition?.Objective);
        WriteReward(writer, definition?.Reward);
        writer.WriteEncodedInt(definition?.Weight ?? 1);
        writer.Write(definition?.Enabled ?? false);
    }

    private static MissionDefinition ReadDefinition(IGenericReader reader)
    {
        return new MissionDefinition
        {
            Id = reader.ReadString(),
            Title = reader.ReadString(),
            Description = reader.ReadString(),
            Cadence = reader.ReadEnum<MissionCadence>(),
            Difficulty = reader.ReadEnum<MissionDifficulty>(),
            Objective = ReadObjective(reader),
            Reward = ReadReward(reader),
            Weight = reader.ReadEncodedInt(),
            Enabled = reader.ReadBool()
        };
    }

    private static void WriteObjective(IGenericWriter writer, MissionObjective objective)
    {
        writer.WriteEnum(objective?.Kind ?? MissionObjectiveKind.KillCreature);

        switch (objective)
        {
            case KillCreatureObjective killCreature:
                {
                    writer.Write(killCreature.CreatureTypeName);
                    writer.WriteEncodedInt(killCreature.RequiredCount);
                    break;
                }
            case KillCreatureFamilyObjective killFamily:
                {
                    writer.Write(killFamily.FamilyName);
                    writer.Write(killFamily.BaseTypeName);
                    writer.Write(killFamily.CreatureTypeNames);
                    writer.WriteEncodedInt(killFamily.RequiredCount);
                    break;
                }
            case KillRegionObjective killRegion:
                {
                    writer.Write(killRegion.RegionName);
                    writer.Write(killRegion.RegionTypeName);
                    writer.WriteEncodedInt(killRegion.RequiredCount);
                    break;
                }
            default:
                {
                    writer.Write(string.Empty);
                    writer.WriteEncodedInt(0);
                    break;
                }
        }
    }

    private static MissionObjective ReadObjective(IGenericReader reader)
    {
        var kind = reader.ReadEnum<MissionObjectiveKind>();

        return kind switch
        {
            MissionObjectiveKind.KillCreatureFamily => new KillCreatureFamilyObjective
            {
                FamilyName = reader.ReadString(),
                BaseTypeName = reader.ReadString(),
                CreatureTypeNames = reader.ReadString(),
                RequiredCount = reader.ReadEncodedInt()
            },
            MissionObjectiveKind.KillRegion => new KillRegionObjective
            {
                RegionName = reader.ReadString(),
                RegionTypeName = reader.ReadString(),
                RequiredCount = reader.ReadEncodedInt()
            },
            _ => new KillCreatureObjective
            {
                CreatureTypeName = reader.ReadString(),
                RequiredCount = reader.ReadEncodedInt()
            }
        };
    }

    private static void WriteReward(IGenericWriter writer, MissionReward reward)
    {
        writer.WriteEncodedInt(reward?.Gold ?? 0);
    }

    private static MissionReward ReadReward(IGenericReader reader)
    {
        return new MissionReward
        {
            Gold = reader.ReadEncodedInt()
        };
    }

    private static void WriteProfile(IGenericWriter writer, PlayerMissionProfile profile)
    {
        writer.Write(profile?.PlayerSerial ?? Serial.Zero);
        WriteInstances(writer, profile?.DailyMissives);
        WriteInstances(writer, profile?.WeeklyContracts);
        writer.WriteEncodedInt(profile?.DailyMissivesCompleted ?? 0);
        writer.WriteEncodedInt(profile?.WeeklyContractsCompleted ?? 0);
        writer.Write(profile?.LastDailyMissiveRefresh ?? DateTime.MinValue);
        writer.Write(profile?.LastWeeklyContractRefresh ?? DateTime.MinValue);
    }

    private static PlayerMissionProfile ReadProfile(IGenericReader reader, int version)
    {
        return new PlayerMissionProfile
        {
            PlayerSerial = reader.ReadSerial(),
            DailyMissives = ReadInstances(reader, version),
            WeeklyContracts = ReadInstances(reader, version),
            DailyMissivesCompleted = reader.ReadEncodedInt(),
            WeeklyContractsCompleted = reader.ReadEncodedInt(),
            LastDailyMissiveRefresh = reader.ReadDateTime(),
            LastWeeklyContractRefresh = reader.ReadDateTime()
        };
    }

    private static void WriteInstances(IGenericWriter writer, List<PlayerMissionInstance> instances)
    {
        writer.WriteEncodedInt(instances?.Count ?? 0);

        if (instances == null)
        {
            return;
        }

        for (var i = 0; i < instances.Count; i++)
        {
            WriteInstance(writer, instances[i]);
        }
    }

    private static List<PlayerMissionInstance> ReadInstances(IGenericReader reader, int version)
    {
        var count = reader.ReadEncodedInt();
        var instances = new List<PlayerMissionInstance>(count);

        for (var i = 0; i < count; i++)
        {
            instances.Add(ReadInstance(reader, version));
        }

        return instances;
    }

    private static void WriteInstance(IGenericWriter writer, PlayerMissionInstance instance)
    {
        writer.Write(instance?.InstanceId);
        writer.Write(instance?.DefinitionId);
        writer.WriteEnum(instance?.Cadence ?? MissionCadence.DailyMissive);
        writer.WriteEncodedInt(instance?.CurrentProgress ?? 0);
        writer.WriteEncodedInt(instance?.RequiredProgress ?? 0);
        writer.Write(instance?.Accepted ?? false);
        writer.Write(instance?.Completed ?? false);
        writer.Write(instance?.Claimed ?? false);
        writer.Write(instance?.Cancelled ?? false);
        writer.Write(instance?.AssignedAt ?? DateTime.MinValue);
        writer.Write(instance?.ExpiresAt ?? DateTime.MinValue);
    }

    private static PlayerMissionInstance ReadInstance(IGenericReader reader, int version)
    {
        return new PlayerMissionInstance
        {
            InstanceId = reader.ReadString(),
            DefinitionId = reader.ReadString(),
            Cadence = reader.ReadEnum<MissionCadence>(),
            CurrentProgress = reader.ReadEncodedInt(),
            RequiredProgress = reader.ReadEncodedInt(),
            Accepted = reader.ReadBool(),
            Completed = reader.ReadBool(),
            Claimed = reader.ReadBool(),
            Cancelled = version >= 1 && reader.ReadBool(),
            AssignedAt = reader.ReadDateTime(),
            ExpiresAt = reader.ReadDateTime()
        };
    }
}
