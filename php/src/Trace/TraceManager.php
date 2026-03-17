<?php

declare(strict_types=1);

namespace Zirve\Trace;

use GuzzleHttp\Client;

/**
 * Zirve Trace Manager — OpenTelemetry + Tempo.
 *
 * Distributed tracing: span oluştur, bitir, middleware.
 * OTLP HTTP exporter ile OTel Collector'a gönderir.
 */
final class TraceManager
{
    private Client $http;
    private string $serviceName;

    /** @var list<array<string, mixed>> */
    private array $spans = [];

    private ?string $traceId = null;

    public function __construct(array $config)
    {
        $this->http = new Client([
            'base_uri' => rtrim($config['endpoint'] ?? '', '/') . '/',
            'timeout'  => 5,
        ]);
        $this->serviceName = $config['service_name'] ?? getenv('SERVICE_NAME') ?: 'unknown';
        $this->traceId = bin2hex(random_bytes(16));
    }

    /**
     * Yeni span başlat.
     *
     * @return array{traceId: string, spanId: string, name: string, startTime: int}
     */
    public function startSpan(string $name, ?string $parentSpanId = null, array $attributes = []): array
    {
        $span = [
            'traceId'      => $this->traceId,
            'spanId'       => bin2hex(random_bytes(8)),
            'parentSpanId' => $parentSpanId ?? '',
            'name'         => $name,
            'kind'         => 1, // INTERNAL
            'startTimeUnixNano' => (int) (microtime(true) * 1_000_000_000),
            'endTimeUnixNano'   => 0,
            'attributes'   => $this->formatAttributes($attributes),
            'status'       => ['code' => 0], // UNSET
        ];

        $this->spans[] = $span;

        return [
            'traceId'   => $span['traceId'],
            'spanId'    => $span['spanId'],
            'name'      => $name,
            'startTime' => $span['startTimeUnixNano'],
        ];
    }

    /**
     * Span bitir.
     */
    public function endSpan(string $spanId, ?int $statusCode = null): void
    {
        foreach ($this->spans as &$span) {
            if ($span['spanId'] === $spanId) {
                $span['endTimeUnixNano'] = (int) (microtime(true) * 1_000_000_000);
                if ($statusCode !== null) {
                    $span['status'] = ['code' => $statusCode];
                }
                break;
            }
        }
    }

    /**
     * Süre ölçen wrapper.
     *
     * @template T
     * @param callable(): T $callback
     * @return T
     */
    public function measure(string $name, callable $callback, array $attributes = []): mixed
    {
        $span = $this->startSpan($name, null, $attributes);

        try {
            $result = $callback();
            $this->endSpan($span['spanId'], 1); // OK

            return $result;
        } catch (\Throwable $e) {
            $this->endSpan($span['spanId'], 2); // ERROR
            throw $e;
        }
    }

    /**
     * Tüm span'ları OTel Collector'a gönder (OTLP/HTTP).
     */
    public function flush(): void
    {
        if (empty($this->spans)) {
            return;
        }

        try {
            $this->http->post('v1/traces', [
                'json' => [
                    'resourceSpans' => [[
                        'resource' => [
                            'attributes' => $this->formatAttributes([
                                'service.name' => $this->serviceName,
                            ]),
                        ],
                        'scopeSpans' => [[
                            'scope' => ['name' => 'zirve-sdk', 'version' => '0.1.0'],
                            'spans' => $this->spans,
                        ]],
                    ]],
                ],
            ]);
        } catch (\Throwable) {
            // Tracing gönderimi başarısız — sessizce devam
        }

        $this->spans = [];
    }

    /**
     * Trace ID al (log korelasyonu için).
     */
    public function traceId(): string
    {
        return $this->traceId ?? '';
    }

    /**
     * Yeni trace başlat.
     */
    public function newTrace(): string
    {
        $this->flush();
        $this->traceId = bin2hex(random_bytes(16));

        return $this->traceId;
    }

    /**
     * @return list<array{key: string, value: array{stringValue: string}}>
     */
    private function formatAttributes(array $attrs): array
    {
        $formatted = [];
        foreach ($attrs as $key => $value) {
            $formatted[] = [
                'key'   => (string) $key,
                'value' => ['stringValue' => (string) $value],
            ];
        }

        return $formatted;
    }

    public function __destruct()
    {
        $this->flush();
    }

    public function health(): bool
    {
        try {
            $response = $this->http->get('');
            return $response->getStatusCode() < 500;
        } catch (\Throwable) {
            return false;
        }
    }
}
