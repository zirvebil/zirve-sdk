<?php

declare(strict_types=1);

namespace Zirve\Ingress;

use GuzzleHttp\Client;

/**
 * Zirve Ingress Manager — Traefik API.
 */
final class IngressManager
{
    private Client $http;

    public function __construct(array $config)
    {
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 5,
        ]);
    }

    public function routes(): array
    {
        $response = $this->http->get('api/http/routers');
        return json_decode($response->getBody()->getContents(), true);
    }

    public function middlewares(): array
    {
        $response = $this->http->get('api/http/middlewares');
        return json_decode($response->getBody()->getContents(), true);
    }

    public function services(): array
    {
        $response = $this->http->get('api/http/services');
        return json_decode($response->getBody()->getContents(), true);
    }

    public function entrypoints(): array
    {
        $response = $this->http->get('api/entrypoints');
        return json_decode($response->getBody()->getContents(), true);
    }

    public function health(): bool
    {
        try { $r = $this->http->get('ping'); return $r->getStatusCode() === 200; }
        catch (\Throwable) { return false; }
    }
}
