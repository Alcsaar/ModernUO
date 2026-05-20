using System;
using System.Collections.Generic;

namespace Server.Custom.Systems.MissionSystem;

public static class MissionGenerator
{
    public const int CompletionLimitPerCadence = 3;

    public static void SeedDefaults(List<MissionDefinition> definitions)
    {
        if (definitions == null)
        {
            return;
        }

        RemoveIfExists(definitions, "daily-doom-patrol");
        RemoveIfExists(definitions, "weekly-doom-contract");

        AddIfMissing(
            definitions,
            CreateKillCreature(
                "daily-mongbat-cull",
                "Mongbat Cull",
                "Thin the mongbat packs threatening nearby roads.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Common,
                "Mongbat",
                12,
                25,
                10
            )
        );

        AddIfMissing(
            definitions,
            CreateKillCreature(
                "daily-ettin-watch",
                "Ettin Watch",
                "Put down ettins before they wander into settled lands.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Uncommon,
                "Ettin",
                8,
                200,
                8
            )
        );

        AddIfMissing(
            definitions,
            CreateKillFamily(
                "daily-orc-pressure",
                "Orc Pressure",
                "Break up orc warbands before they gather strength.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Uncommon,
                "orcs",
                string.Empty,
                "Orc,OrcBomber,OrcBrute,OrcCaptain,OrcishLord,OrcishMage",
                15,
                125,
                7
            )
        );

        AddIfMissing(
            definitions,
            CreateKillCreature(
                "daily-lizardman-orders",
                "Lizardman Orders",
                "Disrupt lizardman scouts before they reinforce their lairs.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Common,
                "Lizardman",
                12,
                100,
                8
            )
        );

        AddIfMissing(
            definitions,
            CreateKillCreature(
                "daily-harpy-nuisance",
                "Harpy Nuisance",
                "Clear harpies from the wild approaches.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Uncommon,
                "Harpy",
                10,
                150,
                7
            )
        );

        AddIfMissing(
            definitions,
            CreateKillFamily(
                "daily-undead-sweep",
                "Graveyard Sweep",
                "Lay restless undead back to rest.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Common,
                "undead",
                string.Empty,
                "Skeleton,Zombie,Ghoul,Wraith,Spectre,Shade,Mummy",
                14,
                85,
                7
            )
        );

        AddIfMissing(
            definitions,
            CreateKillFamily(
                "daily-elemental-instability",
                "Elemental Instability",
                "Quiet unstable elementals before they spread.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Uncommon,
                "elementals",
                string.Empty,
                "EarthElemental,AirElemental,FireElemental,WaterElemental",
                12,
                200,
                6
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "daily-despise-patrol",
                "Despise Patrol",
                "Defeat monsters while operating inside Despise.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Uncommon,
                "Despise",
                string.Empty,
                10,
                150,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "daily-covetous-patrol",
                "Covetous Patrol",
                "Defeat monsters while operating inside Covetous.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Uncommon,
                "Covetous",
                string.Empty,
                12,
                150,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "daily-shame-patrol",
                "Shame Patrol",
                "Defeat monsters while operating inside Shame.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Uncommon,
                "Shame",
                string.Empty,
                12,
                175,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "daily-deceit-patrol",
                "Deceit Patrol",
                "Defeat monsters while operating inside Deceit.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Uncommon,
                "Deceit",
                string.Empty,
                12,
                175,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "daily-destard-patrol",
                "Destard Patrol",
                "Defeat monsters while operating inside Destard.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Elite,
                "Destard",
                string.Empty,
                12,
                500,
                4
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "daily-wrong-patrol",
                "Wrong Patrol",
                "Defeat monsters while operating inside Wrong.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Uncommon,
                "Wrong",
                string.Empty,
                12,
                175,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "daily-hythloth-patrol",
                "Hythloth Patrol",
                "Defeat monsters while operating inside Hythloth.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Elite,
                "Hythloth",
                string.Empty,
                12,
                500,
                4
            )
        );

        AddIfMissing(
            definitions,
            CreateKillCreature(
                "weekly-dragon-writ",
                "Dragon Writ",
                "Hunt dragons for a high-risk contract.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Elite,
                "Dragon",
                50,
                1000,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillCreature(
                "weekly-balron-writ",
                "Balron Writ",
                "Banish balrons for a dangerous high-value contract.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Elite,
                "Balron",
                20,
                1500,
                3
            )
        );

        AddIfMissing(
            definitions,
            CreateKillCreature(
                "weekly-ancient-lich-writ",
                "Ancient Lich Writ",
                "Destroy ancient liches before their influence spreads.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Elite,
                "AncientLich",
                30,
                900,
                3
            )
        );

        AddIfMissing(
            definitions,
            CreateKillFamily(
                "weekly-daemon-contract",
                "Daemon Contract",
                "Banish daemonic threats wherever they gather.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Elite,
                "daemons",
                string.Empty,
                "Daemon,Balron,IceFiend,Imp,Gargoyle,StoneGargoyle",
                90,
                650,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillFamily(
                "weekly-reptile-contract",
                "Scaled Contract",
                "Cull dangerous reptiles and their kin.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Veteran,
                "reptiles",
                string.Empty,
                "Lizardman,Snake,GiantSerpent,Drake,Dragon,Wyvern",
                120,
                350,
                6
            )
        );

        AddIfMissing(
            definitions,
            CreateKillFamily(
                "weekly-undead-contract",
                "Undead Contract",
                "Break a large concentration of undead over the week.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Veteran,
                "undead",
                string.Empty,
                "Skeleton,Zombie,Ghoul,Wraith,Spectre,Shade,Mummy,Lich,AncientLich",
                110,
                300,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "weekly-destard-contract",
                "Destard Contract",
                "Clear hostile creatures from the caverns of Destard.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Elite,
                "Destard",
                string.Empty,
                100,
                650,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "weekly-shame-contract",
                "Shame Contract",
                "Hold the line against hostile creatures inside Shame.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Veteran,
                "Shame",
                string.Empty,
                110,
                450,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "weekly-hythloth-contract",
                "Hythloth Contract",
                "Defeat monsters while operating inside Hythloth.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Elite,
                "Hythloth",
                string.Empty,
                100,
                650,
                4
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "weekly-deceit-contract",
                "Deceit Contract",
                "Destroy monsters while inside Deceit.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Veteran,
                "Deceit",
                string.Empty,
                100,
                350,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "weekly-covetous-contract",
                "Covetous Contract",
                "Hold the line against hostile creatures inside Covetous.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Veteran,
                "Covetous",
                string.Empty,
                100,
                350,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "weekly-despise-contract",
                "Despise Contract",
                "Hold the line against hostile creatures inside Despise.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Veteran,
                "Despise",
                string.Empty,
                100,
                350,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "weekly-wrong-contract",
                "Wrong Contract",
                "Defeat monsters while operating inside Wrong.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Veteran,
                "Wrong",
                string.Empty,
                100,
                350,
                5
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "daily-fire-dungeon-patrol",
                "Fire Dungeon Patrol",
                "Defeat monsters while operating inside Fire Dungeon.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Elite,
                "Fire Dungeon",
                string.Empty,
                12,
                500,
                4
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "daily-ice-dungeon-patrol",
                "Ice Dungeon Patrol",
                "Defeat monsters while operating inside Ice Dungeon.",
                MissionCadence.DailyMissive,
                MissionDifficulty.Elite,
                "Ice Dungeon",
                string.Empty,
                12,
                500,
                4
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "weekly-fire-dungeon-contract",
                "Fire Dungeon Contract",
                "Defeat monsters while operating inside Fire Dungeon.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Elite,
                "Fire Dungeon",
                string.Empty,
                100,
                650,
                4
            )
        );

        AddIfMissing(
            definitions,
            CreateKillRegion(
                "weekly-ice-dungeon-contract",
                "Ice Dungeon Contract",
                "Defeat monsters while operating inside Ice Dungeon.",
                MissionCadence.WeeklyContract,
                MissionDifficulty.Elite,
                "Ice Dungeon",
                string.Empty,
                100,
                650,
                4
            )
        );
    }

    public static void RefreshProfile(PlayerMissionProfile profile, IReadOnlyList<MissionDefinition> definitions, MissionCadence cadence, DateTime now)
    {
        if (profile == null)
        {
            return;
        }

        var list = GetList(profile, cadence);
        list.Clear();

        var completionReset = cadence == MissionCadence.DailyMissive
            ? profile.DailyMissivesCompleted = 0
            : profile.WeeklyContractsCompleted = 0;

        _ = completionReset;

        var available = BuildAvailableDefinitions(definitions, cadence);
        var expiresAt = cadence == MissionCadence.DailyMissive ? now.Date.AddDays(1) : now.Date.AddDays(7);

        for (var i = 0; i < available.Count; i++)
        {
            var definition = available[i];

            list.Add(
                new PlayerMissionInstance
                {
                    InstanceId = Guid.NewGuid().ToString("N"),
                    DefinitionId = definition.Id,
                    Cadence = cadence,
                    RequiredProgress = definition.Objective?.RequiredCount ?? 1,
                    AssignedAt = now,
                    ExpiresAt = expiresAt
                }
            );
        }

        if (cadence == MissionCadence.DailyMissive)
        {
            profile.LastDailyMissiveRefresh = now;
        }
        else
        {
            profile.LastWeeklyContractRefresh = now;
        }
    }

    private static MissionDefinition CreateKillCreature(
        string id,
        string title,
        string description,
        MissionCadence cadence,
        MissionDifficulty difficulty,
        string creatureTypeName,
        int required,
        int expectedGoldPerKill,
        int weight
    )
    {
        return new MissionDefinition
        {
            Id = id,
            Title = title,
            Description = description,
            Cadence = cadence,
            Difficulty = difficulty,
            Objective = new KillCreatureObjective { CreatureTypeName = creatureTypeName, RequiredCount = required },
            Reward = new MissionReward { Gold = MissionRewardCalculator.CalculateGold(cadence, difficulty, required, expectedGoldPerKill) },
            Weight = weight,
            Enabled = true
        };
    }

    private static MissionDefinition CreateKillFamily(
        string id,
        string title,
        string description,
        MissionCadence cadence,
        MissionDifficulty difficulty,
        string familyName,
        string baseTypeName,
        string typeNames,
        int required,
        int expectedGoldPerKill,
        int weight
    )
    {
        return new MissionDefinition
        {
            Id = id,
            Title = title,
            Description = description,
            Cadence = cadence,
            Difficulty = difficulty,
            Objective = new KillCreatureFamilyObjective
            {
                FamilyName = familyName,
                BaseTypeName = baseTypeName,
                CreatureTypeNames = typeNames,
                RequiredCount = required
            },
            Reward = new MissionReward { Gold = MissionRewardCalculator.CalculateGold(cadence, difficulty, required, expectedGoldPerKill) },
            Weight = weight,
            Enabled = true
        };
    }

    private static MissionDefinition CreateKillRegion(
        string id,
        string title,
        string description,
        MissionCadence cadence,
        MissionDifficulty difficulty,
        string regionName,
        string regionTypeName,
        int required,
        int expectedGoldPerKill,
        int weight
    )
    {
        return new MissionDefinition
        {
            Id = id,
            Title = title,
            Description = description,
            Cadence = cadence,
            Difficulty = difficulty,
            Objective = new KillRegionObjective { RegionName = regionName, RegionTypeName = regionTypeName, RequiredCount = required },
            Reward = new MissionReward { Gold = MissionRewardCalculator.CalculateGold(cadence, difficulty, required, expectedGoldPerKill) },
            Weight = weight,
            Enabled = true
        };
    }

    private static void AddIfMissing(List<MissionDefinition> definitions, MissionDefinition definition)
    {
        for (var i = 0; i < definitions.Count; i++)
        {
            if (string.Equals(definitions[i].Id, definition.Id, StringComparison.OrdinalIgnoreCase))
            {
                definitions[i] = definition;
                return;
            }
        }

        definitions.Add(definition);
    }

    private static void RemoveIfExists(List<MissionDefinition> definitions, string id)
    {
        for (var i = definitions.Count - 1; i >= 0; i--)
        {
            if (string.Equals(definitions[i].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                definitions.RemoveAt(i);
            }
        }
    }

    private static List<MissionDefinition> BuildAvailableDefinitions(IReadOnlyList<MissionDefinition> definitions, MissionCadence cadence)
    {
        var available = new List<MissionDefinition>();

        if (definitions == null)
        {
            return available;
        }

        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];

            if (definition?.Enabled == true && definition.Cadence == cadence && definition.Objective != null)
            {
                available.Add(definition);
            }
        }

        return available;
    }

    private static List<PlayerMissionInstance> GetList(PlayerMissionProfile profile, MissionCadence cadence)
    {
        return cadence == MissionCadence.DailyMissive ? profile.DailyMissives : profile.WeeklyContracts;
    }
}
