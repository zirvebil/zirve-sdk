package crm

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strings"
	"sync"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client   *http.Client
	url      string
	db       string
	user     string
	password string

	mu    sync.Mutex
	uid   int
	rpcId int
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("crm")
	url := mod["url"]
	db := mod["database"]
	user := mod["username"]
	password := mod["password"]

	if url == "" {
		url = "http://odoo.zirve-infra.svc.cluster.local:8069"
	}
	if db == "" {
		db = "zirve"
	}
	if user == "" {
		user = "admin"
	}
	if password == "" {
		password = "admin"
	}

	return &Manager{
		client:   &http.Client{},
		url:      strings.TrimRight(url, "/"),
		db:       db,
		user:     user,
		password: password,
	}
}

func (m *Manager) rpc(method string, params interface{}) (interface{}, error) {
	m.mu.Lock()
	m.rpcId++
	reqId := m.rpcId
	m.mu.Unlock()

	payload := map[string]interface{}{
		"jsonrpc": "2.0",
		"method":  method,
		"params":  params,
		"id":      reqId,
	}

	b, _ := json.Marshal(payload)
	req, err := http.NewRequest("POST", m.url+"/jsonrpc", bytes.NewBuffer(b))
	if err != nil {
		return nil, err
	}
	req.Header.Set("Content-Type", "application/json")

	resp, err := m.client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		return nil, fmt.Errorf("odoo api error: %d", resp.StatusCode)
	}

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}

	var res map[string]interface{}
	if err := json.Unmarshal(body, &res); err != nil {
		return nil, err
	}

	if errObj, ok := res["error"]; ok && errObj != nil {
		b, _ := json.Marshal(errObj)
		return nil, fmt.Errorf("odoo rpc error: %s", string(b))
	}

	return res["result"], nil
}

func (m *Manager) Authenticate() (int, error) {
	m.mu.Lock()
	if m.uid != 0 {
		m.mu.Unlock()
		return m.uid, nil
	}
	m.mu.Unlock()

	res, err := m.rpc("call", map[string]interface{}{
		"service": "common",
		"method":  "authenticate",
		"args":    []interface{}{m.db, m.user, m.password, map[string]interface{}{}},
	})
	if err != nil {
		return 0, err
	}

	uidFloat, ok := res.(float64)
	if !ok || uidFloat == 0 {
		return 0, fmt.Errorf("odoo authentication failed")
	}

	uid := int(uidFloat)
	m.mu.Lock()
	m.uid = uid
	m.mu.Unlock()

	return uid, nil
}

func (m *Manager) ExecuteKw(model, method string, args []interface{}, kwargs map[string]interface{}) (interface{}, error) {
	uid, err := m.Authenticate()
	if err != nil {
		return nil, err
	}

	if kwargs == nil {
		kwargs = make(map[string]interface{})
	}

	return m.rpc("call", map[string]interface{}{
		"service": "object",
		"method":  "execute_kw",
		"args":    []interface{}{m.db, uid, m.password, model, method, args, kwargs},
	})
}

func (m *Manager) Health() bool {
	res, err := m.rpc("call", map[string]interface{}{
		"service": "common",
		"method":  "version",
		"args":    []interface{}{},
	})
	return err == nil && res != nil
}
