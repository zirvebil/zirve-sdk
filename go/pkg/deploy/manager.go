package deploy

import (
	"bytes"
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
	mod := cfg.Module("deploy")
	urlStr := mod["url"]
	token := mod["token"]

	if urlStr == "" {
		urlStr = "http://argocd-server.argocd.svc.cluster.local"
	}

	return &Manager{
		client: &http.Client{},
		url:    strings.TrimRight(urlStr, "/") + "/api/v1",
		token:  token,
	}
}

func (m *Manager) request(method, path string, body interface{}) (interface{}, error) {
	var reqBody io.Reader
	if body != nil {
		b, err := json.Marshal(body)
		if err != nil {
			return nil, err
		}
		reqBody = bytes.NewBuffer(b)
	}

	req, err := http.NewRequest(method, m.url+"/"+strings.TrimLeft(path, "/"), reqBody)
	if err != nil {
		return nil, err
	}
	req.Header.Set("Content-Type", "application/json")
	if m.token != "" {
		req.Header.Set("Authorization", "Bearer "+m.token)
	}

	resp, err := m.client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode == http.StatusNotFound {
		return nil, nil // Not found
	}

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		errBody, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("argocd api error [%d]: %s", resp.StatusCode, string(errBody))
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

func (m *Manager) ListApplications() (interface{}, error) {
	res, err := m.request("GET", "applications", nil)
	if err != nil {
		return nil, err
	}
	if obj, ok := res.(map[string]interface{}); ok {
		if items, hasItems := obj["items"]; hasItems {
			return items, nil
		}
	}
	return res, nil
}

func (m *Manager) Sync(applicationName string) (interface{}, error) {
	return m.request("POST", fmt.Sprintf("applications/%s/sync", url.PathEscape(applicationName)), nil)
}

func (m *Manager) Status(applicationName string) (interface{}, error) {
	return m.request("GET", fmt.Sprintf("applications/%s", url.PathEscape(applicationName)), nil)
}

func (m *Manager) Health() bool {
	req, _ := http.NewRequest("GET", m.url+"/version", nil)
	resp, err := m.client.Do(req)
	if err != nil {
		return false
	}
	defer resp.Body.Close()
	return resp.StatusCode == 200
}
