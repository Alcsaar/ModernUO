using System;
using Server.Custom.Engines.CreatureDifficultySystem;
using Server.Custom.Systems.CustomFeatureFlags;
using Server.Mobiles;

namespace Server.Engines.RelativeThreatSystem;

public static class RelativeThreatService
{
    public static CreatureThreatResult GetThreat(Mobile player, BaseCreature creature)
    {
        if (!CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.RelativeThreat))
        {
            return new CreatureThreatResult("Fair", 1.0, 1.0, 1.0);
        }

        if (player == null || player.Deleted || creature == null || creature.Deleted)
        {
            return new CreatureThreatResult("Fair", 1.0, 1.0, 1.0);
        }

        var playerPower = PlayerCombatPowerEvaluator.Evaluate(player);
        var playerScore = Math.Max(1.0, playerPower.PowerScore);
        var creatureScore = Math.Max(1.0, GetCreatureScore(creature));

        var ratio = creatureScore / playerScore;
        ratio *= GetMatchupMultiplier(player, creature, playerPower);

        var label = GetThreatLabel(ratio);

        return new CreatureThreatResult(label, ratio, creatureScore, playerScore);
    }

    private static string GetThreatLabel(double ratio)
    {
        if (ratio < 0.40)
        {
            return "Trivial";
        }

        if (ratio < 0.70)
        {
            return "Minor";
        }

        if (ratio < 1.00)
        {
            return "Challenging";
        }

        if (ratio < 1.30)
        {
            return "Dangerous";
        }

        if (ratio < 1.75)
        {
            return "Deadly";
        }

        return "Overwhelming";
    }

    private static double GetCreatureScore(BaseCreature creature)
    {
        var result = CreatureDifficultyService.GetDifficulty(creature);
        return result.Score < 1.0 ? 1.0 : result.Score;
    }

    private static double GetMatchupMultiplier(
        Mobile player,
        BaseCreature creature,
        PlayerCombatPowerResult playerPower
    )
    {
        var multiplier = 1.0;

        var casterTier = GetCasterThreatTier(creature);

        if (casterTier > 0)
        {
            multiplier *= GetCasterThreatMultiplier(player, playerPower, casterTier);
        }

        return multiplier;
    }

    private static double GetCasterThreatMultiplier(
        Mobile player,
        PlayerCombatPowerResult playerPower,
        int casterTier
    )
    {
        var magicResist = GetSkill(player, SkillName.MagicResist);
        var mageAbility = playerPower.MageScore;
        var bardAbility = playerPower.BardScore;
        var tamerAbility = playerPower.TamerScore;

        var multiplier = 1.0;

        switch (casterTier)
        {
            case 1:
                {
                    if (magicResist < 30.0)
                    {
                        multiplier += 0.10;
                    }
                    else if (magicResist < 60.0)
                    {
                        multiplier += 0.05;
                    }

                    if (mageAbility <= 0.0 && bardAbility <= 0.0 && tamerAbility <= 0.0)
                    {
                        multiplier += 0.05;
                    }

                    break;
                }
            case 2:
                {
                    if (magicResist < 30.0)
                    {
                        multiplier += 0.25;
                    }
                    else if (magicResist < 60.0)
                    {
                        multiplier += 0.15;
                    }
                    else if (magicResist < 80.0)
                    {
                        multiplier += 0.05;
                    }

                    if (mageAbility <= 0.0 && bardAbility <= 0.0 && tamerAbility <= 0.0)
                    {
                        multiplier += 0.10;
                    }

                    break;
                }
            case 3:
                {
                    if (magicResist < 30.0)
                    {
                        multiplier += 0.45;
                    }
                    else if (magicResist < 60.0)
                    {
                        multiplier += 0.25;
                    }
                    else if (magicResist < 80.0)
                    {
                        multiplier += 0.10;
                    }

                    if (mageAbility <= 0.0 && bardAbility <= 0.0 && tamerAbility <= 0.0)
                    {
                        multiplier += 0.20;
                    }
                    else if (mageAbility <= 0.0 && bardAbility <= 0.0)
                    {
                        multiplier += 0.10;
                    }

                    break;
                }
        }

        return multiplier;
    }

    private static int GetCasterThreatTier(BaseCreature creature)
    {
        var magery = creature.Skills[SkillName.Magery].Value;
        var evalInt = creature.Skills[SkillName.EvalInt].Value;
        var meditation = creature.Skills[SkillName.Meditation].Value;

        var casterScore = magery;

        if (evalInt > casterScore)
        {
            casterScore = evalInt;
        }

        if (meditation > casterScore)
        {
            casterScore = meditation;
        }

        if (casterScore >= 90.0)
        {
            return 3;
        }

        if (casterScore >= 70.0)
        {
            return 2;
        }

        if (casterScore >= 40.0 || creature.AI == AIType.AI_Mage)
        {
            return 1;
        }

        return 0;
    }

    private static double GetSkill(Mobile player, SkillName skillName)
    {
        if (player?.Skills == null)
        {
            return 0.0;
        }

        return player.Skills[skillName].Value;
    }

    public static string GetThreatLabelOnly(Mobile player, BaseCreature creature)
    {
        if (!CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.RelativeThreat))
        {
            return "Fair";
        }

        return GetThreat(player, creature).ThreatLabel;
    }

    public static int GetThreatHue(string threatLabel)
    {
        return threatLabel switch
        {
            "Trivial" => 954,
            "Minor" => 916,
            "Challenging" => 68,
            "Dangerous" => 93,
            "Deadly" => 38,
            "Overwhelming" => 32,
            _ => 916
        };
    }
}
