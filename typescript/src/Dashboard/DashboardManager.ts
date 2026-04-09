/**
 * Zirve Dashboard Manager — Grafana
 *
 * Connects to Grafana API for provisioning dashboards and data sources programmatically.
 */
export class DashboardManager {
  private config: Record<string, any>;
  private url: string;
  private token: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://grafana.zirve-infra.svc.cluster.local').replace(/\/$/, '') + '/api';
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
      throw new Error(`Grafana API Error [${response.status}]`);
    }

    if (response.status === 204 || response.headers.get('content-length') === '0') return true;
    return await response.json();
  }

  public async searchDashboards(query: string = ''): Promise<any[]> {
    const result = await this.request('GET', `search?query=${encodeURIComponent(query)}&type=dash-db`);
    return result || [];
  }

  public async getDashboard(uid: string): Promise<any | null> {
    const result = await this.request('GET', `dashboards/uid/${encodeURIComponent(uid)}`);
    return result?.dashboard || null;
  }

  public async importDashboard(dashboardJson: any, folderId: number = 0, overwrite: boolean = true): Promise<any> {
    return await this.request('POST', 'dashboards/db', {
      dashboard: dashboardJson,
      folderId,
      overwrite
    });
  }

  public async listDataSources(): Promise<any[]> {
    return await this.request('GET', 'datasources') || [];
  }

  public async health(): Promise<boolean> {
    try {
      const response = await fetch(`${this.url}/health`);
      return response.ok;
    } catch {
      return false;
    }
  }
}
