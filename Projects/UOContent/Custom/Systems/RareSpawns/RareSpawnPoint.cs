using System;
using System.Collections.Generic;
using ModernUO.Serialization;

namespace Server.Custom.Systems.RareSpawns;

[SerializationGenerator(0)]
public partial class RareSpawnPoint : Item, ISpawner
{
    private const int DefaultItemId = 7955;
    private static readonly TimeZoneInfo DisplayTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    private bool _enabled;
    private bool _serverBirthConsumed;
    private string _spawnTypeName = string.Empty;
    private string _possibleSpawnTypeNames = string.Empty;
    private string _displayName = string.Empty;
    private RareRespawnProfile _respawnProfile = RareRespawnProfile.Daily;
    private int _minRespawnMinutes = 20 * 60;
    private int _maxRespawnMinutes = 28 * 60;
    private DateTime _nextSpawnTime;
    private uint _spawnedItemSerial;

    [Constructible]
    public RareSpawnPoint() : base(DefaultItemId)
    {
        Visible = false;
        Movable = false;
        Name = "rare spawn point";

        RareSpawnManager.Register(this);
    }

    public override string DefaultName => "rare spawn point";

    public override bool BlocksFit => false;

    public Guid Guid { get; } = Guid.NewGuid();

    public bool UnlinkOnTaming => true;

    public int WalkingRange => 0;

    public Rectangle3D SpawnBounds => new(Location, Location);

    public Region Region => Map != null && Map != Map.Internal ? Region.Find(Location, Map) : null;

    public bool ReturnOnDeactivate => false;

    public bool Running => _enabled;

    [SerializableProperty(0, useField: nameof(_enabled))]
    [CommandProperty(AccessLevel.GameMaster)]
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;

