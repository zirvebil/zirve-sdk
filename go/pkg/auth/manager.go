package auth

import (
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client  *http.Client
	baseURL string
	realm   string
	cfg     *config.Manager
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("auth")
	baseURL := mod["url"]
	realm := mod["realm"]

	if baseURL == "" {
		baseURL = "http://keycloak.zirve-infra.svc.cluster.local"
	}
	if realm == "" {
		realm = "zirve"
	}

	return &Manager{
		client:  &http.Client{},
		baseURL: strings.TrimRight(baseURL, "/"),
		realm:   realm,
		cfg:     cfg,
	}
}

// VerifyToken introspects an OAuth2 token
func (m *Manager) VerifyToken(token string) (map[string]interface{}, error) {
	mod := m.cfg.Module("auth")
	clientId := mod["client_id"]
	if clientId == "" {
		clientId = "zirve-backend"
	}
	clientSecret := mod["client_secret"]

	data := url.Values{}
	data.Set("token", token)
	data.Set("client_id", clientId)
	data.Set("client_secret", clientSecret)

	reqUrl := fmt.Sprintf("%s/realms/%s/protocol/openid-connect/token/introspect", m.baseURL, m.realm)
	req, err := http.NewRequest("POST", reqUrl, strings.NewReader(data.Encode()))
	if err != nil {
		return nil, err
	}
	req.Header.Add("Content-Type", "application/x-www-form-urlencoded")

	resp, err := m.client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		return nil, fmt.Errorf("auth API error: %d", resp.StatusCode)
	}

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}

	var res map[string]interface{}
	err = json.Unmarshal(body, &res)
	return res, err
}

// HasRole checks if introspection validates the given role
func (m *Manager) HasRole(token, roleName string) (bool, error) {
	claims, err := m.VerifyToken(token)
	if err != nil {
		return false, err
	}

	if active, ok := claims["active"].(bool); !ok || !active {
		return false, nil
	}

	if realmAccess, ok := claims["realm_access"].(map[string]interface{}); ok {
		if roles, ok := realmAccess["roles"].([]interface{}); ok {
			for _, r := range roles {
				if rStr, ok := r.(string); ok && rStr == roleName {
					return true, nil
				}
			}
		}
	}

	return false, nil
}

// GetUser retrieves OIDC UserInfo
func (m *Manager) GetUser(token string) (map[string]interface{}, error) {
	reqUrl := fmt.Sprintf("%s/realms/%s/protocol/openid-connect/userinfo", m.baseURL, m.realm)
	req, err := http.NewRequest("GET", reqUrl, nil)
	if err != nil {
		return nil, err
	}
	req.Header.Add("Authorization", "Bearer "+token)

	resp, err := m.client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		return nil, fmt.Errorf("userInfo request failed: %d", resp.StatusCode)
	}

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}

	var res map[string]interface{}
	err = json.Unmarshal(body, &res)
	return res, err
}

// Health readiness check
func (m *Manager) Health() bool {
	reqUrl := fmt.Sprintf("%s/realms/%s/.well-known/openid-configuration", m.baseURL, m.realm)
	resp, err := m.client.Get(reqUrl)
	if err != nil {
		return false
	}
	defer resp.Body.Close()
	return resp.StatusCode >= 200 && resp.StatusCode <= 299
}
