<?php

declare(strict_types=1);

namespace Zirve\Testing;

use GuzzleHttp\Client;

/**
 * Zirve Testing Manager — Keploy API.
 */
final class TestingManager
{
    private Client $http;

    public function __construct(array $config)
    {
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 10,
        ]);
    }

    public function record(string $appName): array
    {
        $response = $this->http->post('api/regression/start', [
            'json' => ['app' => $appName, 'mode' => 'record'],
        ]);
        return json_decode($response->getBody()->getContents(), true);
    }

    public function replay(string $appName): array
    {
        $response = $this->http->post('api/regression/start', [
            'json' => ['app' => $appName, 'mode' => 'test'],
        ]);
        return json_decode($response->getBody()->getContents(), true);
    }

    public function listTests(string $appName): array
    {
        $response = $this->http->get("api/regression/testcase", ['query' => ['app' => $appName]]);
        return json_decode($response->getBody()->getContents(), true);
    }

    public function testResults(string $appName): array
    {
        $response = $this->http->get("api/regression/testrun", ['query' => ['app' => $appName]]);
        return json_decode($response->getBody()->getContents(), true);
    }

    public function health(): bool
    {
        try { $r = $this->http->get('healthz'); return $r->getStatusCode() === 200; }
        catch (\Throwable) { return false; }
    }
}
