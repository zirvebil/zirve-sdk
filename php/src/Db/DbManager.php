<?php

declare(strict_types=1);

namespace Zirve\Db;

use PDO;
use PDOStatement;

/**
 * Zirve Db Manager — PostgreSQL + MariaDB.
 *
 * PDO tabanlı, prepared statement zorunlu, multi-tenant schema desteği.
 */
final class DbManager
{
    private ?PDO $pdo = null;

    /** @var array<string, mixed> */
    private array $config;

    /**
     * @param array<string, mixed> $config
     */
    public function __construct(array $config)
    {
        $this->config = $config;
    }

    /**
     * PDO bağlantısı al (lazy).
     */
    public function connection(): PDO
    {
        if ($this->pdo !== null) {
            return $this->pdo;
        }

        $driver   = $this->config['driver'] ?? 'pgsql';
        $host     = $this->config['host'] ?? 'localhost';
        $port     = (int) ($this->config['port'] ?? ($driver === 'pgsql' ? 5432 : 3306));
        $database = $this->config['database'] ?? 'postgres';
        $username = $this->config['username'] ?? 'postgres';
        $password = $this->config['password'] ?? '';

        $dsn = match ($driver) {
            'pgsql' => "pgsql:host={$host};port={$port};dbname={$database}",
            'mysql', 'mariadb' => "mysql:host={$host};port={$port};dbname={$database};charset=utf8mb4",
            default => throw new \InvalidArgumentException("Desteklenmeyen DB driver: {$driver}"),
        };

        $this->pdo = new PDO($dsn, $username, $password, [
            PDO::ATTR_ERRMODE            => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
            PDO::ATTR_EMULATE_PREPARES   => false,
            PDO::ATTR_STRINGIFY_FETCHES  => false,
        ]);

        return $this->pdo;
    }

    /**
     * Prepared statement ile sorgu çalıştır.
     *
     * @param list<mixed> $bindings
     * @return list<array<string, mixed>>
     */
    public function query(string $sql, array $bindings = []): array
    {
        $stmt = $this->connection()->prepare($sql);
        $stmt->execute($bindings);

        return $stmt->fetchAll();
    }

    /**
     * INSERT / UPDATE / DELETE — etkilenen satır sayısını döndürür.
     *
     * @param list<mixed> $bindings
     */
    public function execute(string $sql, array $bindings = []): int
    {
        $stmt = $this->connection()->prepare($sql);
        $stmt->execute($bindings);

        return $stmt->rowCount();
    }

    /**
     * Transaction wrapper.
     *
     * @template T
     * @param callable(): T $callback
     * @return T
     */
    public function transaction(callable $callback): mixed
    {
        $pdo = $this->connection();
        $pdo->beginTransaction();

        try {
            $result = $callback();
            $pdo->commit();

            return $result;
        } catch (\Throwable $e) {
            $pdo->rollBack();
            throw $e;
        }
    }

    /**
     * Multi-tenant: belirli bir schema'ya geç (PostgreSQL).
     */
    public function tenant(string $schema): self
    {
        $this->connection()->exec("SET search_path TO \"{$schema}\", public");

        return $this;
    }

    /**
     * Son eklenen kaydın ID'si.
     */
    public function lastInsertId(?string $name = null): string
    {
        return $this->connection()->lastInsertId($name);
    }

    /**
     * MariaDB bağlantısı al (farklı host/port).
     */
    public function mariadb(): self
    {
        $mariaConfig = $this->config;
        $mariaConfig['driver']   = 'mariadb';
        $mariaConfig['host']     = $this->config['mariadb.host'] ?? $this->config['host'];
        $mariaConfig['port']     = $this->config['mariadb.port'] ?? 3306;
        $mariaConfig['database'] = $this->config['mariadb.database'] ?? $this->config['database'];
        $mariaConfig['username'] = $this->config['mariadb.username'] ?? $this->config['username'];
        $mariaConfig['password'] = $this->config['mariadb.password'] ?? $this->config['password'];

        return new self($mariaConfig);
    }

    /**
     * Sağlık kontrolü.
     */
    public function health(): bool
    {
        try {
            $result = $this->query('SELECT 1 AS ok');

            return isset($result[0]['ok']) && $result[0]['ok'] == 1;
        } catch (\Throwable) {
            return false;
        }
    }
}
