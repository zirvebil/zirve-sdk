use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::Value;

#[derive(Clone)]
pub struct QualityManager {
    client: Client,
    url: String,
    token: String,
}

impl QualityManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("quality");
        let url = mod_cfg.get("url").cloned().unwrap_or_else(|| {
            "http://sonarqube-sonarqube.zirve-infra.svc.cluster.local:9000".into()
        });
        let token = mod_cfg.get("token").cloned().unwrap_or_else(|| "".into());

        Self {
            client: Client::new(),
            url: format!("{}/api", url.trim_end_matches('/')),
            token,
        }
    }

    async fn request(&self, path: &str) -> Result<Option<Value>, reqwest::Error> {
        let req_url = format!("{}/{}", self.url, path.trim_start_matches('/'));
        let mut req = self.client.get(&req_url);

        if !self.token.is_empty() {
            req = req.bearer_auth(&self.token);
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

    pub async fn get_quality_gate(&self, project_key: &str) -> Result<String, reqwest::Error> {
        let path = format!(
            "qualitygates/project_status?projectKey={}",
            urlencoding::encode(project_key)
        );
        if let Some(res) = self.request(&path).await? {
            if let Some(status) = res
                .get("projectStatus")
                .and_then(|ps| ps.get("status"))
                .and_then(|s| s.as_str())
            {
                return Ok(status.to_string());
            }
        }
        Ok("UNKNOWN".into())
    }

    pub async fn check_passed(&self, project_key: &str) -> Result<bool, reqwest::Error> {
        let st = self.get_quality_gate(project_key).await?;
        Ok(st == "OK")
    }

    pub async fn health(&self) -> bool {
        if let Ok(Some(res)) = self.request("system/health").await {
            if let Some(h) = res.get("health").and_then(|v| v.as_str()) {
                return h == "GREEN";
            }
        }
        false
    }
}
