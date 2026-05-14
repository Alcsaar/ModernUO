using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom.Engines.ActivityTracking;

public static class ActivityTrackingService
{
    private const int MaxRecentCreatureKills = 100;
    private const int MaxPlayerActivities = 200;

    private static readonly Dictionary<uint, ActivityTrackingPlayerData> _players = new();
    private static readonly Dictionary<uint, string> _lastRecordedRegion = new();
    private static readonly List<CreatureKillRecord> _recentCreatureKills = new();
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ActivityTrackingService));
    private static bool _includeStaffMembers = false;
    private static bool _debugEnabled = false;

    public static Dictionary<uint, string> LastRecordedRegion => _lastRecordedRegion;
    public static event Action<CreatureKillRecord> CreatureKillRecorded;

    public static bool DebugEnabled
    {
        get => _debugEnabled;
        private set => _debugEnabled = value;
    }

    public static IReadOnlyList<CreatureKillRecord> RecentCreatureKills
    {
        get
        {
            return _recentCreatureKills.ToArray();
        }
    }

    public static bool IncludeStaffMembers
    {
        get => _includeStaffMembers;
        set => _includeStaffMembers = value;
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: runtime status metrics and memory estimate */
    public static int PlayerCount
    {
        get
        {
            return _players.Count;
        }
    }

    public static int RegionCount
    {
        get
        {
            return _lastRecordedRegion.Count;
        }
    }

    public static int RecentKillCount
    {
        get
        {
            return _recentCreatureKills.Count;
        }
    }

    public static long GetEstimatedMemoryUsage()
    {
        long memory = 0;

        // Rough estimate: 1KB per player data
        memory += _players.Count * 1024L;

        /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: include capped per-player activity strings in runtime memory estimate */
        foreach (var playerData in _players.Values)
        {
            memory += playerData.Activities.Count * 192L;
        }
        /* END ACTIVITY TRACKING CUSTOMIZATION */

        // Rough estimate: 2KB per recent kill record
        memory += _recentCreatureKills.Count * 2048L;

        // Rough estimate: 128 bytes per region entry
        memory += _lastRecordedRegion.Count * 128L;

        return memory;
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    public static void Configure()
    {
        /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: region movement tracking is intentionally part of runtime activity status */
        EventSink.Movement += ActivityTrackingRegions.OnMovement;
        /* END ACTIVITY TRACKING CUSTOMIZATION */
    }

    public static void Initialize()
    {
        // No-op for now.
    }

    public static void LoadPlayerData()
    {
        // No persistence layer; nothing to load.
    }

    public static void ReloadData()
    {
        // No persistence layer; nothing to reload.
    }

    public static void SavePlayerData()
    {
        // No persistence layer; nothing to save.
    }

    public static void SaveAllData()
    {
        // No persistence layer; nothing to save.
    }

    public static void ResetData()
    {
        _players.Clear();
        _lastRecordedRegion.Clear();
        _recentCreatureKills.Clear();
    }

    public static void ToggleDebug()
    {
        DebugEnabled = !DebugEnabled;
    }

    public static void ClearRecentKills()
    {
        _recentCreatureKills.Clear();
    }

    public static void RecordPlayerActivity(PlayerMobile player, string activityType)
    {
        if (player == null || string.IsNullOrWhiteSpace(activityType))
        {
            return;
        }

        var data = GetOrCreatePlayerData(player);
        AddPlayerActivity(data, $"{DateTime.UtcNow:O}: {activityType}");
        data.LastUpdatedUtc = DateTime.UtcNow;
    }

    public static void RecordCreatureKill(PlayerMobile player, BaseCreature creature)
    {
        if (player == null || creature == null)
        {
            return;
        }

        if (!ShouldTrackPlayer(player))
        {
            return;
        }

        var rights = BaseCreature.GetLootingRights(creature.DamageEntries, creature.HitsMax);

        if (rights == null || rights.Count == 0)
        {
            rights = new List<DamageStore> { new DamageStore(player, 0) };
        }

        RecordCreatureKill(creature, rights);
    }

    public static void RecordSkillMilestone(PlayerMobile player, SkillName skill, double oldBase)
    {
        ActivityTrackingSkills.RecordSkillMilestone(player, skill, oldBase);
    }

    public static void RecordCreatureKill(BaseCreature creature, List<DamageStore> rights)
    {
        if (creature == null)
        {
            return;
        }

        rights ??= BaseCreature.GetLootingRights(creature.DamageEntries, creature.HitsMax);

        var participants = BuildParticipants(rights, creature);
        if (participants.Count == 0)
        {
            return;
        }

        UpdatePlayerKillStats(creature, participants);

        var record = CreateCreatureKillRecord(creature, participants);

        _recentCreatureKills.Add(record);

        while (_recentCreatureKills.Count > MaxRecentCreatureKills)
        {
            _recentCreatureKills.RemoveAt(0);
        }

        CreatureKillRecorded?.Invoke(record);

        if (DebugEnabled)
        {
            WriteDebug(record);
        }
    }

    public static bool ShouldTrackPlayer(PlayerMobile player)
    {
        if (player == null)
        {
            return false;
        }

        if (!_includeStaffMembers && player.AccessLevel > AccessLevel.Player)
        {
            return false;
        }

        return true;
    }

    public static ActivityTrackingPlayerData GetOrCreatePlayerData(PlayerMobile player)
    {
        var serial = player.Serial.Value;

        if (!_players.TryGetValue(serial, out var data))
        {
            data = new ActivityTrackingPlayerData
            {
                PlayerSerial = serial,
                PlayerName = player.Name,
                AccountName = player.Account?.Username ?? string.Empty,
                FirstSeenUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            };

            _players[serial] = data;
        }
        else if (data.PlayerName != player.Name)
        {
            data.PlayerName = player.Name;
            data.LastUpdatedUtc = DateTime.UtcNow;
        }

        return data;
    }

    private static void UpdatePlayerKillStats(BaseCreature creature, List<CreatureKillParticipant> participants)
    {
        var creatureTypeName = creature.GetType().Name;
        var timestamp = DateTime.UtcNow;

        foreach (var participant in participants)
        {
            if (!participant.IsPlayer)
            {
                continue;
            }

            var data = GetOrCreatePlayerData(participant.PlayerMobile!);
            data.TotalKills++;
            data.MonsterKills[creatureTypeName] = data.MonsterKills.TryGetValue(creatureTypeName, out var count) ? count + 1 : 1;
            data.LastCreatureKilled = creatureTypeName;
            AddPlayerActivity(data, $"{timestamp:O}: Killed {creatureTypeName} ({creature.Name ?? creatureTypeName}) in {creature.Region?.Name ?? "Unknown"}");
            data.LastUpdatedUtc = timestamp;
        }
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: cap per-player activity history for long server uptimes */
    internal static void AddPlayerActivity(ActivityTrackingPlayerData data, string activity)
    {
        if (data == null || string.IsNullOrWhiteSpace(activity))
        {
            return;
        }

        data.Activities.Add(activity);

        while (data.Activities.Count > MaxPlayerActivities)
        {
            data.Activities.RemoveAt(0);
        }
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    private static List<CreatureKillParticipant> BuildParticipants(List<DamageStore> rights, BaseCreature creature)
    {
        var participants = new List<CreatureKillParticipant>();
        var seen = new HashSet<uint>();

        if (rights == null)
        {
            rights = new List<DamageStore>();
        }

        foreach (var ds in rights)
        {
            if (ds?.m_Mobile == null)
            {
                continue;
            }

            var mobile = ds.m_Mobile;
            var serial = mobile.Serial.Value;

            if (!seen.Add(serial))
            {
                continue;
            }

            if (mobile is PlayerMobile pm && !ShouldTrackPlayer(pm))
            {
                continue;
            }

            participants.Add(new CreatureKillParticipant
            {
                ParticipantSerial = serial,
                ParticipantName = mobile.Name ?? mobile.GetType().Name,
                ParticipantType = mobile.GetType().Name,
                AccountName = mobile is PlayerMobile player ? player.Account?.Username ?? string.Empty : string.Empty,
                Damage = ds.m_Damage,
                HasLootRight = ds.m_HasRight,
                IsPlayer = mobile is PlayerMobile,
                PlayerMobile = mobile as PlayerMobile
            });
        }

        /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: include bard provoker participation for provoked kills */
        if (creature?.BardProvoked == true && creature.BardMaster != null)
        {
            var bard = creature.BardMaster;

            if (bard is PlayerMobile playerBard && !ShouldTrackPlayer(playerBard))
            {
                return participants;
            }

            var bardSerial = bard.Serial.Value;
            if (seen.Add(bardSerial))
            {
                participants.Add(new CreatureKillParticipant
                {
                    ParticipantSerial = bardSerial,
                    ParticipantName = bard.Name ?? "Bard",
                    ParticipantType = bard.GetType().Name,
                    AccountName = bard is PlayerMobile bardPlayer ? bardPlayer.Account?.Username ?? string.Empty : string.Empty,
                    Damage = 0,
                    HasLootRight = false,
                    IsPlayer = bard is PlayerMobile,
                    PlayerMobile = bard as PlayerMobile,
                    IsBardProvocationCredit = true
                });
            }
        }
        /* END ACTIVITY TRACKING CUSTOMIZATION */

        return participants;
    }

    private static CreatureKillRecord CreateCreatureKillRecord(BaseCreature creature, List<CreatureKillParticipant> participants)
    {
        var primary = GetPrimaryParticipant(participants);

        return new CreatureKillRecord
        {
            TimestampUtc = DateTime.UtcNow,
            CreatureSerial = creature.Serial.Value,
            CreatureName = creature.Name ?? creature.GetType().Name,
            CreatureType = creature.GetType().Name,
            Location = creature.Location,
            Map = creature.Map?.ToString() ?? "Unknown",
            RegionName = creature.Region?.Name ?? "Unknown",
            Participants = participants,
            PrimaryParticipantSerial = primary?.ParticipantSerial ?? 0
        };
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: colored kill debug output and participant detail logging */
    private static void WriteDebug(CreatureKillRecord record)
    {
        if (record == null)
        {
            return;
        }

        var primary = GetPrimaryParticipant(record.Participants);
        var primaryInfo = primary != null ? $"Primary={primary.ParticipantName}/{primary.AccountName}" : "Primary=None";
        var summary = $"[ActivityTracking] {record.TimestampUtc:O}: {record.CreatureType} '{record.CreatureName}' killed at {record.Map} {record.Location} region={record.RegionName}. {primaryInfo}. Participants={record.Participants.Count}.";

        try
        {
            Utility.PushColor(ConsoleColor.Yellow);
            _logger.Information(summary);
        }
        finally
        {
            Utility.PopColor();
        }

        try
        {
            Utility.PushColor(ConsoleColor.Cyan);
            foreach (var participant in record.Participants)
            {
                _logger.Information(
                    "  {ParticipantName}/{AccountName} ({ParticipantType}) dmg={Damage} lootRight={HasLootRight}",
                    participant.ParticipantName,
                    participant.AccountName,
                    participant.ParticipantType,
                    participant.Damage,
                    participant.HasLootRight
                );
            }
        }
        finally
        {
            Utility.PopColor();
        }

        foreach (var ns in NetState.Instances)
        {
            if (ns.Mobile is PlayerMobile pm && pm.AccessLevel >= AccessLevel.Counselor)
            {
                pm.SendMessage(0x35, summary);

                foreach (var participant in record.Participants)
                {
                    var credit = participant.IsBardProvocationCredit ? " bardCredit=True" : string.Empty;
                    pm.SendMessage(0x35, $"  {participant.ParticipantName}({participant.ParticipantType}) acct={participant.AccountName} dmg={participant.Damage} lootRight={participant.HasLootRight}{credit}");
                }
            }
        }
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    private static CreatureKillParticipant GetPrimaryParticipant(IReadOnlyList<CreatureKillParticipant> participants)
    {
        CreatureKillParticipant primary = null;

        for (var i = 0; i < participants.Count; i++)
        {
            var participant = participants[i];

            if (primary == null || participant.Damage > primary.Damage)
            {
                primary = participant;
            }
        }

        return primary;
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: colored skill milestone debug output */
    public static void WriteSkillDebug(PlayerMobile player, SkillName skill, int milestoneLevel)
    {
        if (player == null)
        {
            return;
        }

        var accountName = player.Account?.Username ?? "Unknown";
        var summary = $"[ActivityTracking] {DateTime.UtcNow:O}: Player {player.Name}/{accountName} reached {milestoneLevel} on {skill}.";

        try
        {
            Utility.PushColor(ConsoleColor.Green);
            _logger.Information(summary);
        }
        finally
        {
            Utility.PopColor();
        }

        foreach (var ns in NetState.Instances)
        {
            if (ns.Mobile is PlayerMobile pm && pm.AccessLevel >= AccessLevel.Counselor)
            {
                pm.SendMessage(0x35, summary);
            }
        }
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    public sealed class ActivityTrackingPlayerData
    {
        public uint PlayerSerial { get; set; }

        public string PlayerName { get; set; }

        public string AccountName { get; set; }

        public DateTime FirstSeenUtc { get; set; }

        public DateTime LastUpdatedUtc { get; set; }

        public int TotalKills { get; set; }

        public long TotalGoldEarned { get; set; }

        public string LastCreatureKilled { get; set; }

        public Dictionary<string, int> MonsterKills { get; set; } = new();

        public Dictionary<string, List<SkillMilestoneRecord>> SkillMilestones { get; set; } = new();

        public HashSet<string> ExploredRegionNames { get; set; } = new();

        public HashSet<string> ExploredLocationKeys { get; set; } = new();

        public List<string> Activities { get; set; } = new();
    }

    public sealed class CreatureKillRecord
    {
        public DateTime TimestampUtc { get; set; }
        public uint CreatureSerial { get; set; }
        public string CreatureName { get; set; }
        public string CreatureType { get; set; }
        public Point3D Location { get; set; }
        public string Map { get; set; }
        public string RegionName { get; set; }
        public IReadOnlyList<CreatureKillParticipant> Participants { get; set; } = Array.Empty<CreatureKillParticipant>();
        public uint PrimaryParticipantSerial { get; set; }
    }

    public sealed class CreatureKillParticipant
    {
        public uint ParticipantSerial { get; set; }
        public string ParticipantName { get; set; }
        public string ParticipantType { get; set; }
        public string AccountName { get; set; }
        public int Damage { get; set; }
        public bool HasLootRight { get; set; }
        public bool IsPlayer { get; set; }
        public PlayerMobile? PlayerMobile { get; set; }
        public bool IsBardProvocationCredit { get; set; }
    }
}

public sealed class SkillMilestoneRecord
{
    public int MilestoneLevel { get; set; }
    public DateTime ReachedUtc { get; set; }
}
