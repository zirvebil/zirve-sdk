use crate::config::ConfigManager;
use reqwest::Client;
use serde_json::{Value, json};
use std::sync::{Arc, Mutex};

#[derive(Clone)]
pub struct CrmManager {
    client: Client,
    url: String,
    db: String,
    user: String,
    password: String,
    uid: Arc<Mutex<Option<i32>>>,
    rpc_id: Arc<Mutex<i32>>,
}

impl CrmManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("crm");
        let url = mod_cfg
            .get("url")
            .cloned()
            .unwrap_or_else(|| "http://odoo.zirve-infra.svc.cluster.local:8069".into());
        let db = mod_cfg
            .get("database")
            .cloned()
            .unwrap_or_else(|| "zirve".into());
        let user = mod_cfg
            .get("username")
            .cloned()
            .unwrap_or_else(|| "admin".into());
        let password = mod_cfg
            .get("password")
            .cloned()
            .unwrap_or_else(|| "admin".into());

        Self {
            client: Client::new(),
            url: url.trim_end_matches('/').to_string(),
            db,
            user,
            password,
            uid: Arc::new(Mutex::new(None)),
            rpc_id: Arc::new(Mutex::new(0)),
        }
    }

    async fn rpc(&self, method: &str, params: Value) -> Result<Option<Value>, reqwest::Error> {
        let req_id = {
            let mut id = self.rpc_id.lock().unwrap();
            *id += 1;
            *id
        };

        let payload = json!({
            "jsonrpc": "2.0",
            "method": method,
            "params": params,
            "id": req_id,
        });

        let req_url = format!("{}/jsonrpc", self.url);
        let res: reqwest::Response = self.client.post(&req_url).json(&payload).send().await?;

        let parsed: Value = res.json().await?;

        if let Some(err) = parsed.get("error") {
            if !err.is_null() {
                // Log RPC error here if needed.
                return Ok(None);
            }
        }

        Ok(parsed.get("result").cloned())
    }

    pub async fn authenticate(&self) -> Result<Option<i32>, reqwest::Error> {
        {
            let uid_lock = self.uid.lock().unwrap();
            if let Some(u) = *uid_lock {
                return Ok(Some(u));
            }
        }

        let params = json!({
            "service": "common",
            "method": "authenticate",
            "args": [self.db, self.user, self.password, {}]
        });

        let res = self.rpc("call", params).await?;

        if let Some(v) = res {
            if let Some(u) = v.as_i64() {
                let u_i32 = u as i32;
                *self.uid.lock().unwrap() = Some(u_i32);
                return Ok(Some(u_i32));
            }
        }

        Ok(None)
    }

    pub async fn execute_kw(
        &self,
        model: &str,
        method: &str,
        args: Vec<Value>,
        kwargs: Option<Value>,
    ) -> Result<Option<Value>, reqwest::Error> {
        let uid = match self.authenticate().await? {
            Some(u) => u,
            None => return Ok(None),
        };

        let kw = kwargs.unwrap_or_else(|| json!({}));

        let params = json!({
            "service": "object",
            "method": "execute_kw",
            "args": [self.db, uid, self.password, model, method, args, kw]
        });

        self.rpc("call", params).await
    }

    pub async fn health(&self) -> bool {
        let params = json!({
            "service": "common",
            "method": "version",
            "args": []
        });

        self.rpc("call", params).await.is_ok()
    }
}
