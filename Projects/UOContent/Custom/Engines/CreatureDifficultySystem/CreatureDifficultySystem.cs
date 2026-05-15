using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Custom.Engines.CreatureDifficultySystem;

public static class CreatureDifficultySystem
{
    public static void Configure()
    {
        CreatureDifficultyCommands.Configure();
    }
}

public static class CreatureDifficultyService
{
    private static readonly Dictionary<Type, CreatureDifficultyResult> _cache = new();
    private static readonly Dictionary<Type, CreatureDifficultyResult> _overrides = new();

    public static CreatureDifficultyResult GetDifficulty(BaseCreature creature)
    {
        if (creature == null)
        {
            return CreatureDifficultyResult.Empty;
        }

        var type = creature.GetType();

        if (_overrides.TryGetValue(type, out var overrideResult))
        {
            return overrideResult;
        }

        if (_cache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var result = CreatureDifficultyEvaluator.Evaluate(creature);
        _cache[type] = result;
        return result;
    }

    public static CreatureDifficultyResult EvaluateCurrent(BaseCreature creature)
    {
        if (creature == null)
        {
            return CreatureDifficultyResult.Empty;
        }

        var type = creature.GetType();

        if (_overrides.TryGetValue(type, out var overrideResult))
        {
            return overrideResult;
        }

        return CreatureDifficultyEvaluator.Evaluate(creature);
    }

    public static void ClearCache()
    {
        _cache.Clear();
    }

    public static void ClearAll()
    {
        _cache.Clear();
        _overrides.Clear();
    }

    public static void SetOverride(Type creatureType, CreatureDifficultyResult result)
    {
        if (creatureType == null)
        {
            return;
        }

        result.HasOverride = true;
        _overrides[creatureType] = result;
        _cache.Remove(creatureType);
    }

    public static bool RemoveOverride(Type creatureType)
    {
        if (creatureType == null)
        {
            return false;
        }

        return _overrides.Remove(creatureType);
    }
}

public static class CreatureDifficultyEvaluator
{
    private const double DamageWeight = 2.2;
    private const double WrestlingWeight = 0.25;
    private const double TacticsWeight = 0.25;
    private const double DexWeight = 0.02;

    private const double HitsWeight = 0.05;
    private const double VirtualArmorWeight = 0.45;
    private const double MagicResistWeight = 0.20;

    private const double MageryWeight = 0.40;
    private const double EvalIntWeight = 0.40;

    public static CreatureDifficultyResult Evaluate(BaseCreature creature)
    {
        if (creature == null)
        {
            return CreatureDifficultyResult.Empty;
        }

        var offenseScore = EvaluateOffense(creature);
        var defenseScore = EvaluateDefense(creature);
        var magicScore = EvaluateMagic(creature);
        var specialScore = CreatureDifficultySpecialEvaluator.Evaluate(creature);

        var totalScore = offenseScore + defenseScore + magicScore + specialScore;
        var threatScore = offenseScore + magicScore + specialScore + (defenseScore * 0.35);
        var tier = CreatureDifficultyMapper.MapToTier(totalScore);

        return new CreatureDifficultyResult
        {
            Score = totalScore,
            Tier = tier,
            OffenseScore = offenseScore,
            DefenseScore = defenseScore,
            MagicScore = magicScore,
            SpecialScore = specialScore,
            ThreatScore = RoundScore(threatScore),
            DurabilityScore = defenseScore,
            HasOverride = false
        };
    }

    private static double EvaluateOffense(BaseCreature creature)
    {
        var damageMin = ClampAtLeastZero(creature.DamageMin);
        var damageMax = ClampAtLeastZero(creature.DamageMax);
        var averageDamage = (damageMin + damageMax) * 0.5;

        var wrestling = GetBaseSkill(creature, SkillName.Wrestling);
        var tactics = GetBaseSkill(creature, SkillName.Tactics);
        var dex = ClampAtLeastZero(creature.RawDex);

        var score = 0.0;
        score += averageDamage * DamageWeight;
        score += wrestling * WrestlingWeight;
        score += tactics * TacticsWeight;
        score += dex * DexWeight;

        return RoundScore(score);
    }

    private static double EvaluateDefense(BaseCreature creature)
    {
        var maxHits = GetConfiguredMaxHits(creature);
        var virtualArmor = ClampAtLeastZero(creature.VirtualArmor);
        var magicResist = GetBaseSkill(creature, SkillName.MagicResist);

        var score = 0.0;
        score += maxHits * HitsWeight;
        score += virtualArmor * VirtualArmorWeight;
        score += magicResist * MagicResistWeight;

        return RoundScore(score);
    }

    private static double EvaluateMagic(BaseCreature creature)
    {
        var magery = GetBaseSkill(creature, SkillName.Magery);
        var evalInt = GetBaseSkill(creature, SkillName.EvalInt);

        var score = 0.0;
        score += magery * MageryWeight;
        score += evalInt * EvalIntWeight;

        return RoundScore(score);
    }

    private static double GetBaseSkill(BaseCreature creature, SkillName skillName)
    {
        var skill = creature.Skills[skillName];
        return skill?.Base ?? 0.0;
    }

    private static int GetConfiguredMaxHits(BaseCreature creature)
    {
        if (creature.HitsMax > 0)
        {
            return creature.HitsMax;
        }

        return ClampAtLeastZero(creature.Hits);
    }

    private static int ClampAtLeastZero(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        return value;
    }

