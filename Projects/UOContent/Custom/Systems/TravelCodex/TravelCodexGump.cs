using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Network;

namespace Server.Custom.Systems.TravelCodex;

public sealed class TravelCodexGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueSelected = 2213;
    private const int HueUnselected = 2101;
    private const int HueReady = 68;
    private const int HueCooldown = 33;
    private const int HueEmpty = 2401;

    private const int ButtonClose = 0;
    private const int ButtonLoadCharges = 1;
    private const int ButtonCategoryBase = 100;
    private const int ButtonDestinationBase = 1000;

    private const int GumpWidth = 680;
    private const int GumpHeight = 480;
    private const int CategoryButtonY = 108;
    private const int DestinationPanelX = 20;
    private const int DestinationPanelY = 145;
    private const int DestinationPanelWidth = 640;
    private const int DestinationPanelHeight = 305;
    private const int DestinationStartY = 190;
    private const int DestinationRowHeight = 28;
    private const int DestinationLeftX = 35;
    private const int DestinationRightX = 340;
    private const int MaxRowsPerColumn = 9;
    private const int MaxDisplayedDestinations = MaxRowsPerColumn * 2;

    private static readonly TravelCategory[] Categories =
    {
        TravelCategory.Town,
        TravelCategory.Dungeon,
        TravelCategory.Shrine,
        TravelCategory.POV,
        TravelCategory.Custom
    };

    private readonly Mobile _from;
    private readonly TravelCodex _codex;
    private readonly TravelCategory _selectedCategory;
    private readonly List<TravelDiscoveryStone> _destinations;

    public override bool Singleton => true;

    private TravelCodexGump(Mobile from, TravelCodex codex, TravelCategory selectedCategory) : base(50, 50)
    {
        _from = from;
        _codex = codex;
        _selectedCategory = selectedCategory;
        _destinations = TravelCodexManager.GetDiscoveredStones(from, selectedCategory);
    }

    public static void DisplayTo(Mobile from, TravelCodex codex, TravelCategory selectedCategory)
    {
        if (from == null || codex == null || codex.Deleted)
        {
            return;
        }

        if (!TravelCodexManager.IsSystemEnabled())
        {
            from.SendMessage(0x22, "The travel codex is currently disabled.");
            return;
        }

        if (from.NetState == null || from.Backpack == null || !codex.IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001);
            return;
        }

        from.CloseGump<TravelCodexGump>();
        from.SendGump(new TravelCodexGump(from, codex, NormalizeCategory(selectedCategory)));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, 650, 450);

        builder.AddLabel(268, 18, HueTitle, "Travel Codex");
        builder.AddLabel(200, 44, HueText, GetFlavorText(_selectedCategory));

        builder.AddLabel(30, 76, HueHeader, $"Charges: {_codex.Charges}/{TravelCodexSettings.MaxCharges}");
        builder.AddButton(230, 74, 4005, 4007, ButtonLoadCharges, GumpButtonType.Reply, 0);
        builder.AddLabel(265, 76, HueText, "Load Charges");

        if (TravelCodexManager.TryGetCooldownRemaining(_from, out var remaining))
        {
            builder.AddLabel(420, 76, HueCooldown, $"Cooldown: {remaining:mm\\:ss}");
        }
        else
        {
            builder.AddLabel(420, 76, HueReady, "Cooldown: Ready");
        }

        BuildCategoryButtons(ref builder);
        BuildDestinationPanel(ref builder);
    }

    private void BuildCategoryButtons(ref DynamicGumpBuilder builder)
    {
        var x = 25;

        for (var i = 0; i < Categories.Length; i++)
        {
            var category = Categories[i];
            var selected = category == _selectedCategory;
            var hue = selected ? HueSelected : HueUnselected;

            builder.AddButton(x, CategoryButtonY, 4005, 4007, ButtonCategoryBase + i, GumpButtonType.Reply, 0);
            builder.AddLabel(x + 35, CategoryButtonY + 2, hue, GetCategoryLabel(category));

            x += 126;
        }
    }

    private void BuildDestinationPanel(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(DestinationPanelX, DestinationPanelY, DestinationPanelWidth, DestinationPanelHeight, 9250);
        builder.AddAlphaRegion(DestinationPanelX + 5, DestinationPanelY + 5, DestinationPanelWidth - 10, DestinationPanelHeight - 10);
        builder.AddLabel(35, 160, HueHeader, GetCategoryLabel(_selectedCategory));

        if (_destinations.Count == 0)
        {
            builder.AddLabel(40, 210, HueEmpty, "You have not yet discovered any destinations in this category.");
            return;
        }

        var displayedCount = Math.Min(_destinations.Count, MaxDisplayedDestinations);

        for (var i = 0; i < displayedCount; i++)
        {
            var stone = _destinations[i];
            var column = i / MaxRowsPerColumn;
            var row = i % MaxRowsPerColumn;
            var x = column == 0 ? DestinationLeftX : DestinationRightX;
            var y = DestinationStartY + row * DestinationRowHeight;

            builder.AddButton(x, y, 4005, 4007, ButtonDestinationBase + i, GumpButtonType.Reply, 0);
            builder.AddLabel(x + 35, y + 2, HueText, GetDestinationLabel(stone));
        }

        if (_destinations.Count > MaxDisplayedDestinations)
        {
            builder.AddLabel(40, 428, HueMuted, $"Showing first {MaxDisplayedDestinations} destinations.");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;
        if (from == null || _codex == null || _codex.Deleted)
        {
            return;
        }

        var buttonId = info.ButtonID;
        if (buttonId == ButtonClose)
        {
            return;
        }

        if (buttonId == ButtonLoadCharges)
        {
            TravelCodex.BeginLoadCharges(from, _codex);
            return;
        }

        if (buttonId >= ButtonCategoryBase && buttonId < ButtonCategoryBase + Categories.Length)
        {
            var categoryIndex = buttonId - ButtonCategoryBase;
            var category = Categories[categoryIndex];
            DisplayTo(from, _codex, category);
            return;
        }

        if (buttonId >= ButtonDestinationBase)
        {
            var destinationIndex = buttonId - ButtonDestinationBase;
            if (destinationIndex >= 0 && destinationIndex < _destinations.Count)
            {
                TravelCodexManager.TryBeginTravel(from, _codex, _destinations[destinationIndex]);
            }
        }
    }

    private static TravelCategory NormalizeCategory(TravelCategory category)
    {
        return category switch
        {
            TravelCategory.Town => TravelCategory.Town,
            TravelCategory.Dungeon => TravelCategory.Dungeon,
            TravelCategory.Shrine => TravelCategory.Shrine,
            TravelCategory.POV => TravelCategory.POV,
            TravelCategory.Custom => TravelCategory.Custom,
            _ => TravelCategory.Town
        };
    }

    private static string GetCategoryLabel(TravelCategory category)
    {
        return category switch
        {
            TravelCategory.Town => "Towns",
            TravelCategory.Dungeon => "Dungeons",
            TravelCategory.Shrine => "Shrines",
            TravelCategory.POV => "POIs",
            TravelCategory.Custom => "Custom",
            _ => "Towns"
        };
    }

    private static string GetFlavorText(TravelCategory category)
    {
        var flavor = TravelCategoryInfo.GetFlavorText(category);
        return string.IsNullOrWhiteSpace(flavor) ? "Known roads and civilized paths" : flavor;
    }

    private static string GetDestinationLabel(TravelDiscoveryStone stone)
    {
        if (stone == null)
        {
            return "Unknown";
        }

        return string.IsNullOrWhiteSpace(stone.DisplayName) ? "Unnamed Destination" : stone.DisplayName;
    }
}
