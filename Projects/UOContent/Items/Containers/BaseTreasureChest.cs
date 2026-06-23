using System;
using ModernUO.Serialization;
using Server.Custom.Engines.ActivityTracking;

namespace Server.Items;

[SerializationGenerator(1, false)]
public partial class BaseTreasureChest : LockableContainer
{
    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: guard against counting multiple opens for the same dungeon treasure chest */
    private bool _activityOpened;
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    public enum TreasureLevel
    {
        Level1,
        Level2,
        Level3,
        Level4,
        Level5,
        Level6
    }

    private TimerExecutionToken _resetTimer;

    public BaseTreasureChest(int itemID, TreasureLevel level = TreasureLevel.Level2) : base(itemID)
    {
        _level = level;
        _minSpawnTime = TimeSpan.FromMinutes(10);
        _maxSpawnTime = TimeSpan.FromMinutes(60);

        Locked = true;
        Movable = false;

        SetLockLevel();
        GenerateTreasure();
    }

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private TreasureLevel _level;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private TimeSpan _minSpawnTime;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private TimeSpan _maxSpawnTime;

    [CommandProperty(AccessLevel.GameMaster)]
    public override bool Locked
    {
        get => base.Locked;
        set
        {
            if (base.Locked != value)
            {
                base.Locked = value;

                if (!value)
                {
                    StartResetTimer();
                }

                InvalidateProperties();
            }
        }
    }

    public override bool IsDecoContainer => false;

    public override string DefaultName => Locked ? "a locked treasure chest" : "a treasure chest";

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (!Locked)
        {
            StartResetTimer();
        }
    }

    private void Deserialize(IGenericReader reader, int version)
    {
        _level = (TreasureLevel)reader.ReadByte();
        _minSpawnTime = TimeSpan.FromMinutes(reader.ReadShort());
        _maxSpawnTime = TimeSpan.FromMinutes(reader.ReadShort());
    }

    protected virtual void SetLockLevel()
    {
        RequiredSkill = _level switch
        {
            TreasureLevel.Level1 => LockLevel = 5,
            TreasureLevel.Level2 => LockLevel = 20,
            TreasureLevel.Level3 => LockLevel = 50,
            TreasureLevel.Level4 => LockLevel = 70,
            TreasureLevel.Level5 => LockLevel = 90,
            TreasureLevel.Level6 => LockLevel = 100,
            _                    => LockLevel = 120
        };
    }

    private void StartResetTimer()
    {
        _resetTimer.Cancel();

        var randomDuration = Utility.RandomMinMax(_minSpawnTime.Ticks, _maxSpawnTime.Ticks);
        Timer.StartTimer(TimeSpan.FromTicks(randomDuration), Reset, out _resetTimer);
    }

    protected virtual void GenerateTreasure()
    {
        var gold = _level switch
        {
            TreasureLevel.Level1 => Utility.RandomMinMax(100, 300),
            TreasureLevel.Level2 => Utility.RandomMinMax(300, 600),
            TreasureLevel.Level3 => Utility.RandomMinMax(600, 900),
            TreasureLevel.Level4 => Utility.RandomMinMax(900, 1200),
            TreasureLevel.Level5 => Utility.RandomMinMax(900, 3750), // Default gold: 1200-5000
            _                    => Utility.RandomMinMax(3750, 6750), // Default gold: 5000-9000
        };

        var goldItem = new Gold(gold);
        DropItem(goldItem);
        ActivityTrackingService.RegisterDungeonTreasureChestGold(this, goldItem);
    }

    public override bool CheckItemUse(Mobile from, Item item)
    {
        return base.CheckItemUse(from, item);
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: count the first successful open of an unlocked dungeon treasure chest on the actual container open path */
    public override void Open(Mobile from)
    {
        base.Open(from);

        if (!Locked && !_activityOpened)
        {
            _activityOpened = true;
            ActivityTrackingService.RecordDungeonTreasureChestOpened(from, this);
        }
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    public void ClearContents()
    {
        ActivityTrackingService.ClearDungeonTreasureChestGold(this);

        for (var i = Items.Count - 1; i >= 0; --i)
        {
            if (i < Items.Count)
            {
                Items[i].Delete();
            }
        }
    }

    public void Reset()
    {
        _resetTimer.Cancel();

        /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: allow reopened chest spawns to be counted again after reset */
        _activityOpened = false;
        /* END ACTIVITY TRACKING CUSTOMIZATION */

        Locked = true;
        ClearContents();
        GenerateTreasure();
    }

    public override void OnItemLifted(Mobile from, Item item)
    {
        base.OnItemLifted(from, item);
        ActivityTrackingService.RecordDungeonTreasureChestGoldLooted(from, this, item);
    }
}
