<?php

declare(strict_types=1);

namespace Zirve\Quality;

use GuzzleHttp\Client;

/**
 * Zirve Quality Manager — SonarQube Web API.
 */
final class QualityManager
{
    private Client $http;

    public function __construct(array $config)
    {
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 10,
            'auth'     => [$config['token'] ?? '', ''],
        ]);
    }

    public function qualityGate(string $projectKey): array
    {
        $response = $this->http->get('api/qualitygates/project_status', ['query' => ['projectKey' => $projectKey]]);
        return json_decode($response->getBody()->getContents(), true)['projectStatus'] ?? [];
    }

    public function issues(string $projectKey, array $params = []): array
    {
        $query = array_merge(['componentKeys' => $projectKey, 'ps' => 100], $params);
        $response = $this->http->get('api/issues/search', ['query' => $query]);
        return json_decode($response->getBody()->getContents(), true);
    }

    public function metrics(string $projectKey, array $metricKeys = ['bugs', 'vulnerabilities', 'code_smells', 'coverage']): array
    {
        $response = $this->http->get('api/measures/component', [
            'query' => ['component' => $projectKey, 'metricKeys' => implode(',', $metricKeys)],
        ]);
        $data = json_decode($response->getBody()->getContents(), true);
        $result = [];
        foreach ($data['component']['measures'] ?? [] as $m) {
            $result[$m['metric']] = $m['value'] ?? null;
        }
        return $result;
    }

    public function listProjects(): array
    {
        $response = $this->http->get('api/projects/search');
        return json_decode($response->getBody()->getContents(), true)['components'] ?? [];
    }

    public function health(): bool
    {
        try { $r = $this->http->get('api/system/status'); $d = json_decode($r->getBody()->getContents(), true); return ($d['status'] ?? '') === 'UP'; }
        catch (\Throwable) { return false; }
    }
}
