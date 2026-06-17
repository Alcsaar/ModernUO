using System;
using Server.Custom.Systems.AIIntegration;
using Server.Commands;

namespace Server.Custom.Systems.VirtualEcology;

public static class TownChatterCommands
{
    public static void Configure()
    {
        CommandSystem.Register("VirtualEcologyAdmin", AccessLevel.GameMaster, TownChatterAdmin_OnCommand);
        CommandSystem.Register("VEAdmin", AccessLevel.GameMaster, TownChatterAdmin_OnCommand);
        CommandSystem.Register("VEGump", AccessLevel.GameMaster, TownChatterAdmin_OnCommand);
        CommandSystem.Register("TownChatterAdmin", AccessLevel.GameMaster, TownChatterAdmin_OnCommand);
        CommandSystem.Register("TCAdmin", AccessLevel.GameMaster, TownChatterAdmin_OnCommand);
        CommandSystem.Register("TCGump", AccessLevel.GameMaster, TownChatterAdmin_OnCommand);
        CommandSystem.Register("TownChatterGenerate", AccessLevel.GameMaster, TownChatterGenerate_OnCommand);
        CommandSystem.Register("TCGen", AccessLevel.GameMaster, TownChatterGenerate_OnCommand);
        CommandSystem.Register("TownChatterPreview", AccessLevel.GameMaster, TownChatterPreview_OnCommand);
        CommandSystem.Register("TCPreview", AccessLevel.GameMaster, TownChatterPreview_OnCommand);
        CommandSystem.Register("TCPrev", AccessLevel.GameMaster, TownChatterPreview_OnCommand);
        CommandSystem.Register("TownChatterRegenerate", AccessLevel.GameMaster, TownChatterRegenerate_OnCommand);
        CommandSystem.Register("TCRegen", AccessLevel.GameMaster, TownChatterRegenerate_OnCommand);
        CommandSystem.Register("TownChatterRegenerateAll", AccessLevel.GameMaster, TownChatterRegenerateAll_OnCommand);
        CommandSystem.Register("TCRegenAll", AccessLevel.GameMaster, TownChatterRegenerateAll_OnCommand);
        CommandSystem.Register("TownChatterTopUpAll", AccessLevel.GameMaster, TownChatterTopUpAll_OnCommand);
        CommandSystem.Register("TCTopUpAll", AccessLevel.GameMaster, TownChatterTopUpAll_OnCommand);
        CommandSystem.Register("TownChatterDynamic", AccessLevel.GameMaster, TownChatterDynamic_OnCommand);
        CommandSystem.Register("TCDyn", AccessLevel.GameMaster, TownChatterDynamic_OnCommand);
        CommandSystem.Register("TownChatterFacts", AccessLevel.GameMaster, TownChatterFacts_OnCommand);
        CommandSystem.Register("TCFacts", AccessLevel.GameMaster, TownChatterFacts_OnCommand);
        CommandSystem.Register("TownChatterFactLine", AccessLevel.GameMaster, TownChatterFactLine_OnCommand);
        CommandSystem.Register("TCFactLine", AccessLevel.GameMaster, TownChatterFactLine_OnCommand);
        CommandSystem.Register("TownChatterDelete", AccessLevel.GameMaster, TownChatterDelete_OnCommand);
        CommandSystem.Register("TCDelete", AccessLevel.GameMaster, TownChatterDelete_OnCommand);
        CommandSystem.Register("TCDel", AccessLevel.GameMaster, TownChatterDelete_OnCommand);
        CommandSystem.Register("TownChatterClear", AccessLevel.GameMaster, TownChatterClear_OnCommand);
        CommandSystem.Register("TCClear", AccessLevel.GameMaster, TownChatterClear_OnCommand);
    }

    private static void TownChatterAdmin_OnCommand(CommandEventArgs e)
    {
        TownChatterGump.DisplayTo(e.Mobile, e.ArgString?.Trim());
    }

