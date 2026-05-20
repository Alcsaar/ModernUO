using ModernUO.Serialization;
using Server.Custom.Systems.RareSpawns;

namespace Server.Items;

[SerializationGenerator(0, false)]
public abstract partial class CRareSpawnItem : Item
{
    protected CRareSpawnItem(int itemID) : base(itemID)
    {
        Movable = true;
        Stackable = false;
    }

    protected CRareSpawnItem(Serial serial) : base(serial)
    {
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public RareRespawnProfile RareLevel => DefaultRareLevel;

    protected virtual RareRespawnProfile DefaultRareLevel => RareRespawnProfile.Custom;
}

/* BEGIN MISC RARE SPAWN ITEMS: generated item entries for missing rare spawn decorations. */

[SerializationGenerator(0, false)]
public partial class CRareDeceitADungeonOfHorrors4030 : CRareSpawnItem
{
    [Constructible]
    public CRareDeceitADungeonOfHorrors4030() : base(0xFBE)
    {
    }

    public override string DefaultName => "Deceit: A Dungeon Of Horrors";
}

[SerializationGenerator(0, false)]
public partial class CRareOnTheDiversityOfOurLand4030 : CRareSpawnItem
{
    [Constructible]
    public CRareOnTheDiversityOfOurLand4030() : base(0xFBE)
    {
    }

    public override string DefaultName => "On The Diversity Of Our Land";
}

[SerializationGenerator(0, false)]
public partial class CRareRegardingLlamas4030 : CRareSpawnItem
{
    [Constructible]
    public CRareRegardingLlamas4030() : base(0xFBE)
    {
    }

    public override string DefaultName => "Regarding Llamas";
}

[SerializationGenerator(0, false)]
public partial class CRareBookOfTruth17087 : CRareSpawnItem
{
    [Constructible]
    public CRareBookOfTruth17087() : base(0x42BF)
    {
    }

    public override string DefaultName => "Book Of Truth";
}

[SerializationGenerator(0, false)]
public partial class CRareBookOfTruth7187 : CRareSpawnItem
{
    [Constructible]
    public CRareBookOfTruth7187() : base(0x1C13)
    {
    }

    public override string DefaultName => "Book Of Truth";
}

[SerializationGenerator(0, false)]
public partial class CRareBrush4976 : CRareSpawnItem
{
    [Constructible]
    public CRareBrush4976() : base(0x1370)
    {
    }

    public override string DefaultName => "Brush";
}

[SerializationGenerator(0, false)]
public partial class CRareBrush4977 : CRareSpawnItem
{
    [Constructible]
    public CRareBrush4977() : base(0x1371)
    {
    }

    public override string DefaultName => "Brush";
}

[SerializationGenerator(0, false)]
public partial class CRareBrush4978 : CRareSpawnItem
{
    [Constructible]
    public CRareBrush4978() : base(0x1372)
    {
    }

    public override string DefaultName => "Brush";
}

[SerializationGenerator(0, false)]
public partial class CRareBrush4979 : CRareSpawnItem
{
    [Constructible]
    public CRareBrush4979() : base(0x1373)
    {
    }

    public override string DefaultName => "Brush";
}

[SerializationGenerator(0, false)]
public partial class CRareCandelabra2854 : CRareSpawnItem
{
    [Constructible]
    public CRareCandelabra2854() : base(0xB26)
    {
    }

    public override string DefaultName => "Candelabra";
}

[SerializationGenerator(0, false)]
public partial class CRareCandle5168 : CRareSpawnItem
{
    [Constructible]
    public CRareCandle5168() : base(0x1430)
    {
    }

    public override string DefaultName => "Candle";
}

[SerializationGenerator(0, false)]
public partial class CRareCandle5172 : CRareSpawnItem
{
    [Constructible]
    public CRareCandle5172() : base(0x1434)
    {
    }

