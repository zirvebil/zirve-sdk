use std::collections::HashMap;
use std::env;
use std::sync::{Arc, RwLock};

#[derive(Clone)]
pub struct ConfigManager {
    defaults: HashMap<String, String>,
    env_map: HashMap<String, String>,
    cache: Arc<RwLock<HashMap<String, String>>>,
}

impl ConfigManager {
    pub fn new() -> Self {
        let mut defaults = HashMap::new();
        defaults.insert(
            "db.host".into(),
            "postgresql.zirve-infra.svc.cluster.local".into(),
        );
        defaults.insert("db.port".into(), "5432".into());
        defaults.insert("db.dbname".into(), "zirve".into());
        defaults.insert("db.user".into(), "postgres".into());
        defaults.insert(
            "cache.host".into(),
            "redis-master.zirve-infra.svc.cluster.local".into(),
        );
        defaults.insert("cache.port".into(), "6379".into());
        defaults.insert(
            "auth.url".into(),
            "http://keycloak.zirve-infra.svc.cluster.local".into(),
        );
        defaults.insert(
            "secrets.url".into(),
            "http://infisical.zirve-infra.svc.cluster.local".into(),
        );
        defaults.insert(
            "queue.host".into(),
            "rabbitmq.zirve-infra.svc.cluster.local".into(),
        );
        defaults.insert("queue.port".into(), "5672".into());
        defaults.insert("queue.api_port".into(), "15672".into());
        defaults.insert(
            "storage.endpoint".into(),
            "http://minio.zirve-infra.svc.cluster.local:9000".into(),
        );
        defaults.insert(
            "storage.imgproxy".into(),
            "http://imgproxy.zirve-infra.svc.cluster.local".into(),
        );
        defaults.insert(
            "search.host".into(),
            "elasticsearch-master.zirve-infra.svc.cluster.local".into(),
        );
        defaults.insert("search.port".into(), "9200".into());
        defaults.insert(
            "analytics.host".into(),
            "clickhouse.zirve-infra.svc.cluster.local".into(),
        );
        defaults.insert("analytics.port".into(), "8123".into());
        defaults.insert(
            "log.url".into(),
            "http://loki.zirve-infra.svc.cluster.local:3100".into(),
        );
        defaults.insert(
            "error.url".into(),
            "http://sentry.zirve-infra.svc.cluster.local:9000".into(),
        );
        defaults.insert(
            "trace.endpoint".into(),
            "http://opentelemetry-collector.zirve-infra.svc.cluster.local:4318".into(),
        );
        defaults.insert(
            "metrics.url".into(),
            "http://prometheus-server.zirve-infra.svc.cluster.local:9090".into(),
        );
        defaults.insert(
            "billing.url".into(),
            "http://lago-api.zirve-infra.svc.cluster.local".into(),
        );
        defaults.insert(
            "crm.url".into(),
            "http://odoo.zirve-infra.svc.cluster.local:8069".into(),
        );
        defaults.insert(
            "remote.url".into(),
            "http://guacamole.zirve-infra.svc.cluster.local:8080".into(),
        );
        defaults.insert(
            "gateway.url".into(),
            "http://kong-kong-admin.zirve-infra.svc.cluster.local:8001".into(),
        );
        defaults.insert(
            "ingress.url".into(),
            "http://traefik.kube-system.svc.cluster.local:8080".into(),
        );
        defaults.insert(
            "registry.url".into(),
            "http://harbor-core.zirve-infra.svc.cluster.local".into(),
        );
        defaults.insert(
            "deploy.url".into(),
            "http://argocd-server.argocd.svc.cluster.local".into(),
        );
        defaults.insert(
            "cluster.url".into(),
            "http://rancher.cattle-system.svc.cluster.local".into(),
        );
        defaults.insert(
            "quality.url".into(),
            "http://sonarqube-sonarqube.zirve-infra.svc.cluster.local:9000".into(),
        );
        defaults.insert(
            "oncall.url".into(),
            "http://grafana-oncall-engine.zirve-infra.svc.cluster.local:8080".into(),
        );
        defaults.insert(
            "dashboard.url".into(),
            "http://grafana.zirve-infra.svc.cluster.local".into(),
        );
        defaults.insert(
            "testing.url".into(),
            "http://keploy.zirve-infra.svc.cluster.local:6789".into(),
        );

        let mut env_map = HashMap::new();
        env_map.insert("db.host".into(), "PG_HOST".into());
        env_map.insert("db.port".into(), "PG_PORT".into());
        env_map.insert("db.dbname".into(), "PG_DBNAME".into());
        env_map.insert("db.user".into(), "PG_USER".into());
        env_map.insert("db.password".into(), "PG_PASSWORD".into());
        env_map.insert("cache.host".into(), "REDIS_HOST".into());
        env_map.insert("cache.port".into(), "REDIS_PORT".into());
        env_map.insert("cache.password".into(), "REDIS_PASSWORD".into());
        env_map.insert("auth.url".into(), "KEYCLOAK_URL".into());
        env_map.insert("auth.realm".into(), "KEYCLOAK_REALM".into());
        env_map.insert("auth.client_id".into(), "KEYCLOAK_CLIENT_ID".into());
        env_map.insert("auth.client_secret".into(), "KEYCLOAK_CLIENT_SECRET".into());
        env_map.insert("secrets.url".into(), "INFISICAL_URL".into());
        env_map.insert("secrets.token".into(), "INFISICAL_TOKEN".into());
        env_map.insert("secrets.project_id".into(), "INFISICAL_PROJECT_ID".into());
        env_map.insert("queue.host".into(), "RABBITMQ_HOST".into());
        env_map.insert("queue.user".into(), "RABBITMQ_USER".into());
        env_map.insert("queue.password".into(), "RABBITMQ_PASSWORD".into());
        env_map.insert("storage.endpoint".into(), "MINIO_ENDPOINT".into());
        env_map.insert("storage.key".into(), "MINIO_ACCESS_KEY".into());
        env_map.insert("storage.secret".into(), "MINIO_SECRET_KEY".into());
        env_map.insert("storage.imgproxy".into(), "IMGPROXY_URL".into());
        env_map.insert("search.host".into(), "ELASTIC_HOST".into());
        env_map.insert("search.username".into(), "ELASTIC_USER".into());
        env_map.insert("search.password".into(), "ELASTIC_PASSWORD".into());
        env_map.insert("analytics.host".into(), "CLICKHOUSE_HOST".into());
        env_map.insert("analytics.username".into(), "CLICKHOUSE_USER".into());
        env_map.insert("analytics.password".into(), "CLICKHOUSE_PASSWORD".into());
        env_map.insert("log.url".into(), "LOKI_URL".into());
        env_map.insert("error.dsn".into(), "SENTRY_DSN".into());
        env_map.insert(
            "trace.endpoint".into(),
            "OTEL_EXPORTER_OTLP_ENDPOINT".into(),
        );
        env_map.insert("metrics.url".into(), "PROMETHEUS_URL".into());
        env_map.insert("billing.url".into(), "LAGO_URL".into());
        env_map.insert("billing.api_key".into(), "LAGO_API_KEY".into());
        env_map.insert("crm.url".into(), "ODOO_URL".into());
        env_map.insert("crm.username".into(), "ODOO_USER".into());
        env_map.insert("crm.password".into(), "ODOO_PASSWORD".into());
        env_map.insert("crm.database".into(), "ODOO_DB".into());
        env_map.insert("remote.url".into(), "GUACAMOLE_URL".into());
        env_map.insert("remote.username".into(), "GUACAMOLE_USER".into());
        env_map.insert("remote.password".into(), "GUACAMOLE_PASSWORD".into());
        env_map.insert("gateway.url".into(), "KONG_ADMIN_URL".into());
        env_map.insert("ingress.url".into(), "TRAEFIK_API_URL".into());
        env_map.insert("registry.url".into(), "HARBOR_URL".into());
        env_map.insert("registry.username".into(), "HARBOR_USER".into());
        env_map.insert("registry.password".into(), "HARBOR_PASSWORD".into());
        env_map.insert("deploy.url".into(), "ARGOCD_URL".into());
        env_map.insert("deploy.token".into(), "ARGOCD_TOKEN".into());
        env_map.insert("cluster.url".into(), "RANCHER_URL".into());
        env_map.insert("cluster.token".into(), "RANCHER_TOKEN".into());
        env_map.insert("quality.url".into(), "SONAR_URL".into());
        env_map.insert("quality.token".into(), "SONAR_TOKEN".into());
        env_map.insert("oncall.url".into(), "GRAFANA_ONCALL_URL".into());
        env_map.insert("oncall.token".into(), "GRAFANA_ONCALL_TOKEN".into());
        env_map.insert("dashboard.url".into(), "GRAFANA_URL".into());
        env_map.insert("dashboard.token".into(), "GRAFANA_TOKEN".into());
        env_map.insert("testing.url".into(), "KEPLOY_URL".into());

        Self {
            defaults,
            env_map,
            cache: Arc::new(RwLock::new(HashMap::new())),
        }
    }

