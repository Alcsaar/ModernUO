using System;
using System.Collections.Generic;
using System.IO;
using Server.Json;
using Server.Logging;
using Server.Items;
using Server.Network;
using Server.Spells;
using Server.Targeting;

namespace Server.Custom.Systems.TravelCodex;

public sealed class TravelCastState
{
    public Mobile Caster { get; init; }
    public TravelCodex Codex { get; init; }
    public TravelDiscoveryStone Stone { get; init; }
    public Point3D Destination { get; init; }
    public Map DestinationMap { get; init; }
    public int StartingHits { get; init; }
    public bool AppliedFreeze { get; set; }
    public TimerExecutionToken PollToken { get; set; }
    public TimerExecutionToken CompleteToken { get; set; }
}

public static class TravelCodexManager
{
    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(TravelCodexManager));

    private static readonly Dictionary<string, HashSet<string>> _discoveries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Serial, DateTime> _cooldowns = new();
    private static readonly Dictionary<Serial, TravelCastState> _activeCasts = new();
    private static readonly HashSet<TravelDiscoveryStone> _stones = new();
    private static bool _loadedPlayerDataFromPersistence;
    private static bool _legacyPlayerDataImportAttempted;

    public static void Configure()
    {
        TravelCodexPersistence.Configure();
        TravelCodexSettings.Configure();
        TravelCodexCommands.Configure();
        EventSink.Movement += OnMovement;
    }

    public static void Initialize()
    {
        EnsureFlagRegistered();
        LoadPlayerData();
    }

    public static void EnsureFlagRegistered()
    {
        if (!Custom.Systems.CustomFeatureFlags.CustomFeatureFlagManager.IsRegistered(
                Custom.Systems.CustomFeatureFlags.CustomFeatureFlagKeys.TravelCodex))
        {
            Custom.Systems.CustomFeatureFlags.CustomFeatureFlagManager.Register(
                Custom.Systems.CustomFeatureFlags.CustomFeatureFlagKeys.TravelCodex,
                "Travel Codex",
                "Non-magery codex travel system",
                "Custom Systems",
                defaultEnabled: true
            );
        }
    }

    public static bool IsSystemEnabled()
    {
        return Custom.Systems.CustomFeatureFlags.CustomFeatureFlagManager.IsEnabled(
            Custom.Systems.CustomFeatureFlags.CustomFeatureFlagKeys.TravelCodex
        );
    }

    public static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim().ToLowerInvariant();
    }

    public static void RegisterStone(TravelDiscoveryStone stone)
    {
        if (stone == null)
        {
            return;
        }

        _stones.Add(stone);
    }

    public static void UnregisterStone(TravelDiscoveryStone stone)
    {
        if (stone == null)
        {
            return;
        }

        _stones.Remove(stone);
    }

    public static bool HasConflictingDestinationKey(TravelDiscoveryStone source, string key)
    {
        var normalized = NormalizeKey(key);
        if (normalized == null)
        {
            return false;
        }

        foreach (var stone in _stones)
        {
            if (stone == null || stone.Deleted || ReferenceEquals(stone, source))
            {
                continue;
            }

            if (string.Equals(stone.DestinationKey, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static List<TravelDiscoveryStone> GetActiveStones()
    {
        var list = new List<TravelDiscoveryStone>();

        foreach (var stone in _stones)
        {
            if (stone == null || stone.Deleted)
            {
                continue;
            }

            if (!stone.IsActive)
            {
                continue;
            }

            list.Add(stone);
        }

        return list;
    }

    public static List<TravelDiscoveryStone> GetDiscoveredStones(Mobile player, TravelCategory category)
    {
        var list = new List<TravelDiscoveryStone>();

        if (player == null)
        {
            return list;
        }

        var discoveries = GetOrCreateDiscoveries(player);

        foreach (var stone in _stones)
        {
            if (stone == null || stone.Deleted || !stone.IsActive)
            {
                continue;
            }

            if (stone.TravelCategory != category)
            {
                continue;
            }

            if (!discoveries.Contains(stone.DestinationKey))
            {
                continue;
            }

            list.Add(stone);
        }

        list.Sort(static (a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static HashSet<string> GetOrCreateDiscoveries(Mobile player)
    {
        var key = player.Serial.ToString();

        if (!_discoveries.TryGetValue(key, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _discoveries[key] = set;
        }

        return set;
    }

    public static bool HasDiscovered(Mobile player, string destinationKey)
    {
        if (player == null || string.IsNullOrWhiteSpace(destinationKey))
        {
            return false;
        }

        return GetOrCreateDiscoveries(player).Contains(NormalizeKey(destinationKey));
    }

    public static bool Discover(Mobile player, TravelDiscoveryStone stone)
    {
        if (player == null || stone == null || !stone.IsActive || !IsSystemEnabled())
        {
            return false;
        }

        var normalizedKey = NormalizeKey(stone.DestinationKey);
        if (normalizedKey == null)
        {
            return false;
        }

        var discoveries = GetOrCreateDiscoveries(player);
        if (!discoveries.Add(normalizedKey))
        {
            return false;
        }

        SavePlayerData();

        var message = $"You commit the path to {stone.DisplayName} to your travel codex.";
        player.LocalOverheadMessage(MessageType.Regular, TravelCodexSettings.DiscoveryMessageHue, false, message);
        player.SendMessage(TravelCodexSettings.DiscoveryMessageHue, message);
        player.FixedEffect(0x373A, 10, 15, 1153, 0);
        player.PlaySound(0x1F5);

        return true;
    }

    public static bool AddDiscovery(Mobile player, string destinationKey)
    {
        if (player == null || string.IsNullOrWhiteSpace(destinationKey))
        {
            return false;
        }

        var normalizedKey = NormalizeKey(destinationKey);
        if (normalizedKey == null)
        {
            return false;
        }

        var discoveries = GetOrCreateDiscoveries(player);
        if (!discoveries.Add(normalizedKey))
        {
            return false;
        }

        SavePlayerData();
        return true;
    }

    public static bool RemoveDiscovery(Mobile player, string destinationKey)
    {
        if (player == null || string.IsNullOrWhiteSpace(destinationKey))
        {
            return false;
        }

        var normalizedKey = NormalizeKey(destinationKey);
        if (normalizedKey == null)
        {
            return false;
        }

        var discoveries = GetOrCreateDiscoveries(player);
        if (!discoveries.Remove(normalizedKey))
        {
            return false;
        }

        SavePlayerData();
        return true;
    }

    public static int AddAllDiscoveries(Mobile player)
    {
        if (player == null)
        {
            return 0;
        }

        var discoveries = GetOrCreateDiscoveries(player);
        var count = 0;

        foreach (var stone in _stones)
        {
            if (stone == null || stone.Deleted || !stone.IsActive)
            {
                continue;
            }

            if (discoveries.Add(stone.DestinationKey))
            {
                count++;
            }
        }

        if (count > 0)
        {
            SavePlayerData();
        }

        return count;
    }

    public static int RemoveAllDiscoveries(Mobile player)
    {
        if (player == null)
        {
            return 0;
        }

        var discoveries = GetOrCreateDiscoveries(player);
        var count = discoveries.Count;
        discoveries.Clear();

        if (count > 0)
        {
            SavePlayerData();
        }

        return count;
    }

    public static bool TryGetCooldownRemaining(Mobile player, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;

        if (player == null)
        {
            return false;
        }

        if (!_cooldowns.TryGetValue(player.Serial, out var nextAllowed))
        {
            return false;
        }

        var now = Core.Now;
        if (nextAllowed <= now)
        {
            _cooldowns.Remove(player.Serial);
            return false;
        }

        remaining = nextAllowed - now;
        return true;
    }

    public static void StartCooldown(Mobile player)
    {
        if (player == null)
        {
            return;
        }

        _cooldowns[player.Serial] = Core.Now + TravelCodexSettings.Cooldown;
    }

    public static bool TryResolveTravelDestination(TravelDiscoveryStone stone, out Map map, out Point3D point)
    {
        map = null;
        point = Point3D.Zero;

        if (stone == null || !stone.IsActive)
        {
            return false;
        }

        if (stone.TravelMap == null || stone.TravelMap == Map.Internal)
        {
            return false;
        }

        map = stone.TravelMap;
        point = stone.TravelPoint;

        return SpellHelper.FindValidSpawnLocation(map, ref point, false);
    }

    public static bool TryBeginTravel(Mobile from, TravelCodex codex, TravelDiscoveryStone stone)
    {
        if (from == null || codex == null || stone == null)
        {
            return false;
        }

        if (!IsSystemEnabled())
        {
            from.SendMessage(0x22, "The travel codex is currently disabled.");
            return false;
        }

        if (_activeCasts.ContainsKey(from.Serial))
        {
            from.SendMessage(0x22, "You are already channeling codex travel.");
            return false;
        }

        if (!codex.IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001);
            return false;
        }

        if (!stone.IsActive)
        {
            from.SendMessage(0x22, "That destination is not currently available.");
            return false;
        }

        if (!HasDiscovered(from, stone.DestinationKey))
        {
            from.SendMessage(0x22, "You have not yet discovered that destination.");
            return false;
        }

        if (codex.Charges < 1)
        {
            from.SendMessage(0x22, "Your travel codex lacks the charges to do that.");
            return false;
        }

        if (TryGetCooldownRemaining(from, out var remaining))
        {
            from.SendMessage(0x22, $"Your travel codex is still recovering for {remaining:mm\\:ss}.");
            return false;
        }

        if (!SpellHelper.CheckTravel(from, TravelCheckType.RecallFrom, out var travelMessage))
        {
            SendTravelMessage(from, travelMessage);
            return false;
        }

        if (!TryResolveTravelDestination(stone, out var destinationMap, out var destination))
        {
            from.SendMessage(0x22, "That destination is not currently safe for arrival.");
            return false;
        }

        if (from.Spell != null)
        {
            from.SendLocalizedMessage(502642);
            return false;
        }

        var state = new TravelCastState
        {
            Caster = from,
            Codex = codex,
            Stone = stone,
            Destination = destination,
            DestinationMap = destinationMap,
            StartingHits = from.Hits
        };

        _activeCasts[from.Serial] = state;

        if (!from.Frozen)
        {
            from.Frozen = true;
            state.AppliedFreeze = true;
        }

        from.Animate(17, 7, 1, true, false, 0);
        from.FixedEffect(0x376A, 9, 32);
        from.PlaySound(0x20E);
        from.SendMessage(0x35, $"You begin channeling the path to {stone.DisplayName}.");

        Timer.StartTimer(
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(250.0),
            () => MonitorCast(from.Serial),
            out var pollToken
        );
        state.PollToken = pollToken;

        Timer.StartTimer(TravelCodexSettings.CastDelay, () => CompleteCast(from.Serial), out var completeToken);
        state.CompleteToken = completeToken;

        return true;
    }

    private static void MonitorCast(Serial playerSerial)
    {
        if (!_activeCasts.TryGetValue(playerSerial, out var state))
        {
            return;
        }

        var caster = state.Caster;
        if (caster == null || caster.Deleted || state.Codex == null || state.Codex.Deleted)
        {
            CancelCast(playerSerial, "Your codex channeling collapses.");
            return;
        }

        if (caster.Hits < state.StartingHits)
        {
            CancelCast(playerSerial, "Your codex channeling is broken by the blow.");
            return;
        }

        if (!caster.Alive)
        {
            CancelCast(playerSerial, "You cannot complete codex travel in your current state.");
            return;
        }
    }

    private static void CompleteCast(Serial playerSerial)
    {
        if (!_activeCasts.TryGetValue(playerSerial, out var state))
        {
            return;
        }

        var caster = state.Caster;
        if (caster == null || caster.Deleted)
        {
            CancelCast(playerSerial, null);
            return;
        }

        if (!IsSystemEnabled())
        {
            CancelCast(playerSerial, "The travel codex is currently disabled.");
            return;
        }

        if (!state.Codex.IsChildOf(caster.Backpack))
        {
            CancelCast(playerSerial, "Your travel codex must remain in your backpack.");
            return;
        }

        if (!state.Stone.IsActive)
        {
            CancelCast(playerSerial, "That destination is no longer available.");
            return;
        }

        if (!SpellHelper.CheckTravel(caster, TravelCheckType.RecallFrom, out var travelMessage))
        {
            FinishCastCleanup(playerSerial);
            SendTravelMessage(caster, travelMessage);
            return;
        }

        if (!TryResolveTravelDestination(state.Stone, out var destinationMap, out var destination))
        {
            CancelCast(playerSerial, "That destination is not currently safe for arrival.");
            return;
        }

        if (TryGetCooldownRemaining(caster, out _))
        {
            CancelCast(playerSerial, "Your travel codex has not yet recovered.");
            return;
        }

        if (state.Codex.Charges < 1)
        {
            CancelCast(playerSerial, "Your travel codex lacks the charges to do that.");
            return;
        }

        state.Codex.Charges -= 1;
        state.Codex.InvalidateProperties();
        state.Codex.MarkDirty();
        StartCooldown(caster);

        FinishCastCleanup(playerSerial);

        caster.MoveToWorld(destination, destinationMap);
        caster.FixedEffect(0x3728, 10, 13);
        caster.PlaySound(0x1FE);
    }

    public static void CancelCast(Serial playerSerial, string message)
    {
        if (!_activeCasts.TryGetValue(playerSerial, out var state))
        {
            return;
        }

        var caster = state.Caster;
        FinishCastCleanup(playerSerial);

        if (caster != null && !string.IsNullOrWhiteSpace(message))
        {
            caster.SendMessage(0x22, message);
        }
    }

    private static void FinishCastCleanup(Serial playerSerial)
    {
        if (!_activeCasts.TryGetValue(playerSerial, out var state))
        {
            return;
        }

        state.PollToken.Cancel();
        state.CompleteToken.Cancel();

        if (state.Caster != null && !state.Caster.Deleted && state.AppliedFreeze)
        {
            state.Caster.Frozen = false;
        }

        _activeCasts.Remove(playerSerial);
    }

    public static bool TryConsumeRecallScrolls(Mobile from, TravelCodex codex, RecallScroll scroll)
    {
        if (from?.Backpack == null || codex == null || scroll == null)
        {
            return false;
        }

        if (!codex.IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001);
            return false;
        }

        if (!scroll.IsChildOf(from.Backpack))
        {
            from.SendMessage(0x22, "The recall scroll must be in your backpack.");
            return false;
        }

        var freeCharges = TravelCodexSettings.MaxCharges - codex.Charges;
        if (freeCharges <= 0)
        {
            from.SendMessage(0x22, "Your travel codex is already fully charged.");
            return false;
        }

        var amount = scroll.Amount > 0 ? scroll.Amount : 1;
        var consume = Math.Min(amount, freeCharges);

        if (consume <= 0)
        {
            return false;
        }

        scroll.Consume(consume);

        codex.Charges += consume;
        codex.InvalidateProperties();
        codex.MarkDirty();

        from.SendMessage(0x35, $"You load {consume} recall scroll{(consume == 1 ? string.Empty : "s")} into the codex.");
        return true;
    }

    public static string[] ValidateStone(TravelDiscoveryStone stone)
    {
        var errors = new List<string>();

        if (stone == null)
        {
            errors.Add("No stone selected.");
            return errors.ToArray();
        }

        if (string.IsNullOrWhiteSpace(stone.DestinationKey))
        {
            errors.Add("DestinationKey is required.");
        }
        else if (HasConflictingDestinationKey(stone, stone.DestinationKey))
        {
            errors.Add("DestinationKey conflicts with another travel stone.");
        }

        if (string.IsNullOrWhiteSpace(stone.DisplayName))
        {
            errors.Add("DisplayName is required.");
        }

        if (stone.DiscoverRange <= 0)
        {
            errors.Add("DiscoverRange must be greater than zero.");
        }

        if (stone.TravelMap == null || stone.TravelMap == Map.Internal)
        {
            errors.Add("TravelMap must be a valid world map.");
        }

        if (stone.TravelPoint == Point3D.Zero && stone.TravelMap != Map.Internal && stone.TravelMap != null)
        {
            if (stone.X != 0 || stone.Y != 0 || stone.Z != 0)
            {
                // Allow world origin only if explicitly intended; no error here if item location is also zero.
            }
        }

        if (stone.TravelMap != null && stone.TravelMap != Map.Internal)
        {
            var point = stone.TravelPoint;
            if (!SpellHelper.FindValidSpawnLocation(stone.TravelMap, ref point, false))
            {
                errors.Add("TravelPoint does not resolve to a valid arrival location.");
            }
        }

        return errors.ToArray();
    }

    public static void SendTravelMessage(Mobile to, TextDefinition message)
    {
        if (to == null || message == null)
        {
            return;
        }

        if (message.Number > 0)
        {
            to.SendLocalizedMessage(message.Number);
            return;
        }

        if (!string.IsNullOrWhiteSpace(message.String))
        {
            to.SendMessage(0x22, message.String);
        }
    }

    private static void OnMovement(MovementEventArgs e)
    {
        var from = e.Mobile;
        if (from == null || from.Deleted || from.NetState == null || !from.Player || !IsSystemEnabled())
        {
            return;
        }

        if (from.Map == null || from.Map == Map.Internal)
        {
            return;
        }

        foreach (var stone in from.Map.GetItemsInRange<TravelDiscoveryStone>(from.Location, 18))
        {
            if (stone == null || stone.Deleted || !stone.IsActive)
            {
                continue;
            }

            if (!from.InRange(stone.GetWorldLocation(), stone.DiscoverRange))
            {
                continue;
            }

            _ = Discover(from, stone);
        }
    }

    public static void LoadPlayerData()
    {
        try
        {
            if (_loadedPlayerDataFromPersistence)
            {
                return;
            }

            if (_legacyPlayerDataImportAttempted)
            {
                return;
            }

            _legacyPlayerDataImportAttempted = true;
            _discoveries.Clear();

            if (!File.Exists(TravelCodexSettings.PlayerDataPath))
            {
                return;
            }

            var file = JsonConfig.Deserialize<TravelCodexPlayerDataFile>(TravelCodexSettings.PlayerDataPath);
            if (file?.Players == null)
            {
                return;
            }

            for (var i = 0; i < file.Players.Count; i++)
            {
                var record = file.Players[i];
                if (record == null || string.IsNullOrWhiteSpace(record.PlayerSerial))
                {
                    continue;
                }

                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (record.DiscoveredDestinationKeys != null)
                {
                    for (var j = 0; j < record.DiscoveredDestinationKeys.Count; j++)
                    {
                        var key = NormalizeKey(record.DiscoveredDestinationKeys[j]);
                        if (key != null)
                        {
                            set.Add(key);
                        }
                    }
                }

                _discoveries[record.PlayerSerial] = set;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load Travel Codex player discovery data");
        }
    }

    public static void SavePlayerData()
    {
        // Player discovery state is persisted by TravelCodexPersistence during world saves.
    }

    internal static void SerializePlayerData(IGenericWriter writer)
    {
        writer.WriteEncodedInt(_discoveries.Count);

        foreach (var pair in _discoveries)
        {
            writer.Write(pair.Key);
            writer.WriteEncodedInt(pair.Value?.Count ?? 0);

            if (pair.Value == null)
            {
                continue;
            }

            foreach (var destinationKey in pair.Value)
            {
                writer.Write(destinationKey);
            }
        }
    }

    internal static void DeserializePlayerData(IGenericReader reader)
    {
        _discoveries.Clear();

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var playerSerial = reader.ReadString();
            var destinationCount = reader.ReadEncodedInt();

            if (string.IsNullOrWhiteSpace(playerSerial))
            {
                for (var j = 0; j < destinationCount; j++)
                {
                    reader.ReadString();
                }

                continue;
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var j = 0; j < destinationCount; j++)
            {
                var key = NormalizeKey(reader.ReadString());

                if (key != null)
                {
                    set.Add(key);
                }
            }

            _discoveries[playerSerial] = set;
        }

        _loadedPlayerDataFromPersistence = true;
    }
}