    private static async void TownChatterGenerate_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage("AI integration is disabled. Enable the ai_integration custom feature flag first.");
            return;
        }

        var args = e.ArgString?.Trim();
        if (string.IsNullOrWhiteSpace(args))
        {
            from.SendMessage("Usage: [TownChatterGenerate <town> [count] [theme]");
            return;
        }

        var town = args;
        var count = TownChatterService.DefaultLineCount;
        string theme = null;
        var firstSpace = args.IndexOf(' ');

        if (firstSpace >= 0)
        {
            town = args[..firstSpace].Trim();
            var rest = args[(firstSpace + 1)..].Trim();
            var nextSpace = rest.IndexOf(' ');

            if (nextSpace >= 0 && int.TryParse(rest[..nextSpace], out var parsedCount))
            {
                count = parsedCount;
                theme = rest[(nextSpace + 1)..].Trim();
            }
            else if (int.TryParse(rest, out parsedCount))
            {
                count = parsedCount;
            }
            else
            {
                theme = rest;
            }
        }

        from.SendMessage($"Generating {Math.Clamp(count, 3, TownChatterService.MaxLineCount)} chatter lines for {town}...");

        var cache = await TownChatterService.GenerateAsync(town, count, theme);

        PostToGameLoop(() =>
        {
            if (from?.Deleted != false)
            {
                return;
            }

            from.SendMessage($"Generated {cache.Lines.Count} chatter lines for {cache.Town}.");
            SendPreview(from, cache);
        });
    }

    private static void TownChatterPreview_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var town = e.ArgString?.Trim();

        if (string.IsNullOrWhiteSpace(town))
        {
            from.SendMessage("Usage: [TownChatterPreview <town>");
            return;
        }

        if (!TownChatterService.TryGetCache(town, out var cache))
        {
            from.SendMessage($"No cached town chatter for {town}.");
            return;
        }

        SendPreview(from, cache);
    }

    private static async void TownChatterRegenerate_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var town = e.ArgString?.Trim();

        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage("AI integration is disabled. Enable the ai_integration custom feature flag first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(town))
        {
            from.SendMessage("Usage: [TownChatterRegenerate <town>");
            return;
        }

        from.SendMessage($"Regenerating cached town chatter for {town}...");

        var cache = await TownChatterService.RegenerateAsync(town);

        PostToGameLoop(() =>
        {
            if (from?.Deleted != false)
            {
                return;
            }

            from.SendMessage($"Regenerated {cache.Lines.Count} chatter lines for {cache.Town}.");
            SendPreview(from, cache);
        });
    }

    private static async void TownChatterRegenerateAll_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var count = TownChatterService.DefaultLineCount;

        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage("AI integration is disabled. Enable the ai_integration custom feature flag first.");
            return;
        }

        var args = e.ArgString?.Trim();
        if (!string.IsNullOrWhiteSpace(args) && !int.TryParse(args, out count))
        {
            from.SendMessage("Usage: [TownChatterRegenerateAll [count]");
            return;
        }

        count = Math.Clamp(count, 3, TownChatterService.MaxLineCount);
        from.SendMessage($"Regenerating cached town chatter for {TownChatterService.DefaultTowns.Length} towns...");

        var caches = await TownChatterService.RegenerateAllAsync(count);

        PostToGameLoop(() =>
        {
            if (from?.Deleted != false)
            {
                return;
            }

            for (var i = 0; i < caches.Count; i++)
            {
                var cache = caches[i];
                from.SendMessage($"{cache.Town}: {cache.Lines.Count} line(s), {cache.RejectedLines.Count} rejected.");
            }
        });
    }

    private static async void TownChatterTopUpAll_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var count = TownChatterService.AutoTopUpLineCount;

        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage("AI integration is disabled. Enable the ai_integration custom feature flag first.");
            return;
        }

        var args = e.ArgString?.Trim();
        if (!string.IsNullOrWhiteSpace(args) && !int.TryParse(args, out count))
        {
            from.SendMessage("Usage: [TownChatterTopUpAll [count]");
            return;
        }

        count = Math.Clamp(count, 1, TownChatterService.MaxLineCount);
        from.SendMessage($"Adding {count} fresh town chatter line(s) to each default town...");

        var caches = await TownChatterService.TopUpAllAsync(count);

        PostToGameLoop(() =>
        {
            if (from?.Deleted != false)
            {
                return;
            }

            for (var i = 0; i < caches.Count; i++)
            {
                var cache = caches[i];
                from.SendMessage($"{cache.Town}: {cache.Lines.Count}/{TownChatterService.MaxLineCount} line(s), {cache.RejectedLines.Count} rejected.");
            }
        });
    }

    private static async void TownChatterDynamic_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage("AI integration is disabled. Enable the ai_integration custom feature flag first.");
            return;
        }

        var args = e.ArgString?.Trim();
        if (string.IsNullOrWhiteSpace(args))
        {
            from.SendMessage("Usage: [TownChatterDynamic <town> [nearby context]");
            return;
        }

        var town = args;
        string nearbyContext = null;
        var firstSpace = args.IndexOf(' ');

        if (firstSpace >= 0)
        {
            town = args[..firstSpace].Trim();
            nearbyContext = args[(firstSpace + 1)..].Trim();
        }

        from.SendMessage($"Generating dynamic town chatter reaction for {town}...");
        var line = await TownChatterService.GenerateDynamicReactionAsync(town, nearbyContext);

        PostToGameLoop(() =>
        {
            if (from?.Deleted != false)
            {
                return;
            }

            from.SendMessage(line);
        });
    }

    private static void TownChatterFacts_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var facts = TownChatterService.RecentFacts;

        from.SendMessage($"Town chatter facts: {facts.Count}");

        var start = Math.Max(0, facts.Count - 10);

        for (var i = facts.Count - 1; i >= start; i--)
        {
            var fact = facts[i];
            var location = string.IsNullOrWhiteSpace(fact.Location) ? string.Empty : $" in {fact.Location}";
            from.SendMessage($"{i + 1}. {fact.Kind}: {fact.PlayerName} {fact.Detail}{location}");
        }
    }

    private static void TownChatterFactLine_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        from.SendMessage("Generating chatter from the latest recorded server fact...");
        from.SendMessage(TownChatterService.GenerateLatestFactComment());
    }


    private static void TownChatterDelete_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var args = e.ArgString?.Trim();

        if (string.IsNullOrWhiteSpace(args))
        {
            from.SendMessage("Usage: [TownChatterDelete <town> <line number>");
            return;
        }

        var split = args.LastIndexOf(' ');
        if (split <= 0 || !int.TryParse(args[(split + 1)..], out var index))
        {
            from.SendMessage("Usage: [TownChatterDelete <town> <line number>");
            return;
        }

        var town = args[..split].Trim();

        if (TownChatterService.DeleteLine(town, index, out var removedLine))
        {
            from.SendMessage($"Deleted {town} chatter line {index}: {removedLine}");
            return;
        }

        from.SendMessage($"No cached {town} chatter line {index}.");
    }

    private static void TownChatterClear_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var town = e.ArgString?.Trim();

        if (string.IsNullOrWhiteSpace(town))
        {
            from.SendMessage("Usage: [TownChatterClear <town>");
            return;
        }

        from.SendMessage(
            TownChatterService.Clear(town)
                ? $"Cleared cached town chatter for {town}."
                : $"No cached town chatter for {town}."
        );
    }

    private static void SendPreview(Mobile from, TownChatterCache cache)
    {
        from.SendMessage($"{cache.Town} town chatter ({cache.GeneratedAt:g})");

        for (var i = 0; i < cache.Lines.Count; i++)
        {
            from.SendMessage($"{i + 1}. {cache.Lines[i]}");
        }

        if (cache.RejectedLines.Count > 0)
        {
            from.SendMessage($"Rejected {cache.RejectedLines.Count} generated line(s).");

            for (var i = 0; i < cache.RejectedLines.Count && i < 5; i++)
            {
                from.SendMessage($"Rejected {i + 1}: {cache.RejectedLines[i]}");
            }
        }
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
}
