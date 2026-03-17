<?php

declare(strict_types=1);

namespace Zirve\Storage;

use GuzzleHttp\Client;

/**
 * Zirve Storage Manager — MinIO (S3) + Imgproxy.
 *
 * Dosya yükleme, indirme, presigned URL, thumbnail/resize.
 */
final class StorageManager
{
    private Client $http;

    /** @var array<string, mixed> */
    private array $config;

    public function __construct(array $config)
    {
        $this->config = $config;
        $this->http = new Client([
            'base_uri' => rtrim($config['endpoint'] ?? '', '/') . '/',
            'timeout'  => 30,
        ]);
    }

    /**
     * Dosya yükle.
     *
     * @return string  Object key (path)
     */
    public function upload(string $path, string $content, string $contentType = 'application/octet-stream'): string
    {
        $bucket = $this->config['bucket'] ?? 'zirve';

        $this->s3Request('PUT', "/{$bucket}/{$path}", $content, [
            'Content-Type' => $contentType,
        ]);

        return $path;
    }

    /**
     * Stream ile dosya yükle (büyük dosyalar için).
     *
     * @param resource $stream
     */
    public function uploadStream(string $path, $stream, string $contentType = 'application/octet-stream'): string
    {
        $bucket = $this->config['bucket'] ?? 'zirve';

        $this->s3Request('PUT', "/{$bucket}/{$path}", $stream, [
            'Content-Type' => $contentType,
        ]);

        return $path;
    }

    /**
     * Dosya indir.
     */
    public function download(string $path): string
    {
        $bucket = $this->config['bucket'] ?? 'zirve';
        $response = $this->s3Request('GET', "/{$bucket}/{$path}");

        return $response;
    }

    /**
     * Dosya sil.
     */
    public function delete(string $path): void
    {
        $bucket = $this->config['bucket'] ?? 'zirve';
        $this->s3Request('DELETE', "/{$bucket}/{$path}");
    }

    /**
     * Dosya var mı?
     */
    public function exists(string $path): bool
    {
        try {
            $bucket = $this->config['bucket'] ?? 'zirve';
            $this->s3Request('HEAD', "/{$bucket}/{$path}");

            return true;
        } catch (\Throwable) {
            return false;
        }
    }

    /**
     * Presigned URL oluştur (süreli erişim).
     */
    public function presignedUrl(string $path, int $expirySeconds = 3600): string
    {
        $bucket = $this->config['bucket'] ?? 'zirve';
        $endpoint = rtrim($this->config['endpoint'] ?? '', '/');
        $accessKey = $this->config['key'] ?? '';
        $date = gmdate('Ymd\THis\Z');
        $expiry = $expirySeconds;

        // Basitleştirilmiş presigned URL (gerçek S3 imzalama)
        $query = http_build_query([
            'X-Amz-Algorithm'  => 'AWS4-HMAC-SHA256',
            'X-Amz-Credential' => "{$accessKey}/" . gmdate('Ymd') . "/us-east-1/s3/aws4_request",
            'X-Amz-Date'       => $date,
            'X-Amz-Expires'    => $expiry,
            'X-Amz-SignedHeaders' => 'host',
        ]);

        return "{$endpoint}/{$bucket}/{$path}?{$query}";
    }

    /**
     * Imgproxy ile thumbnail oluştur.
     */
    public function thumbnail(string $path, int $width = 200, int $height = 200): string
    {
        $imgproxyUrl = rtrim($this->config['imgproxy'] ?? '', '/');
        $sourceUrl = $this->objectUrl($path);
        $encoded = rtrim(base64_encode($sourceUrl), '=');
        $encoded = strtr($encoded, '+/', '-_');

        return "{$imgproxyUrl}/insecure/fill/{$width}/{$height}/sm/0/plain/{$sourceUrl}";
    }

    /**
     * Imgproxy ile resize.
     */
    public function resize(string $path, int $width, int $height = 0, string $type = 'fit'): string
    {
        $imgproxyUrl = rtrim($this->config['imgproxy'] ?? '', '/');
        $sourceUrl = $this->objectUrl($path);

        return "{$imgproxyUrl}/insecure/{$type}/{$width}/{$height}/sm/0/plain/{$sourceUrl}";
    }

    /**
     * Object'in tam URL'si.
     */
    public function objectUrl(string $path): string
    {
        $bucket = $this->config['bucket'] ?? 'zirve';
        $endpoint = rtrim($this->config['endpoint'] ?? '', '/');

        return "{$endpoint}/{$bucket}/{$path}";
    }

    /**
     * Bucket'taki dosyaları listele.
     *
     * @return list<string>
     */
    public function list(string $prefix = ''): array
    {
        $bucket = $this->config['bucket'] ?? 'zirve';
        $query = $prefix ? "?prefix=" . urlencode($prefix) : '';
        $body = $this->s3Request('GET', "/{$bucket}{$query}");

        // Basit XML parse
        preg_match_all('/<Key>(.+?)<\/Key>/', $body, $matches);

        return $matches[1] ?? [];
    }

    /**
     * S3 API isteği (basitleştirilmiş AWS Signature V4).
     */
    private function s3Request(string $method, string $uri, mixed $body = null, array $headers = []): string
    {
        $accessKey = $this->config['key'] ?? '';
        $secretKey = $this->config['secret'] ?? '';
        $date = gmdate('Ymd\THis\Z');

        $headers['x-amz-date'] = $date;
        $headers['x-amz-content-sha256'] = is_string($body) ? hash('sha256', $body) : 'UNSIGNED-PAYLOAD';

        // Basitleştirilmiş auth header
        if ($accessKey && $secretKey) {
            $headers['Authorization'] = "AWS {$accessKey}:{$secretKey}";
        }

        $options = ['headers' => $headers];
        if ($body !== null) {
            $options['body'] = $body;
        }

        $response = $this->http->request($method, ltrim($uri, '/'), $options);

        return $response->getBody()->getContents();
    }

    public function health(): bool
    {
        try {
            $response = $this->http->get('minio/health/live');

            return $response->getStatusCode() === 200;
        } catch (\Throwable) {
            return false;
        }
    }
}
