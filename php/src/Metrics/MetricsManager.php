<?php

declare(strict_types=1);

namespace Zirve\Metrics;

use GuzzleHttp\Client;

/**
 * Zirve Metrics Manager — Prometheus (Pushgateway).
 *
 * Counter, gauge, histogram metrikleri.
 * Prometheus Pushgateway üzerinden push.
 */
final class MetricsManager
{
    private Client $http;
    private string $job;

    /** @var array<string, array{type: string, value: float, help: string, labels: array, name: string}> */
    private array $metrics = [];

    public function __construct(array $config)
    {
        $url = $config['pushgateway_url'] ?? $config['url'] ?? 'http://localhost:9091';
        $this->http = new Client([
            'base_uri' => rtrim($url, '/') . '/',
            'timeout'  => 5,
        ]);
        $this->job = $config['job'] ?? getenv('SERVICE_NAME') ?: 'zirve-app';
    }

    /**
     * Counter oluştur/artır.
     */
    public function counter(string $name, float $increment = 1, array $labels = [], string $help = ''): self
    {
        $key = $this->metricKey($name, $labels);
        if (!isset($this->metrics[$key])) {
            $this->metrics[$key] = ['type' => 'counter', 'value' => 0, 'help' => $help, 'labels' => $labels, 'name' => $name];
        }
        $this->metrics[$key]['value'] += $increment;

        return $this;
    }

    /**
     * Gauge ayarla.
     */
    public function gauge(string $name, float $value, array $labels = [], string $help = ''): self
    {
        $key = $this->metricKey($name, $labels);
        $this->metrics[$key] = ['type' => 'gauge', 'value' => $value, 'help' => $help, 'labels' => $labels, 'name' => $name];

        return $this;
    }

    /**
     * Histogram observe.
     */
    public function histogram(string $name, float $value, array $labels = [], string $help = ''): self
    {
        // Basitleştirilmiş: gauge olarak sakla (gerçek histogram Prometheus tarafında)
        $key = $this->metricKey($name . '_sum', $labels);
        if (!isset($this->metrics[$key])) {
            $this->metrics[$key] = ['type' => 'gauge', 'value' => 0, 'help' => $help, 'labels' => $labels, 'name' => $name . '_sum'];
        }
        $this->metrics[$key]['value'] += $value;

        $countKey = $this->metricKey($name . '_count', $labels);
        if (!isset($this->metrics[$countKey])) {
            $this->metrics[$countKey] = ['type' => 'counter', 'value' => 0, 'help' => $help . ' count', 'labels' => $labels, 'name' => $name . '_count'];
        }
        $this->metrics[$countKey]['value'] += 1;

        return $this;
    }

    /**
     * Süre ölçen wrapper (histogram olarak kaydeder).
     *
     * @template T
     * @param callable(): T $callback
     * @return T
     */
    public function timer(string $name, callable $callback, array $labels = []): mixed
    {
        $start = hrtime(true);

        try {
            $result = $callback();
            $duration = (hrtime(true) - $start) / 1_000_000_000;
            $this->histogram($name, $duration, array_merge($labels, ['status' => 'success']));

            return $result;
        } catch (\Throwable $e) {
            $duration = (hrtime(true) - $start) / 1_000_000_000;
            $this->histogram($name, $duration, array_merge($labels, ['status' => 'error']));
            throw $e;
        }
    }

    /**
     * Metrikleri Pushgateway'e gönder (Prometheus text format).
     */
    public function push(): void
    {
        if (empty($this->metrics)) {
            return;
        }

        $body = '';
        foreach ($this->metrics as $metric) {
            $name = $metric['name'];
            if ($metric['help']) {
                $body .= "# HELP {$name} {$metric['help']}\n";
            }
            $body .= "# TYPE {$name} {$metric['type']}\n";

            $labelStr = '';
            if (!empty($metric['labels'])) {
                $parts = [];
                foreach ($metric['labels'] as $k => $v) {
                    $parts[] = "{$k}=\"{$v}\"";
                }
                $labelStr = '{' . implode(',', $parts) . '}';
            }

            $body .= "{$name}{$labelStr} {$metric['value']}\n";
        }

        try {
            $this->http->put("metrics/job/{$this->job}", [
                'headers' => ['Content-Type' => 'text/plain'],
                'body'    => $body,
            ]);
        } catch (\Throwable) {
            // Push başarısız — sessizce devam
        }
    }

    /**
     * Increment kısayol (chainable).
     */
    public function increment(string $name, array $labels = []): self
    {
        return $this->counter($name, 1, $labels);
    }

    private function metricKey(string $name, array $labels): string
    {
        ksort($labels);

        return $name . ':' . json_encode($labels);
    }

    public function __destruct()
    {
        $this->push();
    }

    public function health(): bool
    {
        try {
            $response = $this->http->get('-/healthy');
            return $response->getStatusCode() === 200;
        } catch (\Throwable) {
            return false;
        }
    }
}
