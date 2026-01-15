using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AttendanceAgent.Core.Configuration;
using System.Text.Json;

namespace AttendanceAgent.Core.Services.Storage;

public interface ILocalStore
{
    Task InitializeAsync();
    Task<Dictionary<string, object>?> GetCursorAsync(int deviceId);
    Task SaveCursorAsync(int deviceId, Dictionary<string, object> cursor);
}

public class LocalStore : ILocalStore
{
    private readonly string _dbPath;
    private readonly ILogger<LocalStore> _logger;
    private readonly string _connectionString;

    public LocalStore(IOptions<AgentConfiguration> config, ILogger<LocalStore> logger)
    {
        _dbPath = config.Value.LocalStore.DatabasePath;
        _logger = logger;

        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={_dbPath}";
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS device_cursors (
                device_id INTEGER PRIMARY KEY,
                cursor_json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS pending_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id INTEGER NOT NULL,
                event_json TEXT NOT NULL,
                cursor_json TEXT,
                created_at TEXT NOT NULL,
                synced INTEGER DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_pending_synced ON pending_events(synced, device_id);
        ";
        await command.ExecuteNonQueryAsync();

        _logger.LogInformation("Local store initialized at {DbPath}", _dbPath);
    }

    public async Task<Dictionary<string, object>?> GetCursorAsync(int deviceId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT cursor_json FROM device_cursors WHERE device_id = @deviceId";
        command.Parameters.AddWithValue("@deviceId", deviceId);

        var result = await command.ExecuteScalarAsync();
        if (result == null)
            return null;

        return JsonSerializer.Deserialize<Dictionary<string, object>>(result.ToString()!);
    }

    public async Task SaveCursorAsync(int deviceId, Dictionary<string, object> cursor)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO device_cursors (device_id, cursor_json, updated_at)
            VALUES (@deviceId, @cursorJson, @updatedAt)
            ON CONFLICT(device_id) DO UPDATE SET
                cursor_json = @cursorJson,
                updated_at = @updatedAt
        ";
        command.Parameters.AddWithValue("@deviceId", deviceId);
        command.Parameters.AddWithValue("@cursorJson", JsonSerializer.Serialize(cursor));
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
        _logger.LogDebug("Cursor saved for device {DeviceId}", deviceId);
    }
}