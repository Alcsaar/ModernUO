namespace Server.Engines.RelativeThreatSystem;

public sealed class PlayerCombatPowerResult
{
    public PlayerCombatPowerResult(
        double powerScore,
        double meleeScore,
        double archerScore,
        double mageScore,
        double tamerScore,
        double bardScore,
        double healingScore,
        double magicResistScore,
        string primaryStyle,
        string secondaryStyle
    )
    {
        PowerScore = powerScore;
        MeleeScore = meleeScore;
        ArcherScore = archerScore;
        MageScore = mageScore;
        TamerScore = tamerScore;
        BardScore = bardScore;
        HealingScore = healingScore;
        MagicResistScore = magicResistScore;
        PrimaryStyle = primaryStyle ?? "Unknown";
        SecondaryStyle = secondaryStyle ?? "Unknown";
    }

    public double PowerScore { get; }

    public double MeleeScore { get; }

    public double ArcherScore { get; }

    public double MageScore { get; }

    public double TamerScore { get; }

    public double BardScore { get; }

    public double HealingScore { get; }

    public double MagicResistScore { get; }

    public string PrimaryStyle { get; }

    public string SecondaryStyle { get; }
}
