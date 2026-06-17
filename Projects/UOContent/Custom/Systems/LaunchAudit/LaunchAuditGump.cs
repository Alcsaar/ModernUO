using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Accounting;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Guilds;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;

namespace Server.Custom.Systems.LaunchAudit;

public sealed class LaunchAuditGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueWarn = 33;
    private const int HueReady = 68;
    private const int ButtonRefresh = 1;
    private const int ButtonPrevPage = 2;
    private const int ButtonNextPage = 3;
    private const int ButtonFilterAll = 10;
    private const int ButtonFilterCurrency = 11;
    private const int ButtonFilterLooseMovable = 12;
    private const int ButtonFilterPublicContainer = 13;
    private const int ButtonFilterSpawner = 14;
    private const int ButtonGoBase = 1000;
    private const int EntriesPerPage = 6;
    private const int SummaryRuleY = 250;
    private const int ListHeaderY = 268;
    private const int ListStartY = 326;

    private readonly LaunchAuditReport _report;
    private readonly List<LaunchAuditItemEntry> _entries;
    private readonly int _pageIndex;
    private readonly LaunchAuditFilter _filter;

    public override bool Singleton => true;

    private LaunchAuditGump(int pageIndex, LaunchAuditFilter filter) : base(50, 45)
    {
        _filter = filter;
        _pageIndex = Math.Max(0, pageIndex);
        _report = LaunchAuditService.BuildReport();
        _entries = FilterEntries(_report.Entries, filter);
    }

    public static void DisplayTo(PlayerMobile from, int pageIndex = 0, LaunchAuditFilter filter = LaunchAuditFilter.All)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel.Administrator)
        {
            return;
        }

        from.CloseGump<LaunchAuditGump>();
        from.SendGump(new LaunchAuditGump(pageIndex, filter));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 860, 610, 9270);
        builder.AddAlphaRegion(15, 15, 830, 580);

        builder.AddLabel(330, 20, HueTitle, "Launch Wipe Audit");
        builder.AddButton(724, 20, 4005, 4007, ButtonRefresh);
        builder.AddLabel(758, 22, HueText, "Refresh");

        DrawSummary(ref builder);
        DrawItemList(ref builder);
    }

    private void DrawSummary(ref DynamicGumpBuilder builder)
    {
        DrawRule(ref builder, 28, 54, 804);

        builder.AddLabel(40, 72, HueHeader, "Player Data");
        builder.AddLabel(52, 98, HueText, $"Accounts: {_report.AccountCount:N0}");
        builder.AddLabel(52, 120, HueText, $"Player mobiles: {_report.PlayerMobileCount:N0}");
        builder.AddLabel(52, 142, HueText, $"Guilds: {_report.GuildCount:N0}");
        builder.AddLabel(52, 164, HueText, $"Player houses: {_report.PlayerHouseCount:N0}");
        builder.AddLabel(52, 186, HueText, $"Player vendors: {_report.PlayerVendorCount:N0}");
        builder.AddLabel(52, 208, HueText, $"Player creatures: {_report.PlayerCreatureCount:N0}");

        builder.AddLabel(330, 72, HueHeader, "Currency Risks");
        builder.AddLabel(342, 98, _report.GoldTotal > 0 ? HueWarn : HueReady, $"Gold stacks: {_report.GoldStackCount:N0}");
        builder.AddLabel(342, 120, _report.GoldTotal > 0 ? HueWarn : HueReady, $"Gold total: {_report.GoldTotal:N0}");
        builder.AddLabel(342, 142, _report.BankCheckTotal > 0 ? HueWarn : HueReady, $"Checks: {_report.BankCheckCount:N0}");
        builder.AddLabel(342, 164, _report.BankCheckTotal > 0 ? HueWarn : HueReady, $"Check total: {_report.BankCheckTotal:N0}");
        builder.AddLabel(342, 186, HueMuted, $"Excluded NPC currency: {_report.ExcludedCurrencyCount:N0}");
        builder.AddLabel(342, 208, HueMuted, $"Spawner containers: {_report.ExcludedSpawnerContainerItemCount:N0}");

        builder.AddLabel(610, 72, HueHeader, "World Item Risks");
        builder.AddLabel(622, 98, _report.LooseMovableItemCount > 0 ? HueWarn : HueReady, $"Loose movable: {_report.LooseMovableItemCount:N0}");
        builder.AddLabel(622, 120, _report.PublicContainerItemCount > 0 ? HueWarn : HueReady, $"Public container items: {_report.PublicContainerItemCount:N0}");
        builder.AddLabel(622, 142, HueText, $"All entries: {_report.Entries.Count:N0}");
        builder.AddLabel(622, 164, HueText, $"Filtered entries: {_entries.Count:N0}");
        builder.AddLabel(622, 186, _report.UnexportedSpawnerCount > 0 ? HueWarn : HueReady, $"Unexported spawners: {_report.UnexportedSpawnerCount:N0}");
        builder.AddLabel(622, 208, _report.ChangedSpawnerCount > 0 ? HueWarn : HueReady, $"Changed spawners: {_report.ChangedSpawnerCount:N0}");
        builder.AddLabel(622, 230, HueMuted, $"Locked/reagent/quest spawns: {_report.ExcludedLockedContainerItemCount:N0}/{_report.ExcludedSingleReagentCount:N0}/{_report.ExcludedQuestSpawnerCount:N0}");

        DrawRule(ref builder, 28, SummaryRuleY, 804);
    }

    private void DrawItemList(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(40, ListHeaderY, HueHeader, "Audited Items");
        DrawFilterButton(ref builder, 170, ListHeaderY - 2, ButtonFilterAll, LaunchAuditFilter.All, "All");
        DrawFilterButton(ref builder, 238, ListHeaderY - 2, ButtonFilterCurrency, LaunchAuditFilter.Currency, "Currency");
        DrawFilterButton(ref builder, 342, ListHeaderY - 2, ButtonFilterLooseMovable, LaunchAuditFilter.LooseMovable, "Loose");
        DrawFilterButton(ref builder, 430, ListHeaderY - 2, ButtonFilterPublicContainer, LaunchAuditFilter.PublicContainerItem, "Containers");
        DrawFilterButton(ref builder, 562, ListHeaderY - 2, ButtonFilterSpawner, LaunchAuditFilter.Spawner, "Spawners");

        var totalPages = GetTotalPages(_entries.Count);
        var page = Math.Min(_pageIndex, totalPages - 1);
        var start = page * EntriesPerPage;
        var end = Math.Min(start + EntriesPerPage, _entries.Count);

        builder.AddLabel(704, ListHeaderY, HueText, $"Page {page + 1}/{totalPages}");
        builder.AddLabel(52, 302, HueMuted, "Item");
        builder.AddLabel(368, 302, HueMuted, "Source");
        builder.AddLabel(520, 302, HueMuted, "Location");
        builder.AddLabel(658, 302, HueMuted, "Serial");

        var y = ListStartY;

        if (_entries.Count == 0)
        {
            builder.AddLabel(52, y, HueReady, "No item risks found.");
        }

        for (var i = start; i < end; i++)
        {
            var entry = _entries[i];
            builder.AddImageTiled(40, y + 38, 780, 1, 2624);
            builder.AddLabel(52, y, GetEntryHue(entry.Kind), Truncate(entry.Name, 28));
            builder.AddLabel(52, y + 18, HueMuted, Truncate(entry.Detail, 42));
            builder.AddLabel(368, y, HueMuted, Truncate(entry.SourceContainerType, 22));
            builder.AddLabel(520, y, HueText, Truncate(entry.LocationText, 20));
            builder.AddLabel(658, y, HueMuted, entry.SerialText);
            builder.AddButton(760, y + 8, 4005, 4007, ButtonGoBase + i - start);
            builder.AddLabel(794, y + 10, HueText, "Go");

            y += 42;
        }

        if (page > 0)
        {
            builder.AddButton(610, 574, 4014, 4016, ButtonPrevPage);
            builder.AddLabel(644, 576, HueText, "Prev");
        }

        if (page + 1 < totalPages)
        {
            builder.AddButton(724, 574, 4005, 4007, ButtonNextPage);
            builder.AddLabel(758, 576, HueText, "Next");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender?.Mobile is not PlayerMobile from || from.AccessLevel < AccessLevel.Administrator)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case 0:
                return;
            case ButtonRefresh:
                DisplayTo(from, _pageIndex, _filter);
                return;
            case ButtonPrevPage:
                DisplayTo(from, Math.Max(0, _pageIndex - 1), _filter);
                return;
            case ButtonNextPage:
                DisplayTo(from, _pageIndex + 1, _filter);
                return;
            case ButtonFilterAll:
                DisplayTo(from, 0, LaunchAuditFilter.All);
                return;
            case ButtonFilterCurrency:
                DisplayTo(from, 0, LaunchAuditFilter.Currency);
                return;
            case ButtonFilterLooseMovable:
                DisplayTo(from, 0, LaunchAuditFilter.LooseMovable);
                return;
            case ButtonFilterPublicContainer:
                DisplayTo(from, 0, LaunchAuditFilter.PublicContainerItem);
                return;
            case ButtonFilterSpawner:
                DisplayTo(from, 0, LaunchAuditFilter.Spawner);
                return;
        }

        var page = Math.Min(_pageIndex, GetTotalPages(_entries.Count) - 1);
        var index = page * EntriesPerPage + info.ButtonID - ButtonGoBase;
        if (index >= 0 && index < _entries.Count)
        {
            LaunchAuditService.TeleportToEntry(from, _entries[index]);
            DisplayTo(from, _pageIndex, _filter);
        }
    }

    private static int GetEntryHue(LaunchAuditItemKind kind)
    {
        return kind switch
        {
            LaunchAuditItemKind.Currency => HueWarn,
            LaunchAuditItemKind.LooseMovable => HueWarn,
            LaunchAuditItemKind.PublicContainerItem => HueHeader,
            LaunchAuditItemKind.UnexportedSpawner => HueWarn,
            LaunchAuditItemKind.ChangedSpawner => HueWarn,
            _ => HueText
        };
    }

    private static int GetTotalPages(int count) => Math.Max(1, (count + EntriesPerPage - 1) / EntriesPerPage);

    private void DrawFilterButton(
        ref DynamicGumpBuilder builder,
        int x,
        int y,
        int buttonId,
        LaunchAuditFilter filter,
        string label
    )
    {
        builder.AddButton(x, y, _filter == filter ? 4006 : 4005, 4007, buttonId);
        builder.AddLabel(x + 34, y + 2, _filter == filter ? HueHeader : HueText, label);
    }

    private static List<LaunchAuditItemEntry> FilterEntries(
        IReadOnlyList<LaunchAuditItemEntry> entries,
        LaunchAuditFilter filter
    )
    {
        var filtered = new List<LaunchAuditItemEntry>();

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            if (filter == LaunchAuditFilter.All || MatchesFilter(entry, filter))
            {
                filtered.Add(entry);
            }
        }

        if (filter == LaunchAuditFilter.Currency)
        {
            filtered.Sort(static (left, right) => right.CurrencyValue.CompareTo(left.CurrencyValue));
        }

        return filtered;
    }

    private static bool MatchesFilter(LaunchAuditItemEntry entry, LaunchAuditFilter filter)
    {
        return filter switch
        {
            LaunchAuditFilter.Currency => entry.Kind == LaunchAuditItemKind.Currency,
            LaunchAuditFilter.LooseMovable => entry.Kind == LaunchAuditItemKind.LooseMovable,
            LaunchAuditFilter.PublicContainerItem => entry.Kind == LaunchAuditItemKind.PublicContainerItem,
            LaunchAuditFilter.Spawner => entry.Kind is LaunchAuditItemKind.UnexportedSpawner or LaunchAuditItemKind.ChangedSpawner,
            _ => true
        };
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text ?? string.Empty;
        }

        return $"{text[..Math.Max(0, maxLength - 3)]}...";
    }
}

