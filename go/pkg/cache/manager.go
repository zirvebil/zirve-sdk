package cache

import (
	"context"
	"encoding/json"
	"time"

	"github.com/redis/go-redis/v9"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	client *redis.Client
	prefix string
}

func NewManager(cfg *config.Manager) *Manager {
	mod := cfg.Module("cache")
	host := mod["host"]
	port := mod["port"]
	password := mod["password"]
	prefix := mod["prefix"]

	if host == "" {
		host = "localhost"
	}
	if port == "" {
		port = "6379"
	}
	if prefix == "" {
		prefix = "zirve:"
	}

	client := redis.NewClient(&redis.Options{
		Addr:     host + ":" + port,
		Password: password,
		DB:       0, // use default DB
	})

	return &Manager{
		client: client,
		prefix: prefix,
	}
}

func (m *Manager) key(k string) string {
	return m.prefix + k
}

// Client returns the raw redis.Client
func (m *Manager) Client() *redis.Client {
	return m.client
}

// Has checks if a key exists
func (m *Manager) Has(ctx context.Context, key string) (bool, error) {
	n, err := m.client.Exists(ctx, m.key(key)).Result()
	if err != nil {
		return false, err
	}
	return n > 0, nil
}

// Get string payload
func (m *Manager) GetString(ctx context.Context, key string) (string, error) {
	return m.client.Get(ctx, m.key(key)).Result()
}

// Get Unmarshals into generic struct v
func (m *Manager) Get(ctx context.Context, key string, v any) error {
	val, err := m.GetString(ctx, key)
	if err != nil {
		return err
	}
	return json.Unmarshal([]byte(val), v)
}

// Set generic struct v or string
func (m *Manager) Set(ctx context.Context, key string, v any, expiration time.Duration) error {
	var payload interface{}
	if str, ok := v.(string); ok {
		payload = str
	} else {
		b, err := json.Marshal(v)
		if err != nil {
			return err
		}
		payload = b
	}

	return m.client.Set(ctx, m.key(key), payload, expiration).Err()
}

// Delete removes key
func (m *Manager) Delete(ctx context.Context, key string) error {
	return m.client.Del(ctx, m.key(key)).Err()
}

// Lock distributed lock
func (m *Manager) Lock(ctx context.Context, key string, ownerId string, ttl time.Duration) (bool, error) {
	return m.client.SetNX(ctx, m.key("lock:"+key), ownerId, ttl).Result()
}

// Unlock unlocks safely using Lua script
func (m *Manager) Unlock(ctx context.Context, key string, ownerId string) (bool, error) {
	script := `
		if redis.call("get", KEYS[1]) == ARGV[1] then
			return redis.call("del", KEYS[1])
		else
			return 0
		end
	`
	res, err := m.client.Eval(ctx, script, []string{m.key("lock:" + key)}, ownerId).Int()
	if err != nil && err != redis.Nil {
		return false, err
	}
	return res == 1, nil
}

// Health checks redis status
func (m *Manager) Health(ctx context.Context) bool {
	cmd := m.client.Ping(ctx)
	return cmd.Err() == nil
}

// Close gracefully
func (m *Manager) Close() error {
	return m.client.Close()
}
