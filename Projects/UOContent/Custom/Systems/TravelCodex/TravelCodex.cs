using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Custom.Systems.TravelCodex;

[SerializationGenerator(0)]
public partial class TravelCodex : Item
{
    private int _charges;

    [SerializableProperty(0, useField: nameof(_charges))]
    [CommandProperty(AccessLevel.GameMaster)]
    public int Charges
    {
        get => _charges;
        set
        {
            var next = value;

            if (next < 0)
            {
                next = 0;
            }

            if (next > TravelCodexSettings.MaxCharges)
            {
                next = TravelCodexSettings.MaxCharges;
            }

            if (_charges == next)
            {
                return;
            }

            _charges = next;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [Constructible]
    public TravelCodex() : base(0x42BF)
    {
        Weight = 1.0;
        LootType = LootType.Blessed;
        Hue = 1150;
        _charges = 0;
    }

    public override string DefaultName => "a travel codex";

    public override void OnDoubleClick(Mobile from)
    {
        if (!TravelCodexManager.IsSystemEnabled())
        {
            from.SendMessage(0x22, "The travel codex is currently disabled.");
            return;
        }

        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001);
            return;
        }

        TravelCodexGump.DisplayTo(from, this, TravelCategory.Town);
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{"Charges: "}{_charges}/{TravelCodexSettings.MaxCharges}");
    }

    public static void BeginLoadCharges(Mobile from, TravelCodex codex)
    {
        if (from == null || codex == null)
        {
            return;
        }

        if (!codex.IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001);
            return;
        }

        from.SendMessage(0x35, "Target recall scrolls in your backpack to load charges into the codex.");
        from.Target = new LoadRecallScrollTarget(codex);
    }

    private sealed class LoadRecallScrollTarget : Target
    {
        private readonly TravelCodex _codex;

        public LoadRecallScrollTarget(TravelCodex codex) : base(-1, false, TargetFlags.None)
        {
            _codex = codex;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_codex == null || _codex.Deleted)
            {
                from.SendMessage(0x22, "That codex is no longer available.");
                return;
            }

            if (targeted is RecallScroll scroll)
            {
                TravelCodexManager.TryConsumeRecallScrolls(from, _codex, scroll);
            }
            else
            {
                from.SendMessage(0x22, "That is not a recall scroll.");
            }
        }
    }
}
