using System;
using Server.Mobiles;
using Server.Regions;

namespace Server.Custom.Systems.MissionSystem;

public enum MissionObjectiveKind
{
    KillCreature,
    KillCreatureFamily,
    KillRegion
}

public abstract class MissionObjective
{
    public abstract MissionObjectiveKind Kind { get; }
    public abstract int RequiredCount { get; set; }
    public abstract string Summary { get; }
    public abstract bool Matches(MissionKillCredit credit);
}

public sealed class KillCreatureObjective : MissionObjective
{
    public string CreatureTypeName { get; set; } = string.Empty;
    public override int RequiredCount { get; set; }
    public override MissionObjectiveKind Kind => MissionObjectiveKind.KillCreature;
    public override string Summary => $"Defeat {RequiredCount} {CreatureTypeName}";

    public override bool Matches(MissionKillCredit credit)
    {
        if (credit?.CreatureType == null || string.IsNullOrWhiteSpace(CreatureTypeName))
        {
            return false;
        }

        return string.Equals(credit.CreatureType.Name, CreatureTypeName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(credit.CreatureType.FullName, CreatureTypeName, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class KillCreatureFamilyObjective : MissionObjective
{
    public string FamilyName { get; set; } = string.Empty;
    public string BaseTypeName { get; set; } = string.Empty;
    public string CreatureTypeNames { get; set; } = string.Empty;
    public override int RequiredCount { get; set; }
    public override MissionObjectiveKind Kind => MissionObjectiveKind.KillCreatureFamily;
    public override string Summary => $"Defeat {RequiredCount} {FamilyName}";

    public override bool Matches(MissionKillCredit credit)
    {
        if (credit?.CreatureType == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(BaseTypeName))
        {
            var baseType = AssemblyHandler.FindTypeByName(BaseTypeName) ?? AssemblyHandler.FindTypeByName(BaseTypeName, true);

            if (baseType != null && baseType.IsAssignableFrom(credit.CreatureType))
            {
                return true;
            }
        }

        return MatchesTypeList(credit.CreatureType, CreatureTypeNames);
    }

    private static bool MatchesTypeList(Type creatureType, string typeNames)
    {
        if (creatureType == null || string.IsNullOrWhiteSpace(typeNames))
        {
            return false;
        }

        var names = typeNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < names.Length; i++)
        {
            if (string.Equals(creatureType.Name, names[i], StringComparison.OrdinalIgnoreCase) ||
                string.Equals(creatureType.FullName, names[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class KillRegionObjective : MissionObjective
{
    public string RegionName { get; set; } = string.Empty;
    public string RegionTypeName { get; set; } = string.Empty;
    public override int RequiredCount { get; set; }
    public override MissionObjectiveKind Kind => MissionObjectiveKind.KillRegion;
    public override string Summary => $"Defeat {RequiredCount} monsters in {RegionName}";

    public override bool Matches(MissionKillCredit credit)
    {
        if (credit == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(RegionName) &&
            string.Equals(credit.RegionName, RegionName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(RegionTypeName) && credit.RegionType != null)
        {
            return string.Equals(credit.RegionType.Name, RegionTypeName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(credit.RegionType.FullName, RegionTypeName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

public sealed class MissionKillCredit
{
    public PlayerMobile Player { get; set; }
    public BaseCreature Creature { get; set; }
    public Type CreatureType { get; set; }
    public Region Region { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public Type RegionType { get; set; }
    public bool StaffTestCredit { get; set; }
}
