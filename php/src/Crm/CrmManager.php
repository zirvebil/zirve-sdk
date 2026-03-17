<?php

declare(strict_types=1);

namespace Zirve\Crm;

use GuzzleHttp\Client;

/**
 * Zirve CRM Manager — Odoo (JSON-RPC).
 */
final class CrmManager
{
    private Client $http;
    private array $config;
    private ?int $uid = null;

    public function __construct(array $config)
    {
        $this->config = $config;
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 10,
        ]);
    }

    public function createContact(array $data): int
    {
        return $this->call('res.partner', 'create', [$data]);
    }

    public function createLead(array $data): int
    {
        return $this->call('crm.lead', 'create', [$data]);
    }

    public function createTicket(array $data): int
    {
        return $this->call('helpdesk.ticket', 'create', [$data]);
    }

    public function syncCustomer(string $externalId, array $data): int
    {
        $existing = $this->search('res.partner', [['x_external_id', '=', $externalId]]);
        if (!empty($existing)) {
            $this->call('res.partner', 'write', [$existing, $data]);
            return $existing[0];
        }
        $data['x_external_id'] = $externalId;
        return $this->createContact($data);
    }

    public function search(string $model, array $domain, int $limit = 100): array
    {
        return $this->call($model, 'search', [$domain], ['limit' => $limit]);
    }

    public function read(string $model, array $ids, array $fields = []): array
    {
        return $this->call($model, 'read', [$ids, $fields]);
    }

    private function call(string $model, string $method, array $args = [], array $kwargs = []): mixed
    {
        $uid = $this->authenticate();
        $db = $this->config['database'] ?? 'odoo';
        $password = $this->config['api_key'] ?? $this->config['password'] ?? '';

        $response = $this->http->post('jsonrpc', [
            'json' => [
                'jsonrpc' => '2.0',
                'method'  => 'call',
                'params'  => [
                    'service' => 'object',
                    'method'  => 'execute_kw',
                    'args'    => [$db, $uid, $password, $model, $method, $args, (object) $kwargs],
                ],
            ],
        ]);

        $data = json_decode($response->getBody()->getContents(), true);
        if (isset($data['error'])) {
            throw new \RuntimeException('Odoo error: ' . ($data['error']['data']['message'] ?? 'unknown'));
        }

        return $data['result'] ?? null;
    }

    private function authenticate(): int
    {
        if ($this->uid !== null) return $this->uid;

        $response = $this->http->post('jsonrpc', [
            'json' => [
                'jsonrpc' => '2.0',
                'method'  => 'call',
                'params'  => [
                    'service' => 'common',
                    'method'  => 'authenticate',
                    'args'    => [
                        $this->config['database'] ?? 'odoo',
                        $this->config['username'] ?? 'admin',
                        $this->config['api_key'] ?? $this->config['password'] ?? '',
                        [],
                    ],
                ],
            ],
        ]);

        $data = json_decode($response->getBody()->getContents(), true);
        $this->uid = $data['result'] ?? throw new \RuntimeException('Odoo authentication failed');

        return $this->uid;
    }

    public function health(): bool
    {
        try { $r = $this->http->get('web/database/list'); return $r->getStatusCode() === 200; }
        catch (\Throwable) { return false; }
    }
}
