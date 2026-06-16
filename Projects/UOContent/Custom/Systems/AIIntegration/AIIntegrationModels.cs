using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Server.Custom.Systems.AIIntegration;

public sealed class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("keep_alive")]
    public string KeepAlive { get; set; }

    [JsonPropertyName("options")]
    public Dictionary<string, object> Options { get; set; }
}

public sealed class OllamaGenerateResponse
{
    [JsonPropertyName("response")]
    public string Response { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public sealed class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModelInfo> Models { get; set; }
}

public sealed class OllamaModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("modified_at")]
    public string ModifiedAt { get; set; }

    [JsonPropertyName("details")]
    public OllamaModelDetails Details { get; set; }
}

public sealed class OllamaModelDetails
{
    [JsonPropertyName("family")]
    public string Family { get; set; }

    [JsonPropertyName("parameter_size")]
    public string ParameterSize { get; set; }

    [JsonPropertyName("quantization_level")]
    public string QuantizationLevel { get; set; }
}
