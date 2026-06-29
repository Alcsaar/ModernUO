using System;
using Server.Mobiles;

namespace Server.Custom.Systems.VirtueAlignment;

public enum VirtueAlignmentPath
{
    None,
    Compassion,
    Justice,
    Honesty,
    Honor,
    Spirituality,
    Valor,
    Sacrifice,
    Humility,
    Cruelty,
    Vengeance,
    Deceit,
    Treachery,
    Corruption,
    Cowardice,
    Greed,
    Pride
}

public enum VirtueAlignmentSide
{
    None,
    Virtue,
    Vice
}

public enum VirtueAlignmentActionKind
{
    Manual,
    Staff,
    CompassionateAid,
    CruelAct,
    JustAct,
    VengefulAct,
    HonestAct,
    DeceitfulAct,
    HonorableAct,
    TreacherousAct,
    SpiritualAct,
    CorruptAct,
    ValiantAct,
    CowardlyAct,
    SacrificialAct,
    GreedyAct,
    HumbleAct,
    PridefulAct
}

public enum VirtueConvictionRank
{
    Unproven,
    Initiate,
    Seeker,
    Follower,
    Adept,
    Exemplar
}

public sealed class VirtueAlignmentProfile
{
    public PlayerMobile Player { get; set; }
    public VirtueAlignmentPath PrimaryAspiration { get; set; }
    public VirtueAlignmentPath SecondaryAspiration { get; set; }
    public DateTime AspirationsChosenAt { get; set; }
    public string AspirationsChosenBy { get; set; }
    public int[] Tendencies { get; set; } = new int[Enum.GetValues<VirtueAlignmentPath>().Length];
    public int Conviction { get; set; }

    /*
     * Aspirations are what the player says they are reaching toward. Expressed
     * alignment is calculated from action-earned tendency scores in the service.
     */
    public bool HasAspirations =>
        PrimaryAspiration != VirtueAlignmentPath.None && SecondaryAspiration != VirtueAlignmentPath.None;

    public bool HasSelection => HasAspirations;
}