    public override string DefaultName => "Candle";
}

[SerializationGenerator(0, false)]
public partial class CRareCandle5173 : CRareSpawnItem
{
    [Constructible]
    public CRareCandle5173() : base(0x1435)
    {
    }

    public override string DefaultName => "Candle";
}

[SerializationGenerator(0, false)]
public partial class CRareCannonBalls3700 : CRareSpawnItem
{
    [Constructible]
    public CRareCannonBalls3700() : base(0xE74)
    {
    }

    public override string DefaultName => "Cannon Balls";
}

[SerializationGenerator(0, false)]
public partial class CRareCauldron2420 : CRareSpawnItem
{
    [Constructible]
    public CRareCauldron2420() : base(0x974)
    {
    }

    public override string DefaultName => "Cauldron";
}

[SerializationGenerator(0, false)]
public partial class CRareCauldron2421 : CRareSpawnItem
{
    [Constructible]
    public CRareCauldron2421() : base(0x975)
    {
    }

    public override string DefaultName => "Cauldron";
}

[SerializationGenerator(0, false)]
public partial class CRareCeramicMug2506 : CRareSpawnItem
{
    [Constructible]
    public CRareCeramicMug2506() : base(0x9CA)
    {
    }

    public override string DefaultName => "Ceramic Mug";
}

[SerializationGenerator(0, false)]
public partial class CRareCeramicMug2453 : CRareSpawnItem
{
    [Constructible]
    public CRareCeramicMug2453() : base(0x995)
    {
    }

    public override string DefaultName => "Ceramic Mug";
}

[SerializationGenerator(0, false)]
public partial class CRareCeramicMug2454 : CRareSpawnItem
{
    [Constructible]
    public CRareCeramicMug2454() : base(0x996)
    {
    }

    public override string DefaultName => "Ceramic Mug";
}

[SerializationGenerator(0, false)]
public partial class CRareCeramicMug2455 : CRareSpawnItem
{
    [Constructible]
    public CRareCeramicMug2455() : base(0x997)
    {
    }

    public override string DefaultName => "Ceramic Mug";
}

[SerializationGenerator(0, false)]
public partial class CRareCeramicMug2456 : CRareSpawnItem
{
    [Constructible]
    public CRareCeramicMug2456() : base(0x998)
    {
    }

    public override string DefaultName => "Ceramic Mug";
}

[SerializationGenerator(0, false)]
public partial class CRareCeramicMug2457 : CRareSpawnItem
{
    [Constructible]
    public CRareCeramicMug2457() : base(0x999)
    {
    }

    public override string DefaultName => "Ceramic Mug";
}

[SerializationGenerator(0, false)]
public partial class CRareCheckers4004 : CRareSpawnItem
{
    [Constructible]
    public CRareCheckers4004() : base(0xFA4)
    {
    }

    public override string DefaultName => "Checkers";
}

[SerializationGenerator(0, false)]
public partial class CRareCheckers4005 : CRareSpawnItem
{
    [Constructible]
    public CRareCheckers4005() : base(0xFA5)
    {
    }

    public override string DefaultName => "Checkers";
}

[SerializationGenerator(0, false)]
public partial class CRareChessmen4008 : CRareSpawnItem
{
    [Constructible]
    public CRareChessmen4008() : base(0xFA8)
    {
    }

    public override string DefaultName => "Chessmen";
}

[SerializationGenerator(0, false)]
public partial class CRareChickenOnASpit7828 : CRareSpawnItem
{
    [Constructible]
    public CRareChickenOnASpit7828() : base(0x1E94)
    {
    }

    public override string DefaultName => "Chicken On A Spit";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.ServerBirth;
}

[SerializationGenerator(0, false)]
public partial class CRareCloth5991 : CRareSpawnItem
{
    [Constructible]
    public CRareCloth5991() : base(0x1767)
    {
    }

    public override string DefaultName => "Cloth";
}

[SerializationGenerator(0, false)]
public partial class CRareConchShell4036 : CRareSpawnItem
{
    [Constructible]
    public CRareConchShell4036() : base(0xFC4)
    {
    }

