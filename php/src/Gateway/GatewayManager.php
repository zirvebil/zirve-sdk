<?php

declare(strict_types=1);

namespace Zirve\Gateway;

use GuzzleHttp\Client;

/**
 * Zirve Gateway Manager — Kong Admin API.
 */
final class GatewayManager
{
    private Client $http;

    public function __construct(array $config)
    {
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 5,
        ]);
    }

    public function addRoute(string $name, array $paths, string $serviceId, array $methods = ['GET', 'POST']): array
    {
        $response = $this->http->post('routes', [
            'json' => ['name' => $name, 'paths' => $paths, 'methods' => $methods, 'service' => ['id' => $serviceId]],
        ]);
        return json_decode($response->getBody()->getContents(), true);
    }

    public function addService(string $name, string $url): array
    {
        $response = $this->http->post('services', ['json' => ['name' => $name, 'url' => $url]]);
        return json_decode($response->getBody()->getContents(), true);
    }

    public function addPlugin(string $name, array $config = [], ?string $serviceId = null): array
    {
        $body = ['name' => $name, 'config' => (object) $config];
        if ($serviceId) $body['service'] = ['id' => $serviceId];
        $response = $this->http->post('plugins', ['json' => $body]);
        return json_decode($response->getBody()->getContents(), true);
    }

    public function rateLimit(string $serviceId, int $minute = 60, int $hour = 1000): array
    {
        return $this->addPlugin('rate-limiting', ['minute' => $minute, 'hour' => $hour, 'policy' => 'local'], $serviceId);
    }

    public function consumers(): array
    {
        $response = $this->http->get('consumers');
        return json_decode($response->getBody()->getContents(), true)['data'] ?? [];
    }

    public function listRoutes(): array
    {
        $response = $this->http->get('routes');
        return json_decode($response->getBody()->getContents(), true)['data'] ?? [];
    }

    public function listServices(): array
    {
        $response = $this->http->get('services');
        return json_decode($response->getBody()->getContents(), true)['data'] ?? [];
    }

    public function health(): bool
    {
        try { $r = $this->http->get('status'); return $r->getStatusCode() === 200; }
        catch (\Throwable) { return false; }
    }
}
