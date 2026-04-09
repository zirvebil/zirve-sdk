use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::{Value, json};

#[derive(Clone)]
pub struct BillingManager {
    client: Client,
    url: String,
    api_key: String,
}

impl BillingManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("billing");
        let url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://lago-api.zirve-infra.svc.cluster.local".into());
        let api_key = mod_cfg.get("api_key").cloned().unwrap_or_else(|| "".into());

        Self {
            client: Client::new(),
            url: format!("{}/api/v1", url.trim_end_matches('/')),
            api_key,
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

        if !self.api_key.is_empty() {
            req = req.bearer_auth(&self.api_key);
        }

        let res: reqwest::Response = req.send().await?;

        if res.status().as_u16() == 404
            || res.status().as_u16() == 204
            || res.content_length() == Some(0)
        {
            return Ok(None);
        }

        if !res.status().is_success() {
            // Optional: extract Lago API custom error message.
            // Silent error boundary standard in observability.
        }

        Ok(Some(res.json::<Value>().await?))
    }

    pub async fn create_customer<T: serde::Serialize>(
        &self,
        customer: &T,
    ) -> Result<Option<Value>, reqwest::Error> {
        self.request(
            reqwest::Method::POST,
            "customers",
            Some(json!({ "customer": customer })),
        )
        .await
    }

    pub async fn create_subscription<T: serde::Serialize>(
        &self,
        sub: &T,
    ) -> Result<Option<Value>, reqwest::Error> {
        self.request(
            reqwest::Method::POST,
            "subscriptions",
            Some(json!({ "subscription": sub })),
        )
        .await
    }

    pub async fn add_event<T: serde::Serialize>(
        &self,
        event: &T,
    ) -> Result<Option<Value>, reqwest::Error> {
        self.request(
            reqwest::Method::POST,
            "events",
            Some(json!({ "event": event })),
        )
        .await
    }

    pub async fn health(&self) -> bool {
        let req_url = format!("{}/organizations", self.url);
        let mut req = self.client.get(&req_url);
        if !self.api_key.is_empty() {
            req = req.bearer_auth(&self.api_key);
        }

        let res = req.send().await;
        if let Ok(r) = res {
            // Lago usually returns 401 if missing auth but API is alive, or 200
            return r.status().is_success() || r.status().as_u16() == 401;
        }
        false
    }
}