    public override string DefaultName => "Conch Shell";
}

[SerializationGenerator(0, false)]
public partial class CRareCopperCoins3820 : CRareSpawnItem
{
    [Constructible]
    public CRareCopperCoins3820() : base(0xEEC)
    {
    }

    public override string DefaultName => "Copper Coins";
}

[SerializationGenerator(0, false)]
public partial class CRareCopperIngot7139 : CRareSpawnItem
{
    [Constructible]
    public CRareCopperIngot7139() : base(0x1BE3)
    {
    }

    public override string DefaultName => "Copper Ingot";
}

[SerializationGenerator(0, false)]
public partial class CRareCopperIngot7142 : CRareSpawnItem
{
    [Constructible]
    public CRareCopperIngot7142() : base(0x1BE6)
    {
    }

    public override string DefaultName => "Copper Ingot";
}

[SerializationGenerator(0, false)]
public partial class CRareCopperIngots7140 : CRareSpawnItem
{
    [Constructible]
    public CRareCopperIngots7140() : base(0x1BE4)
    {
    }

    public override string DefaultName => "Copper Ingots";
}

[SerializationGenerator(0, false)]
public partial class CRareCopperIngots7143 : CRareSpawnItem
{
    [Constructible]
    public CRareCopperIngots7143() : base(0x1BE7)
    {
    }

    public override string DefaultName => "Copper Ingots";
}

[SerializationGenerator(0, false)]
public partial class CRareCopperIngots7144 : CRareSpawnItem
{
    [Constructible]
    public CRareCopperIngots7144() : base(0x1BE8)
    {
    }

    public override string DefaultName => "Copper Ingots";
}

[SerializationGenerator(0, false)]
public partial class CRareCrossbowBolt7166 : CRareSpawnItem
{
    [Constructible]
    public CRareCrossbowBolt7166() : base(0x1BFE)
    {
    }

    public override string DefaultName => "Crossbow Bolt";
}

[SerializationGenerator(0, false)]
public partial class CRareCrossbowBolt7167 : CRareSpawnItem
{
    [Constructible]
    public CRareCrossbowBolt7167() : base(0x1BFF)
    {
    }

    public override string DefaultName => "Crossbow Bolt";
}

[SerializationGenerator(0, false)]
public partial class CRareCrossbowBolts7165 : CRareSpawnItem
{
    [Constructible]
    public CRareCrossbowBolts7165() : base(0x1BFD)
    {
    }

    public override string DefaultName => "Crossbow Bolts";
}

[SerializationGenerator(0, false)]
public partial class CRareCrystalBall3629 : CRareSpawnItem
{
    [Constructible]
    public CRareCrystalBall3629() : base(0xE2D)
    {
    }

    public override string DefaultName => "Crystal Ball";
}

[SerializationGenerator(0, false)]
public partial class CRareCutHair3582 : CRareSpawnItem
{
    [Constructible]
    public CRareCutHair3582() : base(0xDFE)
    {
    }

    public override string DefaultName => "Cut Hair";
}

[SerializationGenerator(0, false)]
public partial class CRareDippingStick5160 : CRareSpawnItem
{
    [Constructible]
    public CRareDippingStick5160() : base(0x1428)
    {
    }

    public override string DefaultName => "Dipping Stick";
}

[SerializationGenerator(0, false)]
public partial class CRareDoughBowl4323 : CRareSpawnItem
{
    [Constructible]
    public CRareDoughBowl4323() : base(0x10E3)
    {
    }

    public override string DefaultName => "Dough Bowl";
}

[SerializationGenerator(0, false)]
public partial class CRareJugsOfCider2445 : CRareSpawnItem
{
    [Constructible]
    public CRareJugsOfCider2445() : base(0x98D)
    {
    }

    public override string DefaultName => "Jugs Of Cider";
}

[SerializationGenerator(0, false)]
public partial class CRareJugsOfCider2446 : CRareSpawnItem
{
    [Constructible]
    public CRareJugsOfCider2446() : base(0x98E)
    {
    }

