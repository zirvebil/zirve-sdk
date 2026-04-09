/**
 * Zirve Secrets Manager — Infisical Integration.
 *
 * Retrieves parameters and secrets from Infisical with an in-memory cache layer.
 */
export class SecretsManager {
  private config: Record<string, any>;
  private baseUrl: string;
  private token: string;
  private projectId: string;
  
  // In-memory local cache for quick secret access
  private cache: Record<string, { value: string; expiresAt: number }> = {};
  private readonly CACHE_TTL_MS = 300000; // 5 minutes

  constructor(config: Record<string, any>) {
    this.config = config;
    this.baseUrl = (config.url || 'http://infisical.zirve-infra.svc.cluster.local').replace(/\/$/, '');
    this.token = config.token || '';
    this.projectId = config.project_id || '';
  }

  /**
   * Get a secret by its key name from Infisical.
   */
  public async get(secretName: string, environment: string = 'dev', path: string = '/'): Promise<string | null> {
    const cacheKey = `${environment}:${path}:${secretName}`;

    // Check cache
    if (this.cache[cacheKey] && this.cache[cacheKey].expiresAt > Date.now()) {
      return this.cache[cacheKey].value;
    }

    if (!this.token || !this.projectId) {
      throw new Error('Infisical token and project ID must be configured.');
    }

    const url = new URL(`${this.baseUrl}/api/v3/secrets/raw/${secretName}`);
    url.searchParams.append('workspaceId', this.projectId);
    url.searchParams.append('environment', environment);
    url.searchParams.append('secretPath', path);

    const response = await fetch(url.toString(), {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${this.token}`,
      },
    });

    if (response.status === 404) {
      return null;
    }

    if (!response.ok) {
      throw new Error(`Infisical fetch failed: ${response.statusText}`);
    }

    const data = await response.json();
    const value = data.secret.secretValue;

    // Cache the secret
    this.cache[cacheKey] = {
      value,
      expiresAt: Date.now() + this.CACHE_TTL_MS,
    };

    return value;
  }

  /**
   * List all secrets in a specific environment and path.
   */
  public async list(environment: string = 'dev', path: string = '/'): Promise<Record<string, string>> {
    const url = new URL(`${this.baseUrl}/api/v3/secrets/raw`);
    url.searchParams.append('workspaceId', this.projectId);
    url.searchParams.append('environment', environment);
    url.searchParams.append('secretPath', path);

    const response = await fetch(url.toString(), {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${this.token}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Infisical list failed: ${response.statusText}`);
    }

    const data = await response.json();
    const result: Record<string, string> = {};

    for (const secret of data.secrets) {
      result[secret.secretKey] = secret.secretValue;
    }

    return result;
  }

  /**
   * Basic health check.
   */
  public async health(): Promise<boolean> {
    try {
      const response = await fetch(`${this.baseUrl}/api/v1/health`, {
        method: 'GET',
      });
      return response.ok;
    } catch {
      return false;
    }
  }
}