public static class LaunchAuditService
{
    public static LaunchAuditReport BuildReport()
    {
        var report = new LaunchAuditReport();

        CountPlayerData(report);
        AuditSpawners(report);
        AuditItems(report);

        report.Entries.Sort(static (left, right) =>
        {
            var kind = left.Kind.CompareTo(right.Kind);
            return kind != 0 ? kind : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });

        return report;
    }

    public static void TeleportToEntry(PlayerMobile from, LaunchAuditItemEntry entry)
    {
        if (from == null)
        {
            return;
        }

        var item = World.FindItem(entry.Serial);
        if (item == null || item.Deleted)
        {
            from.SendMessage(0x22, "That audited item no longer exists.");
            return;
        }

        if (!TryGetWorldLocation(item, out var map, out var location))
        {
            from.SendMessage(0x22, "That audited item is not in a valid world location.");
            return;
        }

        /* BEGIN LAUNCH AUDIT STAFF CONTROL: hide staff before moving to audited player-accessible items. */
        from.Hidden = true;
        from.MoveToWorld(location, map);
        from.SendMessage(0x35, $"Teleported hidden to {GetItemName(item)}.");
        /* END LAUNCH AUDIT STAFF CONTROL */
    }

    /* BEGIN LAUNCH WIPE SPAWNER AUDIT: compare live spawners against exported spawn data before wipe planning. */
    private static void AuditSpawners(LaunchAuditReport report)
    {
        var exportedSpawners = LoadExportedSpawnerIndex(report);
        var decorationSpawners = LoadDecorationSpawnerIndex(report);

        foreach (var item in World.Items.Values)
        {
            if (item == null || item.Deleted || item is not ISpawner spawner || !TryGetWorldLocation(item, out var map, out var location))
            {
                continue;
            }

            if (spawner is not BaseSpawner baseSpawner)
            {
                report.UnexportedSpawnerCount++;
                AddSpawnerEntry(
                    report,
                    item,
                    LaunchAuditItemKind.UnexportedSpawner,
                    "Custom/non-exported spawner type; verify it is intentionally preserved or backed up.",
                    string.Empty
                );
                continue;
            }

            if (IsGeneratedQuestSpawner(baseSpawner))
            {
                report.ExcludedQuestSpawnerCount++;
                continue;
            }

            var decorationKey = BuildSpawnerLocationKey(item.GetType().Name, map.Name, location);

            if (!exportedSpawners.TryGetValue(baseSpawner.Guid, out var exported))
            {
                if (decorationSpawners.TryGetValue(decorationKey, out var decoration))
                {
                    var decorationMismatch = FindDecorationSpawnerMismatch(baseSpawner, decoration);

                    if (!string.IsNullOrWhiteSpace(decorationMismatch))
                    {
                        report.ChangedSpawnerCount++;
                        AddSpawnerEntry(report, item, LaunchAuditItemKind.ChangedSpawner, decorationMismatch, decoration.SourceFile);
                    }

                    continue;
                }

                report.UnexportedSpawnerCount++;
                AddSpawnerEntry(
                    report,
                    item,
                    LaunchAuditItemKind.UnexportedSpawner,
                    "No matching GUID in Distribution/Data/Spawns export files.",
                    string.Empty
                );
                continue;
            }

            var mismatch = FindSpawnerMismatch(baseSpawner, map, location, exported);

            if (!string.IsNullOrWhiteSpace(mismatch))
            {
                report.ChangedSpawnerCount++;
                AddSpawnerEntry(report, item, LaunchAuditItemKind.ChangedSpawner, mismatch, exported.SourceFile);
            }
        }
    }

