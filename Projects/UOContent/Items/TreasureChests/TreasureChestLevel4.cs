using System;
using ModernUO.Serialization;
using Server.Custom.Engines.ActivityTracking;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class TreasureChestLevel4 : LockableContainer
{
    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: guard against counting multiple opens for the same legacy dungeon treasure chest */
    private bool _activityOpened;
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    [Constructible]
    public TreasureChestLevel4() : base(0xE41)
    {
        SetChestAppearance();
        Movable = false;

        TrapType = TrapType.ExplosionTrap;
        TrapPower = 4 * Utility.Random(10, 25);
        Locked = true;

        RequiredSkill = 92;
        LockLevel = RequiredSkill - Utility.Random(1, 10);
        MaxLockLevel = RequiredSkill + Utility.Random(1, 10);

        // According to OSI, loot in level 4 chest is:
        //  Gold 500 - 900
        //  Reagents
        //  Scrolls
        //  Blank scrolls
        //  Potions
        //  Gems
        //  Magic Wand
        //  Magic weapon
        //  Magic armour
        //  Magic clothing (not implemented)
        //  Magic jewelry (not implemented)
        //  Crystal ball (not implemented)

        // Gold
        var gold = new Gold(Utility.Random(200, 400));
        DropItem(gold);
        ActivityTrackingService.RegisterDungeonTreasureChestGold(this, gold);

        // Reagents
        for (var i = Utility.Random(4); i > 0; i--)
        {
            var reagents = Loot.RandomReagent();
            reagents.Amount = 12;
            DropItem(reagents);
        }

        // Scrolls
        for (var i = Utility.Random(4); i > 0; i--)
        {
            var scroll = Loot.RandomScroll(0, 47, SpellbookType.Regular);
            scroll.Amount = 16;
            DropItem(scroll);
        }

        // Drop blank scrolls
        DropItem(new BlankScroll(Utility.Random(1, 4)));

        // Potions
        for (var i = Utility.Random(4); i > 0; i--)
        {
            DropItem(Loot.RandomPotion());
        }

        // Gems
        for (var i = Utility.Random(4); i > 0; i--)
        {
            var gems = Loot.RandomGem();
            gems.Amount = 12;
            DropItem(gems);
        }

        // Magic Wand
        for (var i = Utility.Random(4); i > 0; i--)
        {
            DropItem(Loot.RandomWand());
        }

        // Equipment
        for (var i = Utility.Random(4); i > 0; i--)
        {
            var item = Loot.RandomArmorOrShieldOrWeapon();

            if (item is BaseWeapon weapon)
            {
                weapon.DamageLevel = (WeaponDamageLevel)Utility.Random(4);
                weapon.AccuracyLevel = (WeaponAccuracyLevel)Utility.Random(4);
                weapon.DurabilityLevel = (WeaponDurabilityLevel)Utility.Random(4);
                weapon.Quality = WeaponQuality.Regular;
            }
            else if (item is BaseArmor armor)
            {
                armor.ProtectionLevel = (ArmorProtectionLevel)Utility.Random(4);
                armor.Durability = (ArmorDurabilityLevel)Utility.Random(4);
                armor.Quality = ArmorQuality.Regular;
            }

            DropItem(item);
        }

        // Clothing
        for (var i = Utility.Random(3); i > 0; i--)
        {
            DropItem(Loot.RandomClothing());
        }

        // Jewelry
        for (var i = Utility.Random(3); i > 0; i--)
        {
            DropItem(Loot.RandomJewelry());
        }

        // Crystal ball (not implemented)
    }

    public override bool Decays => true;

    public override bool IsDecoContainer => false;

    public override TimeSpan DecayTime => TimeSpan.FromMinutes(Utility.Random(15, 60));

    public override int DefaultGumpID => 0x42;

    public override int DefaultDropSound => 0x42;

    public override Rectangle2D Bounds => new(18, 105, 144, 73);

    private static readonly (int, int)[] _chestAppearances =
    {
        // Wooden Chest
        (0xe42, 0x49),
        (0xe43, 0x49),

        // Metal Chest
        (0x9ab, 0x4A),
        (0xe7c, 0x4A),

        // Metal Golden Chest
        (0xe40, 0x42),
        (0xe41, 0x42),

        // Keg
        (0xe7f, 0x3e),
    };

    private void SetChestAppearance()
    {
        (ItemID, GumpID) = _chestAppearances.RandomElement();
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: count and credit legacy dungeon treasure chest interactions on open and gold lift */
    public override void Open(Mobile from)
    {
        base.Open(from);

        if (!Locked && !_activityOpened)
        {
            _activityOpened = true;
            ActivityTrackingService.RecordDungeonTreasureChestOpened(from, this, nameof(TreasureChestLevel4));
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
