using AttendanceAgent.Core;
using AttendanceAgent.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        path: @"C:\ProgramData\AttendanceAgent\Logs\service-. txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("Starting Attendance Agent Service");

    var builder = Host.CreateApplicationBuilder(args);

    // Load configuration
    builder.Configuration
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings. json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings. {builder.Environment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables();

    // Use Serilog
    builder.Services.AddSerilog();

    // Add Windows Service support
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "AttendanceAgent";
    });

    // Add Core services
    builder.Services.AddAttendanceAgentCore(builder.Configuration);

    // Add Worker
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}