package zirve

import (
	"context"

	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/analytics"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/auth"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/billing"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/cache"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/cluster"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/config"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/crm"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/dashboard"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/deploy"
	zerror "github.com/zirvebilgisayar/zirve-sdk/go/pkg/error"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/gateway"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/ingress"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/log"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/metrics"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/oncall"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/quality"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/queue"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/registry"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/remote"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/search"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/secrets"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/storage"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/testing"
	"github.com/zirvebilgisayar/zirve-sdk/go/pkg/trace"
	
	zdb "github.com/zirvebilgisayar/zirve-sdk/go/pkg/db"
)

// Client is the main Zirve SDK struct holding all manager instances.
type Client struct {
	Config    *config.Manager
	Db        *zdb.Manager
	Cache     *cache.Manager
	Auth      *auth.Manager
	Secrets   *secrets.Manager
	Queue     *queue.Manager
	Storage   *storage.Manager
	Search    *search.Manager
	Analytics *analytics.Manager

	Log     *log.Manager
	Error   *zerror.Manager
	Trace   *trace.Manager // note trace struct handles OTEL
	Metrics *metrics.Manager

	Billing *billing.Manager
	Crm     *crm.Manager
	Remote  *remote.Manager

	Gateway   *gateway.Manager
	Ingress   *ingress.Manager
	Registry  *registry.Manager
	Deploy    *deploy.Manager
	Cluster   *cluster.Manager
	Quality   *quality.Manager
	OnCall    *oncall.Manager
	Dashboard *dashboard.Manager
	Testing   *testing.Manager
}

// NewClient initializes the Zirve SDK and all integrated services.
func NewClient() (*Client, error) {
	cfg := config.NewManager()

	dbManager, err := zdb.NewManager(cfg)
	if err != nil {
		return nil, err
	}

	storageManager, err := storage.NewManager(cfg)
	if err != nil {
		dbManager.Close()
		return nil, err
	}

	return &Client{
		Config:    cfg,
		Db:        dbManager,
		Cache:     cache.NewManager(cfg),
		Auth:      auth.NewManager(cfg),
		Secrets:   secrets.NewManager(cfg),
		Queue:     queue.NewManager(cfg),
		Storage:   storageManager,
		Search:    search.NewManager(cfg),
		Analytics: analytics.NewManager(cfg),
		
		Log:       log.NewManager(cfg),
		Error:     zerror.NewManager(cfg),
		Trace:     trace.NewManager(cfg),
		Metrics:   metrics.NewManager(cfg),
		
		Billing:   billing.NewManager(cfg),
		Crm:       crm.NewManager(cfg),
		Remote:    remote.NewManager(cfg),
		
		Gateway:   gateway.NewManager(cfg),
		Ingress:   ingress.NewManager(cfg),
		Registry:  registry.NewManager(cfg),
		Deploy:    deploy.NewManager(cfg),
		Cluster:   cluster.NewManager(cfg),
		Quality:   quality.NewManager(cfg),
		OnCall:    oncall.NewManager(cfg),
		Dashboard: dashboard.NewManager(cfg),
		Testing:   testing.NewManager(cfg),
	}, nil
}

// Health checks the readiness of all connected infrastructure pieces.
func (c *Client) Health(ctx context.Context) map[string]string {
	status := make(map[string]string)

	check := func(name string, healthy bool) {
		if healthy {
			status[name] = "healthy"
		} else {
			status[name] = "unhealthy"
		}
	}

	check("db", c.Db.Health())
	check("cache", c.Cache.Health(ctx))
	check("auth", c.Auth.Health())
	check("secrets", c.Secrets.Health())
	check("queue", c.Queue.Health())
	check("storage", c.Storage.Health(ctx))
	check("search", c.Search.Health())
	check("analytics", c.Analytics.Health())
	
	check("log", c.Log.Health())
	check("error", c.Error.Health())
	check("trace", c.Trace.Health())
	check("metrics", c.Metrics.Health())
	
	check("billing", c.Billing.Health())
	check("crm", c.Crm.Health())
	check("remote", c.Remote.Health())
	
	check("gateway", c.Gateway.Health())
	check("ingress", c.Ingress.Health())
	check("registry", c.Registry.Health())
	check("deploy", c.Deploy.Health())
	check("cluster", c.Cluster.Health())
	check("quality", c.Quality.Health())
	check("oncall", c.OnCall.Health())
	check("dashboard", c.Dashboard.Health())
	check("testing", c.Testing.Health())

	return status
}

// Close gracefully shuts down internal connection pools, buffers, etc.
func (c *Client) Close() {
	c.Db.Close()
	c.Cache.Close()
	c.Log.Close()
}
