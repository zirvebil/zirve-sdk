package search

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client  *http.Client
	baseURL string
	user    string
	pass    string
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("search")

	host := mod["host"]
	port := mod["port"]
	user := mod["username"]
	pass := mod["password"]

	if host == "" {
		host = "elasticsearch-master.zirve-infra.svc.cluster.local"
	}
	if port == "" {
		port = "9200"
	}
	if user == "" {
		user = "elastic"
	}

	return &Manager{
		client:  &http.Client{},
		baseURL: fmt.Sprintf("http://%s:%s", host, port),
		user:    user,
		pass:    pass,
	}
}

// Index creates or updates a document
func (m *Manager) Index(index, id string, document interface{}) (map[string]interface{}, error) {
	b, err := json.Marshal(document)
	if err != nil {
		return nil, err
	}

	method := "POST"
	path := fmt.Sprintf("/%s/_doc", index)
	if id != "" {
		method = "PUT"
		path = fmt.Sprintf("/%s/_doc/%s", index, id)
	}

	req, err := http.NewRequest(method, m.baseURL+path, bytes.NewBuffer(b))
	if err != nil {
		return nil, err
	}
	req.Header.Add("Content-Type", "application/json")
	if m.pass != "" {
		req.SetBasicAuth(m.user, m.pass)
	}

	return m.doParse(req)
}

// Search executes a raw query
func (m *Manager) Search(index string, query interface{}) (map[string]interface{}, error) {
	b, err := json.Marshal(query)
	if err != nil {
		return nil, err
	}

	path := fmt.Sprintf("/%s/_search", index)
	req, err := http.NewRequest("POST", m.baseURL+path, bytes.NewBuffer(b))
	if err != nil {
		return nil, err
	}
	req.Header.Add("Content-Type", "application/json")
	if m.pass != "" {
		req.SetBasicAuth(m.user, m.pass)
	}

	return m.doParse(req)
}

func (m *Manager) doParse(req *http.Request) (map[string]interface{}, error) {
	resp, err := m.client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		return nil, fmt.Errorf("elasticsearch API error: %d", resp.StatusCode)
	}

	var res map[string]interface{}
	err = json.NewDecoder(resp.Body).Decode(&res)
	return res, err
}

func (m *Manager) Health() bool {
	req, err := http.NewRequest("GET", m.baseURL+"/_cluster/health", nil)
	if err != nil {
		return false
	}
	if m.pass != "" {
		req.SetBasicAuth(m.user, m.pass)
	}

	resp, err := m.client.Do(req)
	if err != nil {
		return false
	}
	defer resp.Body.Close()

	if resp.StatusCode != 200 {
		return false
	}

	var res map[string]interface{}
	if err := json.NewDecoder(resp.Body).Decode(&res); err != nil {
		return false
	}

	status, _ := res["status"].(string)
	return status == "green" || status == "yellow"
}
