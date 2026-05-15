using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class IngotStone : Item
{
    [Constructible]
    public IngotStone() : base(0xED4)
    {
        Movable = false;
        Hue = 0x480;
    }

    public override string DefaultName => "an Ingot stone";

    public override void OnDoubleClick(Mobile from)
    {
        /* BEGIN SUPPLY STONE FEATURE FLAG GUARD: prevent player resource generation unless the custom feature flag is enabled. */
        if (!SupplyStoneFeatureGate.CanUse(from))
        {
            return;
        }
        /* END SUPPLY STONE FEATURE FLAG GUARD */

        var ingotBag = new BagOfingots();

        if (!from.AddToBackpack(ingotBag))
        {
            ingotBag.Delete();
        }
    }
}
