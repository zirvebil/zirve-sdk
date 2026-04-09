package config

import (
	"os"
	"strings"
	"sync"
)

// Manager handles Zirve SDK configuration.
type Manager struct {
	defaults map[string]string
	envMap   map[string]string
	cache    sync.Map
}

func NewManager() *Manager {
	defaults := map[string]string{
		"db.host":          "postgresql.zirve-infra.svc.cluster.local",
		"db.port":          "5432",
		"db.dbname":        "zirve",
		"db.user":          "postgres",
		"cache.host":       "redis-master.zirve-infra.svc.cluster.local",
		"cache.port":       "6379",
		"auth.url":         "http://keycloak.zirve-infra.svc.cluster.local",
		"secrets.url":      "http://infisical.zirve-infra.svc.cluster.local",
		"queue.host":       "rabbitmq.zirve-infra.svc.cluster.local",
		"queue.port":       "5672",
		"queue.api_port":   "15672",
		"storage.endpoint": "http://minio.zirve-infra.svc.cluster.local:9000",
		"storage.imgproxy": "http://imgproxy.zirve-infra.svc.cluster.local",
		"search.host":      "elasticsearch-master.zirve-infra.svc.cluster.local",
		"search.port":      "9200",
		"analytics.host":   "clickhouse.zirve-infra.svc.cluster.local",
		"analytics.port":   "8123",
		"log.url":          "http://loki.zirve-infra.svc.cluster.local:3100",
		"error.url":        "http://sentry.zirve-infra.svc.cluster.local:9000",
		"trace.endpoint":   "http://opentelemetry-collector.zirve-infra.svc.cluster.local:4318",
		"metrics.url":      "http://prometheus-server.zirve-infra.svc.cluster.local:9090",
		"billing.url":      "http://lago-api.zirve-infra.svc.cluster.local",
		"crm.url":          "http://odoo.zirve-infra.svc.cluster.local:8069",
		"remote.url":       "http://guacamole.zirve-infra.svc.cluster.local:8080",
		"gateway.url":      "http://kong-kong-admin.zirve-infra.svc.cluster.local:8001",
		"ingress.url":      "http://traefik.kube-system.svc.cluster.local:8080",
		"registry.url":     "http://harbor-core.zirve-infra.svc.cluster.local",
		"deploy.url":       "http://argocd-server.argocd.svc.cluster.local",
		"cluster.url":      "http://rancher.cattle-system.svc.cluster.local",
		"quality.url":      "http://sonarqube-sonarqube.zirve-infra.svc.cluster.local:9000",
		"oncall.url":       "http://grafana-oncall-engine.zirve-infra.svc.cluster.local:8080",
		"dashboard.url":    "http://grafana.zirve-infra.svc.cluster.local",
		"testing.url":      "http://keploy.zirve-infra.svc.cluster.local:6789",
	}

	envMap := map[string]string{
		"db.host":          "PG_HOST",
		"db.port":          "PG_PORT",
		"db.dbname":        "PG_DBNAME",
		"db.user":          "PG_USER",
		"db.password":      "PG_PASSWORD",
		"cache.host":       "REDIS_HOST",
		"cache.port":       "REDIS_PORT",
		"cache.password":   "REDIS_PASSWORD",
		"auth.url":         "KEYCLOAK_URL",
		"auth.realm":       "KEYCLOAK_REALM",
		"auth.client_id":   "KEYCLOAK_CLIENT_ID",
		"auth.client_secret": "KEYCLOAK_CLIENT_SECRET",
		"secrets.url":      "INFISICAL_URL",
		"secrets.token":    "INFISICAL_TOKEN",
		"secrets.project_id": "INFISICAL_PROJECT_ID",
		"queue.host":       "RABBITMQ_HOST",
		"queue.user":       "RABBITMQ_USER",
		"queue.password":   "RABBITMQ_PASSWORD",
		"storage.endpoint": "MINIO_ENDPOINT",
		"storage.key":      "MINIO_ACCESS_KEY",
		"storage.secret":   "MINIO_SECRET_KEY",
		"storage.imgproxy": "IMGPROXY_URL",
		"search.host":      "ELASTIC_HOST",
		"search.username":  "ELASTIC_USER",
		"search.password":  "ELASTIC_PASSWORD",
		"analytics.host":   "CLICKHOUSE_HOST",
		"analytics.username": "CLICKHOUSE_USER",
		"analytics.password": "CLICKHOUSE_PASSWORD",
		"log.url":          "LOKI_URL",
		"error.dsn":        "SENTRY_DSN",
		"trace.endpoint":   "OTEL_EXPORTER_OTLP_ENDPOINT",
		"metrics.url":      "PROMETHEUS_URL",
		"billing.url":      "LAGO_URL",
		"billing.api_key":  "LAGO_API_KEY",
		"crm.url":          "ODOO_URL",
		"crm.username":     "ODOO_USER",
		"crm.password":     "ODOO_PASSWORD",
		"crm.database":     "ODOO_DB",
		"remote.url":       "GUACAMOLE_URL",
		"remote.username":  "GUACAMOLE_USER",
		"remote.password":  "GUACAMOLE_PASSWORD",
		"gateway.url":      "KONG_ADMIN_URL",
		"ingress.url":      "TRAEFIK_API_URL",
		"registry.url":     "HARBOR_URL",
		"registry.username": "HARBOR_USER",
		"registry.password": "HARBOR_PASSWORD",
		"deploy.url":       "ARGOCD_URL",
		"deploy.token":     "ARGOCD_TOKEN",
		"cluster.url":      "RANCHER_URL",
		"cluster.token":    "RANCHER_TOKEN",
		"quality.url":      "SONAR_URL",
		"quality.token":    "SONAR_TOKEN",
		"oncall.url":       "GRAFANA_ONCALL_URL",
		"oncall.token":     "GRAFANA_ONCALL_TOKEN",
		"dashboard.url":    "GRAFANA_URL",
		"dashboard.token":  "GRAFANA_TOKEN",
		"testing.url":      "KEPLOY_URL",
	}

	return &Manager{
		defaults: defaults,
		envMap:   envMap,
	}
}

// Get checking env -> defaults
func (c *Manager) Get(key string, defaultVal string) string {
	if val, ok := c.cache.Load(key); ok {
		return val.(string)
	}

	if envKey, ok := c.envMap[key]; ok {
		if val := os.Getenv(envKey); val != "" {
			c.cache.Store(key, val)
			return val
		}
	}

	if val, ok := c.defaults[key]; ok {
		c.cache.Store(key, val)
		return val
	}

	return defaultVal
}

// Module gets a module-specific config dictionary.
func (c *Manager) Module(prefix string) map[string]string {
	result := make(map[string]string)
	keys := make(map[string]bool)

	for k := range c.defaults {
		keys[k] = true
	}
	for k := range c.envMap {
		keys[k] = true
	}

	for k := range keys {
		if strings.HasPrefix(k, prefix+".") {
			shortKey := strings.TrimPrefix(k, prefix+".")
			result[shortKey] = c.Get(k, "")
		}
	}

	return result
}
