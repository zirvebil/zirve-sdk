<?php

declare(strict_types=1);

namespace Zirve;

use Zirve\Config\ConfigManager;

/**
 * Zirve SDK — Ana giriş noktası.
 *
 * Tüm 26 altyapı servisine tek bir import ile erişim sağlar.
 * Modüller lazy-load edilir: sadece kullanıldığında oluşturulur.
 *
 * @property-read Config\ConfigManager       $config     Service discovery + env
 * @property-read Db\DbManager              $db         PostgreSQL + MariaDB
 * @property-read Cache\CacheManager         $cache      Redis
 * @property-read Auth\AuthManager           $auth       Keycloak
 * @property-read Secrets\SecretsManager     $secrets    Infisical
 * @property-read Queue\QueueManager         $queue      RabbitMQ
 * @property-read Storage\StorageManager     $storage    MinIO + Imgproxy
 * @property-read Search\SearchManager       $search     Elasticsearch
 * @property-read Analytics\AnalyticsManager $analytics  ClickHouse
 * @property-read Log\LogManager             $log        Loki
 * @property-read Error\ErrorManager         $error      Sentry
 * @property-read Trace\TraceManager         $trace      OTel + Tempo
 * @property-read Metrics\MetricsManager     $metrics    Prometheus
 * @property-read Billing\BillingManager     $billing    Lago
 * @property-read Crm\CrmManager             $crm        Odoo
 * @property-read Remote\RemoteManager       $remote     Guacamole
 * @property-read Gateway\GatewayManager     $gateway    Kong
 * @property-read Ingress\IngressManager     $ingress    Traefik
 * @property-read Registry\RegistryManager   $registry   Harbor
 * @property-read Deploy\DeployManager       $deploy     ArgoCD
 * @property-read Cluster\ClusterManager     $cluster    Rancher
 * @property-read Quality\QualityManager     $quality    SonarQube
 * @property-read OnCall\OnCallManager       $oncall     Grafana OnCall
 * @property-read Dashboard\DashboardManager $dashboard  Grafana
 * @property-read Testing\TestingManager     $testing    Keploy
 */
final class Zirve
{
    private static ?self $instance = null;

    private ConfigManager $configManager;

    /** @var array<string, object> */
    private array $modules = [];

    /** Module class map: property name → [class, config-prefix] */
    private const MODULE_MAP = [
        // Tier 1 — Data
        'config'    => [Config\ConfigManager::class, 'config'],
        'db'        => [Db\DbManager::class, 'db'],
        'cache'     => [Cache\CacheManager::class, 'cache'],
        'search'    => [Search\SearchManager::class, 'search'],
        'storage'   => [Storage\StorageManager::class, 'storage'],
        'analytics' => [Analytics\AnalyticsManager::class, 'analytics'],
        // Tier 2 — Security & Communication
        'auth'      => [Auth\AuthManager::class, 'auth'],
        'secrets'   => [Secrets\SecretsManager::class, 'secrets'],
        'queue'     => [Queue\QueueManager::class, 'queue'],
        // Tier 3 — Observability
        'log'       => [Log\LogManager::class, 'log'],
        'error'     => [Error\ErrorManager::class, 'error'],
        'trace'     => [Trace\TraceManager::class, 'trace'],
        'metrics'   => [Metrics\MetricsManager::class, 'metrics'],
        // Tier 4 — Business
        'billing'   => [Billing\BillingManager::class, 'billing'],
        'crm'       => [Crm\CrmManager::class, 'crm'],
        'remote'    => [Remote\RemoteManager::class, 'remote'],
        // Tier 5 — Platform & DevOps
        'gateway'   => [Gateway\GatewayManager::class, 'gateway'],
        'ingress'   => [Ingress\IngressManager::class, 'ingress'],
        'registry'  => [Registry\RegistryManager::class, 'registry'],
        'deploy'    => [Deploy\DeployManager::class, 'deploy'],
        'cluster'   => [Cluster\ClusterManager::class, 'cluster'],
        'quality'   => [Quality\QualityManager::class, 'quality'],
        'oncall'    => [OnCall\OnCallManager::class, 'oncall'],
        'dashboard' => [Dashboard\DashboardManager::class, 'dashboard'],
        'testing'   => [Testing\TestingManager::class, 'testing'],
    ];

    private function __construct(ConfigManager $configManager)
    {
        $this->configManager = $configManager;
        $this->modules['config'] = $configManager;
    }

    /**
     * SDK'yı başlat. Ortam değişkenlerinden veya verilen config ile.
     *
     * @param array<string, mixed> $overrides  Opsiyonel config override'ları
     */
    public static function init(array $overrides = []): self
    {
        if (self::$instance !== null) {
            return self::$instance;
        }

        $config = new ConfigManager($overrides);
        self::$instance = new self($config);

        return self::$instance;
    }

    /**
     * Singleton'ı sıfırla (test amaçlı).
     */
    public static function reset(): void
    {
        self::$instance = null;
    }

    /**
     * Lazy-load modül erişimi.
     */
    public function __get(string $name): object
    {
        if (isset($this->modules[$name])) {
            return $this->modules[$name];
        }

        if (!isset(self::MODULE_MAP[$name])) {
            throw new \InvalidArgumentException(
                "Bilinmeyen SDK modülü: '{$name}'. Mevcut modüller: " . implode(', ', array_keys(self::MODULE_MAP))
            );
        }

        [$class, $prefix] = self::MODULE_MAP[$name];
        $moduleConfig = $this->configManager->module($prefix);
        $this->modules[$name] = new $class($moduleConfig);

        return $this->modules[$name];
    }

    /**
     * Tüm servislerin sağlık kontrolü.
     *
     * @return array<string, array{status: string, detail: string, ms: int}>
     */
    public function health(): array
    {
        $results = [];

        foreach (self::MODULE_MAP as $name => [$class, $prefix]) {
            $start = hrtime(true);
            try {
                $module = $this->__get($name);
                if (method_exists($module, 'health')) {
                    $health = $module->health();
                    $results[$name] = [
                        'status' => $health ? 'healthy' : 'unhealthy',
                        'detail' => $health ? 'OK' : 'Health check failed',
                        'ms'     => (int) ((hrtime(true) - $start) / 1_000_000),
                    ];
                } else {
                    $results[$name] = [
                        'status' => 'unknown',
                        'detail' => 'No health check',
                        'ms'     => 0,
                    ];
                }
            } catch (\Throwable $e) {
                $results[$name] = [
                    'status' => 'unhealthy',
                    'detail' => $e->getMessage(),
                    'ms'     => (int) ((hrtime(true) - $start) / 1_000_000),
                ];
            }
        }

        return $results;
    }
}
