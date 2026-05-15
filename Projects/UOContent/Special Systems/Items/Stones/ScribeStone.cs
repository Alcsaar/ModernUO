using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ScribeStone : Item
{
    [Constructible]
    public ScribeStone() : base(0xED4)
    {
        Movable = false;
        Hue = 0x105;
    }

    public override string DefaultName => "a Scribe Supply Stone";

    public override void OnDoubleClick(Mobile from)
    {
        /* BEGIN SUPPLY STONE FEATURE FLAG GUARD: prevent player resource generation unless the custom feature flag is enabled. */
        if (!SupplyStoneFeatureGate.CanUse(from))
        {
            return;
        }
        /* END SUPPLY STONE FEATURE FLAG GUARD */

        var scribeBag = new ScribeBag();

        if (!from.AddToBackpack(scribeBag))
        {
            scribeBag.Delete();
        }
    }
}
