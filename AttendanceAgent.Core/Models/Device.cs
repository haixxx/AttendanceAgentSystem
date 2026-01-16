using System.Text.Json.Serialization;

namespace AttendanceAgent.Core.Models;

public class Device
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("brand")]
    public string Brand { get; set; } = string.Empty;

    [JsonPropertyName("connect_mode")]
    public string ConnectMode { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = string.Empty;

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("last_cursor_json")]
    public Dictionary<string, object>? LastCursorJson { get; set; }

    [JsonPropertyName("sdk_profile")]
    public object? SdkProfile { get; set; }

    // Computed property for display name
    public string DisplayName => $"{Brand} @ {Host}:{Port}";
}