    private static Dictionary<Guid, ExportedSpawnerDefinition> LoadExportedSpawnerIndex(LaunchAuditReport report)
    {
        var index = new Dictionary<Guid, ExportedSpawnerDefinition>();
        var spawnRoot = Path.Combine(Core.BaseDirectory, "Data", "Spawns");

        if (!Directory.Exists(spawnRoot))
        {
            return index;
        }

        foreach (var path in Directory.EnumerateFiles(spawnRoot, "*.json", SearchOption.AllDirectories))
        {
            LoadExportedSpawnerFile(report, index, path, spawnRoot);
        }

        return index;
    }

    private static Dictionary<string, DecorationSpawnerDefinition> LoadDecorationSpawnerIndex(LaunchAuditReport report)
    {
        var index = new Dictionary<string, DecorationSpawnerDefinition>(StringComparer.OrdinalIgnoreCase);
        var decorationRoot = Path.Combine(Core.BaseDirectory, "Data", "Decoration");

        if (!Directory.Exists(decorationRoot))
        {
            return index;
        }

        LoadDecorationSpawnerFolder(report, index, decorationRoot, "Britannia", "Trammel", "Felucca");
        LoadDecorationSpawnerFolder(report, index, decorationRoot, "Trammel", "Trammel");
        LoadDecorationSpawnerFolder(report, index, decorationRoot, "Felucca", "Felucca");
        LoadDecorationSpawnerFolder(report, index, decorationRoot, "Ilshenar", "Ilshenar");
        LoadDecorationSpawnerFolder(report, index, decorationRoot, "Malas", "Malas");
        LoadDecorationSpawnerFolder(report, index, decorationRoot, "Tokuno", "Tokuno");
        LoadDecorationSpawnerFolder(report, index, decorationRoot, "BountyBoards", "Felucca");

        return index;
    }

