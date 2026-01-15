using AttendanceAgent.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AttendanceAgent.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IAgentOrchestrator _orchestrator;
    private readonly int _pollIntervalSeconds;

    public Worker(
        ILogger<Worker> logger,
        IAgentOrchestrator orchestrator,
        Microsoft.Extensions.Options.IOptions<Core.Configuration.AgentConfiguration> config)
    {
        _logger = logger;
        _orchestrator = orchestrator;
        _pollIntervalSeconds = config.Value.Agent.PollIntervalSeconds;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Attendance Agent Service starting...");

        try
        {
            // Initialize agent (register if needed)
            await _orchestrator.InitializeAsync(stoppingToken);

            var heartbeatCounter = 0;
            var heartbeatInterval = 5; // Send heartbeat every 5 cycles

            while (!stoppingToken.IsCancellationRequested)
            {
                // Run main cycle
                await _orchestrator.RunCycleAsync(stoppingToken);

                // Send heartbeat periodically
                heartbeatCounter++;
                if (heartbeatCounter >= heartbeatInterval)
                {
                    await _orchestrator.SendHeartbeatAsync(stoppingToken);
                    heartbeatCounter = 0;
                }

                // Wait for next cycle
                await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Service is stopping gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in service");
            throw;
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attendance Agent Service stopping...");
        return base.StopAsync(cancellationToken);
    }
}