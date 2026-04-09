package analytics

import (
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strings"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client  *http.Client
	baseURL string
	user    string
	pass    string
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("analytics")
	host := mod["host"]
	port := mod["port"]
	user := mod["username"]
	pass := mod["password"]

	if host == "" {
		host = "clickhouse.zirve-infra.svc.cluster.local"
	}
	if port == "" {
		port = "8123"
	}
	if user == "" {
		user = "default"
	}

	return &Manager{
		client:  &http.Client{},
		baseURL: fmt.Sprintf("http://%s:%s", host, port),
		user:    user,
		pass:    pass,
	}
}

// Query issues JSONFormat query to Clickhouse HTTP API
func (m *Manager) Query(sql string, params map[string]interface{}) (map[string]interface{}, error) {
	// Simple parameter replacement for typical basic queries
	if params != nil {
		for k, v := range params {
			var valStr string
			switch t := v.(type) {
			case string:
				valStr = "'" + strings.ReplaceAll(t, "'", "''") + "'"
			default:
				valStr = fmt.Sprintf("%v", v)
			}
			sql = strings.ReplaceAll(sql, "{"+k+"}", valStr)
		}
	}

	// Always append FORMAT JSON
	sql = strings.TrimSpace(sql)
	if !strings.HasSuffix(strings.ToUpper(sql), "FORMAT JSON") {
		if strings.HasSuffix(sql, ";") {
			sql = strings.TrimSuffix(sql, ";") + " FORMAT JSON;"
		} else {
			sql += " FORMAT JSON"
		}
	}

	req, err := http.NewRequest("POST", m.baseURL+"/", strings.NewReader(sql))
	if err != nil {
		return nil, err
	}
	req.Header.Set("X-ClickHouse-User", m.user)
	if m.pass != "" {
		req.Header.Set("X-ClickHouse-Key", m.pass)
	}

	resp, err := m.client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		body, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("clickhouse error: %d - %s", resp.StatusCode, string(body))
	}

	// Inserts won't always return JSON data body if they are successful
	if resp.ContentLength == 0 {
		return map[string]interface{}{"status": "success"}, nil
	}

	var res map[string]interface{}
	err = json.NewDecoder(resp.Body).Decode(&res)
	
	if err == io.EOF {
		return map[string]interface{}{"status": "success"}, nil
	}

	return res, err
}

func (m *Manager) Health() bool {
	req, err := http.NewRequest("GET", m.baseURL+"/ping", nil)
	if err != nil {
		return false
	}
	
	resp, err := m.client.Do(req)
	if err != nil {
		return false
	}
	defer resp.Body.Close()

	body, _ := io.ReadAll(resp.Body)
	return strings.TrimSpace(string(body)) == "Ok."
}
