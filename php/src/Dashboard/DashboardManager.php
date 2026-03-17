<?php

declare(strict_types=1);

namespace Zirve\Dashboard;

use GuzzleHttp\Client;

/**
 * Zirve Dashboard Manager — Grafana HTTP API.
 */
final class DashboardManager
{
    private Client $http;

    public function __construct(array $config)
    {
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 10,
            'headers'  => ['Authorization' => 'Bearer ' . ($config['api_key'] ?? ($config['token'] ?? ''))],
        ]);
    }

    public function createDashboard(array $dashboard, bool $overwrite = true): array
    {
        $response = $this->http->post('api/dashboards/db', [
            'json' => ['dashboard' => $dashboard, 'overwrite' => $overwrite],
        ]);
        return json_decode($response->getBody()->getContents(), true);
    }

    public function getDashboard(string $uid): array
    {
        $response = $this->http->get("api/dashboards/uid/{$uid}");
        return json_decode($response->getBody()->getContents(), true);
    }

    public function listDashboards(string $type = 'dash-db'): array
    {
        $response = $this->http->get('api/search', ['query' => ['type' => $type]]);
        return json_decode($response->getBody()->getContents(), true);
    }

    public function deleteDashboard(string $uid): void
    {
        $this->http->delete("api/dashboards/uid/{$uid}");
    }

    public function listDatasources(): array
    {
        $response = $this->http->get('api/datasources');
        return json_decode($response->getBody()->getContents(), true);
    }

    public function createDatasource(array $datasource): array
    {
        $response = $this->http->post('api/datasources', ['json' => $datasource]);
        return json_decode($response->getBody()->getContents(), true);
    }

    public function health(): bool
    {
        try { $r = $this->http->get('api/health'); return $r->getStatusCode() === 200; }
        catch (\Throwable) { return false; }
    }
}
