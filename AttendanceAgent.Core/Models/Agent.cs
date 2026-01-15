using System.Text.Json.Serialization;

namespace AttendanceAgent.Core.Models;

public class AgentRegisterRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = "";

    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}

public class AgentRegisterResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; } = "";
}

public class HeartbeatRequest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("stats")]
    public AgentStats Stats { get; set; } = new();

    [JsonPropertyName("drift_minutes")]
    public int DriftMinutes { get; set; }
}

public class AgentStats
{
    [JsonPropertyName("pulled")]
    public int Pulled { get; set; }

    [JsonPropertyName("pushed")]
    public int Pushed { get; set; }

    [JsonPropertyName("errors")]
    public int Errors { get; set; }
}

public class HeartbeatResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("received")]
    public Dictionary<string, object>? Received { get; set; }
}

public class UpdateCursorRequest
{
    [JsonPropertyName("last_cursor_json")]
    public Dictionary<string, object> LastCursorJson { get; set; } = new();
}

public class UpdateCursorResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }
}