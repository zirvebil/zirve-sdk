/**
 * Zirve Registry Manager — Harbor
 *
 * Integrates with Harbor v2 HTTP API to list images, scan vulnerabilities, and manage tags.
 */
export class RegistryManager {
  private config: Record<string, any>;
  private url: string;
  private authHeader: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://harbor-core.zirve-infra.svc.cluster.local').replace(/\/$/, '') + '/api/v2.0';
    
    const user = config.username || 'admin';
    const pass = config.password || 'Harbor12345';
    this.authHeader = 'Basic ' + Buffer.from(`${user}:${pass}`).toString('base64');
  }

  private async request(method: string, path: string, body?: any): Promise<any> {
    const options: RequestInit = {
      method,
      headers: {
        'Authorization': this.authHeader,
        'Content-Type': 'application/json'
      },
      body: body ? JSON.stringify(body) : undefined
    };

    const response = await fetch(`${this.url}/${path.replace(/^\/+/, '')}`, options);
    
    if (!response.ok) {
      if (response.status === 404) return null;
      throw new Error(`Harbor API Error [${response.status}]`);
    }

    if (response.status === 204 || response.headers.get('content-length') === '0') return true;
    return await response.json();
  }

  public async listProjects(): Promise<any[]> {
    const projects = await this.request('GET', 'projects');
    return projects || [];
  }

  public async listImages(projectName: string): Promise<any[]> {
    const repos = await this.request('GET', `projects/${projectName}/repositories`);
    return repos || [];
  }

  public async scanImage(projectName: string, repositoryName: string, reference: string): Promise<boolean> {
    // Escaping repo name required by Harbor for deeply nested paths
    const safeRepo = encodeURIComponent(repositoryName);
    try {
      await this.request('POST', `projects/${projectName}/repositories/${safeRepo}/artifacts/${reference}/scan`);
      return true;
    } catch {
      return false;
    }
  }

  public async scanReport(projectName: string, repositoryName: string, reference: string): Promise<any> {
    const safeRepo = encodeURIComponent(repositoryName);
    return await this.request('GET', `projects/${projectName}/repositories/${safeRepo}/artifacts/${reference}/additions/vulnerabilities`);
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
