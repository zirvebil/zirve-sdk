import { describe, it, expect, beforeEach } from 'vitest';
import { Zirve } from '../src/index';

describe('Zirve SDK Entrypoint', () => {
  beforeEach(() => {
    Zirve.reset();
  });

  it('Zirve.init() singleton döndürür', () => {
    const sdk1 = Zirve.init();
    const sdk2 = Zirve.getInstance();

    expect(sdk1).toBe(sdk2);
  });

  it('Zirve.reset() singleton sıfırlar', () => {
    const sdk1 = Zirve.init();
    Zirve.reset();
    
    expect(() => Zirve.getInstance()).toThrow('Zirve SDK is not initialized');
  });

  it('config modülü erişilebilir ve config çalışıyor', () => {
    const sdk = Zirve.init({ 'db.host': 'test-host' });
    expect(sdk.config.get('db.host')).toBe('test-host');
  });

  it('db ve cache modülleri lazy-load edilir', () => {
    const sdk = Zirve.init();
    
    // property lere ilk defa erisilince instance olusmali
    expect(sdk.db).toBeDefined();
    expect(sdk.cache).toBeDefined();

    // ikinci kez erisimde ayni nesne gelmeli (lazily loaded)
    const db1 = sdk.db;
    const db2 = sdk.db;
    expect(db1).toBe(db2);
  });
});
