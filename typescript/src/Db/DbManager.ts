import { Pool, PoolClient, QueryResult } from 'pg';

/**
 * Zirve DB Manager — PostgreSQL & MariaDB.
 * 
 * Provides prepared queries, multi-tenant schema switching, and transactions.
 * Utilizes generic `pg` Pool for connection management.
 */
export class DbManager {
  private pool: Pool;
  private config: Record<string, any>;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.pool = new Pool({
      host: config.host || 'localhost',
      port: Number(config.port) || 5432,
      database: config.dbname || 'zirve',
      user: config.user || 'postgres',
      password: config.password || '',
      max: 20, // max connection pool size
      idleTimeoutMillis: 30000,
    });
  }

  /**
   * Execute a parameterized SQL query.
   */
  public async query<T = any>(sql: string, params: any[] = []): Promise<T[]> {
    const result = await this.pool.query(sql, params);
    return result.rows;
  }

  /**
   * Execute a single scalar query, returning the first column of the first row.
   */
  public async scalar<T = any>(sql: string, params: any[] = []): Promise<T | null> {
    const rows = await this.query(sql, params);
    if (!rows || rows.length === 0) return null;
    return Object.values(rows[0])[0] as T;
  }

  /**
   * Safely execute operations within a database transaction.
   * Auto-commits on success, rolls back on exception.
   */
  public async transaction<T>(callback: (client: PoolClient) => Promise<T>): Promise<T> {
    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');
      const result = await callback(client);
      await client.query('COMMIT');
      return result;
    } catch (err) {
      await client.query('ROLLBACK');
      throw err;
    } finally {
      client.release();
    }
  }

  /**
   * Set PostgreSQL `search_path` to isolated tenant schema for multi-tenant data.
   */
  public async tenant(tenantId: string): Promise<void> {
    // Escaping identifier to prevent SQL injection in schema name
    const cleanId = tenantId.replace(/[^a-zA-Z0-9_]/g, '');
    await this.query(`SET search_path TO tenant_${cleanId}, public`);
  }

  /**
   * Health check to ping the database.
   */
  public async health(): Promise<boolean> {
    try {
      await this.query('SELECT 1');
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Close the connection pool gracefully.
   */
  public async close(): Promise<void> {
    await this.pool.end();
  }
}
