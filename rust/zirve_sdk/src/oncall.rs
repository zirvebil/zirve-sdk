use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::{Value, json};

#[derive(Clone)]
pub struct OnCallManager {
    client: Client,
    url: String,
    token: String,
}

impl OnCallManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("oncall");
        let url = mod_cfg.get("url").cloned().unwrap_or_else(|| {
            "http://grafana-oncall-engine.zirve-infra.svc.cluster.local:8080".into()
        });
        let token = mod_cfg.get("token").cloned().unwrap_or_else(|| "".into());

        Self {
            client: Client::new(),
            url: format!("{}/api/v1", url.trim_end_matches('/')),
            token,
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

        if !self.token.is_empty() {
            req = req.header("Authorization", format!("Token {}", self.token));
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

    pub async fn create_alert(
        &self,
        integration_url: &str,
        title: &str,
        message: &str,
        state: Option<&str>,
    ) -> Result<bool, reqwest::Error> {
        let st = state.unwrap_or("alerting");
        let payload = json!({
            "title": title,
            "message": message,
            "state": st,
        });

        let res = self
            .client
            .post(integration_url)
            .json(&payload)
            .send()
            .await?;

        Ok(res.status().is_success())
    }

    pub async fn list_incidents(
        &self,
        state: Option<&str>,
    ) -> Result<Option<Value>, reqwest::Error> {
        let st = state.unwrap_or("triggered");
        let path = format!("alert_groups?state={}", urlencoding::encode(st));

        let res = self.request(reqwest::Method::GET, &path, None).await?;
        if let Some(v) = res {
            if let Some(results) = v.get("results") {
                return Ok(Some(results.clone()));
            }
        }
        Ok(None)
    }

    pub async fn health(&self) -> bool {
        let req_url = format!("{}/health", self.url);
        self.client
            .get(&req_url)
            .send()
            .await
            .map(|r: reqwest::Response| r.status().is_success())
            .unwrap_or(false)
    }
}
