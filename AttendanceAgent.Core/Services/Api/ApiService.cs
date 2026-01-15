using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AttendanceAgent.Core.Configuration;
using AttendanceAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AttendanceAgent.Core.Services.Api;

public interface IApiService
{
    Task<AgentRegistration> RegisterAgentAsync(CancellationToken cancellationToken = default);
    Task SendHeartbeatAsync(HeartbeatRequest request, CancellationToken cancellationToken = default);
    Task<List<Device>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<IngestBatchResponse> IngestBatchAsync(IngestBatchRequest request, CancellationToken cancellationToken = default);
    Task UpdateCursorAsync(int deviceId, Dictionary<string, object> cursor, CancellationToken cancellationToken = default);
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly AgentConfiguration _config;
    private readonly ILogger<ApiService> _logger;

    public ApiService(HttpClient httpClient, IOptions<AgentConfiguration> config, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_config.ServerUrl);

        if (_config.Agent.ApiKey != null)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config.Agent.ApiKey);
        }
    }

    public async Task<AgentRegistration> RegisterAgentAsync(CancellationToken cancellationToken = default)
    {
        var request = new
        {
            name = _config.Agent.Name,
            version = _config.Agent.Version
        };

        var response = await _httpClient.PostAsJsonAsync(
            "/api/attendance/agents/register",
            request,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AgentRegistration>(cancellationToken: cancellationToken);

        _logger.LogInformation("Agent registered successfully:  ID={AgentId}", result!.AgentId);

        return result;
    }

    public async Task SendHeartbeatAsync(HeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/attendance/agents/{_config.Agent.AgentId}/heartbeat",
            request,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();
    }

    public async Task<List<Device>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/api/attendance/agents/{_config.Agent.AgentId}/devices",
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        var devices = await response.Content.ReadFromJsonAsync<List<Device>>(cancellationToken: cancellationToken);

        _logger.LogInformation("Fetched {Count} devices", devices?.Count ?? 0);

        return devices ?? new List<Device>();
    }

    public async Task<IngestBatchResponse> IngestBatchAsync(IngestBatchRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/attendance/ingest/batch/",
            request,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IngestBatchResponse>(cancellationToken: cancellationToken);

        return result!;
    }

    public async Task UpdateCursorAsync(int deviceId, Dictionary<string, object> cursor, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/attendance/devices/{deviceId}/cursor/",
            new { cursor },
            cancellationToken
        );

        response.EnsureSuccessStatusCode();
    }
}

public record AgentRegistration(int AgentId, string ApiKey);
public record HeartbeatRequest(string Version, AgentStats Stats, int DriftMinutes);
public record AgentStats(int Pulled, int Pushed, int Errors);
public record IngestBatchRequest(string DeviceId, List<AttendanceEvent> Events, Dictionary<string, object> Cursor);
public record IngestBatchResponse(int Processed, Dictionary<string, object>? AcceptedCursor);