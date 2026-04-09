use reqwest::Client;
use serde_json::Value;
use crate::config::ConfigManager;

#[derive(Clone)]
pub struct ClusterManager {
    client: Client,
    url: String,
    token: String,
}

impl ClusterManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("cluster");
        let url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://rancher.cattle-system.svc.cluster.local".into());
        let token = mod_cfg.get("token").cloned().unwrap_or_else(|| "".into());

        Self {
            client: Client::new(),
            url: format!("{}/v3", url.trim_end_matches('/')),
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

    pub async fn list_clusters(&self) -> Result<Option<Value>, reqwest::Error> {
        let res = self.request(reqwest::Method::GET, "clusters", None).await?;
        if let Some(v) = res {
            if let Some(data) = v.get("data") {
                return Ok(Some(data.clone()));
            }
        }
        Ok(None)
    }

    pub async fn get_cluster(&self, cluster_id: &str) -> Result<Option<Value>, reqwest::Error> {
        let path = format!("clusters/{}", urlencoding::encode(cluster_id));
        self.request(reqwest::Method::GET, &path, None).await
    }

    pub async fn is_healthy(&self, cluster_id: &str) -> Result<bool, reqwest::Error> {
        if let Some(c) = self.get_cluster(cluster_id).await? {
            if let Some(state) = c.get("state").and_then(|s| s.as_str()) {
                return Ok(state == "active");
            }
        }
        Ok(false)
    }

    pub async fn health(&self) -> bool {
        let req_url = self.url.clone();
        if let Ok(r) = self.client.get(&req_url).send().await { r.status().is_success() } else { false }
    }
}
