/**
 * Zirve Metrics Manager — Prometheus Pushgateway Integration.
 *
 * Supports counter, gauge, histogram recording and push mechanism.
 */
export class MetricsManager {
  private config: Record<string, any>;
  private url: string;
  private job: string;
  
  // In-memory metrics registry
  private metrics: Record<string, { type: string; value: number; help: string; name: string }> = {};

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://prometheus-server.zirve-infra.svc.cluster.local:9090').replace(/\/$/, '');
    this.job = config.job || process.env.APP_NAME || 'zirve-node-app';

    // Best-effort push on exit
    if (typeof process !== 'undefined') {
      process.on('beforeExit', () => {
        this.pushAsync().catch(() => {});
      });
    }
  }

  /**
   * Internal helper to ensure metric is registered.
   */
  private register(name: string, type: string, help: string): void {
    if (!this.metrics[name]) {
      this.metrics[name] = { name, type, value: 0, help };
    }
  }

  /**
   * Increment a counter.
   */
  public counter(name: string, value: number = 1, help: string = ''): this {
    this.register(name, 'counter', help || `${name} counter`);
    this.metrics[name].value += value;
    return this;
  }

  /**
   * Set a gauge to a specific value.
   */
  public gauge(name: string, value: number, help: string = ''): this {
    this.register(name, 'gauge', help || `${name} gauge`);
    this.metrics[name].value = value;
    return this;
  }

  /**
   * Record a histogram observation (treated similarly to gauge for simple Pushgateway representation).
   */
  public histogram(name: string, value: number, help: string = ''): this {
    this.register(name, 'histogram', help || `${name} histogram`);
    // Basic implementation: we just keep the last recorded value for simple setups.
    // Proper histograms via Pushgateway require generating bucket series.
    this.metrics[name].value = value; 
    return this;
  }

  /**
   * Automatically measure the execution time of a specific closure.
   */
  public async timer<T>(name: string, callback: () => Promise<T>): Promise<T> {
    const start = process.hrtime();
    try {
      return await callback();
    } finally {
      const diff = process.hrtime(start);
      const ms = (diff[0] * 1000) + (diff[1] / 1000000);
      this.histogram(`${name}_duration_ms`, ms, `Duration of ${name} in ms`);
      // Since it's a timer, we proactively push
      this.pushAsync().catch(() => {});
    }
  }

  /**
   * Render internal metrics to Prometheus text format.
   */
  private renderPayload(): string {
    const lines: string[] = [];
    
    for (const [name, meta] of Object.entries(this.metrics)) {
      lines.push(`# HELP ${name} ${meta.help}`);
      lines.push(`# TYPE ${name} ${meta.type}`);
      // Simple value export
      lines.push(`${name} ${meta.value}`);
    }
    
    return lines.join('\n') + '\n';
  }

  /**
   * Push current metrics state to Prometheus Pushgateway immediately.
   */
  public async push(): Promise<boolean> {
    return await this.pushAsync();
  }

  private async pushAsync(): Promise<boolean> {
    if (Object.keys(this.metrics).length === 0) return true;

    try {
      // Modify URL to Pushgateway format if pointing to standard Prometheus
      // Assuming Pushgateway is mapped appropriately if explicit URL mapping is used.
      const pushUrl = this.url.includes('pushgateway') ? this.url : this.url.replace('9090', '9091');
      
      const payload = this.renderPayload();
      
      const response = await fetch(`${pushUrl}/metrics/job/${this.job}`, {
        method: 'POST',
        headers: { 'Content-Type': 'text/plain' },
        body: payload
      });
      return response.ok;
    } catch {
      return false;
    }
  }

  /**
   * Basic health check.
   */
  public async health(): Promise<boolean> {
    try {
      const response = await fetch(`${this.url}/-/healthy`);
      return response.ok;
    } catch {
      return false;
    }
  }
}
