using System;
using ModernUO.Serialization;
using Server.Custom.Engines.ActivityTracking;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class TreasureChestLevel1 : LockableContainer
{
    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: guard against counting multiple opens for the same legacy dungeon treasure chest */
    private bool _activityOpened;
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    [Constructible]
    public TreasureChestLevel1() : base(0xE41)
    {
        SetChestAppearance();
        Movable = false;

        TrapType = TrapType.DartTrap;
        TrapPower = Utility.Random(1, 25);
        Locked = true;

        RequiredSkill = 57;
        LockLevel = RequiredSkill - Utility.Random(1, 10);
        MaxLockLevel = RequiredSkill + Utility.Random(1, 10);

        // According to OSI, loot in level 1 chest is:
        //  Gold 25 - 50
        //  Bolts 10
        //  Gems
        //  Normal weapon
        //  Normal armour
        //  Normal clothing
        //  Normal jewelry

        // Gold
        var gold = new Gold(Utility.Random(30, 100));
        DropItem(gold);
        ActivityTrackingService.RegisterDungeonTreasureChestGold(this, gold);

        // Drop bolts
        // DropItem( new Bolt( 10 ) );

        // Gems
        if (Utility.RandomBool())
        {
            var gems = Loot.RandomGem();
            gems.Amount = Utility.Random(1, 3);
            DropItem(gems);
        }

        // Weapon
        if (Utility.RandomBool())
        {
            DropItem(Loot.RandomWeapon());
        }

        // Armour
        if (Utility.RandomBool())
        {
            DropItem(Loot.RandomArmorOrShield());
        }

        // Clothing
        if (Utility.RandomBool())
        {
            DropItem(Loot.RandomClothing());
        }

        // Jewelry
        if (Utility.RandomBool())
        {
            DropItem(Loot.RandomJewelry());
        }
    }

    public override bool Decays => true;

    public override bool IsDecoContainer => false;

    public override TimeSpan DecayTime => TimeSpan.FromMinutes(Utility.Random(15, 60));

    public override int DefaultGumpID => 0x44;

    public override int DefaultDropSound => 0x42;

    public override Rectangle2D Bounds => new(18, 105, 144, 73);

    private void SetChestAppearance()
    {
        ItemID = Utility.Random(6) switch
        {
            0 => 0xe3c, // Large Crate
            1 => 0xe3d, // Large Crate
            2 => 0xe3e, // Medium Crate
            3 => 0xe3f, // Medium Crate
            4 => 0x9a9, // Small Crate
            _ => 0xe7e  // Small Crate
        };
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: count and credit legacy dungeon treasure chest interactions on open and gold lift */
    public override void Open(Mobile from)
    {
        base.Open(from);

        if (!Locked && !_activityOpened)
        {
            _activityOpened = true;
            ActivityTrackingService.RecordDungeonTreasureChestOpened(from, this, nameof(TreasureChestLevel1));
        }
    }

    public override void OnItemLifted(Mobile from, Item item)
    {
        base.OnItemLifted(from, item);
        ActivityTrackingService.RecordDungeonTreasureChestGoldLooted(from, this, item);
    }

    public override void OnAfterDelete()
    {
        ActivityTrackingService.ClearDungeonTreasureChestGold(this);
        base.OnAfterDelete();
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */
}
