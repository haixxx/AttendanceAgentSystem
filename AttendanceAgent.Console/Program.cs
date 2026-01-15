using AttendanceAgent.Core;
using AttendanceAgent.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

// Setup
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddLogging(builder => builder.AddSerilog());
services.AddAttendanceAgentCore(configuration);

var serviceProvider = services.BuildServiceProvider();

// Run test
Console.WriteLine("=== Attendance Agent - Test Console ===\n");

try
{
    var orchestrator = serviceProvider.GetRequiredService<IAgentOrchestrator>();

    Console.WriteLine("1. Initializing agent...");
    await orchestrator.InitializeAsync();

    Console.WriteLine("\n2. Running one cycle...");
    await orchestrator.RunCycleAsync();

    Console.WriteLine("\n3. Sending heartbeat...");
    await orchestrator.SendHeartbeatAsync();

    Console.WriteLine("\n✅ Test completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();