    private static double RoundScore(double value)
    {
        return Math.Round(value, 2);
    }
}

public static class CreatureDifficultySpecialEvaluator
{
    public static double Evaluate(BaseCreature creature)
    {
        if (creature == null)
        {
            return 0.0;
        }

        var score = 0.0;

        if (creature.AutoDispel)
        {
            score += CreatureDifficultyAbilityMapper.AutoDispelScore;
        }

        if (creature.BardImmune)
        {
            score += CreatureDifficultyAbilityMapper.BardImmuneScore;
        }

        var abilities = creature.GetMonsterAbilities();

        if (abilities != null)
        {
            for (var i = 0; i < abilities.Length; i++)
            {
                score += CreatureDifficultyAbilityMapper.GetScore(abilities[i]);
            }
        }

        return Math.Round(score, 2);
    }
}

public static class CreatureDifficultyAbilityMapper
{
    public const double AutoDispelScore = 15.0;
    public const double BardImmuneScore = 10.0;

    public static double GetScore(MonsterAbility ability)
    {
        if (ability == null)
        {
            return 0.0;
        }

        return ability switch
        {
            ChaosBreath => 22.0,
            ColdBreath => 18.0,
            FireBreath => 20.0,

            GraspingClaw => 10.0,
            RuneCorruption => 12.0,
            FanningFire => 10.0,

            SummonSkeletonsCounter => 16.0,
            SummonLesserUndeadCounter => 14.0,
            SummonPixiesCounter => 14.0,

            ColossalBlow => 14.0,

            PoisonGasCounter => 15.0,
            PoisonGasAreaAttack => 18.0,

            DeathExplosion => 20.0,

            ThrowHatchetCounter => 8.0,
            EnergyBoltCounter => 10.0,
            FanThrowCounter => 10.0,

            DestroyEquipment => 8.0,

            DrainLifeAreaAttack => 18.0,
            DrainLifeAttack => 14.0,

            MagicalBarrier => 12.0,
            ReflectPhysicalDamage => 10.0,
            BloodBathAttack => 14.0,

            _ => 0.0
        };
    }
}

public static class CreatureDifficultyMapper
{
    public static int MapToTier(double score)
    {
        if (score < 35.0)
        {
            return 0;
        }

        if (score < 70.0)
        {
            return 1;
        }

        if (score < 115.0)
        {
            return 2;
        }

        if (score < 170.0)
        {
            return 3;
        }

        if (score < 250.0)
        {
            return 4;
        }

        if (score < 380.0)
        {
            return 5;
        }

        return 6;
    }
}

public struct CreatureDifficultyResult
{
    public static readonly CreatureDifficultyResult Empty = new()
    {
        Score = 0.0,
        Tier = 0,
        OffenseScore = 0.0,
        DefenseScore = 0.0,
        MagicScore = 0.0,
        SpecialScore = 0.0,
        ThreatScore = 0.0,
        DurabilityScore = 0.0,
        HasOverride = false
    };

    public double Score { get; set; }
    public int Tier { get; set; }
    public double OffenseScore { get; set; }
    public double DefenseScore { get; set; }
    public double MagicScore { get; set; }
    public double SpecialScore { get; set; }
    public double ThreatScore { get; set; }
    public double DurabilityScore { get; set; }
    public bool HasOverride { get; set; }
}

public static class CreatureDifficultyCommands
{
    public static void Configure()
    {
        CommandSystem.Register("CreatureDifficulty", AccessLevel.GameMaster, CreatureDifficulty_OnCommand);
        CommandSystem.Register("CreatureDifficultyClearCache", AccessLevel.GameMaster, CreatureDifficultyClearCache_OnCommand);
        CommandSystem.Register("CreatureDifficultyClearAll", AccessLevel.GameMaster, CreatureDifficultyClearAll_OnCommand);
    }

    [Usage("CreatureDifficulty")]
    [Description("Targets a creature and displays its cached difficulty evaluation.")]
    public static void CreatureDifficulty_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target a creature to inspect its difficulty score.");
        e.Mobile.Target = new CreatureDifficultyTarget();
    }

    [Usage("CreatureDifficultyClearCache")]
    [Description("Clears the cached creature difficulty results.")]
    public static void CreatureDifficultyClearCache_OnCommand(CommandEventArgs e)
    {
        CreatureDifficultyService.ClearCache();
        e.Mobile.SendMessage("Creature difficulty cache cleared.");
    }

    [Usage("CreatureDifficultyClearAll")]
    [Description("Clears the cached creature difficulty results and all overrides.")]
    public static void CreatureDifficultyClearAll_OnCommand(CommandEventArgs e)
    {
        CreatureDifficultyService.ClearAll();
        e.Mobile.SendMessage("Creature difficulty cache and overrides cleared.");
    }

    private sealed class CreatureDifficultyTarget : Target
    {
        public CreatureDifficultyTarget() : base(-1, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not BaseCreature creature)
            {
                from.SendMessage("That is not a creature.");
                return;
            }

            var result = CreatureDifficultyService.GetDifficulty(creature);
            var abilities = creature.GetMonsterAbilities();
            var abilityCount = abilities?.Length ?? 0;

            from.SendMessage($"Creature: {creature.GetType().Name}");
            from.SendMessage($"Score: {result.Score:F2}");
            from.SendMessage($"Tier: {result.Tier}");
            from.SendMessage($"Offense: {result.OffenseScore:F2}");
            from.SendMessage($"Defense: {result.DefenseScore:F2}");
            from.SendMessage($"Magic: {result.MagicScore:F2}");
            from.SendMessage($"Special: {result.SpecialScore:F2}");
            from.SendMessage($"Threat: {result.ThreatScore:F2}");
            from.SendMessage($"Durability: {result.DurabilityScore:F2}");
            from.SendMessage($"Abilities: {abilityCount}");
            from.SendMessage($"Override: {(result.HasOverride ? "Yes" : "No")}");
        }
    }
}
