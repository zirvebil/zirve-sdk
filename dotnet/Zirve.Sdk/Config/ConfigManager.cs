using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Zirve.Sdk.Config;

/// <summary>
/// Zirve Config Manager — Service Discovery & Configuration.
/// Provides configuration values via environment variables overriding default Kubernetes DNS names.
/// Integrated with Microsoft.Extensions.Configuration.
/// </summary>
public class ConfigManager
{
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, string?> _cache = new();

    // Default Kubernetes service endpoints
    private static readonly Dictionary<string, string> Defaults = new()
    {
        { "db.host", "postgresql.zirve-infra.svc.cluster.local" },
        { "db.port", "5432" },
        { "db.dbname", "zirve" },
        { "db.user", "postgres" },
        { "cache.host", "redis-master.zirve-infra.svc.cluster.local" },
        { "cache.port", "6379" },
        { "auth.url", "http://keycloak.zirve-infra.svc.cluster.local" },
        { "secrets.url", "http://infisical.zirve-infra.svc.cluster.local" },
        { "queue.host", "rabbitmq.zirve-infra.svc.cluster.local" },
        { "queue.port", "5672" },
        { "queue.api_port", "15672" },
        { "storage.endpoint", "http://minio.zirve-infra.svc.cluster.local:9000" },
        { "storage.imgproxy", "http://imgproxy.zirve-infra.svc.cluster.local" },
        { "search.host", "elasticsearch-master.zirve-infra.svc.cluster.local" },
        { "search.port", "9200" },
        { "analytics.host", "clickhouse.zirve-infra.svc.cluster.local" },
        { "analytics.port", "8123" },
        { "log.url", "http://loki.zirve-infra.svc.cluster.local:3100" },
        { "error.url", "http://sentry.zirve-infra.svc.cluster.local:9000" },
        { "trace.endpoint", "http://opentelemetry-collector.zirve-infra.svc.cluster.local:4318" },
        { "metrics.url", "http://prometheus-server.zirve-infra.svc.cluster.local:9090" },
        { "billing.url", "http://lago-api.zirve-infra.svc.cluster.local" },
        { "crm.url", "http://odoo.zirve-infra.svc.cluster.local:8069" },
        { "remote.url", "http://guacamole.zirve-infra.svc.cluster.local:8080" },
        { "gateway.url", "http://kong-kong-admin.zirve-infra.svc.cluster.local:8001" },
        { "ingress.url", "http://traefik.kube-system.svc.cluster.local:8080" },
        { "registry.url", "http://harbor-core.zirve-infra.svc.cluster.local" },
        { "deploy.url", "http://argocd-server.argocd.svc.cluster.local" },
        { "cluster.url", "http://rancher.cattle-system.svc.cluster.local" },
        { "quality.url", "http://sonarqube-sonarqube.zirve-infra.svc.cluster.local:9000" },
        { "oncall.url", "http://grafana-oncall-engine.zirve-infra.svc.cluster.local:8080" },
        { "dashboard.url", "http://grafana.zirve-infra.svc.cluster.local" },
        { "testing.url", "http://keploy.zirve-infra.svc.cluster.local:6789" }
    };

