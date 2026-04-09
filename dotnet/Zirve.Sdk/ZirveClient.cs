using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
using Microsoft.Extensions.DependencyInjection;

namespace Zirve.Sdk;

/// <summary>
/// Main Zirve SDK Client encapsulating all 26 Infrastructure services.
/// Designed for Dependency Injection usage (.NET Core paradigms).
/// </summary>
public class ZirveClient : IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;

    public ZirveClient(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public ConfigManager Config => _serviceProvider.GetRequiredService<ConfigManager>();
    public DbManager Db => _serviceProvider.GetRequiredService<DbManager>();
    public CacheManager Cache => _serviceProvider.GetRequiredService<CacheManager>();
    public AuthManager Auth => _serviceProvider.GetRequiredService<AuthManager>();
    public SecretsManager Secrets => _serviceProvider.GetRequiredService<SecretsManager>();
    public QueueManager Queue => _serviceProvider.GetRequiredService<QueueManager>();
    public StorageManager Storage => _serviceProvider.GetRequiredService<StorageManager>();
    public SearchManager Search => _serviceProvider.GetRequiredService<SearchManager>();
    public AnalyticsManager Analytics => _serviceProvider.GetRequiredService<AnalyticsManager>();
    
    public LogManager Log => _serviceProvider.GetRequiredService<LogManager>();
    public ErrorManager Error => _serviceProvider.GetRequiredService<ErrorManager>();
    public TraceManager Trace => _serviceProvider.GetRequiredService<TraceManager>();
    public MetricsManager Metrics => _serviceProvider.GetRequiredService<MetricsManager>();
    
    public BillingManager Billing => _serviceProvider.GetRequiredService<BillingManager>();
    public CrmManager Crm => _serviceProvider.GetRequiredService<CrmManager>();
    public RemoteManager Remote => _serviceProvider.GetRequiredService<RemoteManager>();
    
    public GatewayManager Gateway => _serviceProvider.GetRequiredService<GatewayManager>();
    public IngressManager Ingress => _serviceProvider.GetRequiredService<IngressManager>();
    public RegistryManager Registry => _serviceProvider.GetRequiredService<RegistryManager>();
    public DeployManager Deploy => _serviceProvider.GetRequiredService<DeployManager>();
    public ClusterManager Cluster => _serviceProvider.GetRequiredService<ClusterManager>();
    public QualityManager Quality => _serviceProvider.GetRequiredService<QualityManager>();
    public OnCallManager OnCall => _serviceProvider.GetRequiredService<OnCallManager>();
    public DashboardManager Dashboard => _serviceProvider.GetRequiredService<DashboardManager>();
    public TestingManager Testing => _serviceProvider.GetRequiredService<TestingManager>();

    /// <summary>
    /// Checks the health of all SDK connected components by executing their respective HTTP/Ping checks.
    /// </summary>
    public async Task<Dictionary<string, string>> HealthAsync()
    {
        var status = new Dictionary<string, string>();

        status["db"] = await Db.HealthAsync() ? "healthy" : "unhealthy";
        status["cache"] = await Cache.HealthAsync() ? "healthy" : "unhealthy";
        status["auth"] = await Auth.HealthAsync() ? "healthy" : "unhealthy";
        status["secrets"] = await Secrets.HealthAsync() ? "healthy" : "unhealthy";
        status["queue"] = await Queue.HealthAsync() ? "healthy" : "unhealthy";
        status["storage"] = await Storage.HealthAsync() ? "healthy" : "unhealthy";
        status["search"] = await Search.HealthAsync() ? "healthy" : "unhealthy";
        status["analytics"] = await Analytics.HealthAsync() ? "healthy" : "unhealthy";
        status["log"] = await Log.HealthAsync() ? "healthy" : "unhealthy";
        status["error"] = await Error.HealthAsync() ? "healthy" : "unhealthy";
        status["trace"] = await Trace.HealthAsync() ? "healthy" : "unhealthy";
        status["metrics"] = await Metrics.HealthAsync() ? "healthy" : "unhealthy";
        status["billing"] = await Billing.HealthAsync() ? "healthy" : "unhealthy";
        status["crm"] = await Crm.HealthAsync() ? "healthy" : "unhealthy";
        status["remote"] = await Remote.HealthAsync() ? "healthy" : "unhealthy";
        status["gateway"] = await Gateway.HealthAsync() ? "healthy" : "unhealthy";
        status["ingress"] = await Ingress.HealthAsync() ? "healthy" : "unhealthy";
        status["registry"] = await Registry.HealthAsync() ? "healthy" : "unhealthy";
        status["deploy"] = await Deploy.HealthAsync() ? "healthy" : "unhealthy";
        status["cluster"] = await Cluster.HealthAsync() ? "healthy" : "unhealthy";
        status["quality"] = await Quality.HealthAsync() ? "healthy" : "unhealthy";
        status["oncall"] = await OnCall.HealthAsync() ? "healthy" : "unhealthy";
        status["dashboard"] = await Dashboard.HealthAsync() ? "healthy" : "unhealthy";
        status["testing"] = await Testing.HealthAsync() ? "healthy" : "unhealthy";

        return status;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await Cache.DisposeAsync();
        Storage.Dispose();
        await Log.DisposeAsync();
    }
}
