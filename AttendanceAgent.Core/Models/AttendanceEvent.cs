using System.Text.Json.Serialization;

namespace AttendanceAgent.Core.Models;

public class AttendanceEvent
{
    [JsonPropertyName("device_user_id")]
    public string DeviceUserId { get; set; } = "";

    [JsonPropertyName("event_time_local")]
    public string EventTimeLocal { get; set; } = ""; // ISO8601: 2026-01-12T19:00:00+07:00

    [JsonPropertyName("event_time_utc")]
    public string EventTimeUtc { get; set; } = ""; // ISO8601: 2026-01-12T12:00:00+00:00

    [JsonPropertyName("method")]
    public string Method { get; set; } = "CARD"; // CARD, FINGER, FACE, PASSWORD, OTHER

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "UNKNOWN"; // IN, OUT, UNKNOWN

    [JsonPropertyName("device_event_id")]
    public string? DeviceEventId { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, object>? Meta { get; set; }
}

public class IngestBatchRequest
{
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = "";

    [JsonPropertyName("batch_id")]
    public string? BatchId { get; set; }

    [JsonPropertyName("cursor")]
    public Dictionary<string, object>? Cursor { get; set; }

    [JsonPropertyName("events")]
    public List<AttendanceEvent> Events { get; set; } = new();
}

public class IngestBatchResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("processed")]
    public int Processed { get; set; }

    [JsonPropertyName("duplicates")]
    public int Duplicates { get; set; }

    [JsonPropertyName("rejected")]
    public int Rejected { get; set; }

    [JsonPropertyName("accepted_cursor")]
    public Dictionary<string, object>? AcceptedCursor { get; set; }
}