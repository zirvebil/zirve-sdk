<?php

declare(strict_types=1);

namespace Zirve\Log;

use GuzzleHttp\Client;
use Psr\Log\LoggerInterface;
use Psr\Log\LogLevel;

/**
 * Zirve Log Manager — Loki (PSR-3 uyumlu).
 *
 * Yapısal log gönderimi Loki push API üzerinden.
 */
final class LogManager implements LoggerInterface
{
    private Client $http;
    private string $serviceName;

    /** @var list<array{level: string, message: string, context: array, timestamp: int}> */
    private array $buffer = [];
    private int $bufferSize;

    public function __construct(array $config)
    {
        $this->http = new Client([
            'base_uri' => rtrim($config['url'] ?? '', '/') . '/',
            'timeout'  => 5,
        ]);
        $this->serviceName = $config['service_name'] ?? getenv('SERVICE_NAME') ?: 'unknown';
        $this->bufferSize = (int) ($config['buffer_size'] ?? 10);
    }

    public function emergency(\Stringable|string $message, array $context = []): void { $this->log(LogLevel::EMERGENCY, $message, $context); }
    public function alert(\Stringable|string $message, array $context = []): void { $this->log(LogLevel::ALERT, $message, $context); }
    public function critical(\Stringable|string $message, array $context = []): void { $this->log(LogLevel::CRITICAL, $message, $context); }
    public function error(\Stringable|string $message, array $context = []): void { $this->log(LogLevel::ERROR, $message, $context); }
    public function warning(\Stringable|string $message, array $context = []): void { $this->log(LogLevel::WARNING, $message, $context); }
    public function notice(\Stringable|string $message, array $context = []): void { $this->log(LogLevel::NOTICE, $message, $context); }
    public function info(\Stringable|string $message, array $context = []): void { $this->log(LogLevel::INFO, $message, $context); }
    public function debug(\Stringable|string $message, array $context = []): void { $this->log(LogLevel::DEBUG, $message, $context); }
    public function warn(\Stringable|string $message, array $context = []): void { $this->warning($message, $context); }

    public function log($level, \Stringable|string $message, array $context = []): void
    {
        $this->buffer[] = [
            'level'     => (string) $level,
            'message'   => (string) $message,
            'context'   => $context,
            'timestamp' => (int) (microtime(true) * 1_000_000_000),
        ];

        if (count($this->buffer) >= $this->bufferSize) {
            $this->flush();
        }
    }

    /**
     * Yapısal log (key-value).
     */
    public function structured(string $level, string $message, array $fields = []): void
    {
        $this->log($level, $message, $fields);
    }

    /**
     * Buffer'daki logları Loki'ye gönder.
     */
    public function flush(): void
    {
        if (empty($this->buffer)) {
            return;
        }

        $values = [];
        foreach ($this->buffer as $entry) {
            $logLine = json_encode([
                'level'   => $entry['level'],
                'msg'     => $entry['message'],
                'context' => $entry['context'],
            ]);

            $values[] = [(string) $entry['timestamp'], $logLine];
        }

        try {
            $this->http->post('loki/api/v1/push', [
                'json' => [
                    'streams' => [[
                        'stream' => [
                            'service' => $this->serviceName,
                            'level'   => $this->buffer[0]['level'] ?? 'info',
                        ],
                        'values' => $values,
                    ]],
                ],
            ]);
        } catch (\Throwable) {
            // Log gönderimi başarısız olursa sessizce devam et
        }

        $this->buffer = [];
    }

    public function __destruct()
    {
        $this->flush();
    }

    public function health(): bool
    {
        try {
            $response = $this->http->get('ready');
            return $response->getStatusCode() === 200;
        } catch (\Throwable) {
            return false;
        }
    }
}
