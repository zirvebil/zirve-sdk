/**
 * Zirve Error Manager — Sentry Integration.
 *
 * Lightweight client to push errors directly to Sentry's ingest API.
 */
export class ErrorManager {
  private config: Record<string, any>;
  private dsn: string;
  private user: Record<string, any> | null = null;
  private breadcrumbs: any[] = [];

  constructor(config: Record<string, any>) {
    this.config = config;
    this.dsn = config.dsn || '';
  }

  /**
   * Parse a Sentry DSN into components.
   */
  private parseDsn(dsn: string): Record<string, string> {
    if (!dsn) return {};
    
    const url = new URL(dsn);
    const publicKey = url.username;
    const projectId = url.pathname.replace(/^\//, '');
    const protocol = url.protocol;
    const host = url.host;

    return {
      host: `${protocol}//${host}`,
      public_key: publicKey,
      project_id: projectId
    };
  }

  /**
   * Generate a pseudo-random UUID v4 without external deps.
   */
  private uuidv4(): string {
    return 'xxxxxxxxxxxx4xxxyxxxxxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
      const r = Math.random() * 16 | 0, v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
  }

  /**
   * Send event payload to Sentry.
   */
  private async send(event: Record<string, any>): Promise<string | null> {
    if (!this.dsn) return null;

    const parsed = this.parseDsn(this.dsn);
    if (!parsed.host) return null;

    const eventId = this.uuidv4();
    event.event_id = eventId;
    event.timestamp = new Date().toISOString();
    event.platform = 'node';
    event.sdk = { name: 'zirve-node-sdk', version: '0.1.0' };

    if (this.user) {
      event.user = this.user;
    }
    if (this.breadcrumbs.length > 0) {
      event.breadcrumbs = { values: this.breadcrumbs };
    }

    try {
      const authHeader = `Sentry sentry_version=7, sentry_key=${parsed.public_key}, sentry_client=zirve-node-sdk/0.1.0`;
      
      await fetch(`${parsed.host}/api/${parsed.project_id}/store/`, {
        method: 'POST',
        headers: {
          'X-Sentry-Auth': authHeader,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(event)
      });

      return eventId;
    } catch {
      return null;
    }
  }

  private buildStacktrace(err: Error): any[] {
    const stack = err.stack || '';
    const frames: any[] = [];
    
    // Parse Node V8 stack traces
    const lines = stack.split('\n').slice(1);
    for (const line of lines) {
      const match = line.match(/at (.*?) \((.*?):(\d+):(\d+)\)/) || line.match(/at (.*?):(\d+):(\d+)/);
      if (match) {
        if (match.length === 5) {
          frames.push({
            function: match[1],
            filename: match[2],
            lineno: parseInt(match[3], 10),
            colno: parseInt(match[4], 10),
          });
        } else {
          frames.push({
            function: '?',
            filename: match[1],
            lineno: parseInt(match[2], 10),
            colno: parseInt(match[3], 10),
          });
        }
      }
    }
    
    // Sentry expects frames in reverse chronological order (oldest first)
    return frames.reverse();
  }

  /**
   * Capture a handled exception.
   */
  public async captureException(err: Error): Promise<string | null> {
    const payload = {
      level: 'error',
      exception: {
        values: [{
          type: err.name,
          value: err.message,
          stacktrace: {
            frames: this.buildStacktrace(err)
          }
        }]
      }
    };
    return await this.send(payload);
  }

  /**
   * Capture a plain text message.
   */
  public async captureMessage(message: string, level: string = 'info'): Promise<string | null> {
    const payload = {
      message,
      level
    };
    return await this.send(payload);
  }

  /**
   * Set the current user context.
   */
  public setUser(id: string, email?: string, username?: string): void {
    this.user = { id, email, username };
  }

  /**
   * Clear user context.
   */
  public clearUser(): void {
    this.user = null;
  }

  /**
   * Add a breadcrumb to the current context.
   */
  public addBreadcrumb(message: string, category: string = 'default', level: string = 'info', data: Record<string, any> = {}): void {
    this.breadcrumbs.push({
      message,
      category,
      level,
      data,
      timestamp: Math.floor(Date.now() / 1000)
    });
    
    // Keep max 100 breadcrumbs
    if (this.breadcrumbs.length > 100) {
      this.breadcrumbs.shift();
    }
  }

  /**
   * Basic health check. Validates DSN is set.
   */
  public async health(): Promise<boolean> {
    return !!this.dsn;
  }
}
