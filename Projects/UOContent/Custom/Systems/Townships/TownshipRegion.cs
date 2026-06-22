using Server.Regions;

namespace Server.Custom.Systems.Townships;

public sealed class TownshipRegion : BaseRegion
{
    public const int TownshipPriority = DefaultPriority;

    public TownshipRegion(TownshipState township, Rectangle2D[] area)
        : base(township?.Name ?? "Township", township?.Map, TownshipPriority, area)
    {
        Township = township;

        if (township != null)
        {
            GoLocation = township.FoundingPoint;
        }
    }

    public TownshipState Township { get; }

    public override bool AllowHousing(Mobile from, Point3D p)
    {
        if (Township == null || from == null)
        {
            return base.AllowHousing(from, p);
        }

        return TownshipService.IsGuildMember(from, Township.Guild);
    }

    public override void OnAggressed(Mobile aggressor, Mobile aggressed, bool criminal)
    {
        base.OnAggressed(aggressor, aggressed, criminal);

        if (criminal && aggressor != aggressed)
        {
            TownshipService.AlertTownMilitia(Township, aggressor);
        }
    }

    public override void OnCriminalAction(Mobile m, bool message)
    {
        base.OnCriminalAction(m, message);
        TownshipService.AlertTownMilitia(Township, m);
    }
}
