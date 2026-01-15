using AttendanceAgent.Core.Configuration;
using AttendanceAgent.Core.Models;
using AttendanceAgent.Core.Services.Api;
using AttendanceAgent.Core.Services.Devices;
using AttendanceAgent.Core.Services.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AttendanceAgent.Core.Services;

public interface IAgentOrchestrator
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task RunCycleAsync(CancellationToken cancellationToken = default);
    Task SendHeartbeatAsync(CancellationToken cancellationToken = default);
}

public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IApiService _apiService;
    private readonly IDeviceService _deviceService;
    private readonly ILocalStore _localStore;
    private readonly AgentConfiguration _config;
    private readonly ILogger<AgentOrchestrator> _logger;

    private int _totalPulled;
    private int _totalPushed;
    private int _totalErrors;

    public AgentOrchestrator(
        IApiService apiService,
        IDeviceService deviceService,
        ILocalStore localStore,
        IOptions<AgentConfiguration> config,
        ILogger<AgentOrchestrator> logger)
    {
        _apiService = apiService;
        _deviceService = deviceService;
        _localStore = localStore;
        _config = config.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing Agent Orchestrator...");

        await _localStore.InitializeAsync();

        if (_config.Agent.AgentId == null || string.IsNullOrEmpty(_config.Agent.ApiKey))
        {
            _logger.LogInformation("Agent not registered, registering now...");
            var registration = await _apiService.RegisterAgentAsync(cancellationToken);

            _config.Agent.AgentId = registration.AgentId;
            _config.Agent.ApiKey = registration.ApiKey;

            _logger.LogWarning("IMPORTANT: Save these credentials to appsettings.json:");
            _logger.LogWarning("AgentId: {AgentId}", registration.AgentId);
            _logger.LogWarning("ApiKey: {ApiKey}", registration.ApiKey);
        }

        _logger.LogInformation("Agent initialized:  ID={AgentId}", _config.Agent.AgentId);
    }

    public async Task RunCycleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting agent cycle...");

            var devices = await _apiService.GetDevicesAsync(cancellationToken);
            _logger.LogInformation("Found {Count} devices", devices.Count);

            foreach (var device in devices.Where(d => d.IsActive))
            {
                await ProcessDeviceAsync(device, cancellationToken);
            }

            _logger.LogInformation("Cycle completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during agent cycle");
            _totalErrors++;
        }
    }

    private async Task ProcessDeviceAsync(Models.Device device, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing device {DeviceId} ({Host}:{Port})",
                device.Id, device.Host, device.Port);

            var cursor = device.LastCursorJson ?? await _localStore.GetCursorAsync(device.Id);

            var events = await _deviceService.ReadLogsAsync(device, cursor);
            _totalPulled += events.Count;

            if (!events.Any())
            {
                _logger.LogDebug("No new events for device {DeviceId}", device.Id);
                return;
            }

            _logger.LogInformation("Read {Count} new events from device {DeviceId}",
                events.Count, device.Id);

            var ingestRequest = new IngestBatchRequest
            {
                DeviceId = device.Id.ToString(),
                Events = events,
                Cursor = new Dictionary<string, object>
                {
                    { "last_id", events.Count }
                }
            };

            var ingestResult = await _apiService.IngestBatchAsync(ingestRequest, cancellationToken);
            _totalPushed += ingestResult.Processed;

            if (ingestResult.AcceptedCursor != null)
            {
                await _apiService.UpdateCursorAsync(device.Id, ingestResult.AcceptedCursor, cancellationToken);
                await _localStore.SaveCursorAsync(device.Id, ingestResult.AcceptedCursor);
            }

            _logger.LogInformation("Device {DeviceId} processed:  {Processed} events pushed",
                device.Id, ingestResult.Processed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing device {DeviceId}", device.Id);
            _totalErrors++;
        }
    }

    public async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var heartbeat = new HeartbeatRequest
            {
                Version = _config.Agent.Version,
                Stats = new AgentStats
                {
                    Pulled = _totalPulled,
                    Pushed = _totalPushed,
                    Errors = _totalErrors
                },
                DriftMinutes = 0
            };

            await _apiService.SendHeartbeatAsync(heartbeat, cancellationToken);
            _logger.LogInformation("Heartbeat sent:  Pulled={Pulled}, Pushed={Pushed}, Errors={Errors}",
                _totalPulled, _totalPushed, _totalErrors);

            _totalPulled = 0;
            _totalPushed = 0;
            _totalErrors = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending heartbeat");
        }
    }
}