/**
 * Zirve Log Manager — Loki Integration.
 *
 * Implements a buffered structured logger that pushes to Grafana Loki via HTTP API.
 */
export class LogManager {
  private config: Record<string, any>;
  private url: string;
  private serviceName: string;
  private buffer: any[] = [];
  private batchSize: number;
  private autoFlushInterval: number;
  private timer: NodeJS.Timeout | null = null;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://loki.zirve-infra.svc.cluster.local:3100').replace(/\/$/, '');
    this.serviceName = config.service || process.env.APP_NAME || 'zirve-node-app';
    this.batchSize = config.batchSize || 10;
    this.autoFlushInterval = config.autoFlushInterval || 5000;

    // Start auto flush background timer
    if (this.autoFlushInterval > 0) {
      this.timer = setInterval(() => {
        this.flush().catch(() => {});
      }, this.autoFlushInterval);
      
      // Prevent timer from keeping Node.js process alive unnecessarily
      if (this.timer.unref) {
        this.timer.unref();
      }
    }

    // Attempt to flush on process exit
    if (typeof process !== 'undefined') {
      process.on('beforeExit', () => {
        this.flushSync();
      });
    }
  }

  /**
   * Add a log entry to the buffer.
   */
  public log(level: string, message: string, context: Record<string, any> = {}): void {
    this.buffer.push({
      level,
      message,
      context,
      timestamp: Date.now() * 1000000 // Loki expects nanoseconds
    });

    if (this.buffer.length >= this.batchSize) {
      this.flush().catch(() => {});
    }
  }

  public info(message: string, context: Record<string, any> = {}): void { this.log('info', message, context); }
  public warn(message: string, context: Record<string, any> = {}): void { this.log('warn', message, context); }
  public error(message: string, context: Record<string, any> = {}): void { this.log('error', message, context); }
  public debug(message: string, context: Record<string, any> = {}): void { this.log('debug', message, context); }

  /**
   * Flush buffered logs to Loki asynchronously.
   */
  public async flush(): Promise<void> {
    if (this.buffer.length === 0) return;

    const currentBuffer = [...this.buffer];
    this.buffer = [];

    const values = currentBuffer.map(log => [
      log.timestamp.toString(),
      JSON.stringify({
        msg: log.message,
        ...log.context
      })
    ]);

    // Use the dominant level of the batch for indexing, or generic 'info'
    const batchLevel = currentBuffer[0]?.level || 'info';

    const payload = {
      streams: [{
        stream: {
          service: this.serviceName,
          level: batchLevel
        },
        values
      }]
    };

    try {
      await fetch(`${this.url}/loki/api/v1/push`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
    } catch {
      // Restore buffer on failure to prevent log loss (simple retry mechanism)
      this.buffer = [...currentBuffer, ...this.buffer];
    }
  }

  /**
   * Attempt a synchronous flush using native fetch if possible or drop.
   * Node process is exiting, so async operations are unreliable.
   */
  private flushSync(): void {
    if (this.buffer.length === 0) return;
    
    // In Node 18+ and edge environments, standard fetch might not finish during exit.
    // We do a best-effort blind fire.
    this.flush().catch(() => {});
  }

  /**
   * Gracefully close logger.
   */
  public async close(): Promise<void> {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
    await this.flush();
  }

  /**
   * Basic log server health check.
   */
  public async health(): Promise<boolean> {
    try {
      const response = await fetch(`${this.url}/ready`);
      return response.ok;
    } catch {
      return false;
    }
  }
}
