using System;
using System.Collections.Generic;
using Server.Commands;

namespace Server.Custom.Systems.Townships;

public static class TownshipCommands
{
    private static readonly string[] TestNames =
    [
        "Alys Fairbank",
        "Corvin Reed",
        "Mira Stone",
        "Talen Brook",
        "Serah Goldwynd",
        "Borin Ash",
        "Nessa Vale",
        "Edrin Hale"
    ];

    private static readonly string[] TestDepositReasons =
    [
        "market stall proceeds",
        "citizen donation",
        "escort tithe",
        "trade route settlement",
        "vendor license payment",
        "festival contribution",
        "crafting hall dues",
        "guild patrol reward"
    ];

    private static readonly string[] TestWithdrawalReasons =
    [
        "vendor wage reserve",
        "road marker replacement",
        "guard post supplies",
        "charter filing fee",
        "public works stipend",
        "stable service advance",
        "courier contract payment",
        "maintenance allocation"
    ];

    public static void Configure()
    {
        CommandSystem.Register("Township", AccessLevel.Player, Township_OnCommand);
        CommandSystem.Register("Town", AccessLevel.Player, Township_OnCommand);
        CommandSystem.Register("TS", AccessLevel.Player, Township_OnCommand);
        CommandSystem.Register("TownshipAdmin", AccessLevel.GameMaster, TownshipAdmin_OnCommand);
        CommandSystem.Register("TSAdmin", AccessLevel.GameMaster, TownshipAdmin_OnCommand);
        CommandSystem.Register("TownshipDeed", AccessLevel.GameMaster, TownshipDeed_OnCommand);
        CommandSystem.Register("TSDeed", AccessLevel.GameMaster, TownshipDeed_OnCommand);
        CommandSystem.Register("TownshipInfo", AccessLevel.GameMaster, TownshipInfo_OnCommand);
        CommandSystem.Register("TSInfo", AccessLevel.GameMaster, TownshipInfo_OnCommand);
        CommandSystem.Register("TownshipTestActivity", AccessLevel.GameMaster, TownshipTestActivity_OnCommand);
        CommandSystem.Register("TSTestActivity", AccessLevel.GameMaster, TownshipTestActivity_OnCommand);
        CommandSystem.Register("TownshipTestTreasury", AccessLevel.GameMaster, TownshipTestTreasury_OnCommand);
        CommandSystem.Register("TSTestTreasury", AccessLevel.GameMaster, TownshipTestTreasury_OnCommand);
        CommandSystem.Register("TownshipTestService", AccessLevel.GameMaster, TownshipTestService_OnCommand);
        CommandSystem.Register("TSTestService", AccessLevel.GameMaster, TownshipTestService_OnCommand);
        CommandSystem.Register("TownshipSpawnTownsfolk", AccessLevel.GameMaster, TownshipSpawnTownsfolk_OnCommand);
        CommandSystem.Register("TSSpawnTownsfolk", AccessLevel.GameMaster, TownshipSpawnTownsfolk_OnCommand);
    }

    [Usage("Township")]
    [Aliases("Town", "TS")]
    [Description("Opens the township management gump while standing inside your township.")]
    private static void Township_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (!TownshipService.IsEnabled())
        {
            from.SendMessage(0x22, "The township system is currently disabled.");
            return;
        }

        var township = TownshipService.FindAt(from.Location, from.Map);

        if (township == null)
        {
            from.SendMessage(0x22, "You must be standing inside claimed township land to use this command.");
            return;
        }

        if (!TownshipService.CanAccessTownship(township, from))
        {
            from.SendMessage(0x22, "You do not have permission to view this township.");
            return;
        }

