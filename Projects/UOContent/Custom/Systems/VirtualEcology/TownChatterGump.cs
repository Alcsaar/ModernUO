using System;
using System.Globalization;
using Server.Custom.Systems.AIIntegration;
using Server.Gumps;
using Server.Network;

namespace Server.Custom.Systems.VirtualEcology;

public sealed class TownChatterGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueReady = 68;
    private const int HueWarn = 33;
    private const int ButtonRefresh = 1;
    private const int ButtonGenerate = 2;
    private const int ButtonRegenerate = 3;
    private const int ButtonClear = 4;
    private const int ButtonRegenerateAll = 5;
    private const int ButtonAcceptedTab = 6;
    private const int ButtonRejectedTab = 7;
    private const int ButtonPreviousPage = 8;
    private const int ButtonNextPage = 9;
    private const int ButtonSettings = 10;
    private const int ButtonClearAll = 11;
    private const int ButtonAreaBase = 100;
    private const int ButtonDeleteBase = 1000;
    private const int GumpWidth = 1000;
    private const int GumpHeight = 760;
    private const int MaxVisibleLines = 10;
    private const int MaxVisibleRejectedLines = 6;

    private readonly string _selectedTown;
    private readonly bool _showRejected;
    private readonly int _page;

    public override bool Singleton => true;

    private TownChatterGump(string selectedTown, bool showRejected, int page) : base(70, 55)
    {
        _selectedTown = TownChatterService.NormalizeTown(selectedTown);
        _showRejected = showRejected;
        _page = Math.Max(0, page);
    }

    public static void DisplayTo(Mobile from, string selectedTown = null, bool showRejected = false, int page = 0)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        from.CloseGump<TownChatterGump>();
        from.SendGump(new TownChatterGump(selectedTown, showRejected, page));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);

        builder.AddLabel(30, 22, HueTitle, "Virtual Ecology Chatter");
        builder.AddLabel(30, 50, AIIntegrationService.IsEnabled ? HueReady : HueWarn,
            AIIntegrationService.IsEnabled ? "AI integration enabled" : "AI integration disabled");
        DrawButton(ref builder, 770, 22, ButtonRegenerateAll, "Regenerate All");
        DrawButton(ref builder, 610, 22, ButtonClearAll, "Clear All");
        DrawButton(ref builder, 770, 52, ButtonRefresh, "Refresh");
        DrawButton(ref builder, 610, 52, ButtonSettings, "Settings");
        DrawRule(ref builder, 30, 82, 940);

        DrawAreaList(ref builder);
        DrawSelectedTown(ref builder);
    }

    private void DrawAreaList(ref DynamicGumpBuilder builder)
    {
        builder.AddLabel(36, 102, HueHeader, "Areas");

        var y = 134;
        for (var i = 0; i < TownChatterService.DefaultAreas.Length; i++)
        {
            var town = TownChatterService.DefaultAreas[i];
            var selected = string.Equals(town, _selectedTown, StringComparison.OrdinalIgnoreCase);
            var displayName = ToDisplayName(town);
            var hasCache = TownChatterService.TryGetCache(town, out var cache);

            builder.AddButton(38, y - 2, selected ? 4017 : 4005, selected ? 4019 : 4007, ButtonAreaBase + i);
            builder.AddLabel(72, y, selected ? HueTitle : HueText, displayName);
            builder.AddLabel(160, y, hasCache ? HueReady : HueMuted, hasCache ? $"{cache.Lines.Count}" : "-");
            y += 28;
        }
    }

    private void DrawSelectedTown(ref DynamicGumpBuilder builder)
    {
        var displayName = ToDisplayName(_selectedTown);
        var hasCache = TownChatterService.TryGetCache(_selectedTown, out var cache);

        builder.AddLabel(230, 102, HueHeader, displayName);
        builder.AddLabel(330, 102, HueMuted, hasCache ? $"Generated {cache.GeneratedAt:g}" : "No generated chatter");

        if (hasCache)
        {
            DrawButton(ref builder, 230, 132, ButtonRegenerate, "Regenerate");
            DrawButton(ref builder, 370, 132, ButtonClear, "Clear");
        }
        else
        {
            DrawButton(ref builder, 230, 132, ButtonGenerate, "Generate");
        }

        DrawRule(ref builder, 230, 174, 730);

        if (!hasCache)
        {
            builder.AddLabel(240, 198, HueMuted, "No cached chatter for this area.");
            builder.AddLabel(240, 224, HueMuted, $"Use Generate to create a {TownChatterService.DefaultLineCount}-line pool.");
            return;
        }

        DrawButton(ref builder, 240, 194, ButtonAcceptedTab, $"Accepted ({cache.Lines.Count}/{TownChatterService.MaxLineCount})");
        DrawButton(ref builder, 430, 194, ButtonRejectedTab, $"Rejected ({cache.RejectedLines.Count})");
        builder.AddLabel(
            650,
            198,
            HueMuted,
            $"Auto adds {TownChatterService.AutoTopUpLineCount}: {TownChatterService.CatchUpTopUpInterval.TotalMinutes:0}/{TownChatterService.AutoTopUpInterval.TotalMinutes:0} min"
        );

        if (_showRejected)
        {
            DrawRejectedLines(ref builder, cache);
            return;
        }

        DrawAcceptedLines(ref builder, cache);
    }

    private void DrawAcceptedLines(ref DynamicGumpBuilder builder, TownChatterCache cache)
    {
        var page = Math.Min(_page, GetMaxPage(cache.Lines.Count, MaxVisibleLines));
        var offset = page * MaxVisibleLines;
        var remaining = Math.Max(0, cache.Lines.Count - offset);
        var visibleCount = Math.Min(remaining, MaxVisibleLines);

        builder.AddLabel(240, 228, HueHeader, $"Newest accepted lines - page {page + 1}");
        DrawPageButtons(ref builder, page, cache.Lines.Count, MaxVisibleLines);

        var y = 258;
        for (var i = 0; i < visibleCount; i++)
        {
            var sourceIndex = cache.Lines.Count - 1 - offset - i;
            builder.AddButton(238, y - 2, 4017, 4019, ButtonDeleteBase + sourceIndex);
            builder.AddLabel(272, y, HueMuted, $"{sourceIndex + 1}.");
            builder.AddHtml(304, y - 2, 635, 38, ColorHtml(cache.Lines[sourceIndex], "#F2E6B8"));
            y += 42;
        }

        if (cache.Lines.Count == 0)
        {
            builder.AddLabel(240, y, HueMuted, "No accepted chatter lines are cached.");
            y += 32;
        }

        if (cache.Lines.Count > MaxVisibleLines)
        {
            var firstShown = cache.Lines.Count - offset;
            var lastShown = cache.Lines.Count - offset - visibleCount + 1;
            builder.AddLabel(240, y, HueMuted, $"Showing lines {firstShown}-{lastShown} of {cache.Lines.Count}.");
        }
    }

    private void DrawRejectedLines(ref DynamicGumpBuilder builder, TownChatterCache cache)
    {
        var page = Math.Min(_page, GetMaxPage(cache.RejectedLines.Count, MaxVisibleRejectedLines));
        var offset = page * MaxVisibleRejectedLines;
        var remaining = Math.Max(0, cache.RejectedLines.Count - offset);
        var visibleCount = Math.Min(remaining, MaxVisibleRejectedLines);

        builder.AddLabel(240, 228, HueWarn, $"Rejected lines and reasons - page {page + 1}");
        DrawPageButtons(ref builder, page, cache.RejectedLines.Count, MaxVisibleRejectedLines);

        var y = 258;
        if (cache.RejectedLines.Count == 0)
        {
            builder.AddLabel(240, y, HueReady, "No rejected lines.");
            return;
        }

        for (var i = 0; i < visibleCount; i++)
        {
            var sourceIndex = cache.RejectedLines.Count - 1 - offset - i;
            builder.AddLabel(240, y, HueMuted, $"{sourceIndex + 1}.");
            builder.AddHtml(280, y - 2, 660, 58, ColorHtml(cache.RejectedLines[sourceIndex], "#FFCC66"));
            y += 64;
        }

        if (cache.RejectedLines.Count > MaxVisibleRejectedLines)
        {
            var firstShown = cache.RejectedLines.Count - offset;
            var lastShown = cache.RejectedLines.Count - offset - visibleCount + 1;
            builder.AddLabel(240, y, HueMuted, $"Showing rejected lines {firstShown}-{lastShown} of {cache.RejectedLines.Count}.");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case 0:
                return;
            case ButtonRefresh:
                DisplayTo(from, _selectedTown, _showRejected, _page);
                return;
            case ButtonSettings:
                TownChatterSettingsGump.DisplayTo(from, _selectedTown, _showRejected, _page);
                return;
            case ButtonAcceptedTab:
                DisplayTo(from, _selectedTown);
                return;
            case ButtonRejectedTab:
                DisplayTo(from, _selectedTown, true);
                return;
            case ButtonPreviousPage:
                DisplayTo(from, _selectedTown, _showRejected, Math.Max(0, _page - 1));
                return;
            case ButtonNextPage:
                DisplayTo(from, _selectedTown, _showRejected, _page + 1);
                return;
            case ButtonGenerate:
                BeginGenerate(from, _selectedTown, false);
                return;
            case ButtonRegenerate:
                BeginGenerate(from, _selectedTown, true);
                return;
            case ButtonClear:
                TownChatterService.Clear(_selectedTown);
                DisplayTo(from, _selectedTown, _showRejected);
                return;
            case ButtonClearAll:
                var removed = TownChatterService.ClearAll();
                TownChatterService.RestartAutoTopUpTimer();
                from.SendMessage(0x35, $"Cleared {removed} cached chatter area(s).");
                DisplayTo(from, _selectedTown, _showRejected);
                return;
            case ButtonRegenerateAll:
                BeginRegenerateAll(from);
                return;
        }

        if (info.ButtonID >= ButtonAreaBase && info.ButtonID < ButtonAreaBase + TownChatterService.DefaultAreas.Length)
        {
            var index = info.ButtonID - ButtonAreaBase;
            DisplayTo(from, TownChatterService.DefaultAreas[index], _showRejected);
            return;
        }

        if (info.ButtonID >= ButtonDeleteBase)
        {
            var index = info.ButtonID - ButtonDeleteBase + 1;
            TownChatterService.DeleteLine(_selectedTown, index, out _);
            DisplayTo(from, _selectedTown, _showRejected, _page);
        }
    }

    private static async void BeginGenerate(Mobile from, string town, bool regenerate)
    {
        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage(0x22, "AI integration is disabled. Enable the ai_integration feature flag first.");
            DisplayTo(from, town);
            return;
        }

        from.SendMessage(0x35, $"{(regenerate ? "Regenerating" : "Generating")} chatter for {ToDisplayName(town)}...");
        var cache = regenerate
            ? await TownChatterService.RegenerateAsync(town)
            : await TownChatterService.GenerateAsync(town);

        PostToGameLoop(() =>
        {
            if (from?.Deleted != false)
            {
                return;
            }

            from.SendMessage(0x35, $"{(regenerate ? "Regenerated" : "Generated")} {cache.Lines.Count} line(s) for {cache.Town}; {cache.RejectedLines.Count} rejected.");
            DisplayTo(from, town);
        });
    }

    private static async void BeginRegenerateAll(Mobile from)
    {
        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage(0x22, "AI integration is disabled. Enable the ai_integration feature flag first.");
            DisplayTo(from);
            return;
        }

        from.SendMessage(0x35, "Regenerating chatter for all default areas...");
        var caches = await TownChatterService.RegenerateAllAsync();

        PostToGameLoop(() =>
        {
            if (from?.Deleted != false)
            {
                return;
            }

            from.SendMessage(0x35, $"Regenerated {caches.Count} area chatter cache(s).");
            DisplayTo(from);
        });
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 34, y + 2, HueText, label);
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }

    private static void DrawPageButtons(ref DynamicGumpBuilder builder, int page, int itemCount, int pageSize)
    {
        var maxPage = GetMaxPage(itemCount, pageSize);

        if (page > 0)
        {
            DrawButton(ref builder, 675, 224, ButtonPreviousPage, "Previous");
        }

        if (page < maxPage)
        {
            DrawButton(ref builder, 800, 224, ButtonNextPage, "Next");
        }
    }

    private static int GetMaxPage(int itemCount, int pageSize)
    {
        if (itemCount <= 0)
        {
            return 0;
        }

        return (itemCount - 1) / pageSize;
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

    private static string ToDisplayName(string town)
    {
        town = TownChatterService.NormalizeTown(town);

        if (string.Equals(town, "bucsden", StringComparison.OrdinalIgnoreCase))
        {
            return "Buccaneer's Den";
        }

        if (string.Equals(town, "nujelm", StringComparison.OrdinalIgnoreCase))
        {
            return "Nujel'm";
        }

        if (string.Equals(town, "serpentshold", StringComparison.OrdinalIgnoreCase))
        {
            return "Serpent's Hold";
        }

        if (string.Equals(town, "skara", StringComparison.OrdinalIgnoreCase))
        {
            return "Skara Brae";
        }

        var chars = town.ToCharArray();
        var capitalizeNext = true;

        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i]) || chars[i] == '-')
            {
                capitalizeNext = true;
                continue;
            }

            if (capitalizeNext)
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
                capitalizeNext = false;
            }
        }

        return new string(chars);
    }

    private static string EscapeHtml(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string ColorHtml(string value, string color)
    {
        return $"<BASEFONT COLOR={color}>{EscapeHtml(value)}</BASEFONT>";
    }
}

