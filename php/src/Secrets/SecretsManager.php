<?php

declare(strict_types=1);

namespace Zirve\Secrets;

use GuzzleHttp\Client;

/**
 * Zirve Secrets Manager — Infisical.
 *
 * Merkezi secret yönetimi: get, list, set, rotate.
 */
final class SecretsManager
{
    private Client $http;

    /** @var array<string, mixed> */
    private array $config;

    /** @var array<string, string> Local cache */
    private array $cache = [];

    public function __construct(array $config)
    {
        $this->config = $config;
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 5,
            'headers'  => [
                'Authorization' => 'Bearer ' . ($config['token'] ?? ''),
                'Content-Type'  => 'application/json',
            ],
        ]);
    }

    /**
     * Tek bir secret değeri al.
     */
    public function get(string $key, ?string $environment = null, ?string $path = null): string
    {
        if (isset($this->cache[$key])) {
            return $this->cache[$key];
        }

        $env = $environment ?? $this->config['environment'] ?? 'dev';
        $query = ['environment' => $env, 'secretName' => $key];
        if ($path !== null) {
            $query['secretPath'] = $path;
        }

        $response = $this->http->get('api/v3/secrets/raw/' . urlencode($key), [
            'query' => $query,
        ]);

        $data = json_decode($response->getBody()->getContents(), true);
        $value = $data['secret']['secretValue'] ?? '';
        $this->cache[$key] = $value;

        return $value;
    }

    /**
     * Tüm secret'ları listele.
     *
     * @return array<string, string>
     */
    public function list(?string $environment = null, ?string $path = null): array
    {
        $env = $environment ?? $this->config['environment'] ?? 'dev';
        $query = ['environment' => $env];
        if ($path !== null) {
            $query['secretPath'] = $path;
        }

        $response = $this->http->get('api/v3/secrets/raw', ['query' => $query]);
        $data = json_decode($response->getBody()->getContents(), true);

        $secrets = [];
        foreach ($data['secrets'] ?? [] as $secret) {
            $name = $secret['secretKey'] ?? '';
            $secrets[$name] = $secret['secretValue'] ?? '';
            $this->cache[$name] = $secrets[$name];
        }

        return $secrets;
    }

    /**
     * Secret oluştur veya güncelle.
     */
    public function set(string $key, string $value, ?string $environment = null): void
    {
        $env = $environment ?? $this->config['environment'] ?? 'dev';

        $this->http->post('api/v3/secrets/raw/' . urlencode($key), [
            'json' => [
                'secretName'  => $key,
                'secretValue' => $value,
                'environment' => $env,
                'type'        => 'shared',
            ],
        ]);

        $this->cache[$key] = $value;
    }

    /**
     * Secret'ı sil.
     */
    public function delete(string $key, ?string $environment = null): void
    {
        $env = $environment ?? $this->config['environment'] ?? 'dev';

        $this->http->delete('api/v3/secrets/raw/' . urlencode($key), [
            'json' => [
                'secretName'  => $key,
                'environment' => $env,
            ],
        ]);

        unset($this->cache[$key]);
    }

    /**
     * Secret rotate — yeni değer set et, eski değeri döndür.
     */
    public function rotate(string $key, string $newValue, ?string $environment = null): string
    {
        $oldValue = $this->get($key, $environment);
        $this->set($key, $newValue, $environment);

        return $oldValue;
    }

    public function health(): bool
    {
        try {
            $response = $this->http->get('api/status');

            return $response->getStatusCode() === 200;
        } catch (\Throwable) {
            return false;
        }
    }
}
