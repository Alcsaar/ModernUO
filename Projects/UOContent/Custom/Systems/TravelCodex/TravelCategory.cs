using System;

namespace Server.Custom.Systems.TravelCodex;

public enum TravelCategory
{
    Town,
    Dungeon,
    Shrine,
    POV,
    Custom
}

public static class TravelCategoryInfo
{
    public static int GetOrder(TravelCategory category)
    {
        return category switch
        {
            TravelCategory.Town => 0,
            TravelCategory.Dungeon => 1,
            TravelCategory.Shrine => 2,
            TravelCategory.POV => 3,
            TravelCategory.Custom => 4,
            _ => 99
        };
    }

    public static string GetDisplayName(TravelCategory category)
    {
        return category switch
        {
            TravelCategory.Town => "Towns",
            TravelCategory.Dungeon => "Dungeons",
            TravelCategory.Shrine => "Shrines",
            TravelCategory.POV => "POVs",
            TravelCategory.Custom => "Custom",
            _ => "Unknown"
        };
    }

    public static int GetHue(TravelCategory category)
    {
        return category switch
        {
            TravelCategory.Town => 0x44,
            TravelCategory.Dungeon => 0x26,
            TravelCategory.Shrine => 0x59,
            TravelCategory.POV => 0x47,
            TravelCategory.Custom => 0x55,
            _ => 0x35
        };
    }

    public static int GetDecorationItemId(TravelCategory category)
    {
        return category switch
        {
            TravelCategory.Town => 5359,
            TravelCategory.Dungeon => 6883,
            TravelCategory.Shrine => 7956,
            TravelCategory.POV => 5365,
            TravelCategory.Custom => 7774,
            _ => 5365
        };
    }

    public static string GetFlavorText(TravelCategory category)
    {
        return category switch
        {
            TravelCategory.Town => "Known roads and civilized paths",
            TravelCategory.Dungeon => "Earned routes into danger",
            TravelCategory.Shrine => "Sacred places of reflection",
            TravelCategory.POV => "Landmarks and distant vistas",
            TravelCategory.Custom => "Custom travel knowledge",
            _ => string.Empty
        };
    }
}
