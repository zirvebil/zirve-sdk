<?php

declare(strict_types=1);

namespace Zirve\OnCall;

use GuzzleHttp\Client;

/**
 * Zirve OnCall Manager — Grafana OnCall API.
 */
final class OnCallManager
{
    private Client $http;

    public function __construct(array $config)
    {
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 5,
            'headers'  => ['Authorization' => ($config['token'] ?? '')],
        ]);
    }

    public function createAlert(string $title, string $message, string $severity = 'critical'): void
    {
        $this->http->post('api/v1/alert_groups', [
            'json' => ['title' => $title, 'message' => $message, 'severity' => $severity],
        ]);
    }

    public function listSchedules(): array
    {
        $response = $this->http->get('api/v1/schedules');
        return json_decode($response->getBody()->getContents(), true)['results'] ?? [];
    }

    public function listAlertGroups(): array
    {
        $response = $this->http->get('api/v1/alert_groups');
        return json_decode($response->getBody()->getContents(), true)['results'] ?? [];
    }

    public function escalate(string $alertGroupId): void
    {
        $this->http->post("api/v1/alert_groups/{$alertGroupId}/escalate");
    }

    public function acknowledge(string $alertGroupId): void
    {
        $this->http->post("api/v1/alert_groups/{$alertGroupId}/acknowledge");
    }

    public function resolve(string $alertGroupId): void
    {
        $this->http->post("api/v1/alert_groups/{$alertGroupId}/resolve");
    }

    public function health(): bool
    {
        try {
            $r = $this->http->get('api/v1/schedules?perpage=1');
            return $r->getStatusCode() === 200;
        } catch (\Throwable) {
            return false;
        }
    }
}
