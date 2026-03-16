using System;
using System.Threading.Tasks;
using AttendanceAgent.Core;
using AttendanceAgent.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

class Program
{
    static async Task Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                   .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                   .AddEnvironmentVariables(prefix: "ATT_");
            })
            .ConfigureServices((ctx, services) =>
            {
                // Đăng ký toàn bộ core services qua extension method trong AttendanceAgent.Core
                services.AddAttendanceAgentCore(ctx.Configuration);

                // Logging mức thông tin
                services.AddLogging(b =>
                {
                    b.AddConsole();
                    b.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var orchestrator = host.Services.GetRequiredService<IAgentOrchestrator>();

        try
        {
            await orchestrator.InitializeAsync();
            await orchestrator.RunCycleAsync();
            await orchestrator.SendHeartbeatAsync();

            logger.LogInformation("Console run completed. Nhấn phím bất kỳ để thoát...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi nghiêm trọng trong console runner");
        }
    }
}