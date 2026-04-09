/**
 * Zirve Config Manager — Service Discovery & Configuration.
 * 
 * Provides configuration values via environment variables, with fallback to 
 * Kubernetes service DNS names. Supports Dapr overrides.
 */
export class ConfigManager {
  private overrides: Record<string, any>;
  private cache: Record<string, string> = {};

  // K8s service default endpoints
  private static readonly DEFAULTS: Record<string, string | number> = {
    'db.host': 'postgresql.zirve-infra.svc.cluster.local',
    'db.port': 5432,
    'db.dbname': 'zirve',
    'db.user': 'postgres',
    'cache.host': 'redis-master.zirve-infra.svc.cluster.local',
    'cache.port': 6379,
    'auth.url': 'http://keycloak.zirve-infra.svc.cluster.local',
    'secrets.url': 'http://infisical.zirve-infra.svc.cluster.local',
    'queue.host': 'rabbitmq.zirve-infra.svc.cluster.local',
    'queue.port': 5672,
    'queue.api_port': 15672,
    'storage.endpoint': 'http://minio.zirve-infra.svc.cluster.local:9000',
    'storage.imgproxy': 'http://imgproxy.zirve-infra.svc.cluster.local',
    'search.host': 'elasticsearch-master.zirve-infra.svc.cluster.local',
    'search.port': 9200,
    'analytics.host': 'clickhouse.zirve-infra.svc.cluster.local',
    'analytics.port': 8123,
    'log.url': 'http://loki.zirve-infra.svc.cluster.local:3100',
    'error.url': 'http://sentry.zirve-infra.svc.cluster.local:9000',
    'trace.endpoint': 'http://opentelemetry-collector.zirve-infra.svc.cluster.local:4318',
    'metrics.url': 'http://prometheus-server.zirve-infra.svc.cluster.local:9090',
    'billing.url': 'http://lago-api.zirve-infra.svc.cluster.local',
    'crm.url': 'http://odoo.zirve-infra.svc.cluster.local:8069',
    'remote.url': 'http://guacamole.zirve-infra.svc.cluster.local:8080',
    'gateway.url': 'http://kong-kong-admin.zirve-infra.svc.cluster.local:8001',
    'ingress.url': 'http://traefik.kube-system.svc.cluster.local:8080',
    'registry.url': 'http://harbor-core.zirve-infra.svc.cluster.local',
    'deploy.url': 'http://argocd-server.argocd.svc.cluster.local',
    'cluster.url': 'http://rancher.cattle-system.svc.cluster.local',
    'quality.url': 'http://sonarqube-sonarqube.zirve-infra.svc.cluster.local:9000',
    'oncall.url': 'http://grafana-oncall-engine.zirve-infra.svc.cluster.local:8080',
    'dashboard.url': 'http://grafana.zirve-infra.svc.cluster.local',
    'testing.url': 'http://keploy.zirve-infra.svc.cluster.local:6789',
  };

