package queue

import (
	"bytes"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"net/http"
	"net/url"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client     *http.Client
	apiBase    string
	authHeader string
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("queue")

	host := mod["host"]
	apiPort := mod["api_port"]
	user := mod["user"]
	pass := mod["password"]

	if host == "" {
		host = "rabbitmq.zirve-infra.svc.cluster.local"
	}
	if apiPort == "" {
		apiPort = "15672"
	}
	if user == "" {
		user = "guest"
	}
	if pass == "" {
		pass = "guest"
	}

	auth := base64.StdEncoding.EncodeToString([]byte(user + ":" + pass))

	return &Manager{
		client:     &http.Client{},
		apiBase:    fmt.Sprintf("http://%s:%s/api", host, apiPort),
		authHeader: "Basic " + auth,
	}
}

// Publish queues a generic interface using RabbitMQ HTTP Management API
func (m *Manager) Publish(vhost, exchange, routingKey string, payload interface{}) (bool, error) {
	if exchange == "" {
		exchange = "amq.default"
	}

	var payloadStr string
	if str, ok := payload.(string); ok {
		payloadStr = str
	} else {
		b, err := json.Marshal(payload)
		if err != nil {
			return false, err
		}
		payloadStr = string(b)
	}

	body := map[string]interface{}{
		"properties":       map[string]interface{}{},
		"routing_key":      routingKey,
		"payload":          payloadStr,
		"payload_encoding": "string",
	}

	jsonValue, _ := json.Marshal(body)
	reqUrl := fmt.Sprintf("%s/exchanges/%s/%s/publish", m.apiBase, url.PathEscape(vhost), url.PathEscape(exchange))

	req, err := http.NewRequest("POST", reqUrl, bytes.NewBuffer(jsonValue))
	if err != nil {
		return false, err
	}
	req.Header.Add("Authorization", m.authHeader)
	req.Header.Add("Content-Type", "application/json")

	resp, err := m.client.Do(req)
	if err != nil {
		return false, err
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		return false, fmt.Errorf("queue publish failed: %d", resp.StatusCode)
	}

	var res map[string]interface{}
	err = json.NewDecoder(resp.Body).Decode(&res)
	if err != nil {
		return false, err
	}

	if routed, ok := res["routed"].(bool); ok {
		return routed, nil
	}

	return false, nil
}

func (m *Manager) Health() bool {
	req, err := http.NewRequest("GET", m.apiBase+"/overview", nil)
	if err != nil {
		return false
	}
	req.Header.Add("Authorization", m.authHeader)

	resp, err := m.client.Do(req)
	if err != nil {
		return false
	}
	defer resp.Body.Close()

	return resp.StatusCode >= 200 && resp.StatusCode <= 299
}
