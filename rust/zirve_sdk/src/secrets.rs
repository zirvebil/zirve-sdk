use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::Value;
use std::collections::HashMap;
use std::sync::{Arc, RwLock};
use std::time::{Duration, Instant};

#[derive(Clone)]
struct CacheEntry {
    value: String,
    expires_at: Instant,
}

#[derive(Clone)]
pub struct SecretsManager {
    client: Client,
    base_url: String,
    token: String,
    project_id: String,
    cache: Arc<RwLock<HashMap<String, CacheEntry>>>,
}

impl SecretsManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("secrets");
        let base_url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://infisical.zirve-infra.svc.cluster.local".into());
        let token = mod_cfg.get("token").cloned().unwrap_or_else(|| "".into());
        let project_id = mod_cfg
            .get("project_id")
            .cloned()
            .unwrap_or_else(|| "".into());

        Self {
            client: Client::new(),
            base_url: base_url.trim_end_matches('/').to_string(),
            token,
            project_id,
            cache: Arc::new(RwLock::new(HashMap::new())),
        }
    }

    pub async fn get(
        &self,
        secret_name: &str,
        environment: Option<&str>,
        path: Option<&str>,
    ) -> Result<Option<String>, reqwest::Error> {
        let env = environment.unwrap_or("dev");
        let pth = path.unwrap_or("/");

        let cache_key = format!("{}:{}:{}", env, pth, secret_name);

        {
            let cache_read = self.cache.read().unwrap();
            if let Some(entry) = cache_read.get(&cache_key) {
                if entry.expires_at > Instant::now() {
                    return Ok(Some(entry.value.clone()));
                }
            }
        }

        if self.token.is_empty() || self.project_id.is_empty() {
            // Can't fetch without creds
            return Ok(None);
        }

        let url = format!(
            "{}/api/v3/secrets/raw/{}?workspaceId={}&environment={}&secretPath={}",
            self.base_url, secret_name, self.project_id, env, pth
        );

        let res = self
            .client
            .get(&url)
            .bearer_auth(&self.token)
            .send()
            .await?;

        if res.status().as_u16() == 404 {
            return Ok(None);
        }

        let parsed: Value = res.json().await?;
        if let Some(secret_val) = parsed
            .get("secret")
            .and_then(|s| s.get("secretValue"))
            .and_then(|v| v.as_str())
        {
            let val_str = secret_val.to_string();

            self.cache.write().unwrap().insert(
                cache_key,
                CacheEntry {
                    value: val_str.clone(),
                    expires_at: Instant::now() + Duration::from_secs(300), // 5 minutes
                },
            );

            return Ok(Some(val_str));
        }

        Ok(None)
    }

    pub async fn health(&self) -> bool {
        let url = format!("{}/api/v1/health", self.base_url);
        self.client
            .get(&url)
            .send()
            .await
            .map(|r: reqwest::Response| r.status().is_success())
            .unwrap_or(false)
    }
}
