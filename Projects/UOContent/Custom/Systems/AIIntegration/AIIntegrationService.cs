using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Server.Custom.Systems.CustomFeatureFlags;
using Server.Logging;

namespace Server.Custom.Systems.AIIntegration;

public static class AIIntegrationService
{
    private static readonly ILogger Logger = LogFactory.GetLogger(typeof(AIIntegrationService));

    private static HttpClient _httpClient;
    private static string _configuredEndpoint;

    public static void Configure()
    {
        AIIntegrationSettings.Configure();
        RegisterFeatureFlag();
        AIIntegrationCommands.Configure();
    }

    public static bool IsEnabled => CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.AIIntegration);

    public static ValueTask<string> GenerateStaffAsync(string prompt, string model = null) =>
        GenerateAsync(prompt, AIRequestProfile.Staff, model);

    public static ValueTask<string> GenerateChatterAsync(string prompt, string model = null) =>
        GenerateAsync(prompt, AIRequestProfile.Chatter, model);

    public static async ValueTask<string> GenerateAsync(
        string prompt,
        AIRequestProfile profile = AIRequestProfile.Staff,
        string model = null
    )
    {
        if (!IsEnabled)
        {
            return "AI integration is disabled.";
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return "Prompt is empty.";
        }

        prompt = Trim(prompt, AIIntegrationSettings.MaxPromptLength);
        var requestModel = string.IsNullOrWhiteSpace(model) ? AIIntegrationSettings.GetModel(profile) : model.Trim();

        var request = new OllamaGenerateRequest
        {
            Model = requestModel,
            Prompt = BuildPrompt(prompt, profile),
            Stream = false,
            KeepAlive = AIIntegrationSettings.KeepAlive,
            Options = new Dictionary<string, object>
            {
                ["temperature"] = AIIntegrationSettings.GetTemperature(profile),
                ["num_predict"] = AIIntegrationSettings.GetMaxGeneratedTokens(profile)
            }
        };

        try
        {
            var client = GetClient();
            using var response = await client.PostAsJsonAsync("api/generate", request);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning(
                    "Ollama generate request failed with HTTP {StatusCode}",
                    (int)response.StatusCode
                );

                return $"Ollama request failed: {(int)response.StatusCode} {response.ReasonPhrase}";
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
            var generatedText = payload?.Response?.Trim();

            if (string.IsNullOrWhiteSpace(generatedText))
            {
                return "Ollama returned an empty response.";
            }

            return Trim(generatedText, AIIntegrationSettings.GetMaxResponseLength(profile));
        }
        catch (TaskCanceledException ex)
        {
            Logger.Warning(
                ex,
                "Ollama generate request timed out after {TimeoutSeconds} seconds",
                AIIntegrationSettings.RequestTimeout.TotalSeconds
            );

            return "Ollama request timed out. Try a shorter prompt, use the chatter profile, or increase the AIIntegration RequestTimeout setting.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Ollama generate request failed");
            return $"Ollama request failed: {ex.Message}";
        }
    }

    public static async ValueTask<string> WarmAsync(AIRequestProfile profile)
    {
        var model = AIIntegrationSettings.GetModel(profile);
        var result = await GenerateAsync("Reply with exactly: OK", profile, model);
        return string.IsNullOrWhiteSpace(result) ? $"{model}: empty warmup response" : $"{model}: {result}";
    }

    public static async ValueTask<List<OllamaModelInfo>> GetModelsAsync()
    {
        if (!IsEnabled)
        {
            return new List<OllamaModelInfo>();
        }

        try
        {
            var client = GetClient();
            var payload = await client.GetFromJsonAsync<OllamaTagsResponse>("api/tags");
            return payload?.Models ?? new List<OllamaModelInfo>();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Ollama model list request failed");
            return new List<OllamaModelInfo>();
        }
    }

    private static HttpClient GetClient()
    {
        var endpoint = EnsureTrailingSlash(AIIntegrationSettings.OllamaEndpoint);

        if (_httpClient == null || !string.Equals(_configuredEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
        {
            _httpClient?.Dispose();
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(endpoint),
                Timeout = AIIntegrationSettings.RequestTimeout
            };
            _configuredEndpoint = endpoint;
        }

        return _httpClient;
    }

    private static void RegisterFeatureFlag()
    {
        if (CustomFeatureFlagManager.IsRegistered(CustomFeatureFlagKeys.AIIntegration))
        {
            return;
        }

        CustomFeatureFlagManager.Register(
            CustomFeatureFlagKeys.AIIntegration,
            "AI Integration",
            "Enables staff-controlled Ollama AI integration for custom shard tools.",
            "Custom Systems",
            false
        );
    }

    private static string EnsureTrailingSlash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "http://127.0.0.1:11434/";
        }

        return value.EndsWith('/') ? value : $"{value}/";
    }

    private static string BuildPrompt(string prompt, AIRequestProfile profile) => profile switch
    {
        AIRequestProfile.Chatter =>
            "You are an Ultima Online Renaissance town NPC. " +
            "Answer in-character in one or two short sentences. " +
            "Do not mention AI, models, servers, modern technology, or game mechanics unless the player asks a practical gameplay question.\n\n" +
            $"Player or world context: {prompt}",
        _ =>
            "You are a staff-side assistant for a custom Ultima Online Renaissance shard. " +
            "Be concise, practical, and useful for shard administration or content drafting.\n\n" +
            prompt
    };

    private static string Trim(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return $"{value[..(maxLength - 3)]}...";
    }
}
