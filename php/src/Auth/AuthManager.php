<?php

declare(strict_types=1);

namespace Zirve\Auth;

use GuzzleHttp\Client;

/**
 * Zirve Auth Manager — Keycloak OIDC.
 *
 * Token doğrulama, kullanıcı yönetimi, rol kontrolü.
 */
final class AuthManager
{
    private Client $http;

    /** @var array<string, mixed> */
    private array $config;

    private ?string $adminToken = null;

    public function __construct(array $config)
    {
        $this->config = $config;
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 5,
        ]);
    }

    /**
     * Bearer token doğrula (JWT introspection).
     *
     * @return array<string, mixed>  Kullanıcı bilgileri
     */
    public function verifyToken(string $token): array
    {
        $realm = $this->config['realm'] ?? 'zirve';
        $response = $this->http->post("realms/{$realm}/protocol/openid-connect/token/introspect", [
            'form_params' => [
                'token'         => $token,
                'client_id'     => $this->config['client_id'] ?? '',
                'client_secret' => $this->config['client_secret'] ?? '',
            ],
        ]);

        $data = json_decode($response->getBody()->getContents(), true);

        if (!($data['active'] ?? false)) {
            throw new \RuntimeException('Token geçersiz veya süresi dolmuş');
        }

        return $data;
    }

    /**
     * Token'dan kullanıcı bilgisi al (userinfo endpoint).
     *
     * @return array<string, mixed>
     */
    public function getUser(string $token): array
    {
        $realm = $this->config['realm'] ?? 'zirve';
        $response = $this->http->get("realms/{$realm}/protocol/openid-connect/userinfo", [
            'headers' => ['Authorization' => "Bearer {$token}"],
        ]);

        return json_decode($response->getBody()->getContents(), true);
    }

    /**
     * Kullanıcının belirli bir rolü var mı?
     */
    public function hasRole(string $token, string $role): bool
    {
        $data = $this->verifyToken($token);
        $realmRoles = $data['realm_access']['roles'] ?? [];
        $clientRoles = [];

        foreach ($data['resource_access'] ?? [] as $client) {
            $clientRoles = array_merge($clientRoles, $client['roles'] ?? []);
        }

        return in_array($role, array_merge($realmRoles, $clientRoles), true);
    }

    /**
     * Yeni kullanıcı oluştur (Admin API).
     *
     * @param array<string, mixed> $userData
     */
    public function createUser(array $userData): string
    {
        $realm = $this->config['realm'] ?? 'zirve';
        $response = $this->http->post("admin/realms/{$realm}/users", [
            'headers' => ['Authorization' => "Bearer {$this->getAdminToken()}"],
            'json'    => [
                'username'  => $userData['username'] ?? '',
                'email'     => $userData['email'] ?? '',
                'firstName' => $userData['firstName'] ?? '',
                'lastName'  => $userData['lastName'] ?? '',
                'enabled'   => $userData['enabled'] ?? true,
                'credentials' => isset($userData['password']) ? [[
                    'type'      => 'password',
                    'value'     => $userData['password'],
                    'temporary' => $userData['temporaryPassword'] ?? false,
                ]] : [],
            ],
        ]);

        $location = $response->getHeader('Location')[0] ?? '';

        return basename($location); // User ID
    }

    /**
     * Kullanıcıya rol ata.
     */
    public function assignRole(string $userId, string $roleName): void
    {
        $realm = $this->config['realm'] ?? 'zirve';
        $token = $this->getAdminToken();

        // Rol ID'sini bul
        $response = $this->http->get("admin/realms/{$realm}/roles/{$roleName}", [
            'headers' => ['Authorization' => "Bearer {$token}"],
        ]);
        $role = json_decode($response->getBody()->getContents(), true);

        // Rolü ata
        $this->http->post("admin/realms/{$realm}/users/{$userId}/role-mappings/realm", [
            'headers' => ['Authorization' => "Bearer {$token}"],
            'json'    => [$role],
        ]);
    }

    /**
     * Admin API token al (client credentials).
     */
    private function getAdminToken(): string
    {
        if ($this->adminToken !== null) {
            return $this->adminToken;
        }

        $response = $this->http->post('realms/master/protocol/openid-connect/token', [
            'form_params' => [
                'grant_type'    => 'client_credentials',
                'client_id'     => $this->config['client_id'] ?? '',
                'client_secret' => $this->config['client_secret'] ?? '',
            ],
        ]);

        $data = json_decode($response->getBody()->getContents(), true);
        $this->adminToken = $data['access_token'];

        return $this->adminToken;
    }

    public function health(): bool
    {
        try {
            $realm = $this->config['realm'] ?? 'zirve';
            $response = $this->http->get("realms/{$realm}/.well-known/openid-configuration");

            return $response->getStatusCode() === 200;
        } catch (\Throwable) {
            return false;
        }
    }
}
