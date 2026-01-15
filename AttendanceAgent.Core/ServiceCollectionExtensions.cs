using AttendanceAgent.Core.Configuration;
using AttendanceAgent.Core.Infrastructure.Auth;
using AttendanceAgent.Core.Services;
using AttendanceAgent.Core.Services.Api;
using AttendanceAgent.Core.Services.Devices;
using AttendanceAgent.Core.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace AttendanceAgent.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAttendanceAgentCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AgentConfiguration>(configuration);

        services.AddTransient<HmacAuthHandler>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<AgentConfiguration>>().Value;
            return new HmacAuthHandler(config.Agent.Name, config.Agent.SecretKey);
        });

        services.AddHttpClient<IApiService, ApiService>()
            .AddHttpMessageHandler<HmacAuthHandler>()
            .AddPolicyHandler(GetRetryPolicy());

        services.AddSingleton<ILocalStore, LocalStore>();
        services.AddSingleton<IDeviceService, ZKTecoDeviceService>();
        services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    Console.WriteLine($"Retry {retryAttempt} after {timespan.TotalSeconds}s due to:  {outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString()}");
                });
    }
}