package secrets

import (
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
	"sync"
	"time"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type cacheEntry struct {
	Value     string
	ExpiresAt time.Time
}

type Manager struct {
	client    *http.Client
	baseURL   string
	token     string
	projectId string
	cache     sync.Map
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("secrets")
	baseURL := mod["url"]
	if baseURL == "" {
		baseURL = "http://infisical.zirve-infra.svc.cluster.local"
	}

	return &Manager{
		client:    &http.Client{},
		baseURL:   strings.TrimRight(baseURL, "/"),
		token:     mod["token"],
		projectId: mod["project_id"],
	}
}

// Get secret by name with 5 minute cache
func (m *Manager) Get(secretName, environment, path string) (string, error) {
	if environment == "" {
		environment = "dev"
	}
	if path == "" {
		path = "/"
	}

	cacheKey := environment + ":" + path + ":" + secretName
	if val, ok := m.cache.Load(cacheKey); ok {
		entry := val.(cacheEntry)
		if entry.ExpiresAt.After(time.Now()) {
			return entry.Value, nil
		}
	}

	if m.token == "" || m.projectId == "" {
		return "", fmt.Errorf("infisical token and project ID must be configured")
	}

	uri := fmt.Sprintf("%s/api/v3/secrets/raw/%s?workspaceId=%s&environment=%s&secretPath=%s",
		m.baseURL, url.PathEscape(secretName), m.projectId, environment, url.QueryEscape(path))

	req, err := http.NewRequest("GET", uri, nil)
	if err != nil {
		return "", err
	}
	req.Header.Add("Authorization", "Bearer "+m.token)

	resp, err := m.client.Do(req)
	if err != nil {
		return "", err
	}
	defer resp.Body.Close()

	if resp.StatusCode == http.StatusNotFound {
		return "", nil // Not found
	}

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		return "", fmt.Errorf("secrets API request failed: %d", resp.StatusCode)
	}

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", err
	}

	var parsed map[string]interface{}
	if err := json.Unmarshal(body, &parsed); err != nil {
		return "", err
	}

	secretObj, ok := parsed["secret"].(map[string]interface{})
	if !ok {
		return "", fmt.Errorf("secret object missing")
	}

	secretValue, ok := secretObj["secretValue"].(string)
	if !ok {
		return "", fmt.Errorf("secretValue missing")
	}

	m.cache.Store(cacheKey, cacheEntry{
		Value:     secretValue,
		ExpiresAt: time.Now().Add(5 * time.Minute),
	})

	return secretValue, nil
}

// List secrets in environment and path
func (m *Manager) List(environment, path string) (map[string]string, error) {
	if environment == "" {
		environment = "dev"
	}
	if path == "" {
		path = "/"
	}

	uri := fmt.Sprintf("%s/api/v3/secrets/raw?workspaceId=%s&environment=%s&secretPath=%s",
		m.baseURL, m.projectId, environment, url.QueryEscape(path))

	req, err := http.NewRequest("GET", uri, nil)
	if err != nil {
		return nil, err
	}
	req.Header.Add("Authorization", "Bearer "+m.token)

	resp, err := m.client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		return nil, fmt.Errorf("secrets list API request failed: %d", resp.StatusCode)
	}

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}

	var parsed map[string]interface{}
	if err := json.Unmarshal(body, &parsed); err != nil {
		return nil, err
	}

	secretsArr, ok := parsed["secrets"].([]interface{})
	if !ok {
		return nil, fmt.Errorf("secrets array missing")
	}

	result := make(map[string]string)
	for _, rawSec := range secretsArr {
		if sec, ok := rawSec.(map[string]interface{}); ok {
			k, _ := sec["secretKey"].(string)
			v, _ := sec["secretValue"].(string)
			if k != "" && v != "" {
				result[k] = v
			}
		}
	}

	return result, nil
}

func (m *Manager) Health() bool {
	resp, err := m.client.Get(m.baseURL + "/api/v1/health")
	if err != nil {
		return false
	}
	defer resp.Body.Close()
	return resp.StatusCode >= 200 && resp.StatusCode <= 299
}
