use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::Value;

#[derive(Clone)]
pub struct IngressManager {
    client: Client,
    url: String,
}

impl IngressManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("ingress");
        let url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://traefik.kube-system.svc.cluster.local:8080".into());

        Self {
            client: Client::new(),
            url: url.trim_end_matches('/').to_string(),
        }
    }

    async fn request(&self, path: &str) -> Result<Option<Value>, reqwest::Error> {
        let req_url = format!("{}/{}", self.url, path.trim_start_matches('/'));
        let res: reqwest::Response = self.client.get(&req_url).send().await?;

        if !res.status().is_success() {
            return Ok(None);
        }

        Ok(Some(res.json::<Value>().await?))
    }

    pub async fn get_routes(&self) -> Result<Option<Value>, reqwest::Error> {
        self.request("api/http/routers").await
    }

    pub async fn get_middlewares(&self) -> Result<Option<Value>, reqwest::Error> {
        self.request("api/http/middlewares").await
    }

    pub async fn health(&self) -> bool {
        let req_url = format!("{}/ping", self.url);
        self.client
            .get(&req_url)
            .send()
            .await
            .map(|r: reqwest::Response| r.status().is_success())
            .unwrap_or(false)
    }
}