    public override string DefaultName => "Jugs Of Cider";
}

[SerializationGenerator(0, false)]
public partial class CRareKnife2469 : CRareSpawnItem
{
    [Constructible]
    public CRareKnife2469() : base(0x9A5)
    {
    }

    public override string DefaultName => "Knife";
}

[SerializationGenerator(0, false)]
public partial class CRareKnife2470 : CRareSpawnItem
{
    [Constructible]
    public CRareKnife2470() : base(0x9A6)
    {
    }

    public override string DefaultName => "Knife";
}

[SerializationGenerator(0, false)]
public partial class CRareKnitting3574 : CRareSpawnItem
{
    [Constructible]
    public CRareKnitting3574() : base(0xDF6)
    {
    }

    public override string DefaultName => "Knitting";
}

[SerializationGenerator(0, false)]
public partial class CRareKnitting3575 : CRareSpawnItem
{
    [Constructible]
    public CRareKnitting3575() : base(0xDF7)
    {
    }

    public override string DefaultName => "Knitting";
}

[SerializationGenerator(0, false)]
public partial class CRareLava13371 : CRareSpawnItem
{
    [Constructible]
    public CRareLava13371() : base(0x343B)
    {
    }

    public override string DefaultName => "Lava";
}

[SerializationGenerator(0, false)]
public partial class CRareLoomBench4169 : CRareSpawnItem
{
    [Constructible]
    public CRareLoomBench4169() : base(0x1049)
    {
    }

    public override string DefaultName => "Loom Bench";
}

[SerializationGenerator(0, false)]
public partial class CRareLoomBench4170 : CRareSpawnItem
{
    [Constructible]
    public CRareLoomBench4170() : base(0x104A)
    {
    }

    public override string DefaultName => "Loom Bench";
}

[SerializationGenerator(0, false)]
public partial class CRareLooseGrain5193 : CRareSpawnItem
{
    [Constructible]
    public CRareLooseGrain5193() : base(0x1449)
    {
    }

    public override string DefaultName => "Loose Grain";
}

[SerializationGenerator(0, false)]
public partial class CRareMagicKey4114 : CRareSpawnItem
{
    [Constructible]
    public CRareMagicKey4114() : base(0x1012)
    {
    }

    public override string DefaultName => "Magic Key";
}

[SerializationGenerator(0, false)]
public partial class CRareMagicalCrystal7964 : CRareSpawnItem
{
    [Constructible]
    public CRareMagicalCrystal7964() : base(0x1F1C)
    {
    }

    public override string DefaultName => "Magical Crystal";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleBench1113 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleBench1113() : base(0x459)
    {
    }

    public override string DefaultName => "Marble Bench";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleBench1114 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleBench1114() : base(0x45A)
    {
    }

    public override string DefaultName => "Marble Bench";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleBench7629 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleBench7629() : base(0x1DCD)
    {
    }

    public override string DefaultName => "Marble Bench";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleBench7630 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleBench7630() : base(0x1DCE)
    {
    }

    public override string DefaultName => "Marble Bench";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleBench7631 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleBench7631() : base(0x1DCF)
    {
    }

    public override string DefaultName => "Marble Bench";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleBench7632 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleBench7632() : base(0x1DD0)
    {
    }

    public override string DefaultName => "Marble Bench";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleBench7633 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleBench7633() : base(0x1DD1)
    {
    }

    public override string DefaultName => "Marble Bench";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleBench7634 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleBench7634() : base(0x1DD2)
    {
    }

    public override string DefaultName => "Marble Bench";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleColumn283 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleColumn283() : base(0x11B)
    {
    }

    public override string DefaultName => "Marble Column";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.ServerBirth;
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleTable7617 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleTable7617() : base(0x1DC1)
    {
    }

    public override string DefaultName => "Marble Table";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleTable7618 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleTable7618() : base(0x1DC2)
    {
    }