    // Environment variable mapping
    private static readonly Dictionary<string, string> EnvMap = new()
    {
        { "db.host", "PG_HOST" }, { "db.port", "PG_PORT" }, { "db.dbname", "PG_DBNAME" },
        { "db.user", "PG_USER" }, { "db.password", "PG_PASSWORD" },
        { "cache.host", "REDIS_HOST" }, { "cache.port", "REDIS_PORT" }, { "cache.password", "REDIS_PASSWORD" },
        { "auth.url", "KEYCLOAK_URL" }, { "auth.realm", "KEYCLOAK_REALM" },
        { "auth.client_id", "KEYCLOAK_CLIENT_ID" }, { "auth.client_secret", "KEYCLOAK_CLIENT_SECRET" },
        { "secrets.url", "INFISICAL_URL" }, { "secrets.token", "INFISICAL_TOKEN" }, { "secrets.project_id", "INFISICAL_PROJECT_ID" },
        { "queue.host", "RABBITMQ_HOST" }, { "queue.user", "RABBITMQ_USER" }, { "queue.password", "RABBITMQ_PASSWORD" },
        { "storage.endpoint", "MINIO_ENDPOINT" }, { "storage.key", "MINIO_ACCESS_KEY" },
        { "storage.secret", "MINIO_SECRET_KEY" }, { "storage.imgproxy", "IMGPROXY_URL" },
        { "search.host", "ELASTIC_HOST" }, { "search.username", "ELASTIC_USER" }, { "search.password", "ELASTIC_PASSWORD" },
        { "analytics.host", "CLICKHOUSE_HOST" }, { "analytics.username", "CLICKHOUSE_USER" }, { "analytics.password", "CLICKHOUSE_PASSWORD" },
        { "log.url", "LOKI_URL" },
        { "error.dsn", "SENTRY_DSN" },
        { "trace.endpoint", "OTEL_EXPORTER_OTLP_ENDPOINT" },
        { "metrics.url", "PROMETHEUS_URL" },
        { "billing.url", "LAGO_URL" }, { "billing.api_key", "LAGO_API_KEY" },
        { "crm.url", "ODOO_URL" }, { "crm.username", "ODOO_USER" }, { "crm.password", "ODOO_PASSWORD" }, { "crm.database", "ODOO_DB" },
        { "remote.url", "GUACAMOLE_URL" }, { "remote.username", "GUACAMOLE_USER" }, { "remote.password", "GUACAMOLE_PASSWORD" },
        { "gateway.url", "KONG_ADMIN_URL" },
        { "ingress.url", "TRAEFIK_API_URL" },
        { "registry.url", "HARBOR_URL" }, { "registry.username", "HARBOR_USER" }, { "registry.password", "HARBOR_PASSWORD" },
        { "deploy.url", "ARGOCD_URL" }, { "deploy.token", "ARGOCD_TOKEN" },
        { "cluster.url", "RANCHER_URL" }, { "cluster.token", "RANCHER_TOKEN" },
        { "quality.url", "SONAR_URL" }, { "quality.token", "SONAR_TOKEN" },
        { "oncall.url", "GRAFANA_ONCALL_URL" }, { "oncall.token", "GRAFANA_ONCALL_TOKEN" },
        { "dashboard.url", "GRAFANA_URL" }, { "dashboard.token", "GRAFANA_TOKEN" },
        { "testing.url", "KEPLOY_URL" }
    };

    public ConfigManager(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Checks if a string value exists or resolves from defaults, environments, and IConfiguration source.
    /// Hierarchy: 1. IConfiguration (Overrides/appsettings) -> 2. Process Environment Variables -> 3. Internal Defaults
    /// </summary>
    public string? Get(string key, string? defaultValue = null)
    {
        if (_cache.TryGetValue(key, out var val))
            return val;

        // 1. Check native .NET configuration (can contain appsettings.json or custom overrides)
        var configVal = _configuration[$"Zirve:{key}"];
        if (!string.IsNullOrEmpty(configVal))
        {
            _cache[key] = configVal;
            return configVal;
        }

        // 2. Check mapped process environment variable directly
        if (EnvMap.TryGetValue(key, out var envKey))
        {
            var envVal = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrEmpty(envVal))
            {
                _cache[key] = envVal;
                return envVal;
            }
        }

        // 3. Fallback to K8s internal default service DNS
        if (Defaults.TryGetValue(key, out var defaultVal))
        {
            _cache[key] = defaultVal;
            return defaultVal;
        }

        return defaultValue;
    }

    /// <summary>
    /// Gets a dictionary of configuration mapped to a specific prefix.
    /// Example: Module("db") includes "db.host", "db.port", etc. with keys "host", "port", etc.
    /// </summary>
    public Dictionary<string, string?> Module(string prefix)
    {
        var result = new Dictionary<string, string?>();
        var keysToScan = new HashSet<string>(Defaults.Keys);
        foreach (var k in EnvMap.Keys) keysToScan.Add(k);

        foreach (var key in keysToScan)
        {
            if (key.StartsWith($"{prefix}."))
            {
                var shortKey = key.Substring(prefix.Length + 1);
                result[shortKey] = Get(key);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the base URL for the Dapr sidecar if configured.
    /// </summary>
    public string DaprUrl()
    {
        var port = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3500";
        return $"http://localhost:{port}/v1.0";
    }

    public bool IsDaprEnabled()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DAPR_HTTP_PORT"));
    }
}
