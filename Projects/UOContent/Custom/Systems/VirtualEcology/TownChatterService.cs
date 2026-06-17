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
}

public static class TownChatterService
{
    public const int DefaultLineCount = 12;
    public const int MaxLineCount = 30;
    public const int AutoTopUpLineCount = 3;
    public static readonly TimeSpan AutoTopUpInterval = TimeSpan.FromMinutes(10.0);

    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(TownChatterService));
    private const int MaxGenerationAttempts = 3;
    private const int MaxRejectedLineCount = 30;
    private const int MaxCachedDialogueLength = 110;
    private const int MaxDynamicDialogueLength = 95;
    private const int MaxRecentFactCount = 50;
    private static readonly TimeSpan PlayerDeathFactMergeWindow = TimeSpan.FromMinutes(10.0);
    private static readonly TimeSpan PlayerDeathFactCooldown = TimeSpan.FromHours(6.0);
    private const double MovementCommentChance = 0.02;
    private static readonly TimeSpan PlayerLiveCommentCooldown = TimeSpan.FromMinutes(2.0);
    private static readonly TimeSpan NpcLiveCommentCooldown = TimeSpan.FromMinutes(3.0);
    private static readonly TimeSpan RecentFactMaxAge = TimeSpan.FromHours(6.0);
    private static readonly TimeSpan ServerFirstFactSyncInterval = TimeSpan.FromMinutes(1.0);

    private static readonly Dictionary<string, TownChatterCache> _caches =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<WorldFact> _recentFacts = new();
    private static readonly Dictionary<Serial, DateTime> _nextPlayerLiveComment = new();
    private static readonly Dictionary<Serial, DateTime> _nextNpcLiveComment = new();
    private static TimerExecutionToken _autoTopUpToken;
    private static bool _autoTopUpRunning;
    private static bool _movementHooked;
    private static DateTime _nextServerFirstFactSyncUtc;
    private static readonly Dictionary<string, Region> _townRegions = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> _defaultThemes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["britain"] = "market gossip, guards, merchants, travelers, and daily city life",
            ["minoc"] = "miners, ore, smiths, mountain roads, and work songs",
            ["yew"] = "woodsmen, rangers, old trees, courts, and quiet forest paths",
            ["moonglow"] = "scholars, mages, libraries, telescopes, and island rumors",
            ["trinsic"] = "paladins, guards, honor, harbor traffic, and training yards",
            ["vesper"] = "bridges, canals, traders, sailors, and dockside talk",
            ["skara"] = "rangers, farms, ferries, musicians, and island weather",
            ["jhelom"] = "duelists, fighters, sailors, arenas, and tavern boasts",
            ["magincia"] = "pride, gardens, nobles, merchants, and polished streets"
        };

    private static readonly Dictionary<string, Point3D> _townCenters =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["britain"] = new Point3D(1495, 1629, 10),
            ["minoc"] = new Point3D(2477, 413, 15),
            ["yew"] = new Point3D(633, 858, 0),
            ["moonglow"] = new Point3D(4442, 1122, 5),
            ["trinsic"] = new Point3D(1828, 2821, 0),
            ["vesper"] = new Point3D(2899, 676, 0),
            ["skara"] = new Point3D(576, 2200, 0),
            ["jhelom"] = new Point3D(1378, 3825, 0),
            ["magincia"] = new Point3D(3728, 2164, 20)
        };

    private static readonly Dictionary<string, string> _townLandmarks =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["britain"] = "castle, market, bank, docks, farms, guard posts, blacksmith shops, inns",
            ["minoc"] = "mines, forges, tinkers' shops, mountain roads, gypsy camp, bank",
            ["yew"] = "abbey, courts, farms, wineries, deep woods, ranger paths",
            ["moonglow"] = "lycaeum, mage shops, observatory, telescope, docks, island roads",
            ["trinsic"] = "paladin halls, training yards, harbor, guard posts, south gate",
            ["vesper"] = "bridges, canals, docks, provisioners, shipwrights, taverns",
            ["skara"] = "ferry, farms, ranger guild, docks, music hall, sheep fields",
            ["jhelom"] = "dueling pits, fighter halls, docks, taverns, practice yards",
            ["magincia"] = "gardens, estates, market, docks, bank, polished streets"
        };

    public static string[] DefaultTowns { get; } =
    {
        "britain",
        "minoc",
        "yew",
        "moonglow",
        "trinsic",
        "vesper",
        "skara",
        "jhelom",
        "magincia"
    };

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
        "sure,"
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
        if (!_movementHooked)
        {
            EventSink.Movement += OnMovement;
            _movementHooked = true;
        }

        if (_autoTopUpToken.Running)
        {
            return;
        }

        // Keeps the town chatter pools moving while the AI feature is enabled.
        Timer.StartTimer(AutoTopUpInterval, AutoTopUpInterval, AutoTopUp_OnTick, out _autoTopUpToken);
    }

    public static async ValueTask<TownChatterCache> GenerateAsync(string town, int count = DefaultLineCount, string theme = null)
    {
        town = NormalizeTown(town);
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
        int count = AutoTopUpLineCount,
        string theme = null
    )
    {
        town = NormalizeTown(town);
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

    public static async ValueTask<List<TownChatterCache>> RegenerateAllAsync(int count = DefaultLineCount)
    {
        var caches = new List<TownChatterCache>(DefaultTowns.Length);

        for (var i = 0; i < DefaultTowns.Length; i++)
        {
            caches.Add(await GenerateAsync(DefaultTowns[i], count));
        }

        return caches;
    }

    public static async ValueTask<List<TownChatterCache>> TopUpAllAsync(int count = AutoTopUpLineCount)
    {
        var caches = new List<TownChatterCache>(DefaultTowns.Length);

        for (var i = 0; i < DefaultTowns.Length; i++)
        {
            caches.Add(await TopUpAsync(DefaultTowns[i], count));
        }

        return caches;
    }

    public static async ValueTask<string> GenerateDynamicReactionAsync(string town, string nearbyContext = null)
    {
        town = NormalizeTown(town);

        var prompt =
            $"Town: {ToDisplayName(town)}\n" +
            BuildWorldContext(town, includePeople: false, includeTransient: true) +
            (!string.IsNullOrWhiteSpace(nearbyContext) ? $"Nearby context: {nearbyContext.Trim()}\n" : string.Empty) +
            "Generate one short first-person ambient reaction from a town NPC. " +
            "It may react to the current time, weather, town, nearby shops, or supplied nearby context. " +
            "Do not include the speaker's name, job title, role, or any other personal name. " +
            "Do not write stage directions, narration, or third-person text. " +
            "Keep it under 95 characters. " +
            "Do not imply a quest, task, reward, delivery, missing person, or player objective. " +
            "Return only the dialogue line, without labels or quote marks.";

        var response = await AIIntegrationService.GenerateChatterPoolAsync(prompt);
        var lines = ParseLines(response, 1);

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
    }

    public static string GenerateLatestFactComment()
    {
        RefreshServerFirstFactsFromAchievements(force: true);
        PruneOldFacts();
        TrimTransientFacts();

        if (_recentFacts.Count == 0)
        {
            return "No recent town chatter facts are recorded.";
        }

        var fact = _recentFacts[^1];
        var line = BuildFactComment(fact);

        return string.IsNullOrWhiteSpace(line) ? "The latest fact could not be rendered." : line;
    }

    public static void SerializePersistence(IGenericWriter writer)
    {
        RefreshServerFirstFactsFromAchievements(force: true);
        PruneOldFacts();
        TrimTransientFacts();

        writer.WriteEncodedInt(3); // data version
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
        return true;
    }

    public static bool Clear(string town)
    {
        return _caches.Remove(NormalizeTown(town));
    }

    public static string NormalizeTown(string town)
    {
        return string.IsNullOrWhiteSpace(town) ? "britain" : town.Trim().ToLowerInvariant();
    }

    public static string GetDefaultTheme(string town)
    {
        return _defaultThemes.TryGetValue(NormalizeTown(town), out var theme)
            ? theme
            : "local gossip, work, weather, travelers, and daily town life";
    }

    private static async ValueTask<TownChatterParseResult> GenerateLinesAsync(string town, string theme, int count)
    {
        var result = new TownChatterParseResult(count);

        for (var attempt = 1; attempt <= MaxGenerationAttempts && result.Accepted.Count < count; attempt++)
        {
            var needed = count - result.Accepted.Count;
            var prompt = BuildPrompt(town, theme, needed, attempt);
            var response = await AIIntegrationService.GenerateChatterPoolAsync(prompt);
            var lines = ParseLines(response, needed);

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
            $"Town: {ToDisplayName(town)}\n" +
            $"Theme: {theme}\n" +
            $"Generate {count} ambient NPC chatter lines suitable for wandering townsfolk. " +
            retryText +
            "Use grounded, low-fantasy Ultima Online Renaissance flavor. " +
            "Return only dialogue a town NPC would actually say aloud. " +
            "Do not number the lines. " +
            "Do not include names, titles, speaker labels, explanations, headers, or quote marks. " +
            "Do not include the speaker's name, job title, role, or any personal names. " +
            "Use first-person or anonymous local gossip only. " +
            "Keep each line under 110 characters. " +
            "Rules: mining yields ore, ore is smelted into ingots, and anvils are only used for smithing finished items. " +
            "Lumberjacks cut logs, and boards are made from logs. " +
            "Avoid impossible craft/resource claims, modern terms, post-UOR skills/items, and explicit game mechanics. " +
            "Do not imply an available quest, task, reward, errand, missing person, repair job, delivery, or player objective.\n" +
            BuildWorldContext(town, includePeople: false, includeTransient: false);
    }

    private static string BuildWorldContext(string town, bool includePeople, bool includeTransient)
    {
        town = NormalizeTown(town);
        var builder = new StringBuilder();

        builder.Append("Known town: ").Append(ToDisplayName(town)).Append('\n');

        if (_townLandmarks.TryGetValue(town, out var landmarks))
        {
            builder.Append("Known local places and shops: ").Append(landmarks).Append('\n');
        }

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

    private static TownChatterParseResult ParseLines(string response, int maxLines)
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

            if (ShouldReject(line, out var reason))
            {
                result.Rejected.Add($"{line} ({reason})");
                continue;
            }

            if (line.Length > MaxCachedDialogueLength)
            {
                line = TrimDialogueLine(line, MaxCachedDialogueLength);
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

        return line.Trim().Trim('"', '\'');
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

    private static bool ShouldReject(string line, out string reason)
    {
        reason = null;

        if (line.Length < 8)
        {
            reason = "too short";
            return true;
        }

        if (line.Length > 160)
        {
            reason = "too long";
            return true;
        }

        var padded = $" {line.ToLowerInvariant()} ";

        if (padded.StartsWith(" theme:", StringComparison.OrdinalIgnoreCase) ||
            padded.StartsWith(" town:", StringComparison.OrdinalIgnoreCase) ||
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

        if ((padded.Contains("help", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains("repair", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains("fix", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains("find", StringComparison.OrdinalIgnoreCase)) &&
            (padded.Contains("needed", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains("wanted", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains("bridge", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains("reward", StringComparison.OrdinalIgnoreCase) ||
             padded.Contains("missing", StringComparison.OrdinalIgnoreCase)))
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

        RefreshServerFirstFactsFromAchievements();

        if (_recentFacts.Count == 0 || Utility.RandomDouble() > MovementCommentChance)
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

        var speaker = FindNearbyTownSpeaker(player);

        if (speaker == null)
        {
            return;
        }

        if (_nextNpcLiveComment.TryGetValue(speaker.Serial, out var npcNext) && npcNext > now)
        {
            return;
        }

        if (!TrySelectFact(out var fact))
        {
            return;
        }

        _nextPlayerLiveComment[player.Serial] = now + PlayerLiveCommentCooldown;
        _nextNpcLiveComment[speaker.Serial] = now + NpcLiveCommentCooldown;
        SayLiveFactComment(speaker, fact);
    }

    private static Mobile FindNearbyTownSpeaker(PlayerMobile player)
    {
        foreach (var mobile in player.GetMobilesInRange<Mobile>(4))
        {
            if (mobile?.Deleted != false || mobile.Player || mobile.Hidden || !mobile.Alive ||
                mobile.AccessLevel > AccessLevel.Player || mobile.Region?.IsPartOf<TownRegion>() != true)
            {
                continue;
            }

            if (mobile is BaseVendor || mobile.Body.IsHuman)
            {
                return mobile;
            }
        }

        return null;
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

    private static void SayLiveFactComment(Mobile speaker, WorldFact fact)
    {
        var line = BuildFactComment(fact);

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
            return;
        }

        _autoTopUpRunning = true;

        try
        {
            Logger.Information(
                "[{Timestamp:u}] [Virtual Ecology] Auto-generating {LineCount} town chatter line(s) for {TownCount} town(s).",
                Core.Now,
                AutoTopUpLineCount,
                DefaultTowns.Length
            );

            var previousLineCount = CountStoredLines();
            var previousRejectedCount = CountRejectedLines();
            var caches = await TopUpAllAsync();
            var currentLineCount = CountStoredLines();
            var currentRejectedCount = CountRejectedLines();

            Logger.Information(
                "[{Timestamp:u}] [Virtual Ecology] Town chatter auto-generation complete. Towns={TownCount}, stored delta={StoredDelta}, total stored={TotalStored}, rejected delta={RejectedDelta}.",
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
        }
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

    private static string ToDisplayName(string town)
    {
        if (string.IsNullOrWhiteSpace(town))
        {
            return "Britain";
        }

        return char.ToUpperInvariant(town[0]) + town[1..];
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