    public override string DefaultName => "Marble Table";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleTable7619 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleTable7619() : base(0x1DC3)
    {
    }

    public override string DefaultName => "Marble Table";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleTable7620 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleTable7620() : base(0x1DC4)
    {
    }

    public override string DefaultName => "Marble Table";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleTable7621 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleTable7621() : base(0x1DC5)
    {
    }

    public override string DefaultName => "Marble Table";
}

[SerializationGenerator(0, false)]
public partial class CRareMarbleTable7622 : CRareSpawnItem
{
    [Constructible]
    public CRareMarbleTable7622() : base(0x1DC6)
    {
    }

    public override string DefaultName => "Marble Table";
}

[SerializationGenerator(0, false)]
public partial class CRareMouldingBoard5353 : CRareSpawnItem
{
    [Constructible]
    public CRareMouldingBoard5353() : base(0x14E9)
    {
    }

    public override string DefaultName => "Moulding Board";
}

[SerializationGenerator(0, false)]
public partial class CRareMouldingBoard5354 : CRareSpawnItem
{
    [Constructible]
    public CRareMouldingBoard5354() : base(0x14EA)
    {
    }

    public override string DefaultName => "Moulding Board";
}

[SerializationGenerator(0, false)]
public partial class CRareMushrooms3341 : CRareSpawnItem
{
    [Constructible]
    public CRareMushrooms3341() : base(0xD0D)
    {
    }

    public override string DefaultName => "Mushrooms";
}

[SerializationGenerator(0, false)]
public partial class CRareMushrooms3343 : CRareSpawnItem
{
    [Constructible]
    public CRareMushrooms3343() : base(0xD0F)
    {
    }

    public override string DefaultName => "Mushrooms";
}

[SerializationGenerator(0, false)]
public partial class CRareMusicStand3765 : CRareSpawnItem
{
    [Constructible]
    public CRareMusicStand3765() : base(0xEB5)
    {
    }

    public override string DefaultName => "Music Stand";
}

[SerializationGenerator(0, false)]
public partial class CRareMusicStand3766 : CRareSpawnItem
{
    [Constructible]
    public CRareMusicStand3766() : base(0xEB6)
    {
    }

    public override string DefaultName => "Music Stand";
}

[SerializationGenerator(0, false)]
public partial class CRareMusicStand3767 : CRareSpawnItem
{
    [Constructible]
    public CRareMusicStand3767() : base(0xEB7)
    {
    }

    public override string DefaultName => "Music Stand";
}

[SerializationGenerator(0, false)]
public partial class CRareMusicStand3768 : CRareSpawnItem
{
    [Constructible]
    public CRareMusicStand3768() : base(0xEB8)
    {
    }

    public override string DefaultName => "Music Stand";
}

[SerializationGenerator(0, false)]
public partial class CRareMusicStand3769 : CRareSpawnItem
{
    [Constructible]
    public CRareMusicStand3769() : base(0xEB9)
    {
    }

    public override string DefaultName => "Music Stand";
}

[SerializationGenerator(0, false)]
public partial class CRareMusicStand3770 : CRareSpawnItem
{
    [Constructible]
    public CRareMusicStand3770() : base(0xEBA)
    {
    }

    public override string DefaultName => "Music Stand";
}

[SerializationGenerator(0, false)]
public partial class CRareMusicStand3771 : CRareSpawnItem
{
    [Constructible]
    public CRareMusicStand3771() : base(0xEBB)
    {
    }

    public override string DefaultName => "Music Stand";
}

[SerializationGenerator(0, false)]
public partial class CRareMusicStand3772 : CRareSpawnItem
{
    [Constructible]
    public CRareMusicStand3772() : base(0xEBC)
    {
    }

    public override string DefaultName => "Music Stand";
}

[SerializationGenerator(0, false)]
public partial class CRareNautilus4039 : CRareSpawnItem
{
    [Constructible]
    public CRareNautilus4039() : base(0xFC7)
    {
    }

