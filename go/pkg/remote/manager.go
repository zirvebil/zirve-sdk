package remote

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
	"sync"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client *http.Client
	url    string
	user   string
	pass   string
	
	mu    sync.Mutex
	token string
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("remote")
	urlStr := mod["url"]
	user := mod["username"]
	pass := mod["password"]

	if urlStr == "" {
		urlStr = "http://guacamole.zirve-infra.svc.cluster.local:8080"
	}
	if user == "" {
		user = "guacadmin"
	}
	if pass == "" {
		pass = "guacadmin"
	}

	return &Manager{
		client: &http.Client{},
		url:    strings.TrimRight(urlStr, "/") + "/api",
		user:   user,
		pass:   pass,
	}
}

func (m *Manager) getToken() (string, error) {
	m.mu.Lock()
	if m.token != "" {
		m.mu.Unlock()
		return m.token, nil
	}
	m.mu.Unlock()

	data := url.Values{}
	data.Set("username", m.user)
	data.Set("password", m.pass)

	req, err := http.NewRequest("POST", m.url+"/tokens", strings.NewReader(data.Encode()))
	if err != nil {
		return "", err
	}
	req.Header.Set("Content-Type", "application/x-www-form-urlencoded")

	resp, err := m.client.Do(req)
	if err != nil {
		return "", err
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		return "", fmt.Errorf("guacamole auth failed: %d", resp.StatusCode)
	}

	var res map[string]interface{}
	if err := json.NewDecoder(resp.Body).Decode(&res); err != nil {
		return "", err
	}

	token, ok := res["authToken"].(string)
	if !ok {
		return "", fmt.Errorf("authToken missing in guacamole response")
	}

	m.mu.Lock()
	m.token = token
	m.mu.Unlock()

	return token, nil
}

func (m *Manager) request(method, path string, body interface{}) (map[string]interface{}, error) {
	token, err := m.getToken()
	if err != nil {
		return nil, err
	}

	var reqBody io.Reader
	if body != nil {
		b, err := json.Marshal(body)
		if err != nil {
			return nil, err
		}
		reqBody = bytes.NewBuffer(b)
	}

	reqUrl := fmt.Sprintf("%s/%s?token=%s", m.url, strings.TrimLeft(path, "/"), url.QueryEscape(token))
	req, err := http.NewRequest(method, reqUrl, reqBody)
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
		return nil, fmt.Errorf("guacamole api error [%d]: %s", resp.StatusCode, string(errBody))
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

func (m *Manager) CreateConnection(sourceId, name, protocol string, parameters map[string]string) (string, error) {
	res, err := m.request("POST", "session/data/postgresql/connections", map[string]interface{}{
		"parentIdentifier": sourceId,
		"name":             name,
		"protocol":         protocol,
		"parameters":       parameters,
	})
	if err != nil {
		return "", err
	}
	if res == nil {
		return "", nil
	}
	id, _ := res["identifier"].(string)
	return id, nil
}

func (m *Manager) Health() bool {
	req, _ := http.NewRequest("POST", m.url+"/tokens", strings.NewReader(""))
	req.Header.Set("Content-Type", "application/x-www-form-urlencoded")
	resp, err := m.client.Do(req)
	if err != nil {
		return false
	}
	defer resp.Body.Close()
	return resp.StatusCode != http.StatusServiceUnavailable && resp.StatusCode != http.StatusNotFound
}
