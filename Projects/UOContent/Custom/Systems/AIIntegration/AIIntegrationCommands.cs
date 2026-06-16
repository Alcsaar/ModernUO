using System;
using System.Collections.Generic;
using Server.Commands;

namespace Server.Custom.Systems.AIIntegration;

public static class AIIntegrationCommands
{
    public static void Configure()
    {
        CommandSystem.Register("AIStatus", AccessLevel.GameMaster, AIStatus_OnCommand);
        CommandSystem.Register("AIModels", AccessLevel.GameMaster, AIModels_OnCommand);
        CommandSystem.Register("AITest", AccessLevel.GameMaster, AITest_OnCommand);
        CommandSystem.Register("AIChatterTest", AccessLevel.GameMaster, AIChatterTest_OnCommand);
        CommandSystem.Register("AIWarmup", AccessLevel.GameMaster, AIWarmup_OnCommand);
    }

    private static void AIStatus_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        from.SendMessage($"AI Integration: {(AIIntegrationService.IsEnabled ? "enabled" : "disabled")}");
        from.SendMessage($"Endpoint: {AIIntegrationSettings.OllamaEndpoint}");
        from.SendMessage($"Staff model: {AIIntegrationSettings.StaffModel}");
        from.SendMessage($"Chatter model: {AIIntegrationSettings.ChatterModel}");
        from.SendMessage($"Keep alive: {AIIntegrationSettings.KeepAlive}");
        from.SendMessage($"Prompt limit: {AIIntegrationSettings.MaxPromptLength} chars");
        from.SendMessage(
            $"Staff response/tokens: {AIIntegrationSettings.StaffMaxResponseLength} chars / {AIIntegrationSettings.StaffMaxGeneratedTokens} tokens"
        );
        from.SendMessage(
            $"Chatter response/tokens: {AIIntegrationSettings.ChatterMaxResponseLength} chars / {AIIntegrationSettings.ChatterMaxGeneratedTokens} tokens"
        );
    }

    private static async void AIModels_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage("AI integration is disabled. Enable the ai_integration custom feature flag first.");
            return;
        }

        from.SendMessage("Requesting Ollama model list...");

        var models = await AIIntegrationService.GetModelsAsync();

        Core.LoopContext.Post(() => SendModelList(from, models));
    }

    private static void SendModelList(Mobile from, List<OllamaModelInfo> models)
    {
        if (from?.Deleted != false)
        {
            return;
        }

        if (models.Count == 0)
        {
            from.SendMessage("No Ollama models were returned.");
            return;
        }

        for (var i = 0; i < models.Count; i++)
        {
            var model = models[i];
            var details = model.Details;

            from.SendMessage(
                $"{model.Name} ({FormatBytes(model.Size)}, {details?.ParameterSize ?? "unknown"}, {details?.QuantizationLevel ?? "unknown"})"
            );
        }
    }

    private static async void AITest_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var prompt = e.ArgString?.Trim();

        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage("AI integration is disabled. Enable the ai_integration custom feature flag first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            from.SendMessage("Usage: [AITest <prompt>");
            return;
        }

        from.SendMessage("Sending prompt to Ollama...");

        var response = await AIIntegrationService.GenerateStaffAsync(prompt);

        PostToGameLoop(() =>
        {
            if (from?.Deleted == false)
            {
                from.SendMessage(response);
            }
        });
    }

    private static async void AIChatterTest_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var prompt = e.ArgString?.Trim();

        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage("AI integration is disabled. Enable the ai_integration custom feature flag first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            from.SendMessage("Usage: [AIChatterTest <player/world context>");
            return;
        }

        from.SendMessage("Sending chatter prompt to Ollama...");

        var response = await AIIntegrationService.GenerateChatterAsync(prompt);

        PostToGameLoop(() =>
        {
            if (from?.Deleted == false)
            {
                from.SendMessage(response);
            }
        });
    }

    private static async void AIWarmup_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var profileText = e.ArgString?.Trim();

        if (!AIIntegrationService.IsEnabled)
        {
            from.SendMessage("AI integration is disabled. Enable the ai_integration custom feature flag first.");
            return;
        }

        var warmStaff = string.IsNullOrWhiteSpace(profileText) ||
            profileText.Equals("all", StringComparison.OrdinalIgnoreCase) ||
            profileText.Equals("staff", StringComparison.OrdinalIgnoreCase);
        var warmChatter = string.IsNullOrWhiteSpace(profileText) ||
            profileText.Equals("all", StringComparison.OrdinalIgnoreCase) ||
            profileText.Equals("chatter", StringComparison.OrdinalIgnoreCase);

        if (!warmStaff && !warmChatter)
        {
            from.SendMessage("Usage: [AIWarmup [staff|chatter|all]");
            return;
        }

        from.SendMessage("Warming requested AI model profile...");

        var staffResult = warmStaff ? await AIIntegrationService.WarmAsync(AIRequestProfile.Staff) : null;
        var chatterResult = warmChatter ? await AIIntegrationService.WarmAsync(AIRequestProfile.Chatter) : null;

        PostToGameLoop(() =>
        {
            if (from?.Deleted != false)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(staffResult))
            {
                from.SendMessage(staffResult);
            }

            if (!string.IsNullOrWhiteSpace(chatterResult))
            {
                from.SendMessage(chatterResult);
            }
        });
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

    private static string FormatBytes(long bytes)
    {
        const double gib = 1024.0 * 1024.0 * 1024.0;
        const double mib = 1024.0 * 1024.0;

        if (bytes >= gib)
        {
            return $"{bytes / gib:F1} GiB";
        }

        if (bytes >= mib)
        {
            return $"{bytes / mib:F1} MiB";
        }

        return $"{bytes} bytes";
    }
}
