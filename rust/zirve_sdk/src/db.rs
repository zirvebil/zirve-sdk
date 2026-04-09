use crate::config::ConfigManager;
use sqlx::{PgPool, postgres::PgPoolOptions};
use std::time::Duration;

#[derive(Clone)]
pub struct DbManager {
    pool: PgPool,
}

impl DbManager {
    pub fn new(cfg: &ConfigManager) -> Result<Self, sqlx::Error> {
        let mod_cfg = cfg.module("db");
        let host = mod_cfg
            .get("host")
            .cloned()
            .unwrap_or_else(|| "localhost".into());
        let port = mod_cfg
            .get("port")
            .cloned()
            .unwrap_or_else(|| "5432".into());
        let dbname = mod_cfg
            .get("dbname")
            .cloned()
            .unwrap_or_else(|| "zirve".into());
        let user = mod_cfg
            .get("user")
            .cloned()
            .unwrap_or_else(|| "postgres".into());
        let password = mod_cfg
            .get("password")
            .cloned()
            .unwrap_or_else(|| "".into());

        let conn_str = format!(
            "postgres://{}:{}@{}:{}/{}",
            user, password, host, port, dbname
        );

        let pool = PgPoolOptions::new()
            .max_connections(100)
            .acquire_timeout(Duration::from_secs(5))
            .connect_lazy(&conn_str)?;

        Ok(Self { pool })
    }

    pub fn pool(&self) -> &PgPool {
        &self.pool
    }

    pub async fn health(&self) -> bool {
        sqlx::query("SELECT 1").execute(&self.pool).await.is_ok()
    }

    pub async fn close(&self) {
        self.pool.close().await;
    }
}
