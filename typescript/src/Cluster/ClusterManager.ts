/**
 * Zirve Cluster Manager — Rancher
 *
 * Interacts with Rancher API to list and query cluster statuses.
 */
export class ClusterManager {
  private config: Record<string, any>;
  private url: string;
  private token: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://rancher.cattle-system.svc.cluster.local').replace(/\/$/, '') + '/v3';
    this.token = config.token || '';
  }

  private async request(method: string, path: string, body?: any): Promise<any> {
    const options: RequestInit = {
      method,
      headers: {
        'Authorization': `Bearer ${this.token}`,
        'Content-Type': 'application/json'
      },
      body: body ? JSON.stringify(body) : undefined
    };

    const response = await fetch(`${this.url}/${path.replace(/^\/+/, '')}`, options);
    
    if (!response.ok) {
      if (response.status === 404) return null;
      throw new Error(`Rancher API Error [${response.status}]: ${response.statusText}`);
    }

    if (response.status === 204 || response.headers.get('content-length') === '0') return true;
    return await response.json();
  }

  public async listClusters(): Promise<any[]> {
    const result = await this.request('GET', 'clusters');
    return result?.data || [];
  }

  public async getCluster(clusterId: string): Promise<any | null> {
    return await this.request('GET', `clusters/${clusterId}`);
  }

  public async isHealthy(clusterId: string): Promise<boolean> {
    const cluster = await this.getCluster(clusterId);
    return cluster && cluster.state === 'active';
  }

  public async health(): Promise<boolean> {
    // Rancher v3 API root returns configuration state
    try {
      const response = await fetch(`${this.url}`);
      return response.ok;
    } catch {
      return false;
    }
  }
}
