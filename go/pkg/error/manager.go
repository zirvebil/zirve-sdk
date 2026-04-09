package error

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
	"net/url"
	"os"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client    *http.Client
	dsn       string
	url       string
	key       string
	projectId string
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("error")
	dsn := mod["dsn"]

	m := &Manager{
		client: &http.Client{Timeout: 5 * time.Second},
		dsn:    dsn,
	}

	if dsn != "" && strings.HasPrefix(dsn, "http") {
		u, err := url.Parse(dsn)
		if err == nil {
			m.url = fmt.Sprintf("%s://%s", u.Scheme, u.Host)
			if u.User != nil {
				m.key = u.User.Username()
			}
			m.projectId = strings.TrimLeft(u.Path, "/")
		}
	}

	return m
}

func (m *Manager) CaptureMessage(message, level string, context map[string]interface{}) (string, error) {
	if m.url == "" || m.key == "" || m.projectId == "" {
		return "", nil // Silent skip if DSN not configured
	}

	if level == "" {
		level = "info"
	}

	eventId := strings.ReplaceAll(uuid.New().String(), "-", "")
	timestamp := time.Now().UTC().Format(time.RFC3339)
	env := os.Getenv("APP_ENV")
	if env == "" {
		env = "production"
	}
	serverName, _ := os.Hostname()

	payload := map[string]interface{}{
		"event_id":    eventId,
		"timestamp":   timestamp,
		"platform":    "go",
		"level":       level,
		"environment": env,
		"server_name": serverName,
		"message":     message,
		"extra":       context,
		"exception": map[string]interface{}{
			"values": []interface{}{
				map[string]interface{}{
					"type":  "Exception",
					"value": message,
				},
			},
		},
	}

	b, _ := json.Marshal(payload)
	reqUrl := fmt.Sprintf("%s/api/%s/store/", m.url, m.projectId)

	req, err := http.NewRequest("POST", reqUrl, bytes.NewBuffer(b))
	if err != nil {
		return "", err
	}
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("X-Sentry-Auth", fmt.Sprintf("Sentry sentry_version=7, sentry_key=%s, sentry_client=zirve-go/0.1.0", m.key))

	resp, err := m.client.Do(req)
	if err != nil {
		return "", err
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		return "", fmt.Errorf("sentry api error: %d", resp.StatusCode)
	}

	return eventId, nil
}

func (m *Manager) CaptureException(err error, context map[string]interface{}) (string, error) {
	if err == nil {
		return "", nil
	}
	return m.CaptureMessage(err.Error(), "error", context)
}

func (m *Manager) Health() bool {
	return m.dsn != ""
}
