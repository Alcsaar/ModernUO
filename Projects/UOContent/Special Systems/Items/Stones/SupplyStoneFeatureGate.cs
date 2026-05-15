using Server.Custom.Systems.CustomFeatureFlags;

namespace Server.Items;

public static class SupplyStoneFeatureGate
{
    /* BEGIN SUPPLY STONE FEATURE FLAG GUARD: centralizes player access checks for resource-generating supply stones. */
    public static bool CanUse(Mobile from)
    {
        if (from == null)
        {
            return false;
        }

        if (from.AccessLevel >= AccessLevel.GameMaster)
        {
            if (!CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.SupplyStones))
            {
                from.SendMessage(0x35, "Supply stones are disabled for players; this worked because you are staff.");
            }

            return true;
        }

        if (CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.SupplyStones))
        {
            return true;
        }

        from.SendMessage(0x22, "Supply stones are currently disabled.");
        return false;
    }
    /* END SUPPLY STONE FEATURE FLAG GUARD */
}
