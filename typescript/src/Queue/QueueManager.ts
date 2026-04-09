/**
 * Zirve Queue Manager — RabbitMQ Integration.
 *
 * Interacts with RabbitMQ Management HTTP API for generic queue controls,
 * and handles Dapr pub/sub wrappers.
 */
export class QueueManager {
  private config: Record<string, any>;
  private apiBase: string;
  private authHeader: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    const host = config.host || 'rabbitmq.zirve-infra.svc.cluster.local';
    const apiPort = config.api_port || 15672;
    this.apiBase = `http://${host}:${apiPort}/api`;

    const user = config.user || 'guest';
    const pass = config.password || 'guest';
    this.authHeader = 'Basic ' + Buffer.from(`${user}:${pass}`).toString('base64');
  }

  /**
   * Create a RabbitMQ vhost.
   */
  public async createVhost(vhost: string): Promise<boolean> {
    const safeVhost = encodeURIComponent(vhost);
    const response = await fetch(`${this.apiBase}/vhosts/${safeVhost}`, {
      method: 'PUT',
      headers: { 'Authorization': this.authHeader },
    });
    return response.ok || response.status === 204 || response.status === 201;
  }

  /**
   * Get messages from a queue via Management API (for testing/inspection only, not for high throughput).
   */
  public async getMessages(vhost: string, queue: string, count: number = 1): Promise<any[]> {
    const safeVhost = encodeURIComponent(vhost);
    const safeQueue = encodeURIComponent(queue);

    const response = await fetch(`${this.apiBase}/queues/${safeVhost}/${safeQueue}/get`, {
      method: 'POST',
      headers: {
        'Authorization': this.authHeader,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        count,
        ackmode: 'ack_requeue_true', // fetch without acknowledging
        encoding: 'auto',
      }),
    });

    if (!response.ok) {
      throw new Error(`RabbitMQ get messages failed: ${response.statusText}`);
    }

    return await response.json();
  }

  /**
   * Publish a message directly using RabbitMQ HTTP API.
   * Note: For app pub/sub, it's recommended to use Dapr sidecar instead.
   */
  public async publish(vhost: string, exchange: string, routingKey: string, payload: any): Promise<boolean> {
    const safeVhost = encodeURIComponent(vhost);
    const safeExchange = encodeURIComponent(exchange === '' ? 'amq.default' : exchange);

    const response = await fetch(`${this.apiBase}/exchanges/${safeVhost}/${safeExchange}/publish`, {
      method: 'POST',
      headers: {
        'Authorization': this.authHeader,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        properties: {},
        routing_key: routingKey,
        payload: typeof payload === 'string' ? payload : JSON.stringify(payload),
        payload_encoding: 'string'
      }),
    });

    if (!response.ok) return false;
    const data = await response.json();
    return data.routed === true;
  }

  /**
   * Basic health check for RabbitMQ Management.
   */
  public async health(): Promise<boolean> {
    try {
      const response = await fetch(`${this.apiBase}/overview`, {
        headers: { 'Authorization': this.authHeader },
      });
      return response.ok;
    } catch {
      return false;
    }
  }
}
