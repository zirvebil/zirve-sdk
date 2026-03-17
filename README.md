# Zirve SDK

Zirve Bilgisayar altyapı servisleri için çoklu dil SDK monoreposu.

## Kurulum (PHP)

```bash
composer require zirvebil/zirve-sdk
```

## Hızlı Başlangıç

```php
use Zirve\Zirve;

$z = Zirve::init();

// Veritabanı
$users = $z->db->query('SELECT * FROM users WHERE active = ?', [true]);

// Cache
$data = $z->cache->remember('key', 300, fn() => expensiveQuery());

// Sağlık kontrolü (tüm 26 servis)
$health = $z->health();
```

## Modüller

| Tier | Modüller |
|---|---|
| Veri | Db, Cache, Search, Storage, Analytics |
| Güvenlik | Queue, Auth, Secrets |
| Observability | Log, Error, Trace, Metrics |
| İş Servisleri | Billing, Crm, Remote |
| Platform | Gateway, Ingress, Registry, Deploy, Cluster, Quality, OnCall, Dashboard, Testing, Config |

## Diller

- **PHP** (aktif) — `php/`
- TypeScript (planlı) — `typescript/`
- Go (planlı) — `go/`
- .NET (planlı) — `dotnet/`

## Geliştirme

```bash
cd php
composer install
composer test
```

## Lisans

MIT