    private static void LoadDecorationSpawnerFolder(
        LaunchAuditReport report,
        Dictionary<string, DecorationSpawnerDefinition> index,
        string decorationRoot,
        string folderName,
        params ReadOnlySpan<string> mapNames
    )
    {
        var folder = Path.Combine(decorationRoot, folderName);

        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(folder, "*.cfg", SearchOption.TopDirectoryOnly))
        {
            LoadDecorationSpawnerFile(report, index, path, decorationRoot, mapNames);
        }
    }

    private static void LoadDecorationSpawnerFile(
        LaunchAuditReport report,
        Dictionary<string, DecorationSpawnerDefinition> index,
        string path,
        string decorationRoot,
        ReadOnlySpan<string> mapNames
    )
    {
        try
        {
            var lines = File.ReadAllLines(path);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (!IsDecorationSpawnerHeader(line, out var typeName, out var parameters))
                {
                    continue;
                }

                var definition = BuildDecorationSpawnerDefinition(typeName, parameters, Path.GetRelativePath(decorationRoot, path));

                while (++i < lines.Length)
                {
                    var locationLine = lines[i].Trim();

                    if (string.IsNullOrWhiteSpace(locationLine))
                    {
                        break;
                    }

                    if (locationLine.StartsWithOrdinal("#"))
                    {
                        continue;
                    }

                    if (!TryParseDecorationPoint(locationLine, out var location))
                    {
                        i--;
                        break;
                    }

                    for (var mapIndex = 0; mapIndex < mapNames.Length; mapIndex++)
                    {
                        var clone = definition.CloneForLocation(mapNames[mapIndex], location);
                        index[BuildSpawnerLocationKey(clone.TypeName, clone.MapName, clone.Location)] = clone;
                        report.DecorationSpawnerDefinitionCount++;
                    }
                }
            }
        }
        catch (IOException)
        {
            report.SpawnerExportReadFailureCount++;
        }
        catch (UnauthorizedAccessException)
        {
            report.SpawnerExportReadFailureCount++;
        }
    }

    private static bool IsDecorationSpawnerHeader(string line, out string typeName, out string parameters)
    {
        typeName = null;
        parameters = null;

        if (string.IsNullOrWhiteSpace(line) || line.StartsWithOrdinal("#"))
        {
            return false;
        }

        var parenStart = line.IndexOfOrdinal("(");
        var parenEnd = line.LastIndexOf(')');

        if (parenStart < 0 || parenEnd <= parenStart)
        {
            return false;
        }

        var space = line.IndexOfOrdinal(" ");

        if (space <= 0 || space > parenStart)
        {
            return false;
        }

        typeName = line[..space];

        if (!string.Equals(typeName, nameof(Spawner), StringComparison.Ordinal))
        {
            return false;
        }

        parameters = line[(parenStart + 1)..parenEnd];
        return true;
    }

    private static DecorationSpawnerDefinition BuildDecorationSpawnerDefinition(
        string typeName,
        string parameters,
        string sourceFile
    )
    {
        var definition = new DecorationSpawnerDefinition
        {
            TypeName = typeName,
            Count = 1,
            MinDelay = TimeSpan.FromMinutes(5),
            MaxDelay = TimeSpan.FromMinutes(10),
            Team = 0,
            HomeRange = 0,
            WalkingRange = 0,
            SourceFile = sourceFile
        };

        var parts = parameters.Split(';');

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            var separator = part.IndexOfOrdinal("=");

            if (separator <= 0)
            {
                continue;
            }

            var name = part[..separator].Trim();
            var value = part[(separator + 1)..].Trim();

            if (name.Equals("Spawn", StringComparison.OrdinalIgnoreCase))
            {
                definition.Entries.Add(BuildEntrySignature(value, 1, 100, string.Empty, string.Empty));
            }
            else if (name.Equals("Count", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var count))
            {
                definition.Count = count;

                for (var entryIndex = 0; entryIndex < definition.Entries.Count; entryIndex++)
                {
                    definition.Entries[entryIndex] = ReplaceEntryMaxCount(definition.Entries[entryIndex], count);
                }
            }
            else if (name.Equals("MinDelay", StringComparison.OrdinalIgnoreCase) && TimeSpan.TryParse(value, out var minDelay))
            {
                definition.MinDelay = minDelay;
            }
            else if (name.Equals("MaxDelay", StringComparison.OrdinalIgnoreCase) && TimeSpan.TryParse(value, out var maxDelay))
            {
                definition.MaxDelay = maxDelay;
            }
            else if (name.Equals("Team", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var team))
            {
                definition.Team = team;
            }
            else if (name.Equals("HomeRange", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var homeRange))
            {
                definition.HomeRange = homeRange;
            }
            else if (name.Equals("WalkingRange", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var walkingRange))
            {
                definition.WalkingRange = walkingRange;
            }
            else if (name.Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                definition.Name = value;
            }
        }

        return definition;
    }

    private static string FindDecorationSpawnerMismatch(BaseSpawner spawner, DecorationSpawnerDefinition decoration)
    {
        if (
            !string.IsNullOrWhiteSpace(decoration.Name) &&
            !string.Equals(spawner.Name ?? string.Empty, decoration.Name, StringComparison.Ordinal)
        )
        {
            return $"Decoration name differs from {decoration.SourceFile}";
        }

        if (spawner.Count != decoration.Count)
        {
            return $"Decoration Count differs: expected {decoration.Count:N0}, live {spawner.Count:N0}";
        }

        if (spawner.MinDelay != decoration.MinDelay || spawner.MaxDelay != decoration.MaxDelay)
        {
            return $"Decoration delay differs: expected {decoration.MinDelay}-{decoration.MaxDelay}, live {spawner.MinDelay}-{spawner.MaxDelay}";
        }

        if (spawner.Team != decoration.Team)
        {
            return $"Decoration Team differs: expected {decoration.Team:N0}, live {spawner.Team:N0}";
        }

        if (spawner.HomeRange != decoration.HomeRange)
        {
            return $"Decoration HomeRange differs: expected {decoration.HomeRange:N0}, live {spawner.HomeRange:N0}";
        }

        if (decoration.WalkingRange > 0 && spawner.WalkingRange != decoration.WalkingRange)
        {
            return $"Decoration WalkingRange differs: expected {decoration.WalkingRange:N0}, live {spawner.WalkingRange:N0}";
        }

        var liveEntries = BuildLiveEntrySignatures(spawner);

        if (liveEntries.Count != decoration.Entries.Count)
        {
            return $"Decoration entry count differs: expected {decoration.Entries.Count:N0}, live {liveEntries.Count:N0}";
        }

        for (var i = 0; i < liveEntries.Count; i++)
        {
            if (!string.Equals(liveEntries[i], decoration.Entries[i], StringComparison.Ordinal))
            {
                return $"Decoration entry {i + 1:N0} differs from {decoration.SourceFile}";
            }
        }

        return null;
    }

    private static bool IsGeneratedQuestSpawner(BaseSpawner spawner)
    {
        /* BEGIN LAUNCH WIPE SPAWNER AUDIT: MLQuest.PutSpawner creates MLQS-* spawners outside exported spawn JSON. */
        return spawner?.Name?.StartsWith("MLQS-", StringComparison.Ordinal) == true;
        /* END LAUNCH WIPE SPAWNER AUDIT */
    }

    private static bool TryParseDecorationPoint(string line, out Point3D location)
    {
        location = Point3D.Zero;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (
            parts.Length != 3 ||
            !int.TryParse(parts[0], out var x) ||
            !int.TryParse(parts[1], out var y) ||
            !int.TryParse(parts[2], out var z)
        )
        {
            return false;
        }

        location = new Point3D(x, y, z);
        return true;
    }

    private static string ReplaceEntryMaxCount(string signature, int count)
    {
        var parts = signature.Split('|');

        if (parts.Length != 5)
        {
            return signature;
        }

        return BuildEntrySignature(parts[0], count, int.Parse(parts[2]), parts[3], parts[4]);
    }

    private static void LoadExportedSpawnerFile(
        LaunchAuditReport report,
        Dictionary<Guid, ExportedSpawnerDefinition> index,
        string path,
        string spawnRoot
    )
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (!TryReadExportedSpawner(element, path, spawnRoot, out var definition))
                {
                    continue;
                }

                index[definition.Guid] = definition;
                report.ExportedSpawnerDefinitionCount++;
            }
        }
        catch (JsonException)
        {
            report.SpawnerExportReadFailureCount++;
        }
        catch (IOException)
        {
            report.SpawnerExportReadFailureCount++;
        }
        catch (UnauthorizedAccessException)
        {
            report.SpawnerExportReadFailureCount++;
        }
    }

    private static bool TryReadExportedSpawner(
        JsonElement element,
        string path,
        string spawnRoot,
        out ExportedSpawnerDefinition definition
    )
    {
        definition = null;

        if (
            !TryGetString(element, "type", out var type) ||
            !TryGetGuid(element, "guid", out var guid) ||
            !TryGetString(element, "map", out var map) ||
            !TryGetPoint3D(element, "location", out var location)
        )
        {
            return false;
        }

        definition = new ExportedSpawnerDefinition
        {
            Guid = guid,
            TypeName = type,
            Name = TryGetString(element, "name", out var name) ? name : string.Empty,
            MapName = map,
            Location = location,
            Count = TryGetInt(element, "count", out var count) ? count : 1,
            MinDelay = TryGetTimeSpan(element, "minDelay", out var minDelay) ? minDelay : TimeSpan.FromMinutes(5),
            MaxDelay = TryGetTimeSpan(element, "maxDelay", out var maxDelay) ? maxDelay : TimeSpan.FromMinutes(10),
            Team = TryGetInt(element, "team", out var team) ? team : 0,
            HomeRange = TryGetInt(element, "homeRange", out var homeRange) ? homeRange : -1,
            WalkingRange = TryGetInt(element, "walkingRange", out var walkingRange) ? walkingRange : 0,
            Entries = BuildExportedEntrySignatures(element),
            SourceFile = Path.GetRelativePath(spawnRoot, path)
        };

        return true;
    }

    private static string FindSpawnerMismatch(
        BaseSpawner spawner,
        Map map,
        Point3D location,
        ExportedSpawnerDefinition exported
    )
    {
        if (!string.Equals(spawner.GetType().Name, exported.TypeName, StringComparison.OrdinalIgnoreCase))
        {
            return $"Export type differs: {exported.TypeName}";
        }

        if (!string.Equals(map.Name, exported.MapName, StringComparison.OrdinalIgnoreCase) || location != exported.Location)
        {
            return $"Moved from export: {exported.MapName} {FormatPoint(exported.Location)}";
        }

        if (!string.Equals(spawner.Name ?? string.Empty, exported.Name ?? string.Empty, StringComparison.Ordinal))
        {
            return $"Name differs from export file {exported.SourceFile}";
        }

        if (spawner.Count != exported.Count)
        {
            return $"Count differs: export {exported.Count:N0}, live {spawner.Count:N0}";
        }

        if (spawner.MinDelay != exported.MinDelay || spawner.MaxDelay != exported.MaxDelay)
        {
            return $"Delay differs: export {exported.MinDelay}-{exported.MaxDelay}, live {spawner.MinDelay}-{spawner.MaxDelay}";
        }

        if (spawner.Team != exported.Team)
        {
            return $"Team differs: export {exported.Team:N0}, live {spawner.Team:N0}";
        }

        if (exported.HomeRange >= 0 && spawner.HomeRange != exported.HomeRange)
        {
            return $"HomeRange differs: export {exported.HomeRange:N0}, live {spawner.HomeRange:N0}";
        }

        if (exported.WalkingRange > 0 && spawner.WalkingRange != exported.WalkingRange)
        {
            return $"WalkingRange differs: export {exported.WalkingRange:N0}, live {spawner.WalkingRange:N0}";
        }

        var liveEntries = BuildLiveEntrySignatures(spawner);

        if (liveEntries.Count != exported.Entries.Count)
        {
            return $"Entry count differs: export {exported.Entries.Count:N0}, live {liveEntries.Count:N0}";
        }

        for (var i = 0; i < liveEntries.Count; i++)
        {
            if (!string.Equals(liveEntries[i], exported.Entries[i], StringComparison.Ordinal))
            {
                return $"Entry {i + 1:N0} differs from export file {exported.SourceFile}";
            }
        }

        return null;
    }

    private static List<string> BuildLiveEntrySignatures(BaseSpawner spawner)
    {
        var signatures = new List<string>();

        for (var i = 0; i < spawner.Entries.Count; i++)
        {
            var entry = spawner.Entries[i];
            signatures.Add(BuildEntrySignature(
                entry.SpawnedName,
                entry.SpawnedMaxCount,
                entry.SpawnedProbability,
                entry.Properties,
                entry.Parameters
            ));
        }

        return signatures;
    }

    private static List<string> BuildExportedEntrySignatures(JsonElement element)
    {
        var signatures = new List<string>();

        if (!element.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return signatures;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            signatures.Add(BuildEntrySignature(
                TryGetString(entry, "name", out var name) ? name : string.Empty,
                TryGetInt(entry, "maxCount", out var maxCount) ? maxCount : 1,
                TryGetInt(entry, "probability", out var probability) ? probability : 100,
                TryGetString(entry, "properties", out var properties) ? properties : string.Empty,
                TryGetString(entry, "parameters", out var parameters) ? parameters : string.Empty
            ));
        }

        return signatures;
    }

    private static string BuildEntrySignature(
        string name,
        int maxCount,
        int probability,
        string properties,
        string parameters
    )
    {
        return $"{name ?? string.Empty}|{maxCount}|{probability}|{properties ?? string.Empty}|{parameters ?? string.Empty}";
    }

    private static void AddSpawnerEntry(
        LaunchAuditReport report,
        Item item,
        LaunchAuditItemKind kind,
        string detail,
        string sourceFile
    )
    {
        if (!TryGetWorldLocation(item, out var map, out var location))
        {
            return;
        }

        report.Entries.Add(
            new LaunchAuditItemEntry
            {
                Serial = item.Serial,
                Kind = kind,
                Name = GetItemName(item),
                Detail = detail,
                SourceContainerType = string.IsNullOrWhiteSpace(sourceFile) ? item.GetType().Name : TruncateSourceFile(sourceFile),
                LocationText = $"{map.Name} {location.X},{location.Y},{location.Z}",
                SerialText = item.Serial.ToString()
            }
        );
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool TryGetGuid(JsonElement element, string propertyName, out Guid value)
    {
        value = Guid.Empty;
        return TryGetString(element, propertyName, out var text) && Guid.TryParse(text, out value);
    }

    private static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(property.GetString(), out value),
            _ => false
        };
    }

    private static bool TryGetTimeSpan(JsonElement element, string propertyName, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        return TryGetString(element, propertyName, out var text) && TimeSpan.TryParse(text, out value);
    }

    private static bool TryGetPoint3D(JsonElement element, string propertyName, out Point3D value)
    {
        value = Point3D.Zero;

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var values = new int[3];
        var index = 0;

        foreach (var coordinate in property.EnumerateArray())
        {
            if (index >= values.Length || coordinate.ValueKind != JsonValueKind.Number || !coordinate.TryGetInt32(out values[index]))
            {
                return false;
            }

            index++;
        }

        if (index != values.Length)
        {
            return false;
        }

        value = new Point3D(values[0], values[1], values[2]);
        return true;
    }

    private static string FormatPoint(Point3D point) => $"{point.X},{point.Y},{point.Z}";

    private static string BuildSpawnerLocationKey(string typeName, string mapName, Point3D location) =>
        $"{typeName}|{mapName}|{FormatPoint(location)}";

    private static string TruncateSourceFile(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Length <= 22 ? text : $"...{text[^19..]}";
    }
    /* END LAUNCH WIPE SPAWNER AUDIT */

    private static void CountPlayerData(LaunchAuditReport report)
    {
        foreach (var account in Accounts.GetAccounts())
        {
            if (account != null)
            {
                report.AccountCount++;
            }
        }

        report.GuildCount = World.Guilds.Count;
        report.PlayerHouseCount = BaseHouse.AllHouses.Count;

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile == null || mobile.Deleted)
            {
                continue;
            }

            switch (mobile)
            {
                case PlayerMobile:
                    report.PlayerMobileCount++;
                    break;
                case PlayerVendor:
                    report.PlayerVendorCount++;
                    break;
                case BaseCreature creature when creature.Controlled || creature.Summoned || creature.ControlMaster != null:
                    report.PlayerCreatureCount++;
                    break;
            }
        }
    }

    private static void AuditItems(LaunchAuditReport report)
    {
        foreach (var item in World.Items.Values)
        {
            if (item == null || item.Deleted || !IsOnAuditedMap(item))
            {
                continue;
            }

            if (IsSingleStackReagent(item))
            {
                report.ExcludedSingleReagentCount++;
                continue;
            }

            if (item is BaseBook)
            {
                report.ExcludedBookCount++;
                continue;
            }

            if (IsGameItem(item))
            {
                report.ExcludedGameItemCount++;
                continue;
            }

            if (item is MiniatureMushroom)
            {
                report.ExcludedQuestItemCount++;
                continue;
            }

            if (IsSpawnerContainerContent(item))
            {
                report.ExcludedSpawnerContainerItemCount++;
                continue;
            }

            if (IsSafelyExcludedLockedContainerContent(item))
            {
                report.ExcludedLockedContainerItemCount++;
                continue;
            }

            if (item is Gold or BankCheck)
            {
                AuditCurrency(report, item);
            }

            if (!item.Movable)
            {
                continue;
            }

            if (IsLooseWorldItem(item))
            {
                report.LooseMovableItemCount++;
                AddEntry(report, item, LaunchAuditItemKind.LooseMovable, "Loose movable world item");
                continue;
            }

            if (IsMovableItemInPublicWorldContainer(item))
            {
                report.PublicContainerItemCount++;
                AddEntry(report, item, LaunchAuditItemKind.PublicContainerItem, BuildContainerDetail(item));
            }
        }
    }

    private static void AuditCurrency(LaunchAuditReport report, Item item)
    {
        if (IsExcludedVendorOrMonsterCurrency(item))
        {
            report.ExcludedCurrencyCount++;
            return;
        }

        switch (item)
        {
            case Gold gold:
                report.GoldStackCount++;
                report.GoldTotal += gold.Amount;
                AddEntry(report, item, LaunchAuditItemKind.Currency, $"Gold stack worth {gold.Amount:N0}");
                break;
            case BankCheck check:
                report.BankCheckCount++;
                report.BankCheckTotal += check.Worth;
                AddEntry(report, item, LaunchAuditItemKind.Currency, $"Bank check worth {check.Worth:N0}");
                break;
        }
    }

    private static bool IsExcludedVendorOrMonsterCurrency(Item item)
    {
        return item.RootParent switch
        {
            BaseVendor => true,
            BaseGuard => true,
            BaseCreature creature => !creature.Controlled && !creature.Summoned && creature.ControlMaster == null,
            _ => false
        };
    }

    private static bool IsSafelyExcludedLockedContainerContent(Item item)
    {
        return item.RootParent is LockableContainer { Locked: true } root &&
            item != root &&
            BaseHouse.FindHouseAt(root) == null;
    }

    private static bool IsSpawnerContainerContent(Item item)
    {
        return item.RootParent is Container { Spawner: not null } root && item != root;
    }

    private static bool IsSingleStackReagent(Item item)
    {
        return item is BaseReagent { Amount: 1 };
    }

    private static bool IsGameItem(Item item)
    {
        if (item is BasePiece)
        {
            return true;
        }

        if (item is not BaseBoard board)
        {
            return false;
        }

        var items = board.Items;

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not BasePiece)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLooseWorldItem(Item item)
    {
        return item.Parent == null && item.Map != null && item.Map != Map.Internal;
    }

    private static bool IsMovableItemInPublicWorldContainer(Item item)
    {
        if (item.Parent is not Item || item.RootParent is not Item root)
        {
            return false;
        }

        if (root is BankBox or SecureTradeContainer)
        {
            return false;
        }

        if (BaseHouse.FindHouseAt(root) != null)
        {
            return false;
        }

        return root.Map != null && root.Map != Map.Internal;
    }

    private static void AddEntry(
        LaunchAuditReport report,
        Item item,
        LaunchAuditItemKind kind,
        string detail
    )
    {
        if (!TryGetWorldLocation(item, out var map, out var location))
        {
            return;
        }

        report.Entries.Add(
            new LaunchAuditItemEntry
            {
                Serial = item.Serial,
                Kind = kind,
                Name = GetItemName(item),
                Detail = detail,
                SourceContainerType = GetSourceContainerType(item),
                CurrencyValue = GetCurrencyValue(item),
                LocationText = $"{map.Name} {location.X},{location.Y},{location.Z}",
                SerialText = item.Serial.ToString()
            }
        );
    }

    private static bool TryGetWorldLocation(Item item, out Map map, out Point3D location)
    {
        map = null;
        location = Point3D.Zero;

        if (item == null || item.Deleted)
        {
            return false;
        }

        switch (item.RootParent)
        {
            case Mobile mobile when mobile.Map != null && mobile.Map != Map.Internal:
                map = mobile.Map;
                location = mobile.Location;
                return true;
            case Item root when root.Map != null && root.Map != Map.Internal:
                map = root.Map;
                location = root.GetWorldLocation();
                return true;
        }

        if (item.Map == null || item.Map == Map.Internal)
        {
            return false;
        }

        map = item.Map;
        location = item.GetWorldLocation();
        return true;
    }

    private static bool IsOnAuditedMap(Item item)
    {
        return TryGetWorldLocation(item, out var map, out _) && map == Map.Felucca;
    }

    private static string BuildContainerDetail(Item item)
    {
        return item.Parent is Item parent
            ? $"In public/world container: {GetItemName(parent)}"
            : "In public/world container";
    }

    private static string GetSourceContainerType(Item item)
    {
        return item.RootParent is Item root && root != item
            ? root.GetType().Name
            : string.Empty;
    }

    private static string GetItemName(Item item)
    {
        if (!string.IsNullOrWhiteSpace(item?.Name))
        {
            return item.Name;
        }

        return item?.GetType().Name ?? "Unknown item";
    }

    private static long GetCurrencyValue(Item item)
    {
        return item switch
        {
            Gold gold => gold.Amount,
            BankCheck check => check.Worth,
            _ => 0
        };
    }
}

