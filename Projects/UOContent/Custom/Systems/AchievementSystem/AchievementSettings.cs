namespace Server.Custom.Systems.AchievementSystem;

public static class AchievementSettings
{
    /*
     * ACHIEVEMENT MISSION TRACKING:
     * Set this to false when the MissionSystem is not included in the shard build.
     * The AchievementSystem does not reference MissionSystem types directly, and this
     * source-level switch hides mission achievements and blocks mission progress writes.
     */
    public const bool EnableMissionTracking = true;
}
