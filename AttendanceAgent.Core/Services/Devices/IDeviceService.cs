using AttendanceAgent.Core.Models;

namespace AttendanceAgent.Core.Services.Devices;

// Khai báo các phương thức cần được cài đặt trong mọi DeviceService
public interface IDeviceService
{
    // Kết nối tới thiết bị
    Task<bool> ConnectAsync(Device device);

    // Đọc logs từ thiết bị
    Task<List<AttendanceEvent>> ReadLogsAsync(Device device, Dictionary<string, object>? cursor);
}