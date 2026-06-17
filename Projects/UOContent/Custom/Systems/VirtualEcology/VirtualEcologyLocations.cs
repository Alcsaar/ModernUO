using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Server.Json;

namespace Server.Custom.Systems.VirtualEcology;

public sealed class VirtualEcologyTownLocationEntry
{
    public List<string> Shops { get; set; } = new();
    public List<string> Buildings { get; set; } = new();
    public List<string> Landmarks { get; set; } = new();
}

public sealed class VirtualEcologyTownLocationConfig
{
    public Dictionary<string, VirtualEcologyTownLocationEntry> Towns { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public static class VirtualEcologyLocations
{
    public static string ConfigPath => Path.Combine(VirtualEcologySettings.ConfigDirectory, "town-locations.json");

    private static VirtualEcologyTownLocationConfig _config;
    private static readonly HashSet<string> _normalizedLocationNames = new(StringComparer.OrdinalIgnoreCase);

    public static void Configure()
    {
        if (!Directory.Exists(VirtualEcologySettings.ConfigDirectory))
        {
            Directory.CreateDirectory(VirtualEcologySettings.ConfigDirectory);
        }

        _config = JsonConfig.Deserialize<VirtualEcologyTownLocationConfig>(ConfigPath) ?? CreateDefaultConfig();
        MergeMissingDefaults(_config);
        RebuildLookup();
        Save();
    }

    public static string BuildPromptContext(string town)
    {
        town = TownChatterService.NormalizeTown(town);

        if (_config?.Towns == null || !_config.Towns.TryGetValue(town, out var entry))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        AppendNamedList(builder, "Known real shops", entry.Shops);
        AppendNamedList(builder, "Known real buildings", entry.Buildings);
        AppendNamedList(builder, "Known real landmarks", entry.Landmarks);
        return builder.ToString();
    }

    public static string BuildInlinePromptContext(string town)
    {
        town = TownChatterService.NormalizeTown(town);

        if (_config?.Towns == null || !_config.Towns.TryGetValue(town, out var entry))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        AppendInlineValues(builder, entry.Shops);
        AppendInlineValues(builder, entry.Buildings);
        AppendInlineValues(builder, entry.Landmarks);
        return builder.ToString();
    }

    public static bool IsKnownLocationPhrase(string phrase)
    {
        return !string.IsNullOrWhiteSpace(phrase) && _normalizedLocationNames.Contains(NormalizeLocationName(phrase));
    }

    public static void Save()
    {
        JsonConfig.Serialize(ConfigPath, _config ?? CreateDefaultConfig());
    }

    private static void AppendNamedList(StringBuilder builder, string label, List<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return;
        }

        var added = 0;

        for (var i = 0; i < values.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
            {
                continue;
            }

            if (added == 0)
            {
                builder.Append(label).Append(": ");
            }
            else
            {
                builder.Append("; ");
            }

            builder.Append(values[i].Trim());
            added++;
        }

        if (added > 0)
        {
            builder.Append(".\n");
        }
    }

    private static void RebuildLookup()
    {
        _normalizedLocationNames.Clear();

        if (_config?.Towns == null)
        {
            return;
        }

        foreach (var entry in _config.Towns.Values)
        {
            AddLocationNames(entry.Shops);
            AddLocationNames(entry.Buildings);
            AddLocationNames(entry.Landmarks);
        }
    }

    private static void AddLocationNames(List<string> names)
    {
        if (names == null)
        {
            return;
        }

        for (var i = 0; i < names.Count; i++)
        {
            AddLocationName(names[i]);
        }
    }

    private static void AddLocationName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        name = name.Trim();
        _normalizedLocationNames.Add(NormalizeLocationName(name));

        if (name.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            _normalizedLocationNames.Add(NormalizeLocationName(name[4..]));
        }
        else if (name.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
        {
            _normalizedLocationNames.Add(NormalizeLocationName(name[2..]));
        }
        else if (name.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            _normalizedLocationNames.Add(NormalizeLocationName(name[3..]));
        }
    }

    private static string NormalizeLocationName(string name)
    {
        var builder = new StringBuilder(name.Length);
        var lastWasSpace = false;

        for (var i = 0; i < name.Length; i++)
        {
            var value = name[i];

            if (char.IsLetterOrDigit(value))
            {
                builder.Append(char.ToLowerInvariant(value));
                lastWasSpace = false;
                continue;
            }

            if (value == '\'' || value == '&')
            {
                builder.Append(value);
                lastWasSpace = false;
                continue;
            }

            if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static void MergeMissingDefaults(VirtualEcologyTownLocationConfig config)
    {
        var defaults = CreateDefaultConfig();

        foreach (var (town, defaultEntry) in defaults.Towns)
        {
            if (!config.Towns.TryGetValue(town, out var entry))
            {
                config.Towns[town] = defaultEntry;
                continue;
            }

            entry.Shops ??= new List<string>();
            entry.Buildings ??= new List<string>();
            entry.Landmarks ??= new List<string>();
            AddMissing(entry.Shops, defaultEntry.Shops);
            AddMissing(entry.Buildings, defaultEntry.Buildings);
            AddMissing(entry.Landmarks, defaultEntry.Landmarks);
        }
    }

    private static void AddMissing(List<string> target, List<string> defaults)
    {
        if (target == null)
        {
            return;
        }

        for (var i = 0; i < defaults.Count; i++)
        {
            if (!ContainsValue(target, defaults[i]))
            {
                target.Add(defaults[i]);
            }
        }
    }

    private static bool ContainsValue(List<string> values, string value)
    {
        if (values == null)
        {
            return false;
        }

        for (var i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AppendInlineValues(StringBuilder builder, List<string> values)
    {
        if (values == null)
        {
            return;
        }

        for (var i = 0; i < values.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(values[i].Trim());
        }
    }

    private static VirtualEcologyTownLocationConfig CreateDefaultConfig()
    {
        return new VirtualEcologyTownLocationConfig
        {
            Towns = new Dictionary<string, VirtualEcologyTownLocationEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["britain"] = new()
                {
                    Shops =
                    [
                        "The Blue Boar",
                        "Britain Bank",
                        "First Bank Of Britain",
                        "East Britain Bank",
                        "The Lord's Arms",
                        "The Hammer and Anvil",
                        "The Best Hides Of Britain",
                        "Quality Fletching",
                        "Good Eats",
                        "Healer of Britain",
                        "Britannia Animal Care",
                        "Sweet Dreams",
                        "The Sorceror's Delight",
                        "Strength and Steel",
                        "Heavy Metal Armorer",
                        "Ethereal Goods",
                        "Premier Provisioners and Fish Shop",
                        "Premier Gems",
                        "The Lord's Clothiers",
                        "The Oaken Oar",
                        "The Cleaver",
                        "The Tinker's Guild",
                        "The Cat's Lair",
                        "Artistic Armor",
                        "Incantations and Enchantments",
                        "Sage Advise",
                        "A Girl's best friend",
                        "The Right Fit",
                        "The Wayfarer's Inn",
                        "The Salty Dog",
                        "The Unicorn's Horn",
                        "Profuse Provisions",
                        "Sosarian Steeds"
                    ],
                    Buildings =
                    [
                        "Lord British's Castle",
                        "Castle British",
                        "King Blackthorn's Castle",
                        "Lord Blackthorn's Castle",
                        "The First Library of Britain",
                        "Britain Public Library",
                        "Lord British's Conservatory",
                        "Counselor Guild Hall",
                        "Merchant's Guild",
                        "The Miner's Guild",
                        "Artist's Guild",
                        "Cavalry Guild",
                        "Warrior's Guild",
                        "The King's Men Theater",
                        "The Watch Tower",
                        "Customs",
                        "The Chamber of Virtue",
                        "The North Side Inn"
                    ],
                    Landmarks =
                    [
                        "Britain market",
                        "Britain docks",
                        "Blackthorn court",
                        "arcane circle",
                        "Britain sewers",
                        "Castle Britain Grounds",
                        "Upper West Britain",
                        "Lower West Britain",
                        "East Britain",
                        "West Britain",
                        "East Side Park",
                        "Cemetery",
                        "guard tower",
                        "west bank",
                        "easterly dock",
                        "westerly dock"
                    ]
                },
                ["bucsden"] = new()
                {
                    Shops =
                    [
                        "Cutlass Smithing",
                        "Violente Woodworks",
                        "The Peg Leg Inn",
                        "Healer of Buccaneer's Den",
                        "Buccaneer's Den Leatherworks",
                        "A Place Fer Yer Stuff",
                        "The Pirate's Plunder",
                        "Pirate's Provisioner"
                    ],
                    Buildings = ["The Buccaneer's Bath", "Pirate's Den"],
                    Landmarks = ["Buccaneer's Den Dock", "public moongate", "archway", "road between the bank and the bath house"]
                },
                ["cove"] = new()
                {
                    Shops = ["The Warrior's Supplies", "The Farmer's Market", "Cove Bank", "The Healing Hand"],
                    Buildings = ["Watch Tower"],
                    Landmarks = ["Cove Docks", "Cove Orc Fort", "town gate", "mountain wall"]
                },
                ["minoc"] = new()
                {
                    Shops = ["Minoc Bank", "Minoc Tinker Shop", "Minoc Blacksmith"],
                    Buildings = ["The Survival Shop", "The Matewan"],
                    Landmarks = ["Minoc mines", "mountain roads", "gypsy camp"]
                },
                ["yew"] = new()
                {
                    Shops = ["Yew Winery", "Yew Bank"],
                    Buildings = ["Empath Abbey", "Yew Court"],
                    Landmarks = ["deep woods", "ranger paths", "old vineyards"]
                },
                ["moonglow"] = new()
                {
                    Shops = ["Moonglow Bank", "Moonglow Mage Shop"],
                    Buildings = ["Lycaeum", "Moonglow Observatory"],
                    Landmarks = ["telescope", "island roads", "Moonglow docks"]
                },
                ["nujelm"] = new()
                {
                    Shops = [],
                    Buildings = [],
                    Landmarks = ["Nujel'm palace", "Nujel'm arena", "Nujel'm docks", "desert streets"]
                },
                ["ocllo"] = new()
                {
                    Shops = [],
                    Buildings = [],
                    Landmarks = ["Ocllo farms", "Ocllo docks", "Ocllo training yards", "island roads"]
                },
                ["serpentshold"] = new()
                {
                    Shops = [],
                    Buildings = [],
                    Landmarks = ["Serpent's Hold fortress", "guard posts", "training yards", "Serpent's Hold docks", "armories"]
                },
                ["trinsic"] = new()
                {
                    Shops = ["Trinsic Bank", "Trinsic Provisioner", "Trinsic Shipwright"],
                    Buildings = ["Paladin Hall", "Trinsic Training Yard"],
                    Landmarks = ["south gate", "guard posts", "Trinsic harbor"]
                },
                ["vesper"] = new()
                {
                    Shops = ["Vesper Bank", "Vesper Shipwright", "Vesper Provisioner"],
                    Buildings = ["The Marsh Hall", "Vesper Customs House"],
                    Landmarks = ["Vesper bridges", "canals", "docks"]
                },
                ["skara"] = new()
                {
                    Shops = ["Skara Brae Bank", "Skara Brae Ranger Guild"],
                    Buildings = ["Skara Brae Music Hall", "Skara Brae Ferry Office"],
                    Landmarks = ["ferry", "sheep fields", "island farms"]
                },
                ["jhelom"] = new()
                {
                    Shops = ["Jhelom Bank", "Sailors Guild"],
                    Buildings = ["Jhelom Arena", "Fighter Hall"],
                    Landmarks = ["dueling pits", "practice yards", "Jhelom docks"]
                },
                ["magincia"] = new()
                {
                    Shops = ["Magincia Bank", "Magincia Jeweler", "Magincia Provisioner"],
                    Buildings = ["Magincia Estates", "Magincia Market"],
                    Landmarks = ["gardens", "polished streets", "Magincia docks"]
                },
                ["wind"] = new()
                {
                    Shops =
                    [
                        "Seeker's Inn",
                        "Windy Alchemy",
                        "Mages Things",
                        "Windy Clothes",
                        "Magical Supplies",
                        "The Learned Mage",
                        "Windy Inn",
                        "Windy Healer",
                        "Alchemist of Wind",
                        "Wind Bank",
                        "Mages Appetite"
                    ],
                    Buildings = ["Wind Center", "Seat of Knowledge", "Bunkhouse"],
                    Landmarks = ["North West", "North East", "Park", "hidden entrance", "underground halls"]
                },
                ["wilderness"] = new()
                {
                    Shops = [],
                    Buildings = [],
                    Landmarks =
                    [
                        "old roads",
                        "campsites",
                        "shrines",
                        "ruined walls",
                        "deep woods",
                        "river crossings",
                        "Brigand Camps",
                        "Cemeteries",
                        "Cove Orc Fort",
                        "Fire Temple",
                        "Forgotten Pyramid",
                        "Hidden Valley",
                        "Lizardman Village",
                        "Ratman Lair",
                        "Ratman Village",
                        "Serpentine Passage",
                        "Temple of Knowledge",
                        "The Hedge Maze",
                        "Britain Passage",
                        "Castle Blackthorn",
                        "Covetous",
                        "Deceit",
                        "Despise",
                        "Destard",
                        "Fire",
                        "Hythloth",
                        "Ice",
                        "Lizardman Passage",
                        "Ancient Wyrm Lair",
                        "Orc Caves",
                        "Ratman Cellar",
                        "Ratman Mine",
                        "Shame",
                        "Wind",
                        "Wrong"
                    ]
                }
            }
        };
    }
}
