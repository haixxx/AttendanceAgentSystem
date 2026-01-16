using System;
using System.Threading.Tasks;
using AttendanceAgent.Core.Models;
using AttendanceAgent.Core.Services.Devices;
using Microsoft.Extensions.Logging;

class Program
{
    static async Task Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole(); // Requires Microsoft.Extensions.Logging.Console package
        });
        var logger = loggerFactory.CreateLogger<ZKTecoDeviceService>();

        var svc = new ZKTecoDeviceService(logger);

        var device = new Device
        {
            Id = 2,
            Host = "192.168.1.13",
            Port = 4370,
            Brand = "ZKTeco"
        };

        // Optional: check drift and sync time before reading logs
        if (svc is IDeviceTimeSync timeSync)
        {
            var devTime = await timeSync.GetDeviceLocalTimeAsync(device);
            if (devTime != null)
            {
                var driftSeconds = Math.Abs((devTime.Value - DateTime.Now).TotalSeconds);
                if (driftSeconds > 120)
                {
                    Console.WriteLine($"Clock drift ~{(int)driftSeconds}s. Syncing device time...");
                    var synced = await timeSync.SyncDeviceTimeAsync(device);
                    Console.WriteLine($"Time sync: {(synced ? "OK" : "FAILED")}");
                }
            }
        }

        Console.WriteLine("Reading logs...");
        var logs = await svc.ReadLogsAsync(device, null);

        Console.WriteLine($"Read {logs.Count} logs");
        foreach (var e in logs)
        {
            Console.WriteLine($"{e.DeviceUserId} | {e.EventTimeLocal} | {e.Method}/{e.Direction}");
        }
    }
}