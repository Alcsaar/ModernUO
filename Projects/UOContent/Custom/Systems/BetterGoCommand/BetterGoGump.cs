using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Network;

namespace Server.Custom.Systems.BetterGoCommand;

/*
 * BEGIN BETTER GO DESTINATION GUMP:
 * Presents staff travel destinations in the custom system gump style, replacing the legacy town branch
 * with richer town destination lists while preserving the non-town legacy Go categories.
 */
public sealed class BetterGoGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueReady = 68;
    private const int ButtonBack = 1;
    private const int ButtonPrevPage = 2;
    private const int ButtonNextPage = 3;
    private const int ButtonFacetBase = 20;
    private const int ButtonCategoryBase = 100;
    private const int ButtonDestinationBase = 1000;
    private const int GumpWidth = 760;
    private const int GumpHeight = 620;
    private const int EntriesPerPage = 12;

    private static readonly Map[] _facetOrder =
    {
        Map.Trammel,
        Map.Felucca,
        Map.Ilshenar,
        Map.Malas,
        Map.Tokuno,
        Map.TerMur
    };

    private Map _map;
    private GoNode _node;
    private int _page;

    public override bool Singleton => true;

    private BetterGoGump(Map map, GoNode node, int page) : base(70, 55)
    {
        _map = BetterGoCommand.IsUsableMap(map) ? map : Map.Trammel;
        _node = node ?? BuildRoot(_map);
        _page = Math.Max(0, page);
    }

    public static void DisplayTo(Mobile from)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel.Counselor)
        {
            return;
        }

        var map = BetterGoCommand.IsUsableMap(from.Map) ? from.Map : Map.Trammel;

        from.CloseGump<BetterGoGump>();
        from.SendGump(new BetterGoGump(map, BuildRoot(map), 0));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);

        builder.AddLabel(30, 22, HueTitle, "Go Destinations");
        builder.AddLabel(30, 50, HueMuted, GetPathLabel(_node));
        DrawFacetButtons(ref builder);
        DrawRule(ref builder, 28, 82, GumpWidth - 56);

        if (_node.Parent != null)
        {
            DrawButton(ref builder, 32, 98, ButtonBack, "Back");
        }

        builder.AddLabel(128, 102, HueHeader, _node.Name);
        builder.AddLabel(590, 102, HueMuted, _map.Name);

        DrawEntries(ref builder);
        DrawFooter(ref builder);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || from.AccessLevel < AccessLevel.Counselor)
        {
            return;
        }

        if (info.ButtonID == ButtonBack)
        {
            if (_node.Parent != null)
            {
                _node = _node.Parent;
                _page = 0;
                from.SendGump(this);
            }

            return;
        }

        if (info.ButtonID == ButtonPrevPage)
        {
            _page = Math.Max(0, _page - 1);
            from.SendGump(this);
            return;
        }

        if (info.ButtonID == ButtonNextPage)
        {
            _page++;
            from.SendGump(this);
            return;
        }

        if (TryGetFacet(info.ButtonID, out var facet))
        {
            _map = facet;
            _node = BuildRoot(_map);
            _page = 0;
            from.SendGump(this);
            return;
        }

        var categoryIndex = info.ButtonID - ButtonCategoryBase;

        if (categoryIndex >= 0 && categoryIndex < _node.Categories.Length)
        {
            _node = _node.Categories[categoryIndex];
            _page = 0;
            from.SendGump(this);
            return;
        }

        var destinationIndex = info.ButtonID - ButtonDestinationBase;

        if (destinationIndex >= 0 && destinationIndex < _node.Destinations.Length)
        {
            var destination = _node.Destinations[destinationIndex];
            BetterGoCommand.MoveToWorld(from, destination.Location, _map);
        }
    }

    private void DrawFacetButtons(ref DynamicGumpBuilder builder)
    {
        for (var i = 0; i < _facetOrder.Length; i++)
        {
            var map = _facetOrder[i];

            if (!BetterGoCommand.IsUsableMap(map))
            {
                continue;
            }

            var x = 306 + i * 72;
            var hue = map == _map ? HueReady : HueText;

            builder.AddButton(x, 23, 4005, 4007, ButtonFacetBase + i);
            builder.AddLabel(x + 30, 25, hue, GetFacetShortName(map));
        }
    }

    private void DrawEntries(ref DynamicGumpBuilder builder)
    {
        var totalEntries = _node.Categories.Length + _node.Destinations.Length;
        var totalPages = GetTotalPages(totalEntries);
        var page = Math.Min(_page, Math.Max(0, totalPages - 1));
        var start = page * EntriesPerPage;
        var end = Math.Min(start + EntriesPerPage, totalEntries);
        var y = 140;

        if (totalEntries == 0)
        {
            builder.AddLabel(44, y, HueMuted, "No destinations are available for this category.");
            return;
        }

        for (var index = start; index < end; index++)
        {
            if (index < _node.Categories.Length)
            {
                DrawCategoryRow(ref builder, _node.Categories[index], index, y);
            }
            else
            {
                var destinationIndex = index - _node.Categories.Length;
                DrawDestinationRow(ref builder, _node.Destinations[destinationIndex], destinationIndex, y);
            }

            y += 34;
        }
    }

    private static void DrawCategoryRow(ref DynamicGumpBuilder builder, GoNode category, int index, int y)
    {
        builder.AddImageTiled(42, y + 30, 676, 1, 2624);
        builder.AddButton(48, y + 4, 4005, 4007, ButtonCategoryBase + index);
        builder.AddLabel(84, y + 6, HueHeader, category.Name);
        builder.AddLabel(426, y + 6, HueMuted, $"{category.Categories.Length:N0} categories");
        builder.AddLabel(548, y + 6, HueMuted, $"{category.Destinations.Length:N0} locations");
    }

    private static void DrawDestinationRow(ref DynamicGumpBuilder builder, GoDestination destination, int index, int y)
    {
        builder.AddImageTiled(42, y + 30, 676, 1, 2624);
        builder.AddButton(48, y + 4, 4005, 4007, ButtonDestinationBase + index);
        builder.AddLabel(84, y + 6, destination.Kind == GoDestinationKind.Bank ? HueReady : HueText, destination.Name);
        builder.AddLabel(426, y + 6, HueMuted, destination.KindName);
        builder.AddLabel(548, y + 6, HueMuted, $"{destination.Location.X} {destination.Location.Y} {destination.Location.Z}");
    }

    private void DrawFooter(ref DynamicGumpBuilder builder)
    {
        var totalEntries = _node.Categories.Length + _node.Destinations.Length;
        var totalPages = GetTotalPages(totalEntries);
        var page = Math.Min(_page, Math.Max(0, totalPages - 1));

        DrawRule(ref builder, 28, 560, GumpWidth - 56);
        builder.AddLabel(342, 580, HueText, $"Page {page + 1}/{Math.Max(1, totalPages)}");

        if (page > 0)
        {
            DrawButton(ref builder, 40, 576, ButtonPrevPage, "Prev");
        }

        if (page + 1 < totalPages)
        {
            DrawButton(ref builder, 650, 576, ButtonNextPage, "Next");
        }
    }

    private static GoNode BuildRoot(Map map)
    {
        var root = new GoNode(map.Name);
        var categories = new List<GoNode>();

        var tree = GoLocations.GetLocations(map);

        if (map == Map.Trammel || map == Map.Felucca)
        {
            categories.Add(BuildBritanniaTowns());
        }

        if (tree?.Root?.Categories != null)
        {
            for (var i = 0; i < tree.Root.Categories.Length; i++)
            {
                var category = tree.Root.Categories[i];

                if ((map == Map.Trammel || map == Map.Felucca) && category.Name.InsensitiveEquals("Towns"))
                {
                    continue;
                }

                categories.Add(CloneLegacyCategory(category));
            }
        }

        root.Categories = categories.ToArray();
        SetParents(root);
        return root;
    }

    private static GoNode CloneLegacyCategory(GoCategory category)
    {
        var node = new GoNode(category.Name)
        {
            Categories = CloneLegacyCategories(category.Categories),
            Destinations = CloneLegacyLocations(category.Locations)
        };

        return node;
    }

    private static GoNode[] CloneLegacyCategories(GoCategory[] categories)
    {
        if (categories == null || categories.Length == 0)
        {
            return Array.Empty<GoNode>();
        }

        var nodes = new GoNode[categories.Length];

        for (var i = 0; i < categories.Length; i++)
        {
            nodes[i] = CloneLegacyCategory(categories[i]);
        }

        return nodes;
    }

    private static GoDestination[] CloneLegacyLocations(GoLocation[] locations)
    {
        if (locations == null || locations.Length == 0)
        {
            return Array.Empty<GoDestination>();
        }

        var destinations = new GoDestination[locations.Length];

        for (var i = 0; i < locations.Length; i++)
        {
            destinations[i] = new GoDestination(locations[i].Name, locations[i].Location, GoDestinationKind.Legacy);
        }

        return destinations;
    }

    private static GoNode BuildBritanniaTowns() => new(
        "Towns",
        new[]
        {
            Town(
                "Britain",
                Bank("West Bank", 1434, 1699, 0),
                Bank("East Bank", 1649, 1606, 20),
                Shop("Mage Shop", 1491, 1576, 30),
                Shop("Healer", 1482, 1612, 20),
                Stable("Stables", 1518, 1549, 25),
                Dock("Docks", 1495, 1742, -3),
                Landmark("British Castle", 1323, 1624, 55),
                Landmark("Blackthorn Castle", 1533, 1415, 56),
                Landmark("Cemetery", 1384, 1497, 10),
                Landmark("Center", 1475, 1645, 20)
            ),
            Town(
                "Buccaneers Den",
                Dock("Docks", 2736, 2166, 0),
                Shop("Provisioner", 2711, 2184, 0),
                Shop("Healer", 2707, 2143, 0),
                Landmark("Bathhouse", 2667, 2084, 5),
                Landmark("Tunnels", 2667, 2069, -20)
            ),
            Town(
                "Cove",
                Bank("Bank", 2236, 1199, 0),
                Shop("Healer", 2243, 1221, 0),
                Stable("Stables", 2240, 1211, 0),
                Landmark("Gates", 2285, 1209, 0),
                Landmark("Guard Post", 2218, 1116, 19),
                Landmark("Orc Fort", 2171, 1332, 0),
                Landmark("Cemetery", 2443, 1123, 5)
            ),
            Town(
                "Jhelom",
                Bank("Bank", 1328, 3771, 0),
                Stable("Stables", 1383, 3828, 0),
                Dock("East Docks", 1492, 3696, -3),
                Dock("West Docks", 1320, 3976, 0),
                Shop("Mage Shop", 1383, 3823, 0),
                Landmark("Fighting Pit", 1398, 3742, -21),
                Landmark("Main Island", 1414, 3816, 0),
                Landmark("Cemetery", 1296, 3719, 0)
            ),
            Town(
                "Magincia",
                Bank("Bank", 3730, 2161, 20),
                Dock("Docks", 3675, 2259, 20),
                Shop("Mage Shop", 3718, 2231, 20),
                Landmark("Parliament", 3792, 2248, 20),
                Landmark("Park", 3719, 2063, 25)
            ),
            Town(
                "Minoc",
                Bank("Bank", 2501, 560, 0),
                Stable("Stables", 2521, 375, 23),
                Shop("Tinker Shop", 2476, 456, 15),
                Shop("Mage Shop", 2572, 472, 15),
                Landmark("Mining Camp", 2583, 528, 15),
                Landmark("Bridge", 2539, 501, 30),
                Landmark("Gypsy Camp", 2540, 651, 0)
            ),
            Town(
                "Moonglow",
                Bank("Bank", 4471, 1176, 0),
                Shop("Mage Shop", 4629, 1208, 0),
                Shop("Lycaeum", 4302, 958, 10),
                Stable("Stables", 4488, 1493, 5),
                Dock("Docks", 4406, 1045, -2),
                Landmark("Telescope", 4707, 1124, 0),
                Landmark("Zoo", 4549, 1378, 8),
                Landmark("Cemetery", 4546, 1338, 8)
            ),
            Town(
                "Nujel'm",
                Bank("Bank", 3768, 1248, 0),
                Dock("Docks", 3803, 1279, 5),
                Shop("Mage Shop", 3762, 1305, 0),
                Stable("Stables", 3664, 1211, 0),
                Landmark("Palace", 3698, 1279, 20),
                Landmark("Chess Board", 3728, 1360, 5),
                Landmark("Cemetery", 3536, 1156, 20)
            ),
            Town(
                "New Haven",
                Bank("Bank", 3496, 2572, 14),
                Shop("Mage Shop", 3494, 2530, 14),
                Stable("Stables", 3536, 2574, 14),
                Dock("Docks", 3678, 2643, 0),
                Landmark("Center", 3506, 2570, 14),
                Landmark("Old Haven", 3650, 2592, 0),
                Landmark("Old Haven North", 3650, 2516, 0)
            ),
            Town(
                "Serpents Hold",
                Bank("Bank", 2896, 3478, 15),
                Dock("Docks", 3007, 3456, 15),
                Shop("Mage Shop", 2968, 3405, 15),
                Stable("Stables", 2910, 3510, 10),
                Landmark("North", 3023, 3417, 15),
                Landmark("South", 2906, 3505, 10),
                Landmark("Guard Post", 3011, 3526, 15)
            ),
            Town(
                "Skara Brae",
                Bank("Bank", 594, 2156, 0),
                Stable("Stables", 638, 2168, 0),
                Shop("Mage Shop", 661, 2134, 0),
                Dock("East Docks", 716, 2233, -3),
                Dock("West Docks", 639, 2236, -3),
                Landmark("North", 746, 2165, 0),
                Landmark("South", 899, 2381, 0),
                Landmark("West", 601, 2171, 0)
            ),
            Town(
                "Trinsic",
                Bank("Bank", 1817, 2825, 0),
                Stable("Stables", 2020, 2847, 0),
                Shop("Mage Shop", 1940, 2711, 30),
                Dock("East Docks", 2071, 2855, -3),
                Landmark("Center", 1927, 2779, 0),
                Landmark("North Gate", 1894, 2666, 0),
                Landmark("South Gate", 2000, 2930, 0),
                Landmark("West Gate", 1832, 2779, 0)
            ),
            Town(
                "Vesper",
                Bank("Bank", 2891, 675, 0),
                Stable("Stables", 2919, 823, 0),
                Shop("Mage Shop", 2970, 640, 0),
                Dock("Docks", 3013, 828, -3),
                Landmark("Center", 2882, 788, 0),
                Landmark("North", 2907, 603, 0),
                Landmark("East", 2760, 981, 0),
                Landmark("Cemetery", 2786, 867, 0)
            ),
            Town(
                "Yew",
                Bank("Bank", 548, 979, 0),
                Shop("Mage Shop", 633, 858, 0),
                Stable("Stables", 535, 992, 0),
                Landmark("Empath Abbey", 635, 860, 0),
                Landmark("Courts and Prisons", 354, 836, 20),
                Landmark("Cemetery", 724, 1138, 0),
                Landmark("Orc Fort", 633, 1499, 0)
            ),
            Town(
                "Delucia",
                Bank("Bank", 5228, 3978, 37),
                Stable("Stables", 5291, 3990, 37),
                Shop("Mage Shop", 5217, 3995, 37),
                Landmark("Watch Tower", 5276, 3945, 37),
                Landmark("Orc Fort", 5210, 3636, 3)
            ),
            Town(
                "Papua",
                Bank("Bank", 5669, 3138, 12),
                Shop("Mage Shop", 5729, 3204, -4),
                Stable("Stables", 5704, 3208, -4),
                Dock("Docks", 5825, 3256, 2),
                Landmark("The Just Inn", 5769, 3176, 0),
                Landmark("Center", 5730, 3208, -4)
            )
        },
        Array.Empty<GoDestination>()
    );

    private static GoNode Town(string name, params GoDestination[] destinations) => new(name, Array.Empty<GoNode>(), destinations);

    private static GoDestination Bank(string name, int x, int y, int z) => Destination(name, x, y, z, GoDestinationKind.Bank);

    private static GoDestination Dock(string name, int x, int y, int z) => Destination(name, x, y, z, GoDestinationKind.Dock);

    private static GoDestination Shop(string name, int x, int y, int z) => Destination(name, x, y, z, GoDestinationKind.Shop);

    private static GoDestination Stable(string name, int x, int y, int z) => Destination(name, x, y, z, GoDestinationKind.Stable);

    private static GoDestination Landmark(string name, int x, int y, int z) => Destination(name, x, y, z, GoDestinationKind.Landmark);

    private static GoDestination Destination(string name, int x, int y, int z, GoDestinationKind kind) =>
        new(name, new Point3D(x, y, z), kind);

    private static void SetParents(GoNode node)
    {
        for (var i = 0; i < node.Categories.Length; i++)
        {
            node.Categories[i].Parent = node;
            SetParents(node.Categories[i]);
        }
    }

    private static bool TryGetFacet(int buttonId, out Map map)
    {
        var index = buttonId - ButtonFacetBase;

        if (index >= 0 && index < _facetOrder.Length)
        {
            map = _facetOrder[index];
            return BetterGoCommand.IsUsableMap(map);
        }

        map = null;
        return false;
    }

    private static string GetPathLabel(GoNode node)
    {
        var parts = new List<string>(4);

        for (var current = node; current != null; current = current.Parent)
        {
            parts.Add(current.Name);
        }

        parts.Reverse();
        return string.Join(" / ", parts);
    }

    private static string GetFacetShortName(Map map) => map switch
    {
        _ when map == Map.Trammel => "Tram",
        _ when map == Map.Felucca => "Fel",
        _ when map == Map.Ilshenar => "Ilsh",
        _ when map == Map.Malas => "Malas",
        _ when map == Map.Tokuno => "Tok",
        _ when map == Map.TerMur => "Ter",
        _ => map?.Name ?? "?"
    };

    private static int GetTotalPages(int totalEntries) => Math.Max(1, (totalEntries + EntriesPerPage - 1) / EntriesPerPage);

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string text)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 34, y + 2, HueText, text);
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }
}