    pub fn get(&self, key: &str, default_val: &str) -> String {
        {
            let cache_read = self.cache.read().unwrap();
            if let Some(val) = cache_read.get(key) {
                return val.clone();
            }
        }

        if let Some(env_key) = self.env_map.get(key) {
            if let Ok(val) = env::var(env_key) {
                if !val.is_empty() {
                    self.cache
                        .write()
                        .unwrap()
                        .insert(key.to_string(), val.clone());
                    return val;
                }
            }
        }

        if let Some(val) = self.defaults.get(key) {
            self.cache
                .write()
                .unwrap()
                .insert(key.to_string(), val.clone());
            return val.clone();
        }

        default_val.to_string()
    }

    pub fn module(&self, prefix: &str) -> HashMap<String, String> {
        let mut result = HashMap::new();
        let target_prefix = format!("{}.", prefix);

        for k in self.defaults.keys() {
            if k.starts_with(&target_prefix) {
                let short_key = k.trim_start_matches(&target_prefix).to_string();
                result.insert(short_key, self.get(k, ""));
            }
        }

        for k in self.env_map.keys() {
            if k.starts_with(&target_prefix) {
                let short_key = k.trim_start_matches(&target_prefix).to_string();
                result.insert(short_key, self.get(k, ""));
            }
        }

        result
    }
}