public sealed class TownChatterSettingsGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueReady = 68;
    private const int HueWarn = 33;
    private const int ButtonBack = 1;
    private const int ButtonSave = 2;
    private const int EntryDefaultLineCount = 100;
    private const int EntryMaxLineCount = 101;
    private const int EntryAutoTopUpLineCount = 102;
    private const int EntryAutoTopUpMinutes = 103;
    private const int EntryMaxGenerationAttempts = 104;
    private const int EntryMaxRejectedLineCount = 105;
    private const int EntryMaxCachedDialogueLength = 106;
    private const int EntryMaxDynamicDialogueLength = 107;
    private const int EntryMaxRecentFactCount = 108;
    private const int EntryPlayerDeathMergeMinutes = 109;
    private const int EntryPlayerDeathCooldownHours = 110;
    private const int EntryMovementFactChancePercent = 111;
    private const int EntryMovementFlavorChancePercent = 112;
    private const int EntryPlayerCooldownMinutes = 113;
    private const int EntryNpcCooldownMinutes = 114;
    private const int EntryRecentFactMaxAgeHours = 115;
    private const int EntryServerFirstAnnouncementDays = 116;
    private const int EntryServerFirstSyncSeconds = 117;
    private const int EntryAllowStaffMovementTriggers = 118;
    private const int EntryCatchUpTopUpMinutes = 119;
    private const int EntryLineReuseCooldownMinutes = 120;
    private const int GumpWidth = 760;
    private const int GumpHeight = 660;

    private readonly string _selectedTown;
    private readonly bool _showRejected;
    private readonly int _page;

    public override bool Singleton => true;

    private TownChatterSettingsGump(string selectedTown, bool showRejected, int page) : base(90, 65)
    {
        _selectedTown = TownChatterService.NormalizeTown(selectedTown);
        _showRejected = showRejected;
        _page = Math.Max(0, page);
    }

    public static void DisplayTo(Mobile from, string selectedTown = null, bool showRejected = false, int page = 0)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        from.CloseGump<TownChatterSettingsGump>();
        from.SendGump(new TownChatterSettingsGump(selectedTown, showRejected, page));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var config = VirtualEcologySettings.Snapshot();

        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);

        builder.AddLabel(30, 22, HueTitle, "Virtual Ecology Settings");
        builder.AddLabel(30, 50, HueMuted, $"Config: {VirtualEcologySettings.ConfigPath}");
        DrawButton(ref builder, 570, 22, ButtonSave, "Save Live");
        DrawButton(ref builder, 570, 52, ButtonBack, "Back");
        DrawRule(ref builder, 30, 82, 700);

        builder.AddLabel(40, 104, HueHeader, "Generation");
        var y = 132;
        DrawIntSetting(ref builder, 50, y, "Default lines", EntryDefaultLineCount, config.DefaultLineCount);
        DrawIntSetting(ref builder, 380, y, "Max cached lines", EntryMaxLineCount, config.MaxLineCount);
        y += 34;
        DrawIntSetting(ref builder, 50, y, "Auto top-up lines", EntryAutoTopUpLineCount, config.AutoTopUpLineCount);
        DrawDoubleSetting(ref builder, 380, y, "Auto top-up minutes", EntryAutoTopUpMinutes, config.AutoTopUpInterval.TotalMinutes);
        y += 34;
        DrawDoubleSetting(ref builder, 50, y, "Catch-up minutes", EntryCatchUpTopUpMinutes, config.CatchUpTopUpInterval.TotalMinutes);
        DrawIntSetting(ref builder, 380, y, "Generation attempts", EntryMaxGenerationAttempts, config.MaxGenerationAttempts);
        y += 34;
        DrawIntSetting(ref builder, 50, y, "Rejected line cache", EntryMaxRejectedLineCount, config.MaxRejectedLineCount);
        DrawIntSetting(ref builder, 380, y, "Cached line chars", EntryMaxCachedDialogueLength, config.MaxCachedDialogueLength);
        y += 34;
        DrawIntSetting(ref builder, 50, y, "Live line chars", EntryMaxDynamicDialogueLength, config.MaxDynamicDialogueLength);
        DrawDoubleSetting(ref builder, 380, y, "Line reuse min", EntryLineReuseCooldownMinutes, config.LineReuseCooldown.TotalMinutes);

        builder.AddLabel(40, 316, HueHeader, "Live Movement Chatter");
        y = 344;
        DrawDoubleSetting(ref builder, 50, y, "Real fact chance %", EntryMovementFactChancePercent, config.MovementFactCommentChance * 100.0);
        DrawDoubleSetting(ref builder, 380, y, "AI flavor chance %", EntryMovementFlavorChancePercent, config.MovementFlavorCommentChance * 100.0);
        y += 34;
        DrawDoubleSetting(ref builder, 50, y, "Player cooldown min", EntryPlayerCooldownMinutes, config.PlayerLiveCommentCooldown.TotalMinutes);
        DrawDoubleSetting(ref builder, 380, y, "NPC cooldown min", EntryNpcCooldownMinutes, config.NpcLiveCommentCooldown.TotalMinutes);
        y += 34;
        DrawBoolSetting(ref builder, 50, y, "Staff can trigger 0/1", EntryAllowStaffMovementTriggers, config.AllowStaffMovementTriggers);

        builder.AddLabel(40, 444, HueHeader, "Facts");
        y = 472;
        DrawIntSetting(ref builder, 50, y, "Max recent facts", EntryMaxRecentFactCount, config.MaxRecentFactCount);
        DrawDoubleSetting(ref builder, 380, y, "Recent fact age hrs", EntryRecentFactMaxAgeHours, config.RecentFactMaxAge.TotalHours);
        y += 34;
        DrawDoubleSetting(ref builder, 50, y, "Death merge min", EntryPlayerDeathMergeMinutes, config.PlayerDeathFactMergeWindow.TotalMinutes);
        DrawDoubleSetting(ref builder, 380, y, "Death cooldown hrs", EntryPlayerDeathCooldownHours, config.PlayerDeathFactCooldown.TotalHours);
        y += 34;
        DrawDoubleSetting(ref builder, 50, y, "Server-first days", EntryServerFirstAnnouncementDays, config.ServerFirstAnnouncementMaxAge.TotalDays);
        DrawDoubleSetting(ref builder, 380, y, "Server-first sync sec", EntryServerFirstSyncSeconds, config.ServerFirstFactSyncInterval.TotalSeconds);

        builder.AddLabel(40, 594, HueReady, "Saving applies immediately. Auto top-up restarts with the saved interval.");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case 0:
                return;
            case ButtonBack:
                TownChatterGump.DisplayTo(from, _selectedTown, _showRejected, _page);
                return;
            case ButtonSave:
                SaveSettings(from, info);
                return;
        }
    }

    private void SaveSettings(Mobile from, in RelayInfo info)
    {
        var config = new VirtualEcologyConfig
        {
            DefaultLineCount = ReadInt(info, EntryDefaultLineCount, VirtualEcologySettings.DefaultLineCount),
            MaxLineCount = ReadInt(info, EntryMaxLineCount, VirtualEcologySettings.MaxLineCount),
            AutoTopUpLineCount = ReadInt(info, EntryAutoTopUpLineCount, VirtualEcologySettings.AutoTopUpLineCount),
            AutoTopUpInterval = TimeSpan.FromMinutes(ReadDouble(info, EntryAutoTopUpMinutes, VirtualEcologySettings.AutoTopUpInterval.TotalMinutes)),
            CatchUpTopUpInterval = TimeSpan.FromMinutes(ReadDouble(info, EntryCatchUpTopUpMinutes, VirtualEcologySettings.CatchUpTopUpInterval.TotalMinutes)),
            MaxGenerationAttempts = ReadInt(info, EntryMaxGenerationAttempts, VirtualEcologySettings.MaxGenerationAttempts),
            MaxRejectedLineCount = ReadInt(info, EntryMaxRejectedLineCount, VirtualEcologySettings.MaxRejectedLineCount),
            MaxCachedDialogueLength = ReadInt(info, EntryMaxCachedDialogueLength, VirtualEcologySettings.MaxCachedDialogueLength),
            MaxDynamicDialogueLength = ReadInt(info, EntryMaxDynamicDialogueLength, VirtualEcologySettings.MaxDynamicDialogueLength),
            MaxRecentFactCount = ReadInt(info, EntryMaxRecentFactCount, VirtualEcologySettings.MaxRecentFactCount),
            PlayerDeathFactMergeWindow = TimeSpan.FromMinutes(ReadDouble(info, EntryPlayerDeathMergeMinutes, VirtualEcologySettings.PlayerDeathFactMergeWindow.TotalMinutes)),
            PlayerDeathFactCooldown = TimeSpan.FromHours(ReadDouble(info, EntryPlayerDeathCooldownHours, VirtualEcologySettings.PlayerDeathFactCooldown.TotalHours)),
            MovementFactCommentChance = ReadDouble(info, EntryMovementFactChancePercent, VirtualEcologySettings.MovementFactCommentChance * 100.0) / 100.0,
            MovementFlavorCommentChance = ReadDouble(info, EntryMovementFlavorChancePercent, VirtualEcologySettings.MovementFlavorCommentChance * 100.0) / 100.0,
            AllowStaffMovementTriggers = ReadBool(info, EntryAllowStaffMovementTriggers, VirtualEcologySettings.AllowStaffMovementTriggers),
            PlayerLiveCommentCooldown = TimeSpan.FromMinutes(ReadDouble(info, EntryPlayerCooldownMinutes, VirtualEcologySettings.PlayerLiveCommentCooldown.TotalMinutes)),
            NpcLiveCommentCooldown = TimeSpan.FromMinutes(ReadDouble(info, EntryNpcCooldownMinutes, VirtualEcologySettings.NpcLiveCommentCooldown.TotalMinutes)),
            LineReuseCooldown = TimeSpan.FromMinutes(ReadDouble(info, EntryLineReuseCooldownMinutes, VirtualEcologySettings.LineReuseCooldown.TotalMinutes)),
            RecentFactMaxAge = TimeSpan.FromHours(ReadDouble(info, EntryRecentFactMaxAgeHours, VirtualEcologySettings.RecentFactMaxAge.TotalHours)),
            ServerFirstAnnouncementMaxAge = TimeSpan.FromDays(ReadDouble(info, EntryServerFirstAnnouncementDays, VirtualEcologySettings.ServerFirstAnnouncementMaxAge.TotalDays)),
            ServerFirstFactSyncInterval = TimeSpan.FromSeconds(ReadDouble(info, EntryServerFirstSyncSeconds, VirtualEcologySettings.ServerFirstFactSyncInterval.TotalSeconds))
        };

        VirtualEcologySettings.Save(config);
        TownChatterService.RestartAutoTopUpTimer();

        from.SendMessage(0x35, "Virtual Ecology settings saved and applied.");
        DisplayTo(from, _selectedTown, _showRejected, _page);
    }

    private static int ReadInt(in RelayInfo info, int entryId, int fallback)
    {
        var text = info.GetTextEntry(entryId)?.Trim();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static double ReadDouble(in RelayInfo info, int entryId, double fallback)
    {
        var text = info.GetTextEntry(entryId)?.Trim();
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static bool ReadBool(in RelayInfo info, int entryId, bool fallback)
    {
        var text = info.GetTextEntry(entryId)?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value != 0;
        }

        return bool.TryParse(text, out var parsed) ? parsed : fallback;
    }

    private static void DrawIntSetting(ref DynamicGumpBuilder builder, int x, int y, string label, int entryId, int value)
    {
        DrawTextEntry(ref builder, x, y, label, entryId, value.ToString(CultureInfo.InvariantCulture));
    }

    private static void DrawDoubleSetting(ref DynamicGumpBuilder builder, int x, int y, string label, int entryId, double value)
    {
        DrawTextEntry(ref builder, x, y, label, entryId, value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static void DrawBoolSetting(ref DynamicGumpBuilder builder, int x, int y, string label, int entryId, bool value)
    {
        DrawTextEntry(ref builder, x, y, label, entryId, value ? "1" : "0");
    }

    private static void DrawTextEntry(ref DynamicGumpBuilder builder, int x, int y, string label, int entryId, string value)
    {
        builder.AddLabel(x, y, HueText, label);
        builder.AddBackground(x + 170, y - 2, 90, 24, 9350);
        builder.AddTextEntry(x + 176, y + 1, 78, 20, HueTitle, entryId, value);
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 34, y + 2, HueText, label);
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }
}