    public override string DefaultName => "Nautilus";
}

[SerializationGenerator(0, false)]
public partial class CRareNest6869 : CRareSpawnItem
{
    [Constructible]
    public CRareNest6869() : base(0x1AD5)
    {
    }

    public override string DefaultName => "Nest";
}

[SerializationGenerator(0, false)]
public partial class CRareNestWithEggs6868 : CRareSpawnItem
{
    [Constructible]
    public CRareNestWithEggs6868() : base(0x1AD4)
    {
    }

    public override string DefaultName => "Nest With Eggs";
}

[SerializationGenerator(0, false)]
public partial class CRareNightshade6376 : CRareSpawnItem
{
    [Constructible]
    public CRareNightshade6376() : base(0x18E8)
    {
    }

    public override string DefaultName => "Nightshade";
}

[SerializationGenerator(0, false)]
public partial class CRarePan2547 : CRareSpawnItem
{
    [Constructible]
    public CRarePan2547() : base(0x9F3)
    {
    }

    public override string DefaultName => "Pan";
}

[SerializationGenerator(0, false)]
public partial class CRarePedestal4643 : CRareSpawnItem
{
    [Constructible]
    public CRarePedestal4643() : base(0x1223)
    {
    }

    public override string DefaultName => "Pedestal";
}

[SerializationGenerator(0, false)]
public partial class CRarePedestal7978 : CRareSpawnItem
{
    [Constructible]
    public CRarePedestal7978() : base(0x1F2A)
    {
    }

    public override string DefaultName => "Pedestal";
}

[SerializationGenerator(0, false)]
public partial class CRarePewterMug4095 : CRareSpawnItem
{
    [Constructible]
    public CRarePewterMug4095() : base(0xFFF)
    {
    }

    public override string DefaultName => "Pewter Mug";
}

[SerializationGenerator(0, false)]
public partial class CRarePicnicBasket3706 : CRareSpawnItem
{
    [Constructible]
    public CRarePicnicBasket3706() : base(0xE7A)
    {
    }

    public override string DefaultName => "Picnic Basket";
}

[SerializationGenerator(0, false)]
public partial class CRarePigSFeet7820 : CRareSpawnItem
{
    [Constructible]
    public CRarePigSFeet7820() : base(0x1E8C)
    {
    }

    public override string DefaultName => "Pig's Feet";
}

[SerializationGenerator(0, false)]
public partial class CRarePigSFeet7821 : CRareSpawnItem
{
    [Constructible]
    public CRarePigSFeet7821() : base(0x1E8D)
    {
    }

    public override string DefaultName => "Pig's Feet";
}

[SerializationGenerator(0, false)]
public partial class CRarePigSHead7822 : CRareSpawnItem
{
    [Constructible]
    public CRarePigSHead7822() : base(0x1E8E)
    {
    }

    public override string DefaultName => "Pig's Head";
}

[SerializationGenerator(0, false)]
public partial class CRarePigSHead7823 : CRareSpawnItem
{
    [Constructible]
    public CRarePigSHead7823() : base(0x1E8F)
    {
    }

    public override string DefaultName => "Pig's Head";
}

[SerializationGenerator(0, false)]
public partial class CRarePitcher2518 : CRareSpawnItem
{
    [Constructible]
    public CRarePitcher2518() : base(0x9D6)
    {
    }

    public override string DefaultName => "Pitcher";
}

[SerializationGenerator(0, false)]
public partial class CRarePlateOfFood2479 : CRareSpawnItem
{
    [Constructible]
    public CRarePlateOfFood2479() : base(0x9AF)
    {
    }

    public override string DefaultName => "Plate Of Food";
}

[SerializationGenerator(0, false)]
public partial class CRarePluckedChicken7819 : CRareSpawnItem
{
    [Constructible]
    public CRarePluckedChicken7819() : base(0x1E8B)
    {
    }

