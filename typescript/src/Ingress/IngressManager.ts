/**
 * Zirve Ingress Manager — Traefik
 *
 * Reads routing configurations and status from Traefik API.
 */
export class IngressManager {
  private config: Record<string, any>;
  private url: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://traefik.kube-system.svc.cluster.local:8080').replace(/\/$/, '');
  }

  public async routes(): Promise<any[]> {
    try {
      const response = await fetch(`${this.url}/api/http/routers`);
      return response.ok ? await response.json() : [];
    } catch {
      return [];
    }
  }

  public async middlewares(): Promise<any[]> {
    try {
      const response = await fetch(`${this.url}/api/http/middlewares`);
      return response.ok ? await response.json() : [];
    } catch {
      return [];
    }
  }

  public async health(): Promise<boolean> {
    try {
      const response = await fetch(`${this.url}/ping`);
      return response.ok;
    } catch {
      return false;
    }
  }
}
