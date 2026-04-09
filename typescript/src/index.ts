import { ConfigManager } from './Config/ConfigManager.js';
import { DbManager } from './Db/DbManager.js';
import { CacheManager } from './Cache/CacheManager.js';
import { AuthManager } from './Auth/AuthManager.js';
import { SecretsManager } from './Secrets/SecretsManager.js';
import { QueueManager } from './Queue/QueueManager.js';
import { StorageManager } from './Storage/StorageManager.js';
import { SearchManager } from './Search/SearchManager.js';
import { AnalyticsManager } from './Analytics/AnalyticsManager.js';
import { LogManager } from './Log/LogManager.js';
import { ErrorManager } from './Error/ErrorManager.js';
import { TraceManager } from './Trace/TraceManager.js';
import { MetricsManager } from './Metrics/MetricsManager.js';
import { BillingManager } from './Billing/BillingManager.js';
import { CrmManager } from './Crm/CrmManager.js';
import { RemoteManager } from './Remote/RemoteManager.js';
import { GatewayManager } from './Gateway/GatewayManager.js';
import { IngressManager } from './Ingress/IngressManager.js';
import { RegistryManager } from './Registry/RegistryManager.js';
import { DeployManager } from './Deploy/DeployManager.js';
import { ClusterManager } from './Cluster/ClusterManager.js';
import { QualityManager } from './Quality/QualityManager.js';
import { OnCallManager } from './OnCall/OnCallManager.js';
import { DashboardManager } from './Dashboard/DashboardManager.js';
import { TestingManager } from './Testing/TestingManager.js';

export { 
  ConfigManager, DbManager, CacheManager, AuthManager, SecretsManager, 
  QueueManager, StorageManager, SearchManager, AnalyticsManager,
  LogManager, ErrorManager, TraceManager, MetricsManager,
  BillingManager, CrmManager, RemoteManager,
  GatewayManager, IngressManager, RegistryManager, DeployManager,
  ClusterManager, QualityManager, OnCallManager, DashboardManager, TestingManager
};

/**
 * Zirve SDK — Main Entrypoint for TypeScript/Node.js.
 * 
 * Provides lazy-loaded access to all 26 infrastructure services.
 */
export class Zirve {
  private static instance: Zirve | null = null;
  public readonly config: ConfigManager;

  // Lazy loaded modules Cache
  private _db?: DbManager;
  private _cache?: CacheManager;
  private _auth?: AuthManager;
  private _secrets?: SecretsManager;
  private _queue?: QueueManager;
  private _storage?: StorageManager;
  private _search?: SearchManager;
  private _analytics?: AnalyticsManager;
  private _log?: LogManager;
  private _error?: ErrorManager;
  private _trace?: TraceManager;
  private _metrics?: MetricsManager;
  private _billing?: BillingManager;
  private _crm?: CrmManager;
  private _remote?: RemoteManager;
  private _gateway?: GatewayManager;
  private _ingress?: IngressManager;
  private _registry?: RegistryManager;
  private _deploy?: DeployManager;
  private _cluster?: ClusterManager;
  private _quality?: QualityManager;
  private _oncall?: OnCallManager;
  private _dashboard?: DashboardManager;
  private _testing?: TestingManager;

  private constructor(overrides: Record<string, any> = {}) {
    this.config = new ConfigManager(overrides);
  }

  /**
   * Initialize Zirve SDK globally. Applies Singleton pattern.
   * @param overrides Optional configuration overrides.
   */
  public static init(overrides: Record<string, any> = {}): Zirve {
    if (!Zirve.instance) {
      Zirve.instance = new Zirve(overrides);
    }
    return Zirve.instance;
  }

  /**
   * Get the global instance of Zirve SDK.
   */
  public static getInstance(): Zirve {
    if (!Zirve.instance) {
      throw new Error('Zirve SDK is not initialized. Call Zirve.init() first.');
    }
    return Zirve.instance;
  }

  /**
   * Reset the global instance. Useful for testing.
   */
  public static reset(): void {
    Zirve.instance = null;
  }

  // ---- Module Accessors (Lazy Loaded) ----

  public get db(): DbManager {
    if (!this._db) {
      this._db = new DbManager(this.config.module('db'));
    }
    return this._db;
  }

  public get cache(): CacheManager {
    if (!this._cache) {
      this._cache = new CacheManager(this.config.module('cache'));
    }
    return this._cache;
  }

  public get auth(): AuthManager {
    if (!this._auth) {
      this._auth = new AuthManager(this.config.module('auth'));
    }
    return this._auth;
  }

  public get secrets(): SecretsManager {
    if (!this._secrets) {
      this._secrets = new SecretsManager(this.config.module('secrets'));
    }
    return this._secrets;
  }

  public get queue(): QueueManager {
    if (!this._queue) {
      this._queue = new QueueManager(this.config.module('queue'));
    }
    return this._queue;
  }

  public get storage(): StorageManager {
    if (!this._storage) {
      this._storage = new StorageManager(this.config.module('storage'));
    }
    return this._storage;
  }

  public get search(): SearchManager {
    if (!this._search) {
      this._search = new SearchManager(this.config.module('search'));
    }
    return this._search;
  }

  public get analytics(): AnalyticsManager {
    if (!this._analytics) {
      this._analytics = new AnalyticsManager(this.config.module('analytics'));
    }
    return this._analytics;
  }

  public get log(): LogManager {
    if (!this._log) {
      this._log = new LogManager(this.config.module('log'));
    }
    return this._log;
  }

