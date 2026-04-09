package billing

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strings"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client *http.Client
	url    string
	apiKey string
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("billing")
	baseURL := mod["url"]
	apiKey := mod["api_key"]

	if baseURL == "" {
		baseURL = "http://lago-api.zirve-infra.svc.cluster.local"
	}

	return &Manager{
		client: &http.Client{},
		url:    strings.TrimRight(baseURL, "/") + "/api/v1",
		apiKey: apiKey,
	}
}

func (m *Manager) request(method, path string, body interface{}) (map[string]interface{}, error) {
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
	if m.apiKey != "" {
		req.Header.Set("Authorization", "Bearer "+m.apiKey)
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
		return nil, fmt.Errorf("lago api error [%d]: %s", resp.StatusCode, string(errBody))
	}

	if resp.ContentLength == 0 || resp.StatusCode == http.StatusNoContent {
		return nil, nil
	}

	var res map[string]interface{}
	if err := json.NewDecoder(resp.Body).Decode(&res); err != nil && err != io.EOF {
		return nil, err
	}
	return res, nil
}

func (m *Manager) CreateCustomer(customer interface{}) (map[string]interface{}, error) {
	return m.request("POST", "customers", map[string]interface{}{"customer": customer})
}

func (m *Manager) CreateSubscription(sub interface{}) (map[string]interface{}, error) {
	return m.request("POST", "subscriptions", map[string]interface{}{"subscription": sub})
}

func (m *Manager) AddEvent(event interface{}) (map[string]interface{}, error) {
	return m.request("POST", "events", map[string]interface{}{"event": event})
}

func (m *Manager) Health() bool {
	req, _ := http.NewRequest("GET", m.url+"/organizations", nil)
	if m.apiKey != "" {
		req.Header.Set("Authorization", "Bearer "+m.apiKey)
	}
	resp, err := m.client.Do(req)
	if err != nil {
		return false
	}
	defer resp.Body.Close()
	return resp.StatusCode == 200 || resp.StatusCode == 401
}
