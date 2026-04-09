use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::{Value, json};

#[derive(Clone)]
pub struct GatewayManager {
    client: Client,
    url: String,
}

impl GatewayManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("gateway");
        let url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://kong-kong-admin.zirve-infra.svc.cluster.local:8001".into());

        Self {
            client: Client::new(),
            url: url.trim_end_matches('/').to_string(),
        }
    }

    async fn request(
        &self,
        method: reqwest::Method,
        path: &str,
        body: Option<Value>,
    ) -> Result<Option<Value>, reqwest::Error> {
        let req_url = format!("{}/{}", self.url, path.trim_start_matches('/'));
        let mut req = self.client.request(method, &req_url);

        if let Some(b) = body {
            req = req.json(&b);
        }

        let res: reqwest::Response = req.send().await?;

        if res.status().as_u16() == 404
            || res.status().as_u16() == 204
            || res.content_length() == Some(0)
        {
            return Ok(None);
        }

        Ok(Some(res.json::<Value>().await?))
    }

    pub async fn add_service(
        &self,
        name: &str,
        protocol: &str,
        host: &str,
        port: u16,
        path: &str,
    ) -> Result<Option<Value>, reqwest::Error> {
        let payload = json!({
            "name": name,
            "protocol": protocol,
            "host": host,
            "port": port,
            "path": path,
        });

        self.request(reqwest::Method::POST, "services", Some(payload))
            .await
    }

    pub async fn add_route(
        &self,
        service_id_or_name: &str,
        name: &str,
        paths: Vec<&str>,
    ) -> Result<Option<Value>, reqwest::Error> {
        let payload = json!({
            "name": name,
            "paths": paths,
            "strip_path": true,
        });

        self.request(
            reqwest::Method::POST,
            &format!("services/{}/routes", service_id_or_name),
            Some(payload),
        )
        .await
    }

    pub async fn health(&self) -> bool {
        let req_url = format!("{}/status", self.url);
        self.client
            .get(&req_url)
            .send()
            .await
            .map(|r: reqwest::Response| r.status().is_success())
            .unwrap_or(false)
    }
}
