<?php

declare(strict_types=1);

namespace Zirve\Search;

use GuzzleHttp\Client;

/**
 * Zirve Search Manager — Elasticsearch.
 *
 * Index, search, suggest, bulk operations.
 */
final class SearchManager
{
    private Client $http;

    public function __construct(array $config)
    {
        $scheme = $config['scheme'] ?? 'https';
        $host = $config['host'] ?? 'localhost';
        $port = (int) ($config['port'] ?? 9200);

        $options = [
            'base_uri' => "{$scheme}://{$host}:{$port}/",
            'timeout'  => 10,
            'verify'   => false,
        ];

        $username = $config['username'] ?? '';
        $password = $config['password'] ?? '';
        if ($username) {
            $options['auth'] = [$username, $password];
        }

        $this->http = new Client($options);
    }

    /**
     * Belge indexle.
     *
     * @param array<string, mixed> $document
     */
    public function index(string $indexName, string $id, array $document): void
    {
        $this->http->put("{$indexName}/_doc/{$id}", ['json' => $document]);
    }

    /**
     * Belge al.
     *
     * @return array<string, mixed>|null
     */
    public function get(string $indexName, string $id): ?array
    {
        try {
            $response = $this->http->get("{$indexName}/_doc/{$id}");
            $data = json_decode($response->getBody()->getContents(), true);

            return $data['_source'] ?? null;
        } catch (\Throwable) {
            return null;
        }
    }

    /**
     * Belge sil.
     */
    public function delete(string $indexName, string $id): void
    {
        $this->http->delete("{$indexName}/_doc/{$id}");
    }

    /**
     * Arama yap.
     *
     * @param array<string, mixed> $query  Elasticsearch query DSL
     * @return array{total: int, hits: list<array<string, mixed>>}
     */
    public function search(string $indexName, array $query, int $size = 10, int $from = 0): array
    {
        $response = $this->http->post("{$indexName}/_search", [
            'json' => [
                'query' => $query,
                'size'  => $size,
                'from'  => $from,
            ],
        ]);

        $data = json_decode($response->getBody()->getContents(), true);
        $hits = [];

        foreach ($data['hits']['hits'] ?? [] as $hit) {
            $hits[] = array_merge(['_id' => $hit['_id'], '_score' => $hit['_score']], $hit['_source'] ?? []);
        }

        return [
            'total' => $data['hits']['total']['value'] ?? 0,
            'hits'  => $hits,
        ];
    }

    /**
     * Match query kısayol.
     *
     * @return array{total: int, hits: list<array<string, mixed>>}
     */
    public function match(string $indexName, string $field, string $value, int $size = 10): array
    {
        return $this->search($indexName, ['match' => [$field => $value]], $size);
    }

    /**
     * Multi-match arama.
     *
     * @param list<string> $fields
     * @return array{total: int, hits: list<array<string, mixed>>}
     */
    public function multiMatch(string $indexName, array $fields, string $value, int $size = 10): array
    {
        return $this->search($indexName, [
            'multi_match' => [
                'query'  => $value,
                'fields' => $fields,
                'type'   => 'best_fields',
            ],
        ], $size);
    }

    /**
     * Suggest (autocomplete).
     *
     * @return list<string>
     */
    public function suggest(string $indexName, string $field, string $prefix, int $size = 5): array
    {
        $response = $this->http->post("{$indexName}/_search", [
            'json' => [
                'suggest' => [
                    'suggestion' => [
                        'prefix'     => $prefix,
                        'completion' => [
                            'field' => $field,
                            'size'  => $size,
                        ],
                    ],
                ],
            ],
        ]);

        $data = json_decode($response->getBody()->getContents(), true);
        $options = $data['suggest']['suggestion'][0]['options'] ?? [];

        return array_column($options, 'text');
    }

    /**
     * Toplu indeksleme.
     *
     * @param list<array{id: string, data: array<string, mixed>}> $documents
     */
    public function bulk(string $indexName, array $documents): void
    {
        $body = '';
        foreach ($documents as $doc) {
            $body .= json_encode(['index' => ['_index' => $indexName, '_id' => $doc['id']]]) . "\n";
            $body .= json_encode($doc['data']) . "\n";
        }

        $this->http->post('_bulk', [
            'headers' => ['Content-Type' => 'application/x-ndjson'],
            'body'    => $body,
        ]);
    }

    /**
     * Index oluştur.
     *
     * @param array<string, mixed> $mappings
     */
    public function createIndex(string $indexName, array $mappings = [], array $settings = []): void
    {
        $body = [];
        if ($mappings) {
            $body['mappings'] = ['properties' => $mappings];
        }
        if ($settings) {
            $body['settings'] = $settings;
        }

        $this->http->put($indexName, ['json' => $body ?: new \stdClass()]);
    }

    /**
     * Index sil.
     */
    public function deleteIndex(string $indexName): void
    {
        $this->http->delete($indexName);
    }

    public function health(): bool
    {
        try {
            $response = $this->http->get('_cluster/health');
            $data = json_decode($response->getBody()->getContents(), true);

            return in_array($data['status'] ?? '', ['green', 'yellow'], true);
        } catch (\Throwable) {
            return false;
        }
    }
}
