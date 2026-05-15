using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SmithStone : Item
{
    [Constructible]
    public SmithStone() : base(0xED4)
    {
        Movable = false;
        Hue = 0x476;
    }

    public override string DefaultName => "a Blacksmith Supply Stone";

    public override void OnDoubleClick(Mobile from)
    {
        /* BEGIN SUPPLY STONE FEATURE FLAG GUARD: prevent player resource generation unless the custom feature flag is enabled. */
        if (!SupplyStoneFeatureGate.CanUse(from))
        {
            return;
        }
        /* END SUPPLY STONE FEATURE FLAG GUARD */

        var SmithBag = new SmithBag();

        if (!from.AddToBackpack(SmithBag))
        {
            SmithBag.Delete();
        }
    }
}
