using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Server.Custom.Systems.AchievementSystem;
using Server.Custom.Systems.AIIntegration;
using Server.Items;
using Server.Logging;
using Server.Misc;
using Server.Mobiles;
using Server.Regions;

namespace Server.Custom.Systems.VirtualEcology;

public enum WorldFactKind
{
    PlayerDeath,
    ServerFirstAchievement
}

public sealed class WorldFact
{
    public WorldFactKind Kind { get; set; }
    public string AchievementId { get; set; }
    public uint PlayerSerial { get; set; }
    public string PlayerName { get; set; }
    public string Detail { get; set; }
    public string Location { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class TownChatterCache
{
    public string Town { get; set; }
    public string Theme { get; set; }
    public int RequestedCount { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<string> Lines { get; } = new();
    public List<string> RejectedLines { get; } = new();
    public Dictionary<string, DateTime> NextLineUse { get; } = new(StringComparer.Ordinal);
}

public static class TownChatterService
{
    public static int DefaultLineCount => VirtualEcologySettings.DefaultLineCount;
    public static int MaxLineCount => VirtualEcologySettings.MaxLineCount;
    public static int AutoTopUpLineCount => VirtualEcologySettings.AutoTopUpLineCount;
    public static TimeSpan AutoTopUpInterval => VirtualEcologySettings.AutoTopUpInterval;
    public static TimeSpan CatchUpTopUpInterval => VirtualEcologySettings.CatchUpTopUpInterval;

    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(TownChatterService));
    private static int MaxGenerationAttempts => VirtualEcologySettings.MaxGenerationAttempts;
    private static int MaxRejectedLineCount => VirtualEcologySettings.MaxRejectedLineCount;
    private static int MaxCachedDialogueLength => VirtualEcologySettings.MaxCachedDialogueLength;
    private static int MaxDynamicDialogueLength => VirtualEcologySettings.MaxDynamicDialogueLength;
    private static int MaxRecentFactCount => VirtualEcologySettings.MaxRecentFactCount;
    private static TimeSpan PlayerDeathFactMergeWindow => VirtualEcologySettings.PlayerDeathFactMergeWindow;
    private static TimeSpan PlayerDeathFactCooldown => VirtualEcologySettings.PlayerDeathFactCooldown;
    private static double MovementFactCommentChance => VirtualEcologySettings.MovementFactCommentChance;
    private static double MovementFlavorCommentChance => VirtualEcologySettings.MovementFlavorCommentChance;
    private static bool AllowStaffMovementTriggers => VirtualEcologySettings.AllowStaffMovementTriggers;
    private static TimeSpan PlayerLiveCommentCooldown => VirtualEcologySettings.PlayerLiveCommentCooldown;
    private static TimeSpan NpcLiveCommentCooldown => VirtualEcologySettings.NpcLiveCommentCooldown;
    private static TimeSpan LineReuseCooldown => VirtualEcologySettings.LineReuseCooldown;
    private static TimeSpan RecentFactMaxAge => VirtualEcologySettings.RecentFactMaxAge;
    private static TimeSpan ServerFirstAnnouncementMaxAge => VirtualEcologySettings.ServerFirstAnnouncementMaxAge;
    private static TimeSpan ServerFirstFactSyncInterval => VirtualEcologySettings.ServerFirstFactSyncInterval;

    private static readonly Dictionary<string, TownChatterCache> _caches =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<WorldFact> _recentFacts = new();
    private static readonly Dictionary<Serial, DateTime> _nextPlayerLiveComment = new();
    private static readonly Dictionary<Serial, DateTime> _nextNpcLiveComment = new();
    private static TimerExecutionToken _autoTopUpToken;
    private static bool _autoTopUpRunning;
    private static bool _movementHooked;
    private static DateTime _nextServerFirstFactSyncUtc;
    private static int _nextServerFirstAnnouncementIndex;
    private static readonly Dictionary<string, Region> _townRegions = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> _approvedProperNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Britain",
            "Minoc",
            "Yew",
            "Moonglow",
            "Trinsic",
            "Vesper",
            "Skara",
            "Brae",
            "Jhelom",
            "Magincia",
            "Buccaneer's",
            "Den",
            "Cove",
            "Nujel'm",
            "NuJel'm",
            "Ocllo",
            "Serpent's",
            "Hold",
            "Wind",
            "Britannia",
            "Britannian",
            "Britainia",
            "Arena",
            "Blackthorn",
            "Blackthorns",
            "Guild",
            "Lycaeum",
            "Sailors",
            "Wilderness"
        };

    private static readonly HashSet<string> _properNameSuffixes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Bridge",
            "River",
            "Road",
            "Roads",
            "Forest",
            "Woods",
            "Shrine",
            "Abbey",
            "Castle",
            "Bank",
            "Docks",
            "Market",
            "Gate",
            "Gates",
            "Harbor",
            "Harbour",
            "Canal",
            "Canals",
            "Ferry",
            "Farms",
            "Court",
            "Courts",
            "Library",
            "Observatory",
            "Gardens",
            "Estates"
        };