  // Environment variable mappings
  private static readonly ENV_MAP: Record<string, string> = {
    'db.host': 'PG_HOST', 'db.port': 'PG_PORT', 'db.dbname': 'PG_DBNAME', 'db.user': 'PG_USER', 'db.password': 'PG_PASSWORD',
    'cache.host': 'REDIS_HOST', 'cache.port': 'REDIS_PORT', 'cache.password': 'REDIS_PASSWORD',
    'auth.url': 'KEYCLOAK_URL', 'auth.realm': 'KEYCLOAK_REALM', 'auth.client_id': 'KEYCLOAK_CLIENT_ID', 'auth.client_secret': 'KEYCLOAK_CLIENT_SECRET',
    'secrets.url': 'INFISICAL_URL', 'secrets.token': 'INFISICAL_TOKEN', 'secrets.project_id': 'INFISICAL_PROJECT_ID',
    'queue.host': 'RABBITMQ_HOST', 'queue.user': 'RABBITMQ_USER', 'queue.password': 'RABBITMQ_PASSWORD',
    'storage.endpoint': 'MINIO_ENDPOINT', 'storage.key': 'MINIO_ACCESS_KEY', 'storage.secret': 'MINIO_SECRET_KEY', 'storage.imgproxy': 'IMGPROXY_URL',
    'search.host': 'ELASTIC_HOST', 'search.username': 'ELASTIC_USER', 'search.password': 'ELASTIC_PASSWORD',
    'analytics.host': 'CLICKHOUSE_HOST', 'analytics.username': 'CLICKHOUSE_USER', 'analytics.password': 'CLICKHOUSE_PASSWORD',
    'log.url': 'LOKI_URL',
    'error.dsn': 'SENTRY_DSN',
    'trace.endpoint': 'OTEL_EXPORTER_OTLP_ENDPOINT',
    'metrics.url': 'PROMETHEUS_URL',
    'billing.url': 'LAGO_URL', 'billing.api_key': 'LAGO_API_KEY',
    'crm.url': 'ODOO_URL', 'crm.username': 'ODOO_USER', 'crm.password': 'ODOO_PASSWORD', 'crm.database': 'ODOO_DB',
    'remote.url': 'GUACAMOLE_URL', 'remote.username': 'GUACAMOLE_USER', 'remote.password': 'GUACAMOLE_PASSWORD',
    'gateway.url': 'KONG_ADMIN_URL',
    'ingress.url': 'TRAEFIK_API_URL',
    'registry.url': 'HARBOR_URL', 'registry.username': 'HARBOR_USER', 'registry.password': 'HARBOR_PASSWORD',
    'deploy.url': 'ARGOCD_URL', 'deploy.token': 'ARGOCD_TOKEN',
    'cluster.url': 'RANCHER_URL', 'cluster.token': 'RANCHER_TOKEN',
    'quality.url': 'SONAR_URL', 'quality.token': 'SONAR_TOKEN',
    'oncall.url': 'GRAFANA_ONCALL_URL', 'oncall.token': 'GRAFANA_ONCALL_TOKEN',
    'dashboard.url': 'GRAFANA_URL', 'dashboard.token': 'GRAFANA_TOKEN',
    'testing.url': 'KEPLOY_URL',
  };

  constructor(overrides: Record<string, any> = {}) {
    this.overrides = overrides;
  }

  /**
   * Get a configuration value by key (e.g. 'db.host').
   * Resolution order: 1. Manual Overrides -> 2. Environment Variables -> 3. Defaults.
   */
  public get(key: string, defaultValue: any = null): any {
    if (this.cache[key] !== undefined) {
      return this.cache[key];
    }

    if (this.overrides[key] !== undefined) {
      this.cache[key] = this.overrides[key];
      return this.cache[key];
    }

    const envKey = ConfigManager.ENV_MAP[key];
    if (envKey && process.env[envKey] !== undefined) {
      this.cache[key] = process.env[envKey];
      return this.cache[key];
    }

    if (ConfigManager.DEFAULTS[key] !== undefined) {
      this.cache[key] = ConfigManager.DEFAULTS[key] as string;
      return this.cache[key];
    }

    return defaultValue;
  }

  /**
   * Get all configuration keys for a specific module prefix.
   */
  public module(prefix: string): Record<string, any> {
    const config: Record<string, any> = {};
    const keys = new Set([
      ...Object.keys(ConfigManager.DEFAULTS).filter(k => k.startsWith(`${prefix}.`)),
      ...Object.keys(ConfigManager.ENV_MAP).filter(k => k.startsWith(`${prefix}.`)),
      ...Object.keys(this.overrides).filter(k => k.startsWith(`${prefix}.`))
    ]);

    for (const key of keys) {
      const shortKey = key.substring(prefix.length + 1);
      config[shortKey] = this.get(key);
    }

    return config;
  }

  /**
   * Check if Dapr is enabled via environment variables.
   */
  public isDaprEnabled(): boolean {
    return !!process.env.DAPR_HTTP_PORT;
  }

  /**
   * Get Dapr HTTP endpoint.
   */
  public daprUrl(): string {
    const port = process.env.DAPR_HTTP_PORT || '3500';
    return `http://localhost:${port}/v1.0`;
  }

  /**
   * Basic health check. Always returns true as config is local/env driven.
   */
  public async health(): Promise<boolean> {
    return true;
  }
}
