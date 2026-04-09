/**
 * Zirve Trace Manager — OpenTelemetry / Tempo Integration.
 *
 * Lightweight generic trace client that exports Spans to OTel HTTP/OTLP endpoints.
 */
export class TraceManager {
  private config: Record<string, any>;
  private endpoint: string;
  private serviceName: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.endpoint = (config.endpoint || 'http://opentelemetry-collector.zirve-infra.svc.cluster.local:4318').replace(/\/$/, '') + '/v1/traces';
    this.serviceName = config.service || process.env.APP_NAME || 'zirve-node-app';
  }

  /**
   * Generate 16-byte hex trace ID.
   */
  private generateTraceId(): string {
    const bytes = new Uint8Array(16);
    if (typeof crypto !== 'undefined' && crypto.getRandomValues) {
      crypto.getRandomValues(bytes);
    } else {
      for (let i = 0; i < 16; i++) bytes[i] = Math.floor(Math.random() * 256);
    }
    return Buffer.from(bytes).toString('hex');
  }

  /**
   * Generate 8-byte hex span ID.
   */
  private generateSpanId(): string {
    const bytes = new Uint8Array(8);
    if (typeof crypto !== 'undefined' && crypto.getRandomValues) {
      crypto.getRandomValues(bytes);
    } else {
      for (let i = 0; i < 8; i++) bytes[i] = Math.floor(Math.random() * 256);
    }
    return Buffer.from(bytes).toString('hex');
  }

  /**
   * Start a logical trace/span.
   */
  public startSpan(name: string, attributes: Record<string, string> = {}): Record<string, any> {
    return {
      traceId: this.generateTraceId(),
      spanId: this.generateSpanId(),
      name,
      startTime: Date.now() * 1000000, // nanoseconds
      attributes
    };
  }

  /**
   * Measure a closure automatically, handling span start and end.
   */
  public async measure<T>(name: string, callback: (span: Record<string, any>) => Promise<T>, attributes: Record<string, string> = {}): Promise<T> {
    const span = this.startSpan(name, attributes);
    try {
      const result = await callback(span);
      // Wait for export without blocking the result
      this.exportSpan(span).catch(() => {});
      return result;
    } catch (err: any) {
      span.attributes['error'] = 'true';
      span.attributes['error.message'] = err.message;
      this.exportSpan(span).catch(() => {});
      throw err;
    }
  }

  /**
   * End and export a span.
   */
  public async exportSpan(span: Record<string, any>): Promise<boolean> {
    const endTime = Date.now() * 1000000;

    const otlpPayload = {
      resourceSpans: [{
        resource: {
          attributes: [
            { key: 'service.name', value: { stringValue: this.serviceName } }
          ]
        },
        scopeSpans: [{
          spans: [{
            traceId: span.traceId,
            spanId: span.spanId,
            name: span.name,
            kind: 1, // SPAN_KIND_INTERNAL
            startTimeUnixNano: span.startTime.toString(),
            endTimeUnixNano: endTime.toString(),
            attributes: this.formatAttributes(span.attributes)
          }]
        }]
      }]
    };

    try {
      const response = await fetch(this.endpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(otlpPayload)
      });
      return response.ok;
    } catch {
      return false;
    }
  }

  /**
   * Internal helper to format attributes to OTLP JSON.
   */
  private formatAttributes(attrs: Record<string, string>): any[] {
    return Object.entries(attrs).map(([key, val]) => ({
      key,
      value: { stringValue: String(val) }
    }));
  }

  /**
   * Basic OTel health check.
   */
  public async health(): Promise<boolean> {
    try {
      const hostUrl = new URL(this.endpoint);
      const healthEndpoint = `${hostUrl.protocol}//${hostUrl.host}/health`;
      const response = await fetch(healthEndpoint);
      return response.ok;
    } catch {
      return false; // Very basic check
    }
  }
}
