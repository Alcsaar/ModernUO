using System;
using Server.Custom.Engines.ActivityTracking;
using Server.Mobiles;

namespace Server.Custom.Systems.MissionSystem;

public static class MissionProgressService
{
    public static void Configure()
    {
        ActivityTrackingService.CreatureKillRecorded += OnActivityTrackingCreatureKillRecorded;
    }

    private static void OnActivityTrackingCreatureKillRecorded(ActivityTrackingService.CreatureKillRecord record)
    {
        if (record?.Participants == null)
        {
            return;
        }

        for (var i = 0; i < record.Participants.Count; i++)
        {
            var participant = record.Participants[i];

            if (!participant.HasLootRight || !participant.IsPlayer || participant.PlayerMobile == null)
            {
                continue;
            }

            var credit = new MissionKillCredit
            {
                Player = participant.PlayerMobile,
                CreatureType = AssemblyHandler.FindTypeByName(record.CreatureType) ?? AssemblyHandler.FindTypeByName(record.CreatureType, true),
                RegionName = record.RegionName ?? string.Empty
            };

            MissionSystemService.RecordKillCredit(credit);
        }
    }

    /* BEGIN MISSION SYSTEM KILL INTEGRATION STUB: direct creature death hook for future participant-credit expansion */
    public static void RecordCreatureKillFromDeathRights(BaseCreature creature, System.Collections.Generic.List<DamageStore> rights)
    {
        // ActivityTracking currently provides the clean participant-aware kill event used above.
        // If ActivityTracking is removed or disabled later, move direct BaseCreature death-rights integration here.
    }
    /* END MISSION SYSTEM KILL INTEGRATION STUB */
}