    public override string DefaultName => "Plucked Chicken";
}

[SerializationGenerator(0, false)]
public partial class CRarePoleWithShackles5696 : CRareSpawnItem
{
    [Constructible]
    public CRarePoleWithShackles5696() : base(0x1640)
    {
    }

    public override string DefaultName => "Pole With Shackles";
}

[SerializationGenerator(0, false)]
public partial class CRarePotOfWax5162 : CRareSpawnItem
{
    [Constructible]
    public CRarePotOfWax5162() : base(0x142A)
    {
    }

    public override string DefaultName => "Pot Of Wax";
}

[SerializationGenerator(0, false)]
public partial class CRarePotOfWax5163 : CRareSpawnItem
{
    [Constructible]
    public CRarePotOfWax5163() : base(0x142B)
    {
    }

    public override string DefaultName => "Pot Of Wax";
}

[SerializationGenerator(0, false)]
public partial class CRarePullies7836 : CRareSpawnItem
{
    [Constructible]
    public CRarePullies7836() : base(0x1E9C)
    {
    }

    public override string DefaultName => "Pullies";
}

[SerializationGenerator(0, false)]
public partial class CRarePullies7837 : CRareSpawnItem
{
    [Constructible]
    public CRarePullies7837() : base(0x1E9D)
    {
    }

    public override string DefaultName => "Pullies";
}

[SerializationGenerator(0, false)]
public partial class CRarePully7838 : CRareSpawnItem
{
    [Constructible]
    public CRarePully7838() : base(0x1E9E)
    {
    }

    public override string DefaultName => "Pully";
}

[SerializationGenerator(0, false)]
public partial class CRarePully7839 : CRareSpawnItem
{
    [Constructible]
    public CRarePully7839() : base(0x1E9F)
    {
    }

    public override string DefaultName => "Pully";
}

[SerializationGenerator(0, false)]
public partial class CRarePylon7882 : CRareSpawnItem
{
    [Constructible]
    public CRarePylon7882() : base(0x1ECA)
    {
    }

    public override string DefaultName => "Pylon";
}

[SerializationGenerator(0, false)]
public partial class CRareRackOfCanvases3954 : CRareSpawnItem
{
    [Constructible]
    public CRareRackOfCanvases3954() : base(0xF72)
    {
    }

    public override string DefaultName => "Rack Of Canvases";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.ServerBirth;
}

[SerializationGenerator(0, false)]
public partial class CRareRackOfCanvases3955 : CRareSpawnItem
{
    [Constructible]
    public CRareRackOfCanvases3955() : base(0xF73)
    {
    }

    public override string DefaultName => "Rack Of Canvases";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.ServerBirth;
}

[SerializationGenerator(0, false)]
public partial class CRareRawFish7701 : CRareSpawnItem
{
    [Constructible]
    public CRareRawFish7701() : base(0x1E15)
    {
    }

    public override string DefaultName => "Raw Fish";
}

[SerializationGenerator(0, false)]
public partial class CRareStatue4824 : CRareSpawnItem
{
    [Constructible]
    public CRareStatue4824() : base(0x12D8)
    {
    }

    public override string DefaultName => "Statue";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.ServerBirth;
}

[SerializationGenerator(0, false)]
public partial class CRareStatue4825 : CRareSpawnItem
{
    [Constructible]
    public CRareStatue4825() : base(0x12D9)
    {
    }

    public override string DefaultName => "Statue";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.ServerBirth;
}

[SerializationGenerator(0, false)]
public partial class CRareBeefCarcass6257 : CRareSpawnItem
{
    [Constructible]
    public CRareBeefCarcass6257() : base(0x1871)
    {
    }

    public override string DefaultName => "Beef Carcass";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.ServerBirth;
}

[SerializationGenerator(0, false)]
public partial class CRareBeefCarcass6258 : CRareSpawnItem
{
    [Constructible]
    public CRareBeefCarcass6258() : base(0x1872)
    {
    }

