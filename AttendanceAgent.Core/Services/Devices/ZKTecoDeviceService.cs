using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using AttendanceAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AttendanceAgent.Core.Services.Devices
{
    // Optional interface to expose time sync capabilities
    public interface IDeviceTimeSync
    {
        Task<DateTime?> GetDeviceLocalTimeAsync(Device device);
        Task<bool> SyncDeviceTimeAsync(Device device, DateTime? targetTime = null);
    }

    public class ZKTecoDeviceService : IDeviceService, IDeviceTimeSync
    {
        private readonly ILogger<ZKTecoDeviceService> _logger;
        private zkemkeeper.CZKEMClass? _zk;
        private bool _connected;
        private const int MachineNumber = 1;

        public ZKTecoDeviceService(ILogger<ZKTecoDeviceService> logger)
        {
            _logger = logger;
        }

        private void EnsureComLoaded()
        {
            if (_zk != null) return;
            _zk = ZKTecoInterop.Create();
        }

        public Task<bool> ConnectAsync(Device device)
        {
            try
            {
                EnsureComLoaded();
                if (_connected) return Task.FromResult(true);

                // If the device needs CommKey, set it via: _zk!.SetCommPassword(device.CommKey);
                var ok = _zk!.Connect_Net(device.Host, device.Port);
                if (!ok)
                {
                    int err = 0; _zk.GetLastError(ref err);
                    _logger.LogError("ZKTeco connect failed {Host}:{Port} - Error={Error}", device.Host, device.Port, err);
                    return Task.FromResult(false);
                }
                _connected = true;
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConnectAsync error device {DeviceId}", device.Id);
                return Task.FromResult(false);
            }
        }

        public async Task<DateTime?> GetDeviceLocalTimeAsync(Device device)
        {
            var ok = await ConnectAsync(device);
            if (!ok) return null;

            int y = 0, m = 0, d = 0, hh = 0, mm = 0, ss = 0;
            if (_zk!.GetDeviceTime(MachineNumber, ref y, ref m, ref d, ref hh, ref mm, ref ss))
            {
                try
                {
                    return new DateTime(y, m, d, hh, mm, ss, DateTimeKind.Local);
                }
                catch
                {
                    return null;
                }
            }
            int err = 0; _zk.GetLastError(ref err);
            _logger.LogWarning("GetDeviceTime failed Error={Error}", err);
            return null;
        }

        public async Task<bool> SyncDeviceTimeAsync(Device device, DateTime? targetTime = null)
        {
            var ok = await ConnectAsync(device);
            if (!ok) return false;

            try
            {
                var now = targetTime ?? DateTime.Now;
                var success = _zk!.SetDeviceTime2(MachineNumber, now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
                if (!success)
                {
                    int err = 0; _zk.GetLastError(ref err);
                    _logger.LogWarning("SetDeviceTime2 failed Error={Error}", err);
                    return false;
                }
                _zk.RefreshData(MachineNumber);
                _logger.LogInformation("Device {DeviceId} time synced to {Time}.", device.Id, now);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncDeviceTimeAsync error device {DeviceId}", device.Id);
                return false;
            }
        }

        public async Task<List<AttendanceEvent>> ReadLogsAsync(Device device, Dictionary<string, object>? cursor)
        {
            var logs = new List<AttendanceEvent>();

            try
            {
                var connected = await ConnectAsync(device);
                if (!connected) return logs;

                // toTime from device clock
                string toTime = GetDeviceTimeString();
                // fromTime from cursor if present
                string? fromTime = TryGetFromCursor(cursor);

                _zk!.EnableDevice(MachineNumber, false);

                bool haveData;
                if (!string.IsNullOrWhiteSpace(fromTime))
                {
                    // Prefer ranged read; fallback to full read if unsupported
                    haveData = _zk.ReadTimeGLogData(MachineNumber, fromTime!, toTime);
                    if (!haveData)
                    {
                        int err = 0; _zk.GetLastError(ref err);
                        _logger.LogWarning("ReadTimeGLogData failed ({From} -> {To}) Error={Error}. Fallback to ReadGeneralLogData.", fromTime, toTime, err);
                        haveData = _zk.ReadGeneralLogData(MachineNumber);
                    }
                }
                else
                {
                    haveData = _zk.ReadGeneralLogData(MachineNumber);
                }

                if (haveData)
                {
                    string enrollNumber = "";
                    int verifyMode = 0;
                    int inOutMode = 0;
                    int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
                    int workcode = 0;

                    while (_zk.SSR_GetGeneralLogData(
                        MachineNumber,
                        out enrollNumber,
                        out verifyMode,
                        out inOutMode,
                        out year,
                        out month,
                        out day,
                        out hour,
                        out minute,
                        out second,
                        ref workcode))
                    {
                        var localTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local);
                        var utc = localTime.ToUniversalTime();

                        logs.Add(new AttendanceEvent
                        {
                            DeviceUserId = enrollNumber,
                            EventTimeLocal = localTime.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                            EventTimeUtc = utc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            Method = MapMethod(verifyMode),
                            Direction = MapDirection(inOutMode),
                            DeviceEventId = $"ZK-{device.Id}-{year}{month:00}{day:00}{hour:00}{minute:00}{second:00}",
                            Meta = new Dictionary<string, object>
                            {
                                ["device_brand"] = device.Brand ?? "ZKTeco",
                                ["workcode"] = workcode,
                                ["verify_mode"] = verifyMode,
                                ["inout_mode"] = inOutMode
                            }
                        });
                    }
                }
                else
                {
                    int err = 0; _zk.GetLastError(ref err);
                    _logger.LogInformation("No logs returned. Error={Error} (0 means no data).", err);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReadLogsAsync error device {DeviceId}", device.Id);
            }
            finally
            {
                _zk?.EnableDevice(MachineNumber, true);
            }

            return logs;
        }

        private string GetDeviceTimeString()
        {
            int y = 0, m = 0, d = 0, hh = 0, mm = 0, ss = 0;
            if (_zk!.GetDeviceTime(MachineNumber, ref y, ref m, ref d, ref hh, ref mm, ref ss))
            {
                // SDK expects: yyyy-MM-dd HH:mm:ss
                return $"{y:D4}-{m:D2}-{d:D2} {hh:D2}:{mm:D2}:{ss:D2}";
            }
            var now = DateTime.Now;
            return now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static string? TryGetFromCursor(Dictionary<string, object>? cursor)
        {
            if (cursor == null) return null;
            // Cursor: { "last_device_time": "yyyy-MM-dd HH:mm:ss" }
            if (cursor.TryGetValue("last_device_time", out var val) && val is string s && !string.IsNullOrWhiteSpace(s))
            {
                // Bump one second to avoid re-reading the last record
                if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    return dt.AddSeconds(1).ToString("yyyy-MM-dd HH:mm:ss");
                }
                return s;
            }
            return null;
        }

        private static string MapMethod(int verifyMode) => verifyMode switch
        {
            0 => "PASSWORD",
            1 => "FINGERPRINT",
            2 => "CARD",
            15 => "FACE",
            _ => "OTHER"
        };

        private static string MapDirection(int inOutMode) => inOutMode switch
        {
            0 => "IN",
            1 => "OUT",
            _ => "UNKNOWN"
        };
    }
}