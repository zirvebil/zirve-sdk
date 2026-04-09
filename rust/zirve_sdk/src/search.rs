use reqwest::Client;
use serde_json::Value;
use crate::config::ConfigManager;

#[derive(Clone)]
pub struct SearchManager {
    client: Client,
    base_url: String,
    user: String,
    pass: String,
}

impl SearchManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("search");
        let host = mod_cfg
            .get("host")
            .cloned()
            .unwrap_or_else(|| "elasticsearch-master.zirve-infra.svc.cluster.local".into());
        let port = mod_cfg
            .get("port")
            .cloned()
            .unwrap_or_else(|| "9200".into());
        let user = mod_cfg
            .get("username")
            .cloned()
            .unwrap_or_else(|| "elastic".into());
        let pass = mod_cfg
            .get("password")
            .cloned()
            .unwrap_or_else(|| "".into());

        Self {
            client: Client::new(),
            base_url: format!("http://{}:{}", host, port),
            user,
            pass,
        }
    }

    pub async fn index<T: serde::Serialize>(
        &self,
        index: &str,
        id: Option<&str>,
        document: &T,
    ) -> Result<Value, reqwest::Error> {
        let url = if let Some(doc_id) = id {
            format!("{}/{}/_doc/{}", self.base_url, index, doc_id)
        } else {
            format!("{}/{}/_doc", self.base_url, index)
        };

        let mut req = if id.is_some() {
            self.client.put(&url)
        } else {
            self.client.post(&url)
        };

        req = req.json(document);
        if !self.pass.is_empty() {
            req = req.basic_auth(&self.user, Some(&self.pass));
        }

        let res: reqwest::Response = req.send().await?;
        res.json::<Value>().await
    }

    pub async fn search<T: serde::Serialize>(
        &self,
        index: &str,
        query: &T,
    ) -> Result<Value, reqwest::Error> {
        let url = format!("{}/{}/_search", self.base_url, index);

        let mut req = self.client.post(&url).json(query);
        if !self.pass.is_empty() {
            req = req.basic_auth(&self.user, Some(&self.pass));
        }

        let res: reqwest::Response = req.send().await?;
        res.json::<Value>().await
    }

    pub async fn health(&self) -> bool {
        let url = format!("{}/_cluster/health", self.base_url);
        let mut req = self.client.get(&url);
        if !self.pass.is_empty() {
            req = req.basic_auth(&self.user, Some(&self.pass));
        }

        if let Ok(r) = req.send().await { r.status().is_success() } else { false }
    }
}
