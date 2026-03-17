<?php

declare(strict_types=1);

use Zirve\Config\ConfigManager;

test('varsayılan değerler K8s servis DNS adresleri ile döner', function () {
    $config = new ConfigManager();

    expect($config->get('db.host'))->toBe('postgresql.zirve-data.svc.cluster.local');
    expect($config->get('db.port'))->toBe(5432);
    expect($config->get('cache.host'))->toBe('redis-master.zirve-data.svc.cluster.local');
    expect($config->get('cache.port'))->toBe(6379);
    expect($config->get('auth.url'))->toContain('keycloak');
    expect($config->get('environment'))->toBe('local');
});

test('override değerleri varsayılanları ezer', function () {
    $config = new ConfigManager([
        'db.host' => 'custom-pg-host',
        'db.port' => 5433,
    ]);

    expect($config->get('db.host'))->toBe('custom-pg-host');
    expect($config->get('db.port'))->toBe(5433);
});

test('module() prefix ile filtreleme yapar', function () {
    $config = new ConfigManager();
    $dbConfig = $config->module('db');

    expect($dbConfig)->toHaveKey('host');
    expect($dbConfig)->toHaveKey('port');
    expect($dbConfig)->toHaveKey('username');
    expect($dbConfig)->toHaveKey('password');
    expect($dbConfig)->toHaveKey('database');
    expect($dbConfig)->toHaveKey('driver');
    expect($dbConfig)->not->toHaveKey('cache.host');
});

test('module() cache prefix doğru çalışır', function () {
    $config = new ConfigManager();
    $cacheConfig = $config->module('cache');

    expect($cacheConfig)->toHaveKey('host');
    expect($cacheConfig)->toHaveKey('port');
    expect($cacheConfig)->toHaveKey('password');
    expect($cacheConfig)->toHaveKey('prefix');
    expect($cacheConfig['prefix'])->toBe('zirve:');
});

test('serviceUrl() servis URL döndürür', function () {
    $config = new ConfigManager();

    expect($config->serviceUrl('auth'))->toContain('keycloak');
    expect($config->serviceUrl('billing'))->toContain('lago');
    expect($config->serviceUrl('quality'))->toContain('sonarqube');
    expect($config->serviceUrl('deploy'))->toContain('argocd');
});

test('environment() ortam bilgisi döndürür', function () {
    $config = new ConfigManager(['environment' => 'production']);

    expect($config->environment())->toBe('production');
});

test('health() her zaman true döner', function () {
    $config = new ConfigManager();

    expect($config->health())->toBeTrue();
});

test('env override çalışır', function () {
    putenv('PG_HOST=env-pg-host');

    $config = new ConfigManager();

    expect($config->get('db.host'))->toBe('env-pg-host');

    putenv('PG_HOST');  // Cleanup
});

test('bilinmeyen key için default döner', function () {
    $config = new ConfigManager();

    expect($config->get('nonexistent'))->toBeNull();
    expect($config->get('nonexistent', 'fallback'))->toBe('fallback');
});

test('tüm 26 servis için URL veya host tanımlı', function () {
    $config = new ConfigManager();
    $expectedModules = [
        'db', 'cache', 'search', 'storage', 'analytics',
        'queue', 'auth', 'secrets',
        'log', 'error', 'trace', 'metrics',
        'billing', 'crm', 'remote',
        'gateway', 'ingress', 'registry', 'deploy', 'cluster',
        'quality', 'oncall', 'dashboard', 'testing',
    ];

    foreach ($expectedModules as $module) {
        $moduleConfig = $config->module($module);
        $hasEndpoint = isset($moduleConfig['url']) || isset($moduleConfig['host']) || isset($moduleConfig['endpoint']) || isset($moduleConfig['dsn']);
        expect($hasEndpoint)->toBeTrue("Modül '{$module}' için endpoint tanımlı olmalı");
    }
});
