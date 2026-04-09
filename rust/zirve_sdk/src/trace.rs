use crate::config::ConfigManager;
use chrono::{DateTime, Utc};
use reqwest::Client;
use serde_json::{Value, json};
use std::env;
use std::time::Duration;
use uuid::Uuid;

#[derive(Clone)]
pub struct TraceManager {
    client: Client,
    endpoint: String,
    app_name: String,
}

impl TraceManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("trace");
        let endpoint = mod_cfg.get("endpoint").cloned().unwrap_or_else(|| {
            "http://opentelemetry-collector.zirve-infra.svc.cluster.local:4318".into()
        });
        let app_name = env::var("APP_NAME").unwrap_or_else(|_| "zirve-rust".into());

        Self {
            client: Client::builder()
                .timeout(Duration::from_secs(5))
                .build()
                .unwrap(),
            endpoint: endpoint.trim_end_matches('/').to_string(),
            app_name,
        }
    }

    pub async fn send_span(
        &self,
        name: &str,
        start_time: DateTime<Utc>,
        end_time: DateTime<Utc>,
        trace_id: &str,
        parent_id: Option<&str>,
        _attributes: Option<Value>,
    ) -> Result<String, reqwest::Error> {
        let span_id = Uuid::new_v4().simple().to_string()[..16].to_string();
        let start_nano = start_time.timestamp_nanos_opt().unwrap_or(0).to_string();
        let end_nano = end_time.timestamp_nanos_opt().unwrap_or(0).to_string();
        let env_name = env::var("APP_ENV").unwrap_or_else(|_| "production".into());

        let pid = parent_id.unwrap_or("");

        let payload = json!({
            "resourceSpans": [
                {
                    "resource": {
                        "attributes": [
                            { "key": "service.name", "value": { "stringValue": self.app_name } },
                            { "key": "environment", "value": { "stringValue": env_name } }
                        ]
                    },
                    "scopeSpans": [
                        {
                            "spans": [
                                {
                                    "traceId": trace_id,
                                    "spanId": span_id,
                                    "parentSpanId": pid,
                                    "name": name,
                                    "kind": 1, // SPAN_KIND_INTERNAL
                                    "startTimeUnixNano": start_nano,
                                    "endTimeUnixNano": end_nano,
                                    "status": { "code": 1 } // STATUS_CODE_OK
                                }
                            ]
                        }
                    ]
                }
            ]
        });

        let url = format!("{}/v1/traces", self.endpoint);
        let res: reqwest::Response = self.client.post(&url).json(&payload).send().await?;

        if !res.status().is_success() {
            // Logging ignored
        }

        Ok(span_id)
    }

    pub async fn health(&self) -> bool {
        !self.endpoint.is_empty()
    }
}
