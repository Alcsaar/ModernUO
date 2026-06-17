using System;
using System.Collections.Generic;
using Server;
using Server.Accounting;
using Server.Custom.Systems.AchievementSystem;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Text;

namespace Server.Custom.Engines.ActivityTracking;

public static class ActivityTrackingService
{
    private const int MaxRecentCreatureKills = 100;
    private const int MaxPlayerActivities = 200;
    private static readonly TimeSpan CurrencySnapshotRefreshInterval = TimeSpan.FromSeconds(5.0);

    private static readonly Dictionary<uint, ActivityTrackingPlayerData> _players = new();
    private static readonly Dictionary<string, AccountBalanceRecord> _accountBalances = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint, string> _lastRecordedRegion = new();
    private static readonly Dictionary<uint, MonsterCorpseGoldRecord> _monsterCorpseGold = new();
    private static readonly Dictionary<uint, TreasureChestGoldRecord> _treasureMapChestGold = new();
    private static readonly Dictionary<uint, TreasureChestGoldRecord> _dungeonTreasureChestGold = new();
    private static readonly List<CreatureKillRecord> _recentCreatureKills = new();
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(ActivityTrackingService));
    private static CurrencySnapshot _currencySnapshot = new();
    private static bool _includeStaffMembers = false;
    private static bool _debugEnabled = false;

    public static Dictionary<uint, string> LastRecordedRegion => _lastRecordedRegion;
    public static event Action<CreatureKillRecord> CreatureKillRecorded;

    public static bool DebugEnabled
    {
        get => _debugEnabled;
        private set => _debugEnabled = value;
    }

    public static IReadOnlyList<CreatureKillRecord> RecentCreatureKills
    {
        get
        {
            return _recentCreatureKills.ToArray();
        }
    }

    public static bool IncludeStaffMembers
    {
        get => _includeStaffMembers;
        set => _includeStaffMembers = value;
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: runtime status metrics and memory estimate */
    public static int PlayerCount
    {
        get
        {
            return _players.Count;
        }
    }

    public static int AccountBalanceCount
    {
        get
        {
            EnsureCurrencySnapshotCurrent();
            return _currencySnapshot.VirtualAccountCount;
        }
    }

    public static int RegionCount
    {
        get
        {
            return _lastRecordedRegion.Count;
        }
    }

    public static int RecentKillCount
    {
        get
        {
            return _recentCreatureKills.Count;
        }
    }

    public static int MonsterCorpseGoldRecordCount
    {
        get
        {
            return _monsterCorpseGold.Count;
        }
    }

    public static int TotalDeathCount
    {
        get
        {
            var count = 0;

            foreach (var data in _players.Values)
            {
                count += data.TotalDeaths;
            }

            return count;
        }
    }

    public static int TotalCraftingActions
    {
        get
        {
            var count = 0;

            foreach (var data in _players.Values)
            {
                count += data.TotalCraftingActions;
            }

            return count;
        }
    }

    public static int TotalCraftedQuantity
    {
        get
        {
            var count = 0;

            foreach (var data in _players.Values)
            {
                count += data.TotalCraftedQuantity;
            }

            return count;
        }
    }

    public static int TotalFishingCatches
    {
        get
        {
            var count = 0;

            foreach (var data in _players.Values)
            {
                count += data.TotalFishingCatches;
            }

            return count;
        }
    }

    public static int TotalTreasureMapChestsOpened
    {
        get
        {
            var count = 0;

            foreach (var data in _players.Values)
            {
                count += data.TotalTreasureMapChestsOpened;
            }

            return count;
        }
    }

    public static int TotalMiningMaterials
    {
        get
        {
            var count = 0;

            foreach (var data in _players.Values)
            {
                count += data.TotalMiningMaterials;
            }

            return count;
        }
    }

    public static int TotalLumberMaterials
    {
        get
        {
            var count = 0;

            foreach (var data in _players.Values)
            {
                count += data.TotalLumberMaterials;
            }

            return count;
        }
    }

    public static long TotalTreasureMapChestGoldLooted
    {
        get
        {
            long total = 0;

            foreach (var data in _players.Values)
            {
                total += data.TotalTreasureMapChestGoldLooted;
            }

            return total;
        }
    }

    public static long TotalDungeonTreasureChestGoldLooted
    {
        get
        {
            long total = 0;

            foreach (var data in _players.Values)
            {
                total += data.TotalDungeonTreasureChestGoldLooted;
            }

            return total;
        }
    }

    public static int TotalDungeonTreasureChestsOpened
    {
        get
        {
            var count = 0;

            foreach (var data in _players.Values)
            {
                count += data.TotalDungeonTreasureChestsOpened;
            }

            return count;
        }
    }

    public static long TotalGoldEarned
    {
        get
        {
            long total = 0;

            foreach (var data in _players.Values)
            {
                total += data.TotalGoldEarned;
            }

            return total;
        }
    }

    public static IReadOnlyDictionary<string, int> MiningMaterialTotals => BuildAggregateMaterialTotals(true);

    public static IReadOnlyDictionary<string, int> LumberMaterialTotals => BuildAggregateMaterialTotals(false);

    public static long TotalMonsterGoldLooted
    {
        get
        {
            long total = 0;

            foreach (var data in _players.Values)
            {
                total += data.TotalMonsterGoldLooted;
            }

            return total;
        }
    }

    public static long TotalNpcVendorGoldEarned
    {
        get
        {
            long total = 0;

            foreach (var data in _players.Values)
            {
                total += data.TotalNpcVendorGoldEarned;
            }

            return total;
        }
    }

    public static long TotalGoldSpent
    {
        get
        {
            long total = 0;

            foreach (var data in _players.Values)
            {
                total += data.TotalGoldSpent;
            }

            return total;
        }
    }

    public static long TotalNpcVendorGoldSpent
    {
        get
        {
            long total = 0;

            foreach (var data in _players.Values)
            {
                total += data.TotalNpcVendorGoldSpent;
            }

            return total;
        }
    }

    public static long TotalPlayerVendorSales
    {
        get
        {
            long total = 0;

            foreach (var data in _players.Values)
            {
                total += data.TotalPlayerVendorSales;
            }

            return total;
        }
    }

    public static long TotalPlayerVendorCommissions
    {
        get
        {
            long total = 0;

            foreach (var data in _players.Values)
            {
                total += data.TotalPlayerVendorCommissions;
            }

            return total;
        }
    }

    public static long TotalGoldDecayed { get; private set; }

    public static long TotalGoldLeavingEconomy => TotalNpcVendorGoldSpent + TotalPlayerVendorCommissions + TotalGoldDecayed;

    public static long TotalKnownBankBalance
    {
        get
        {
            EnsureCurrencySnapshotCurrent();
            return _currencySnapshot.TotalCurrency;
        }
    }

    public static long GetEstimatedMemoryUsage()
    {
        long memory = 0;

        // Rough estimate: 1KB per player data
        memory += _players.Count * 1024L;

        /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: include capped per-player activity strings in runtime memory estimate */
        foreach (var playerData in _players.Values)
        {
            memory += playerData.Activities.Count * 192L;
            memory += playerData.ExploredRegions.Count * 160L;
        }
        /* END ACTIVITY TRACKING CUSTOMIZATION */

        /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: include cached account economy balances in runtime memory estimate */
        memory += _accountBalances.Count * 160L;
        /* END ACTIVITY TRACKING CUSTOMIZATION */

        /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: include active monster corpse gold records in runtime memory estimate */
        memory += _monsterCorpseGold.Count * 96L;
        /* END ACTIVITY TRACKING CUSTOMIZATION */

        // Rough estimate: 2KB per recent kill record
        memory += _recentCreatureKills.Count * 2048L;

        // Rough estimate: 128 bytes per region entry
        memory += _lastRecordedRegion.Count * 128L;

        return memory;
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    public static void Configure()
    {
        /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: region movement tracking is intentionally part of runtime activity status */
        EventSink.Movement += ActivityTrackingRegions.OnMovement;
        /* END ACTIVITY TRACKING CUSTOMIZATION */
    }

    public static void Initialize()
    {
        /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: initialize cached economy snapshot for status visibility */
        RefreshKnownCurrencySnapshot(true);
        /* END ACTIVITY TRACKING CUSTOMIZATION */
    }

    public static void LoadPlayerData()
    {
        // No persistence layer; nothing to load.
    }

    public static void ReloadData()
    {
        // No persistence layer; nothing to reload.
    }

    public static void SavePlayerData()
    {
        // No persistence layer; nothing to save.
    }

    public static void SaveAllData()
    {
        // No persistence layer; nothing to save.
    }

    public static void ResetData()
    {
        _players.Clear();
        _accountBalances.Clear();
        _lastRecordedRegion.Clear();
        _monsterCorpseGold.Clear();
        _treasureMapChestGold.Clear();
        _dungeonTreasureChestGold.Clear();
        _recentCreatureKills.Clear();
        _currencySnapshot = new CurrencySnapshot();
        TotalGoldDecayed = 0;
    }

    public static void ToggleDebug()
    {
        DebugEnabled = !DebugEnabled;
    }

    public static void ClearRecentKills()
    {
        _recentCreatureKills.Clear();
    }

    public static void RecordPlayerActivity(PlayerMobile player, string activityType)
    {
        if (player == null || string.IsNullOrWhiteSpace(activityType))
        {
            return;
        }

        var data = GetOrCreatePlayerData(player);
        AddPlayerActivity(data, $"{DateTime.UtcNow:O}: {activityType}");
        data.LastUpdatedUtc = DateTime.UtcNow;
    }

    public static void RecordCreatureKill(PlayerMobile player, BaseCreature creature)
    {
        if (player == null || creature == null)
        {
            return;
        }

        if (!ShouldTrackPlayer(player))
        {
            return;
        }

        var rights = BaseCreature.GetLootingRights(creature.DamageEntries, creature.HitsMax);

        if (rights == null || rights.Count == 0)
        {
            rights = new List<DamageStore> { new DamageStore(player, 0) };
        }

        RecordCreatureKill(creature, rights);
    }

    public static void RecordSkillMilestone(PlayerMobile player, SkillName skill, double oldBase)
    {
        ActivityTrackingSkills.RecordSkillMilestone(player, skill, oldBase);
    }

    public static void RecordCraftedItem(Mobile from, Item item, CraftSystem craftSystem, int quality)
    {
        if (from is not PlayerMobile player || item == null || craftSystem == null || !ShouldTrackPlayer(player))
        {
            return;
        }

        var data = GetOrCreatePlayerData(player);
        var timestamp = DateTime.UtcNow;
        var quantity = item.Amount > 0 ? item.Amount : 1;
        var qualityText = quality switch
        {
            > 1 => " exceptional",
            < 0 => " failed",
            _   => string.Empty
        };

        data.TotalCraftingActions++;
        data.TotalCraftedQuantity += quantity;
        AddPlayerActivity(
            data,
            $"{timestamp:O}: Crafted {quantity}x {item.GetType().Name} via {craftSystem.GetType().Name} ({craftSystem.MainSkill}){qualityText}"
        );
        data.LastUpdatedUtc = timestamp;

        if (DebugEnabled)
        {
            WriteActivityDebug(player, $"crafted {quantity}x {item.GetType().Name} via {craftSystem.GetType().Name} ({craftSystem.MainSkill}){qualityText}");
        }
    }

    public static void RecordFishingCatch(Mobile from, Item item)
    {
        if (from is not PlayerMobile player || item == null || !ShouldTrackPlayer(player))
        {
            return;
        }

        var data = GetOrCreatePlayerData(player);
        var timestamp = DateTime.UtcNow;
        var quantity = item.Amount > 0 ? item.Amount : 1;

        data.TotalFishingCatches++;
        AddPlayerActivity(data, $"{timestamp:O}: Caught {quantity}x {item.GetType().Name} while fishing");
        data.LastUpdatedUtc = timestamp;

        if (DebugEnabled)
        {
            WriteActivityDebug(player, $"caught {quantity}x {item.GetType().Name} while fishing");
        }
    }

    public static void RecordMiningYield(Mobile from, Item item)
    {
        RecordHarvestYield(from, item, true);
    }

    public static void RecordLumberYield(Mobile from, Item item)
    {
        RecordHarvestYield(from, item, false);
    }

    public static void RecordTreasureMapCompleted(Mobile from, TreasureMapChest chest)
    {
        if (from is not PlayerMobile player || chest == null || !ShouldTrackPlayer(player))
        {
            return;
        }

        var data = GetOrCreatePlayerData(player);
        var timestamp = DateTime.UtcNow;
        var region = Region.Find(chest.Location, chest.Map);

        data.TotalTreasureMapChestsOpened++;
        AddPlayerActivity(
            data,
            $"{timestamp:O}: Completed treasure map chest level {chest.Level} at {chest.Map} {chest.Location} region={region?.Name ?? "Unknown"}"
        );
        data.LastUpdatedUtc = timestamp;

        if (DebugEnabled)
        {
            WriteActivityDebug(player, $"completed treasure map chest level {chest.Level} at {chest.Map} {chest.Location} region={region?.Name ?? "Unknown"}");
        }
    }

    public static void RegisterTreasureMapChestGold(TreasureMapChest chest, Gold gold)
    {
        RegisterTreasureChestGold(_treasureMapChestGold, chest, gold, "TreasureMapChest");
    }

    public static void RegisterDungeonTreasureChestGold(BaseTreasureChest chest, Gold gold)
    {
        RegisterTreasureChestGold(_dungeonTreasureChestGold, chest, gold, "DungeonTreasureChest");
    }

    public static void RegisterDungeonTreasureChestGold(LockableContainer chest, Gold gold)
    {
        RegisterTreasureChestGold(_dungeonTreasureChestGold, chest, gold, chest?.GetType().Name ?? "DungeonTreasureChest");
    }

    public static void RecordTreasureMapChestGoldLooted(Mobile from, TreasureMapChest chest, Item item)
    {
        RecordTreasureChestGoldLooted(from, chest, item, _treasureMapChestGold, ActivityGoldSource.TreasureMapChestGold);
    }

    public static void RecordDungeonTreasureChestGoldLooted(Mobile from, BaseTreasureChest chest, Item item)
    {
        RecordTreasureChestGoldLooted(from, chest, item, _dungeonTreasureChestGold, ActivityGoldSource.DungeonTreasureChestGold);
    }

    public static void RecordDungeonTreasureChestGoldLooted(Mobile from, LockableContainer chest, Item item)
    {
        RecordTreasureChestGoldLooted(from, chest, item, _dungeonTreasureChestGold, ActivityGoldSource.DungeonTreasureChestGold);
    }

    public static void ClearTreasureMapChestGold(TreasureMapChest chest)
    {
        ClearTreasureChestGold(_treasureMapChestGold, chest);
    }

    public static void ClearDungeonTreasureChestGold(BaseTreasureChest chest)
    {
        ClearTreasureChestGold(_dungeonTreasureChestGold, chest);
    }

    public static void ClearDungeonTreasureChestGold(LockableContainer chest)
    {
        ClearTreasureChestGold(_dungeonTreasureChestGold, chest);
    }

    public static void RecordTreasureMapChestOpened(Mobile from, TreasureMapChest chest)
    {
        if (from is not PlayerMobile player || chest == null || !ShouldTrackPlayer(player))
        {
            return;
        }

        var data = GetOrCreatePlayerData(player);
        var timestamp = DateTime.UtcNow;
        var region = Region.Find(chest.Location, chest.Map);

        data.TotalTreasureMapChestsOpened++;
        AddPlayerActivity(
            data,
            $"{timestamp:O}: Opened treasure map chest level {chest.Level} at {chest.Map} {chest.Location} region={region?.Name ?? "Unknown"}"
        );
        data.LastUpdatedUtc = timestamp;

        if (DebugEnabled)
        {
            WriteActivityDebug(player, $"opened treasure map chest level {chest.Level} at {chest.Map} {chest.Location} region={region?.Name ?? "Unknown"}");
        }
    }

    public static void RecordDungeonTreasureChestOpened(Mobile from, BaseTreasureChest chest)
    {
        if (from is not PlayerMobile player || chest == null || !ShouldTrackPlayer(player))
        {
            return;
        }

        var data = GetOrCreatePlayerData(player);
        var timestamp = DateTime.UtcNow;
        var region = Region.Find(chest.Location, chest.Map);

        data.TotalDungeonTreasureChestsOpened++;
        AddPlayerActivity(
            data,
            $"{timestamp:O}: Opened dungeon treasure chest {chest.Level} at {chest.Map} {chest.Location} region={region?.Name ?? "Unknown"}"
        );
        data.LastUpdatedUtc = timestamp;

        if (DebugEnabled)
        {
            WriteActivityDebug(player, $"opened dungeon treasure chest {chest.Level} at {chest.Map} {chest.Location} region={region?.Name ?? "Unknown"}");
        }
    }

    public static void RecordDungeonTreasureChestOpened(Mobile from, LockableContainer chest, string chestName)
    {
        if (from is not PlayerMobile player || chest == null || !ShouldTrackPlayer(player))
        {
            return;
        }

        var data = GetOrCreatePlayerData(player);
        var timestamp = DateTime.UtcNow;
        var region = Region.Find(chest.Location, chest.Map);
        var resolvedName = chestName.DefaultIfNullOrEmpty(chest.GetType().Name);

        data.TotalDungeonTreasureChestsOpened++;
        AddPlayerActivity(
            data,
            $"{timestamp:O}: Opened dungeon treasure chest {resolvedName} at {chest.Map} {chest.Location} region={region?.Name ?? "Unknown"}"
        );
        data.LastUpdatedUtc = timestamp;

        if (DebugEnabled)
        {
            WriteActivityDebug(player, $"opened dungeon treasure chest {resolvedName} at {chest.Map} {chest.Location} region={region?.Name ?? "Unknown"}");
        }
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: player death and gold source tracking */
    public static void RecordPlayerDeath(PlayerMobile player, Mobile killer)
    {
        if (player == null || !ShouldTrackPlayer(player))
        {
            return;
        }

        var data = GetOrCreatePlayerData(player);
        var timestamp = DateTime.UtcNow;

        data.TotalDeaths++;
        data.LastDeathUtc = timestamp;
        data.LastDeathRegion = player.Region?.Name ?? "Unknown";
        data.LastDeathMap = player.Map?.ToString() ?? "Unknown";
        data.LastDeathLocation = player.Location;
        data.LastKillerSerial = killer?.Serial.Value ?? 0;
        data.LastKillerName = killer?.Name ?? "Unknown";
        data.LastKillerType = killer?.GetType().Name ?? "Unknown";

        AddPlayerActivity(data, $"{timestamp:O}: Died to {data.LastKillerName} ({data.LastKillerType}, {data.LastKillerSerial}) in {data.LastDeathRegion}");
        data.LastUpdatedUtc = timestamp;

        if (DebugEnabled)
        {
            WriteDeathDebug(player, data);
        }
    }

    public static void RegisterMonsterCorpseGold(BaseCreature creature, Container corpseContainer)
    {
        if (creature == null || corpseContainer is not Corpse corpse || corpse.Deleted || creature.Summoned || creature.Controlled || creature.Owners.Count > 0)
        {
            return;
        }

        var totalGold = CountGold(corpse);

        if (totalGold <= 0)
        {
            return;
        }

        _monsterCorpseGold[corpse.Serial.Value] = new MonsterCorpseGoldRecord
        {
            CreatureSerial = creature.Serial.Value,
            CreatureName = creature.Name ?? creature.GetType().Name,
            CreatureType = creature.GetType().Name,
            RegionName = creature.Region?.Name ?? "Unknown",
            Map = creature.Map?.ToString() ?? "Unknown",
            Location = creature.Location,
            RemainingGold = totalGold
        };
    }

    public static void RecordMonsterCorpseGoldLooted(Mobile from, Corpse corpse, Item item)
    {
        if (from is not PlayerMobile player || corpse == null || item is not Gold gold || !ShouldTrackPlayer(player))
        {
            return;
        }

        if (!_monsterCorpseGold.TryGetValue(corpse.Serial.Value, out var record) || record.RemainingGold <= 0)
        {
            return;
        }

        var creditedGold = Math.Min(gold.Amount, record.RemainingGold);

        if (creditedGold <= 0)
        {
            return;
        }

        record.RemainingGold -= creditedGold;

        if (record.RemainingGold <= 0)
        {
            _monsterCorpseGold.Remove(corpse.Serial.Value);
        }

        RecordGoldEarned(
            player,
            creditedGold,
            ActivityGoldSource.MonsterCorpse,
            record.CreatureSerial,
            record.CreatureName,
            record.CreatureType,
            new ActivityLocation
            {
                RegionName = player.Region?.Name ?? record.RegionName,
                Map = player.Map?.ToString() ?? record.Map,
                Location = player.Location
            }
        );
    }

    public static void ClearMonsterCorpseGold(Corpse corpse)
    {
        if (corpse == null)
        {
            return;
        }

        _monsterCorpseGold.Remove(corpse.Serial.Value);
    }

    public static void RecordVendorGoldEarned(
        Mobile seller,
        BaseVendor vendor,
        int amount,
        IReadOnlyList<VendorSaleLine> saleLines
    )
    {
        if (seller is not PlayerMobile player || vendor == null || amount <= 0 || !ShouldTrackPlayer(player))
        {
            return;
        }

        RecordGoldEarned(
            player,
            amount,
            ActivityGoldSource.NpcVendorSale,
            vendor.Serial.Value,
            vendor.Name ?? vendor.GetType().Name,
            vendor.GetType().Name,
            new ActivityLocation
            {
                RegionName = vendor.Region?.Name ?? "Unknown",
                Map = vendor.Map?.ToString() ?? "Unknown",
                Location = vendor.Location
            },
            saleLines
        );
    }

    public static void RecordNpcVendorGoldSpent(
        Mobile buyer,
        BaseVendor vendor,
        int amount,
        IReadOnlyList<VendorSaleLine> purchaseLines
    )
    {
        if (buyer is not PlayerMobile player || vendor == null || amount <= 0 || !ShouldTrackPlayer(player))
        {
            return;
        }

        RecordGoldSpent(
            player,
            amount,
            ActivityGoldSink.NpcVendorPurchase,
            vendor.Serial.Value,
            vendor.Name ?? vendor.GetType().Name,
            vendor.GetType().Name,
            new ActivityLocation
            {
                RegionName = vendor.Region?.Name ?? "Unknown",
                Map = vendor.Map?.ToString() ?? "Unknown",
                Location = vendor.Location
            },
            purchaseLines
        );
    }

    public static void RecordPlayerVendorSale(Mobile buyer, PlayerVendor vendor, Item item, VendorItem vendorItem, int amount)
    {
        if (buyer is not PlayerMobile player || vendor == null || item == null || vendorItem == null || amount < 0 || !ShouldTrackPlayer(player))
        {
            return;
        }

        var owner = vendor.Owner as PlayerMobile;
        var quantity = item.Amount > 0 ? item.Amount : 1;
        var line = new VendorSaleLine
        {
            ItemSerial = item.Serial.Value,
            SellerSerial = vendor.Owner?.Serial.Value ?? 0,
            BuyerSerial = player.Serial.Value,
            VendorSerial = vendor.Serial.Value,
            RegionName = vendor.Region?.Name ?? "Unknown",
            Map = vendor.Map?.ToString() ?? "Unknown",
            Location = vendor.Location,
            ItemType = item.GetType().Name,
            ItemName = item.Name ?? vendorItem.Description.DefaultIfNullOrEmpty(item.GetType().Name),
            Quantity = quantity,
            UnitPrice = quantity > 0 ? amount / quantity : amount,
            TotalPrice = amount
        };

        var timestamp = DateTime.UtcNow;
        var data = GetOrCreatePlayerData(player);
        data.TotalGoldSpent += amount;
        data.TotalPlayerVendorPurchaseGold += amount;
        AddPlayerActivity(
            data,
            $"{timestamp:O}: Bought {BuildVendorSaleSummary([line])} from player vendor {vendor.Name ?? vendor.GetType().Name}/{vendor.Serial.Value} at {line.Map} {line.Location} region={line.RegionName}"
        );
        data.LastUpdatedUtc = timestamp;
        RefreshBankBalance(player);

        if (owner != null && ShouldTrackPlayer(owner))
        {
            var ownerData = GetOrCreatePlayerData(owner);
            ownerData.TotalPlayerVendorSales += amount;
            AddPlayerActivity(
                ownerData,
                $"{timestamp:O}: Player vendor {vendor.Name ?? vendor.GetType().Name}/{vendor.Serial.Value} sold {BuildVendorSaleSummary([line])} to {player.Name ?? "Unknown"}/{player.Serial.Value} at {line.Map} {line.Location} region={line.RegionName}"
            );
            ownerData.LastUpdatedUtc = timestamp;
            RefreshBankBalance(owner);
        }

        if (DebugEnabled)
        {
            WritePlayerVendorSaleDebug(player, owner, vendor, line);
        }
    }

    public static void RecordPlayerVendorCommission(PlayerVendor vendor, int amount, string fundingSource)
    {
        if (vendor == null || amount <= 0)
        {
            return;
        }

        var owner = vendor.Owner as PlayerMobile;
        var timestamp = DateTime.UtcNow;

        if (owner != null && ShouldTrackPlayer(owner))
        {
            var data = GetOrCreatePlayerData(owner);
            data.TotalPlayerVendorCommissions += amount;
            AddPlayerActivity(
                data,
                $"{timestamp:O}: Player vendor commission removed {amount} gold from {fundingSource} for {vendor.Name ?? vendor.GetType().Name}/{vendor.Serial.Value} at {vendor.Map} {vendor.Location} region={vendor.Region?.Name ?? "Unknown"}"
            );
            data.LastUpdatedUtc = timestamp;
            RefreshBankBalance(owner);
        }

        if (DebugEnabled)
        {
            WriteEconomyDebug(
                $"[ActivityTracking] {timestamp:O}: Player vendor commission removed {amount} gold from {fundingSource} for {vendor.Name ?? vendor.GetType().Name}/{vendor.Serial.Value}."
            );
        }
    }

    public static void RecordGoldDecayed(Item item, int amount, string currencyType)
    {
        if (item == null || amount <= 0)
        {
            return;
        }

        TotalGoldDecayed += amount;

        if (DebugEnabled)
        {
            var region = Region.Find(item.Location, item.Map);
            WriteEconomyDebug(
                $"[ActivityTracking] {DateTime.UtcNow:O}: {currencyType} decayed for {amount} gold at {item.Map} {item.Location} region={region?.Name ?? "Unknown"} item={item.Serial.Value}."
            );
        }
    }

    public static void RefreshBankBalance(PlayerMobile player)
    {
        if (player == null || !ShouldTrackPlayer(player))
        {
            return;
        }

        var data = GetOrCreatePlayerData(player);
        var balance = Banker.GetBalance(player);
        var timestamp = DateTime.UtcNow;

        data.LastKnownBankBalance = balance;
        data.LastBankBalanceUtc = timestamp;
        data.LastUpdatedUtc = timestamp;

        var accountName = player.Account?.Username ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(accountName))
        {
            _accountBalances[accountName] = new AccountBalanceRecord
            {
                AccountName = accountName,
                LastPlayerSerial = player.Serial.Value,
                LastPlayerName = player.Name ?? string.Empty,
                Balance = balance,
                LastUpdatedUtc = timestamp
            };
        }
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: status economy total now scans world currency, player-held currency, and account gold */
    public static void RefreshKnownCurrencySnapshot(bool force = false)
    {
        var now = DateTime.UtcNow;

        if (!force && now - _currencySnapshot.LastUpdatedUtc < CurrencySnapshotRefreshInterval)
        {
            return;
        }

        long totalGold = 0;
        long totalChecks = 0;
        long totalAccountGold = 0;
        var goldStackCount = 0;
        var checkCount = 0;
        var virtualAccountCount = 0;

        foreach (var item in World.Items.Values)
        {
            if (item == null || item.Deleted || !ShouldCountCurrencyItem(item))
            {
                continue;
            }

            switch (item)
            {
                case Gold gold:
                    totalGold += gold.Amount;
                    goldStackCount++;
                    break;
                case BankCheck check:
                    totalChecks += check.Worth;
                    checkCount++;
                    break;
            }
        }

        if (AccountGold.Enabled)
        {
            foreach (var account in Accounts.GetAccounts())
            {
                if (account == null)
                {
                    continue;
                }

                var balance = account.GetTotalGold();
                if (balance <= 0)
                {
                    continue;
                }

                totalAccountGold += balance;
                virtualAccountCount++;
            }
        }

        _currencySnapshot = new CurrencySnapshot
        {
            LastUpdatedUtc = now,
            GoldItemTotal = totalGold,
            BankCheckTotal = totalChecks,
            VirtualAccountGoldTotal = totalAccountGold,
            GoldStackCount = goldStackCount,
            BankCheckCount = checkCount,
            VirtualAccountCount = virtualAccountCount
        };
    }

    private static void EnsureCurrencySnapshotCurrent()
    {
        RefreshKnownCurrencySnapshot();
    }

    private static bool ShouldCountCurrencyItem(Item item)
    {
        if (item.Map == Map.Internal)
        {
            return false;
        }

        var root = item.RootParent;

        return root switch
        {
            null         => true,
            PlayerMobile => true,
            Item         => BaseHouse.FindHouseAt(item) != null,
            _            => false
        };
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    private static void RecordGoldEarned(
        PlayerMobile player,
        int amount,
        ActivityGoldSource source,
        uint sourceSerial,
        string sourceName,
        string sourceType,
        ActivityLocation location,
        IReadOnlyList<VendorSaleLine> saleLines = null
    )
    {
        var data = GetOrCreatePlayerData(player);
        var timestamp = DateTime.UtcNow;

        data.TotalGoldEarned += amount;

        switch (source)
        {
            case ActivityGoldSource.MonsterCorpse:
                data.TotalMonsterGoldLooted += amount;
                break;
            case ActivityGoldSource.NpcVendorSale:
                data.TotalNpcVendorGoldEarned += amount;
                break;
            case ActivityGoldSource.TreasureMapChestGold:
                data.TotalTreasureMapChestGoldLooted += amount;
                break;
            case ActivityGoldSource.DungeonTreasureChestGold:
                data.TotalDungeonTreasureChestGoldLooted += amount;
                break;
        }

        RecordAchievementEconomyGold(player, source, amount);

        var activity = source == ActivityGoldSource.NpcVendorSale && saleLines?.Count > 0
            ? $"{timestamp:O}: Earned {amount} gold from {source} ({sourceName}/{sourceType}/{sourceSerial}) at {location.Map} {location.Location} region={location.RegionName} selling {BuildVendorSaleSummary(saleLines)}"
            : $"{timestamp:O}: Earned {amount} gold from {source} ({sourceName}/{sourceType}/{sourceSerial}) at {location.Map} {location.Location} region={location.RegionName}";

        AddPlayerActivity(data, activity);
        data.LastUpdatedUtc = timestamp;

        if (DebugEnabled)
        {
            WriteGoldDebug(player, amount, source, sourceSerial, sourceName, sourceType, location, saleLines);
        }
    }

    private static void RecordAchievementEconomyGold(PlayerMobile player, ActivityGoldSource source, int amount)
    {
        switch (source)
        {
            case ActivityGoldSource.MonsterCorpse:
                AchievementService.RecordEconomyGoldEarned(player, AchievementEconomyGoldSource.MonsterLoot, amount);
                return;
            case ActivityGoldSource.NpcVendorSale:
                AchievementService.RecordEconomyGoldEarned(player, AchievementEconomyGoldSource.VendorSale, amount);
                return;
            case ActivityGoldSource.TreasureMapChestGold:
                AchievementService.RecordEconomyGoldEarned(player, AchievementEconomyGoldSource.TreasureMapChest, amount);
                return;
            case ActivityGoldSource.DungeonTreasureChestGold:
                AchievementService.RecordEconomyGoldEarned(player, AchievementEconomyGoldSource.DungeonChest, amount);
                return;
        }
    }

    private static void RecordGoldSpent(
        PlayerMobile player,
        int amount,
        ActivityGoldSink sink,
        uint sinkSerial,
        string sinkName,
        string sinkType,
        ActivityLocation location,
        IReadOnlyList<VendorSaleLine> purchaseLines = null
    )
    {
        var data = GetOrCreatePlayerData(player);
        var timestamp = DateTime.UtcNow;

        data.TotalGoldSpent += amount;

        switch (sink)
        {
            case ActivityGoldSink.NpcVendorPurchase:
                data.TotalNpcVendorGoldSpent += amount;
                break;
        }

        var activity = sink == ActivityGoldSink.NpcVendorPurchase && purchaseLines?.Count > 0
            ? $"{timestamp:O}: Spent {amount} gold on {sink} ({sinkName}/{sinkType}/{sinkSerial}) at {location.Map} {location.Location} region={location.RegionName} buying {BuildVendorSaleSummary(purchaseLines)}"
            : $"{timestamp:O}: Spent {amount} gold on {sink} ({sinkName}/{sinkType}/{sinkSerial}) at {location.Map} {location.Location} region={location.RegionName}";

        AddPlayerActivity(data, activity);
        data.LastUpdatedUtc = timestamp;
        RefreshBankBalance(player);

        if (DebugEnabled)
        {
            WriteGoldSpentDebug(player, amount, sink, sinkSerial, sinkName, sinkType, location, purchaseLines);
        }
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    public static void RecordCreatureKill(BaseCreature creature, List<DamageStore> rights)
    {
        if (creature == null)
        {
            return;
        }

        rights ??= BaseCreature.GetLootingRights(creature.DamageEntries, creature.HitsMax);

        var participants = BuildParticipants(rights, creature);
        if (participants.Count == 0)
        {
            return;
        }

        UpdatePlayerKillStats(creature, participants);

        var record = CreateCreatureKillRecord(creature, participants);

        _recentCreatureKills.Add(record);

        while (_recentCreatureKills.Count > MaxRecentCreatureKills)
        {
            _recentCreatureKills.RemoveAt(0);
        }

        CreatureKillRecorded?.Invoke(record);

        if (DebugEnabled)
        {
            WriteDebug(record);
        }
    }

    public static bool ShouldTrackPlayer(PlayerMobile player)
    {
        if (player == null)
        {
            return false;
        }

        if (!_includeStaffMembers && player.AccessLevel > AccessLevel.Player)
        {
            return false;
        }

        return true;
    }

    public static ActivityTrackingPlayerData GetOrCreatePlayerData(PlayerMobile player)
    {
        var serial = player.Serial.Value;

        if (!_players.TryGetValue(serial, out var data))
        {
            data = new ActivityTrackingPlayerData
            {
                PlayerSerial = serial,
                PlayerName = player.Name,
                AccountName = player.Account?.Username ?? string.Empty,
                FirstSeenUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            };

            _players[serial] = data;
        }
        else if (data.PlayerName != player.Name)
        {
            data.PlayerName = player.Name;
            data.LastUpdatedUtc = DateTime.UtcNow;
        }

        return data;
    }

    private static void UpdatePlayerKillStats(BaseCreature creature, List<CreatureKillParticipant> participants)
    {
        var creatureTypeName = creature.GetType().Name;
        var timestamp = DateTime.UtcNow;

        foreach (var participant in participants)
        {
            if (!participant.IsPlayer)
            {
                continue;
            }

            var data = GetOrCreatePlayerData(participant.PlayerMobile!);
            data.TotalKills++;
            data.MonsterKills[creatureTypeName] = data.MonsterKills.TryGetValue(creatureTypeName, out var count) ? count + 1 : 1;
            data.LastCreatureKilled = creatureTypeName;
            AddPlayerActivity(data, $"{timestamp:O}: Killed {creatureTypeName} ({creature.Name ?? creatureTypeName}) in {creature.Region?.Name ?? "Unknown"}");
            data.LastUpdatedUtc = timestamp;
        }
    }

    private static void RecordHarvestYield(Mobile from, Item item, bool mining)
    {
        if (from is not PlayerMobile player || item == null || !ShouldTrackPlayer(player))
        {
            return;
        }

        var data = GetOrCreatePlayerData(player);
        var timestamp = DateTime.UtcNow;
        var quantity = item.Amount > 0 ? item.Amount : 1;
        var typeName = item.GetType().Name;
        var totals = mining ? data.MiningMaterialTotals : data.LumberMaterialTotals;

        totals[typeName] = totals.TryGetValue(typeName, out var current) ? current + quantity : quantity;

        if (mining)
        {
            data.TotalMiningMaterials += quantity;
            AddPlayerActivity(data, $"{timestamp:O}: Mined {quantity}x {typeName}");
        }
        else
        {
            data.TotalLumberMaterials += quantity;
            AddPlayerActivity(data, $"{timestamp:O}: Chopped {quantity}x {typeName}");
        }

        data.LastUpdatedUtc = timestamp;

        if (DebugEnabled)
        {
            WriteActivityDebug(player, $"{(mining ? "mined" : "chopped")} {quantity}x {typeName}");
        }
    }

    private static IReadOnlyDictionary<string, int> BuildAggregateMaterialTotals(bool mining)
    {
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var data in _players.Values)
        {
            var source = mining ? data.MiningMaterialTotals : data.LumberMaterialTotals;

            foreach (var kvp in source)
            {
                totals[kvp.Key] = totals.TryGetValue(kvp.Key, out var current) ? current + kvp.Value : kvp.Value;
            }
        }

        return totals;
    }

    private static void RegisterTreasureChestGold(
        Dictionary<uint, TreasureChestGoldRecord> records,
        Container chest,
        Gold gold,
        string chestType
    )
    {
        if (chest == null || gold == null || gold.Amount <= 0)
        {
            return;
        }

        records[chest.Serial.Value] = new TreasureChestGoldRecord
        {
            ChestSerial = chest.Serial.Value,
            ChestType = chestType,
            RegionName = Region.Find(chest.Location, chest.Map)?.Name ?? "Unknown",
            Map = chest.Map?.ToString() ?? "Unknown",
            Location = chest.Location,
            RemainingGold = gold.Amount
        };
    }

    private static void RecordTreasureChestGoldLooted(
        Mobile from,
        Container chest,
        Item item,
        Dictionary<uint, TreasureChestGoldRecord> records,
        ActivityGoldSource source
    )
    {
        if (from is not PlayerMobile player || chest == null || item is not Gold gold || !ShouldTrackPlayer(player))
        {
            return;
        }

        if (!records.TryGetValue(chest.Serial.Value, out var record) || record.RemainingGold <= 0)
        {
            return;
        }

        var creditedGold = Math.Min(gold.Amount, record.RemainingGold);

        if (creditedGold <= 0)
        {
            return;
        }

        record.RemainingGold -= creditedGold;

        if (record.RemainingGold <= 0)
        {
            records.Remove(chest.Serial.Value);
        }

        RecordGoldEarned(
            player,
            creditedGold,
            source,
            record.ChestSerial,
            record.ChestType,
            record.ChestType,
            new ActivityLocation
            {
                RegionName = player.Region?.Name ?? record.RegionName,
                Map = player.Map?.ToString() ?? record.Map,
                Location = player.Location
            }
        );
    }

    private static void ClearTreasureChestGold(Dictionary<uint, TreasureChestGoldRecord> records, Container chest)
    {
        if (chest == null)
        {
            return;
        }

        records.Remove(chest.Serial.Value);
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: cap per-player activity history for long server uptimes */
    internal static void AddPlayerActivity(ActivityTrackingPlayerData data, string activity)
    {
        if (data == null || string.IsNullOrWhiteSpace(activity))
        {
            return;
        }

        data.Activities.Add(activity);

        while (data.Activities.Count > MaxPlayerActivities)
        {
            data.Activities.RemoveAt(0);
        }
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    private static int CountGold(Container container)
    {
        var total = 0;
        var items = container.Items;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (item is Gold gold)
            {
                total += gold.Amount;
            }
            else if (item is Container childContainer)
            {
                total += CountGold(childContainer);
            }
        }

        return total;
    }

    private static string BuildVendorSaleSummary(IReadOnlyList<VendorSaleLine> saleLines)
    {
        using var builder = ValueStringBuilder.Create();

        for (var i = 0; i < saleLines.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var line = saleLines[i];
            builder.Append(line.Quantity);
            builder.Append('x');
            builder.Append(line.ItemType);
            builder.Append('=');
            builder.Append(line.TotalPrice);
        }

        return builder.ToString();
    }

    private static List<CreatureKillParticipant> BuildParticipants(List<DamageStore> rights, BaseCreature creature)
    {
        var participants = new List<CreatureKillParticipant>();
        var seen = new HashSet<uint>();

        if (rights == null)
        {
            rights = new List<DamageStore>();
        }

        foreach (var ds in rights)
        {
            if (ds?.m_Mobile == null)
            {
                continue;
            }

            var mobile = ds.m_Mobile;
            var serial = mobile.Serial.Value;

            if (!seen.Add(serial))
            {
                continue;
            }

            if (mobile is PlayerMobile pm && !ShouldTrackPlayer(pm))
            {
                continue;
            }

            participants.Add(new CreatureKillParticipant
            {
                ParticipantSerial = serial,
                ParticipantName = mobile.Name ?? mobile.GetType().Name,
                ParticipantType = mobile.GetType().Name,
                AccountName = mobile is PlayerMobile player ? player.Account?.Username ?? string.Empty : string.Empty,
                Damage = ds.m_Damage,
                HasLootRight = ds.m_HasRight,
                IsPlayer = mobile is PlayerMobile,
                PlayerMobile = mobile as PlayerMobile
            });
        }

        /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: include bard provoker participation for provoked kills */
        if (creature?.BardProvoked == true && creature.BardMaster != null)
        {
            var bard = creature.BardMaster;

            if (bard is PlayerMobile playerBard && !ShouldTrackPlayer(playerBard))
            {
                return participants;
            }

            var bardSerial = bard.Serial.Value;
            if (seen.Add(bardSerial))
            {
                participants.Add(new CreatureKillParticipant
                {
                    ParticipantSerial = bardSerial,
                    ParticipantName = bard.Name ?? "Bard",
                    ParticipantType = bard.GetType().Name,
                    AccountName = bard is PlayerMobile bardPlayer ? bardPlayer.Account?.Username ?? string.Empty : string.Empty,
                    Damage = 0,
                    HasLootRight = false,
                    IsPlayer = bard is PlayerMobile,
                    PlayerMobile = bard as PlayerMobile,
                    IsBardProvocationCredit = true
                });
            }
        }
        /* END ACTIVITY TRACKING CUSTOMIZATION */

        return participants;
    }

    private static CreatureKillRecord CreateCreatureKillRecord(BaseCreature creature, List<CreatureKillParticipant> participants)
    {
        var primary = GetPrimaryParticipant(participants);

        return new CreatureKillRecord
        {
            TimestampUtc = DateTime.UtcNow,
            CreatureSerial = creature.Serial.Value,
            CreatureName = creature.Name ?? creature.GetType().Name,
            CreatureType = creature.GetType().Name,
            Location = creature.Location,
            Map = creature.Map?.ToString() ?? "Unknown",
            RegionName = creature.Region?.Name ?? "Unknown",
            Participants = participants,
            PrimaryParticipantSerial = primary?.ParticipantSerial ?? 0
        };
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: colored kill debug output and participant detail logging */
    private static void WriteDebug(CreatureKillRecord record)
    {
        if (record == null)
        {
            return;
        }

        var primary = GetPrimaryParticipant(record.Participants);
        var primaryInfo = primary != null ? $"Primary={primary.ParticipantName}/{primary.AccountName}" : "Primary=None";
        var summary = $"[ActivityTracking] {record.TimestampUtc:O}: {record.CreatureType} '{record.CreatureName}' killed at {record.Map} {record.Location} region={record.RegionName}. {primaryInfo}. Participants={record.Participants.Count}.";

        try
        {
            Utility.PushColor(ConsoleColor.Yellow);
            _logger.Information(summary);
        }
        finally
        {
            Utility.PopColor();
        }

        try
        {
            Utility.PushColor(ConsoleColor.Cyan);
            foreach (var participant in record.Participants)
            {
                _logger.Information(
                    "  {ParticipantName}/{AccountName} ({ParticipantType}) dmg={Damage} lootRight={HasLootRight}",
                    participant.ParticipantName,
                    participant.AccountName,
                    participant.ParticipantType,
                    participant.Damage,
                    participant.HasLootRight
                );
            }
        }
        finally
        {
            Utility.PopColor();
        }

        foreach (var ns in NetState.Instances)
        {
            if (ns.Mobile is PlayerMobile pm && pm.AccessLevel >= AccessLevel.Counselor)
            {
                pm.SendMessage(0x35, summary);

                foreach (var participant in record.Participants)
                {
                    var credit = participant.IsBardProvocationCredit ? " bardCredit=True" : string.Empty;
                    pm.SendMessage(0x35, $"  {participant.ParticipantName}({participant.ParticipantType}) acct={participant.AccountName} dmg={participant.Damage} lootRight={participant.HasLootRight}{credit}");
                }
            }
        }
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    private static CreatureKillParticipant GetPrimaryParticipant(IReadOnlyList<CreatureKillParticipant> participants)
    {
        CreatureKillParticipant primary = null;

        for (var i = 0; i < participants.Count; i++)
        {
            var participant = participants[i];

            if (primary == null || participant.Damage > primary.Damage)
            {
                primary = participant;
            }
        }

        return primary;
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: colored death and gold debug output */
    private static void WriteDeathDebug(PlayerMobile player, ActivityTrackingPlayerData data)
    {
        var accountName = player.Account?.Username ?? "Unknown";
        var summary = $"[ActivityTracking] {DateTime.UtcNow:O}: Player {player.Name}/{accountName}/{data.PlayerSerial} died to {data.LastKillerName} ({data.LastKillerType}/{data.LastKillerSerial}) at {data.LastDeathMap} {data.LastDeathLocation} region={data.LastDeathRegion}.";

        try
        {
            Utility.PushColor(ConsoleColor.Red);
            _logger.Information(summary);
        }
        finally
        {
            Utility.PopColor();
        }

        foreach (var ns in NetState.Instances)
        {
            if (ns.Mobile is PlayerMobile pm && pm.AccessLevel >= AccessLevel.Counselor)
            {
                pm.SendMessage(0x35, summary);
            }
        }
    }

    private static void WriteGoldDebug(
        PlayerMobile player,
        int amount,
        ActivityGoldSource source,
        uint sourceSerial,
        string sourceName,
        string sourceType,
        ActivityLocation location,
        IReadOnlyList<VendorSaleLine> saleLines
    )
    {
        var accountName = player.Account?.Username ?? "Unknown";
        var summary = $"[ActivityTracking] {DateTime.UtcNow:O}: Player {player.Name}/{accountName}/{player.Serial.Value} earned {amount} gold from {source} ({sourceName}/{sourceType}/{sourceSerial}) at {location.Map} {location.Location} region={location.RegionName}.";

        try
        {
            Utility.PushColor(ConsoleColor.DarkYellow);
            _logger.Information(summary);

            if (source == ActivityGoldSource.NpcVendorSale && saleLines?.Count > 0)
            {
                for (var i = 0; i < saleLines.Count; i++)
                {
                    var line = saleLines[i];
                    _logger.Information(
                        "  Sold {Quantity}x {ItemName} ({ItemType}, {ItemSerial}) at {UnitPrice} each for {TotalPrice} gold",
                        line.Quantity,
                        line.ItemName,
                        line.ItemType,
                        line.ItemSerial,
                        line.UnitPrice,
                        line.TotalPrice
                    );
                }
            }
        }
        finally
        {
            Utility.PopColor();
        }

        foreach (var ns in NetState.Instances)
        {
            if (ns.Mobile is PlayerMobile pm && pm.AccessLevel >= AccessLevel.Counselor)
            {
                pm.SendMessage(0x35, summary);

                if (source == ActivityGoldSource.NpcVendorSale && saleLines?.Count > 0)
                {
                    for (var i = 0; i < saleLines.Count; i++)
                    {
                        var line = saleLines[i];
                        pm.SendMessage(0x35, $"  Sold {line.Quantity}x {line.ItemName} ({line.ItemType}/{line.ItemSerial}) at {line.UnitPrice} each for {line.TotalPrice} gold");
                    }
                }
            }
        }
    }

    private static void WriteGoldSpentDebug(
        PlayerMobile player,
        int amount,
        ActivityGoldSink sink,
        uint sinkSerial,
        string sinkName,
        string sinkType,
        ActivityLocation location,
        IReadOnlyList<VendorSaleLine> purchaseLines
    )
    {
        var accountName = player.Account?.Username ?? "Unknown";
        var summary = $"[ActivityTracking] {DateTime.UtcNow:O}: Player {player.Name}/{accountName}/{player.Serial.Value} spent {amount} gold on {sink} ({sinkName}/{sinkType}/{sinkSerial}) at {location.Map} {location.Location} region={location.RegionName}.";

        try
        {
            Utility.PushColor(ConsoleColor.Magenta);
            _logger.Information(summary);

            if (purchaseLines?.Count > 0)
            {
                for (var i = 0; i < purchaseLines.Count; i++)
                {
                    var line = purchaseLines[i];
                    _logger.Information(
                        "  Bought {Quantity}x {ItemName} ({ItemType}, {ItemSerial}) at {UnitPrice} each for {TotalPrice} gold",
                        line.Quantity,
                        line.ItemName,
                        line.ItemType,
                        line.ItemSerial,
                        line.UnitPrice,
                        line.TotalPrice
                    );
                }
            }
        }
        finally
        {
            Utility.PopColor();
        }
    }

    private static void WritePlayerVendorSaleDebug(
        PlayerMobile buyer,
        PlayerMobile owner,
        PlayerVendor vendor,
        VendorSaleLine line
    )
    {
        var summary = $"[ActivityTracking] {DateTime.UtcNow:O}: Player vendor {vendor.Name ?? vendor.GetType().Name}/{vendor.Serial.Value} sold {line.Quantity}x {line.ItemName} ({line.ItemType}/{line.ItemSerial}) to {buyer.Name}/{buyer.Serial.Value} for {line.TotalPrice} gold. Owner={owner?.Name ?? "Unknown"}/{owner?.Serial.Value ?? 0}.";

        WriteEconomyDebug(summary);
    }

    private static void WriteEconomyDebug(string summary)
    {
        try
        {
            Utility.PushColor(ConsoleColor.DarkCyan);
            _logger.Information(summary);
        }
        finally
        {
            Utility.PopColor();
        }
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    private static void WriteActivityDebug(PlayerMobile player, string detail)
    {
        if (player == null || string.IsNullOrWhiteSpace(detail))
        {
            return;
        }

        var accountName = player.Account?.Username ?? "Unknown";
        var summary = $"[ActivityTracking] {DateTime.UtcNow:O}: Player {player.Name}/{accountName}/{player.Serial.Value} {detail}.";

        try
        {
            Utility.PushColor(ConsoleColor.Blue);
            _logger.Information(summary);
        }
        finally
        {
            Utility.PopColor();
        }

        foreach (var ns in NetState.Instances)
        {
            if (ns.Mobile is PlayerMobile pm && pm.AccessLevel >= AccessLevel.Counselor)
            {
                pm.SendMessage(0x35, summary);
            }
        }
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: colored skill milestone debug output */
    public static void WriteSkillDebug(PlayerMobile player, SkillName skill, int milestoneLevel)
    {
        if (player == null)
        {
            return;
        }

        var accountName = player.Account?.Username ?? "Unknown";
        var summary = $"[ActivityTracking] {DateTime.UtcNow:O}: Player {player.Name}/{accountName} reached {milestoneLevel} on {skill}.";

        try
        {
            Utility.PushColor(ConsoleColor.Green);
            _logger.Information(summary);
        }
        finally
        {
            Utility.PopColor();
        }

        foreach (var ns in NetState.Instances)
        {
            if (ns.Mobile is PlayerMobile pm && pm.AccessLevel >= AccessLevel.Counselor)
            {
                pm.SendMessage(0x35, summary);
            }
        }
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */

    public sealed class ActivityTrackingPlayerData
    {
        public uint PlayerSerial { get; set; }

        public string PlayerName { get; set; }

        public string AccountName { get; set; }

        public DateTime FirstSeenUtc { get; set; }

        public DateTime LastUpdatedUtc { get; set; }

        public int TotalKills { get; set; }

        public int TotalDeaths { get; set; }

        public int TotalCraftingActions { get; set; }

        public int TotalCraftedQuantity { get; set; }

        public int TotalFishingCatches { get; set; }

        public int TotalTreasureMapChestsOpened { get; set; }

        public int TotalMiningMaterials { get; set; }

        public int TotalLumberMaterials { get; set; }

        public int TotalDungeonTreasureChestsOpened { get; set; }

        public long TotalGoldEarned { get; set; }

        public long TotalMonsterGoldLooted { get; set; }

        public long TotalTreasureMapChestGoldLooted { get; set; }

        public long TotalDungeonTreasureChestGoldLooted { get; set; }

        public long TotalNpcVendorGoldEarned { get; set; }

        public long TotalGoldSpent { get; set; }

        public long TotalNpcVendorGoldSpent { get; set; }

        public long TotalPlayerVendorPurchaseGold { get; set; }

        public long TotalPlayerVendorSales { get; set; }

        public long TotalPlayerVendorCommissions { get; set; }

        public long LastKnownBankBalance { get; set; }

        public DateTime LastBankBalanceUtc { get; set; }

        public string LastCreatureKilled { get; set; }

        public DateTime LastDeathUtc { get; set; }

        public string LastDeathRegion { get; set; }

        public string LastDeathMap { get; set; }

        public Point3D LastDeathLocation { get; set; }

        public uint LastKillerSerial { get; set; }

        public string LastKillerName { get; set; }

        public string LastKillerType { get; set; }

        public Dictionary<string, int> MonsterKills { get; set; } = new();

        public Dictionary<string, List<SkillMilestoneRecord>> SkillMilestones { get; set; } = new();

        public Dictionary<string, int> MiningMaterialTotals { get; set; } = new();

        public Dictionary<string, int> LumberMaterialTotals { get; set; } = new();

        public HashSet<string> ExploredRegionNames { get; set; } = new();

        public Dictionary<string, RegionEntryRecord> ExploredRegions { get; set; } = new();

        public HashSet<string> ExploredLocationKeys { get; set; } = new();

        public List<string> Activities { get; set; } = new();
    }

    public sealed class CreatureKillRecord
    {
        public DateTime TimestampUtc { get; set; }
        public uint CreatureSerial { get; set; }
        public string CreatureName { get; set; }
        public string CreatureType { get; set; }
        public Point3D Location { get; set; }
        public string Map { get; set; }
        public string RegionName { get; set; }
        public IReadOnlyList<CreatureKillParticipant> Participants { get; set; } = Array.Empty<CreatureKillParticipant>();
        public uint PrimaryParticipantSerial { get; set; }
    }

    public sealed class CreatureKillParticipant
    {
        public uint ParticipantSerial { get; set; }
        public string ParticipantName { get; set; }
        public string ParticipantType { get; set; }
        public string AccountName { get; set; }
        public int Damage { get; set; }
        public bool HasLootRight { get; set; }
        public bool IsPlayer { get; set; }
        public PlayerMobile? PlayerMobile { get; set; }
        public bool IsBardProvocationCredit { get; set; }
    }

    public sealed class VendorSaleLine
    {
        public uint ItemSerial { get; set; }
        public uint SellerSerial { get; set; }
        public uint BuyerSerial { get; set; }
        public uint VendorSerial { get; set; }
        public string RegionName { get; set; }
        public string Map { get; set; }
        public Point3D Location { get; set; }
        public string ItemType { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public int UnitPrice { get; set; }
        public int TotalPrice { get; set; }
    }

    private sealed class MonsterCorpseGoldRecord
    {
        public uint CreatureSerial { get; set; }
        public string CreatureName { get; set; }
        public string CreatureType { get; set; }
        public string RegionName { get; set; }
        public string Map { get; set; }
        public Point3D Location { get; set; }
        public int RemainingGold { get; set; }
    }

    private sealed class TreasureChestGoldRecord
    {
        public uint ChestSerial { get; set; }
        public string ChestType { get; set; }
        public string RegionName { get; set; }
        public string Map { get; set; }
        public Point3D Location { get; set; }
        public int RemainingGold { get; set; }
    }

    private sealed class ActivityLocation
    {
        public string RegionName { get; set; }
        public string Map { get; set; }
        public Point3D Location { get; set; }
    }

    private enum ActivityGoldSource
    {
        MonsterCorpse,
        NpcVendorSale,
        TreasureMapChestGold,
        DungeonTreasureChestGold
    }

    private enum ActivityGoldSink
    {
        NpcVendorPurchase
    }

    public sealed class AccountBalanceRecord
    {
        public string AccountName { get; set; }
        public uint LastPlayerSerial { get; set; }
        public string LastPlayerName { get; set; }
        public long Balance { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }

    /* BEGIN ACTIVITY TRACKING CUSTOMIZATION: cached world currency snapshot for status reporting */
    private sealed class CurrencySnapshot
    {
        public DateTime LastUpdatedUtc { get; set; }
        public long GoldItemTotal { get; set; }
        public long BankCheckTotal { get; set; }
        public long VirtualAccountGoldTotal { get; set; }
        public int GoldStackCount { get; set; }
        public int BankCheckCount { get; set; }
        public int VirtualAccountCount { get; set; }

        public long TotalCurrency => GoldItemTotal + BankCheckTotal + VirtualAccountGoldTotal;
    }
    /* END ACTIVITY TRACKING CUSTOMIZATION */
}

public sealed class SkillMilestoneRecord
{
    public int MilestoneLevel { get; set; }
    public DateTime ReachedUtc { get; set; }
}

public sealed class RegionEntryRecord
{
    public string RegionName { get; set; }
    public DateTime FirstEnteredUtc { get; set; }
    public string Map { get; set; }
    public Point3D Location { get; set; }
}
