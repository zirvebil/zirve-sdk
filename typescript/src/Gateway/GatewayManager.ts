/**
 * Zirve Gateway Manager — Kong
 *
 * Interacts with Kong Admin API for routing and plugins.
 */
export class GatewayManager {
  private config: Record<string, any>;
  private url: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://kong-kong-admin.zirve-infra.svc.cluster.local:8001').replace(/\/$/, '');
  }

  private async request(method: string, path: string, body?: any): Promise<any> {
    const options: RequestInit = {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: body ? JSON.stringify(body) : undefined
    };

    const response = await fetch(`${this.url}/${path.replace(/^\/+/, '')}`, options);
    
    if (!response.ok) {
      if (response.status === 204 || response.status === 404) return null;
      throw new Error(`Kong API Error [${response.status}]`);
    }

    if (response.status === 204) return true;
    return await response.json();
  }

  public async addRoute(serviceIdOrName: string, name: string, paths: string[]): Promise<any> {
    return await this.request('POST', `services/${serviceIdOrName}/routes`, {
      name,
      paths,
      strip_path: true
    });
  }

  public async addService(name: string, protocol: string, host: string, port: number, path: string = '/'): Promise<any> {
    return await this.request('POST', 'services', {
      name,
      protocol,
      host,
      port,
      path
    });
  }

  public async addPlugin(serviceIdOrName: string, name: string, config: any): Promise<any> {
    return await this.request('POST', `services/${serviceIdOrName}/plugins`, {
      name,
      config
    });
  }

  public async rateLimit(serviceIdOrName: string, minute: number, hour: number): Promise<any> {
    return await this.addPlugin(serviceIdOrName, 'rate-limiting', {
      minute,
      hour,
      policy: 'local'
    });
  }

  public async health(): Promise<boolean> {
    try {
      const response = await fetch(`${this.url}/status`);
      return response.ok;
    } catch {
      return false;
    }
  }
}
