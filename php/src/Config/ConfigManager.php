<?php

declare(strict_types=1);

namespace Zirve\Config;

/**
 * Zirve Config Manager — Service discovery + environment configuration.
 *
 * Ortam değişkenlerinden ve Kubernetes service DNS'ten
 * tüm servis URL'lerini ve bağlantı bilgilerini okur.
 */
final class ConfigManager
{
    /** @var array<string, mixed> */
    private array $config;

    /** Default K8s service endpoints (cluster DNS) */
    private const DEFAULTS = [
        // Tier 1 — Data
        'db.host'           => 'postgresql.zirve-data.svc.cluster.local',
        'db.port'           => 5432,
        'db.username'       => 'postgres',
        'db.password'       => '',
        'db.database'       => 'postgres',
        'db.driver'         => 'pgsql',

        'db.mariadb.host'   => 'mariadb.zirve-data.svc.cluster.local',
        'db.mariadb.port'   => 3306,

        'cache.host'        => 'redis-master.zirve-data.svc.cluster.local',
        'cache.port'        => 6379,
        'cache.password'    => '',
        'cache.prefix'      => 'zirve:',

        'search.host'       => 'elasticsearch-master.zirve-data.svc.cluster.local',
        'search.port'       => 9200,
        'search.scheme'     => 'https',
        'search.username'   => 'elastic',
        'search.password'   => '',

        'storage.endpoint'  => 'http://minio.zirve-services.svc.cluster.local:9000',
        'storage.key'       => '',
        'storage.secret'    => '',
        'storage.bucket'    => 'zirve',
        'storage.imgproxy'  => 'http://imgproxy.zirve-services.svc.cluster.local:8080',

        'analytics.host'    => 'clickhouse.zirve-monitoring.svc.cluster.local',
        'analytics.port'    => 8123,

        // Tier 2 — Security & Communication
        'queue.host'        => 'rabbitmq.zirve-services.svc.cluster.local',
        'queue.port'        => 5672,
        'queue.username'    => 'guest',
        'queue.password'    => 'guest',

        'auth.url'          => 'http://keycloak.zirve-services.svc.cluster.local:8080',
        'auth.realm'        => 'zirve',
        'auth.client_id'    => '',
        'auth.client_secret'=> '',

        'secrets.url'       => 'http://infisical-infisical-standalone-infisical.zirve-security.svc.cluster.local:8080',
        'secrets.token'     => '',

        // Tier 3 — Observability
        'log.url'           => 'http://loki.zirve-monitoring.svc.cluster.local:3100',
        'error.dsn'         => '',
        'trace.endpoint'    => 'http://otel-collector.zirve-monitoring.svc.cluster.local:4318',
        'metrics.url'       => 'http://prometheus.zirve-monitoring.svc.cluster.local:9090',

        // Tier 4 — Business
        'billing.url'       => 'http://lago-api.zirve-services.svc.cluster.local:3000',
        'billing.api_key'   => '',
        'crm.url'           => 'http://odoo.zirve-services.svc.cluster.local:8069',
        'crm.api_key'       => '',
        'remote.url'        => 'http://guacamole.zirve-services.svc.cluster.local:8080',

        // Tier 5 — Platform & DevOps
        'gateway.url'       => 'http://kong-admin.zirve-ingress.svc.cluster.local:8001',
        'ingress.url'       => 'http://traefik.kube-system.svc.cluster.local:8080',
        'registry.url'      => 'http://harbor-core.zirve-ci.svc.cluster.local:80',
        'deploy.url'        => 'http://argocd-server.argocd.svc.cluster.local:80',
        'cluster.url'       => 'http://rancher.cattle-system.svc.cluster.local:80',
        'quality.url'       => 'http://sonarqube-sonarqube.zirve-ci.svc.cluster.local:9000',
        'oncall.url'        => 'http://grafana-oncall-engine.zirve-monitoring.svc.cluster.local:8080',
        'dashboard.url'     => 'http://kube-prometheus-stack-grafana.zirve-monitoring.svc.cluster.local:80',
        'testing.url'       => 'http://keploy.zirve-ci.svc.cluster.local:16789',

        // Meta
        'environment'       => 'local',
        'service_name'      => 'unknown',
    ];

