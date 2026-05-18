using System;
using Server.Custom.Engines.CreatureDifficultySystem;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom.Systems.MissionSystem;

public sealed class MissionReward
{
    public int Gold { get; set; }

    public void GrantTo(PlayerMobile player)
    {
        if (player == null || Gold <= 0)
        {
            return;
        }

        var bank = player.BankBox;

        if (bank == null)
        {
            return;
        }

        var remaining = Gold;

        while (remaining > 1000000)
        {
            bank.DropItem(new BankCheck(1000000));
            remaining -= 1000000;
        }

        bank.DropItem(new BankCheck(remaining));
    }
}

public static class MissionRewardCalculator
{
    private const int MinimumDailyGold = 125;
    private const int MinimumWeeklyGold = 250;
    private const int DailyEfficiencyBonusPercent = 15;
    private const int WeeklyEfficiencyBonusPercent = 12;

    public static int CalculateGold(MissionCadence cadence, MissionDifficulty difficulty, int requiredCount, int expectedGoldPerKill)
    {
        var efficiencyPercent = cadence == MissionCadence.WeeklyContract ? WeeklyEfficiencyBonusPercent : DailyEfficiencyBonusPercent;
        var difficultyMultiplier = difficulty switch
        {
            MissionDifficulty.Uncommon => 105,
            MissionDifficulty.Veteran => 110,
            MissionDifficulty.Elite => 115,
            _ => 100
        };
        var baseline = Math.Max(0, expectedGoldPerKill) * Math.Max(1, requiredCount);
        var rawGold = baseline * efficiencyPercent * difficultyMultiplier / 10000;
        var minimum = cadence == MissionCadence.WeeklyContract ? MinimumWeeklyGold : MinimumDailyGold;

        return Math.Max(minimum, RoundToNearest(rawGold, 25));
    }

    public static double EstimateCreatureScore(string typeName)
    {
        var type = AssemblyHandler.FindTypeByName(typeName) ?? AssemblyHandler.FindTypeByName(typeName, true);

        if (type == null || !typeof(BaseCreature).IsAssignableFrom(type))
        {
            return 0.0;
        }

        BaseCreature creature = null;

        try
        {
            creature = Activator.CreateInstance(type) as BaseCreature;

            if (creature == null)
            {
                return 0.0;
            }

            return CreatureDifficultyService.EvaluateCurrent(creature).Score;
        }
        catch
        {
            return 0.0;
        }
        finally
        {
            creature?.Delete();
        }
    }

    private static int RoundToNearest(int value, int step)
    {
        if (step <= 0)
        {
            return value;
        }

        return ((value + step / 2) / step) * step;
    }
}
