/**
 * Zirve CRM Manager — Odoo Integration.
 *
 * Interacts with Odoo via JSON-RPC endpoint.
 */
export class CrmManager {
  private config: Record<string, any>;
  private url: string;
  private db: string;
  private uid: number | null = null;
  private sessionContext: Record<string, any> = {};
  
  // Track JSON-RPC id
  private rpcId = 0;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://odoo.zirve-infra.svc.cluster.local:8069').replace(/\/$/, '');
    this.db = config.database || 'zirve';
  }

  /**
   * Internal JSON-RPC executor.
   */
  private async rpc(method: string, params: Record<string, any>): Promise<any> {
    this.rpcId++;
    
    const response = await fetch(`${this.url}/jsonrpc`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        jsonrpc: '2.0',
        method,
        params,
        id: this.rpcId
      })
    });

    if (!response.ok) {
      throw new Error(`Odoo RPC failed HTTP ${response.status}`);
    }

    const data = await response.json();
    if (data.error) {
      throw new Error(`Odoo RPC Error: ${data.error.message || JSON.stringify(data.error)}`);
    }

    return data.result;
  }

  /**
   * Authenticate and get a session.
   */
  private async authenticate(): Promise<number> {
    if (this.uid !== null) return this.uid;
    
    const user = this.config.username || 'admin';
    const password = this.config.password || 'admin';

    const result = await this.rpc('call', {
      service: 'common',
      method: 'authenticate',
      args: [this.db, user, password, {}]
    });

    if (!result) {
      throw new Error('Odoo Authentication failed.');
    } // result is the UID

    this.uid = result;
    return result;
  }

  /**
   * Execute an ORM method contextually.
   */
  private async executeKw(model: string, method: string, args: any[], kwargs: Record<string, any> = {}): Promise<any> {
    const uid = await this.authenticate();
    const password = this.config.password || 'admin';

    return await this.rpc('call', {
      service: 'object',
      method: 'execute_kw',
      args: [this.db, uid, password, model, method, args, kwargs]
    });
  }

  /**
   * Create a contact (partner) in Odoo.
   */
  public async createContact(contactData: Record<string, any>): Promise<number> {
    return await this.executeKw('res.partner', 'create', [contactData]);
  }

  /**
   * Create a CRM lead.
   */
  public async createLead(leadData: Record<string, any>): Promise<number> {
    return await this.executeKw('crm.lead', 'create', [leadData]);
  }

  /**
   * Create a helpdesk ticket.
   */
  public async createTicket(ticketData: Record<string, any>): Promise<number> {
    return await this.executeKw('helpdesk.ticket', 'create', [ticketData]);
  }

  /**
   * Sync a Zirve customer to Odoo. Returns partner ID.
   */
  public async syncCustomer(id: string, name: string, email: string, isCompany: boolean = true): Promise<number> {
    // Check if exists
    const existing = await this.executeKw('res.partner', 'search', [[['ref', '=', id]]]);
    
    const partnerData = {
      name,
      email,
      ref: id,
      is_company: isCompany
    };

    if (existing && existing.length > 0) {
      await this.executeKw('res.partner', 'write', [[existing[0]], partnerData]);
      return existing[0] as number;
    }

    return await this.createContact(partnerData);
  }

  /**
   * Basic Odoo health check.
   */
  public async health(): Promise<boolean> {
    try {
      const result = await this.rpc('call', {
        service: 'common',
        method: 'version',
        args: []
      });
      return !!result;
    } catch {
      return false;
    }
  }
}
