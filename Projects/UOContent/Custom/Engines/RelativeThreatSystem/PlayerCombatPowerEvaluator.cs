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

    public static PlayerCombatPowerResult EvaluateTemplate(RelativeThreatPlayerTemplate template)
    {
        var primaryScore = template.Style switch
        {
            RelativeThreatPlayerStyle.Warrior => EvaluateMeleeTemplate(template.SkillValue),
            RelativeThreatPlayerStyle.Archer => EvaluateArcherTemplate(template.SkillValue),
            RelativeThreatPlayerStyle.Mage => EvaluateMageTemplate(template.SkillValue),
            RelativeThreatPlayerStyle.Tamer => EvaluateTamerTemplate(template.SkillValue),
            RelativeThreatPlayerStyle.BardMage => EvaluateBardTemplate(template.SkillValue) + (EvaluateMageTemplate(template.SkillValue) * 0.25),
            RelativeThreatPlayerStyle.BardDexxer => EvaluateBardTemplate(template.SkillValue) + (EvaluateMeleeTemplate(template.SkillValue) * 0.25),
            RelativeThreatPlayerStyle.Paladin => EvaluateMeleeTemplate(template.SkillValue) + (template.SkillValue * 0.20),
            _ => 0.0
        };

        var secondaryScore = template.Style switch
        {
            RelativeThreatPlayerStyle.BardMage => EvaluateMageTemplate(template.SkillValue),
            RelativeThreatPlayerStyle.BardDexxer => EvaluateMeleeTemplate(template.SkillValue),
            RelativeThreatPlayerStyle.Paladin => template.SkillValue * 0.80,
            _ => 0.0
        };

        var healingBonus = template.HasHealing ? GetHealingBonus(template.SkillValue) : 0.0;
        var resistBonus = template.HasMagicResist ? GetMagicResistBonus(template.SkillValue) : 0.0;
        var baseScore = primaryScore;

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
            template.Style is RelativeThreatPlayerStyle.Warrior or RelativeThreatPlayerStyle.BardDexxer or RelativeThreatPlayerStyle.Paladin
                ? EvaluateMeleeTemplate(template.SkillValue)
                : 0.0,
            template.Style == RelativeThreatPlayerStyle.Archer ? EvaluateArcherTemplate(template.SkillValue) : 0.0,
            template.Style is RelativeThreatPlayerStyle.Mage or RelativeThreatPlayerStyle.BardMage
                ? EvaluateMageTemplate(template.SkillValue)
                : 0.0,
            template.Style == RelativeThreatPlayerStyle.Tamer ? EvaluateTamerTemplate(template.SkillValue) : 0.0,
            template.Style is RelativeThreatPlayerStyle.BardMage or RelativeThreatPlayerStyle.BardDexxer
                ? EvaluateBardTemplate(template.SkillValue)
                : 0.0,
            healingBonus,
            resistBonus,
            template.StyleName,
            secondaryScore > 0.0 ? template.SecondaryStyleName : "None"
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
            (weaponSkill * 0.65) +
            (tactics * 0.45) +
            (anatomy * 0.25) +
            (player.Str * 0.10) +
            (player.Dex * 0.03);

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
            (archery * 0.70) +
            (tactics * 0.43) +
            (anatomy * 0.20) +
            (player.Dex * 0.11) +
            (player.Str * 0.03);

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
            (magery * 0.65) +
            (evalInt * 0.65) +
            (meditation * 0.20) +
            (player.Int * 0.12) +
            (player.ManaMax * 0.04);

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

        var petMultiplier = HasActivePet(player) ? 1.0 : 0.25;

        var score =
            ((taming * 0.55) +
            (lore * 0.50) +
            (vet * 0.20) +
            (player.Int * 0.04)) * petMultiplier;

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
            (music * 0.35) +
            (provo * 0.42) +
            (disco * 0.38) +
            (peace * 0.28) +
            (player.Int * 0.03);

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

        return GetHealingBonus(combined);
    }

    private static double EvaluateMagicResistBonus(Mobile player)
    {
        var resist = GetSkill(player, SkillName.MagicResist);

        if (resist < MinimumSupportSkill)
        {
            return 0.0;
        }

        return GetMagicResistBonus(resist);
    }

    private static double EvaluateMeleeTemplate(double skillValue)
    {
        return (skillValue * 0.65) + (skillValue * 0.45) + (skillValue * 0.25) + (skillValue * 0.10) + (skillValue * 0.03);
    }

    private static double EvaluateArcherTemplate(double skillValue)
    {
        return (skillValue * 0.70) + (skillValue * 0.43) + (skillValue * 0.20) + (skillValue * 0.11) + (skillValue * 0.03);
    }

    private static double EvaluateMageTemplate(double skillValue)
    {
        return (skillValue * 0.65) + (skillValue * 0.65) + (skillValue * 0.20) + (skillValue * 0.12) + (skillValue * 0.04);
    }

    private static double EvaluateTamerTemplate(double skillValue)
    {
        return (skillValue * 0.55) + (skillValue * 0.50) + (skillValue * 0.20) + (skillValue * 0.04);
    }

    private static double EvaluateBardTemplate(double skillValue)
    {
        return (skillValue * 0.35) + (skillValue * 0.42) + (skillValue * 0.38) + (skillValue * 0.28) + (skillValue * 0.03);
    }

    private static double GetHealingBonus(double value)
    {
        if (value >= 100.0)
        {
            return 0.16;
        }

        if (value >= 80.0)
        {
            return 0.11;
        }

        if (value >= 60.0)
        {
            return 0.06;
        }

        return 0.03;
    }

    private static double GetMagicResistBonus(double value)
    {
        if (value >= 100.0)
        {
            return 0.14;
        }

        if (value >= 80.0)
        {
            return 0.09;
        }

        if (value >= 60.0)
        {
            return 0.05;
        }

        return 0.02;
    }

    private static bool HasActivePet(Mobile player)
    {
        if (player is not PlayerMobile pm || pm.AllFollowers == null)
        {
            return false;
        }

        foreach (var follower in pm.AllFollowers)
        {
            if (follower is BaseCreature pet &&
                !pet.Deleted &&
                pet.Alive &&
                pet.Controlled &&
                pet.ControlMaster == player &&
                pet.Map != null &&
                pet.Map != Map.Internal)
            {
                return true;
            }
        }

        return false;
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

public enum RelativeThreatPlayerStyle
{
    Warrior,
    Archer,
    Mage,
    Tamer,
    BardMage,
    BardDexxer,
    Paladin
}

public readonly struct RelativeThreatPlayerTemplate
{
    public RelativeThreatPlayerTemplate(
        RelativeThreatPlayerStyle style,
        double skillValue,
        bool hasHealing,
        bool hasMagicResist
    )
    {
        Style = style;
        SkillValue = skillValue;
        HasHealing = hasHealing;
        HasMagicResist = hasMagicResist;
    }

    public RelativeThreatPlayerStyle Style { get; }

    public double SkillValue { get; }

    public bool HasHealing { get; }

    public bool HasMagicResist { get; }

    public string StyleName => Style switch
    {
        RelativeThreatPlayerStyle.BardMage => "Bard Mage",
        RelativeThreatPlayerStyle.BardDexxer => "Bard Dexxer",
        _ => Style.ToString()
    };

    public string SecondaryStyleName => Style switch
    {
        RelativeThreatPlayerStyle.BardMage => "Mage",
        RelativeThreatPlayerStyle.BardDexxer => "Melee",
        RelativeThreatPlayerStyle.Paladin => "Chivalry",
        _ => "None"
    };
}
