using Server.Targeting;
using System;

namespace Server.Custom.Systems.TravelCodex;

public static class TravelCodexCommands
{
    public static void Configure()
    {
        CommandSystem.Register("TravelAddStone", AccessLevel.GameMaster, TravelAddStone_OnCommand);
        CommandSystem.Register("tas", AccessLevel.GameMaster, TravelAddStone_OnCommand);
        CommandSystem.Register("TravelValidate", AccessLevel.GameMaster, TravelValidate_OnCommand);
        CommandSystem.Register("td", AccessLevel.GameMaster, TravelValidate_OnCommand);
        CommandSystem.Register("TravelUnlock", AccessLevel.GameMaster, TravelUnlock_OnCommand);
        CommandSystem.Register("tu", AccessLevel.GameMaster, TravelUnlock_OnCommand);
        CommandSystem.Register("TravelRemove", AccessLevel.GameMaster, TravelRemove_OnCommand);
        CommandSystem.Register("tr", AccessLevel.GameMaster, TravelRemove_OnCommand);
        CommandSystem.Register("TravelUnlockAll", AccessLevel.GameMaster, TravelUnlockAll_OnCommand);
        CommandSystem.Register("tua", AccessLevel.GameMaster, TravelUnlockAll_OnCommand);
        CommandSystem.Register("TravelRemoveAll", AccessLevel.GameMaster, TravelRemoveAll_OnCommand);
        CommandSystem.Register("tra", AccessLevel.GameMaster, TravelRemoveAll_OnCommand);
        CommandSystem.Register("TravelGiveCodex", AccessLevel.GameMaster, TravelGiveCodex_OnCommand);
        CommandSystem.Register("tgc", AccessLevel.GameMaster, TravelGiveCodex_OnCommand);
        CommandSystem.Register("TravelExportLocations", AccessLevel.Administrator, TravelExportLocations_OnCommand);
        CommandSystem.Register("texport", AccessLevel.Administrator, TravelExportLocations_OnCommand);
        CommandSystem.Register("TravelImportLocations", AccessLevel.Administrator, TravelImportLocations_OnCommand);
        CommandSystem.Register("timport", AccessLevel.Administrator, TravelImportLocations_OnCommand);
    }

    [Usage("TravelAddStone <destinationKey> <category> <discoverRange> <display name>")]
    [Aliases("tas")]
    [Description("Create a hidden disabled travel stone and place it at a targeted location.")]
    private static void TravelAddStone_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (e.Length < 4)
        {
            from.SendMessage("Usage: [TravelAddStone <destinationKey> <category> <discoverRange> <display name>");
            return;
        }

        var destinationKey = TravelCodexManager.NormalizeKey(e.GetString(0));
        if (string.IsNullOrWhiteSpace(destinationKey))
        {
            from.SendMessage(0x22, "DestinationKey is required.");
            return;
        }

        if (!Enum.TryParse<TravelCategory>(e.GetString(1), true, out var category))
        {
            from.SendMessage(0x22, "Unknown category. Valid: Town, Dungeon, Shrine, POV, Custom.");
            return;
        }

        var discoverRange = e.GetInt32(2);
        if (discoverRange <= 0)
        {
            from.SendMessage(0x22, "DiscoverRange must be greater than zero.");
            return;
        }

