/**
 * Zirve Quality Manager — SonarQube
 *
 * Integrates with SonarQube Web API for retrieving code quality metrics.
 */
export class QualityManager {
  private config: Record<string, any>;
  private url: string;
  private token: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.url = (config.url || 'http://sonarqube-sonarqube.zirve-infra.svc.cluster.local:9000').replace(/\/$/, '') + '/api';
    this.token = config.token || '';
  }

  private async request(method: string, path: string, params: Record<string, string> = {}): Promise<any> {
    const url = new URL(`${this.url}/${path.replace(/^\/+/, '')}`);
    for (const [k, v] of Object.entries(params)) {
      url.searchParams.append(k, v);
    }

    const options: RequestInit = {
      method,
      headers: {
        'Authorization': `Bearer ${this.token}`,
        'Content-Type': 'application/json'
        // Alternative basic auth based on SQ version: 'Basic ' + Buffer.from(`${this.token}:`).toString('base64')
      }
    };

    const response = await fetch(url.toString(), options);
    
    if (!response.ok) {
      if (response.status === 404) return null;
      throw new Error(`SonarQube API Error [${response.status}]`);
    }

    if (response.status === 204 || response.headers.get('content-length') === '0') return true;
    return await response.json();
  }

  public async getQualityGate(projectKey: string): Promise<string> {
    const result = await this.request('GET', 'qualitygates/project_status', { projectKey });
    return result?.projectStatus?.status || 'UNKNOWN';
  }

  public async checkPassed(projectKey: string): Promise<boolean> {
    const status = await this.getQualityGate(projectKey);
    return status === 'OK';
  }

  public async getIssues(projectKey: string, severity: string = 'BLOCKER,CRITICAL'): Promise<any[]> {
    const result = await this.request('GET', 'issues/search', {
      componentKeys: projectKey,
      severities: severity
    });
    return result?.issues || [];
  }

  public async getMetrics(projectKey: string): Promise<any> {
    const result = await this.request('GET', 'measures/component', {
      component: projectKey,
      metricKeys: 'bugs,vulnerabilities,code_smells,coverage,duplicated_lines_density'
    });
    
    const formatted: Record<string, any> = {};
    if (result && result.component && result.component.measures) {
      for (const measure of result.component.measures) {
        formatted[measure.metric] = measure.value;
      }
    }
    return formatted;
  }

  public async health(): Promise<boolean> {
    try {
      const response = await fetch(`${this.url}/system/health`);
      const body = await response.json();
      return body.health === 'GREEN';
    } catch {
      return false;
    }
  }
}
