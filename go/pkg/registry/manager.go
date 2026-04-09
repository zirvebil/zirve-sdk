package registry

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
	user   string
	pass   string
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("registry")
	urlStr := mod["url"]
	user := mod["username"]
	pass := mod["password"]

	if urlStr == "" {
		urlStr = "http://harbor-core.zirve-infra.svc.cluster.local"
	}
	if user == "" {
		user = "admin"
	}
	if pass == "" {
		pass = "Harbor12345"
	}

	return &Manager{
		client: &http.Client{},
		url:    strings.TrimRight(urlStr, "/") + "/api/v2.0",
		user:   user,
		pass:   pass,
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
	if m.pass != "" {
		req.SetBasicAuth(m.user, m.pass)
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
		return nil, fmt.Errorf("harbor api error [%d]: %s", resp.StatusCode, string(errBody))
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

func (m *Manager) ListProjects() (interface{}, error) {
	return m.request("GET", "projects", nil)
}

func (m *Manager) ListImages(projectName string) (interface{}, error) {
	return m.request("GET", fmt.Sprintf("projects/%s/repositories", url.PathEscape(projectName)), nil)
}

func (m *Manager) ScanImage(projectName, repositoryName, reference string) (bool, error) {
	_, err := m.request("POST", fmt.Sprintf("projects/%s/repositories/%s/artifacts/%s/scan", 
		url.PathEscape(projectName), url.PathEscape(repositoryName), url.PathEscape(reference)), nil)
	return err == nil, err
}

func (m *Manager) ScanReport(projectName, repositoryName, reference string) (interface{}, error) {
	return m.request("GET", fmt.Sprintf("projects/%s/repositories/%s/artifacts/%s/additions/vulnerabilities", 
		url.PathEscape(projectName), url.PathEscape(repositoryName), url.PathEscape(reference)), nil)
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
