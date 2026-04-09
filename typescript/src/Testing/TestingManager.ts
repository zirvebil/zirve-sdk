/**
 * Zirve Testing Manager — Keploy
 *
 * Interfaces with Keploy UI/API endpoint for tracking test runs.
 */
export class TestingManager {
  private config: Record<string, any>;
  private url: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://keploy.zirve-infra.svc.cluster.local:6789').replace(/\/$/, '') + '/api';
  }

  private async request(method: string, path: string): Promise<any> {
    const options: RequestInit = {
      method,
      headers: { 'Content-Type': 'application/json' }
    };

    const response = await fetch(`${this.url}/${path.replace(/^\/+/, '')}`, options);
    
    if (!response.ok) {
      if (response.status === 404) return null;
      throw new Error(`Keploy API Error [${response.status}]`);
    }

    if (response.status === 204 || response.headers.get('content-length') === '0') return true;
    return await response.json();
  }

  public async listTestSets(app: string): Promise<any[]> {
    const result = await this.request('GET', `test-sets?app=${encodeURIComponent(app)}`);
    return result || [];
  }

  public async getTestRun(id: string): Promise<any | null> {
    return await this.request('GET', `test-run/${encodeURIComponent(id)}`);
  }

  public async health(): Promise<boolean> {
    try {
      // Basic ping/health based on available endpoints
      const response = await fetch(`${this.url}/healthz`); // Placeholder check
      return response.ok;
    } catch {
      return false;
    }
  }
}
