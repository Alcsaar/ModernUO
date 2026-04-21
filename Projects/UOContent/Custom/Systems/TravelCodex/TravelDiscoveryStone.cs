using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Custom.Systems.TravelCodex;

[SerializationGenerator(0)]
public partial class TravelDiscoveryStone : Item
{
    private const int DefaultItemId = 0x1593;

    private bool _enabled;
    private string _destinationKey = string.Empty;
    private string _displayName = string.Empty;
    private TravelCategory _travelCategory = TravelCategory.Custom;
    private int _discoverRange;
    private Map _travelMap;
    private Point3D _travelPoint;

    [Constructible]
    public TravelDiscoveryStone() : base(DefaultItemId)
    {
        Visible = false;
        Movable = false;
        Hue = 0;
        _enabled = false;
        _discoverRange = TravelCodexSettings.DefaultDiscoverRange;
        _travelMap = Map.Internal;
        _travelPoint = Point3D.Zero;

        TravelCodexManager.RegisterStone(this);
    }

    public override string DefaultName => "travel discovery stone";

    public override bool BlocksFit => false;

    [SerializableProperty(0, useField: nameof(_destinationKey))]
    [CommandProperty(AccessLevel.GameMaster)]
    public string DestinationKey
    {
        get => _destinationKey;
        set
        {
            var normalized = TravelCodexManager.NormalizeKey(value) ?? string.Empty;
            if (_destinationKey == normalized)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(normalized) && TravelCodexManager.HasConflictingDestinationKey(this, normalized))
            {
                return;
            }

            _destinationKey = normalized;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(1, useField: nameof(_displayName))]
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

    [SerializableProperty(2, useField: nameof(_travelCategory))]
    [CommandProperty(AccessLevel.GameMaster)]
    public TravelCategory TravelCategory
    {
        get => _travelCategory;
        set
        {
            if (_travelCategory == value)
            {
                return;
            }

            _travelCategory = value;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(3, useField: nameof(_discoverRange))]
    [CommandProperty(AccessLevel.GameMaster)]
    public int DiscoverRange
    {
        get => _discoverRange;
        set
        {
            var next = value < 1 ? 1 : value;
            if (_discoverRange == next)
            {
                return;
            }

            _discoverRange = next;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(4, useField: nameof(_travelMap))]
    [CommandProperty(AccessLevel.GameMaster)]
    public Map TravelMap
    {
        get => _travelMap;
        set
        {
            if (_travelMap == value)
            {
                return;
            }

            _travelMap = value;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(5, useField: nameof(_travelPoint))]
    [CommandProperty(AccessLevel.GameMaster)]
    public Point3D TravelPoint
    {
        get => _travelPoint;
        set
        {
            if (_travelPoint == value)
            {
                return;
            }

            _travelPoint = value;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(6, useField: nameof(_enabled))]
    [CommandProperty(AccessLevel.GameMaster)]
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (!value)
            {
                if (_enabled)
                {
                    _enabled = false;
                    InvalidateProperties();
                    this.MarkDirty();
                }

                return;
            }

            var errors = TravelCodexManager.ValidateStone(this);
            if (errors.Length > 0)
            {
                _enabled = false;
                InvalidateProperties();
                this.MarkDirty();
                return;
            }

            if (!_enabled)
            {
                _enabled = true;
                InvalidateProperties();
                this.MarkDirty();
            }
        }
    }

    public bool IsActive => !Deleted && _enabled && TravelCodexManager.ValidateStone(this).Length == 0;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add($"{"Key: "}{(string.IsNullOrWhiteSpace(DestinationKey) ? "(unset)" : DestinationKey)}");
        list.Add($"{"Name: "}{(string.IsNullOrWhiteSpace(DisplayName) ? "(unset)" : DisplayName)}");
        list.Add($"{"Category: "}{TravelCategoryInfo.GetDisplayName(TravelCategory)}");
        list.Add($"{"Enabled: "}{(_enabled ? "Yes" : "No")}");
        list.Add($"{"Ready: "}{(IsActive ? "Yes" : "No")}");
        list.Add($"{"Discover Range: "}{DiscoverRange}");
        list.Add($"{"Travel Map: "}{TravelMap}");
        list.Add($"{"Travel Point: "}{TravelPoint}");
    }

    public override void OnAfterDelete()
    {
        TravelCodexManager.UnregisterStone(this);
        base.OnAfterDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        TravelCodexManager.RegisterStone(this);
    }
}
