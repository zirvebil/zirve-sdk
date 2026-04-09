use crate::config::ConfigManager;
use chrono::Utc;
use reqwest::Client;
use serde_json::{Value, json};
use std::env;
use std::time::Duration;
use uuid::Uuid;

#[derive(Clone)]
pub struct ErrorManager {
    client: Client,
    dsn: String,
    url: String,
    key: String,
    project_id: String,
}

impl ErrorManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("error");
        let dsn = mod_cfg.get("dsn").cloned().unwrap_or_else(|| "".into());

        let mut url = String::new();
        let mut key = String::new();
        let mut project_id = String::new();

        if !dsn.is_empty() && dsn.starts_with("http") {
            if let Ok(parsed) = reqwest::Url::parse(&dsn) {
                url = format!("{}://{}", parsed.scheme(), parsed.host_str().unwrap_or(""));
                key = parsed.username().to_string();
                project_id = parsed.path().trim_start_matches('/').to_string();
            }
        }

        Self {
            client: Client::builder()
                .timeout(Duration::from_secs(5))
                .build()
                .unwrap(),
            dsn,
            url,
            key,
            project_id,
        }
    }

    pub async fn capture_message(
        &self,
        message: &str,
        level: Option<&str>,
        context: Option<Value>,
    ) -> Result<String, reqwest::Error> {
        if self.url.is_empty() || self.key.is_empty() || self.project_id.is_empty() {
            return Ok("".into());
        }

        let lvl = level.unwrap_or("info");
        let event_id = Uuid::new_v4().simple().to_string();
        let timestamp = Utc::now().to_rfc3339();
        let env_name = env::var("APP_ENV").unwrap_or_else(|_| "production".into());
        let server_name = hostname::get()
            .map(|h| h.to_string_lossy().into_owned())
            .unwrap_or_else(|_| "unknown".into());

        let payload = json!({
            "event_id": event_id,
            "timestamp": timestamp,
            "platform": "rust",
            "level": lvl,
            "environment": env_name,
            "server_name": server_name,
            "message": message,
            "extra": context.unwrap_or_else(|| json!({})),
            "exception": {
                "values": [
                    {
                        "type": "Exception",
                        "value": message
                    }
                ]
            }
        });

        let req_url = format!("{}/api/{}/store/", self.url, self.project_id);
        let auth_header = format!(
            "Sentry sentry_version=7, sentry_key={}, sentry_client=zirve-rust/0.1.0",
            self.key
        );

        let res = self
            .client
            .post(&req_url)
            .header("X-Sentry-Auth", auth_header)
            .json(&payload)
            .send()
            .await?;

        if !res.status().is_success() {
            // Option to log the error but silent skip typically for SDK robustness
        }

        Ok(event_id)
    }

    pub async fn capture_exception(
        &self,
        err: &dyn std::error::Error,
        context: Option<Value>,
    ) -> Result<String, reqwest::Error> {
        self.capture_message(&err.to_string(), Some("error"), context)
            .await
    }

    pub async fn health(&self) -> bool {
        !self.dsn.is_empty()
    }
}
