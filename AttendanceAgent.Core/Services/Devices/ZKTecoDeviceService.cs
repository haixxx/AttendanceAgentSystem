using AttendanceAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AttendanceAgent.Core.Services.Devices; // ← Thêm 's'

public interface IDeviceService
{
    Task<List<AttendanceEvent>> ReadLogsAsync(Models.Device device, Dictionary<string, object>? cursor);
}

/// <summary>
/// STUB Implementation - Cần thay bằng ZKTeco SDK thực tế
/// </summary>
public class ZKTecoDeviceService : IDeviceService
{
    private readonly ILogger<ZKTecoDeviceService> _logger;

    public ZKTecoDeviceService(ILogger<ZKTecoDeviceService> logger)
    {
        _logger = logger;
    }

    public async Task<List<AttendanceEvent>> ReadLogsAsync(Models.Device device, Dictionary<string, object>? cursor)
    {
        _logger.LogWarning("Using STUB implementation - no real SDK integration yet");

        // TODO: Thay bằng code thực tế khi có ZKTeco SDK
        // Ví dụ: 
        // 1. Connect to device:  zkDevice.Connect(device. Host, device.Port)
        // 2. Read logs since cursor:  zkDevice.GetAttendanceLogs(lastId)
        // 3. Transform to AttendanceEvent format
        // 4. Return list

        await Task.Delay(100); // Simulate SDK call

        // Fake data để test
        if (cursor == null || !cursor.ContainsKey("last_id"))
        {
            return new List<AttendanceEvent>
            {
                new()
                {
                    DeviceUserId = "12345",
                    EventTimeLocal = DateTime.Now. ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    EventTimeUtc = DateTime. UtcNow.ToString("yyyy-MM-ddTHH: mm:ssZ"),
                    Method = "CARD",
                    Direction = "IN",
                    DeviceEventId = $"EVT-{DateTime.Now. Ticks}"
                }
            };
        }

        return new List<AttendanceEvent>(); // No new logs
    }
}