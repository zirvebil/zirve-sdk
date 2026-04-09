use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::Value;
use std::collections::HashMap;

#[derive(Clone)]
pub struct AuthManager {
    client: Client,
    base_url: String,
    realm: String,
    cfg: ConfigManager,
}

impl AuthManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("auth");
        let base_url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://keycloak.zirve-infra.svc.cluster.local".into());
        let realm = mod_cfg
            .get("realm")
            .cloned()
            .unwrap_or_else(|| "zirve".into());

        Self {
            client: Client::new(),
            base_url: base_url.trim_end_matches('/').to_string(),
            realm,
            cfg: cfg.clone(),
        }
    }

    pub async fn verify_token(&self, token: &str) -> Result<Value, reqwest::Error> {
        let mod_cfg = self.cfg.module("auth");
        let client_id = mod_cfg
            .get("client_id")
            .cloned()
            .unwrap_or_else(|| "zirve-backend".into());
        let client_secret = mod_cfg
            .get("client_secret")
            .cloned()
            .unwrap_or_else(|| "".into());

        let url = format!(
            "{}/realms/{}/protocol/openid-connect/token/introspect",
            self.base_url, self.realm
        );
        let body_str = format!(
            "token={}&client_id={}&client_secret={}",
            urlencoding::encode(token),
            urlencoding::encode(&client_id),
            urlencoding::encode(&client_secret)
        );
        let res: reqwest::Response = self
            .client
            .post(&url)
            .header("Content-Type", "application/x-www-form-urlencoded")
            .body(body_str)
            .send()
            .await?;

        res.json::<Value>().await
    }

    pub async fn has_role(&self, token: &str, role_name: &str) -> Result<bool, reqwest::Error> {
        let claims = self.verify_token(token).await?;

        if let Some(active) = claims.get("active").and_then(|v| v.as_bool()) {
            if !active {
                return Ok(false);
            }
        } else {
            return Ok(false);
        }

        if let Some(realm_access) = claims.get("realm_access") {
            if let Some(roles) = realm_access.get("roles").and_then(|v| v.as_array()) {
                for r in roles {
                    if let Some(r_str) = r.as_str() {
                        if r_str == role_name {
                            return Ok(true);
                        }
                    }
                }
            }
        }

        Ok(false)
    }

    pub async fn get_user(&self, token: &str) -> Result<Value, reqwest::Error> {
        let url = format!(
            "{}/realms/{}/protocol/openid-connect/userinfo",
            self.base_url, self.realm
        );
        self.client
            .get(&url)
            .bearer_auth(token)
            .send()
            .await?
            .json::<Value>()
            .await
    }

    pub async fn health(&self) -> bool {
        let url = format!(
            "{}/realms/{}/.well-known/openid-configuration",
            self.base_url, self.realm
        );
        self.client
            .get(&url)
            .send()
            .await
            .map(|r: reqwest::Response| r.status().is_success())
            .unwrap_or(false)
    }
}
