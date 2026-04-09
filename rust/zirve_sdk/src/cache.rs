use crate::config::ConfigManager;
use redis::{AsyncCommands, Client};
use std::time::Duration;

#[derive(Clone)]
pub struct CacheManager {
    client: Client,
    prefix: String,
}

impl CacheManager {
    pub fn new(cfg: &ConfigManager) -> Result<Self, redis::RedisError> {
        let mod_cfg = cfg.module("cache");
        let host = mod_cfg
            .get("host")
            .cloned()
            .unwrap_or_else(|| "localhost".into());
        let port = mod_cfg
            .get("port")
            .cloned()
            .unwrap_or_else(|| "6379".into());
        let password = mod_cfg
            .get("password")
            .cloned()
            .unwrap_or_else(|| "".into());
        let prefix = mod_cfg
            .get("prefix")
            .cloned()
            .unwrap_or_else(|| "zirve:".into());

        let conn_str = if password.is_empty() {
            format!("redis://{}:{}", host, port)
        } else {
            format!("redis://:{}@{}:{}", password, host, port)
        };

        let client = Client::open(conn_str)?;

        Ok(Self { client, prefix })
    }

    fn key(&self, k: &str) -> String {
        format!("{}{}", self.prefix, k)
    }

    pub async fn set(
        &self,
        key: &str,
        value: &str,
        ttl: Duration,
    ) -> Result<(), redis::RedisError> {
        let mut con: redis::aio::MultiplexedConnection =
            self.client.get_multiplexed_async_connection().await?;
        con.set_ex(self.key(key), value, ttl.as_secs()).await
    }

    pub async fn get(&self, key: &str) -> Result<String, redis::RedisError> {
        let mut con: redis::aio::MultiplexedConnection =
            self.client.get_multiplexed_async_connection().await?;
        con.get(self.key(key)).await
    }

    pub async fn delete(&self, key: &str) -> Result<(), redis::RedisError> {
        let mut con: redis::aio::MultiplexedConnection =
            self.client.get_multiplexed_async_connection().await?;
        con.del(self.key(key)).await
    }

    pub async fn lock(
        &self,
        key: &str,
        owner_id: &str,
        ttl: Duration,
    ) -> Result<bool, redis::RedisError> {
        let mut con: redis::aio::MultiplexedConnection =
            self.client.get_multiplexed_async_connection().await?;
        let res: Option<String> = redis::cmd("SET")
            .arg(self.key(&format!("lock:{}", key)))
            .arg(owner_id)
            .arg("NX")
            .arg("PX")
            .arg(ttl.as_millis() as u64)
            .query_async(&mut con)
            .await?;
        Ok(res == Some("OK".into()))
    }

    pub async fn unlock(&self, key: &str, owner_id: &str) -> Result<bool, redis::RedisError> {
        let script = r#"
            if redis.call("get", KEYS[1]) == ARGV[1] then
                return redis.call("del", KEYS[1])
            else
                return 0
            end
        "#;
        let mut con: redis::aio::MultiplexedConnection =
            self.client.get_multiplexed_async_connection().await?;
        let res: i32 = redis::Script::new(script)
            .key(self.key(&format!("lock:{}", key)))
            .arg(owner_id)
            .invoke_async(&mut con)
            .await?;
        Ok(res == 1)
    }

    pub async fn health(&self) -> bool {
        if let Ok(mut con) = self.client.get_multiplexed_async_connection().await {
            let res: Result<String, _> = redis::cmd("PING").query_async(&mut con).await;
            return res.is_ok();
        }
        false
    }
}