public sealed class LaunchAuditReport
{
    public int AccountCount { get; set; }
    public int PlayerMobileCount { get; set; }
    public int GuildCount { get; set; }
    public int PlayerHouseCount { get; set; }
    public int PlayerVendorCount { get; set; }
    public int PlayerCreatureCount { get; set; }
    public int GoldStackCount { get; set; }
    public long GoldTotal { get; set; }
    public int BankCheckCount { get; set; }
    public long BankCheckTotal { get; set; }
    public int ExcludedCurrencyCount { get; set; }
    public int ExcludedLockedContainerItemCount { get; set; }
    public int ExcludedSpawnerContainerItemCount { get; set; }
    public int ExcludedSingleReagentCount { get; set; }
    public int ExcludedBookCount { get; set; }
    public int ExcludedGameItemCount { get; set; }
    public int ExcludedQuestItemCount { get; set; }
    public int LooseMovableItemCount { get; set; }
    public int PublicContainerItemCount { get; set; }
    public int ExportedSpawnerDefinitionCount { get; set; }
    public int DecorationSpawnerDefinitionCount { get; set; }
    public int SpawnerExportReadFailureCount { get; set; }
    public int UnexportedSpawnerCount { get; set; }
    public int ChangedSpawnerCount { get; set; }
    public int ExcludedQuestSpawnerCount { get; set; }
    public List<LaunchAuditItemEntry> Entries { get; } = new();
}

