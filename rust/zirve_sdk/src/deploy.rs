use reqwest::Client;
use serde_json::Value;
use crate::config::ConfigManager;

#[derive(Clone)]
pub struct DeployManager {
    client: Client,
    url: String,
    token: String,
}

impl DeployManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("deploy");
        let url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://argocd-server.argocd.svc.cluster.local".into());
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

    pub async fn list_applications(&self) -> Result<Option<Value>, reqwest::Error> {
        let res = self
            .request(reqwest::Method::GET, "applications", None)
            .await?;
        if let Some(v) = res {
            if let Some(items) = v.get("items") {
                return Ok(Some(items.clone()));
            }
        }
        Ok(None)
    }

    pub async fn sync(&self, application_name: &str) -> Result<Option<Value>, reqwest::Error> {
        let path = format!(
            "applications/{}/sync",
            urlencoding::encode(application_name)
        );
        self.request(reqwest::Method::POST, &path, None).await
    }

    pub async fn status(&self, application_name: &str) -> Result<Option<Value>, reqwest::Error> {
        let path = format!("applications/{}", urlencoding::encode(application_name));
        self.request(reqwest::Method::GET, &path, None).await
    }

    pub async fn health(&self) -> bool {
        let req_url = format!("{}/version", self.url);
        if let Ok(r) = self.client.get(&req_url).send().await { r.status().is_success() } else { false }
    }
}
