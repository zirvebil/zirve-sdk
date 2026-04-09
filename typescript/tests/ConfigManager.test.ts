import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { ConfigManager } from '../src/Config/ConfigManager';

describe('ConfigManager', () => {
  let originalEnv: NodeJS.ProcessEnv;

  beforeEach(() => {
    // Save original env
    originalEnv = { ...process.env };
  });

  afterEach(() => {
    // Restore original env
    process.env = originalEnv;
  });

  it('varsayılan değerler K8s servis DNS adresleri ile döner', () => {
    const config = new ConfigManager();
    expect(config.get('db.host')).toBe('postgresql.zirve-infra.svc.cluster.local');
    expect(config.get('db.port')).toBe(5432);
    expect(config.get('cache.host')).toBe('redis-master.zirve-infra.svc.cluster.local');
  });

  it('override değerleri varsayılanları ezer', () => {
    const config = new ConfigManager({
      'db.host': 'custom-db-host',
      'cache.port': 9999
    });

    expect(config.get('db.host')).toBe('custom-db-host');
    expect(config.get('cache.port')).toBe(9999);
    // Diğerleri varsayılan değerinde kalmalı
    expect(config.get('db.port')).toBe(5432);
  });

  it('çevre değişkenleri override edilebilir', () => {
    process.env.PG_HOST = 'env-db-host';
    process.env.REDIS_PORT = '8888';

    const config = new ConfigManager();
    expect(config.get('db.host')).toBe('env-db-host');
    expect(config.get('cache.port')).toBe('8888');
  });

  it('öncelik sırası: override > env > default', () => {
    process.env.PG_HOST = 'env-db-host';
    const config = new ConfigManager({ 'db.host': 'override-db-host' });

    expect(config.get('db.host')).toBe('override-db-host');
  });

  it('module() prefix ile doğru konfigürasyon kümesini döndürür', () => {
    process.env.PG_USER = 'admin';
    const config = new ConfigManager({ 'db.password': 'secret123' });

    const dbConfig = config.module('db');

    expect(dbConfig.host).toBe('postgresql.zirve-infra.svc.cluster.local');
    expect(dbConfig.port).toBe(5432);
    expect(dbConfig.dbname).toBe('zirve');
    expect(dbConfig.user).toBe('admin');
    expect(dbConfig.password).toBe('secret123');
  });

  it('dapr URL döndürür', () => {
    const config = new ConfigManager();
    expect(config.daprUrl()).toContain('http://localhost');
  });

  it('bilinmeyen key için default veya null döner', () => {
    const config = new ConfigManager();
    expect(config.get('unknown.key')).toBeNull();
    expect(config.get('unknown.key', 'fallback')).toBe('fallback');
  });
});
