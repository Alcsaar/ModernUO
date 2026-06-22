using ModernUO.Serialization;
using Server.Guilds;
using Server.Gumps;
using Server.Items;
using Server.Multis;
using Server.Prompts;
using Server.Targeting;

namespace Server.Custom.Systems.Townships;

[SerializationGenerator(0)]
public partial class TownshipDeed : Item
{
    [Constructible]
    public TownshipDeed() : base(0x14F0)
    {
        Weight = 1.0;
        LootType = LootType.Blessed;
        Hue = 1151;
    }

    public override string DefaultName => "a township deed";

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001);
            return;
        }

        from.SendMessage(0x35, "Target a location inside your guildmaster-owned house to found the township.");
        from.Target = new FoundTownshipTarget(this);
    }

    private sealed class FoundTownshipTarget : Target
    {
        private readonly TownshipDeed _deed;

        public FoundTownshipTarget(TownshipDeed deed) : base(-1, true, TargetFlags.None)
        {
            _deed = deed;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_deed?.Deleted != false || !_deed.IsChildOf(from.Backpack))
            {
                from.SendMessage(0x22, "That township deed is no longer available.");
                return;
            }

            if (targeted is not IPoint3D point)
            {
                from.SendMessage(0x22, "That is not a valid township founding point.");
                return;
            }

            var location = new Point3D(point);
            var map = from.Map;

            if (!from.InRange(location, 3))
            {
                from.SendLocalizedMessage(500446);
                return;
            }

            from.SendMessage(0x35, "Enter a unique name for your township.");
            from.Prompt = new TownshipFoundingNamePrompt(_deed, location, map);
        }
    }

    private sealed class TownshipFoundingNamePrompt : Prompt
    {
        private readonly TownshipDeed _deed;
        private readonly Point3D _location;
        private readonly Map _map;

        public TownshipFoundingNamePrompt(TownshipDeed deed, Point3D location, Map map)
        {
            _deed = deed;
            _location = location;
            _map = map;
        }

        public override void OnResponse(Mobile from, string text)
        {
            if (_deed?.Deleted != false || !_deed.IsChildOf(from.Backpack))
            {
                from.SendMessage(0x22, "That township deed is no longer available.");
                return;
            }

            if (!TownshipService.TryFoundTownship(from, _location, _map, text, out var township, out var reason))
            {
                from.SendMessage(0x22, reason);
                return;
            }

            var stone = new TownshipStone(township.Id);
            township.Stone = stone;
            stone.MoveToWorld(_location, _map);
            TownshipService.RebuildRegion(township);
            _deed.Delete();

            from.SendMessage(0x35, $"{township.Name} has been founded.");
            TownshipService.BroadcastFounded(township);
            TownshipGump.DisplayTo(from, township);
        }

        public override void OnCancel(Mobile from)
        {
            from.SendMessage(0x22, "Township founding cancelled.");
        }
    }
}

[SerializationGenerator(0)]
public partial class TownshipStone : Item, ITownshipOwnedObject
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _townshipId;

    [Constructible]
    public TownshipStone() : this(null)
    {
    }

    public TownshipStone(string townshipId) : base(0xED4)
    {
        _townshipId = townshipId;
        Movable = false;
        Hue = 1151;
    }

    public override string DefaultName => "a township stone";

    public TownshipState Township => TownshipService.FindById(_townshipId);

    public void OnTownshipDeleted(TownshipState township)
    {
    }

    public override void OnDoubleClick(Mobile from)
    {
        var township = Township;

        if (township == null)
        {
            from.SendMessage(0x22, "This township stone is not linked to an active township.");
            return;
        }

        if (!from.InRange(GetWorldLocation(), 3) && from.AccessLevel < AccessLevel.GameMaster)
        {
            from.SendLocalizedMessage(500446);
            return;
        }

        if (TownshipService.IsGuildMember(from, township.Guild) || TownshipService.CanUseStaffTools(from))
        {
            TownshipGump.DisplayTo(from, township);
            return;
        }

        if (township.IsDelinquent)
        {
            TownshipPublicDelinquencyGump.DisplayTo(from, township);
            return;
        }

        from.SendMessage(0x22, "Only township guild members may use this township stone.");
    }

    public static void BeginMove(Mobile from, TownshipState township)
    {
        from.SendMessage(0x35, "Target a new location inside claimed township land.");
        from.Target = new MoveStoneTarget(township);
    }

    private sealed class MoveStoneTarget : Target
    {
        private readonly TownshipState _township;

        public MoveStoneTarget(TownshipState township) : base(-1, true, TargetFlags.None)
        {
            _township = township;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not IPoint3D point)
            {
                from.SendMessage(0x22, "That is not a valid location.");
                return;
            }

            if (!TownshipService.MoveStone(_township, from, new Point3D(point), from.Map, out var reason))
            {
                from.SendMessage(0x22, reason);
                return;
            }

            from.SendMessage(0x35, "The township stone has been moved.");
            TownshipGump.DisplayTo(from, _township);
        }
    }
}
