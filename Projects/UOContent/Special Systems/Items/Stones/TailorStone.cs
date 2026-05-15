using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class TailorStone : Item
{
    [Constructible]
    public TailorStone() : base(0xED4)
    {
        Movable = false;
        Hue = 0x315;
    }

    public override string DefaultName => "a Tailor Supply Stone";

    public override void OnDoubleClick(Mobile from)
    {
        /* BEGIN SUPPLY STONE FEATURE FLAG GUARD: prevent player resource generation unless the custom feature flag is enabled. */
        if (!SupplyStoneFeatureGate.CanUse(from))
        {
            return;
        }
        /* END SUPPLY STONE FEATURE FLAG GUARD */

        var tailorBag = new TailorBag();

        if (!from.AddToBackpack(tailorBag))
        {
            tailorBag.Delete();
        }
    }
}
