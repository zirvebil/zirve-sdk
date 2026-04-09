package quality

import (
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client *http.Client
	url    string
	token  string
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("quality")
	urlStr := mod["url"]
	token := mod["token"]

	if urlStr == "" {
		urlStr = "http://sonarqube-sonarqube.zirve-infra.svc.cluster.local:9000"
	}

	return &Manager{
		client: &http.Client{},
		url:    strings.TrimRight(urlStr, "/") + "/api",
		token:  token, // Note: SonarToken might be passed in BasicAuth username sometimes, or Bearer later versions. Use Bearer here mapping.
	}
}

func (m *Manager) request(method, path string) (interface{}, error) {
	req, err := http.NewRequest(method, m.url+"/"+strings.TrimLeft(path, "/"), nil)
	if err != nil {
		return nil, err
	}
	if m.token != "" {
		req.Header.Set("Authorization", "Bearer "+m.token)
	}

	resp, err := m.client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode == http.StatusNotFound {
		return nil, nil
	}

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		errBody, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("sonarqube api error [%d]: %s", resp.StatusCode, string(errBody))
	}

	if resp.ContentLength == 0 || resp.StatusCode == http.StatusNoContent {
		return nil, nil
	}

	var res interface{}
	if err := json.NewDecoder(resp.Body).Decode(&res); err != nil && err != io.EOF {
		return nil, err
	}

	return res, nil
}

func (m *Manager) GetQualityGate(projectKey string) (string, error) {
	res, err := m.request("GET", fmt.Sprintf("qualitygates/project_status?projectKey=%s", url.QueryEscape(projectKey)))
	if err != nil || res == nil {
		return "UNKNOWN", err
	}

	if val, ok := res.(map[string]interface{}); ok {
		if ps, ok := val["projectStatus"].(map[string]interface{}); ok {
			if st, ok := ps["status"].(string); ok {
				return st, nil
			}
		}
	}
	return "UNKNOWN", nil
}

func (m *Manager) CheckPassed(projectKey string) (bool, error) {
	st, err := m.GetQualityGate(projectKey)
	return st == "OK", err
}

func (m *Manager) Health() bool {
	res, err := m.request("GET", "system/health")
	if err != nil || res == nil {
		return false
	}
	if val, ok := res.(map[string]interface{}); ok {
		if hs, ok := val["health"].(string); ok {
			return hs == "GREEN"
		}
	}
	return false
}
