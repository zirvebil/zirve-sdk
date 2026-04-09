using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Storage;

public class StorageManager : IDisposable
{
    private readonly AmazonS3Client _s3Client;
    private readonly string _bucket;
    private readonly string _imgproxyUrl;

    public StorageManager(ConfigManager configManager)
    {
        var cfg = configManager.Module("storage");
        
        var endpoint = cfg.GetValueOrDefault("endpoint", "http://minio.zirve-infra.svc.cluster.local:9000");
        var accessKey = cfg.GetValueOrDefault("key", "");
        var secretKey = cfg.GetValueOrDefault("secret", "");
        _bucket = cfg.GetValueOrDefault("bucket", "zirve-storage") ?? "zirve-storage";
        _imgproxyUrl = (cfg.GetValueOrDefault("imgproxy", "http://imgproxy.zirve-infra.svc.cluster.local") ?? "").TrimEnd('/');

        var s3Config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true, // Required for MinIO
            UseHttp = endpoint != null && endpoint.StartsWith("http://")
        };

        _s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);
    }

    /// <summary>
    /// Uploads stream content to MinIO bucket.
    /// </summary>
    public async Task<string> UploadAsync(string path, Stream content, string contentType = "application/octet-stream")
    {
        var safePath = path.TrimStart('/');
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = safePath,
            InputStream = content,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(request);
        return $"s3://{_bucket}/{safePath}";
    }

    /// <summary>
    /// Checks if object exists in MinIO.
    /// </summary>
    public async Task<bool> ExistsAsync(string path)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(_bucket, path.TrimStart('/'));
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <summary>
    /// Downloads object content into memory stream.
    /// </summary>
    public async Task<MemoryStream?> DownloadAsync(string path)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucket,
                Key = path.TrimStart('/')
            };

            using var response = await _s3Client.GetObjectAsync(request);
            var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes an object.
    /// </summary>
    public async Task<bool> DeleteAsync(string path)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _bucket,
                Key = path.TrimStart('/')
            };
            await _s3Client.DeleteObjectAsync(request);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates a presigned URL.
    /// </summary>
    public string PresignedUrl(string path, int expiresInSeconds = 3600)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = path.TrimStart('/'),
            Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds),
            Verb = HttpVerb.GET
        };

        return _s3Client.GetPreSignedURL(request);
    }

    /// <summary>
    /// Constructs basic Imgproxy URL.
    /// </summary>
    public string Resize(string path, int width, int height, string fit = "fill")
    {
        var s3Url = $"s3://{_bucket}/{path.TrimStart('/')}";
        var encodedUrl = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s3Url))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
            
        return $"{_imgproxyUrl}/insecure/rs:{fit}:{width}:{height}:0/{encodedUrl}";
    }

    public string Thumbnail(string path, int size = 150)
    {
        return Resize(path, size, size, "fill");
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucket,
                MaxKeys = 1
            };
            await _s3Client.ListObjectsV2Async(request);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
