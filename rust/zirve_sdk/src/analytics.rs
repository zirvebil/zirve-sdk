use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::Value;
use std::collections::HashMap;

#[derive(Clone)]
pub struct AnalyticsManager {
    client: Client,
    base_url: String,
    user: String,
    pass: String,
}

impl AnalyticsManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("analytics");
        let host = mod_cfg
            .get("host")
            .cloned()
            .unwrap_or_else(|| "clickhouse.zirve-infra.svc.cluster.local".into());
        let port = mod_cfg
            .get("port")
            .cloned()
            .unwrap_or_else(|| "8123".into());
        let user = mod_cfg
            .get("username")
            .cloned()
            .unwrap_or_else(|| "default".into());
        let pass = mod_cfg
            .get("password")
            .cloned()
            .unwrap_or_else(|| "".into());

        Self {
            client: Client::new(),
            base_url: format!("http://{}:{}", host, port),
            user,
            pass,
        }
    }

    pub async fn query(&self, sql: &str) -> Result<Value, reqwest::Error> {
        let mut final_sql = sql.trim().to_string();

        if !final_sql.to_uppercase().ends_with("FORMAT JSON") {
            if final_sql.ends_with(';') {
                final_sql = format!("{} FORMAT JSON;", final_sql.trim_end_matches(';'));
            } else {
                final_sql = format!("{} FORMAT JSON", final_sql);
            }
        }

        let mut req = self
            .client
            .post(&format!("{}/", self.base_url))
            .body(final_sql)
            .header("X-ClickHouse-User", &self.user);

        if !self.pass.is_empty() {
            req = req.header("X-ClickHouse-Key", &self.pass);
        }

        let res: reqwest::Response = req.send().await?;

        // Handle inserts which return no JSON body
        if res.content_length() == Some(0) {
            return Ok(serde_json::json!({"status": "success"}));
        }

        res.json::<Value>().await
    }

    pub async fn health(&self) -> bool {
        let url = format!("{}/ping", self.base_url);
        self.client
            .get(&url)
            .send()
            .await
            .map(|r: reqwest::Response| r.status().is_success())
            .unwrap_or(false)
    }
}
