<?php

declare(strict_types=1);

namespace Zirve\Analytics;

use GuzzleHttp\Client;

/**
 * Zirve Analytics Manager — ClickHouse.
 *
 * Analitik sorgular, veri ekleme, tablo yönetimi.
 */
final class AnalyticsManager
{
    private Client $http;

    /** @var array<string, mixed> */
    private array $config;

    public function __construct(array $config)
    {
        $host = $config['host'] ?? 'localhost';
        $port = (int) ($config['port'] ?? 8123);

        $this->config = $config;
        $this->http = new Client([
            'base_uri' => "http://{$host}:{$port}/",
            'timeout'  => 30,
        ]);
    }

    /**
     * SELECT sorgusu çalıştır.
     *
     * @param array<string, mixed> $params
     * @return list<array<string, mixed>>
     */
    public function query(string $sql, array $params = []): array
    {
        $finalSql = $this->interpolateParams($sql, $params);
        $response = $this->http->post('', [
            'body'  => $finalSql . ' FORMAT JSON',
            'query' => $this->authQuery(),
        ]);

        $data = json_decode($response->getBody()->getContents(), true);

        return $data['data'] ?? [];
    }

    /**
     * INSERT sorgusu (tek satır).
     *
     * @param array<string, mixed> $row
     */
    public function insert(string $table, array $row): void
    {
        $columns = implode(', ', array_keys($row));
        $values = implode(', ', array_map(fn($v) => $this->quote($v), array_values($row)));

        $this->execute("INSERT INTO {$table} ({$columns}) VALUES ({$values})");
    }

    /**
     * INSERT sorgusu (çoklu satır — batch).
     *
     * @param list<array<string, mixed>> $rows
     */
    public function insertBatch(string $table, array $rows): void
    {
        if (empty($rows)) {
            return;
        }

        $columns = implode(', ', array_keys($rows[0]));
        $valueRows = [];

        foreach ($rows as $row) {
            $valueRows[] = '(' . implode(', ', array_map(fn($v) => $this->quote($v), array_values($row))) . ')';
        }

        $this->execute("INSERT INTO {$table} ({$columns}) VALUES " . implode(', ', $valueRows));
    }

    /**
     * DDL / DML sorgusu çalıştır (sonuç döndürmez).
     */
    public function execute(string $sql): void
    {
        $this->http->post('', [
            'body'  => $sql,
            'query' => $this->authQuery(),
        ]);
    }

    /**
     * Tablo oluştur.
     *
     * @param array<string, string> $columns  ['name' => 'String', 'age' => 'UInt8']
     */
    public function createTable(string $name, array $columns, string $engine = 'MergeTree()', string $orderBy = ''): void
    {
        $colDefs = [];
        foreach ($columns as $col => $type) {
            $colDefs[] = "{$col} {$type}";
        }

        $sql = "CREATE TABLE IF NOT EXISTS {$name} (" . implode(', ', $colDefs) . ") ENGINE = {$engine}";
        if ($orderBy) {
            $sql .= " ORDER BY ({$orderBy})";
        }

        $this->execute($sql);
    }

    /**
     * Tablo sil.
     */
    public function dropTable(string $name): void
    {
        $this->execute("DROP TABLE IF EXISTS {$name}");
    }

    /**
     * Satır sayısı.
     */
    public function count(string $table, string $where = '1=1'): int
    {
        $result = $this->query("SELECT count() AS cnt FROM {$table} WHERE {$where}");

        return (int) ($result[0]['cnt'] ?? 0);
    }

    private function quote(mixed $value): string
    {
        if (is_null($value)) {
            return 'NULL';
        }
        if (is_int($value) || is_float($value)) {
            return (string) $value;
        }
        if (is_bool($value)) {
            return $value ? '1' : '0';
        }

        return "'" . addslashes((string) $value) . "'";
    }

    private function interpolateParams(string $sql, array $params): string
    {
        foreach ($params as $key => $value) {
            $sql = str_replace(":{$key}", $this->quote($value), $sql);
        }

        return $sql;
    }

    /**
     * @return array<string, string>
     */
    private function authQuery(): array
    {
        $query = [];
        $user = $this->config['username'] ?? '';
        $pass = $this->config['password'] ?? '';

        if ($user) {
            $query['user'] = $user;
        }
        if ($pass) {
            $query['password'] = $pass;
        }

        $db = $this->config['database'] ?? '';
        if ($db) {
            $query['database'] = $db;
        }

        return $query;
    }

    public function health(): bool
    {
        try {
            $response = $this->http->get('ping');

            return $response->getBody()->getContents() === "Ok.\n";
        } catch (\Throwable) {
            return false;
        }
    }
}
