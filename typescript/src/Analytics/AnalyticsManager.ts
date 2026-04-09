/**
 * Zirve Analytics Manager — ClickHouse Integration.
 *
 * Uses raw HTTP interface for executing ClickHouse SQL queries and inserts.
 */
export class AnalyticsManager {
  private config: Record<string, any>;
  private url: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    const protocol = config.scheme || 'http';
    const host = config.host || 'clickhouse.zirve-infra.svc.cluster.local';
    const port = config.port || 8123;
    
    this.url = `${protocol}://${host}:${port}/`;
  }

  /**
   * Execute an SQL query against ClickHouse using the HTTP interface.
   */
  public async query(sql: string, params: Record<string, any> = {}): Promise<any[]> {
    let finalQuery = sql;
    
    // Add ClickHouse format instruction to get JSON
    if (!finalQuery.toUpperCase().includes('FORMAT JSON')) {
      finalQuery += ' FORMAT JSON';
    }

    // Basic string replacement for parameters (ClickHouse syntax: {param:String})
    // This is a naive implementation; in production, you might want to URL encode params
    for (const [key, value] of Object.entries(params)) {
      const placeholder = `{${key}}`;
      if (finalQuery.includes(placeholder)) {
        // String escaping logic for safety
        const safeValue = typeof value === 'string' ? `'${value.replace(/'/g, "\\'")}'` : Number(value);
        finalQuery = finalQuery.replace(new RegExp(placeholder, 'g'), String(safeValue));
      }
    }

    const headers: Record<string, string> = {};
    if (this.config.username) headers['X-ClickHouse-User'] = this.config.username;
    if (this.config.password) headers['X-ClickHouse-Key'] = this.config.password;
    if (this.config.database) headers['X-ClickHouse-Database'] = this.config.database;

    const response = await fetch(this.url, {
      method: 'POST',
      headers,
      body: finalQuery,
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`ClickHouse query failed [${response.status}]: ${errorText}`);
    }

    const data = await response.json();
    return data.data || [];
  }

  /**
   * Execute DDL or non-returning operations.
   */
  public async execute(sql: string): Promise<boolean> {
    const headers: Record<string, string> = {};
    if (this.config.username) headers['X-ClickHouse-User'] = this.config.username;
    if (this.config.password) headers['X-ClickHouse-Key'] = this.config.password;
    if (this.config.database) headers['X-ClickHouse-Database'] = this.config.database;

    const response = await fetch(this.url, {
      method: 'POST',
      headers,
      body: sql,
    });

    // Valid response statuses: 200 OK
    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`ClickHouse execute failed [${response.status}]: ${errorText}`);
    }

    return true;
  }

  /**
   * Insert a single row into a table.
   */
  public async insert(table: string, data: Record<string, any>): Promise<boolean> {
    const keys = Object.keys(data).join(', ');
    const values = Object.values(data).map(v => 
      typeof v === 'string' ? `'${v.replace(/'/g, "\\'")}'` : v
    ).join(', ');

    const sql = `INSERT INTO ${table} (${keys}) VALUES (${values})`;
    return await this.execute(sql);
  }

  /**
   * Basic health check.
   */
  public async health(): Promise<boolean> {
    try {
      const response = await fetch(`${this.url}ping`);
      const text = await response.text();
      return text.trim() === 'Ok.';
    } catch {
      return false;
    }
  }
}
