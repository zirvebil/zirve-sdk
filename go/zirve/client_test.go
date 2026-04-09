package zirve_test

import (
	"os"
	"testing"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
	// "github.com/zirvebilgisayar/zirve-sdk/go/zirve" - typically we'd test the client but that requires postgres ping locally on init
)

func TestConfigManagerDefaults(t *testing.T) {
	cfg := config.NewManager()
	
	host := cfg.Get("db.host", "")
	if host != "postgresql.zirve-infra.svc.cluster.local" {
		t.Errorf("Expected default db.host, got %s", host)
	}
}

func TestConfigManagerEnvOverrides(t *testing.T) {
	os.Setenv("PG_DBNAME", "test_override_db")
	defer os.Unsetenv("PG_DBNAME")

	cfg := config.NewManager()
	dbName := cfg.Get("db.dbname", "")
	if dbName != "test_override_db" {
		t.Errorf("Expected test_override_db from ENV, got %s", dbName)
	}
}

// Ensure module mapping returns subsets properly
func TestConfigManagerModule(t *testing.T) {
	os.Setenv("REDIS_PORT", "9999")
	defer os.Unsetenv("REDIS_PORT")

	cfg := config.NewManager()
	cacheMod := cfg.Module("cache")

	if cacheMod["host"] != "redis-master.zirve-infra.svc.cluster.local" {
		t.Errorf("Unexpected cache host: %s", cacheMod["host"])
	}

	if cacheMod["port"] != "9999" {
		t.Errorf("Expected 9999 from ENV override, got %s", cacheMod["port"])
	}
}