            if (_enabled && !HasActiveSpawn && _nextSpawnTime == DateTime.MinValue)
            {
                ScheduleNextSpawn(true);
            }

            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(1, useField: nameof(_spawnTypeName))]
    [CommandProperty(AccessLevel.GameMaster)]
    public string SpawnTypeName
    {
        get => _spawnTypeName;
        set
        {
            var next = value?.Trim() ?? string.Empty;
            if (_spawnTypeName == next)
            {
                return;
            }

            _spawnTypeName = next;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(9, useField: nameof(_possibleSpawnTypeNames))]
    [CommandProperty(AccessLevel.GameMaster)]
    public string PossibleSpawnTypeNames
    {
        get => _possibleSpawnTypeNames;
        set
        {
            var next = NormalizeSpawnTypeList(value);
            if (_possibleSpawnTypeNames == next)
            {
                return;
            }

            _possibleSpawnTypeNames = next;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(2, useField: nameof(_displayName))]
    [CommandProperty(AccessLevel.GameMaster)]
    public string DisplayName
    {
        get => _displayName;
        set
        {
            var next = value?.Trim() ?? string.Empty;
            if (_displayName == next)
            {
                return;
            }

            _displayName = next;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(3, useField: nameof(_respawnProfile))]
    [CommandProperty(AccessLevel.GameMaster)]
    public RareRespawnProfile RespawnProfile
    {
        get => _respawnProfile;
        set
        {
            if (_respawnProfile == value)
            {
                return;
            }

            _respawnProfile = value;
            ApplyProfileDefaults(value);
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(4, useField: nameof(_minRespawnMinutes))]
    [CommandProperty(AccessLevel.GameMaster)]
    public int MinRespawnMinutes
    {
        get => _minRespawnMinutes;
        set
        {
            var next = Math.Max(0, value);
            if (_minRespawnMinutes == next)
            {
                return;
            }

            _minRespawnMinutes = next;
            if (_maxRespawnMinutes < _minRespawnMinutes)
            {
                _maxRespawnMinutes = _minRespawnMinutes;
            }

            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(5, useField: nameof(_maxRespawnMinutes))]
    [CommandProperty(AccessLevel.GameMaster)]
    public int MaxRespawnMinutes
    {
        get => _maxRespawnMinutes;
        set
        {
            var next = Math.Max(0, value);
            if (_maxRespawnMinutes == next)
            {
                return;
            }

            _maxRespawnMinutes = Math.Max(next, _minRespawnMinutes);
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(6, useField: nameof(_nextSpawnTime))]
    [CommandProperty(AccessLevel.GameMaster)]
    public DateTime NextSpawnTime
    {
        get => _nextSpawnTime;
        set
        {
            if (_nextSpawnTime == value)
            {
                return;
            }

            _nextSpawnTime = value;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(7, useField: nameof(_spawnedItemSerial))]
    [CommandProperty(AccessLevel.GameMaster)]
    public uint SpawnedItemSerial
    {
        get => _spawnedItemSerial;
        set
        {
            if (_spawnedItemSerial == value)
            {
                return;
            }

            _spawnedItemSerial = value;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(8, useField: nameof(_serverBirthConsumed))]
    [CommandProperty(AccessLevel.GameMaster)]
    public bool ServerBirthConsumed
    {
        get => _serverBirthConsumed;
        set
        {
            if (_serverBirthConsumed == value)
            {
                return;
            }

            _serverBirthConsumed = value;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    public Item SpawnedItem => _spawnedItemSerial > 0 ? World.FindItem((Serial)_spawnedItemSerial) : null;

    public bool HasActiveSpawn
    {
        get
        {
            var spawned = SpawnedItem;
            return spawned != null &&
                   !spawned.Deleted &&
                   spawned.Spawner == this &&
                   spawned.Parent == null &&
                   spawned.Map == Map &&
                   spawned.Location == Location;
        }
    }

    public bool IsConfigured => RareSpawnManager.TryResolveAnyItemType(GetSpawnTypeList(), out _);

    public void Remove(ISpawnable spawn)
    {
        if (spawn is not Item item || item.Serial.Value != _spawnedItemSerial)
        {
            return;
        }

        /* BEGIN RARE SPAWN CUSTOMIZATION: immediately schedule the next rare when the current one is taken or deleted. */
        _spawnedItemSerial = 0;
        ScheduleNextSpawn(false);
        /* END RARE SPAWN CUSTOMIZATION */
    }

    public Point3D GetSpawnPosition(ISpawnable spawned, Map map) => Location;

    public void Respawn()
    {
        if (!HasActiveSpawn)
        {
            _spawnedItemSerial = 0;
        }

        _nextSpawnTime = Core.Now;
        CheckRespawn();
    }

    public bool ForceRespawn(out string reason)
    {
        /* BEGIN RARE SPAWN CUSTOMIZATION: staff force-respawn replaces the current rare and refreshes spawn state. */
        if (SpawnedItem is { Deleted: false } spawned)
        {
            if (spawned.Spawner == this)
            {
                spawned.Spawner = null;
            }

            spawned.Delete();
        }

        _spawnedItemSerial = 0;

        if (_respawnProfile == RareRespawnProfile.ServerBirth)
        {
            _serverBirthConsumed = false;
        }

        _nextSpawnTime = Core.Now;
        return TrySpawn(out reason);
        /* END RARE SPAWN CUSTOMIZATION */
    }

    public bool IsInSpawnBounds(Point3D location) => location == Location;

    [CommandProperty(AccessLevel.GameMaster)]
    public string NextSpawnDisplay => GetNextSpawnDisplay();

    [CommandProperty(AccessLevel.GameMaster)]
    public string RespawnWindow => $"{_minRespawnMinutes}-{_maxRespawnMinutes} minutes";

    public void ApplyProfileDefaults(RareRespawnProfile profile)
    {
        /* BEGIN RARE SPAWN CUSTOMIZATION: set practical default windows for common rare schedules. */
        switch (profile)
        {
            case RareRespawnProfile.ServerBirth:
                _minRespawnMinutes = 0;
                _maxRespawnMinutes = 0;
                break;
            case RareRespawnProfile.Daily:
                _minRespawnMinutes = 20 * 60;
                _maxRespawnMinutes = 24 * 60;
                break;
            case RareRespawnProfile.Weekly:
                _minRespawnMinutes = 6 * 24 * 60;
                _maxRespawnMinutes = 8 * 24 * 60;
                break;
            case RareRespawnProfile.Monthly:
                _minRespawnMinutes = 25 * 24 * 60;
                _maxRespawnMinutes = 35 * 24 * 60;
                break;
        }
        /* END RARE SPAWN CUSTOMIZATION */
    }

    public void ScheduleNextSpawn(bool allowImmediate)
    {
        /* BEGIN RARE SPAWN CUSTOMIZATION: compute the next spawn time without creating extra timers per rare point. */
        if (_respawnProfile == RareRespawnProfile.ServerBirth)
        {
            _nextSpawnTime = _serverBirthConsumed ? DateTime.MaxValue : Core.Now;
        }
        else if (allowImmediate)
        {
            _nextSpawnTime = Core.Now;
        }
        else
        {
            var min = Math.Max(0, _minRespawnMinutes);
            var max = Math.Max(min, _maxRespawnMinutes);
            var delay = min == max ? min : Utility.RandomMinMax(min, max);
            _nextSpawnTime = Core.Now + TimeSpan.FromMinutes(delay);
        }

        InvalidateProperties();
        this.MarkDirty();
        /* END RARE SPAWN CUSTOMIZATION */
    }

    public bool TrySpawn(out string reason)
    {
        reason = null;

        if (Deleted || !_enabled)
        {
            reason = "The rare spawn point is disabled.";
            return false;
        }

        if (Map == null || Map == Map.Internal)
        {
            reason = "The rare spawn point is not in the world.";
            return false;
        }

        if (HasActiveSpawn)
        {
            reason = "The rare is already spawned.";
            return false;
        }

        if (_respawnProfile == RareRespawnProfile.ServerBirth && _serverBirthConsumed)
        {
            reason = "This server birth rare has already been consumed.";
            return false;
        }

        if (!TryResolveSpawnType(out var type, out var spawnReason))
        {
            reason = spawnReason;
            return false;
        }

        var item = Loot.Construct(type);
        if (item == null)
        {
            reason = "The rare item could not be constructed.";
            return false;
        }

        item.MoveToWorld(Location, Map);
        item.Spawner = this;
        _spawnedItemSerial = item.Serial.Value;

        if (_respawnProfile == RareRespawnProfile.ServerBirth)
        {
            _serverBirthConsumed = true;
            _nextSpawnTime = DateTime.MaxValue;
        }

        InvalidateProperties();
        this.MarkDirty();
        return true;
    }

    public string[] GetSpawnTypeList()
    {
        var source = !string.IsNullOrWhiteSpace(_possibleSpawnTypeNames)
            ? _possibleSpawnTypeNames
            : _spawnTypeName;

        return ParseSpawnTypeNames(source);
    }

    public string GetSpawnTypeDisplay()
    {
        var names = GetSpawnTypeList();
        if (names.Length == 0)
        {
            return "(unset)";
        }

        if (names.Length == 1)
        {
            return names[0];
        }

        return $"{names[0]} +{names.Length - 1}";
    }

    public bool TryResolveSpawnType(out Type type, out string reason)
    {
        type = null;
        reason = null;

        var names = GetSpawnTypeList();
        if (names.Length == 0)
        {
            reason = "No spawn item types are configured.";
            return false;
        }

        var start = names.Length == 1 ? 0 : Utility.Random(names.Length);

        for (var offset = 0; offset < names.Length; offset++)
        {
            var index = (start + offset) % names.Length;
            if (RareSpawnManager.TryResolveItemType(names[index], out type))
            {
                return true;
            }
        }

        reason = "None of the configured spawn item types resolve to item classes.";
        return false;
    }

    public void CheckRespawn()
    {
        if (Deleted || !_enabled)
        {
            return;
        }

        /* BEGIN RARE SPAWN CUSTOMIZATION: treat a missing or moved rare as claimed, then schedule the replacement. */
        if (_spawnedItemSerial > 0 && !HasActiveSpawn)
        {
            if (SpawnedItem is { Spawner: not null } item && item.Spawner == this)
            {
                item.Spawner = null;
            }

            _spawnedItemSerial = 0;
            ScheduleNextSpawn(false);
            return;
        }
        /* END RARE SPAWN CUSTOMIZATION */

        if (_spawnedItemSerial == 0 && _nextSpawnTime == DateTime.MinValue)
        {
            ScheduleNextSpawn(true);
            return;
        }

        if (_spawnedItemSerial == 0 && _nextSpawnTime <= Core.Now)
        {
            if (!TrySpawn(out _))
            {
                ScheduleNextSpawn(false);
            }
        }
    }

    public void ResetSchedule(bool clearServerBirth)
    {
        if (clearServerBirth)
        {
            _serverBirthConsumed = false;
        }

        _spawnedItemSerial = 0;
        _nextSpawnTime = DateTime.MinValue;
        ApplyProfileDefaults(_respawnProfile);
        ScheduleNextSpawn(true);
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add($"{"Enabled: "}{(_enabled ? "Yes" : "No")}");
        list.Add($"{"Rare: "}{(string.IsNullOrWhiteSpace(_displayName) ? _spawnTypeName : _displayName)}");
        list.Add($"{"Type: "}{GetSpawnTypeDisplay()}");
        list.Add($"{"Possible Types: "}{GetSpawnTypeList().Length}");
        list.Add($"{"Profile: "}{_respawnProfile}");
        list.Add($"{"Window Minutes: "}{_minRespawnMinutes}-{_maxRespawnMinutes}");
        list.Add($"{"Spawned: "}{(HasActiveSpawn ? "Yes" : "No")}");
        list.Add($"{"Next Spawn: "}{GetNextSpawnDisplay()}");
    }

    public override void OnAfterDelete()
    {
        RareSpawnManager.Unregister(this);
        base.OnAfterDelete();
    }

    public string GetNextSpawnDisplay()
    {
        return FormatSpawnTime(_nextSpawnTime, includeDateForToday: true);
    }

    public static string FormatSpawnTime(DateTime value, bool includeDateForToday)
    {
        if (value == DateTime.MinValue)
        {
            return "unscheduled";
        }

        if (value == DateTime.MaxValue)
        {
            return "never";
        }

        var local = ToDisplayLocalTime(value);
        var today = ToDisplayLocalTime(Core.Now).Date;

        if (local.Date == today)
        {
            return includeDateForToday ? $"Today {local:h:mm tt}" : local.ToString("h:mm tt");
        }

        if (local.Date == today.AddDays(1.0))
        {
            return $"Tomorrow {local:h:mm tt}";
        }

        if (local.Date == today.AddDays(-1.0))
        {
            return $"Yesterday {local:h:mm tt}";
        }

        return local.ToString("MMM d h:mm tt");
    }

    private static DateTime ToDisplayLocalTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
        return TimeZoneInfo.ConvertTimeFromUtc(utc, DisplayTimeZone);
    }

    public static string NormalizeSpawnTypeList(string value)
    {
        var names = ParseSpawnTypeNames(value);
        return names.Length == 0 ? string.Empty : string.Join(", ", names);
    }

    public static string[] ParseSpawnTypeNames(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        var parts = value.Split([',', ';', '\n', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        for (var i = 0; i < parts.Length; i++)
        {
            var name = parts[i].Trim();
            if (name.Length > 0 && !ContainsName(names, name))
            {
                names.Add(name);
            }
        }

        return names.ToArray();
    }

    private static bool ContainsName(List<string> names, string name)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        RareSpawnManager.Register(this);
    }
}
