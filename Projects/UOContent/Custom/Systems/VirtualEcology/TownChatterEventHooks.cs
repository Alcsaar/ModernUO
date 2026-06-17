using ModernUO.CodeGeneratedEvents;
using Server.Mobiles;

namespace Server.Custom.Systems.VirtualEcology;

public static class TownChatterEventHooks
{
    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeathEvent(PlayerMobile player)
    {
        TownChatterService.RecordPlayerDeath(player);
    }
}