public sealed class LaunchAuditItemEntry
{
    public Serial Serial { get; set; }
    public LaunchAuditItemKind Kind { get; set; }
    public string Name { get; set; }
    public string Detail { get; set; }
    public string SourceContainerType { get; set; }
    public long CurrencyValue { get; set; }
    public string LocationText { get; set; }
    public string SerialText { get; set; }
}

public enum LaunchAuditItemKind
{
    Currency,
    LooseMovable,
    PublicContainerItem,
    UnexportedSpawner,
    ChangedSpawner
}

public enum LaunchAuditFilter
{
    All,
    Currency,
    LooseMovable,
    PublicContainerItem,
    Spawner
}

public sealed class ExportedSpawnerDefinition
{
    public Guid Guid { get; set; }
    public string TypeName { get; set; }
    public string Name { get; set; }
    public string MapName { get; set; }
    public Point3D Location { get; set; }
    public int Count { get; set; }
    public TimeSpan MinDelay { get; set; }
    public TimeSpan MaxDelay { get; set; }
    public int Team { get; set; }
    public int HomeRange { get; set; }
    public int WalkingRange { get; set; }
    public string SourceFile { get; set; }
    public List<string> Entries { get; set; } = new();
}

public sealed class DecorationSpawnerDefinition
{
    public string TypeName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MapName { get; set; }
    public Point3D Location { get; set; }
    public int Count { get; set; }
    public TimeSpan MinDelay { get; set; }
    public TimeSpan MaxDelay { get; set; }
    public int Team { get; set; }
    public int HomeRange { get; set; }
    public int WalkingRange { get; set; }
    public string SourceFile { get; set; }
    public List<string> Entries { get; set; } = new();

    public DecorationSpawnerDefinition CloneForLocation(string mapName, Point3D location)
    {
        return new DecorationSpawnerDefinition
        {
            TypeName = TypeName,
            Name = Name,
            MapName = mapName,
            Location = location,
            Count = Count,
            MinDelay = MinDelay,
            MaxDelay = MaxDelay,
            Team = Team,
            HomeRange = HomeRange,
            WalkingRange = WalkingRange,
            SourceFile = SourceFile,
            Entries = new List<string>(Entries)
        };
    }
}
