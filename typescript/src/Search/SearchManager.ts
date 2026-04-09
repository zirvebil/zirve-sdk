/**
 * Zirve Search Manager — Elasticsearch Integration.
 *
 * Interacts with Elasticsearch using native fetch, avoiding heavy client dependencies.
 */
export class SearchManager {
  private config: Record<string, any>;
  private baseUrl: string;
  private authHeader?: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    const protocol = config.scheme || 'http';
    const host = config.host || 'elasticsearch-master.zirve-infra.svc.cluster.local';
    const port = config.port || 9200;
    
    this.baseUrl = `${protocol}://${host}:${port}`;

    if (config.username && config.password) {
      this.authHeader = 'Basic ' + Buffer.from(`${config.username}:${config.password}`).toString('base64');
    }
  }

  /**
   * Internal wrapper for fetch requests to Elasticsearch.
   */
  private async request(method: string, path: string, body?: any): Promise<any> {
    const url = `${this.baseUrl}/${path.replace(/^\/+/, '')}`;
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };

    if (this.authHeader) {
      headers['Authorization'] = this.authHeader;
    }

    const options: RequestInit = {
      method,
      headers,
    };

    if (body) {
      // Elasticsearch bulk API requires NDJSON (newline-delimited JSON)
      if (typeof body === 'string') {
        options.body = body;
        if (path.includes('_bulk')) {
          headers['Content-Type'] = 'application/x-ndjson';
        }
      } else {
        options.body = JSON.stringify(body);
      }
    }

    const response = await fetch(url, options);
    
    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Elasticsearch request failed [${response.status}]: ${errorText}`);
    }

    return await response.json();
  }

  /**
   * Index a document.
   */
  public async index(index: string, id: string | null, document: Record<string, any>): Promise<any> {
    const path = id ? `${index}/_doc/${id}` : `${index}/_doc`;
    const method = id ? 'PUT' : 'POST';
    return await this.request(method, path, document);
  }

  /**
   * Get a document by its ID.
   */
  public async get(index: string, id: string): Promise<Record<string, any> | null> {
    try {
      const response = await this.request('GET', `${index}/_doc/${id}`);
      return response.found ? response._source : null;
    } catch {
      return null;
    }
  }

  /**
   * Delete a document.
   */
  public async delete(index: string, id: string): Promise<boolean> {
    try {
      const response = await this.request('DELETE', `${index}/_doc/${id}`);
      return response.result === 'deleted';
    } catch {
      return false;
    }
  }

  /**
   * Execute an advanced raw search query.
   */
  public async search(index: string, queryBody: Record<string, any>): Promise<any[]> {
    const response = await this.request('POST', `${index}/_search`, queryBody);
    if (!response?.hits?.hits) return [];
    
    return response.hits.hits.map((hit: any) => ({
      ...hit._source,
      _id: hit._id,
      _score: hit._score,
    }));
  }

  /**
   * Match terms across multiple fields.
   */
  public async multiMatch(index: string, query: string, fields: string[]): Promise<any[]> {
    return this.search(index, {
      query: {
        multi_match: {
          query,
          fields,
        }
      }
    });
  }

  /**
   * Perform bulk operations. Expected an array of actions and docs.
   */
  public async bulk(operations: any[]): Promise<any> {
    const ndjson = operations.map(op => JSON.stringify(op)).join('\n') + '\n';
    return await this.request('POST', '_bulk', ndjson);
  }

  /**
   * Basic ES health check.
   */
  public async health(): Promise<boolean> {
    try {
      const response = await this.request('GET', '_cluster/health');
      return response.status === 'green' || response.status === 'yellow';
    } catch {
      return false;
    }
  }
}
