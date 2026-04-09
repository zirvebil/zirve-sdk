use reqwest::Client;
use serde_json::Value;
use crate::config::ConfigManager;

#[derive(Clone)]
pub struct RegistryManager {
    client: Client,
    url: String,
    user: String,
    pass: String,
}

impl RegistryManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("registry");
        let url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://harbor-core.zirve-infra.svc.cluster.local".into());
        let user = mod_cfg
            .get("username")
            .cloned()
            .unwrap_or_else(|| "admin".into());
        let pass = mod_cfg
            .get("password")
            .cloned()
            .unwrap_or_else(|| "Harbor12345".into());

        Self {
            client: Client::new(),
            url: format!("{}/api/v2.0", url.trim_end_matches('/')),
            user,
            pass,
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

        if !self.pass.is_empty() {
            req = req.basic_auth(&self.user, Some(&self.pass));
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

    pub async fn list_projects(&self) -> Result<Option<Value>, reqwest::Error> {
        self.request(reqwest::Method::GET, "projects", None).await
    }

    pub async fn list_images(&self, project_name: &str) -> Result<Option<Value>, reqwest::Error> {
        self.request(
            reqwest::Method::GET,
            &format!(
                "projects/{}/repositories",
                urlencoding::encode(project_name)
            ),
            None,
        )
        .await
    }

    pub async fn scan_image(
        &self,
        project_name: &str,
        repository_name: &str,
        reference: &str,
    ) -> Result<bool, reqwest::Error> {
        let path = format!(
            "projects/{}/repositories/{}/artifacts/{}/scan",
            urlencoding::encode(project_name),
            urlencoding::encode(repository_name),
            urlencoding::encode(reference)
        );
        let res = self.request(reqwest::Method::POST, &path, None).await;
        Ok(res.is_ok())
    }

    pub async fn scan_report(
        &self,
        project_name: &str,
        repository_name: &str,
        reference: &str,
    ) -> Result<Option<Value>, reqwest::Error> {
        let path = format!(
            "projects/{}/repositories/{}/artifacts/{}/additions/vulnerabilities",
            urlencoding::encode(project_name),
            urlencoding::encode(repository_name),
            urlencoding::encode(reference)
        );
        self.request(reqwest::Method::GET, &path, None).await
    }

    pub async fn health(&self) -> bool {
        let req_url = format!("{}/health", self.url);
        if let Ok(r) = self.client.get(&req_url).send().await { r.status().is_success() } else { false }
    }
}