        TownshipGump.DisplayTo(from, township);
    }

    private static void TownshipAdmin_OnCommand(CommandEventArgs e)
    {
        TownshipAdminGump.DisplayTo(e.Mobile);
    }

    private static void TownshipDeed_OnCommand(CommandEventArgs e)
    {
        e.Mobile.AddToBackpack(new TownshipDeed());
        e.Mobile.SendMessage(0x35, "A township deed has been placed in your backpack.");
    }

    private static void TownshipInfo_OnCommand(CommandEventArgs e)
    {
        var township = TownshipService.FindAt(e.Mobile.Location, e.Mobile.Map);

        if (township == null)
        {
            e.Mobile.SendMessage(0x22, "You are not standing in claimed township land.");
            return;
        }

        TownshipGump.DisplayTo(e.Mobile, township);
    }

    [Usage("TSTestActivity [amount|Camp|Hamlet|Village|Town|City]")]
    [Description("Raises the activity score for the township you are standing in.")]
    private static void TownshipTestActivity_OnCommand(CommandEventArgs e)
    {
        var township = TownshipService.FindAt(e.Mobile.Location, e.Mobile.Map);

        if (township == null)
        {
            e.Mobile.SendMessage(0x22, "You are not standing in claimed township land.");
            return;
        }

        var amount = 50;
        var targetLevel = TownshipActivityLevel.Camp;
        var setToLevel = false;

        if (e.Length > 0)
        {
            var arg = e.GetString(0);

            if (int.TryParse(arg, out var parsed))
            {
                amount = Math.Clamp(parsed, 1, 1000);
            }
            else if (Enum.TryParse(arg, true, out targetLevel))
            {
                setToLevel = true;
            }
            else
            {
                e.Mobile.SendMessage(0x22, "Usage: TSTestActivity [amount|Camp|Hamlet|Village|Town|City]");
                return;
            }
        }

        if (setToLevel)
        {
            township.ActivityScore = Math.Max(township.ActivityScore, GetActivityThreshold(targetLevel));
            TownshipService.AddLog(township, TownshipLogType.StaffAction, e.Mobile, $"Set activity test score to {township.ActivityScore:N0} for {targetLevel}.");
            e.Mobile.SendMessage(0x35, $"{township.Name} activity is now {township.ActivityLevel} ({township.ActivityScore:N0}).");
            return;
        }

        township.ActivityScore = Math.Min(1000, township.ActivityScore + amount);
        TownshipService.AddLog(township, TownshipLogType.ActivityGain, e.Mobile, $"Staff test added {amount:N0} activity.");
        e.Mobile.SendMessage(0x35, $"{township.Name} gained {amount:N0} activity and is now {township.ActivityLevel} ({township.ActivityScore:N0}).");
    }

    [Usage("TSTestTreasury [entries=12]")]
    [Description("Adds varied fake treasury deposits and withdrawals to the township you are standing in.")]
    private static void TownshipTestTreasury_OnCommand(CommandEventArgs e)
    {
        var township = TownshipService.FindAt(e.Mobile.Location, e.Mobile.Map);

        if (township == null)
        {
            e.Mobile.SendMessage(0x22, "You are not standing in claimed township land.");
            return;
        }

        var entries = e.Length > 0 ? Math.Clamp(e.GetInt32(0), 1, 50) : 12;
        var added = 0;
        var net = 0;

        for (var i = 0; i < entries; i++)
        {
            var withdrawal = i % 4 == 3;
            var name = TestNames[i % TestNames.Length];
            var amount = 100 + (i * 137 % 2400);
            var source = i % 3 == 0 ? TownshipDepositSource.VendorRevenue :
                i % 3 == 1 ? TownshipDepositSource.EscortRevenue : TownshipDepositSource.PlayerDeposit;
            var reason = withdrawal ? TestWithdrawalReasons[i % TestWithdrawalReasons.Length] : TestDepositReasons[i % TestDepositReasons.Length];

            if (withdrawal)
            {
                amount = -Math.Min(amount, Math.Max(0, township.TreasuryBalance + net));
                source = TownshipDepositSource.VendorRevenue;

                if (amount == 0)
                {
                    continue;
                }
            }

            township.DepositLog.Insert(0, new TownshipDepositLogEntry
            {
                Timestamp = Core.Now.AddMinutes(-i * 7),
                PlayerSerial = Serial.Zero,
                PlayerName = name,
                Source = source,
                Amount = amount,
                Note = reason
            });

            net += amount;
            added++;
        }

        township.TreasuryBalance = Math.Max(0, township.TreasuryBalance + net);
        TownshipService.AddLog(township, TownshipLogType.StaffAction, e.Mobile, $"Generated {added:N0} fake treasury test entries. Net treasury change: {net:N0} gold.");
        Trim(township.DepositLog, TownshipSettings.MaxDepositLogEntries);
        e.Mobile.SendMessage(0x35, $"Added {added:N0} fake treasury entries to {township.Name}. Net change: {net:N0} gold.");
    }

    [Usage("TSTestService [name=\"Test Banker\"] [purchaseCost=1000000] [dailyUpkeep=50000]")]
    [Description("Adds a placeholder paid service to the township you are standing in.")]
    private static void TownshipTestService_OnCommand(CommandEventArgs e)
    {
        var township = TownshipService.FindAt(e.Mobile.Location, e.Mobile.Map);

        if (township == null)
        {
            e.Mobile.SendMessage(0x22, "You are not standing in claimed township land.");
            return;
        }

        var name = e.Length > 0 ? e.GetString(0) : "Test Banker";
        var purchaseCost = e.Length > 1 ? Math.Clamp(e.GetInt32(1), 0, 100000000) : 1000000;
        var dailyUpkeep = e.Length > 2 ? Math.Clamp(e.GetInt32(2), 0, 10000000) : 50000;

        if (!TownshipService.AddPaidService(
            township,
            e.Mobile,
            TownshipPaidServiceType.Banker,
            name,
            purchaseCost,
            dailyUpkeep,
            "Staff test service.",
            out var reason
        ))
        {
            e.Mobile.SendMessage(0x22, reason);
            return;
        }

        e.Mobile.SendMessage(0x35, $"Added placeholder service '{name}' to {township.Name}.");
    }

    [Usage("TSSpawnTownsfolk [count=1]")]
    [Description("Spawns ambient townsfolk in the township you are standing in for testing.")]
    private static void TownshipSpawnTownsfolk_OnCommand(CommandEventArgs e)
    {
        var township = TownshipService.FindAt(e.Mobile.Location, e.Mobile.Map);

        if (township == null)
        {
            e.Mobile.SendMessage(0x22, "You are not standing in claimed township land.");
            return;
        }

        var max = Math.Max(1, TownshipSettings.MaxAmbientTownsfolk);
        var count = e.Length > 0 ? Math.Clamp(e.GetInt32(0), 1, max) : 1;

        if (!TownshipService.SpawnAmbientTownsfolk(township, count, out var spawned, out var reason))
        {
            e.Mobile.SendMessage(0x22, reason);
            return;
        }

        e.Mobile.SendMessage(0x35, $"Spawned {spawned:N0} ambient townsfolk in {township.Name}.");
    }

    private static int GetActivityThreshold(TownshipActivityLevel level) => level switch
    {
        TownshipActivityLevel.City    => 400,
        TownshipActivityLevel.Town    => 250,
        TownshipActivityLevel.Village => 125,
        TownshipActivityLevel.Hamlet  => 50,
        _                             => 0
    };

    private static void Trim<T>(List<T> list, int max)
    {
        while (list.Count > max)
        {
            list.RemoveAt(list.Count - 1);
        }
    }
}
