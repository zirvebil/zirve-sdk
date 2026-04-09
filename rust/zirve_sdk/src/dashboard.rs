use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::{Value, json};

#[derive(Clone)]
pub struct DashboardManager {
    client: Client,
    url: String,
    token: String,
}

impl DashboardManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("dashboard");
        let url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://grafana.zirve-infra.svc.cluster.local".into());
        let token = mod_cfg.get("token").cloned().unwrap_or_else(|| "".into());

        Self {
            client: Client::new(),
            url: format!("{}/api", url.trim_end_matches('/')),
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

    pub async fn search_dashboards(&self, query: &str) -> Result<Option<Value>, reqwest::Error> {
        let path = format!("search?query={}&type=dash-db", urlencoding::encode(query));
        self.request(reqwest::Method::GET, &path, None).await
    }

    pub async fn get_dashboard(&self, uid: &str) -> Result<Option<Value>, reqwest::Error> {
        let path = format!("dashboards/uid/{}", urlencoding::encode(uid));
        let res = self.request(reqwest::Method::GET, &path, None).await?;

        if let Some(v) = res {
            if let Some(dash) = v.get("dashboard") {
                return Ok(Some(dash.clone()));
            }
        }
        Ok(None)
    }

    pub async fn import_dashboard(
        &self,
        dashboard_json: Value,
        folder_id: i32,
        overwrite: bool,
    ) -> Result<Option<Value>, reqwest::Error> {
        let payload = json!({
            "dashboard": dashboard_json,
            "folderId": folder_id,
            "overwrite": overwrite,
        });

        self.request(reqwest::Method::POST, "dashboards/db", Some(payload))
            .await
    }

    pub async fn list_datasources(&self) -> Result<Option<Value>, reqwest::Error> {
        self.request(reqwest::Method::GET, "datasources", None)
            .await
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
