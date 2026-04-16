using Server.Mobiles;

namespace Server.Engines.RelativeThreatSystem;

public static class PlayerCombatPowerEvaluator
{
    private const double MinimumActiveSkill = 30.0;
    private const double MinimumSupportSkill = 40.0;
    private const double ScoreFloor = 10.0;

    public static PlayerCombatPowerResult Evaluate(Mobile player)
    {
        if (player == null || player.Deleted)
        {
            return new PlayerCombatPowerResult(
                ScoreFloor,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                "None",
                "None"
            );
        }

        var meleeScore = EvaluateMelee(player);
        var archerScore = EvaluateArcher(player);
        var mageScore = EvaluateMage(player);
        var tamerScore = EvaluateTamer(player);
        var bardScore = EvaluateBard(player);

        var healingBonus = EvaluateHealingBonus(player);
        var resistBonus = EvaluateMagicResistBonus(player);

        var primaryStyle = "None";
        var primaryScore = 0.0;

        var secondaryStyle = "None";
        var secondaryScore = 0.0;

        UpdateTopTwo("Melee", meleeScore, ref primaryStyle, ref primaryScore, ref secondaryStyle, ref secondaryScore);
        UpdateTopTwo("Archer", archerScore, ref primaryStyle, ref primaryScore, ref secondaryStyle, ref secondaryScore);
        UpdateTopTwo("Mage", mageScore, ref primaryStyle, ref primaryScore, ref secondaryStyle, ref secondaryScore);
        UpdateTopTwo("Tamer", tamerScore, ref primaryStyle, ref primaryScore, ref secondaryStyle, ref secondaryScore);
        UpdateTopTwo("Bard", bardScore, ref primaryStyle, ref primaryScore, ref secondaryStyle, ref secondaryScore);

        var baseScore = primaryScore;

        if (secondaryScore > 0.0)
        {
            baseScore += secondaryScore * 0.25;
        }

        if (baseScore > 0.0)
        {
            baseScore *= 1.0 + healingBonus + resistBonus;
        }

        if (baseScore < ScoreFloor)
        {
            baseScore = ScoreFloor;
        }

        return new PlayerCombatPowerResult(
            baseScore,
            meleeScore,
            archerScore,
            mageScore,
            tamerScore,
            bardScore,
            healingBonus,
            resistBonus,
            primaryStyle,
            secondaryStyle
        );
    }

    private static double EvaluateMelee(Mobile player)
    {
        var weaponSkill = GetBestSkill(player, SkillName.Swords, SkillName.Fencing, SkillName.Macing, SkillName.Wrestling);
        var tactics = GetSkill(player, SkillName.Tactics);
        var anatomy = GetSkill(player, SkillName.Anatomy);

        if (weaponSkill < MinimumActiveSkill)
        {
            return 0.0;
        }

        var score =
            (weaponSkill * 1.20) +
            (tactics * 0.90) +
            (anatomy * 0.50) +
            (player.Str * 0.20) +
            (player.Dex * 0.05);

        return score;
    }

    private static double EvaluateArcher(Mobile player)
    {
        var archery = GetSkill(player, SkillName.Archery);
        var tactics = GetSkill(player, SkillName.Tactics);
        var anatomy = GetSkill(player, SkillName.Anatomy);

        if (archery < MinimumActiveSkill)
        {
            return 0.0;
        }

        var score =
            (archery * 1.25) +
            (tactics * 0.85) +
            (anatomy * 0.40) +
            (player.Dex * 0.22) +
            (player.Str * 0.04);

        return score;
    }

    private static double EvaluateMage(Mobile player)
    {
        var magery = GetSkill(player, SkillName.Magery);
        var evalInt = GetSkill(player, SkillName.EvalInt);
        var meditation = GetSkill(player, SkillName.Meditation);

        if (magery < MinimumActiveSkill && evalInt < MinimumActiveSkill)
        {
            return 0.0;
        }

        var score =
            (magery * 1.15) +
            (evalInt * 1.10) +
            (meditation * 0.35) +
            (player.Int * 0.25) +
            (player.ManaMax * 0.08);

        return score;
    }

    private static double EvaluateTamer(Mobile player)
    {
        var taming = GetSkill(player, SkillName.AnimalTaming);
        var lore = GetSkill(player, SkillName.AnimalLore);
        var vet = GetSkill(player, SkillName.Veterinary);

        if (taming < MinimumActiveSkill || lore < MinimumActiveSkill)
        {
            return 0.0;
        }

        var score =
            (taming * 1.05) +
            (lore * 0.95) +
            (vet * 0.35) +
            (player.Int * 0.08);

        return score;
    }

    private static double EvaluateBard(Mobile player)
    {
        var music = GetSkill(player, SkillName.Musicianship);
        var provo = GetSkill(player, SkillName.Provocation);
        var disco = GetSkill(player, SkillName.Discordance);
        var peace = GetSkill(player, SkillName.Peacemaking);

        var bestBardSkill = Max(provo, disco, peace);

        if (music < MinimumActiveSkill || bestBardSkill < MinimumActiveSkill)
        {
            return 0.0;
        }

        var score =
            (music * 0.70) +
            (provo * 0.85) +
            (disco * 0.75) +
            (peace * 0.55) +
            (player.Int * 0.06);

        return score;
    }

    private static double EvaluateHealingBonus(Mobile player)
    {
        var healing = GetSkill(player, SkillName.Healing);
        var anatomy = GetSkill(player, SkillName.Anatomy);

        if (healing < MinimumSupportSkill || anatomy < MinimumSupportSkill)
        {
            return 0.0;
        }

        var combined = (healing + anatomy) * 0.5;

        if (combined >= 100.0)
        {
            return 0.20;
        }

        if (combined >= 80.0)
        {
            return 0.14;
        }

        if (combined >= 60.0)
        {
            return 0.08;
        }

        return 0.04;
    }

    private static double EvaluateMagicResistBonus(Mobile player)
    {
        var resist = GetSkill(player, SkillName.MagicResist);

        if (resist < MinimumSupportSkill)
        {
            return 0.0;
        }

        if (resist >= 100.0)
        {
            return 0.18;
        }

        if (resist >= 80.0)
        {
            return 0.12;
        }

        if (resist >= 60.0)
        {
            return 0.07;
        }

        return 0.03;
    }

    private static void UpdateTopTwo(
        string style,
        double score,
        ref string primaryStyle,
        ref double primaryScore,
        ref string secondaryStyle,
        ref double secondaryScore
    )
    {
        if (score <= 0.0)
        {
            return;
        }

        if (score > primaryScore)
        {
            secondaryStyle = primaryStyle;
            secondaryScore = primaryScore;
            primaryStyle = style;
            primaryScore = score;
            return;
        }

        if (score > secondaryScore)
        {
            secondaryStyle = style;
            secondaryScore = score;
        }
    }

    private static double GetBestSkill(Mobile player, SkillName a, SkillName b, SkillName c, SkillName d)
    {
        var best = GetSkill(player, a);
        var value = GetSkill(player, b);

        if (value > best)
        {
            best = value;
        }

        value = GetSkill(player, c);

        if (value > best)
        {
            best = value;
        }

        value = GetSkill(player, d);

        if (value > best)
        {
            best = value;
        }

        return best;
    }

    private static double GetSkill(Mobile player, SkillName skillName)
    {
        if (player?.Skills == null)
        {
            return 0.0;
        }

        return player.Skills[skillName].Value;
    }

    private static double Max(double a, double b, double c)
    {
        var max = a;

        if (b > max)
        {
            max = b;
        }

        if (c > max)
        {
            max = c;
        }

        return max;
    }
}
