/**
 * Zirve OnCall Manager — Grafana OnCall
 *
 * Interacts with Grafana OnCall API to create alerts and manage incidents.
 */
export class OnCallManager {
  private config: Record<string, any>;
  private url: string;
  private token: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://grafana-oncall-engine.zirve-infra.svc.cluster.local:8080').replace(/\/$/, '') + '/api/v1';
    this.token = config.token || '';
  }

  private async request(method: string, path: string, body?: any): Promise<any> {
    const options: RequestInit = {
      method,
      headers: {
        'Authorization': `Token ${this.token}`,
        'Content-Type': 'application/json'
      },
      body: body ? JSON.stringify(body) : undefined
    };

    const response = await fetch(`${this.url}/${path.replace(/^\/+/, '')}`, options);
    
    if (!response.ok) {
      if (response.status === 404) return null;
      throw new Error(`Grafana OnCall API Error [${response.status}]`);
    }

    if (response.status === 204 || response.headers.get('content-length') === '0') return true;
    return await response.json();
  }

  public async createAlert(integrationUrl: string, title: string, message: string, state: 'alerting' | 'ok' = 'alerting'): Promise<boolean> {
    // Note: Grafana OnCall uses an integration URL for inbound webhooks
    try {
      const response = await fetch(integrationUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          title,
          message,
          state
        })
      });
      return response.ok;
    } catch {
      return false;
    }
  }

  public async listSchedules(): Promise<any[]> {
    const result = await this.request('GET', 'schedules');
    return result?.results || [];
  }

  public async listIncidents(state: 'triggered' | 'acknowledged' | 'resolved' = 'triggered'): Promise<any[]> {
    const result = await this.request('GET', `alert_groups?state=${state}`);
    return result?.results || [];
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
