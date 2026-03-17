<?php

declare(strict_types=1);

namespace Zirve\Registry;

use GuzzleHttp\Client;

/**
 * Zirve Registry Manager — Harbor v2 API.
 */
final class RegistryManager
{
    private Client $http;

    public function __construct(array $config)
    {
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 10,
            'auth'     => [$config['username'] ?? 'admin', $config['password'] ?? ''],
        ]);
    }

    public function listProjects(): array
    {
        $response = $this->http->get('api/v2.0/projects');
        return json_decode($response->getBody()->getContents(), true);
    }

    public function listImages(string $project): array
    {
        $response = $this->http->get("api/v2.0/projects/{$project}/repositories");
        return json_decode($response->getBody()->getContents(), true);
    }

    public function listTags(string $project, string $repository): array
    {
        $repo = urlencode($repository);
        $response = $this->http->get("api/v2.0/projects/{$project}/repositories/{$repo}/artifacts");
        return json_decode($response->getBody()->getContents(), true);
    }

    public function scanImage(string $project, string $repository, string $reference = 'latest'): void
    {
        $repo = urlencode($repository);
        $this->http->post("api/v2.0/projects/{$project}/repositories/{$repo}/artifacts/{$reference}/scan");
    }

    public function scanReport(string $project, string $repository, string $reference = 'latest'): array
    {
        $repo = urlencode($repository);
        $response = $this->http->get("api/v2.0/projects/{$project}/repositories/{$repo}/artifacts/{$reference}?with_scan_overview=true");
        return json_decode($response->getBody()->getContents(), true);
    }

    public function deleteImage(string $project, string $repository, string $reference): void
    {
        $repo = urlencode($repository);
        $this->http->delete("api/v2.0/projects/{$project}/repositories/{$repo}/artifacts/{$reference}");
    }

    public function health(): bool
    {
        try { $r = $this->http->get('api/v2.0/health'); return $r->getStatusCode() === 200; }
        catch (\Throwable) { return false; }
    }
}
