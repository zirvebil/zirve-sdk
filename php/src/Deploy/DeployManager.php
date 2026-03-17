<?php

declare(strict_types=1);

namespace Zirve\Deploy;

use GuzzleHttp\Client;

/**
 * Zirve Deploy Manager — ArgoCD API.
 */
final class DeployManager
{
    private Client $http;
    private array $config;
    private ?string $token = null;

    public function __construct(array $config)
    {
        $this->config = $config;
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 10,
            'verify'   => false,
        ]);
    }

    public function listApps(): array
    {
        $response = $this->authedRequest('GET', 'api/v1/applications');
        return json_decode($response->getBody()->getContents(), true)['items'] ?? [];
    }

    public function status(string $appName): array
    {
        $response = $this->authedRequest('GET', "api/v1/applications/{$appName}");
        $data = json_decode($response->getBody()->getContents(), true);
        return [
            'name'       => $data['metadata']['name'] ?? '',
            'syncStatus' => $data['status']['sync']['status'] ?? 'Unknown',
            'health'     => $data['status']['health']['status'] ?? 'Unknown',
            'revision'   => $data['status']['sync']['revision'] ?? '',
        ];
    }

    public function sync(string $appName, ?string $revision = null): void
    {
        $body = [];
        if ($revision) {
            $body['revision'] = $revision;
        }
        $this->authedRequest('POST', "api/v1/applications/{$appName}/sync", $body);
    }

    public function rollback(string $appName, int $deploymentId): void
    {
        $this->authedRequest('POST', "api/v1/applications/{$appName}/rollback", ['id' => $deploymentId]);
    }

    public function history(string $appName): array
    {
        $response = $this->authedRequest('GET', "api/v1/applications/{$appName}");
        $data = json_decode($response->getBody()->getContents(), true);
        return $data['status']['history'] ?? [];
    }

    private function authedRequest(string $method, string $uri, array $body = []): \Psr\Http\Message\ResponseInterface
    {
        $headers = ['Authorization' => 'Bearer ' . $this->getToken()];
        $options = ['headers' => $headers];
        if ($body) {
            $options['json'] = $body;
        }
        return $this->http->request($method, $uri, $options);
    }

    private function getToken(): string
    {
        if ($this->token) {
            return $this->token;
        }
        $this->token = $this->config['token'] ?? '';
        if ($this->token) {
            return $this->token;
        }

        $response = $this->http->post('api/v1/session', [
            'json' => ['username' => $this->config['username'] ?? 'admin', 'password' => $this->config['password'] ?? ''],
        ]);
        $data = json_decode($response->getBody()->getContents(), true);
        $this->token = $data['token'] ?? '';

        return $this->token;
    }

    public function health(): bool
    {
        try {
            $r = $this->http->get('healthz');
            return str_contains($r->getBody()->getContents(), 'ok');
        } catch (\Throwable) {
            return false;
        }
    }
}
