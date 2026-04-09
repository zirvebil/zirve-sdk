package log

import (
	"bytes"
	"encoding/json"
	"net/http"
	"os"
	"strconv"
	"sync"
	"time"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client  *http.Client
	url     string
	appName string
	env     string

	mu     sync.Mutex
	buffer []map[string]interface{}
	stop   chan struct{}
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("log")
	lokiUrl := mod["url"]
	if lokiUrl == "" {
		lokiUrl = "http://loki.zirve-infra.svc.cluster.local:3100"
	}

	appName := os.Getenv("APP_NAME")
	if appName == "" {
		appName = "zirve-go"
	}

	env := os.Getenv("APP_ENV")
	if env == "" {
		env = "production"
	}

	m := &Manager{
		client:  &http.Client{Timeout: 5 * time.Second},
		url:     lokiUrl,
		appName: appName,
		env:     env,
		buffer:  make([]map[string]interface{}, 0),
		stop:    make(chan struct{}),
	}

	go m.flusher()
	return m
}

func (m *Manager) flusher() {
	ticker := time.NewTicker(3 * time.Second)
	defer ticker.Stop()

	for {
		select {
		case <-ticker.C:
			m.Flush()
		case <-m.stop:
			m.Flush()
			return
		}
	}
}

// Flush sends buffer to Loki
func (m *Manager) Flush() {
	m.mu.Lock()
	if len(m.buffer) == 0 {
		m.mu.Unlock()
		return
	}
	
	entries := make([]interface{}, 0, len(m.buffer))
	for _, log := range m.buffer {
		ts := log["timestamp"]
		delete(log, "timestamp")
		
		val, _ := json.Marshal(log)
		entries = append(entries, []interface{}{ts, string(val)})
	}
	m.buffer = make([]map[string]interface{}, 0)
	m.mu.Unlock()

	payload := map[string]interface{}{
		"streams": []interface{}{
			map[string]interface{}{
				"stream": map[string]string{
					"app": m.appName,
					"env": m.env,
				},
				"values": entries,
			},
		},
	}

	payloadBytes, _ := json.Marshal(payload)
	req, _ := http.NewRequest("POST", m.url+"/loki/api/v1/push", bytes.NewReader(payloadBytes))
	req.Header.Set("Content-Type", "application/json")

	resp, err := m.client.Do(req)
	if err == nil {
		resp.Body.Close()
	}
}

func (m *Manager) write(level, message string, context map[string]interface{}) {
	ts := strconv.FormatInt(time.Now().UnixNano(), 10)
	
	ctxStr := "{}"
	if context != nil {
		b, _ := json.Marshal(context)
		ctxStr = string(b)
	}

	entry := map[string]interface{}{
		"timestamp": ts,
		"level":     level,
		"message":   message,
		"context":   ctxStr,
	}

	m.mu.Lock()
	m.buffer = append(m.buffer, entry)
	m.mu.Unlock()
}

func (m *Manager) Info(message string, context map[string]interface{}) {
	m.write("info", message, context)
}

func (m *Manager) Error(message string, context map[string]interface{}) {
	m.write("error", message, context)
}

func (m *Manager) Warn(message string, context map[string]interface{}) {
	m.write("warn", message, context)
}

func (m *Manager) Debug(message string, context map[string]interface{}) {
	m.write("debug", message, context)
}

func (m *Manager) Health() bool {
	resp, err := m.client.Get(m.url + "/ready")
	if err != nil {
		return false
	}
	defer resp.Body.Close()
	return resp.StatusCode == 200
}

func (m *Manager) Close() {
	close(m.stop)
}