    public override string DefaultName => "Beef Carcass";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.ServerBirth;
}

[SerializationGenerator(0, false)]
public partial class CRareCoveredChair3095 : CRareSpawnItem
{
    [Constructible]
    public CRareCoveredChair3095() : base(0x0C17)
    {
    }

    public override string DefaultName => "Covered Chair";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.Monthly;
}

[SerializationGenerator(0, false)]
public partial class CRareCoveredChair3096 : CRareSpawnItem
{
    [Constructible]
    public CRareCoveredChair3096() : base(0x0C18)
    {
    }

    public override string DefaultName => "Covered Chair";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.Monthly;
}

[SerializationGenerator(0, false)]
public partial class CRareEmptyJars3652 : CRareSpawnItem
{
    [Constructible]
    public CRareEmptyJars3652() : base(0x0E44)
    {
    }

    public override string DefaultName => "Empty Jars";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.Daily;
}

[SerializationGenerator(0, false)]
public partial class CRareEmptyJars3653 : CRareSpawnItem
{
    [Constructible]
    public CRareEmptyJars3653() : base(0x0E45)
    {
    }

    public override string DefaultName => "Empty Jars";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.Daily;
}

[SerializationGenerator(0, false)]
public partial class CRareEmptyJars3654 : CRareSpawnItem
{
    [Constructible]
    public CRareEmptyJars3654() : base(0x0E46)
    {
    }

    public override string DefaultName => "Empty Jars";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.Daily;
}

[SerializationGenerator(0, false)]
public partial class CRareEmptyJars3655 : CRareSpawnItem
{
    [Constructible]
    public CRareEmptyJars3655() : base(0x0E47)
    {
    }

    public override string DefaultName => "Empty Jars";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.Daily;
}

[SerializationGenerator(0, false)]
public partial class CRareLog7135 : CRareSpawnItem
{
    [Constructible]
    public CRareLog7135() : base(0x1BDF)
    {
    }

    public override string DefaultName => "Log";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.Monthly;
}

[SerializationGenerator(0, false)]
public partial class CRareLog7136 : CRareSpawnItem
{
    [Constructible]
    public CRareLog7136() : base(0x1BE0)
    {
    }

    public override string DefaultName => "Log";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.Weekly;
}

[SerializationGenerator(0, false)]
public partial class CRareLog7137 : CRareSpawnItem
{
    [Constructible]
    public CRareLog7137() : base(0x1BE1)
    {
    }

    public override string DefaultName => "Log";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.Weekly;
}

[SerializationGenerator(0, false)]
public partial class CRareLog7138 : CRareSpawnItem
{
    [Constructible]
    public CRareLog7138() : base(0x1BE2)
    {
    }

    public override string DefaultName => "Log";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.Monthly;
}

[SerializationGenerator(0, false)]
public partial class CRareSaddle3895 : CRareSpawnItem
{
    [Constructible]
    public CRareSaddle3895() : base(0x0F37)
    {
    }

    public override string DefaultName => "Saddle";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.Monthly;
}

[SerializationGenerator(0, false)]
public partial class CRareSaddle3896 : CRareSpawnItem
{
    [Constructible]
    public CRareSaddle3896() : base(0x0F38)
    {
    }

    public override string DefaultName => "Saddle";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.ServerBirth;
}

[SerializationGenerator(0, false)]
public partial class CRareSkinnedDeer7824 : CRareSpawnItem
{
    [Constructible]
    public CRareSkinnedDeer7824() : base(0x1E90)
    {
    }

    public override string DefaultName => "Skinned Deer";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.ServerBirth;
}

[SerializationGenerator(0, false)]
public partial class CRareSkinnedDeer7825 : CRareSpawnItem
{
    [Constructible]
    public CRareSkinnedDeer7825() : base(0x1E91)
    {
    }

    public override string DefaultName => "Skinned Deer";

    protected override RareRespawnProfile DefaultRareLevel => RareRespawnProfile.ServerBirth;
}

/* END MISC RARE SPAWN ITEMS */
