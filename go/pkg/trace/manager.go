package trace

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"strconv"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client   *http.Client
	endpoint string
	appName  string
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("trace")
	endpoint := mod["endpoint"]

	if endpoint == "" {
		endpoint = "http://opentelemetry-collector.zirve-infra.svc.cluster.local:4318"
	}

	appName := os.Getenv("APP_NAME")
	if appName == "" {
		appName = "zirve-go"
	}

	return &Manager{
		client:   &http.Client{Timeout: 5 * time.Second},
		endpoint: strings.TrimRight(endpoint, "/"),
		appName:  appName,
	}
}

// SendSpan creates an OpenTelemetry span via JSON REST API.
func (m *Manager) SendSpan(name string, startTime, endTime time.Time, traceId, parentId string, attributes map[string]interface{}) (string, error) {
	spanId := strings.ReplaceAll(uuid.New().String(), "-", "")[:16]

	startNano := strconv.FormatInt(startTime.UnixNano(), 10)
	endNano := strconv.FormatInt(endTime.UnixNano(), 10)

	env := os.Getenv("APP_ENV")
	if env == "" {
		env = "production"
	}

	payload := map[string]interface{}{
		"resourceSpans": []interface{}{
			map[string]interface{}{
				"resource": map[string]interface{}{
					"attributes": []interface{}{
						map[string]interface{}{
							"key": "service.name", "value": map[string]interface{}{"stringValue": m.appName},
						},
						map[string]interface{}{
							"key": "environment", "value": map[string]interface{}{"stringValue": env},
						},
					},
				},
				"scopeSpans": []interface{}{
					map[string]interface{}{
						"spans": []interface{}{
							map[string]interface{}{
								"traceId":           traceId,
								"spanId":            spanId,
								"parentSpanId":      parentId,
								"name":              name,
								"kind":              1, // SPAN_KIND_INTERNAL
								"startTimeUnixNano": startNano,
								"endTimeUnixNano":   endNano,
								"status":            map[string]interface{}{"code": 1}, // STATUS_CODE_OK
							},
						},
					},
				},
			},
		},
	}

	b, _ := json.Marshal(payload)
	req, err := http.NewRequest("POST", m.endpoint+"/v1/traces", bytes.NewBuffer(b))
	if err != nil {
		return "", err
	}
	req.Header.Set("Content-Type", "application/json")

	resp, err := m.client.Do(req)
	if err != nil {
		return "", err
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		return "", fmt.Errorf("otel api error: %d", resp.StatusCode)
	}

	return spanId, nil
}

func (m *Manager) Health() bool {
	return m.endpoint != ""
}
