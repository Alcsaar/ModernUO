using System;
using System.Collections.Generic;
using Server.Custom.Systems.CustomFeatureFlags;
using Server.Engines.Events;
using Server.Logging;
using Server.Mobiles;

namespace Server.Custom.Systems.MissionSystem;

public static class MissionSystem
{
    public static void Configure()
    {
        MissionSystemService.Configure();
    }

    public static void Initialize()
    {
        MissionSystemService.Initialize();
    }
}

public static class MissionSystemService
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(MissionSystemService));
    private static readonly List<MissionDefinition> _definitions = new();
    private static readonly Dictionary<Serial, PlayerMissionProfile> _profiles = new();
    private static ScheduledEvent _dailyResetEvent;
    private static ScheduledEvent _weeklyResetEvent;
    private static DateTime _lastDailyReset;
    private static DateTime _lastWeeklyReset;

    public static IReadOnlyList<MissionDefinition> Definitions => _definitions;
    public static DateTime LastDailyReset => _lastDailyReset;
    public static DateTime LastWeeklyReset => _lastWeeklyReset;

    public static void Configure()
    {
        MissionSystemPersistence.Configure();
        MissionCommands.Configure();
        MissionProgressService.Configure();
    }

    public static void Initialize()
    {
        ScheduleResets();
    }

    public static bool IsSystemEnabled()
    {
        return CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.MissionSystem);
    }

    public static void DisplayBoard(PlayerMobile player, MissionBoardView view = MissionBoardView.DailyMissives, int page = 0, bool staffBypass = false)
    {
        if (player?.NetState == null)
        {
            return;
        }

        if (!staffBypass && !IsSystemEnabled())
        {
            player.SendMessage(0x22, "The mission board is not available right now.");
            return;
        }

        EnsureProfileOffers(player);
        MissionBoardGump.DisplayTo(player, view, page, staffBypass);
    }

    public static PlayerMissionProfile GetOrCreateProfile(PlayerMobile player)
    {
        if (player == null)
        {
            return null;
        }

        if (!_profiles.TryGetValue(player.Serial, out var profile))
        {
            profile = new PlayerMissionProfile
            {
                PlayerSerial = player.Serial
            };

            _profiles[player.Serial] = profile;
        }

        return profile;
    }

    public static MissionDefinition GetDefinition(string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            return null;
        }

        for (var i = 0; i < _definitions.Count; i++)
        {
            if (string.Equals(_definitions[i].Id, definitionId, StringComparison.OrdinalIgnoreCase))
            {
                return _definitions[i];
            }
        }

        return null;
    }

    public static void EnsureProfileOffers(PlayerMobile player)
    {
        var profile = GetOrCreateProfile(player);

        if (profile == null)
        {
            return;
        }

        var now = Core.Now;

        if (profile.DailyMissives.Count == 0 || profile.LastDailyMissiveRefresh.Date < now.Date)
        {
            MissionGenerator.RefreshProfile(profile, _definitions, MissionCadence.DailyMissive, now);
        }

        if (profile.WeeklyContracts.Count == 0 || GetWeekStart(profile.LastWeeklyContractRefresh) < GetWeekStart(now))
        {
            MissionGenerator.RefreshProfile(profile, _definitions, MissionCadence.WeeklyContract, now);
        }
    }

    public static bool AcceptMission(PlayerMobile player, string instanceId)
    {
        if (!IsSystemEnabled() && player?.AccessLevel == AccessLevel.Player)
        {
            player.SendMessage(0x22, "The mission board is not available right now.");
            return false;
        }

        var instance = FindInstance(player, instanceId, out var profile);

        if (instance == null || profile == null)
        {
            return false;
        }

        if (instance.Accepted || instance.Completed || instance.Claimed)
        {
            return false;
        }

        if (!CanCompleteMore(profile, instance.Cadence))
        {
            player.SendMessage(0x22, GetLimitMessage(instance.Cadence));
            return false;
        }

        instance.Accepted = true;
        player.SendMessage(0x35, $"{GetCadenceName(instance.Cadence)} accepted.");
        return true;
    }

    public static bool ClaimMission(PlayerMobile player, string instanceId)
    {
        if (!IsSystemEnabled() && player?.AccessLevel == AccessLevel.Player)
        {
            player.SendMessage(0x22, "The mission board is not available right now.");
            return false;
        }

        var instance = FindInstance(player, instanceId, out var profile);

        if (instance == null || profile == null || !instance.Completed || instance.Claimed)
        {
            return false;
        }

        // Completed missions already reserved their cadence slot when accepted.
        // Re-checking the available-slot limit here blocks reward claims once every slot is full.
        var definition = GetDefinition(instance.DefinitionId);

        if (definition == null)
        {
            player.SendMessage(0x22, "That mission definition no longer exists.");
            return false;
        }

        instance.Claimed = true;

        if (instance.Cadence == MissionCadence.DailyMissive)
        {
            profile.DailyMissivesCompleted++;
        }
        else
        {
            profile.WeeklyContractsCompleted++;
        }

        definition.Reward?.GrantTo(player);
        player.SendMessage(0x35, $"A bank check for {definition.Reward?.Gold ?? 0} gold has been deposited into your bank.");
        return true;
    }

    public static bool CancelMission(PlayerMobile player, string instanceId)
    {
        if (!IsSystemEnabled() && player?.AccessLevel == AccessLevel.Player)
        {
            player.SendMessage(0x22, "The mission board is not available right now.");
            return false;
        }

        var instance = FindInstance(player, instanceId, out _);

        if (instance == null || !instance.Accepted || instance.Completed || instance.Claimed)
        {
            return false;
        }

        instance.Accepted = false;
        instance.CurrentProgress = 0;
        instance.Cancelled = true;
        player.SendMessage(0x35, $"{GetCadenceName(instance.Cadence)} cancelled.");
        return true;
    }

    public static void RecordKillCredit(MissionKillCredit credit)
    {
        if (credit?.Player == null)
        {
            return;
        }

        if (!credit.StaffTestCredit && credit.Player.AccessLevel > AccessLevel.Player)
        {
            return;
        }

        if (!IsSystemEnabled())
        {
            return;
        }

        var profile = GetOrCreateProfile(credit.Player);

        if (profile == null)
        {
            return;
        }

        ApplyCredit(profile.DailyMissives, credit);
        ApplyCredit(profile.WeeklyContracts, credit);
    }

    public static int AddProgress(PlayerMobile player, int amount, bool staffTestCredit)
    {
        var profile = GetOrCreateProfile(player);

        if (profile == null || amount <= 0)
        {
            return 0;
        }

        var applied = 0;
        applied += AddProgress(profile.DailyMissives, amount);
        applied += AddProgress(profile.WeeklyContracts, amount);

        return applied;
    }

    public static void ResetDaily(PlayerMobile player)
    {
        var profile = GetOrCreateProfile(player);

        if (profile != null)
        {
            MissionGenerator.RefreshProfile(profile, _definitions, MissionCadence.DailyMissive, Core.Now);
        }
    }

    public static void ResetWeekly(PlayerMobile player)
    {
        var profile = GetOrCreateProfile(player);

        if (profile != null)
        {
            MissionGenerator.RefreshProfile(profile, _definitions, MissionCadence.WeeklyContract, Core.Now);
        }
    }

    public static void ResetAllDaily()
    {
        foreach (var profile in _profiles.Values)
        {
            MissionGenerator.RefreshProfile(profile, _definitions, MissionCadence.DailyMissive, Core.Now);
        }

        _lastDailyReset = Core.Now;
    }

    public static void ResetAllWeekly()
    {
        foreach (var profile in _profiles.Values)
        {
            MissionGenerator.RefreshProfile(profile, _definitions, MissionCadence.WeeklyContract, Core.Now);
        }

        _lastWeeklyReset = Core.Now;
    }

    public static int ReseedAndResetAll()
    {
        var addedOrUpdated = SeedDefaults();

        foreach (var profile in _profiles.Values)
        {
            MissionGenerator.RefreshProfile(profile, _definitions, MissionCadence.DailyMissive, Core.Now);
            MissionGenerator.RefreshProfile(profile, _definitions, MissionCadence.WeeklyContract, Core.Now);
        }

        _lastDailyReset = Core.Now;
        _lastWeeklyReset = Core.Now;
        return addedOrUpdated;
    }

    public static int SeedDefaults()
    {
        var before = _definitions.Count;
        MissionGenerator.SeedDefaults(_definitions);
        return _definitions.Count - before;
    }

    public static int GetRemainingCompletions(PlayerMissionProfile profile, MissionCadence cadence)
    {
        if (profile == null)
        {
            return 0;
        }

        var completed = cadence == MissionCadence.DailyMissive ? profile.DailyMissivesCompleted : profile.WeeklyContractsCompleted;
        var active = CountActiveReserved(profile, cadence);

        return Math.Max(0, MissionGenerator.CompletionLimitPerCadence - completed - active);
    }

    public static IReadOnlyList<PlayerMissionInstance> GetInstances(PlayerMissionProfile profile, MissionCadence cadence)
    {
        if (profile == null)
        {
            return Array.Empty<PlayerMissionInstance>();
        }

        return cadence == MissionCadence.DailyMissive ? profile.DailyMissives : profile.WeeklyContracts;
    }

    public static List<PlayerMissionInstance> GetCompletedUnclaimed(PlayerMissionProfile profile)
    {
        var completed = new List<PlayerMissionInstance>();

        if (profile == null)
        {
            return completed;
        }

        AddCompletedUnclaimed(profile.DailyMissives, completed);
        AddCompletedUnclaimed(profile.WeeklyContracts, completed);
        return completed;
    }

    public static List<PlayerMissionInstance> GetActiveMissions(PlayerMissionProfile profile)
    {
        var active = new List<PlayerMissionInstance>();

        if (profile == null)
        {
            return active;
        }

        AddActive(profile.DailyMissives, active);
        AddActive(profile.WeeklyContracts, active);
        return active;
    }

    public static string GetCadenceName(MissionCadence cadence)
    {
        return cadence == MissionCadence.DailyMissive ? "Daily Missive" : "Weekly Contract";
    }

    public static void SerializePersistence(IGenericWriter writer)
    {
        writer.Write(_lastDailyReset);
        writer.Write(_lastWeeklyReset);
        MissionPersistenceSerializer.WriteDefinitions(writer, _definitions);
        MissionPersistenceSerializer.WriteProfiles(writer, _profiles);
    }

    public static void DeserializePersistence(IGenericReader reader, int version)
    {
        _lastDailyReset = reader.ReadDateTime();
        _lastWeeklyReset = reader.ReadDateTime();
        _definitions.Clear();
        _definitions.AddRange(MissionPersistenceSerializer.ReadDefinitions(reader));
        _profiles.Clear();

        var profiles = MissionPersistenceSerializer.ReadProfiles(reader, version);
        foreach (var profile in profiles)
        {
            _profiles[profile.Key] = profile.Value;
        }
    }

    private static void ScheduleResets()
    {
        _dailyResetEvent?.Cancel();
        _weeklyResetEvent?.Cancel();

        var now = Core.Now;
        var nextDaily = now.Date.AddDays(1);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;

        if (daysUntilMonday == 0)
        {
            daysUntilMonday = 7;
        }

        var nextWeekly = now.Date.AddDays(daysUntilMonday);

        _dailyResetEvent = EventScheduler.DailyAt(nextDaily, ResetAllDaily);
        _weeklyResetEvent = EventScheduler.WeeklyAt(nextWeekly, ResetAllWeekly);
    }

    private static void ApplyCredit(List<PlayerMissionInstance> instances, MissionKillCredit credit)
    {
        for (var i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];

            if (!instance.Accepted || instance.Completed || instance.Claimed)
            {
                continue;
            }

            var definition = GetDefinition(instance.DefinitionId);

            if (definition?.Objective == null || !definition.Objective.Matches(credit))
            {
                continue;
            }

            instance.CurrentProgress = Math.Min(instance.RequiredProgress, instance.CurrentProgress + 1);

            if (instance.CurrentProgress >= instance.RequiredProgress)
            {
                instance.Completed = true;
                credit.Player.SendMessage(0x35, $"{definition.Title} is complete. Return to a mission board to claim your reward.");
            }
            else
            {
                credit.Player.SendMessage(0x35, $"{definition.Title}: {instance.CurrentProgress}/{instance.RequiredProgress}");
            }
        }
    }

    private static int AddProgress(List<PlayerMissionInstance> instances, int amount)
    {
        var applied = 0;

        for (var i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];

            if (!instance.Accepted || instance.Completed || instance.Claimed)
            {
                continue;
            }

            instance.CurrentProgress = Math.Min(instance.RequiredProgress, instance.CurrentProgress + amount);
            applied++;

            if (instance.CurrentProgress >= instance.RequiredProgress)
            {
                instance.Completed = true;
            }
        }

        return applied;
    }

    private static PlayerMissionInstance FindInstance(PlayerMobile player, string instanceId, out PlayerMissionProfile profile)
    {
        profile = GetOrCreateProfile(player);

        if (profile == null || string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        var instance = FindInstance(profile.DailyMissives, instanceId);
        return instance ?? FindInstance(profile.WeeklyContracts, instanceId);
    }

    private static PlayerMissionInstance FindInstance(List<PlayerMissionInstance> instances, string instanceId)
    {
        for (var i = 0; i < instances.Count; i++)
        {
            if (string.Equals(instances[i].InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                return instances[i];
            }
        }

        return null;
    }

    private static bool CanCompleteMore(PlayerMissionProfile profile, MissionCadence cadence)
    {
        return GetRemainingCompletions(profile, cadence) > 0;
    }

    private static int CountActiveReserved(PlayerMissionProfile profile, MissionCadence cadence)
    {
        var instances = cadence == MissionCadence.DailyMissive ? profile.DailyMissives : profile.WeeklyContracts;
        var count = 0;

        for (var i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];

            if (instance.Accepted && !instance.Claimed)
            {
                count++;
            }
        }

        return count;
    }

    private static string GetLimitMessage(MissionCadence cadence)
    {
        return cadence == MissionCadence.DailyMissive
            ? "You have completed all available Daily Missives for today."
            : "You have completed all available Weekly Contracts for this week.";
    }

    private static void AddCompletedUnclaimed(List<PlayerMissionInstance> source, List<PlayerMissionInstance> destination)
    {
        for (var i = 0; i < source.Count; i++)
        {
            var instance = source[i];

            if (instance.Completed && !instance.Claimed)
            {
                destination.Add(instance);
            }
        }
    }

    private static void AddActive(List<PlayerMissionInstance> source, List<PlayerMissionInstance> destination)
    {
        for (var i = 0; i < source.Count; i++)
        {
            var instance = source[i];

            if (instance.Accepted && !instance.Claimed)
            {
                destination.Add(instance);
            }
        }
    }

    private static DateTime GetWeekStart(DateTime value)
    {
        var date = value.Date;
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }
}

public enum MissionBoardView
{
    DailyMissives,
    WeeklyContracts,
    Completed
}
