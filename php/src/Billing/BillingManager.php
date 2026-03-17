<?php

declare(strict_types=1);

namespace Zirve\Billing;

use GuzzleHttp\Client;

/**
 * Zirve Billing Manager — Lago.
 *
 * Abonelik ve kullanım tabanlı faturalandırma.
 */
final class BillingManager
{
    private Client $http;

    public function __construct(array $config)
    {
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 10,
            'headers'  => ['Authorization' => 'Bearer ' . ($config['api_key'] ?? '')],
        ]);
    }

    public function createCustomer(array $data): array
    {
        $response = $this->http->post('api/v1/customers', ['json' => ['customer' => $data]]);
        return json_decode($response->getBody()->getContents(), true)['customer'] ?? [];
    }

    public function getCustomer(string $externalId): array
    {
        $response = $this->http->get("api/v1/customers/{$externalId}");
        return json_decode($response->getBody()->getContents(), true)['customer'] ?? [];
    }

    public function createSubscription(string $customerId, string $planCode, array $params = []): array
    {
        $response = $this->http->post('api/v1/subscriptions', [
            'json' => ['subscription' => array_merge([
                'external_customer_id' => $customerId,
                'plan_code'            => $planCode,
            ], $params)],
        ]);
        return json_decode($response->getBody()->getContents(), true)['subscription'] ?? [];
    }

    public function addUsage(string $subscriptionId, string $eventCode, array $properties = []): void
    {
        $this->http->post('api/v1/events', [
            'json' => ['event' => [
                'transaction_id'            => bin2hex(random_bytes(16)),
                'external_subscription_id'  => $subscriptionId,
                'code'                      => $eventCode,
                'properties'                => (object) $properties,
                'timestamp'                 => time(),
            ]],
        ]);
    }

    public function invoices(string $customerId): array
    {
        $response = $this->http->get('api/v1/invoices', ['query' => ['external_customer_id' => $customerId]]);
        return json_decode($response->getBody()->getContents(), true)['invoices'] ?? [];
    }

    public function health(): bool
    {
        try {
            $r = $this->http->get('api/v1/customers?per_page=1');
            return $r->getStatusCode() === 200;
        } catch (\Throwable) {
            return false;
        }
    }
}
