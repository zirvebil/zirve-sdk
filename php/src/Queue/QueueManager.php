<?php

declare(strict_types=1);

namespace Zirve\Queue;

use GuzzleHttp\Client;

/**
 * Zirve Queue Manager — RabbitMQ (AMQP + HTTP Management API).
 *
 * publish/subscribe, dead letter, queue yönetimi.
 * Not: Gerçek AMQP iletişimi için php-amqplib kullanılır.
 *      Bu sınıf RabbitMQ Management HTTP API wrapper'ıdır.
 *      Dapr sidecar üzerinden pub/sub de desteklenir.
 */
final class QueueManager
{
    private Client $http;

    /** @var array<string, mixed> */
    private array $config;

    public function __construct(array $config)
    {
        $this->config = $config;
        $host = $config['host'] ?? 'localhost';
        $managementPort = (int) ($config['management_port'] ?? 15672);

        $this->http = new Client([
            'base_uri' => "http://{$host}:{$managementPort}/",
            'timeout'  => 5,
            'auth'     => [
                $config['username'] ?? 'guest',
                $config['password'] ?? 'guest',
            ],
        ]);
    }

    /**
     * Mesaj yayınla (RabbitMQ Management API üzerinden).
     *
     * @param array<string, mixed> $payload
     */
    public function publish(string $exchange, string $routingKey, array $payload, array $properties = []): void
    {
        $vhost = $this->config['vhost'] ?? '/';

        $this->http->post('api/exchanges/' . urlencode($vhost) . '/' . urlencode($exchange) . '/publish', [
            'json' => [
                'routing_key'      => $routingKey,
                'payload'          => json_encode($payload),
                'payload_encoding' => 'string',
                'properties'       => array_merge([
                    'content_type' => 'application/json',
                    'delivery_mode' => 2, // persistent
                ], $properties),
            ],
        ]);
    }

    /**
     * Dapr sidecar üzerinden pub/sub mesaj yayınla.
     *
     * @param array<string, mixed> $data
     */
    public function publishViaDapr(string $topic, array $data, string $pubsubName = 'pubsub'): void
    {
        $daprPort = (int) ($this->config['dapr_port'] ?? 3500);
        $client = new Client(['base_uri' => "http://localhost:{$daprPort}/", 'timeout' => 5]);

        $client->post("v1.0/publish/{$pubsubName}/{$topic}", [
            'json' => $data,
        ]);
    }

    /**
     * Kuyruk oluştur.
     */
    public function createQueue(string $name, bool $durable = true, array $arguments = []): void
    {
        $vhost = $this->config['vhost'] ?? '/';

        $this->http->put('api/queues/' . urlencode($vhost) . '/' . urlencode($name), [
            'json' => [
                'durable'    => $durable,
                'auto_delete' => false,
                'arguments'  => (object) $arguments,
            ],
        ]);
    }

    /**
     * Dead letter queue ile kuyruk oluştur.
     */
    public function createQueueWithDeadLetter(string $name, string $dlxExchange = 'dlx', string $dlxRoutingKey = ''): void
    {
        $this->createQueue($name, true, [
            'x-dead-letter-exchange'    => $dlxExchange,
            'x-dead-letter-routing-key' => $dlxRoutingKey ?: "{$name}.dlq",
        ]);

        // DLQ kuyruğunu da oluştur
        $this->createQueue("{$name}.dlq");
    }

    /**
     * Kuyruk bilgisi al.
     *
     * @return array<string, mixed>
     */
    public function queueInfo(string $name): array
    {
        $vhost = $this->config['vhost'] ?? '/';
        $response = $this->http->get('api/queues/' . urlencode($vhost) . '/' . urlencode($name));

        return json_decode($response->getBody()->getContents(), true);
    }

    /**
     * Kuyruktaki mesaj sayısı.
     */
    public function messageCount(string $queue): int
    {
        $info = $this->queueInfo($queue);

        return (int) ($info['messages'] ?? 0);
    }

    /**
     * Tüm kuyrukları listele.
     *
     * @return list<array<string, mixed>>
     */
    public function listQueues(): array
    {
        $response = $this->http->get('api/queues');

        return json_decode($response->getBody()->getContents(), true);
    }

    /**
     * Mesaj al (get, ack değil — test/debug amaçlı).
     *
     * @return list<array<string, mixed>>
     */
    public function getMessages(string $queue, int $count = 1, bool $ackMode = false): array
    {
        $vhost = $this->config['vhost'] ?? '/';

        $response = $this->http->post('api/queues/' . urlencode($vhost) . '/' . urlencode($queue) . '/get', [
            'json' => [
                'count'    => $count,
                'ackmode'  => $ackMode ? 'ack_requeue_true' : 'ack_requeue_false',
                'encoding' => 'auto',
            ],
        ]);

        return json_decode($response->getBody()->getContents(), true);
    }

    public function health(): bool
    {
        try {
            $response = $this->http->get('api/health/checks/alarms');

            return $response->getStatusCode() === 200;
        } catch (\Throwable) {
            return false;
        }
    }
}
