use crate::config::ConfigManager;
use base64::{Engine as _, engine::general_purpose};
use reqwest::Client;
use serde_json::{Value, json};

#[derive(Clone)]
pub struct QueueManager {
    client: Client,
    api_base: String,
    auth_header: String,
}

impl QueueManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("queue");
        let host = mod_cfg
            .get("host")
            .cloned()
            .unwrap_or_else(|| "rabbitmq.zirve-infra.svc.cluster.local".into());
        let api_port = mod_cfg
            .get("api_port")
            .cloned()
            .unwrap_or_else(|| "15672".into());
        let user = mod_cfg
            .get("user")
            .cloned()
            .unwrap_or_else(|| "guest".into());
        let password = mod_cfg
            .get("password")
            .cloned()
            .unwrap_or_else(|| "guest".into());

        let auth_str = format!("{}:{}", user, password);
        let b64 = general_purpose::STANDARD.encode(auth_str);

        Self {
            client: Client::new(),
            api_base: format!("http://{}:{}/api", host, api_port),
            auth_header: format!("Basic {}", b64),
        }
    }

    pub async fn publish<T: serde::Serialize>(
        &self,
        vhost: &str,
        exchange: Option<&str>,
        routing_key: &str,
        payload: &T,
    ) -> Result<bool, reqwest::Error> {
        let ex = exchange.unwrap_or("amq.default");

        let payload_str = match serde_json::to_string(payload) {
            Ok(s) => s,
            Err(_) => return Ok(false), // skip invalid serialization
        };

        let body = json!({
            "properties": {},
            "routing_key": routing_key,
            "payload": payload_str,
            "payload_encoding": "string"
        });

        let url = format!(
            "{}/exchanges/{}/{}/publish",
            self.api_base,
            urlencoding::encode(vhost),
            urlencoding::encode(ex)
        );

        let res = self
            .client
            .post(&url)
            .header("Authorization", &self.auth_header)
            .json(&body)
            .send()
            .await?;

        if !res.status().is_success() {
            return Ok(false);
        }

        let parsed: Value = res.json().await?;
        Ok(parsed
            .get("routed")
            .and_then(|v| v.as_bool())
            .unwrap_or(false))
    }

    pub async fn health(&self) -> bool {
        let url = format!("{}/overview", self.api_base);
        self.client
            .get(&url)
            .header("Authorization", &self.auth_header)
            .send()
            .await
            .map(|r: reqwest::Response| r.status().is_success())
            .unwrap_or(false)
    }
}
