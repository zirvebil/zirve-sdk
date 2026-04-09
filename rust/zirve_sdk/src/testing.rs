use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::Value;

#[derive(Clone)]
pub struct TestingManager {
    client: Client,
    url: String,
}

impl TestingManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("testing");
        let url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://keploy.zirve-infra.svc.cluster.local:6789".into());

        Self {
            client: Client::new(),
            url: format!("{}/api", url.trim_end_matches('/')),
        }
    }

    async fn request(
        &self,
        method: reqwest::Method,
        path: &str,
    ) -> Result<Option<Value>, reqwest::Error> {
        let req_url = format!("{}/{}", self.url, path.trim_start_matches('/'));
        let res: reqwest::Response = self.client.request(method, &req_url).send().await?;

        if res.status().as_u16() == 404
            || res.status().as_u16() == 204
            || res.content_length() == Some(0)
        {
            return Ok(None);
        }

        Ok(Some(res.json::<Value>().await?))
    }

    pub async fn list_test_sets(&self, app: &str) -> Result<Option<Value>, reqwest::Error> {
        let path = format!("test-sets?app={}", urlencoding::encode(app));
        self.request(reqwest::Method::GET, &path).await
    }

    pub async fn get_test_run(&self, id: &str) -> Result<Option<Value>, reqwest::Error> {
        let path = format!("test-run/{}", urlencoding::encode(id));
        self.request(reqwest::Method::GET, &path).await
    }

    pub async fn health(&self) -> bool {
        let req_url = format!("{}/healthz", self.url);
        self.client
            .get(&req_url)
            .send()
            .await
            .map(|r: reqwest::Response| r.status().is_success())
            .unwrap_or(false)
    }
}
