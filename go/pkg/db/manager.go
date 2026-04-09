package db

import (
	"context"
	"fmt"
	"strings"

	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
)

type Manager struct {
	pool *pgxpool.Pool
}

func NewManager(cfg *config.Manager) (*Manager, error) {
	mod := cfg.Module("db")
	host := mod["host"]
	port := mod["port"]
	dbname := mod["dbname"]
	user := mod["user"]
	password := mod["password"]

	if host == "" {
		host = "localhost"
	}
	if port == "" {
		port = "5432"
	}
	if dbname == "" {
		dbname = "zirve"
	}
	if user == "" {
		user = "postgres"
	}

	// build connection string
	connStr := fmt.Sprintf("postgres://%s:%s@%s:%s/%s?pool_max_conns=100", 
		user, password, host, port, dbname)

	pool, err := pgxpool.New(context.Background(), connStr)
	if err != nil {
		return nil, err
	}

	return &Manager{pool: pool}, nil
}

// Pool returns the underlying pgxpool
func (m *Manager) Pool() *pgxpool.Pool {
	return m.pool
}

// Exec runs a simple non-query command
func (m *Manager) Exec(ctx context.Context, sql string, args ...any) error {
	_, err := m.pool.Exec(ctx, sql, args...)
	return err
}

// QueryRow wrapper
func (m *Manager) QueryRow(ctx context.Context, sql string, args ...any) pgx.Row {
	return m.pool.QueryRow(ctx, sql, args...)
}

// Transaction executes inside a tx with commit/rollback
func (m *Manager) Transaction(ctx context.Context, fn func(pgx.Tx) error) error {
	tx, err := m.pool.Begin(ctx)
	if err != nil {
		return err
	}
	defer tx.Rollback(ctx)

	if err := fn(tx); err != nil {
		return err
	}
	return tx.Commit(ctx)
}

// SetTenant sets schema search path dynamically safely
func (m *Manager) SetTenant(ctx context.Context, tx pgx.Tx, tenantId string) error {
	// Sanitize tenantId manually to avoid injection
	safe := strings.ReplaceAll(tenantId, "'", "")
	safe = strings.ReplaceAll(safe, "\"", "")
	safe = strings.ReplaceAll(safe, ";", "")
	
	_, err := tx.Exec(ctx, fmt.Sprintf(`SET search_path TO tenant_%s, public`, safe))
	return err
}

// Health checks DB connectivity
func (m *Manager) Health() bool {
	return m.pool.Ping(context.Background()) == nil
}

// Close closes the connection pool
func (m *Manager) Close() {
	if m.pool != nil {
		m.pool.Close()
	}
}
