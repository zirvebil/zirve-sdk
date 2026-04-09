use crate::config::ConfigManager;
use reqwest::Client;
use std::env;
use std::time::Duration;

#[derive(Clone)]
pub struct Metric {
    pub name: String,
    pub value: f64,
    pub typ: String,
    pub help: String,
}

#[derive(Clone)]
pub struct MetricsManager {
    client: Client,
    url: String,
    job_name: String,
}

impl MetricsManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("metrics");
        let url = mod_cfg.get("url").cloned().unwrap_or_else(|| {
            "http://prometheus-server.zirve-infra.svc.cluster.local:9090".into()
        });
        let job_name = env::var("APP_NAME").unwrap_or_else(|_| "zirve-rust".into());

        Self {
            client: Client::builder()
                .timeout(Duration::from_secs(5))
                .build()
                .unwrap(),
            url: url.trim_end_matches('/').to_string(),
            job_name,
        }
    }

    pub async fn push(&self, metrics: Vec<Metric>) -> Result<bool, reqwest::Error> {
        if metrics.is_empty() {
            return Ok(true);
        }

        let mut buf = String::new();
        for m in metrics {
            let typ = if m.typ.is_empty() { "gauge" } else { &m.typ };
            let help = if m.help.is_empty() {
                "Zirve SDK Metric"
            } else {
                &m.help
            };

            buf.push_str(&format!("# HELP {} {}\n", m.name, help));
            buf.push_str(&format!("# TYPE {} {}\n", m.name, typ));
            buf.push_str(&format!("{} {}\n", m.name, m.value));
        }

        let url = format!(
            "{}/metrics/job/{}",
            self.url,
            urlencoding::encode(&self.job_name)
        );
        let res = self
            .client
            .post(&url)
            .header("Content-Type", "text/plain")
            .body(buf)
            .send()
            .await?;

        Ok(res.status().is_success())
    }

    pub async fn health(&self) -> bool {
        !self.url.is_empty()
    }
}
