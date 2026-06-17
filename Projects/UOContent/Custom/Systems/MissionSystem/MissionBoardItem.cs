using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Custom.Systems.MissionSystem;

[SerializationGenerator(0)]
public partial class MissionBoardItem : Item
{
    [Constructible]
    public MissionBoardItem() : base(7774)
    {
        Name = "mission board";
        Movable = false;
    }

    public override string DefaultName => "mission board";

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player)
        {
            return;
        }

        if (!from.InRange(Location, 3))
        {
            from.SendMessage("You are too far away to use that.");
            return;
        }

        MissionSystemService.DisplayBoard(player, MissionBoardView.DailyMissives);
    }
}