public sealed class GoNode
{
    public GoNode(string name) : this(name, Array.Empty<GoNode>(), Array.Empty<GoDestination>())
    {
    }

    public GoNode(string name, GoNode[] categories, GoDestination[] destinations)
    {
        Name = name;
        Categories = categories ?? Array.Empty<GoNode>();
        Destinations = destinations ?? Array.Empty<GoDestination>();
    }

    public string Name { get; }

    public GoNode Parent { get; set; }

    public GoNode[] Categories { get; set; }

    public GoDestination[] Destinations { get; set; }
}

public sealed class GoDestination
{
    public GoDestination(string name, Point3D location, GoDestinationKind kind)
    {
        Name = name;
        Location = location;
        Kind = kind;
    }

    public string Name { get; }

    public Point3D Location { get; }

    public GoDestinationKind Kind { get; }

    public string KindName => Kind switch
    {
        GoDestinationKind.Bank => "Bank",
        GoDestinationKind.Dock => "Dock",
        GoDestinationKind.Shop => "Shop",
        GoDestinationKind.Stable => "Stable",
        GoDestinationKind.Landmark => "Point",
        _ => "Legacy"
    };
}

public enum GoDestinationKind
{
    Legacy,
    Bank,
    Dock,
    Shop,
    Stable,
    Landmark
}