        var displayName = string.Join(" ", e.Arguments, 3, e.Arguments.Length - 3).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            from.SendMessage(0x22, "DisplayName is required.");
            return;
        }

        if (TravelCodexManager.HasConflictingDestinationKey(null, destinationKey))
        {
            from.SendMessage(0x22, $"DestinationKey '{destinationKey}' is already in use.");
            return;
        }

        from.SendMessage("Target the world location where the travel stone should be placed.");
        from.Target = new AddStoneTarget(destinationKey, category, discoverRange, displayName);
    }

    private sealed class AddStoneTarget : Target
    {
        private readonly string _destinationKey;
        private readonly TravelCategory _category;
        private readonly int _discoverRange;
        private readonly string _displayName;

        public AddStoneTarget(string destinationKey, TravelCategory category, int discoverRange, string displayName)
            : base(-1, true, TargetFlags.None)
        {
            _destinationKey = destinationKey;
            _category = category;
            _discoverRange = discoverRange;
            _displayName = displayName;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not IPoint3D ip)
            {
                from.SendMessage(0x22, "That is not a valid world location.");
                return;
            }

            var map = from.Map;
            var point = new Point3D(ip);

            if (targeted is Item item)
            {
                map = item.Map;
                point = item.GetWorldLocation();
            }
            else if (targeted is Mobile mobile)
            {
                map = mobile.Map;
                point = mobile.Location;
            }
            else if (targeted is LandTarget land)
            {
                map = from.Map;
                point = land.Location;
            }
            else if (targeted is StaticTarget st)
            {
                map = from.Map;
                point = st.Location;
            }

            if (map == null || map == Map.Internal)
            {
                from.SendMessage(0x22, "That is not a valid map.");
                return;
            }

            var stone = new TravelDiscoveryStone
            {
                DestinationKey = _destinationKey,
                DisplayName = _displayName,
                TravelCategory = _category,
                DiscoverRange = _discoverRange,
                TravelMap = map,
                TravelPoint = point,
                Enabled = false
            };

            stone.MoveToWorld(point, map);
            from.SendMessage(0x35, $"Travel stone '{_displayName}' created in a disabled state.");
        }
    }

    [Usage("TravelValidate")]
    [Aliases("td")]
    [Description("Target a travel stone to validate its configuration.")]
    private static void TravelValidate_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target a travel stone to validate.");
        e.Mobile.Target = new ValidateStoneTarget();
    }

    private sealed class ValidateStoneTarget : Target
    {
        public ValidateStoneTarget() : base(-1, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not TravelDiscoveryStone stone)
            {
                from.SendMessage(0x22, "That is not a travel discovery stone.");
                return;
            }

            var errors = TravelCodexManager.ValidateStone(stone);
            if (errors.Length == 0)
            {
                from.SendMessage(0x35, "Travel stone validation passed.");
                return;
            }

            from.SendMessage(0x22, "Travel stone validation failed:");
            for (var i = 0; i < errors.Length; i++)
            {
                from.SendMessage(0x22, $" - {errors[i]}");
            }
        }
    }

    [Usage("TravelUnlock <destinationKey>")]
    [Aliases("tu")]
    [Description("Target a player and grant a single travel codex discovery.")]
    private static void TravelUnlock_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 1)
        {
            e.Mobile.SendMessage("Usage: [TravelUnlock <destinationKey>");
            return;
        }

        var key = TravelCodexManager.NormalizeKey(e.GetString(0));
        e.Mobile.SendMessage($"Target a player to unlock '{key}'.");
        e.Mobile.Target = new PlayerDiscoveryTarget(key, add: true);
    }

    [Usage("TravelRemove <destinationKey>")]
    [Aliases("tr")]
    [Description("Target a player and remove a single travel codex discovery.")]
    private static void TravelRemove_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 1)
        {
            e.Mobile.SendMessage("Usage: [TravelRemove <destinationKey>");
            return;
        }

        var key = TravelCodexManager.NormalizeKey(e.GetString(0));
        e.Mobile.SendMessage($"Target a player to remove '{key}'.");
        e.Mobile.Target = new PlayerDiscoveryTarget(key, add: false);
    }

    private sealed class PlayerDiscoveryTarget : Target
    {
        private readonly string _key;
        private readonly bool _add;

        public PlayerDiscoveryTarget(string key, bool add) : base(-1, false, TargetFlags.None)
        {
            _key = key;
            _add = add;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile mobile || !mobile.Player)
            {
                from.SendMessage(0x22, "That is not a player.");
                return;
            }

            var result = _add
                ? TravelCodexManager.AddDiscovery(mobile, _key)
                : TravelCodexManager.RemoveDiscovery(mobile, _key);

            if (result)
            {
                from.SendMessage(0x35, $"{(_add ? "Added" : "Removed")} '{_key}' for {mobile.Name}.");
            }
            else
            {
                from.SendMessage(0x22, $"No change made for {mobile.Name}.");
            }
        }
    }

    [Usage("TravelUnlockAll")]
    [Aliases("tua")]
    [Description("Target a player and unlock all current travel destinations.")]
    private static void TravelUnlockAll_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target a player to unlock all travel destinations.");
        e.Mobile.Target = new PlayerDiscoveryAllTarget(add: true);
    }

    [Usage("TravelRemoveAll")]
    [Aliases("tra")]
    [Description("Target a player and remove all travel discoveries.")]
    private static void TravelRemoveAll_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target a player to remove all travel discoveries.");
        e.Mobile.Target = new PlayerDiscoveryAllTarget(add: false);
    }

    private sealed class PlayerDiscoveryAllTarget : Target
    {
        private readonly bool _add;

        public PlayerDiscoveryAllTarget(bool add) : base(-1, false, TargetFlags.None)
        {
            _add = add;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile mobile || !mobile.Player)
            {
                from.SendMessage(0x22, "That is not a player.");
                return;
            }

            var count = _add
                ? TravelCodexManager.AddAllDiscoveries(mobile)
                : TravelCodexManager.RemoveAllDiscoveries(mobile);

            from.SendMessage(0x35, $"{(_add ? "Added" : "Removed")} {count} travel discoveries for {mobile.Name}.");
        }
    }

    [Usage("TravelGiveCodex")]
    [Aliases("tgc")]
    [Description("Target a player to place a Travel Codex in their backpack.")]
    private static void TravelGiveCodex_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target a player to receive a Travel Codex.");
        e.Mobile.Target = new GiveCodexTarget();
    }

    private sealed class GiveCodexTarget : Target
    {
        public GiveCodexTarget() : base(-1, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile mobile || !mobile.Player || mobile.Backpack == null)
            {
                from.SendMessage(0x22, "That player cannot receive a codex.");
                return;
            }

            mobile.Backpack.DropItem(new TravelCodex());
            from.SendMessage(0x35, $"A Travel Codex has been placed in {mobile.Name}'s backpack.");
        }
    }

    [Usage("TravelExportLocations [path]")]
    [Aliases("texport")]
    [Description("Exports Travel Codex discovery stone locations for fresh-server restore.")]
    private static void TravelExportLocations_OnCommand(CommandEventArgs e)
    {
        var path = e.Length > 0 ? e.GetString(0) : null;
        var count = TravelCodexLocationExport.Export(path);
        e.Mobile.SendMessage(0x35, $"Exported {count:N0} travel locations to {TravelCodexLocationExport.ResolveDisplayPath(path)}.");
    }

    [Usage("TravelImportLocations [path]")]
    [Aliases("timport")]
    [Description("Imports Travel Codex discovery stone locations from export JSON.")]
    private static void TravelImportLocations_OnCommand(CommandEventArgs e)
    {
        var path = e.Length > 0 ? e.GetString(0) : null;
        if (!TravelCodexLocationExport.Import(path, out var created, out var updated, out var failureReason))
        {
            e.Mobile.SendMessage(0x22, failureReason ?? "Travel location import failed.");
            return;
        }

        e.Mobile.SendMessage(0x35, $"Imported travel locations. Created: {created:N0}. Updated: {updated:N0}.");
    }
}
