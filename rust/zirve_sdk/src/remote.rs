use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::{Value, json};
use std::collections::HashMap;
use std::sync::{Arc, Mutex};

#[derive(Clone)]
pub struct RemoteManager {
    client: Client,
    url: String,
    user: String,
    pass: String,
    token: Arc<Mutex<Option<String>>>,
}

impl RemoteManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("remote");
        let url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://guacamole.zirve-infra.svc.cluster.local:8080".into());
        let user = mod_cfg
            .get("username")
            .cloned()
            .unwrap_or_else(|| "guacadmin".into());
        let pass = mod_cfg
            .get("password")
            .cloned()
            .unwrap_or_else(|| "guacadmin".into());

        Self {
            client: Client::new(),
            url: format!("{}/api", url.trim_end_matches('/')),
            user,
            pass,
            token: Arc::new(Mutex::new(None)),
        }
    }

    async fn get_token(&self) -> Result<Option<String>, reqwest::Error> {
        {
            let t_lock = self.token.lock().unwrap();
            if let Some(t) = &*t_lock {
                return Ok(Some(t.clone()));
            }
        }

        let req_url = format!("{}/tokens", self.url);
        let body_str = format!(
            "username={}&password={}",
            urlencoding::encode(&self.user),
            urlencoding::encode(&self.pass)
        );
        let res: reqwest::Response = self
            .client
            .post(&req_url)
            .header("Content-Type", "application/x-www-form-urlencoded")
            .body(body_str)
            .send()
            .await?;

        if !res.status().is_success() {
            return Ok(None);
        }

        let parsed: Value = res.json().await?;
        if let Some(t) = parsed.get("authToken").and_then(|t| t.as_str()) {
            let t_str = t.to_string();
            *self.token.lock().unwrap() = Some(t_str.clone());
            return Ok(Some(t_str));
        }

        Ok(None)
    }

    async fn request(
        &self,
        method: reqwest::Method,
        path: &str,
        body: Option<Value>,
    ) -> Result<Option<Value>, reqwest::Error> {
        let token_opt = self.get_token().await?;
        if token_opt.is_none() {
            return Ok(None);
        }
        let token = token_opt.unwrap();

        let req_url = format!(
            "{}/{}?token={}",
            self.url,
            path.trim_start_matches('/'),
            urlencoding::encode(&token)
        );
        let mut req = self.client.request(method, &req_url);

        if let Some(b) = body {
            req = req.json(&b);
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

    pub async fn create_connection(
        &self,
        source_id: &str,
        name: &str,
        protocol: &str,
        parameters: HashMap<String, String>,
    ) -> Result<Option<String>, reqwest::Error> {
        let payload = json!({
            "parentIdentifier": source_id,
            "name": name,
            "protocol": protocol,
            "parameters": parameters,
        });

        let res = self
            .request(
                reqwest::Method::POST,
                "session/data/postgresql/connections",
                Some(payload),
            )
            .await?;
        if let Some(r) = res {
            if let Some(id) = r.get("identifier").and_then(|v| v.as_str()) {
                return Ok(Some(id.to_string()));
            }
        }

        Ok(None)
    }

    pub async fn health(&self) -> bool {
        let req_url = format!("{}/tokens", self.url);
        let res: Result<reqwest::Response, reqwest::Error> = self
            .client
            .post(&req_url)
            .header("Content-Type", "application/x-www-form-urlencoded")
            .send()
            .await;
        if let Ok(r) = res {
            return r.status().as_u16() != 503 && r.status().as_u16() != 404;
        }
        false
    }
}
