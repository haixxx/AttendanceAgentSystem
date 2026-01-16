using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AttendanceAgent.Core.Configuration;
using AttendanceAgent.Core.Models;
using AttendanceAgent.Core.Services.Api;
using AttendanceAgent.Core.Services.Devices;
using AttendanceAgent.Core.Storage;
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
    private double _maxDriftMinutesThisCycle;

    // Server default is 1000; keep aligned to avoid 413 batch too large
    private const int MaxBatchSize = 1000;
    private const int TimeSyncDriftThresholdSeconds = 120; // 2 phút

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

        _logger.LogInformation("Agent initialized: ID={AgentId}", _config.Agent.AgentId);
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

    private async Task ProcessDeviceAsync(Device device, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing device {DeviceId} - {Brand} ({Host}:{Port})",
                device.Id, device.Brand, device.Host, device.Port);

            // 1) Đồng bộ thời gian nếu lệch lớn
            if (_deviceService is IDeviceTimeSync timeSync)
            {
                var devTime = await timeSync.GetDeviceLocalTimeAsync(device);
                if (devTime != null)
                {
                    var driftSeconds = Math.Abs((devTime.Value - DateTime.Now).TotalSeconds);
                    var driftMinutes = Math.Round(driftSeconds / 60.0, 2);
                    _maxDriftMinutesThisCycle = Math.Max(_maxDriftMinutesThisCycle, driftMinutes);

                    if (driftSeconds > TimeSyncDriftThresholdSeconds)
                    {
                        _logger.LogWarning("Device {DeviceId} clock drift ~{DriftSec}s. Syncing to agent time...", device.Id, (int)driftSeconds);
                        var synced = await timeSync.SyncDeviceTimeAsync(device);
                        if (!synced)
                        {
                            _logger.LogWarning("Time sync failed for device {DeviceId}. Continuing without sync.", device.Id);
                        }
                    }
                }
            }

            // 2) Prefer server-provided cursor, fallback to local store
            var cursor = device.LastCursorJson ?? await _localStore.GetCursorAsync(device.Id);

            // 3) Read logs incrementally based on cursor (service will fallback if device doesn't support time range)
            var events = await _deviceService.ReadLogsAsync(device, cursor);
            _totalPulled += events.Count;

            if (!events.Any())
            {
                _logger.LogDebug("No new events for device {DeviceId}", device.Id);
                return;
            }

            _logger.LogInformation("Read {Count} new events from device {DeviceId}",
                events.Count, device.Id);

            // 4) Chunk to respect server batch limit and ingest
            int offset = 0;
            while (offset < events.Count)
            {
                var chunk = events.Skip(offset).Take(MaxBatchSize).ToList();

                // Compute next cursor from chunk (last_device_time + last_id)
                var nextCursor = BuildCursorFromEvents(cursor, chunk);

                var ingestRequest = new IngestBatchRequest
                {
                    DeviceId = device.Id.ToString(),
                    BatchId = Guid.NewGuid().ToString("N"),
                    Cursor = nextCursor,
                    Events = chunk
                };

                var ingestResult = await _apiService.IngestBatchAsync(ingestRequest, cancellationToken);

                if (ingestResult.Ok)
                {
                    _totalPushed += ingestResult.Processed;

                    // Prefer server-accepted cursor; fallback to our computed nextCursor
                    var accepted = ingestResult.AcceptedCursor ?? nextCursor;

                    // Update server cursor and local store
                    await _apiService.UpdateCursorAsync(device.Id, accepted, cancellationToken);
                    await _localStore.SaveCursorAsync(device.Id, accepted);

                    _logger.LogInformation("Device {DeviceId} chunk processed: pushed={Processed}, duplicates={Duplicates}, rejected={Rejected}",
                        device.Id, ingestResult.Processed, ingestResult.Duplicates, ingestResult.Rejected);
                }
                else
                {
                    _logger.LogWarning("Ingest failed for device {DeviceId} on chunk starting at offset {Offset}", device.Id, offset);
                    _totalErrors++;
                    // Stop further chunks on failure to avoid cascading errors
                    break;
                }

                offset += chunk.Count;
            }
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
                DriftMinutes = (int)Math.Round(_maxDriftMinutesThisCycle)
            };

            await _apiService.SendHeartbeatAsync(heartbeat, cancellationToken);
            _logger.LogInformation("Heartbeat sent: Pulled={Pulled}, Pushed={Pushed}, Errors={Errors}, DriftMinutes={Drift}",
                _totalPulled, _totalPushed, _totalErrors, heartbeat.DriftMinutes);

            _totalPulled = 0;
            _totalPushed = 0;
            _totalErrors = 0;
            _maxDriftMinutesThisCycle = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending heartbeat");
        }
    }

    /// <summary>
    /// Build next cursor from events. Adds/updates:
    /// - last_id: number of events in this chunk
    /// - last_device_time: max EventTimeLocal in "yyyy-MM-dd HH:mm:ss" (device local time without offset)
    /// Preserves existing cursor entries.
    /// </summary>
    private static Dictionary<string, object> BuildCursorFromEvents(
        Dictionary<string, object>? currentCursor,
        List<AttendanceEvent> events)
    {
        var next = currentCursor != null
            ? new Dictionary<string, object>(currentCursor)
            : new Dictionary<string, object>();

        next["last_id"] = events.Count;

        // Compute max local device time from EventTimeLocal (ISO 8601 with offset)
        DateTime maxLocal = DateTime.MinValue;
        foreach (var ev in events)
        {
            if (string.IsNullOrWhiteSpace(ev.EventTimeLocal)) continue;

            // Parse EventTimeLocal as DateTimeOffset to respect offset, then take LocalDateTime
            if (DateTimeOffset.TryParse(ev.EventTimeLocal, CultureInfo.InvariantCulture, out var dto))
            {
                var local = dto.LocalDateTime;
                if (local > maxLocal) maxLocal = local;
            }
            else
            {
                // Fallback parse without offset if format deviates
                if (DateTime.TryParse(ev.EventTimeLocal, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                {
                    if (dt > maxLocal) maxLocal = dt;
                }
            }
        }

        if (maxLocal != DateTime.MinValue)
        {
            // Device-time string format expected by SDK ReadTimeGLogData
            next["last_device_time"] = maxLocal.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return next;
    }
}