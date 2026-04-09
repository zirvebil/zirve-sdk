/**
 * Zirve Deploy Manager — ArgoCD
 *
 * Interfaces with ArgoCD Server REST API to trigger app deployments.
 */
export class DeployManager {
  private config: Record<string, any>;
  private url: string;
  private token: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://argocd-server.argocd.svc.cluster.local').replace(/\/$/, '') + '/api/v1';
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
      throw new Error(`ArgoCD API Error [${response.status}]: ${response.statusText}`);
    }

    if (response.status === 204 || response.headers.get('content-length') === '0') return true;
    return await response.json();
  }

  public async listApplications(): Promise<any[]> {
    const result = await this.request('GET', 'applications');
    return result?.items || [];
  }

  public async sync(applicationName: string): Promise<any> {
    return await this.request('POST', `applications/${applicationName}/sync`);
  }

  public async status(applicationName: string): Promise<any> {
    return await this.request('GET', `applications/${applicationName}`);
  }

  public async history(applicationName: string): Promise<any[]> {
    const app = await this.status(applicationName);
    return app?.status?.history || [];
  }

  public async rollback(applicationName: string, id: number): Promise<any> {
    return await this.request('POST', `applications/${applicationName}/rollback`, { id });
  }

  public async health(): Promise<boolean> {
    // Use version endpoint as health check for REST
    try {
      const response = await fetch(`${this.url}/version`);
      return response.ok;
    } catch {
      return false;
    }
  }
}
