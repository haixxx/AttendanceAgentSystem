namespace AttendanceAgent.Core.Configuration;

public class AgentConfiguration
{
    public string ServerUrl { get; set; } = string.Empty;
    public AgentSettings Agent { get; set; } = new();
    public LocalStoreSettings LocalStore { get; set; } = new();
}

public class AgentSettings
{
    public int? AgentId { get; set; }
    public string? ApiKey { get; set; }
    public string SecretKey { get; set; } = string.Empty;
    public string Name { get; set; } = Environment.MachineName;
    public string Version { get; set; } = "1.0.0";
    public int PollIntervalSeconds { get; set; } = 60;
    public int HeartbeatIntervalSeconds { get; set; } = 300;
}

public class LocalStoreSettings
{
    public string DatabasePath { get; set; } = "Data/agent.db";
}