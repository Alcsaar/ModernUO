namespace Server.Engines.RelativeThreatSystem;

public sealed class CreatureThreatResult
{
    public CreatureThreatResult(
        string threatLabel,
        double ratio,
        double creatureScore,
        double playerScore
    )
    {
        ThreatLabel = threatLabel ?? "Fair";
        Ratio = ratio;
        CreatureScore = creatureScore;
        PlayerScore = playerScore;
    }

    public string ThreatLabel { get; }

    public double Ratio { get; }

    public double CreatureScore { get; }

    public double PlayerScore { get; }
}
