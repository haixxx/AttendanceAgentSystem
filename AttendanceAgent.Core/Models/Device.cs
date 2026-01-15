using System.Text.Json.Serialization;

namespace AttendanceAgent.Core.Models;

public class Device
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("brand")]
    public string Brand { get; set; } = "ZKTeco";

    [JsonPropertyName("host")]
    public string Host { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 4370;

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("last_cursor_json")]
    public Dictionary<string, object>? LastCursorJson { get; set; }

    [JsonPropertyName("sdk_profile")]
    public string? SdkProfile { get; set; }
}

public class DeviceListResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("devices")]
    public List<Device> Devices { get; set; } = new();
}