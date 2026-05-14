using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Text;

namespace Server.Custom.Engines.ActivityTracking;

public static class ActivityTrackingService
{
    private const int MaxRecentCreatureKills = 100;
    private const int MaxPlayerActivities = 200;

    private static readonly Dictionary<uint, ActivityTrackingPlayerData> _players = new();
    private static readonly Dictionary<uint, string> _lastRecordedRegion = new();
    private static readonly Dictionary<uint, MonsterCorpseGoldRecord> _monsterCorpseGold = new();
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

    public static int MonsterCorpseGoldRecordCount
    {
        get
        {
            return _monsterCorpseGold.Count;
        }
    }

    public static int TotalDeathCount
    {
        get
        {
            var count = 0;

            foreach (var data in _players.Values)
            {
                count += data.TotalDeaths;
            }

            return count;
        }
    }

    public static long TotalGoldEarned
    {
        get
        {
            long total = 0;

            foreach (var data in _players.Values)
            {
                total += data.TotalGoldEarned;
            }

            return total;
        }
    }

    public static long TotalMonsterGoldLooted
    {
        get
        {
            long total = 0;

            foreach (var data in _players.Values)
            {
                total += data.TotalMonsterGoldLooted;
            }

            return total;
        }
    }

    public static long TotalNpcVendorGoldEarned
    {
        get
        {
            long total = 0;

            foreach (var data in _players.Values)
            {
                total += data.TotalNpcVendorGoldEarned;
            }

            return total;
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
            memory += playerData.ExploredRegions.Count * 160L;
        }
        /* END ACTIVITY TRACKING CUSTOMIZATION */

        /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: include active monster corpse gold records in runtime memory estimate */
        memory += _monsterCorpseGold.Count * 96L;
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
        _monsterCorpseGold.Clear();
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

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: player death and gold source tracking */
    public static void RecordPlayerDeath(PlayerMobile player, Mobile killer)
    {
        if (player == null || !ShouldTrackPlayer(player))
        {
            return;
        }

        var data = GetOrCreatePlayerData(player);
        var timestamp = DateTime.UtcNow;

        data.TotalDeaths++;
        data.LastDeathUtc = timestamp;
        data.LastDeathRegion = player.Region?.Name ?? "Unknown";
        data.LastDeathMap = player.Map?.ToString() ?? "Unknown";
        data.LastDeathLocation = player.Location;
        data.LastKillerSerial = killer?.Serial.Value ?? 0;
        data.LastKillerName = killer?.Name ?? "Unknown";
        data.LastKillerType = killer?.GetType().Name ?? "Unknown";

        AddPlayerActivity(data, $"{timestamp:O}: Died to {data.LastKillerName} ({data.LastKillerType}, {data.LastKillerSerial}) in {data.LastDeathRegion}");
        data.LastUpdatedUtc = timestamp;

        if (DebugEnabled)
        {
            WriteDeathDebug(player, data);
        }
    }

    public static void RegisterMonsterCorpseGold(BaseCreature creature, Container corpseContainer)
    {
        if (creature == null || corpseContainer is not Corpse corpse || corpse.Deleted || creature.Summoned || creature.Controlled || creature.Owners.Count > 0)
        {
            return;
        }

        var totalGold = CountGold(corpse);

        if (totalGold <= 0)
        {
            return;
        }

        _monsterCorpseGold[corpse.Serial.Value] = new MonsterCorpseGoldRecord
        {
            CreatureSerial = creature.Serial.Value,
            CreatureName = creature.Name ?? creature.GetType().Name,
            CreatureType = creature.GetType().Name,
            RegionName = creature.Region?.Name ?? "Unknown",
            Map = creature.Map?.ToString() ?? "Unknown",
            Location = creature.Location,
            RemainingGold = totalGold
        };
    }

    public static void RecordMonsterCorpseGoldLooted(Mobile from, Corpse corpse, Item item)
    {
        if (from is not PlayerMobile player || corpse == null || item is not Gold gold || !ShouldTrackPlayer(player))
        {
            return;
        }

        if (!_monsterCorpseGold.TryGetValue(corpse.Serial.Value, out var record) || record.RemainingGold <= 0)
        {
            return;
        }

        var creditedGold = Math.Min(gold.Amount, record.RemainingGold);

        if (creditedGold <= 0)
        {
            return;
        }

        record.RemainingGold -= creditedGold;

        if (record.RemainingGold <= 0)
        {
            _monsterCorpseGold.Remove(corpse.Serial.Value);
        }

        RecordGoldEarned(
            player,
            creditedGold,
            ActivityGoldSource.MonsterCorpse,
            record.CreatureSerial,
            record.CreatureName,
            record.CreatureType,
            new ActivityLocation
            {
                RegionName = player.Region?.Name ?? record.RegionName,
                Map = player.Map?.ToString() ?? record.Map,
                Location = player.Location
            }
        );
    }

    public static void ClearMonsterCorpseGold(Corpse corpse)
    {
        if (corpse == null)
        {
            return;
        }

        _monsterCorpseGold.Remove(corpse.Serial.Value);
    }

    public static void RecordVendorGoldEarned(
        Mobile seller,
        BaseVendor vendor,
        int amount,
        IReadOnlyList<VendorSaleLine> saleLines
    )
    {
        if (seller is not PlayerMobile player || vendor == null || amount <= 0 || !ShouldTrackPlayer(player))
        {
            return;
        }

        RecordGoldEarned(
            player,
            amount,
            ActivityGoldSource.NpcVendorSale,
            vendor.Serial.Value,
            vendor.Name ?? vendor.GetType().Name,
            vendor.GetType().Name,
            new ActivityLocation
            {
                RegionName = vendor.Region?.Name ?? "Unknown",
                Map = vendor.Map?.ToString() ?? "Unknown",
                Location = vendor.Location
            },
            saleLines
        );
    }

    private static void RecordGoldEarned(
        PlayerMobile player,
        int amount,
        ActivityGoldSource source,
        uint sourceSerial,
        string sourceName,
        string sourceType,
        ActivityLocation location,
        IReadOnlyList<VendorSaleLine> saleLines = null
    )
    {
        var data = GetOrCreatePlayerData(player);
        var timestamp = DateTime.UtcNow;

        data.TotalGoldEarned += amount;

        switch (source)
        {
            case ActivityGoldSource.MonsterCorpse:
                data.TotalMonsterGoldLooted += amount;
                break;
            case ActivityGoldSource.NpcVendorSale:
                data.TotalNpcVendorGoldEarned += amount;
                break;
        }

        var activity = source == ActivityGoldSource.NpcVendorSale && saleLines?.Count > 0
            ? $"{timestamp:O}: Earned {amount} gold from {source} ({sourceName}/{sourceType}/{sourceSerial}) at {location.Map} {location.Location} region={location.RegionName} selling {BuildVendorSaleSummary(saleLines)}"
            : $"{timestamp:O}: Earned {amount} gold from {source} ({sourceName}/{sourceType}/{sourceSerial}) at {location.Map} {location.Location} region={location.RegionName}";

        AddPlayerActivity(data, activity);
        data.LastUpdatedUtc = timestamp;

        if (DebugEnabled)
        {
            WriteGoldDebug(player, amount, source, sourceSerial, sourceName, sourceType, location, saleLines);
        }
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

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

    private static int CountGold(Container container)
    {
        var total = 0;
        var items = container.Items;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (item is Gold gold)
            {
                total += gold.Amount;
            }
            else if (item is Container childContainer)
            {
                total += CountGold(childContainer);
            }
        }

        return total;
    }

    private static string BuildVendorSaleSummary(IReadOnlyList<VendorSaleLine> saleLines)
    {
        using var builder = ValueStringBuilder.Create();

        for (var i = 0; i < saleLines.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var line = saleLines[i];
            builder.Append(line.Quantity);
            builder.Append('x');
            builder.Append(line.ItemType);
            builder.Append('=');
            builder.Append(line.TotalPrice);
        }

        return builder.ToString();
    }

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

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: colored death and gold debug output */
    private static void WriteDeathDebug(PlayerMobile player, ActivityTrackingPlayerData data)
    {
        var accountName = player.Account?.Username ?? "Unknown";
        var summary = $"[ActivityTracking] {DateTime.UtcNow:O}: Player {player.Name}/{accountName}/{data.PlayerSerial} died to {data.LastKillerName} ({data.LastKillerType}/{data.LastKillerSerial}) at {data.LastDeathMap} {data.LastDeathLocation} region={data.LastDeathRegion}.";

        try
        {
            Utility.PushColor(ConsoleColor.Red);
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

    private static void WriteGoldDebug(
        PlayerMobile player,
        int amount,
        ActivityGoldSource source,
        uint sourceSerial,
        string sourceName,
        string sourceType,
        ActivityLocation location,
        IReadOnlyList<VendorSaleLine> saleLines
    )
    {
        var accountName = player.Account?.Username ?? "Unknown";
        var summary = $"[ActivityTracking] {DateTime.UtcNow:O}: Player {player.Name}/{accountName}/{player.Serial.Value} earned {amount} gold from {source} ({sourceName}/{sourceType}/{sourceSerial}) at {location.Map} {location.Location} region={location.RegionName}.";

        try
        {
            Utility.PushColor(ConsoleColor.DarkYellow);
            _logger.Information(summary);

            if (source == ActivityGoldSource.NpcVendorSale && saleLines?.Count > 0)
            {
                for (var i = 0; i < saleLines.Count; i++)
                {
                    var line = saleLines[i];
                    _logger.Information(
                        "  Sold {Quantity}x {ItemName} ({ItemType}, {ItemSerial}) at {UnitPrice} each for {TotalPrice} gold",
                        line.Quantity,
                        line.ItemName,
                        line.ItemType,
                        line.ItemSerial,
                        line.UnitPrice,
                        line.TotalPrice
                    );
                }
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

                if (source == ActivityGoldSource.NpcVendorSale && saleLines?.Count > 0)
                {
                    for (var i = 0; i < saleLines.Count; i++)
                    {
                        var line = saleLines[i];
                        pm.SendMessage(0x35, $"  Sold {line.Quantity}x {line.ItemName} ({line.ItemType}/{line.ItemSerial}) at {line.UnitPrice} each for {line.TotalPrice} gold");
                    }
                }
            }
        }
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

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

        public int TotalDeaths { get; set; }

        public long TotalGoldEarned { get; set; }

        public long TotalMonsterGoldLooted { get; set; }

        public long TotalNpcVendorGoldEarned { get; set; }

        public string LastCreatureKilled { get; set; }

        public DateTime LastDeathUtc { get; set; }

        public string LastDeathRegion { get; set; }

        public string LastDeathMap { get; set; }

        public Point3D LastDeathLocation { get; set; }

        public uint LastKillerSerial { get; set; }

        public string LastKillerName { get; set; }

        public string LastKillerType { get; set; }

        public Dictionary<string, int> MonsterKills { get; set; } = new();

        public Dictionary<string, List<SkillMilestoneRecord>> SkillMilestones { get; set; } = new();

        public HashSet<string> ExploredRegionNames { get; set; } = new();

        public Dictionary<string, RegionEntryRecord> ExploredRegions { get; set; } = new();

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

    public sealed class VendorSaleLine
    {
        public uint ItemSerial { get; set; }
        public uint SellerSerial { get; set; }
        public uint VendorSerial { get; set; }
        public string RegionName { get; set; }
        public string Map { get; set; }
        public Point3D Location { get; set; }
        public string ItemType { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public int UnitPrice { get; set; }
        public int TotalPrice { get; set; }
    }

    private sealed class MonsterCorpseGoldRecord
    {
        public uint CreatureSerial { get; set; }
        public string CreatureName { get; set; }
        public string CreatureType { get; set; }
        public string RegionName { get; set; }
        public string Map { get; set; }
        public Point3D Location { get; set; }
        public int RemainingGold { get; set; }
    }

    private sealed class ActivityLocation
    {
        public string RegionName { get; set; }
        public string Map { get; set; }
        public Point3D Location { get; set; }
    }

    private enum ActivityGoldSource
    {
        MonsterCorpse,
        NpcVendorSale
    }
}

public sealed class SkillMilestoneRecord
{
    public int MilestoneLevel { get; set; }
    public DateTime ReachedUtc { get; set; }
}

public sealed class RegionEntryRecord
{
    public string RegionName { get; set; }
    public DateTime FirstEnteredUtc { get; set; }
    public string Map { get; set; }
    public Point3D Location { get; set; }
}
