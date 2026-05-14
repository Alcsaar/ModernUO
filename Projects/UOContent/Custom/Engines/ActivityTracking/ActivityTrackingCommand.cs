using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Mobiles;
using Server.Text;

namespace Server.Custom.Engines.ActivityTracking;

public static class ActivityTrackingCommand
{
    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: register admin command aliases during Configure */
    public static void Configure()
    {
        CommandSystem.Register("ActivityTracking", AccessLevel.Administrator, ActivityTrackingHandler);
        CommandSystem.Register("AT", AccessLevel.Administrator, ActivityTrackingHandler);
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    [Usage("ActivityTracking <command> [args]")]
    [Description("Admin command for activity tracking hooks. Commands: debug, recent [count], clear, reset, staff|stafftoggle, status")]
    public static void ActivityTrackingHandler(CommandEventArgs e)
    {
        if (e.Arguments.Length < 1)
        {
            e.Mobile.SendMessage("Usage: ActivityTracking <debug|recent [count]|clear|reset|staff|stafftoggle|status>");
            return;
        }

        var command = e.Arguments[0].ToLower();

        switch (command)
        {
            case "debug":
                ToggleDebug(e);
                break;
            case "recent":
                ViewRecentKills(e);
                break;
            case "clear":
                ClearRecentKills(e);
                break;
            case "reset":
                ResetTracking(e);
                break;
            case "stafftoggle":
            case "staff":
                ToggleStaffTracking(e);
                break;
            case "status":
                ShowStatus(e);
                break;
            default:
                e.Mobile.SendMessage($"Unknown command: {command}");
                break;
        }
    }

    private static void ToggleDebug(CommandEventArgs e)
    {
        ActivityTrackingService.ToggleDebug();
        e.Mobile.SendMessage($"Activity tracking debug is now {(ActivityTrackingService.DebugEnabled ? "ENABLED" : "DISABLED")}.");
    }

    private static void ViewRecentKills(CommandEventArgs e)
    {
        var count = 10;

        if (e.Arguments.Length > 1 && int.TryParse(e.Arguments[1], out var parsedCount))
        {
            count = Math.Max(1, parsedCount);
        }

        var recent = ActivityTrackingService.RecentCreatureKills;

        if (recent.Count == 0)
        {
            e.Mobile.SendMessage("No creature kills have been recorded yet.");
            return;
        }

        e.Mobile.SendMessage($"Showing {Math.Min(count, recent.Count)} of {recent.Count} recent creature kills:");

        var shown = 0;

        for (var i = recent.Count - 1; i >= 0 && shown < count; i--, shown++)
        {
            var record = recent[i];
            var participantSummary = BuildParticipantSummary(record.Participants);
            var primary = FindPrimaryParticipant(record);
            var primarySummary = primary != null ? $"{primary.ParticipantName}/{primary.AccountName} dmg={primary.Damage}" : "None";

            e.Mobile.SendMessage($"{record.TimestampUtc:HH:mm:ss} {record.CreatureType} '{record.CreatureName}' @ {record.RegionName}/{record.Map} ({record.Location.X},{record.Location.Y},{record.Location.Z}) - Primary: {primarySummary}; Participants: {participantSummary}");

            for (var j = 0; j < record.Participants.Count; j++)
            {
                var participant = record.Participants[j];
                var credit = participant.IsBardProvocationCredit ? " bardCredit=True" : string.Empty;
                e.Mobile.SendMessage($"  {participant.ParticipantName}/{participant.AccountName} {participant.ParticipantType} dmg={participant.Damage} lootRight={participant.HasLootRight}{credit}");
            }
        }
    }

    private static ActivityTrackingService.CreatureKillParticipant FindPrimaryParticipant(ActivityTrackingService.CreatureKillRecord record)
    {
        for (var i = 0; i < record.Participants.Count; i++)
        {
            var participant = record.Participants[i];

            if (participant.ParticipantSerial == record.PrimaryParticipantSerial)
            {
                return participant;
            }
        }

        return null;
    }

    private static string BuildParticipantSummary(IReadOnlyList<ActivityTrackingService.CreatureKillParticipant> participants)
    {
        using var builder = ValueStringBuilder.Create();

        for (var i = 0; i < participants.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var participant = participants[i];
            builder.Append(participant.ParticipantName);
            builder.Append('/');
            builder.Append(participant.AccountName);
        }

        return builder.ToString();
    }

    private static void ClearRecentKills(CommandEventArgs e)
    {
        ActivityTrackingService.ClearRecentKills();
        e.Mobile.SendMessage("Recent activity tracking kill history has been cleared.");
    }

    private static void ResetTracking(CommandEventArgs e)
    {
        ActivityTrackingService.ResetData();
        e.Mobile.SendMessage("All activity tracking runtime data has been reset.");
    }

    private static void ToggleStaffTracking(CommandEventArgs e)
    {
        ActivityTrackingService.IncludeStaffMembers = !ActivityTrackingService.IncludeStaffMembers;
        e.Mobile.SendMessage($"Staff member tracking is now {(ActivityTrackingService.IncludeStaffMembers ? "ENABLED" : "DISABLED")}.");
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: status output includes tracked player/region counts and estimated memory usage */
    private static void ShowStatus(CommandEventArgs e)
    {
        var memoryKb = ActivityTrackingService.GetEstimatedMemoryUsage() / 1024;
        e.Mobile.SendMessage($"Debug: {(ActivityTrackingService.DebugEnabled ? "ON" : "OFF")}, Staff tracking: {(ActivityTrackingService.IncludeStaffMembers ? "ENABLED" : "DISABLED")}, Recent kills: {ActivityTrackingService.RecentKillCount}, Players tracked: {ActivityTrackingService.PlayerCount}, Regions recorded: {ActivityTrackingService.RegionCount}, Est. memory: {memoryKb} KB.");
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */
}
