using System;
using System.Collections.Generic;
using Server.Mobiles;

namespace Server.Custom.Engines.ActivityTracking;

public static class ActivityTrackingSkills
{
    public static void RecordSkillMilestone(PlayerMobile player, SkillName skill, double oldBase)
    {
        if (player == null || !ActivityTrackingService.ShouldTrackPlayer(player))
        {
            return;
        }

        var newBase = player.Skills[skill].Base;

        if (newBase <= oldBase)
        {
            return;
        }

        var milestones = new[] { 60.0, 70.0, 80.0, 90.0, 100.0, 110.0, 120.0 };
        var key = skill.ToString();
        var data = ActivityTrackingService.GetOrCreatePlayerData(player);
        var recorded = false;

        if (!data.SkillMilestones.TryGetValue(key, out var milestoneList))
        {
            milestoneList = new List<SkillMilestoneRecord>();
            data.SkillMilestones[key] = milestoneList;
        }

        foreach (var milestone in milestones)
        {
            if (oldBase < milestone && newBase >= milestone)
            {
                var milestoneValue = (int)milestone;
                var existing = false;

                for (var i = 0; i < milestoneList.Count; i++)
                {
                    if (milestoneList[i].MilestoneLevel == milestoneValue)
                    {
                        existing = true;
                        break;
                    }
                }

                if (!existing)
                {
                    milestoneList.Add(new SkillMilestoneRecord { MilestoneLevel = milestoneValue, ReachedUtc = DateTime.UtcNow });
                    ActivityTrackingService.AddPlayerActivity(data, $"{DateTime.UtcNow:O}: Reached {milestoneValue} on {key}");
                    recorded = true;

                    if (ActivityTrackingService.DebugEnabled)
                    {
                        ActivityTrackingService.WriteSkillDebug(player, skill, milestoneValue);
                    }
                }
            }
        }

        if (recorded)
        {
            data.LastUpdatedUtc = DateTime.UtcNow;
            ActivityTrackingService.SavePlayerData();
        }
    }
}

