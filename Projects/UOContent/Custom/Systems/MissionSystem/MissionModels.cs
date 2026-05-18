using System;
using System.Collections.Generic;

namespace Server.Custom.Systems.MissionSystem;

public enum MissionCadence
{
    DailyMissive,
    WeeklyContract
}

public enum MissionDifficulty
{
    Common,
    Uncommon,
    Veteran,
    Elite
}

public sealed class MissionDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MissionCadence Cadence { get; set; }
    public MissionDifficulty Difficulty { get; set; }
    public MissionObjective Objective { get; set; }
    public MissionReward Reward { get; set; } = new();
    public int Weight { get; set; } = 1;
    public bool Enabled { get; set; } = true;
}

public sealed class PlayerMissionProfile
{
    public Serial PlayerSerial { get; set; }
    public List<PlayerMissionInstance> DailyMissives { get; set; } = new();
    public List<PlayerMissionInstance> WeeklyContracts { get; set; } = new();
    public int DailyMissivesCompleted { get; set; }
    public int WeeklyContractsCompleted { get; set; }
    public DateTime LastDailyMissiveRefresh { get; set; }
    public DateTime LastWeeklyContractRefresh { get; set; }
}

public sealed class PlayerMissionInstance
{
    public string InstanceId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public MissionCadence Cadence { get; set; }
    public int CurrentProgress { get; set; }
    public int RequiredProgress { get; set; }
    public bool Accepted { get; set; }
    public bool Completed { get; set; }
    public bool Claimed { get; set; }
    public bool Cancelled { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
