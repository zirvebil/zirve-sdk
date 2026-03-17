<?php

declare(strict_types=1);

namespace Zirve\Cluster;

use GuzzleHttp\Client;

/**
 * Zirve Cluster Manager — Rancher v3 API.
 */
final class ClusterManager
{
    private Client $http;

    public function __construct(array $config)
    {
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 10,
            'verify'   => false,
            'headers'  => ['Authorization' => 'Bearer ' . ($config['token'] ?? '')],
        ]);
    }

    public function listClusters(): array
    {
        $response = $this->http->get('v3/clusters');
        return json_decode($response->getBody()->getContents(), true)['data'] ?? [];
    }

    public function clusterStatus(string $clusterId): array
    {
        $response = $this->http->get("v3/clusters/{$clusterId}");
        $data = json_decode($response->getBody()->getContents(), true);
        return [
            'name'  => $data['name'] ?? '',
            'state' => $data['state'] ?? 'unknown',
            'nodes' => $data['nodeCount'] ?? 0,
            'provider' => $data['provider'] ?? '',
        ];
    }

    public function nodeStatus(string $clusterId): array
    {
        $response = $this->http->get("v3/clusters/{$clusterId}/nodes");
        return json_decode($response->getBody()->getContents(), true)['data'] ?? [];
    }

    public function health(): bool
    {
        try {
            $r = $this->http->get('healthz');
            return $r->getStatusCode() === 200;
        } catch (\Throwable) {
            return false;
        }
    }
}
