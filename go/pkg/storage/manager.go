package storage

import (
	"context"
	"encoding/base64"
	"fmt"
	"net/url"
	"strings"
	"time"

	"github.com/minio/minio-go/v7"
	"github.com/minio/minio-go/v7/pkg/credentials"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client      *minio.Client
	endpoint    string
	imgproxyUrl string
}

func NewManager(cfg *config.Manager) (*Manager, error) {
	mod := cfg.Module("storage")
	endpointUrl := mod["endpoint"]
	accessKey := mod["key"]
	secretKey := mod["secret"]
	imgproxyUrl := mod["imgproxy"]

	if endpointUrl == "" {
		endpointUrl = "http://minio.zirve-infra.svc.cluster.local:9000"
	}
	if imgproxyUrl == "" {
		imgproxyUrl = "http://imgproxy.zirve-infra.svc.cluster.local"
	}

	u, err := url.Parse(endpointUrl)
	if err != nil {
		return nil, err
	}

	useSSL := u.Scheme == "https"
	endpoint := u.Host

	client, err := minio.New(endpoint, &minio.Options{
		Creds:  credentials.NewStaticV4(accessKey, secretKey, ""),
		Secure: useSSL,
	})
	if err != nil {
		return nil, err
	}

	return &Manager{
		client:      client,
		endpoint:    endpointUrl,
		imgproxyUrl: strings.TrimRight(imgproxyUrl, "/"),
	}, nil
}

// Client returns the raw minio client
func (m *Manager) Client() *minio.Client {
	return m.client
}

// PresignedUrl generates a temporary access link for S3 Object
func (m *Manager) PresignedUrl(ctx context.Context, bucketName, objectName string, expires time.Duration) (string, error) {
	reqParams := make(url.Values)
	u, err := m.client.PresignedGetObject(ctx, bucketName, objectName, expires, reqParams)
	if err != nil {
		return "", err
	}
	return u.String(), nil
}

// ImgproxyUrl constructs an insecure URL for image processing
func (m *Manager) ImgproxyUrl(s3Url, processingOptions string) string {
	encodedUrl := base64URLEncode([]byte(s3Url))
	if processingOptions == "" {
		processingOptions = "rs:auto:800:800/q:80"
	}
	return fmt.Sprintf("%s/insecure/%s/plain/%s", m.imgproxyUrl, processingOptions, encodedUrl)
}

func base64URLEncode(data []byte) string {
	encoded := b64encode(data)
	encoded = strings.ReplaceAll(encoded, "+", "-")
	encoded = strings.ReplaceAll(encoded, "/", "_")
	return strings.TrimRight(encoded, "=")
}

// poor man's simple base64 using stdlib
func b64encode(data []byte) string {
	return base64.StdEncoding.EncodeToString(data)
}

// Health checks minio list buckets
func (m *Manager) Health(ctx context.Context) bool {
	_, err := m.client.ListBuckets(ctx)
	return err == nil
}
