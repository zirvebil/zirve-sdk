<?php

declare(strict_types=1);

namespace Zirve\Remote;

use GuzzleHttp\Client;

/**
 * Zirve Remote Manager — Apache Guacamole.
 */
final class RemoteManager
{
    private Client $http;
    private array $config;
    private ?string $token = null;

    public function __construct(array $config)
    {
        $this->config = $config;
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/api/',
            'timeout'  => 10,
        ]);
    }

    public function createConnection(array $data): array
    {
        $response = $this->http->post('session/data/postgresql/connections', [
            'query' => ['token' => $this->getToken()],
            'json'  => [
                'name'             => $data['name'] ?? 'connection-' . time(),
                'protocol'         => $data['protocol'] ?? 'rdp',
                'parameters'       => [
                    'hostname' => $data['hostname'] ?? '',
                    'port'     => (string) ($data['port'] ?? 3389),
                    'username' => $data['username'] ?? '',
                    'password' => $data['password'] ?? '',
                ],
                'parentIdentifier' => 'ROOT',
            ],
        ]);

        return json_decode($response->getBody()->getContents(), true);
    }

    public function getSession(string $connectionId): string
    {
        return rtrim($this->config['url'] ?? '', '/') . '/#/client/' . base64_encode("c/{$connectionId}");
    }

    public function listConnections(): array
    {
        $response = $this->http->get('session/data/postgresql/connections', [
            'query' => ['token' => $this->getToken()],
        ]);
        return json_decode($response->getBody()->getContents(), true);
    }

    public function deleteConnection(string $id): void
    {
        $this->http->delete("session/data/postgresql/connections/{$id}", [
            'query' => ['token' => $this->getToken()],
        ]);
    }

    public function activeConnections(): array
    {
        $response = $this->http->get('session/data/postgresql/activeConnections', [
            'query' => ['token' => $this->getToken()],
        ]);
        return json_decode($response->getBody()->getContents(), true);
    }

    private function getToken(): string
    {
        if ($this->token !== null) {
            return $this->token;
        }

        $response = $this->http->post('tokens', [
            'form_params' => [
                'username' => $this->config['username'] ?? 'guacadmin',
                'password' => $this->config['password'] ?? 'guacadmin',
            ],
        ]);

        $data = json_decode($response->getBody()->getContents(), true);
        $this->token = $data['authToken'] ?? throw new \RuntimeException('Guacamole auth failed');

        return $this->token;
    }

    public function health(): bool
    {
        try {
            $this->getToken();
            return true;
        } catch (\Throwable) {
            return false;
        }
    }
}
