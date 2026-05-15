using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class RegStone : Item
{
    [Constructible]
    public RegStone() : base(0xED4)
    {
        Movable = false;
        Hue = 0x2D1;
    }

    public override string DefaultName => "a reagent stone";

    public override void OnDoubleClick(Mobile from)
    {
        /* BEGIN SUPPLY STONE FEATURE FLAG GUARD: prevent player resource generation unless the custom feature flag is enabled. */
        if (!SupplyStoneFeatureGate.CanUse(from))
        {
            return;
        }
        /* END SUPPLY STONE FEATURE FLAG GUARD */

        var regBag = new BagOfReagents();

        if (!from.AddToBackpack(regBag))
        {
            regBag.Delete();
        }
    }
}
