package ingress

import (
	"encoding/json"
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
	mod := cfg.Module("ingress")
	urlStr := mod["url"]

	if urlStr == "" {
		urlStr = "http://traefik.kube-system.svc.cluster.local:8080"
	}

	return &Manager{
		client: &http.Client{},
		url:    strings.TrimRight(urlStr, "/"),
	}
}

func (m *Manager) request(path string) (interface{}, error) {
	resp, err := m.client.Get(m.url + "/" + strings.TrimLeft(path, "/"))
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode != 200 {
		return nil, nil // Not ready or not found
	}

	var res interface{}
	if err := json.NewDecoder(resp.Body).Decode(&res); err != nil && err != io.EOF {
		return nil, err
	}
	return res, nil
}

func (m *Manager) GetRoutes() (interface{}, error) {
	return m.request("api/http/routers")
}

func (m *Manager) GetMiddlewares() (interface{}, error) {
	return m.request("api/http/middlewares")
}

func (m *Manager) Health() bool {
	resp, err := m.client.Get(m.url + "/ping")
	if err != nil {
		return false
	}
	defer resp.Body.Close()
	return resp.StatusCode == 200
}
