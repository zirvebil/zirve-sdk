<?php

declare(strict_types=1);

namespace Zirve\Cache;

/**
 * Zirve Cache Manager — Redis.
 *
 * phpredis ext veya Predis ile Redis erişimi.
 * remember(), lock(), tags() gibi yüksek seviye API'ler.
 */
final class CacheManager
{
    private ?\Redis $redis = null;

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
     * Redis bağlantısı al (lazy).
     */
    public function connection(): \Redis
    {
        if ($this->redis !== null) {
            return $this->redis;
        }

        $this->redis = new \Redis();
        $host = $this->config['host'] ?? '127.0.0.1';
        $port = (int) ($this->config['port'] ?? 6379);

        $this->redis->connect($host, $port, 3.0);

        $password = $this->config['password'] ?? '';
        if ($password !== '') {
            $this->redis->auth($password);
        }

        $prefix = $this->config['prefix'] ?? 'zirve:';
        if ($prefix !== '') {
            $this->redis->setOption(\Redis::OPT_PREFIX, $prefix);
        }

        $this->redis->setOption(\Redis::OPT_SERIALIZER, \Redis::SERIALIZER_JSON);

        return $this->redis;
    }

    /**
     * Cache'den değer al.
     */
    public function get(string $key, mixed $default = null): mixed
    {
        $value = $this->connection()->get($key);

        return $value !== false ? $value : $default;
    }

    /**
     * Cache'e değer yaz.
     *
     * @param int $ttl  Saniye cinsinden süre (0 = sınırsız)
     */
    public function set(string $key, mixed $value, int $ttl = 0): bool
    {
        if ($ttl > 0) {
            return $this->connection()->setex($key, $ttl, $value);
        }

        return $this->connection()->set($key, $value);
    }

    /**
     * Cache'de varsa döndür, yoksa callback'i çalıştır, sonucu cache'le ve döndür.
     *
     * @param int $ttl  Saniye
     */
    public function remember(string $key, int $ttl, callable $callback): mixed
    {
        $cached = $this->get($key);

        if ($cached !== null) {
            return $cached;
        }

        $value = $callback();
        $this->set($key, $value, $ttl);

        return $value;
    }

    /**
     * Cache'den sil.
     */
    public function forget(string $key): bool
    {
        return $this->connection()->del($key) > 0;
    }

    /**
     * Key var mı?
     */
    public function has(string $key): bool
    {
        return (bool) $this->connection()->exists($key);
    }

    /**
     * Sayacı artır.
     */
    public function increment(string $key, int $by = 1): int
    {
        return $this->connection()->incrBy($key, $by);
    }

    /**
     * Sayacı azalt.
     */
    public function decrement(string $key, int $by = 1): int
    {
        return $this->connection()->decrBy($key, $by);
    }

    /**
     * Distributed lock (SET NX EX pattern).
     *
     * @param int $ttl  Lock süresi (saniye)
     * @return string|false  Lock token veya false
     */
    public function lock(string $key, int $ttl = 10): string|false
    {
        $token = bin2hex(random_bytes(16));
        $acquired = $this->connection()->set(
            "lock:{$key}",
            $token,
            ['NX', 'EX' => $ttl]
        );

        return $acquired ? $token : false;
    }

    /**
     * Distributed lock serbest bırak.
     */
    public function unlock(string $key, string $token): bool
    {
        $lua = <<<'LUA'
                if redis.call("get", KEYS[1]) == ARGV[1] then
                    return redis.call("del", KEYS[1])
                else
                    return 0
                end
            LUA;

        return (bool) $this->connection()->eval($lua, ["lock:{$key}", $token], 1);
    }

    /**
     * Tüm cache'i temizle (prefix scope'unda).
     */
    public function flush(): bool
    {
        return $this->connection()->flushDB();
    }

    /**
     * Sağlık kontrolü.
     */
    public function health(): bool
    {
        try {
            return $this->connection()->ping() === true;
        } catch (\Throwable) {
            return false;
        }
    }
}
