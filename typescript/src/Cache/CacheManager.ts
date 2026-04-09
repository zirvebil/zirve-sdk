import { Redis, RedisOptions } from 'ioredis';

/**
 * Zirve Cache Manager — Redis.
 * 
 * Provides get/set with JSON serialization, caching closure via `remember()`, 
 * and distributed locking.
 */
export class CacheManager {
  private redis: Redis | null = null;
  private config: Record<string, any>;
  private prefix: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.prefix = config.prefix || 'zirve:';
  }

  /**
   * Lazy load Redis connection to avoid connections unless used.
   */
  private getRedis(): Redis {
    if (!this.redis) {
      const options: RedisOptions = {
        host: this.config.host || 'localhost',
        port: Number(this.config.port) || 6379,
        keyPrefix: this.prefix,
        lazyConnect: true,
      };

      if (this.config.password) {
        options.password = this.config.password;
      }

      this.redis = new Redis(options);
    }
    return this.redis;
  }

  /**
   * Retrieve a value from the cache.
   */
  public async get<T>(key: string): Promise<T | null> {
    const value = await this.getRedis().get(key);
    if (!value) return null;

    try {
      return JSON.parse(value) as T;
    } catch {
      return value as unknown as T;
    }
  }

  /**
   * Store an item in the cache for a given number of seconds.
   */
  public async set(key: string, value: any, ttlSeconds: number = 3600): Promise<void> {
    const encoded = typeof value === 'string' ? value : JSON.stringify(value);
    await this.getRedis().set(key, encoded, 'EX', ttlSeconds);
  }

  /**
   * Remove an item from the cache.
   */
  public async forget(key: string): Promise<void> {
    await this.getRedis().del(key);
  }

  /**
   * Get an item from the cache, or execute the given closure and store the result.
   */
  public async remember<T>(key: string, ttlSeconds: number, callback: () => Promise<T>): Promise<T> {
    const cached = await this.get<T>(key);
    if (cached !== null) {
      return cached;
    }

    const val = await callback();
    await this.set(key, val, ttlSeconds);

    return val;
  }

  /**
   * Distributed Lock using Redis SET NX.
   * Returns true if lock was acquired, false if it is already locked by another process.
   */
  public async lock(key: string, ownerId: string, ttlSeconds: number = 60): Promise<boolean> {
    const result = await this.getRedis().set(
      `lock:${key}`,
      ownerId,
      'EX',
      ttlSeconds,
      'NX'
    );
    return result === 'OK';
  }

  /**
   * Release a distributed lock, only if owned by the ownerId.
   */
  public async unlock(key: string, ownerId: string): Promise<boolean> {
    const script = `
      if redis.call("get",KEYS[1]) == ARGV[1] then
          return redis.call("del",KEYS[1])
      else
          return 0
      end
    `;

    // ioredis allows evaluating custom lua scripts
    // prefix is automatically applied to keys if supported, but via script we need to be careful
    // Since we set `keyPrefix` on the client, ioredis automatically prefixes keys passed in the KEYS array.
    const result = await this.getRedis().eval(script, 1, `lock:${key}`, ownerId);
    return result === 1;
  }

  /**
   * Clear entire Redis database. Use with caution.
   */
  public async flush(): Promise<void> {
    await this.getRedis().flushdb();
  }

  /**
   * Basic health check.
   */
  public async health(): Promise<boolean> {
    try {
      const res = await this.getRedis().ping();
      return res === 'PONG';
    } catch {
      return false;
    }
  }

  /**
   * Gracefully close Redis connection.
   */
  public async close(): Promise<void> {
    if (this.redis) {
      await this.redis.quit();
    }
  }
}
