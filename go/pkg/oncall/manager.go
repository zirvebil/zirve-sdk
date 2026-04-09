package oncall

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
	mod := cfg.Module("oncall")
	urlStr := mod["url"]
	token := mod["token"]

	if urlStr == "" {
		urlStr = "http://grafana-oncall-engine.zirve-infra.svc.cluster.local:8080"
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
		req.Header.Set("Authorization", "Token "+m.token)
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
		return nil, fmt.Errorf("oncall api error [%d]: %s", resp.StatusCode, string(errBody))
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

func (m *Manager) CreateAlert(integrationUrl, title, message, state string) (bool, error) {
	if state == "" {
		state = "alerting"
	}

	payload := map[string]string{
		"title":   title,
		"message": message,
		"state":   state,
	}
	
	b, _ := json.Marshal(payload)
	
	// Create an alert using the provided webhook/integration URL (typically separate from auth api)
	req, err := http.NewRequest("POST", integrationUrl, bytes.NewReader(b))
	if err != nil {
		return false, err
	}
	req.Header.Set("Content-Type", "application/json")
	
	resp, err := m.client.Do(req)
	if err != nil {
		return false, err
	}
	defer resp.Body.Close()
	
	return resp.StatusCode >= 200 && resp.StatusCode <= 299, nil
}

func (m *Manager) ListIncidents(state string) (interface{}, error) {
	if state == "" {
		state = "triggered"
	}
	res, err := m.request("GET", fmt.Sprintf("alert_groups?state=%s", url.QueryEscape(state)), nil)
	if err != nil {
		return nil, err
	}
	if obj, ok := res.(map[string]interface{}); ok {
		if results, ok := obj["results"]; ok {
			return results, nil
		}
	}
	return res, nil
}

func (m *Manager) Health() bool {
	req, _ := http.NewRequest("GET", m.url+"/health", nil)
	resp, err := m.client.Do(req)
	if err != nil {
		return false
	}
	defer resp.Body.Close()
	return resp.StatusCode == 200
}
