using System.Text.Json.Serialization;

namespace IdempotentAPI.TestWebAPIs3;

[Serializable]
public class Response
{
    [JsonInclude]
    public string? Message { get; set; }

    [JsonInclude]
    public string? IdempotencyKey { get; set; }

    [JsonInclude]
    public DateTime Timestamp { get; set; }
}