    /** Env var prefix mapping: config key → ENV var name */
    private const ENV_MAP = [
        'db.host'            => 'PG_HOST',
        'db.port'            => 'PG_PORT',
        'db.username'        => 'PG_USER',
        'db.password'        => 'PG_PASSWORD',
        'db.database'        => 'PG_DATABASE',
        'db.driver'          => 'DB_DRIVER',
        'db.mariadb.host'    => 'MARIADB_HOST',
        'db.mariadb.port'    => 'MARIADB_PORT',
        'cache.host'         => 'REDIS_HOST',
        'cache.port'         => 'REDIS_PORT',
        'cache.password'     => 'REDIS_PASSWORD',
        'cache.prefix'       => 'REDIS_PREFIX',
        'search.host'        => 'ES_HOST',
        'search.port'        => 'ES_PORT',
        'search.username'    => 'ES_USER',
        'search.password'    => 'ES_PASSWORD',
        'storage.endpoint'   => 'MINIO_ENDPOINT',
        'storage.key'        => 'MINIO_ACCESS_KEY',
        'storage.secret'     => 'MINIO_SECRET_KEY',
        'storage.bucket'     => 'MINIO_BUCKET',
        'storage.imgproxy'   => 'IMGPROXY_URL',
        'analytics.host'     => 'CLICKHOUSE_HOST',
        'analytics.port'     => 'CLICKHOUSE_PORT',
        'queue.host'         => 'RABBITMQ_HOST',
        'queue.port'         => 'RABBITMQ_PORT',
        'queue.username'     => 'RABBITMQ_USER',
        'queue.password'     => 'RABBITMQ_PASSWORD',
        'auth.url'           => 'KEYCLOAK_URL',
        'auth.realm'         => 'KEYCLOAK_REALM',
        'auth.client_id'     => 'KEYCLOAK_CLIENT_ID',
        'auth.client_secret' => 'KEYCLOAK_CLIENT_SECRET',
        'secrets.url'        => 'INFISICAL_URL',
        'secrets.token'      => 'INFISICAL_TOKEN',
        'log.url'            => 'LOKI_URL',
        'error.dsn'          => 'SENTRY_DSN',
        'trace.endpoint'     => 'OTEL_EXPORTER_OTLP_ENDPOINT',
        'metrics.url'        => 'PROMETHEUS_URL',
        'billing.url'        => 'LAGO_URL',
        'billing.api_key'    => 'LAGO_API_KEY',
        'crm.url'            => 'ODOO_URL',
        'crm.api_key'        => 'ODOO_API_KEY',
        'remote.url'         => 'GUACAMOLE_URL',
        'gateway.url'        => 'KONG_ADMIN_URL',
        'ingress.url'        => 'TRAEFIK_API_URL',
        'registry.url'       => 'HARBOR_URL',
        'deploy.url'         => 'ARGOCD_URL',
        'cluster.url'        => 'RANCHER_URL',
        'quality.url'        => 'SONARQUBE_URL',
        'oncall.url'         => 'ONCALL_URL',
        'dashboard.url'      => 'GRAFANA_URL',
        'testing.url'        => 'KEPLOY_URL',
        'environment'        => 'DEPLOY_ENV',
        'service_name'       => 'SERVICE_NAME',
    ];

    /**
     * @param array<string, mixed> $overrides
     */
    public function __construct(array $overrides = [])
    {
        $this->config = self::DEFAULTS;

        // Override from environment variables
        foreach (self::ENV_MAP as $key => $envVar) {
            $value = getenv($envVar);
            if ($value !== false && $value !== '') {
                $this->config[$key] = is_numeric($value) ? (int) $value : $value;
            }
        }

        // Override from explicit config
        foreach ($overrides as $key => $value) {
            $this->config[$key] = $value;
        }
    }

    /**
     * Tek config değeri al.
     */
    public function get(string $key, mixed $default = null): mixed
    {
        return $this->config[$key] ?? $default;
    }

    /**
     * Belirli bir modüle ait tüm config'leri al.
     *
     * @return array<string, mixed>
     */
    public function module(string $prefix): array
    {
        $result = [];
        $prefix .= '.';
        $len = strlen($prefix);

        foreach ($this->config as $key => $value) {
            if (str_starts_with($key, $prefix)) {
                $result[substr($key, $len)] = $value;
            }
        }

        return $result;
    }

    /**
     * Bir servisin K8s cluster DNS URL'ini döndür.
     */
    public function serviceUrl(string $module): string
    {
        return (string) ($this->config["{$module}.url"] ?? $this->config["{$module}.endpoint"] ?? '');
    }

    /**
     * Ortam bilgisi.
     */
    public function environment(): string
    {
        return (string) $this->config['environment'];
    }

    /**
     * Sağlık kontrolü — her zaman healthy (config statik).
     */
    public function health(): bool
    {
        return true;
    }
}
