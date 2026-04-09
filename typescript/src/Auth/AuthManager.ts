/**
 * Zirve Auth Manager — Keycloak Integration.
 *
 * Provides token introspection, user info, and role verification using the Keycloak OIDC endpoint.
 */
export class AuthManager {
  private config: Record<string, any>;
  private baseUrl: string;
  private realm: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.baseUrl = (config.url || 'http://keycloak.zirve-infra.svc.cluster.local').replace(/\/$/, '');
    this.realm = config.realm || 'zirve';
  }

  /**
   * Introspect an OAuth2 / OIDC access token.
   * Checks if the token is active and returns its claims.
   */
  public async verifyToken(token: string): Promise<Record<string, any>> {
    const clientId = this.config.client_id || 'zirve-backend';
    const clientSecret = this.config.client_secret || '';

    const params = new URLSearchParams();
    params.append('token', token);
    params.append('client_id', clientId);
    if (clientSecret) {
      params.append('client_secret', clientSecret);
    }

    const response = await fetch(`${this.baseUrl}/realms/${this.realm}/protocol/openid-connect/token/introspect`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
      },
      body: params.toString(),
    });

    if (!response.ok) {
      throw new Error(`Keycloak introspection failed with status: ${response.status}`);
    }

    const data = await response.json();
    return data;
  }

  /**
   * Check if a token has a specific realm role.
   */
  public async hasRole(token: string, roleName: string): Promise<boolean> {
    const claims = await this.verifyToken(token);
    if (!claims.active) {
      return false;
    }

    const roles = claims.realm_access?.roles || [];
    return roles.includes(roleName);
  }

  /**
   * Fetch the user info from Keycloak based on the access token.
   */
  public async getUser(token: string): Promise<Record<string, any>> {
    const response = await fetch(`${this.baseUrl}/realms/${this.realm}/protocol/openid-connect/userinfo`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Keycloak userinfo failed with status: ${response.status}`);
    }

    return await response.json();
  }

  /**
   * Basic health check.
   */
  public async health(): Promise<boolean> {
    try {
      const response = await fetch(`${this.baseUrl}/realms/${this.realm}/.well-known/openid-configuration`, {
        method: 'GET',
      });
      return response.ok;
    } catch {
      return false;
    }
  }
}
