/**
 * Zirve Remote Manager — Apache Guacamole Integration.
 *
 * Uses Guacamole REST API to manage connections and generate session URLs.
 */
export class RemoteManager {
  private config: Record<string, any>;
  private url: string;
  private token: string | null = null;
  private tokenExpires: number = 0;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://guacamole.zirve-infra.svc.cluster.local:8080').replace(/\/$/, '');
  }

  /**
   * Authenticate with Guacamole REST API to obtain a token.
   */
  private async authenticate(): Promise<string> {
    if (this.token && Date.now() < this.tokenExpires) {
      return this.token;
    }

    const username = this.config.username || 'guacadmin';
    const password = this.config.password || 'guacadmin';

    const params = new URLSearchParams();
    params.append('username', username);
    params.append('password', password);

    const response = await fetch(`${this.url}/api/tokens`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded'
      },
      body: params.toString()
    });

    if (!response.ok) {
      throw new Error(`Guacamole authentication failed [${response.status}]`);
    }

    const data = await response.json();
    this.token = data.authToken;
    // Basic expire tracking (assume 60 min, refresh at 50 min)
    this.tokenExpires = Date.now() + (50 * 60 * 1000);
    
    return this.token as string;
  }

  /**
   * Internal wrapper for API calls.
   */
  private async request(method: string, path: string, body?: any): Promise<any> {
    const token = await this.authenticate();
    
    // Guacamole API requires token passed as a query param
    const separator = path.includes('?') ? '&' : '?';
    const fullUrl = `${this.url}/api/${path.replace(/^\/+/, '')}${separator}token=${token}`;

    const options: RequestInit = {
      method,
      headers: {
        'Content-Type': 'application/json'
      }
    };

    if (body) {
      options.body = JSON.stringify(body);
    }

    const response = await fetch(fullUrl, options);
    
    if (!response.ok) {
      if (response.status === 401) {
        // Force token refresh on next call
        this.token = null;
      }
      throw new Error(`Guacamole request failed: ${response.statusText}`);
    }

    if (response.status === 204 || response.headers.get('content-length') === '0') {
      return null;
    }

    return await response.json();
  }

  /**
   * Create a new remote connection (RDP, VNC, SSH, Telnet).
   */
  public async createConnection(
    name: string, 
    protocol: 'rdp' | 'vnc' | 'ssh' | 'telnet', 
    parameters: Record<string, string>, 
    parentIdentifier: string = 'ROOT'
  ): Promise<string> {
    const payload = {
      parentIdentifier,
      name,
      protocol,
      parameters,
      attributes: {}
    };

    const data = await this.request('POST', 'session/data/postgresql/connections', payload);
    return data.identifier || ''; // Connection ID
  }

  /**
   * Generate a Client URL for an existing connection.
   */
  public async getSession(connectionId: string): Promise<string> {
    const token = await this.authenticate();
    // In Guacamole, the URL structure typically expects a base64 encoded string `id\0c\0postgresql`
    // Example format:
    // Identifier base is roughly: connectionId + \0 + "c" + \0 + "postgresql"
    const rawIds = `${connectionId}\u0000c\u0000postgresql`;
    const encoded = Buffer.from(rawIds).toString('base64');

    return `${this.url}/#/client/${encoded}?token=${token}`;
  }

  /**
   * List available connections.
   */
  public async listConnections(): Promise<Record<string, any>> {
    return await this.request('GET', 'session/data/postgresql/connections');
  }

  /**
   * Delete a connection.
   */
  public async deleteConnection(connectionId: string): Promise<boolean> {
    try {
      await this.request('DELETE', `session/data/postgresql/connections/${connectionId}`);
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Basic Guacamole health check.
   */
  public async health(): Promise<boolean> {
    try {
      const response = await fetch(`${this.url}/api/ext/auth/postgresql/schema`);
      return response.ok;
    } catch {
      return false;
    }
  }
}
