<?php

declare(strict_types=1);

namespace Zirve\Error;

use GuzzleHttp\Client;

/**
 * Zirve Error Manager — Sentry.
 *
 * Hata yakalama, exception gönderimi, breadcrumb, kullanıcı context.
 */
final class ErrorManager
{
    private Client $http;
    private string $dsn;
    private ?array $user = null;

    /** @var list<array<string, mixed>> */
    private array $breadcrumbs = [];

    public function __construct(array $config)
    {
        $this->dsn = $config['dsn'] ?? '';
        $parsed = $this->parseDsn($this->dsn);

        $this->http = new Client([
            'base_uri' => $parsed['base_uri'],
            'timeout'  => 5,
        ]);
    }

    /**
     * Exception yakala ve Sentry'ye gönder.
     */
    public function captureException(\Throwable $exception, array $extra = []): ?string
    {
        return $this->send([
            'exception' => [
                'values' => [[
                    'type'       => get_class($exception),
                    'value'      => $exception->getMessage(),
                    'stacktrace' => $this->buildStacktrace($exception),
                ]],
            ],
            'level' => 'error',
            'extra' => $extra,
        ]);
    }

    /**
     * Mesaj gönder.
     */
    public function capture(string $message, string $level = 'info', array $extra = []): ?string
    {
        return $this->send([
            'message' => ['formatted' => $message],
            'level'   => $level,
            'extra'   => $extra,
        ]);
    }

    /**
     * Kullanıcı context ayarla.
     */
    public function setUser(array $user): void
    {
        $this->user = $user;
    }

    /**
     * Breadcrumb ekle.
     */
    public function breadcrumb(string $message, string $category = 'default', array $data = []): void
    {
        $this->breadcrumbs[] = [
            'type'      => 'default',
            'category'  => $category,
            'message'   => $message,
            'data'      => $data,
            'timestamp' => time(),
        ];

        // Max 100 breadcrumb tut
        if (count($this->breadcrumbs) > 100) {
            array_shift($this->breadcrumbs);
        }
    }

    private function send(array $event): ?string
    {
        if (empty($this->dsn)) {
            return null;
        }

        $eventId = str_replace('-', '', \Ramsey\Uuid\Uuid::uuid4()->toString() ?? bin2hex(random_bytes(16)));
        $parsed = $this->parseDsn($this->dsn);

        $event['event_id']  = $eventId;
        $event['timestamp'] = gmdate('Y-m-d\TH:i:s\Z');
        $event['platform']  = 'php';
        $event['sdk']       = ['name' => 'zirve-sdk', 'version' => '0.1.0'];

        if ($this->user) {
            $event['user'] = $this->user;
        }
        if ($this->breadcrumbs) {
            $event['breadcrumbs'] = ['values' => $this->breadcrumbs];
        }

        try {
            $this->http->post("api/{$parsed['project_id']}/store/", [
                'headers' => [
                    'X-Sentry-Auth' => "Sentry sentry_version=7, sentry_key={$parsed['public_key']}, sentry_client=zirve-sdk/0.1.0",
                    'Content-Type'  => 'application/json',
                ],
                'json' => $event,
            ]);

            return $eventId;
        } catch (\Throwable) {
            return null;
        }
    }

    private function buildStacktrace(\Throwable $e): array
    {
        $frames = [];
        foreach ($e->getTrace() as $frame) {
            $frames[] = [
                'filename' => $frame['file'] ?? 'unknown',
                'lineno'   => $frame['line'] ?? 0,
                'function' => $frame['function'] ?? 'unknown',
                'module'   => $frame['class'] ?? null,
            ];
        }

        return ['frames' => array_reverse($frames)];
    }

    /**
     * @return array{base_uri: string, public_key: string, project_id: string}
     */
    private function parseDsn(string $dsn): array
    {
        if (empty($dsn)) {
            return ['base_uri' => 'http://localhost/', 'public_key' => '', 'project_id' => '0'];
        }

        $parsed = parse_url($dsn);
        $scheme = $parsed['scheme'] ?? 'http';
        $host = $parsed['host'] ?? 'localhost';
        $port = isset($parsed['port']) ? ":{$parsed['port']}" : '';
        $projectId = ltrim($parsed['path'] ?? '/0', '/');
        $publicKey = $parsed['user'] ?? '';

        return [
            'base_uri'   => "{$scheme}://{$host}{$port}/",
            'public_key' => $publicKey,
            'project_id' => $projectId,
        ];
    }

    public function health(): bool
    {
        if (empty($this->dsn)) {
            return false;
        }

        try {
            $parsed = $this->parseDsn($this->dsn);
            $response = $this->http->get("api/{$parsed['project_id']}/store/");

            return $response->getStatusCode() < 500;
        } catch (\Throwable) {
            return false;
        }
    }
}
