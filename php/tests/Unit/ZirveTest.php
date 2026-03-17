<?php

declare(strict_types=1);

use Zirve\Zirve;

beforeEach(function () {
    Zirve::reset();
});

test('Zirve::init() singleton döndürür', function () {
    $z1 = Zirve::init();
    $z2 = Zirve::init();

    expect($z1)->toBe($z2);
});

test('Zirve::reset() singleton sıfırlar', function () {
    $z1 = Zirve::init();
    Zirve::reset();
    $z2 = Zirve::init();

    expect($z1)->not->toBe($z2);
});

test('config modülü erişilebilir', function () {
    $z = Zirve::init();

    expect($z->config)->toBeInstanceOf(\Zirve\Config\ConfigManager::class);
});

test('db modülü lazy-load edilir', function () {
    $z = Zirve::init();

    expect($z->db)->toBeInstanceOf(\Zirve\Db\DbManager::class);
});

test('cache modülü lazy-load edilir', function () {
    $z = Zirve::init();

    expect($z->cache)->toBeInstanceOf(\Zirve\Cache\CacheManager::class);
});

test('bilinmeyen modül exception fırlatır', function () {
    $z = Zirve::init();

    expect(fn() => $z->nonexistent)->toThrow(\InvalidArgumentException::class);
});

test('health() tüm modüller için sonuç döndürür', function () {
    $z = Zirve::init();
    $health = $z->health();

    expect($health)->toBeArray();
    expect($health)->toHaveKey('config');
    expect($health['config']['status'])->toBe('healthy');
});

test('init() override config ile çalışır', function () {
    $z = Zirve::init(['db.host' => 'custom-host']);

    expect($z->config->get('db.host'))->toBe('custom-host');
});
