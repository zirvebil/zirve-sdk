/**
 * Zirve Billing Manager — Lago Integration.
 *
 * Lightweight wrapper around Lago HTTP API for usage-based billing.
 */
export class BillingManager {
  private config: Record<string, any>;
  private url: string;
  private apiKey: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://lago-api.zirve-infra.svc.cluster.local').replace(/\/$/, '');
    this.apiKey = config.api_key || '';
  }

  /**
   * Internal wrapper for fetch requests to Lago.
   */
  private async request(method: string, path: string, body?: any): Promise<any> {
    const url = `${this.url}/api/v1/${path.replace(/^\/+/, '')}`;
    const options: RequestInit = {
      method,
      headers: {
        'Authorization': `Bearer ${this.apiKey}`,
        'Content-Type': 'application/json',
      },
    };

    if (body) {
      options.body = JSON.stringify(body);
    }

    const response = await fetch(url, options);

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Lago request failed [${response.status}]: ${errorText}`);
    }

    // Handle empty 200/204 responses
    if (response.status === 204 || response.headers.get('content-length') === '0') {
      return null;
    }

    return await response.json();
  }

  /**
   * Create a new customer in Lago.
   */
  public async createCustomer(customerData: Record<string, any>): Promise<any> {
    const data = await this.request('POST', 'customers', { customer: customerData });
    return data.customer;
  }

  /**
   * Create a subscription for a customer.
   */
  public async createSubscription(subscriptionData: Record<string, any>): Promise<any> {
    const data = await this.request('POST', 'subscriptions', { subscription: subscriptionData });
    return data.subscription;
  }

  /**
   * Send a usage event to Lago for a specific customer/subscription.
   */
  public async addUsage(eventData: Record<string, any>): Promise<any> {
    const data = await this.request('POST', 'events', { event: eventData });
    return data.event;
  }

  /**
   * List invoices for a specific customer.
   */
  public async invoices(externalCustomerId: string): Promise<any[]> {
    const data = await this.request('GET', `invoices?external_customer_id=${encodeURIComponent(externalCustomerId)}`);
    return data.invoices || [];
  }

  /**
   * Basic Lago health check.
   */
  public async health(): Promise<boolean> {
    try {
      if (!this.apiKey) return false;
      const response = await fetch(`${this.url}/api/v1/plans`, {
        headers: { 'Authorization': `Bearer ${this.apiKey}` },
      });
      return response.ok;
    } catch {
      return false;
    }
  }
}
