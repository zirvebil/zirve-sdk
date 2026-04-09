package gateway

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
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("gateway")
	urlStr := mod["url"]

	if urlStr == "" {
		urlStr = "http://kong-kong-admin.zirve-infra.svc.cluster.local:8001"
	}

	return &Manager{
		client: &http.Client{},
		url:    strings.TrimRight(urlStr, "/"),
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
		return nil, fmt.Errorf("kong api error [%d]: %s", resp.StatusCode, string(errBody))
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

func (m *Manager) AddService(name, protocol, host string, port int, path string) (map[string]interface{}, error) {
	return m.request("POST", "services", map[string]interface{}{
		"name":     name,
		"protocol": protocol,
		"host":     host,
		"port":     port,
		"path":     path,
	})
}

func (m *Manager) AddRoute(serviceIdOrName, name string, paths []string) (map[string]interface{}, error) {
	return m.request("POST", fmt.Sprintf("services/%s/routes", serviceIdOrName), map[string]interface{}{
		"name":       name,
		"paths":      paths,
		"strip_path": true,
	})
}

func (m *Manager) Health() bool {
	resp, err := m.client.Get(m.url + "/status")
	if err != nil {
		return false
	}
	defer resp.Body.Close()
	return resp.StatusCode == 200
}
