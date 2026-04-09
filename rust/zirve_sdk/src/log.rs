use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::{Value, json};
use std::env;
use std::sync::{Arc, Mutex};
use std::time::{Duration, SystemTime, UNIX_EPOCH};
use tokio::time::interval;

#[derive(Clone)]
pub struct LogManager {
    client: Client,
    url: String,
    app_name: String,
    env_name: String,
    buffer: Arc<Mutex<Vec<Value>>>,
}

impl LogManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("log");
        let url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://loki.zirve-infra.svc.cluster.local:3100".into());
        let app_name = env::var("APP_NAME").unwrap_or_else(|_| "zirve-rust".into());
        let env_name = env::var("APP_ENV").unwrap_or_else(|_| "production".into());

        let manager = Self {
            client: Client::builder()
                .timeout(Duration::from_secs(5))
                .build()
                .unwrap(),
            url: url.trim_end_matches('/').to_string(),
            app_name,
            env_name,
            buffer: Arc::new(Mutex::new(Vec::new())),
        };

        // Start flusher task
        let flusher = manager.clone();
        tokio::spawn(async move {
            let mut int = interval(Duration::from_secs(3));
            loop {
                int.tick().await;
                let _ = flusher.flush().await;
            }
        });

        manager
    }

    pub async fn flush(&self) -> Result<(), reqwest::Error> {
        let entries: Vec<Value> = {
            let mut buf = self.buffer.lock().unwrap();
            if buf.is_empty() {
                return Ok(());
            }
            std::mem::take(&mut *buf)
        };

        let payload = json!({
            "streams": [
                {
                    "stream": {
                        "app": self.app_name,
                        "env": self.env_name
                    },
                    "values": entries.into_iter().map(|e| {
                        let ts = e.get("timestamp").and_then(|v| v.as_str()).unwrap_or("").to_string();
                        let mut msg = e.clone();
                        msg.as_object_mut().unwrap().remove("timestamp");
                        vec![json!(ts), json!(msg.to_string())]
                    }).collect::<Vec<_>>()
                }
            ]
        });

        let url = format!("{}/loki/api/v1/push", self.url);
        self.client.post(&url).json(&payload).send().await?;

        Ok(())
    }

    fn write(&self, level: &str, message: &str, context: Option<Value>) {
        let ts = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos()
            .to_string();

        let entry = json!({
            "timestamp": ts,
            "level": level,
            "message": message,
            "context": context.unwrap_or_else(|| json!({}))
        });

        self.buffer.lock().unwrap().push(entry);
    }

    pub fn info(&self, message: &str, context: Option<Value>) {
        self.write("info", message, context);
    }

    pub fn error(&self, message: &str, context: Option<Value>) {
        self.write("error", message, context);
    }

    pub fn warn(&self, message: &str, context: Option<Value>) {
        self.write("warn", message, context);
    }

    pub fn debug(&self, message: &str, context: Option<Value>) {
        self.write("debug", message, context);
    }

    pub async fn health(&self) -> bool {
        let url = format!("{}/ready", self.url);
        self.client
            .get(&url)
            .send()
            .await
            .map(|r: reqwest::Response| r.status().is_success())
            .unwrap_or(false)
    }
}
