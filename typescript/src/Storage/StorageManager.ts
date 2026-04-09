import { S3Client, PutObjectCommand, GetObjectCommand, DeleteObjectCommand, HeadObjectCommand, ListObjectsV2Command } from '@aws-sdk/client-s3';
import { getSignedUrl } from '@aws-sdk/s3-request-presigner';

/**
 * Zirve Storage Manager — MinIO & Imgproxy Integration.
 *
 * Provides upload, download, presigned URLs, and on-the-fly image manipulation.
 */
export class StorageManager {
  private config: Record<string, any>;
  private s3: S3Client;
  private bucket: string;
  private imgproxyUrl: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    const endpoint = config.endpoint || 'http://minio.zirve-infra.svc.cluster.local:9000';
    const accessKeyId = config.key || '';
    const secretAccessKey = config.secret || '';
    
    this.bucket = config.bucket || 'zirve-storage';
    this.imgproxyUrl = (config.imgproxy || 'http://imgproxy.zirve-infra.svc.cluster.local').replace(/\/$/, '');

    // Configure S3 client for MinIO Standard
    this.s3 = new S3Client({
      endpoint,
      region: config.region || 'us-east-1', // MinIO defaults to us-east-1 if not set
      credentials: {
        accessKeyId,
        secretAccessKey,
      },
      forcePathStyle: true, // Required for MinIO
    });
  }

  /**
   * Upload a file from an ArrayBuffer, Buffer or Blob to a specific path in MinIO.
   */
  public async upload(path: string, content: Buffer | Uint8Array | string, contentType: string = 'application/octet-stream'): Promise<string> {
    const safePath = path.replace(/^\/+/, '');
    const command = new PutObjectCommand({
      Bucket: this.bucket,
      Key: safePath,
      Body: content,
      ContentType: contentType,
    });

    await this.s3.send(command);
    return `s3://${this.bucket}/${safePath}`;
  }

  /**
   * Check if an object exists at the given path.
   */
  public async exists(path: string): Promise<boolean> {
    const safePath = path.replace(/^\/+/, '');
    try {
      const command = new HeadObjectCommand({
        Bucket: this.bucket,
        Key: safePath,
      });
      await this.s3.send(command);
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Download a file's content directly into memory.
   */
  public async download(path: string): Promise<Uint8Array | null> {
    const safePath = path.replace(/^\/+/, '');
    try {
      const command = new GetObjectCommand({
        Bucket: this.bucket,
        Key: safePath,
      });
      const response = await this.s3.send(command);
      if (response.Body) {
        return await response.Body.transformToByteArray();
      }
      return null;
    } catch {
      return null;
    }
  }

  /**
   * Delete an object.
   */
  public async delete(path: string): Promise<boolean> {
    const safePath = path.replace(/^\/+/, '');
    try {
      const command = new DeleteObjectCommand({
        Bucket: this.bucket,
        Key: safePath,
      });
      await this.s3.send(command);
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Generate a presigned URL for downloading/viewing an object.
   */
  public async presignedUrl(path: string, expiresInSeconds: number = 3600): Promise<string> {
    const safePath = path.replace(/^\/+/, '');
    const command = new GetObjectCommand({
      Bucket: this.bucket,
      Key: safePath,
    });
    return await getSignedUrl(this.s3, command, { expiresIn: expiresInSeconds });
  }

  /**
   * Generate a basic Imgproxy URL for an image stored in MinIO.
   * Assumes insecure mode (no URL signature) for internal microservice usage.
   */
  public resize(path: string, width: number, height: number, fit: string = 'fill'): string {
    const safePath = path.replace(/^\/+/, '');
    const encodedUrl = Buffer.from(`s3://${this.bucket}/${safePath}`).toString('base64url');
    // Basic Imgproxy format: /insecure/rs:fill:300:400:0/plain/b64URL
    return `${this.imgproxyUrl}/insecure/rs:${fit}:${width}:${height}:0/${encodedUrl}`;
  }

  /**
   * Generate a thumbnail.
   */
  public thumbnail(path: string, size: number = 150): string {
    return this.resize(path, size, size, 'fill');
  }

  /**
   * List objects in a given prefix/directory.
   */
  public async list(prefix: string = ''): Promise<string[]> {
    const safePrefix = prefix.replace(/^\/+/, '');
    const command = new ListObjectsV2Command({
      Bucket: this.bucket,
      Prefix: safePrefix,
    });
    
    try {
      const response = await this.s3.send(command);
      if (!response.Contents) return [];
      
      return response.Contents
        .map(obj => obj.Key)
        .filter((k): k is string => k !== undefined);
    } catch {
      return [];
    }
  }

  /**
   * Basic MinIO health check.
   */
  public async health(): Promise<boolean> {
    try {
      const command = new ListObjectsV2Command({ Bucket: this.bucket, MaxKeys: 1 });
      await this.s3.send(command);
      return true;
    } catch {
      return false;
    }
  }
}
