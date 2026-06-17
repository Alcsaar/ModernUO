using Server.Custom.Systems.CustomFeatureFlags;
using Server.Engines.Harvest;
using Server.Items;
using Server.Misc;

namespace Server.Custom.Systems.HarvestingAutomation;

public static class HarvestingAutomationService
{
    private const int WeightBuffer = 15;
    private const string NodeDepletedMessage = "The harvest node is out of resources.";
    private const string NearWeightLimitMessage = "You stop harvesting as you near your weight limit.";

    public static bool IsEnabled()
    {
        return CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.HarvestingAutomation);
    }

    public static void TryContinue(
        Mobile from,
        Item tool,
        HarvestSystem system,
        HarvestDefinition def,
        object toHarvest,
        Item harvestedItem,
        bool notifyDepleted
    )
    {
        if (!CanContinue(from, tool, system, def, toHarvest, harvestedItem, out var depleted, out var nearWeightLimit))
        {
            if (from?.Deleted == false && from.NetState != null)
            {
                if (notifyDepleted && depleted)
                {
                    from.SendMessage(NodeDepletedMessage);
                }
                else if (nearWeightLimit)
                {
                    from.SendMessage(NearWeightLimitMessage);
                }
            }

            return;
        }

        Timer.DelayCall(ContinueHarvesting, from, tool, system, def, toHarvest);
    }

    private static void ContinueHarvesting(
        Mobile from,
        Item tool,
        HarvestSystem system,
        HarvestDefinition def,
        object toHarvest
    )
    {
        if (!CanContinue(from, tool, system, def, toHarvest, null, out _, out _))
        {
            return;
        }

        system.StartHarvesting(from, tool, toHarvest);
    }

    private static bool CanContinue(
        Mobile from,
        Item tool,
        HarvestSystem system,
        HarvestDefinition def,
        object toHarvest,
        Item harvestedItem,
        out bool depleted,
        out bool nearWeightLimit
    )
    {
        depleted = false;
        nearWeightLimit = false;

        if (!IsEnabled() || system is not (Mining or Lumberjacking or Fishing))
        {
            return false;
        }

        if (from == null || from.Deleted || !from.Alive || from.NetState == null)
        {
            return false;
        }

        if (tool?.Deleted != false || (tool as IUsesRemaining)?.UsesRemaining <= 0)
        {
            return false;
        }

        if (ShouldStopForFishingEncounter(system, harvestedItem))
        {
            return false;
        }

        if (IsNearWeightLimit(from))
        {
            nearWeightLimit = true;
            return false;
        }

        if (def == null || !system.GetHarvestDetails(from, tool, toHarvest, out var tileID, out var map, out var loc, out var isLand))
        {
            return false;
        }

        if (!def.Validate(tileID, isLand) || from.Map != map || !from.InRange(loc, def.MaxRange))
        {
            return false;
        }

        var bank = def.GetBank(map, loc.X, loc.Y);
        depleted = bank?.Current < def.ConsumedPerHarvest;

        if (depleted)
        {
            return system is Fishing && HasNearbySos(from);
        }

        return true;
    }

    private static bool HasNearbySos(Mobile from)
    {
        var pack = from.Backpack;

        if (pack == null || from.Map != Map.Felucca && from.Map != Map.Trammel)
        {
            return false;
        }

        foreach (var sos in pack.FindItemsByType<SOS>())
        {
            if (from.InRange(sos.TargetLocation, 60))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldStopForFishingEncounter(HarvestSystem system, Item harvestedItem)
    {
        return system is Fishing && harvestedItem is TreasureMap or MessageInABottle or SpecialFishingNet;
    }

    private static bool IsNearWeightLimit(Mobile from)
    {
        var maxWeight = StaminaSystem.GetMaxWeight(from);

        if (maxWeight == int.MaxValue)
        {
            return false;
        }

        return Mobile.BodyWeight + from.TotalWeight >= maxWeight - WeightBuffer;
    }
}
