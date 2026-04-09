package testing

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
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("testing")
	urlStr := mod["url"]

	if urlStr == "" {
		urlStr = "http://keploy.zirve-infra.svc.cluster.local:6789"
	}

	return &Manager{
		client: &http.Client{},
		url:    strings.TrimRight(urlStr, "/") + "/api",
	}
}

func (m *Manager) request(method, path string) (interface{}, error) {
	req, err := http.NewRequest(method, m.url+"/"+strings.TrimLeft(path, "/"), nil)
	if err != nil {
		return nil, err
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
		return nil, fmt.Errorf("keploy api error [%d]: %s", resp.StatusCode, string(errBody))
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

func (m *Manager) ListTestSets(app string) (interface{}, error) {
	return m.request("GET", fmt.Sprintf("test-sets?app=%s", url.QueryEscape(app)))
}

func (m *Manager) GetTestRun(id string) (interface{}, error) {
	return m.request("GET", fmt.Sprintf("test-run/%s", url.PathEscape(id)))
}

func (m *Manager) Health() bool {
	req, _ := http.NewRequest("GET", m.url+"/healthz", nil)
	resp, err := m.client.Do(req)
	if err != nil {
		return false
	}
	defer resp.Body.Close()
	return resp.StatusCode == 200
}
