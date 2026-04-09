using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Zirve.Sdk.Config;
using Zirve.Sdk.Db;
using Zirve.Sdk.Cache;
using Zirve.Sdk.Auth;
using Zirve.Sdk.Secrets;
using Zirve.Sdk.Queue;
using Zirve.Sdk.Storage;
using Zirve.Sdk.Search;
using Zirve.Sdk.Analytics;
using Zirve.Sdk.Log;
using Zirve.Sdk.Error;
using Zirve.Sdk.Trace;
using Zirve.Sdk.Metrics;
using Zirve.Sdk.Billing;
using Zirve.Sdk.Crm;
using Zirve.Sdk.Remote;
using Zirve.Sdk.Gateway;
using Zirve.Sdk.Ingress;
using Zirve.Sdk.Registry;
using Zirve.Sdk.Deploy;
using Zirve.Sdk.Cluster;
using Zirve.Sdk.Quality;
using Zirve.Sdk.OnCall;
using Zirve.Sdk.Dashboard;
using Zirve.Sdk.Testing;

namespace Zirve.Sdk.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Zirve SDK and all its modules into the IServiceCollection.
    /// Manages appropriate lifecycles and HttpClient instances gracefully.
    /// </summary>
    public static IServiceCollection AddZirve(this IServiceCollection services)
    {
        // Add Core Configurations (Singleton)
        services.AddSingleton<ConfigManager>();
        
        // Add Connection Managers (Singleton)
        services.AddSingleton<DbManager>();
        services.AddSingleton<CacheManager>();
        services.AddSingleton<StorageManager>();
        
        // Background pushers (Singleton)
        services.AddHttpClient<LogManager>();
        
        // Scoped/Transient API Integrations
        services.AddHttpClient<AuthManager>();
        services.AddHttpClient<SecretsManager>();
        services.AddHttpClient<QueueManager>();
        services.AddHttpClient<SearchManager>();
        services.AddHttpClient<AnalyticsManager>();
        services.AddHttpClient<ErrorManager>();
        services.AddHttpClient<TraceManager>();
        services.AddHttpClient<MetricsManager>();
        services.AddHttpClient<BillingManager>();
        services.AddHttpClient<CrmManager>();
        services.AddHttpClient<RemoteManager>();
        services.AddHttpClient<GatewayManager>();
        services.AddHttpClient<IngressManager>();
        services.AddHttpClient<RegistryManager>();
        services.AddHttpClient<DeployManager>();
        services.AddHttpClient<ClusterManager>();
        services.AddHttpClient<QualityManager>();
        services.AddHttpClient<OnCallManager>();
        services.AddHttpClient<DashboardManager>();
        services.AddHttpClient<TestingManager>();

        // Register main Facade
        services.AddScoped<ZirveClient>();

        return services;
    }
}
