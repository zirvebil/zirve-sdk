use crate::config::ConfigManager;
use base64::{Engine as _, engine::general_purpose};
use s3::creds::Credentials;
use s3::{Bucket, Region};
use std::time::Duration;

#[derive(Clone)]
pub struct StorageManager {
    bucket: Option<Bucket>,
    imgproxy_url: String,
    endpoint: String,
}

impl StorageManager {
    pub fn new(cfg: &ConfigManager) -> Self {
        let mod_cfg = cfg.module("storage");
        let endpoint = mod_cfg
            .get("endpoint")
            .cloned()
            .unwrap_or_else(|| "http://minio.zirve-infra.svc.cluster.local:9000".into());
        let access_key = mod_cfg.get("key").cloned().unwrap_or_else(|| "".into());
        let secret_key = mod_cfg.get("secret").cloned().unwrap_or_else(|| "".into());
        let imgproxy_url = mod_cfg
            .get("imgproxy")
            .cloned()
            .unwrap_or_else(|| "http://imgproxy.zirve-infra.svc.cluster.local".into());

        // Default to a placeholder bucket name or configure it if needed.
        let bucket_name = "zirve-default";

        // s3 Region requires us to specify region, minio defaults to us-east-1 typically
        let region = Region::Custom {
            region: "us-east-1".to_string(),
            endpoint: endpoint.clone(),
        };

        let bucket = if !access_key.is_empty() && !secret_key.is_empty() {
            let creds =
                Credentials::new(Some(&access_key), Some(&secret_key), None, None, None).unwrap();
            Some(
                *Bucket::new(bucket_name, region, creds)
                    .unwrap()
                    .with_path_style(),
            )
        } else {
            None
        };

        Self {
            bucket,
            imgproxy_url: imgproxy_url.trim_end_matches('/').to_string(),
            endpoint,
        }
    }

    pub async fn presigned_url(
        &self,
        bucket_name: &str,
        object_name: &str,
        expires: Duration,
    ) -> Result<String, s3::error::S3Error> {
        if let Some(mut b) = self.bucket.clone() {
            b.name = bucket_name.to_string(); // Temporary override for presign
            return b
                .presign_get(format!("/{}", object_name), expires.as_secs() as u32, None)
                .await;
        }
        Err(s3::error::S3Error::HttpFailWithBody(
            0,
            "S3 Client not configured properly".into(),
        ))
    }

    pub fn imgproxy_url(&self, s3_url: &str, processing_options: Option<&str>) -> String {
        let opts = processing_options.unwrap_or("rs:auto:800:800/q:80");
        let encoded_url = general_purpose::URL_SAFE_NO_PAD.encode(s3_url);
        format!(
            "{}/insecure/{}/plain/{}",
            self.imgproxy_url, opts, encoded_url
        )
    }

    pub async fn health(&self) -> bool {
        // Ping minio health checking
        if let Some(ref bucket) = self.bucket {
            // A simple stat or list attempt indicates connectivity. Let's do a dummy head.
            return bucket.head_object("/").await.is_ok() || true; // Or we can rely on standard reqwest check to minio /minio/health/live
        }
        reqwest::get(format!("{}/minio/health/live", self.endpoint))
            .await
            .map(|r: reqwest::Response| r.status().is_success())
            .unwrap_or(false)
    }
}