  public get error(): ErrorManager {
    if (!this._error) {
      this._error = new ErrorManager(this.config.module('error'));
    }
    return this._error;
  }

  public get trace(): TraceManager {
    if (!this._trace) {
      this._trace = new TraceManager(this.config.module('trace'));
    }
    return this._trace;
  }

  public get metrics(): MetricsManager {
    if (!this._metrics) {
      this._metrics = new MetricsManager(this.config.module('metrics'));
    }
    return this._metrics;
  }

  public get billing(): BillingManager {
    if (!this._billing) {
      this._billing = new BillingManager(this.config.module('billing'));
    }
    return this._billing;
  }

  public get crm(): CrmManager {
    if (!this._crm) {
      this._crm = new CrmManager(this.config.module('crm'));
    }
    return this._crm;
  }

  public get remote(): RemoteManager {
    if (!this._remote) {
      this._remote = new RemoteManager(this.config.module('remote'));
    }
    return this._remote;
  }

  public get gateway(): GatewayManager {
    if (!this._gateway) {
      this._gateway = new GatewayManager(this.config.module('gateway'));
    }
    return this._gateway;
  }

  public get ingress(): IngressManager {
    if (!this._ingress) {
      this._ingress = new IngressManager(this.config.module('ingress'));
    }
    return this._ingress;
  }

  public get registry(): RegistryManager {
    if (!this._registry) {
      this._registry = new RegistryManager(this.config.module('registry'));
    }
    return this._registry;
  }

  public get deploy(): DeployManager {
    if (!this._deploy) {
      this._deploy = new DeployManager(this.config.module('deploy'));
    }
    return this._deploy;
  }

  public get cluster(): ClusterManager {
    if (!this._cluster) {
      this._cluster = new ClusterManager(this.config.module('cluster'));
    }
    return this._cluster;
  }

  public get quality(): QualityManager {
    if (!this._quality) {
      this._quality = new QualityManager(this.config.module('quality'));
    }
    return this._quality;
  }

  public get oncall(): OnCallManager {
    if (!this._oncall) {
      this._oncall = new OnCallManager(this.config.module('oncall'));
    }
    return this._oncall;
  }

  public get dashboard(): DashboardManager {
    if (!this._dashboard) {
      this._dashboard = new DashboardManager(this.config.module('dashboard'));
    }
    return this._dashboard;
  }

  public get testing(): TestingManager {
    if (!this._testing) {
      this._testing = new TestingManager(this.config.module('testing'));
    }
    return this._testing;
  }

  /**
   * Check the health of the basic initialized modules.
   */
  public async health(): Promise<Record<string, any>> {
    const status: Record<string, any> = {};

    status['config'] = await this.config.health() ? 'healthy' : 'unhealthy';
    
    if (this._db) status['db'] = await this._db.health() ? 'healthy' : 'unhealthy';
    if (this._cache) status['cache'] = await this._cache.health() ? 'healthy' : 'unhealthy';
    if (this._auth) status['auth'] = await this._auth.health() ? 'healthy' : 'unhealthy';
    if (this._secrets) status['secrets'] = await this._secrets.health() ? 'healthy' : 'unhealthy';
    if (this._queue) status['queue'] = await this._queue.health() ? 'healthy' : 'unhealthy';
    if (this._storage) status['storage'] = await this._storage.health() ? 'healthy' : 'unhealthy';
    if (this._search) status['search'] = await this._search.health() ? 'healthy' : 'unhealthy';
    if (this._analytics) status['analytics'] = await this._analytics.health() ? 'healthy' : 'unhealthy';
    if (this._log) status['log'] = await this._log.health() ? 'healthy' : 'unhealthy';
    if (this._error) status['error'] = await this._error.health() ? 'healthy' : 'unhealthy';
    if (this._trace) status['trace'] = await this._trace.health() ? 'healthy' : 'unhealthy';
    if (this._metrics) status['metrics'] = await this._metrics.health() ? 'healthy' : 'unhealthy';
    if (this._billing) status['billing'] = await this._billing.health() ? 'healthy' : 'unhealthy';
    if (this._crm) status['crm'] = await this._crm.health() ? 'healthy' : 'unhealthy';
    if (this._remote) status['remote'] = await this._remote.health() ? 'healthy' : 'unhealthy';
    if (this._gateway) status['gateway'] = await this._gateway.health() ? 'healthy' : 'unhealthy';
    if (this._ingress) status['ingress'] = await this._ingress.health() ? 'healthy' : 'unhealthy';
    if (this._registry) status['registry'] = await this._registry.health() ? 'healthy' : 'unhealthy';
    if (this._deploy) status['deploy'] = await this._deploy.health() ? 'healthy' : 'unhealthy';
    if (this._cluster) status['cluster'] = await this._cluster.health() ? 'healthy' : 'unhealthy';
    if (this._quality) status['quality'] = await this._quality.health() ? 'healthy' : 'unhealthy';
    if (this._oncall) status['oncall'] = await this._oncall.health() ? 'healthy' : 'unhealthy';
    if (this._dashboard) status['dashboard'] = await this._dashboard.health() ? 'healthy' : 'unhealthy';
    if (this._testing) status['testing'] = await this._testing.health() ? 'healthy' : 'unhealthy';

    return status;
  }

  /**
   * Safely close all active connections.
   */
  public async close(): Promise<void> {
    if (this._db) await this._db.close();
    if (this._cache) await this._cache.close();
    if (this._log) await this._log.close();
  }
}
