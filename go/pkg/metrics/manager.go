package metrics

import (
	"bytes"
	"fmt"
	"net/http"
	"net/url"
	"os"
	"strings"
	"time"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client  *http.Client
	url     string
	jobName string
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("metrics")
	metricsUrl := mod["url"]

	if metricsUrl == "" {
		metricsUrl = "http://prometheus-server.zirve-infra.svc.cluster.local:9090"
	}

	app := os.Getenv("APP_NAME")
	if app == "" {
		app = "zirve-go"
	}

	return &Manager{
		client:  &http.Client{Timeout: 5 * time.Second},
		url:     strings.TrimRight(metricsUrl, "/"),
		jobName: app,
	}
}

// Push uploads metrics via Pushgateway text format
func (m *Manager) Push(metrics []map[string]interface{}) (bool, error) {
	if len(metrics) == 0 {
		return true, nil
	}

	var buf bytes.Buffer
	for _, metric := range metrics {
		name, nOk := metric["name"].(string)
		val, vOk := metric["value"]
		if !nOk || !vOk {
			continue
		}

		typ, ok := metric["type"].(string)
		if !ok {
			typ = "gauge"
		}

		help, ok := metric["help"].(string)
		if !ok {
			help = "Zirve SDK Metric"
		}

		buf.WriteString(fmt.Sprintf("# HELP %s %s\n", name, help))
		buf.WriteString(fmt.Sprintf("# TYPE %s %s\n", name, typ))
		buf.WriteString(fmt.Sprintf("%s %v\n", name, val))
	}

	reqUrl := fmt.Sprintf("%s/metrics/job/%s", m.url, url.PathEscape(m.jobName))
	req, err := http.NewRequest("POST", reqUrl, &buf)
	if err != nil {
		return false, err
	}
	req.Header.Set("Content-Type", "text/plain")

	resp, err := m.client.Do(req)
	if err != nil {
		return false, err
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		// Log but don't crash
		return false, fmt.Errorf("prometheus push failed: %d", resp.StatusCode)
	}

	return true, nil
}

func (m *Manager) Health() bool {
	return m.url != ""
}