    private static readonly HashSet<string> _sentenceStartWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "A",
            "An",
            "As",
            "Do",
            "Don't",
            "Folks",
            "Heard",
            "I",
            "I've",
            "If",
            "My",
            "No",
            "Old",
            "Our",
            "Some",
            "Someone",
            "The",
            "They",
            "There",
            "Those",
            "Travelers",
            "Watch",
            "We",
            "You"
        };

    private static readonly HashSet<string> _allowedCapitalizedCommonWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "I",
            "I'd",
            "I'll",
            "I'm",
            "I've"
        };

    private static readonly HashSet<string> _tradeGoodProperAdjectiveProducts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ale",
            "bread",
            "cloth",
            "flour",
            "mead",
            "pie",
            "silk",
            "tea",
            "wine",
            "wool"
        };

    private static readonly Dictionary<string, string> _displayNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["bucsden"] = "Buccaneer's Den",
            ["nujelm"] = "Nujel'm",
            ["serpentshold"] = "Serpent's Hold",
            ["skara"] = "Skara Brae"
        };

    private static readonly Dictionary<string, string> _defaultThemes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["britain"] = "market gossip, guards, merchants, travelers, and daily city life",
            ["bucsden"] = "pirates, thieves, smugglers, taverns, docks, and lawless island gossip",
            ["cove"] = "small-town gossip, mountain walls, docks, healers, and rumors near the orc fort",
            ["minoc"] = "miners, ore, smiths, mountain roads, and work songs",
            ["yew"] = "woodsmen, rangers, old trees, courts, and quiet forest paths",
            ["moonglow"] = "scholars, mages, libraries, telescopes, and island rumors",
            ["nujelm"] = "desert island nobles, jewelers, palaces, docks, and arena talk",
            ["ocllo"] = "island farms, docks, training yards, healers, and quiet southern roads",
            ["serpentshold"] = "fortress guards, knights, training yards, docks, and military gossip",
            ["trinsic"] = "paladins, guards, honor, harbor traffic, and training yards",
            ["vesper"] = "bridges, canals, traders, sailors, and dockside talk",
            ["wind"] = "hidden mage city, arcane shops, underground halls, and scholarly gossip",
            ["skara"] = "rangers, farms, ferries, musicians, and island weather",
            ["jhelom"] = "duelists, fighters, sailors, arenas, and tavern boasts",
            ["magincia"] = "pride, gardens, nobles, merchants, and polished streets",
            ["wilderness"] = "roadside camps, forests, ruins, patrols, weather, and wilderness rumors"
        };

    private static readonly Dictionary<string, Point3D> _townCenters =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["britain"] = new Point3D(1495, 1629, 10),
            ["bucsden"] = new Point3D(2711, 2160, 0),
            ["cove"] = new Point3D(2228, 1200, 0),
            ["minoc"] = new Point3D(2477, 413, 15),
            ["yew"] = new Point3D(633, 858, 0),
            ["moonglow"] = new Point3D(4442, 1122, 5),
            ["nujelm"] = new Point3D(3730, 1260, 0),
            ["ocllo"] = new Point3D(3650, 2520, 0),
            ["serpentshold"] = new Point3D(3000, 3400, 15),
            ["trinsic"] = new Point3D(1828, 2821, 0),
            ["vesper"] = new Point3D(2899, 676, 0),
            ["wind"] = new Point3D(5225, 175, 15),
            ["skara"] = new Point3D(576, 2200, 0),
            ["jhelom"] = new Point3D(1378, 3825, 0),
            ["magincia"] = new Point3D(3728, 2164, 20),
            ["wilderness"] = new Point3D(1520, 1450, 0)
        };

    private static readonly Dictionary<string, string> _townLandmarks =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["britain"] = "castle, market, bank, docks, farms, guard posts, blacksmith shops, inns",
            ["bucsden"] = "Cutlass Smithing, Pirate's Plunder, bath house, bank, docks, provisioner",
            ["cove"] = "watch tower, town gate, bank, docks, healer, nearby orc fort",
            ["minoc"] = "mines, forges, tinkers' shops, mountain roads, gypsy camp, bank",
            ["yew"] = "abbey, courts, farms, wineries, deep woods, ranger paths",
            ["moonglow"] = "lycaeum, mage shops, observatory, telescope, docks, island roads",
            ["nujelm"] = "palace, arena, bank, jewelers, docks, desert streets",
            ["ocllo"] = "farms, bank, docks, healers, training yards, island roads",
            ["serpentshold"] = "fortress, guard posts, training yards, docks, armories",
            ["trinsic"] = "paladin halls, training yards, harbor, guard posts, south gate",
            ["vesper"] = "bridges, canals, docks, provisioners, shipwrights, taverns",
            ["wind"] = "hidden entrance, mage shops, bank, park, underground halls",
            ["skara"] = "ferry, farms, ranger guild, docks, music hall, sheep fields",
            ["jhelom"] = "dueling pits, fighter halls, docks, taverns, practice yards",
            ["magincia"] = "gardens, estates, market, docks, bank, polished streets",
            ["wilderness"] = "old roads, campsites, shrines, ruined walls, deep woods, river crossings"
        };

    public static string[] DefaultAreas { get; } =
    {
        "britain",
        "bucsden",
        "cove",
        "minoc",
        "yew",
        "moonglow",
        "nujelm",
        "ocllo",
        "serpentshold",
        "trinsic",
        "vesper",
        "wind",
        "skara",
        "jhelom",
        "magincia",
        "wilderness"
    };

    public static string[] DefaultTowns => DefaultAreas;

    private static readonly string[] _forbiddenFragments =
    {
        " ai ",
        "artificial intelligence",
        "chatbot",
        "language model",
        "server",
        "prompt",
        "token",
        "internet",
        "website",
        "computer",
        "electricity",
        "cellphone",
        "smartphone",
        "television",
        "samurai",
        "ninja",
        "necromancy",
        "spellweaving",
        "imbuing",
        "chivalry",
        "mysticism",
        "certainly",
        "sure,",
        "felucca",
        "trammel",
        "delucia",
        "papua"
    };

    public static IReadOnlyDictionary<string, TownChatterCache> Caches => _caches;
    public static IReadOnlyList<WorldFact> RecentFacts
    {
        get
        {
            RefreshServerFirstFactsFromAchievements(force: true);
            PruneOldFacts();
            TrimTransientFacts();
            return _recentFacts;
        }
    }

    public static void Configure()
    {
        VirtualEcologySettings.Configure();
        VirtualEcologyLocations.Configure();

        if (!_movementHooked)
        {
            EventSink.Movement += OnMovement;
            _movementHooked = true;
        }

        RestartAutoTopUpTimer();
    }

    public static void RestartAutoTopUpTimer()
    {
        if (_autoTopUpToken.Running)
        {
            _autoTopUpToken.Cancel();
        }

        // Keeps the town chatter pools moving while the AI feature is enabled.
        Timer.StartTimer(GetNextAutoTopUpInterval(), AutoTopUp_OnTick, out _autoTopUpToken);
    }

    public static async ValueTask<TownChatterCache> GenerateAsync(string town, int count = 0, string theme = null)
    {
        town = NormalizeTown(town);
        count = count <= 0 ? DefaultLineCount : count;
        count = Math.Clamp(count, 3, MaxLineCount);

        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = GetDefaultTheme(town);
        }

        var cache = new TownChatterCache
        {
            Town = ToDisplayName(town),
            Theme = theme,
            RequestedCount = count,
            GeneratedAt = Core.Now
        };

        var generated = await GenerateLinesAsync(town, theme, count);
        AppendLines(cache, generated.Accepted);
        AppendRejectedLines(cache, generated.Rejected);

        if (cache.Lines.Count == 0)
        {
            cache.Lines.Add("No acceptable chatter lines were generated. Try a narrower town/theme prompt.");
        }

        _caches[town] = cache;
        return cache;
    }

    public static async ValueTask<TownChatterCache> TopUpAsync(
        string town,
        int count = 0,
        string theme = null
    )
    {
        town = NormalizeTown(town);
        count = count <= 0 ? AutoTopUpLineCount : count;
        count = Math.Clamp(count, 1, MaxLineCount);

        if (!TryGetCache(town, out var cache))
        {
            cache = new TownChatterCache
            {
                Town = ToDisplayName(town),
                Theme = string.IsNullOrWhiteSpace(theme) ? GetDefaultTheme(town) : theme,
                RequestedCount = DefaultLineCount,
                GeneratedAt = Core.Now
            };

            _caches[town] = cache;
        }
        else if (!string.IsNullOrWhiteSpace(theme))
        {
            cache.Theme = theme;
        }

        var generated = await GenerateLinesAsync(town, cache.Theme, count);
        AppendLines(cache, generated.Accepted);
        AppendRejectedLines(cache, generated.Rejected);
        cache.GeneratedAt = Core.Now;

        return cache;
    }

    public static ValueTask<TownChatterCache> RegenerateAsync(string town)
    {
        if (!TryGetCache(town, out var cache))
        {
            return GenerateAsync(town);
        }

        return GenerateAsync(cache.Town, cache.RequestedCount, cache.Theme);
    }

    public static async ValueTask<List<TownChatterCache>> RegenerateAllAsync(int count = 0)
    {
        count = count <= 0 ? DefaultLineCount : count;
        var caches = new List<TownChatterCache>(DefaultAreas.Length);

        for (var i = 0; i < DefaultAreas.Length; i++)
        {
            caches.Add(await GenerateAsync(DefaultAreas[i], count));
        }

        return caches;
    }

    public static async ValueTask<List<TownChatterCache>> TopUpAllAsync(int count = 0)
    {
        count = count <= 0 ? AutoTopUpLineCount : count;
        var caches = new List<TownChatterCache>(DefaultAreas.Length);

        for (var i = 0; i < DefaultAreas.Length; i++)
        {
            if (TryGetCache(DefaultAreas[i], out var existing) && existing.Lines.Count >= MaxLineCount)
            {
                continue;
            }

            caches.Add(await TopUpAsync(DefaultAreas[i], count));
        }

        return caches;
    }

    public static async ValueTask<string> GenerateDynamicReactionAsync(string town, string nearbyContext = null)
    {
        town = NormalizeTown(town);

        var prompt =
            $"Area: {ToDisplayName(town)}\n" +
            BuildWorldContext(town, includePeople: false, includeTransient: true) +
            (!string.IsNullOrWhiteSpace(nearbyContext) ? $"Nearby context: {nearbyContext.Trim()}\n" : string.Empty) +
            "Generate one short first-person ambient reaction from a town NPC discussing this area. " +
            "It may react to the current time, weather, local rumors, nearby shops, or supplied nearby context. " +
            "Do not include the speaker's name, job title, role, or any other personal name. " +
            "Do not write stage directions, narration, or third-person text. " +
            $"Keep it under {MaxDynamicDialogueLength} characters. " +
            "Do not imply a quest, task, reward, delivery, missing person, or player objective. " +
            "Return only the dialogue line, without labels or quote marks.";

        var response = await AIIntegrationService.GenerateChatterPoolAsync(prompt);
        var lines = ParseLines(response, 1, town);

        return lines.Accepted.Count > 0
            ? TrimDialogueLine(lines.Accepted[0], MaxDynamicDialogueLength)
            : TrimDialogueLine(response, MaxDynamicDialogueLength);
    }

    public static void RecordPlayerDeath(PlayerMobile player)
    {
        if (player == null || string.IsNullOrWhiteSpace(player.Name))
        {
            return;
        }

        var location = GetRegionDisplayName(player.Region);
        var killer = player.LastKiller;
        var detail = "died";

        if (killer is BaseCreature creature)
        {
            var master = creature.GetMaster();

            if (master?.Player == true)
            {
                detail = "was murdered";
            }
            else
            {
                detail = $"was killed by {GetCreatureDisplayName(creature)}";
            }
        }
        else if (killer?.Player == true)
        {
            detail = "was murdered";
        }

        AddOrUpdatePlayerDeathFact(new WorldFact
        {
            Kind = WorldFactKind.PlayerDeath,
            PlayerSerial = player.Serial.Value,
            PlayerName = player.Name,
            Detail = detail,
            Location = location,
            CreatedUtc = DateTime.UtcNow
        });
    }

    public static void RecordReportedMurder(PlayerMobile victim, Mobile killer)
    {
        if (victim == null || killer == null || string.IsNullOrWhiteSpace(victim.Name) ||
            string.IsNullOrWhiteSpace(killer.Name))
        {
            return;
        }

        var location = GetRegionDisplayName(victim.Region);

        for (var i = _recentFacts.Count - 1; i >= 0; i--)
        {
            var fact = _recentFacts[i];

            if (fact.Kind != WorldFactKind.PlayerDeath ||
                !IsSamePlayerFact(fact, victim.Serial.Value, victim.Name) ||
                fact.CreatedUtc < DateTime.UtcNow - PlayerDeathFactMergeWindow)
            {
                continue;
            }

            fact.Detail = $"was murdered by {killer.Name}";
            fact.Location = string.IsNullOrWhiteSpace(fact.Location) ? location : fact.Location;
            return;
        }

        AddOrUpdatePlayerDeathFact(new WorldFact
        {
            Kind = WorldFactKind.PlayerDeath,
            PlayerSerial = victim.Serial.Value,
            PlayerName = victim.Name,
            Detail = $"was murdered by {killer.Name}",
            Location = location,
            CreatedUtc = DateTime.UtcNow
        });
    }

    public static void RecordServerFirstAchievement(string playerName, string skillDisplayName)
    {
        RecordServerFirstAchievement(null, playerName, skillDisplayName);
    }

    public static void RecordServerFirstAchievement(string achievementId, string playerName, string skillDisplayName)
    {
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(skillDisplayName))
        {
            return;
        }

        AddWorldFact(new WorldFact
        {
            Kind = WorldFactKind.ServerFirstAchievement,
            AchievementId = achievementId?.Trim(),
            PlayerSerial = 0,
            PlayerName = playerName.Trim(),
            Detail = skillDisplayName.Trim(),
            Location = null,
            CreatedUtc = DateTime.UtcNow
        });

        ForceBeginServerFirstTownCrierAnnouncements();
    }

    public static string GenerateLatestFactComment()
    {
        RefreshServerFirstFactsFromAchievements(force: true);
        PruneOldFacts();
        TrimTransientFacts();

        if (_recentFacts.Count == 0)
        {
            return "No recent chatter facts are recorded.";
        }

        var fact = _recentFacts[^1];
        var line = BuildFactComment(fact);

        return string.IsNullOrWhiteSpace(line) ? "The latest fact could not be rendered." : line;
    }

    public static bool TryGetServerFirstAnnouncement(out string[] lines)
    {
        lines = null;

        RefreshServerFirstFactsFromAchievements();
        PruneOldFacts();
        TrimTransientFacts();

        var now = DateTime.UtcNow;
        var eligibleCount = 0;

        for (var i = 0; i < _recentFacts.Count; i++)
        {
            if (IsEligibleServerFirstAnnouncement(_recentFacts[i], now))
            {
                eligibleCount++;
            }
        }

        if (eligibleCount == 0)
        {
            _nextServerFirstAnnouncementIndex = 0;
            return false;
        }

        if (_nextServerFirstAnnouncementIndex >= eligibleCount)
        {
            _nextServerFirstAnnouncementIndex = 0;
        }

        var targetIndex = _nextServerFirstAnnouncementIndex++;
        var currentIndex = 0;

        for (var i = 0; i < _recentFacts.Count; i++)
        {
            var fact = _recentFacts[i];

            if (!IsEligibleServerFirstAnnouncement(fact, now))
            {
                continue;
            }

            if (currentIndex++ != targetIndex)
            {
                continue;
            }

            lines = BuildServerFirstAnnouncementLines(fact);
            return lines?.Length > 0;
        }

        _nextServerFirstAnnouncementIndex = 0;
        return false;
    }

    public static bool HasActiveServerFirstAnnouncements()
    {
        RefreshServerFirstFactsFromAchievements();
        PruneOldFacts();
        TrimTransientFacts();

        return HasEligibleServerFirstAnnouncement(DateTime.UtcNow);
    }

    public static void SerializePersistence(IGenericWriter writer)
    {
        RefreshServerFirstFactsFromAchievements(force: true);
        PruneOldFacts();
        TrimTransientFacts();

        writer.WriteEncodedInt(5); // data version
        writer.WriteEncodedInt(_caches.Count);

        foreach (var entry in _caches)
        {
            var cache = entry.Value;

            writer.Write(entry.Key);
            writer.Write(cache.Town);
            writer.Write(cache.Theme);
            writer.WriteEncodedInt(cache.RequestedCount);
            writer.Write(cache.GeneratedAt);
            WriteStringList(writer, cache.Lines);
            WriteStringList(writer, cache.RejectedLines);
            WriteLineCooldowns(writer, cache);
        }

        writer.WriteEncodedInt(_recentFacts.Count);

        for (var i = 0; i < _recentFacts.Count; i++)
        {
            var fact = _recentFacts[i];

            writer.WriteEnum(fact.Kind);
            writer.Write(fact.AchievementId);
            writer.Write(fact.PlayerSerial);
            writer.Write(fact.PlayerName);
            writer.Write(fact.Detail);
            writer.Write(fact.Location);
            writer.Write(fact.CreatedUtc);
        }

        writer.WriteEncodedInt(_nextServerFirstAnnouncementIndex);
    }

    public static void DeserializePersistence(IGenericReader reader, int version)
    {
        _caches.Clear();
        _recentFacts.Clear();

        var dataVersion = version >= 1 ? reader.ReadEncodedInt() : 0;

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var key = NormalizeTown(reader.ReadString());
            var cache = new TownChatterCache
            {
                Town = reader.ReadString(),
                Theme = reader.ReadString(),
                RequestedCount = Math.Clamp(reader.ReadEncodedInt(), 3, MaxLineCount),
                GeneratedAt = reader.ReadDateTime()
            };

            ReadStringList(reader, cache.Lines, MaxLineCount);
            ReadStringList(reader, cache.RejectedLines, MaxRejectedLineCount);

            if (dataVersion >= 5)
            {
                ReadLineCooldowns(reader, cache);
            }

            if (string.IsNullOrWhiteSpace(cache.Town))
            {
                cache.Town = ToDisplayName(key);
            }

            if (string.IsNullOrWhiteSpace(cache.Theme))
            {
                cache.Theme = GetDefaultTheme(key);
            }

            _caches[key] = cache;
        }

        if (dataVersion < 1)
        {
            return;
        }

        var factCount = reader.ReadEncodedInt();

        for (var i = 0; i < factCount; i++)
        {
            var fact = new WorldFact
            {
                Kind = reader.ReadEnum<WorldFactKind>(),
                AchievementId = dataVersion >= 2 ? reader.ReadString() : null,
                PlayerSerial = dataVersion >= 3 ? reader.ReadUInt() : 0,
                PlayerName = reader.ReadString(),
                Detail = reader.ReadString(),
                Location = reader.ReadString(),
                CreatedUtc = reader.ReadDateTime()
            };

            if (!string.IsNullOrWhiteSpace(fact.PlayerName))
            {
                _recentFacts.Add(fact);
            }
        }

        PruneOldFacts();
        TrimTransientFacts();

        _nextServerFirstAnnouncementIndex = dataVersion >= 4 ? Math.Max(0, reader.ReadEncodedInt()) : 0;

        if (HasEligibleServerFirstAnnouncement(DateTime.UtcNow))
        {
            ForceBeginServerFirstTownCrierAnnouncements();
        }
    }

    public static void RefreshServerFirstFactsFromAchievements(bool force = false)
    {
        var now = DateTime.UtcNow;

        if (!force && _nextServerFirstFactSyncUtc > now)
        {
            return;
        }

        _nextServerFirstFactSyncUtc = now + ServerFirstFactSyncInterval;

        var records = AchievementService.GetServerFirstRecords();

        for (var i = _recentFacts.Count - 1; i >= 0; i--)
        {
            if (_recentFacts[i].Kind == WorldFactKind.ServerFirstAchievement)
            {
                _recentFacts.RemoveAt(i);
            }
        }

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];

            if (string.IsNullOrWhiteSpace(record.AchievementId) ||
                string.IsNullOrWhiteSpace(record.PlayerName) ||
                string.IsNullOrWhiteSpace(record.SkillDisplayName))
            {
                continue;
            }

            _recentFacts.Add(new WorldFact
            {
                Kind = WorldFactKind.ServerFirstAchievement,
                AchievementId = record.AchievementId,
                PlayerSerial = record.PlayerSerial,
                PlayerName = record.PlayerName,
                Detail = record.SkillDisplayName,
                Location = null,
                CreatedUtc = record.AchievedUtc
            });
        }

        _recentFacts.Sort(static (a, b) => a.CreatedUtc.CompareTo(b.CreatedUtc));

        if (HasEligibleServerFirstAnnouncement(now))
        {
            ForceBeginServerFirstTownCrierAnnouncements();
        }
    }

    public static bool TryGetCache(string town, out TownChatterCache cache)
    {
        return _caches.TryGetValue(NormalizeTown(town), out cache);
    }

    public static bool DeleteLine(string town, int index, out string removedLine)
    {
        removedLine = null;

        if (!TryGetCache(town, out var cache) || index < 1 || index > cache.Lines.Count)
        {
            return false;
        }

        removedLine = cache.Lines[index - 1];
        cache.Lines.RemoveAt(index - 1);
        cache.NextLineUse.Remove(removedLine);
        return true;
    }

    public static bool Clear(string town)
    {
        return _caches.Remove(NormalizeTown(town));
    }

    public static int ClearAll()
    {
        var count = _caches.Count;
        _caches.Clear();
        return count;
    }

    public static string NormalizeTown(string town)
    {
        return string.IsNullOrWhiteSpace(town) ? "britain" : town.Trim().ToLowerInvariant();
    }

    public static string GetDefaultTheme(string town)
    {
        return _defaultThemes.TryGetValue(NormalizeTown(town), out var theme)
            ? theme
            : "local gossip, work, weather, travelers, and regional rumors";
    }

    private static async ValueTask<TownChatterParseResult> GenerateLinesAsync(string town, string theme, int count)
    {
        var result = new TownChatterParseResult(count);

        for (var attempt = 1; attempt <= MaxGenerationAttempts && result.Accepted.Count < count; attempt++)
        {
            var needed = count - result.Accepted.Count;
            var prompt = BuildPrompt(town, theme, needed, attempt);
            var response = await AIIntegrationService.GenerateChatterPoolAsync(prompt);
            var lines = ParseLines(response, needed, town);

            for (var i = 0; i < lines.Accepted.Count && result.Accepted.Count < count; i++)
            {
                result.Accepted.Add(lines.Accepted[i]);
            }

            for (var i = 0; i < lines.Rejected.Count; i++)
            {
                result.Rejected.Add($"Attempt {attempt}: {lines.Rejected[i]}");
            }
        }

        return result;
    }

    private static string BuildPrompt(string town, string theme, int count, int attempt)
    {
        var retryText = attempt <= 1
            ? string.Empty
            : "Previous generated lines were rejected by validation. Be stricter and return only clean ambient dialogue. ";

        return
            $"Area: {ToDisplayName(town)}\n" +
            $"Theme: {theme}\n" +
            $"Generate {count} ambient NPC chatter lines suitable for town NPCs discussing this area. " +
            retryText +
            "Use grounded, low-fantasy Ultima Online Renaissance flavor. " +
            "Return only dialogue a town NPC would actually say aloud. " +
            "Do not number the lines. " +
            "Do not include names, titles, speaker labels, explanations, headers, or quote marks. " +
            "Do not include the speaker's name, job title, role, or any personal names. " +
            "Use first-person or anonymous local gossip only. " +
            $"Keep each line under {MaxDynamicDialogueLength} characters. " +
            "Rules: mining yields ore, ore is smelted into ingots, and anvils are only used for smithing finished items. " +
            "Lumberjacks cut logs, and boards are made from logs. " +
            "Avoid impossible craft/resource claims, modern terms, post-UOR skills/items, and explicit game mechanics. " +
            "Do not invent named places, rivers, shrines, ruins, roads, villages, people, or organizations. " +
            "Only use proper names from the known area, known local details, and allowed rumor destinations. " +
            "Most lines should be local, but some may be secondhand rumors about another allowed town or wilderness. " +
            "Cross-town rumors must name the other town and use wording like 'over in Minoc' or 'from Vesper.' " +
            "Local damage, repairs, shortages, and public works are allowed as rumors. " +
            "Do not offer the player a task, reward, errand, missing-person search, delivery, or objective. " +
            GetAreaPromptRules(town) +
            "\n" +
            BuildWorldContext(town, includePeople: false, includeTransient: false) +
            BuildRumorContext(town);
    }

    private static string GetAreaPromptRules(string town)
    {
        town = NormalizeTown(town);

        if (string.Equals(town, "wilderness", StringComparison.OrdinalIgnoreCase))
        {
            return "Every wilderness line must clearly mention a known road, woods, wilds, forest, shrine, camp, ruin, dungeon, lair, passage, or other configured wilderness landmark. ";
        }

        return string.Empty;
    }

    private static string BuildWorldContext(string town, bool includePeople, bool includeTransient)
    {
        town = NormalizeTown(town);
        var builder = new StringBuilder();

        builder.Append("Known area: ").Append(ToDisplayName(town)).Append('\n');

        if (_townLandmarks.TryGetValue(town, out var landmarks))
        {
            builder.Append("Known local details: ").Append(landmarks).Append('\n');
        }

        builder.Append(VirtualEcologyLocations.BuildPromptContext(town));

        if (includePeople)
        {
            AppendTownNpcContext(builder, town);
        }

        if (includeTransient)
        {
            AppendTransientContext(builder, town);
        }

        return builder.ToString();
    }

    private static string BuildRumorContext(string town)
    {
        town = NormalizeTown(town);
        var builder = new StringBuilder();
        builder.Append("Allowed rumor destinations: ");
        var added = 0;

        for (var i = 0; i < DefaultAreas.Length; i++)
        {
            var area = DefaultAreas[i];

            if (string.Equals(area, town, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (added > 0)
            {
                builder.Append("; ");
            }

            builder.Append(ToDisplayName(area));

            if (_townLandmarks.TryGetValue(area, out var landmarks))
            {
                builder.Append(" (").Append(landmarks).Append(')');
            }

            var locationContext = VirtualEcologyLocations.BuildInlinePromptContext(area);
            if (!string.IsNullOrWhiteSpace(locationContext))
            {
                builder.Append(" [real places: ").Append(locationContext).Append(']');
            }

            added++;
        }

        builder.Append(".\n");
        builder.Append("Example style: I heard the cave veins are running dry over in Minoc.\n");
        return builder.ToString();
    }

    private static void AppendTownNpcContext(StringBuilder builder, string town)
    {
        if (!TryGetTownRegion(town, out var region))
        {
            builder.Append("Real NPC names available: none; avoid personal names.\n");
            return;
        }

        using var mobiles = region.GetMobilesPooled();
        var added = 0;

        for (var i = 0; i < mobiles.Count && added < 5; i++)
        {
            var mobile = mobiles[i];

            if (mobile?.Deleted != false || mobile.Player || mobile.AccessLevel > AccessLevel.Player ||
                string.IsNullOrWhiteSpace(mobile.Name))
            {
                continue;
            }

            var role = GetNpcRole(mobile);
            var gender = mobile.Female ? "female" : "male";

            if (added == 0)
            {
                builder.Append("Real NPC names available: ");
            }
            else
            {
                builder.Append("; ");
            }

            builder.Append(gender).Append(' ').Append(role).Append(" named ").Append(mobile.Name.Trim());
            added++;
        }

        builder.Append(added == 0 ? "Real NPC names available: none; avoid personal names.\n" : "\n");
    }

    private static void AppendTransientContext(StringBuilder builder, string town)
    {
        var point = GetTownCenter(town);
        var map = Map.Felucca;

        Clock.GetTime(map, point.X, point.Y, out int hours, out int minutes);
        builder.Append("Current local time: ").Append(GetTimeDescription(hours)).Append(" (")
            .Append(hours.ToString("00")).Append(':').Append(minutes.ToString("00")).Append(").\n");
        builder.Append("Current local weather: ").Append(GetWeatherDescription(map, point)).Append(".\n");
    }

    private static bool TryGetTownRegion(string town, out Region region)
    {
        town = NormalizeTown(town);

        if (_townRegions.TryGetValue(town, out region))
        {
            return region != null;
        }

        var displayName = ToDisplayName(town);

        for (var i = 0; i < Region.Regions.Count; i++)
        {
            var candidate = Region.Regions[i];

            if (candidate is TownRegion &&
                string.Equals(candidate.Name, displayName, StringComparison.OrdinalIgnoreCase))
            {
                _townRegions[town] = candidate;
                region = candidate;
                return true;
            }
        }

        _townRegions[town] = null;
        region = null;
        return false;
    }

    private static string GetNpcRole(Mobile mobile)
    {
        if (mobile is BaseVendor && !string.IsNullOrWhiteSpace(mobile.Title))
        {
            return CleanRole(mobile.Title);
        }

        return mobile.GetType().Name;
    }

    private static string CleanRole(string value)
    {
        value = value.Trim();
        return value.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ? value[4..] : value;
    }

    private static Point3D GetTownCenter(string town)
    {
        return _townCenters.TryGetValue(NormalizeTown(town), out var point) ? point : _townCenters["britain"];
    }

    private static string GetTimeDescription(int hours)
    {
        return hours switch
        {
            >= 20 => "late night",
            >= 16 => "early evening",
            >= 13 => "afternoon",
            >= 12 => "noon",
            >= 8  => "late morning",
            >= 4  => "early morning",
            >= 1  => "middle of the night",
            _     => "witching hour"
        };
    }

    private static string GetWeatherDescription(Map map, Point3D point)
    {
        var list = Weather.GetWeatherList(map);

        if (list == null)
        {
            return "fair";
        }

        for (var i = 0; i < list.Count; i++)
        {
            var weather = list[i];

            for (var j = 0; j < weather.Area.Length; j++)
            {
                if (!weather.Area[j].Contains(point))
                {
                    continue;
                }

                return weather.Temperature < 0 ? "cold, unsettled skies" : "unsettled skies";
            }
        }

        return "fair";
    }

    private static TownChatterParseResult ParseLines(string response, int maxLines, string area)
    {
        var result = new TownChatterParseResult(maxLines);

        if (string.IsNullOrWhiteSpace(response) ||
            response.StartsWith("Ollama request", StringComparison.OrdinalIgnoreCase) ||
            response.StartsWith("AI integration", StringComparison.OrdinalIgnoreCase))
        {
            result.Accepted.Add(response ?? "No response.");
            return result;
        }

        var split = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < split.Length && result.Accepted.Count < maxLines; i++)
        {
            if (IsMetaLine(split[i]))
            {
                result.Rejected.Add($"{split[i].Trim()} (meta text)");
                continue;
            }

            var line = CleanLine(split[i]);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (ShouldReject(line, area, out var reason))
            {
                result.Rejected.Add($"{line} ({reason})");
                continue;
            }

            result.Accepted.Add(line);
        }

        return result;
    }

    private static bool IsMetaLine(string line)
    {
        line = line.Trim();

        return line.StartsWith("Theme:", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Town:", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Here are", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Generated", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Generate", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Sure,", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Certainly", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanLine(string line)
    {
        line = line.Trim();

        while (line.Length > 0 && (line[0] == '-' || line[0] == '*' || line[0] == '"' || line[0] == '\''))
        {
            line = line[1..].TrimStart();
        }

        var index = 0;
        while (index < line.Length && char.IsDigit(line[index]))
        {
            index++;
        }

        if (index > 0 && index < line.Length && (line[index] == '.' || line[index] == ')'))
        {
            line = line[(index + 1)..].TrimStart();
        }

        var colonIndex = line.IndexOf(':');
        if (colonIndex > 0 && colonIndex < 50)
        {
            line = line[(colonIndex + 1)..].TrimStart();
        }

        var quoteIndex = line.IndexOf('"');
        if (quoteIndex > 0 && quoteIndex < 50)
        {
            line = line[(quoteIndex + 1)..].TrimStart();
        }

        line = StripSpeakerPrefix(line);

        return CapitalizeDialogueStart(line.Trim().Trim('"', '\''));
    }

    private static string CapitalizeDialogueStart(string line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (!char.IsLetter(line[i]))
            {
                continue;
            }

            if (!char.IsLower(line[i]))
            {
                return line;
            }

            return line[..i] + char.ToUpperInvariant(line[i]) + line[(i + 1)..];
        }

        return line;
    }

    private static string StripSpeakerPrefix(string line)
    {
        var lower = line.ToLowerInvariant();

        var saysIndex = lower.IndexOf(" says ", StringComparison.Ordinal);
        if (saysIndex > 0 && saysIndex < 70)
        {
            line = line[(saysIndex + 6)..].TrimStart();
        }

        if (line.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = line.IndexOf(',');
            if (commaIndex > 0 && commaIndex < 70)
            {
                line = line[(commaIndex + 1)..].TrimStart();
            }
        }

        return line;
    }

    private static string TrimDialogueLine(string line, int maxLength)
    {
        line = CleanLine(line);

        var sentenceEnd = line.IndexOfAny(new[] { '.', '!', '?' });
        if (sentenceEnd > 0 && sentenceEnd < line.Length - 1)
        {
            line = line[..(sentenceEnd + 1)].Trim();
        }

        if (line.Length <= maxLength)
        {
            return line;
        }

        var trimAt = line.LastIndexOf(' ', Math.Min(line.Length - 1, maxLength - 4));

        if (trimAt < maxLength / 2)
        {
            trimAt = maxLength - 3;
        }

        return $"{line[..trimAt].TrimEnd()}...";
    }

    private static bool ShouldReject(string line, string area, out string reason)
    {
        reason = null;
        area = NormalizeTown(area);

        if (line.Length < 8)
        {
            reason = "too short";
            return true;
        }

        if (line.Length > MaxDynamicDialogueLength)
        {
            reason = $"too long for live speech: {line.Length}/{MaxDynamicDialogueLength}";
            return true;
        }

        var padded = $" {line.ToLowerInvariant()} ";

        if (padded.StartsWith(" theme:", StringComparison.OrdinalIgnoreCase) ||
            padded.StartsWith(" town:", StringComparison.OrdinalIgnoreCase) ||
            padded.StartsWith(" area:", StringComparison.OrdinalIgnoreCase) ||
            padded.StartsWith(" generate ", StringComparison.OrdinalIgnoreCase) ||
            padded.StartsWith(" generated ", StringComparison.OrdinalIgnoreCase) ||
            padded.StartsWith(" line ", StringComparison.OrdinalIgnoreCase))
        {
            reason = "meta text";
            return true;
        }

        for (var i = 0; i < _forbiddenFragments.Length; i++)
        {
            if (ContainsForbiddenFragment(padded, _forbiddenFragments[i]))
            {
                reason = $"forbidden term: {_forbiddenFragments[i].Trim()}";
                return true;
            }
        }

        if (ContainsUnknownProperName(line, out var unknownName))
        {
            reason = $"unknown proper name: {unknownName}";
            return true;
        }

        if (padded.Contains("ingot", StringComparison.OrdinalIgnoreCase) &&
            padded.Contains("anvil", StringComparison.OrdinalIgnoreCase))
        {
            reason = "invalid smithing process";
            return true;
        }

        if (padded.Contains("ore", StringComparison.OrdinalIgnoreCase) &&
            padded.Contains("anvil", StringComparison.OrdinalIgnoreCase))
        {
            reason = "invalid ore process";
            return true;
        }

        if ((padded.Contains(" help ", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains(" find ", StringComparison.OrdinalIgnoreCase)) &&
            (padded.Contains(" reward", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains(" missing ", StringComparison.OrdinalIgnoreCase)))
        {
            reason = "implied quest objective";
            return true;
        }

        if ((padded.Contains(" deliver", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains(" delivery ", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains(" errand", StringComparison.OrdinalIgnoreCase)) &&
            (padded.Contains(" needed", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains(" wanted", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains(" reward", StringComparison.OrdinalIgnoreCase)))
        {
            reason = "implied quest objective";
            return true;
        }

        return false;
    }

    private static bool ContainsForbiddenFragment(string paddedLine, string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment))
        {
            return false;
        }

        if (fragment[0] == ' ' || fragment[^1] == ' ')
        {
            return paddedLine.Contains(fragment, StringComparison.OrdinalIgnoreCase);
        }

        return ContainsWholeWord(paddedLine, fragment);
    }

    private static bool ContainsUnknownProperName(string line, out string unknownName)
    {
        unknownName = null;
        var sentenceStart = true;

        for (var i = 0; i < line.Length;)
        {
            var value = line[i];

            if (!char.IsLetter(value))
            {
                if (value == '.' || value == '!' || value == '?' || value == ';' || value == ':')
                {
                    sentenceStart = true;
                }

                i++;
                continue;
            }

            var start = i;
            while (i < line.Length && (char.IsLetter(line[i]) || line[i] == '\''))
            {
                i++;
            }

            var token = line[start..i];
            var isCapitalized = char.IsUpper(token[0]) && HasLowercaseLetter(token);

            if (!isCapitalized)
            {
                sentenceStart = false;
                continue;
            }

            if (_allowedCapitalizedCommonWords.Contains(token))
            {
                sentenceStart = false;
                continue;
            }

            if (TryConsumeKnownLocationPhrase(line, start, out var locationEnd))
            {
                i = locationEnd;
                sentenceStart = false;
                continue;
            }

            if (IsAllowedProperNameToken(token))
            {
                sentenceStart = false;
                continue;
            }

            if (sentenceStart && _sentenceStartWords.Contains(token))
            {
                sentenceStart = false;
                continue;
            }

            if (NextTokenIsTradeGoodProduct(line, i))
            {
                sentenceStart = false;
                continue;
            }

            if (sentenceStart && !NextTokenLooksLikeProperName(line, i))
            {
                sentenceStart = false;
                continue;
            }

            unknownName = token;
            return true;
        }

        return false;
    }

    private static bool TryConsumeKnownLocationPhrase(string line, int start, out int end)
    {
        end = start;
        var index = start;
        var builder = new StringBuilder();
        var bestEnd = -1;

        for (var wordCount = 0; wordCount < 8; wordCount++)
        {
            while (index < line.Length && !char.IsLetterOrDigit(line[index]))
            {
                if (line[index] is ',' or '.' or '!' or '?' or ';' or ':')
                {
                    return bestEnd >= 0 && SetEnd(bestEnd, out end);
                }

                index++;
            }

            if (index >= line.Length)
            {
                break;
            }

            var wordStart = index;
            while (index < line.Length && (char.IsLetterOrDigit(line[index]) || line[index] == '\''))
            {
                index++;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(line, wordStart, index - wordStart);

            if (VirtualEcologyLocations.IsKnownLocationPhrase(builder.ToString()))
            {
                bestEnd = index;
            }

            if (index >= line.Length || line[index] is ',' or '.' or '!' or '?' or ';' or ':')
            {
                break;
            }
        }

        return bestEnd >= 0 && SetEnd(bestEnd, out end);
    }

    private static bool SetEnd(int value, out int end)
    {
        end = value;
        return true;
    }

    private static bool IsAllowedProperNameToken(string token)
    {
        token = NormalizeProperToken(token);
        return _approvedProperNames.Contains(token) || _properNameSuffixes.Contains(token);
    }

    private static string NormalizeProperToken(string token)
    {
        return token.EndsWith("'s", StringComparison.OrdinalIgnoreCase) ? token[..^2] : token;
    }

    private static bool NextTokenIsTradeGoodProduct(string line, int index)
    {
        while (index < line.Length && !char.IsLetter(line[index]))
        {
            index++;
        }

        if (index >= line.Length || !char.IsLower(line[index]))
        {
            return false;
        }

        var start = index;
        while (index < line.Length && char.IsLetter(line[index]))
        {
            index++;
        }

        return _tradeGoodProperAdjectiveProducts.Contains(line[start..index]);
    }

    private static bool NextTokenLooksLikeProperName(string line, int index)
    {
        while (index < line.Length && !char.IsLetter(line[index]))
        {
            index++;
        }

        if (index >= line.Length)
        {
            return false;
        }

        var start = index;
        while (index < line.Length && (char.IsLetter(line[index]) || line[index] == '\''))
        {
            index++;
        }

        var token = NormalizeProperToken(line[start..index]);
        if (_allowedCapitalizedCommonWords.Contains(token))
        {
            return false;
        }

        return char.IsUpper(token[0]) && (HasLowercaseLetter(token) || _properNameSuffixes.Contains(token));
    }

    private static bool HasLowercaseLetter(string token)
    {
        for (var i = 1; i < token.Length; i++)
        {
            if (char.IsLower(token[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsWholeWord(string paddedLine, string word)
    {
        var start = 0;

        while (start < paddedLine.Length)
        {
            var index = paddedLine.IndexOf(word, start, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                return false;
            }

            var before = index == 0 ? ' ' : paddedLine[index - 1];
            var afterIndex = index + word.Length;
            var after = afterIndex >= paddedLine.Length ? ' ' : paddedLine[afterIndex];

            if (!char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after))
            {
                return true;
            }

            start = index + word.Length;
        }

        return false;
    }

    private static void AddWorldFact(WorldFact fact)
    {
        if (fact == null || string.IsNullOrWhiteSpace(fact.PlayerName))
        {
            return;
        }

        PruneOldFacts();
        _recentFacts.Add(fact);

        TrimTransientFacts();
    }

    private static void AddOrUpdatePlayerDeathFact(WorldFact fact)
    {
        if (fact == null || fact.Kind != WorldFactKind.PlayerDeath || string.IsNullOrWhiteSpace(fact.PlayerName))
        {
            return;
        }

        var mergeCutoff = DateTime.UtcNow - PlayerDeathFactMergeWindow;
        var cooldownCutoff = DateTime.UtcNow - PlayerDeathFactCooldown;

        for (var i = _recentFacts.Count - 1; i >= 0; i--)
        {
            var existing = _recentFacts[i];

            if (existing.Kind != WorldFactKind.PlayerDeath ||
                existing.CreatedUtc < mergeCutoff ||
                !IsSamePlayerFact(existing, fact.PlayerSerial, fact.PlayerName))
            {
                continue;
            }

            existing.Detail = PreferSpecificDeathDetail(existing.Detail, fact.Detail);
            existing.Location = string.IsNullOrWhiteSpace(existing.Location) ? fact.Location : existing.Location;
            existing.CreatedUtc = fact.CreatedUtc > existing.CreatedUtc ? fact.CreatedUtc : existing.CreatedUtc;
            return;
        }

        for (var i = _recentFacts.Count - 1; i >= 0; i--)
        {
            var existing = _recentFacts[i];

            if (existing.Kind == WorldFactKind.PlayerDeath &&
                existing.CreatedUtc >= cooldownCutoff &&
                IsSamePlayerFact(existing, fact.PlayerSerial, fact.PlayerName))
            {
                return;
            }
        }

        AddWorldFact(fact);
    }

    private static bool IsSamePlayerFact(WorldFact fact, uint playerSerial, string playerName)
    {
        if (fact == null)
        {
            return false;
        }

        if (fact.PlayerSerial != 0 && playerSerial != 0)
        {
            return fact.PlayerSerial == playerSerial;
        }

        return !string.IsNullOrWhiteSpace(playerName) &&
            string.Equals(fact.PlayerName, playerName, StringComparison.OrdinalIgnoreCase);
    }

    private static string PreferSpecificDeathDetail(string current, string incoming)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return incoming;
        }

        if (string.IsNullOrWhiteSpace(incoming) || string.Equals(current, incoming, StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }

        if (incoming.Contains(" by ", StringComparison.OrdinalIgnoreCase))
        {
            return incoming;
        }

        if (current.Contains(" by ", StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }

        if (string.Equals(current, "died", StringComparison.OrdinalIgnoreCase))
        {
            return incoming;
        }

        return current;
    }

    private static void PruneOldFacts()
    {
        var cutoff = DateTime.UtcNow - RecentFactMaxAge;

        for (var i = _recentFacts.Count - 1; i >= 0; i--)
        {
            if (!IsPermanentFact(_recentFacts[i]) && _recentFacts[i].CreatedUtc < cutoff)
            {
                _recentFacts.RemoveAt(i);
            }
        }
    }

    private static void TrimTransientFacts()
    {
        var transientCount = 0;

        for (var i = _recentFacts.Count - 1; i >= 0; i--)
        {
            if (IsPermanentFact(_recentFacts[i]))
            {
                continue;
            }

            transientCount++;

            if (transientCount > MaxRecentFactCount)
            {
                _recentFacts.RemoveAt(i);
            }
        }
    }

    private static bool IsPermanentFact(WorldFact fact)
    {
        return fact?.Kind == WorldFactKind.ServerFirstAchievement;
    }

    private static void OnMovement(MovementEventArgs args)
    {
        if (!AIIntegrationService.IsEnabled || args?.Mobile is not PlayerMobile player || !player.Alive)
        {
            return;
        }

        if (player.AccessLevel > AccessLevel.Player && !AllowStaffMovementTriggers)
        {
            return;
        }

        var now = Core.Now;

        if (_nextPlayerLiveComment.TryGetValue(player.Serial, out var playerNext) && playerNext > now)
        {
            return;
        }

        if (player.Region?.IsPartOf<TownRegion>() != true)
        {
            return;
        }

        var town = GetTownKey(player.Region);
        var roll = Utility.RandomDouble();
        var wantsFact = roll < MovementFactCommentChance;
        var wantsFlavor = !wantsFact && roll < MovementFactCommentChance + MovementFlavorCommentChance;

        if (!wantsFact && !wantsFlavor)
        {
            return;
        }

        var speaker = FindNearbyTownSpeaker(player, now);

        if (speaker == null)
        {
            return;
        }

        string line = null;
        WorldFact fact = null;

        // Prioritize real shard facts. Cached AI flavor is intentionally rarer and only fills ambient gaps.
        if (wantsFact && TrySelectFact(out fact))
        {
            line = BuildFactComment(fact);
        }
        else if (wantsFlavor)
        {
            TrySelectCachedChatter(town, out line);
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        _nextPlayerLiveComment[player.Serial] = now + PlayerLiveCommentCooldown;
        _nextNpcLiveComment[speaker.Serial] = now + NpcLiveCommentCooldown;
        SayLiveComment(speaker, line);
    }

    private static string GetTownKey(Region region)
    {
        while (region != null)
        {
            if (region is TownRegion && !string.IsNullOrWhiteSpace(region.Name))
            {
                return NormalizeTown(region.Name);
            }

            region = region.Parent;
        }

        return "britain";
    }

    private static Mobile FindNearbyTownSpeaker(PlayerMobile player, DateTime now)
    {
        Mobile selected = null;
        var eligibleCount = 0;

        foreach (var mobile in player.GetMobilesInRange<Mobile>(4))
        {
            if (mobile?.Deleted != false || mobile.Player || mobile.Hidden || !mobile.Alive ||
                mobile.AccessLevel > AccessLevel.Player || mobile.Region?.IsPartOf<TownRegion>() != true ||
                mobile.Map != player.Map)
            {
                continue;
            }

            if (mobile is not BaseVendor && !mobile.Body.IsHuman)
            {
                continue;
            }

            if (_nextNpcLiveComment.TryGetValue(mobile.Serial, out var npcNext) && npcNext > now)
            {
                continue;
            }

            // Use map line-of-sight directly so staff testing does not bypass building walls.
            if (player.Map?.LineOfSight(player, mobile) != true)
            {
                continue;
            }

            // Randomly select from eligible speakers so enumeration order does not make one NPC monopolize chatter.
            eligibleCount++;
            if (Utility.Random(eligibleCount) == 0)
            {
                selected = mobile;
            }
        }

        return selected;
    }

    private static bool TrySelectFact(out WorldFact fact)
    {
        fact = null;
        RefreshServerFirstFactsFromAchievements();
        PruneOldFacts();
        TrimTransientFacts();

        if (_recentFacts.Count == 0)
        {
            return false;
        }

        fact = _recentFacts[Utility.Random(_recentFacts.Count)];
        return fact != null;
    }

    private static bool TrySelectCachedChatter(string town, out string line)
    {
        line = null;

        if (!TryGetCache(town, out var cache) || cache.Lines.Count == 0)
        {
            return false;
        }

        var now = Core.Now;
        PruneLineCooldowns(cache, now);

        var selectedLine = SelectAvailableCachedLine(cache, now);

        if (string.IsNullOrWhiteSpace(selectedLine))
        {
            return false;
        }

        line = TrimDialogueLine(selectedLine, MaxDynamicDialogueLength);

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var cooldown = LineReuseCooldown;

        if (cooldown > TimeSpan.Zero)
        {
            cache.NextLineUse[selectedLine] = now + cooldown;
        }

        return true;
    }

    private static string SelectAvailableCachedLine(TownChatterCache cache, DateTime now)
    {
        string selectedLine = null;
        var eligibleCount = 0;

        for (var i = 0; i < cache.Lines.Count; i++)
        {
            var candidate = cache.Lines[i];

            if (string.IsNullOrWhiteSpace(candidate) ||
                cache.NextLineUse.TryGetValue(candidate, out var nextUse) && nextUse > now)
            {
                continue;
            }

            eligibleCount++;

            if (Utility.Random(eligibleCount) == 0)
            {
                selectedLine = candidate;
            }
        }

        return selectedLine;
    }

    private static void PruneLineCooldowns(TownChatterCache cache, DateTime now)
    {
        if (cache?.NextLineUse.Count == 0)
        {
            return;
        }

        List<string> staleKeys = null;

        foreach (var entry in cache.NextLineUse)
        {
            if (entry.Value <= now || !CacheContainsLine(cache, entry.Key))
            {
                staleKeys ??= new List<string>();
                staleKeys.Add(entry.Key);
            }
        }

        if (staleKeys == null)
        {
            return;
        }

        for (var i = 0; i < staleKeys.Count; i++)
        {
            cache.NextLineUse.Remove(staleKeys[i]);
        }
    }

    private static bool CacheContainsLine(TownChatterCache cache, string line)
    {
        if (cache == null || string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        for (var i = 0; i < cache.Lines.Count; i++)
        {
            if (string.Equals(cache.Lines[i], line, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void SayLiveComment(Mobile speaker, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        PostToGameLoop(() =>
        {
            if (speaker?.Deleted == false && speaker.Alive)
            {
                speaker.Say(line);
            }
        });
    }

    private static string BuildFactComment(WorldFact fact)
    {
        if (fact == null || string.IsNullOrWhiteSpace(fact.PlayerName))
        {
            return null;
        }

        var line = fact.Kind switch
        {
            WorldFactKind.PlayerDeath => BuildPlayerDeathFactComment(fact),
            WorldFactKind.ServerFirstAchievement => BuildServerFirstFactComment(fact),
            _ => null
        };

        return string.IsNullOrWhiteSpace(line) ? null : TrimDialogueLine(line, MaxDynamicDialogueLength);
    }

    private static string BuildPlayerDeathFactComment(WorldFact fact)
    {
        if (string.IsNullOrWhiteSpace(fact.Detail))
        {
            return null;
        }

        var location = string.IsNullOrWhiteSpace(fact.Location) ? string.Empty : $" in {fact.Location}";

        return Utility.Random(5) switch
        {
            0 => $"I heard {fact.PlayerName} {fact.Detail}{location}.",
            1 => $"Word is {fact.PlayerName} {fact.Detail}{location}.",
            2 => $"They say {fact.PlayerName} {fact.Detail}{location}.",
            3 => $"Have you heard? {fact.PlayerName} {fact.Detail}{location}.",
            _ => $"News travels fast: {fact.PlayerName} {fact.Detail}{location}."
        };
    }

    private static string BuildServerFirstFactComment(WorldFact fact)
    {
        if (string.IsNullOrWhiteSpace(fact.Detail))
        {
            return null;
        }

        return Utility.Random(7) switch
        {
            0 => $"There's {fact.PlayerName}, first to reach Grandmaster {fact.Detail}.",
            1 => $"They say {fact.PlayerName} was first in Britannia to master {fact.Detail}.",
            2 => $"{fact.PlayerName} was first to earn Grandmaster {fact.Detail}, so I hear.",
            3 => $"No one reached Grandmaster {fact.Detail} before {fact.PlayerName}.",
            4 => $"I heard {fact.PlayerName} claimed the first Grandmaster {fact.Detail} honor.",
            5 => $"Remember the name {fact.PlayerName}: first Grandmaster of {fact.Detail}.",
            _ => $"Word is {fact.PlayerName} led the shard to Grandmaster {fact.Detail}."
        };
    }

    private static bool IsEligibleServerFirstAnnouncement(WorldFact fact, DateTime now)
    {
        return fact?.Kind == WorldFactKind.ServerFirstAchievement &&
            !string.IsNullOrWhiteSpace(fact.PlayerName) &&
            !string.IsNullOrWhiteSpace(fact.Detail) &&
            fact.CreatedUtc >= now - ServerFirstAnnouncementMaxAge;
    }

    private static bool HasEligibleServerFirstAnnouncement(DateTime now)
    {
        for (var i = 0; i < _recentFacts.Count; i++)
        {
            if (IsEligibleServerFirstAnnouncement(_recentFacts[i], now))
            {
                return true;
            }
        }

        return false;
    }

    private static void ForceBeginServerFirstTownCrierAnnouncements()
    {
        var instances = TownCrier.Instances;

        for (var i = 0; i < instances.Count; i++)
        {
            instances[i].ForceBeginAutoShout();
        }
    }

    private static string[] BuildServerFirstAnnouncementLines(WorldFact fact)
    {
        if (fact == null || string.IsNullOrWhiteSpace(fact.PlayerName) || string.IsNullOrWhiteSpace(fact.Detail))
        {
            return null;
        }

        return Utility.Random(4) switch
        {
            0 => new[]
            {
                $"Let it be known: {fact.PlayerName} is the first to reach Grandmaster {fact.Detail}!",
                "Their name shall be remembered among the records of Britannia!"
            },
            1 => new[]
            {
                $"Hear ye! {fact.PlayerName} has claimed a place in the realm's chronicles!",
                $"None reached Grandmaster {fact.Detail} before them!"
            },
            2 => new[]
            {
                $"News from the realm: {fact.PlayerName} stands first among Grandmaster {fact.Detail}s!",
                "Raise a cheer for this new record!"
            },
            _ => new[]
            {
                $"By official record, {fact.PlayerName} was first to master {fact.Detail}!",
                "Let the towns remember the first of their craft!"
            }
        };
    }

    private static void PostToGameLoop(Action callback)
    {
        if (Core.LoopContext != null)
        {
            Core.LoopContext.Post(callback);
            return;
        }

        callback();
    }

    private static string GetRegionDisplayName(Region region)
    {
        while (region != null)
        {
            if (!string.IsNullOrWhiteSpace(region.Name) && !region.IsDefault)
            {
                return region.Name;
            }

            region = region.Parent;
        }

        return null;
    }

    private static string GetCreatureDisplayName(BaseCreature creature)
    {
        if (creature == null)
        {
            return "a creature";
        }

        var typeName = GetCreatureTypeDisplayName(creature);
        var name = TrimCreatureArticle(creature.Name);

        if (string.IsNullOrWhiteSpace(name) || CreatureNameContainsType(name, typeName))
        {
            return AddCreatureArticle(typeName);
        }

        return $"{AddCreatureArticle(typeName)} named {name}";
    }

    private static string GetCreatureTypeDisplayName(BaseCreature creature)
    {
        var typeName = creature.GetType().Name;

        if (string.IsNullOrWhiteSpace(typeName))
        {
            return "creature";
        }

        var builder = new StringBuilder(typeName.Length + 4);

        for (var i = 0; i < typeName.Length; i++)
        {
            var current = typeName[i];

            if (i > 0 && char.IsUpper(current) &&
                (char.IsLower(typeName[i - 1]) || i + 1 < typeName.Length && char.IsLower(typeName[i + 1])))
            {
                builder.Append(' ');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString().Trim();
    }

    private static string TrimCreatureArticle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        name = name.Trim();

        if (name.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
        {
            return name[2..].Trim();
        }

        if (name.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            return name[3..].Trim();
        }

        if (name.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            return name[4..].Trim();
        }

        return name;
    }

    private static bool CreatureNameContainsType(string name, string typeName)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        return name.Contains(typeName, StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains(name, StringComparison.OrdinalIgnoreCase);
    }

    private static string AddCreatureArticle(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return "a creature";
        }

        typeName = typeName.Trim();

        if (typeName.StartsWith("a ", StringComparison.OrdinalIgnoreCase) ||
            typeName.StartsWith("an ", StringComparison.OrdinalIgnoreCase) ||
            typeName.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            return typeName;
        }

        var article = IsVowelSound(typeName[0]) ? "an" : "a";
        return $"{article} {typeName}";
    }

    private static bool IsVowelSound(char value)
    {
        return char.ToLowerInvariant(value) is 'a' or 'e' or 'i' or 'o' or 'u';
    }

    private static void AppendLines(TownChatterCache cache, List<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            cache.Lines.Add(lines[i]);
        }

        while (cache.Lines.Count > MaxLineCount)
        {
            cache.NextLineUse.Remove(cache.Lines[0]);
            cache.Lines.RemoveAt(0);
        }
    }

    private static void AppendRejectedLines(TownChatterCache cache, List<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            cache.RejectedLines.Add(lines[i]);
        }

        while (cache.RejectedLines.Count > MaxRejectedLineCount)
        {
            cache.RejectedLines.RemoveAt(0);
        }
    }

    private static async void AutoTopUp_OnTick()
    {
        if (_autoTopUpRunning || !AIIntegrationService.IsEnabled)
        {
            RestartAutoTopUpTimer();
            return;
        }

        _autoTopUpRunning = true;

        try
        {
            Logger.Information(
                "[{Timestamp:u}] [Virtual Ecology] Auto-generating {LineCount} chatter line(s) for {AreaCount} area(s).",
                Core.Now,
                AutoTopUpLineCount,
                DefaultAreas.Length
            );

            var previousLineCount = CountStoredLines();
            var previousRejectedCount = CountRejectedLines();
            var caches = await TopUpAllAsync();
            var currentLineCount = CountStoredLines();
            var currentRejectedCount = CountRejectedLines();

            Logger.Information(
                "[{Timestamp:u}] [Virtual Ecology] Chatter auto-generation complete. Areas={AreaCount}, stored delta={StoredDelta}, total stored={TotalStored}, rejected delta={RejectedDelta}.",
                Core.Now,
                caches.Count,
                currentLineCount - previousLineCount,
                currentLineCount,
                currentRejectedCount - previousRejectedCount
            );
        }
        finally
        {
            _autoTopUpRunning = false;
            RestartAutoTopUpTimer();
        }
    }

    private static TimeSpan GetNextAutoTopUpInterval()
    {
        return HasDefaultAreaUnderCap() ? CatchUpTopUpInterval : AutoTopUpInterval;
    }

    private static bool HasDefaultAreaUnderCap()
    {
        for (var i = 0; i < DefaultAreas.Length; i++)
        {
            if (!TryGetCache(DefaultAreas[i], out var cache) || cache.Lines.Count < MaxLineCount)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountStoredLines()
    {
        var count = 0;

        foreach (var cache in _caches.Values)
        {
            count += cache.Lines.Count;
        }

        return count;
    }

    private static int CountRejectedLines()
    {
        var count = 0;

        foreach (var cache in _caches.Values)
        {
            count += cache.RejectedLines.Count;
        }

        return count;
    }

    private static void WriteStringList(IGenericWriter writer, List<string> values)
    {
        writer.WriteEncodedInt(values?.Count ?? 0);

        if (values == null)
        {
            return;
        }

        for (var i = 0; i < values.Count; i++)
        {
            writer.Write(values[i]);
        }
    }

    private static void WriteLineCooldowns(IGenericWriter writer, TownChatterCache cache)
    {
        PruneLineCooldowns(cache, Core.Now);
        writer.WriteEncodedInt(cache?.NextLineUse.Count ?? 0);

        if (cache == null)
        {
            return;
        }

        foreach (var entry in cache.NextLineUse)
        {
            writer.Write(entry.Key);
            writer.Write(entry.Value);
        }
    }

    private static void ReadStringList(IGenericReader reader, List<string> values, int maxCount)
    {
        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var value = reader.ReadString();

            if (!string.IsNullOrWhiteSpace(value) && values.Count < maxCount)
            {
                values.Add(value);
            }
        }
    }

    private static void ReadLineCooldowns(IGenericReader reader, TownChatterCache cache)
    {
        var count = reader.ReadEncodedInt();
        var now = Core.Now;

        for (var i = 0; i < count; i++)
        {
            var line = reader.ReadString();
            var nextUse = reader.ReadDateTime();

            if (!string.IsNullOrWhiteSpace(line) && nextUse > now && CacheContainsLine(cache, line))
            {
                cache.NextLineUse[line] = nextUse;
            }
        }
    }

    private static string ToDisplayName(string town)
    {
        if (string.IsNullOrWhiteSpace(town))
        {
            return "Britain";
        }

        town = NormalizeTown(town);

        if (_displayNames.TryGetValue(town, out var displayName))
        {
            return displayName;
        }

        var builder = new StringBuilder(town.Length);
        var capitalizeNext = true;

        for (var i = 0; i < town.Length; i++)
        {
            var value = town[i];

            if (char.IsWhiteSpace(value) || value == '-')
            {
                builder.Append(value);
                capitalizeNext = true;
                continue;
            }

            builder.Append(capitalizeNext ? char.ToUpperInvariant(value) : value);
            capitalizeNext = false;
        }

        return builder.ToString();
    }

    private sealed class TownChatterParseResult
    {
        public TownChatterParseResult(int capacity)
        {
            Accepted = new List<string>(capacity);
            Rejected = new List<string>();
        }

        public List<string> Accepted { get; }
        public List<string> Rejected { get; }
    }
}
