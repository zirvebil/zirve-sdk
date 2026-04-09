pub mod analytics;
pub mod auth;
pub mod billing;
pub mod cache;
pub mod cluster;
pub mod config;
pub mod crm;
pub mod dashboard;
pub mod db;
pub mod deploy;
pub mod error;
pub mod gateway;
pub mod ingress;
pub mod log;
pub mod metrics;
pub mod oncall;
pub mod quality;
pub mod queue;
pub mod registry;
pub mod remote;
pub mod search;
pub mod secrets;
pub mod storage;
pub mod testing;
pub mod trace;

use std::sync::Arc;
use tokio::sync::RwLock;

// Ensure all managers implement clone or use Arc internally if not naturally cheap-to-clone
// Most of them use reqwest::Client which is an Arc internally.

#[derive(Clone)]
pub struct ZirveClient {
    pub config: config::ConfigManager,
    pub db: db::DbManager,
    pub cache: cache::CacheManager,
    pub auth: auth::AuthManager,
    pub secrets: secrets::SecretsManager,
    pub queue: queue::QueueManager,
    pub storage: storage::StorageManager,
    pub search: search::SearchManager,
    pub analytics: analytics::AnalyticsManager,

    pub log: log::LogManager,
    pub error: error::ErrorManager,
    pub trace: trace::TraceManager,
    pub metrics: metrics::MetricsManager,

    pub billing: billing::BillingManager,
    pub crm: crm::CrmManager,
    pub remote: remote::RemoteManager,

    pub gateway: gateway::GatewayManager,
    pub ingress: ingress::IngressManager,
    pub registry: registry::RegistryManager,
    pub deploy: deploy::DeployManager,
    pub cluster: cluster::ClusterManager,
    pub quality: quality::QualityManager,
    pub oncall: oncall::OnCallManager,
    pub dashboard: dashboard::DashboardManager,
    pub testing: testing::TestingManager,
}

impl ZirveClient {
    pub async fn new() -> Result<Self, Box<dyn std::error::Error>> {
        let cfg = config::ConfigManager::new();

        let db = db::DbManager::new(&cfg)?;
        let cache = cache::CacheManager::new(&cfg)?;
        let auth = auth::AuthManager::new(&cfg);
        let secrets = secrets::SecretsManager::new(&cfg);
        let queue = queue::QueueManager::new(&cfg);
        let storage = storage::StorageManager::new(&cfg);
        let search = search::SearchManager::new(&cfg);
        let analytics = analytics::AnalyticsManager::new(&cfg);

        let log = log::LogManager::new(&cfg);
        let error = error::ErrorManager::new(&cfg);
        let trace = trace::TraceManager::new(&cfg);
        let metrics = metrics::MetricsManager::new(&cfg);

        let billing = billing::BillingManager::new(&cfg);
        let crm = crm::CrmManager::new(&cfg);
        let remote = remote::RemoteManager::new(&cfg);

        let gateway = gateway::GatewayManager::new(&cfg);
        let ingress = ingress::IngressManager::new(&cfg);
        let registry = registry::RegistryManager::new(&cfg);
        let deploy = deploy::DeployManager::new(&cfg);
        let cluster = cluster::ClusterManager::new(&cfg);
        let quality = quality::QualityManager::new(&cfg);
        let oncall = oncall::OnCallManager::new(&cfg);
        let dashboard = dashboard::DashboardManager::new(&cfg);
        let testing = testing::TestingManager::new(&cfg);

        Ok(Self {
            config: cfg,
            db,
            cache,
            auth,
            secrets,
            queue,
            storage,
            search,
            analytics,
            log,
            error,
            trace,
            metrics,
            billing,
            crm,
            remote,
            gateway,
            ingress,
            registry,
            deploy,
            cluster,
            quality,
            oncall,
            dashboard,
            testing,
        })
    }

    pub async fn close(&self) {
        self.db.close().await;
        let _ = self.log.flush().await; // attempt a last flush
    }
}
