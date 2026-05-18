/**
 * My Meal Planner — Service Worker
 * Caches static assets + saved recipes for offline access.
 * Strategy: Cache-first for assets, Network-first for pages.
 */

const CACHE_NAME    = 'mmp-v1';
const OFFLINE_PAGE  = '/offline.html';

const STATIC_ASSETS = [
    '/',
    '/css/site.css',
    '/js/site.js',
    '/manifest.json',
    OFFLINE_PAGE,
    '/images/icon.svg',
];

// ── Install: cache static assets ─────────────────────────────
self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(STATIC_ASSETS))
            .then(() => self.skipWaiting())
    );
});

// ── Activate: clean up old caches ────────────────────────────
self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(
                keys
                    .filter(k => k !== CACHE_NAME)
                    .map(k => caches.delete(k))
            )
        ).then(() => self.clients.claim())
    );
});

// ── Fetch: serve from cache or network ───────────────────────
self.addEventListener('fetch', event => {
    const { request } = event;
    const url = new URL(request.url);

    // Skip non-GET and non-same-origin requests
    if (request.method !== 'GET' || url.origin !== location.origin) return;

    // Skip API, SignalR, and auth routes
    if (['/api/', '/hubs/', '/Account/Login', '/Account/Logout', '/admin/'].some(p => url.pathname.startsWith(p))) return;

    // Static assets — cache first
    if (url.pathname.match(/\.(css|js|png|jpg|jpeg|webp|svg|ico|woff2?)$/)) {
        event.respondWith(cacheFirst(request));
        return;
    }

    // Recipe detail pages — save for offline
    if (url.pathname.startsWith('/Recipe/Details/')) {
        event.respondWith(networkFirstWithCache(request));
        return;
    }

    // Everything else — network first, offline fallback
    event.respondWith(networkWithOfflineFallback(request));
});

async function cacheFirst(request) {
    const cached = await caches.match(request);
    if (cached) return cached;
    try {
        const response = await fetch(request);
        if (response.ok) {
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, response.clone());
        }
        return response;
    } catch {
        return new Response('', { status: 503 });
    }
}

async function networkFirstWithCache(request) {
    try {
        const response = await fetch(request);
        if (response.ok) {
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, response.clone());
        }
        return response;
    } catch {
        const cached = await caches.match(request);
        return cached ?? await caches.match(OFFLINE_PAGE);
    }
}

async function networkWithOfflineFallback(request) {
    try {
        return await fetch(request);
    } catch {
        const cached = await caches.match(request);
        return cached ?? await caches.match(OFFLINE_PAGE);
    }
}

// ── Push Notifications ────────────────────────────────────────
self.addEventListener('push', event => {
    if (!event.data) return;
    const data = event.data.json();
    event.waitUntil(
        self.registration.showNotification(data.title ?? 'My Meal Planner 🍽️', {
            body:    data.body    ?? 'New content is waiting for you!',
            icon:    '/images/icon.svg',
            badge:   '/images/icon.svg',
            data:    { url: data.actionUrl ?? '/' },
            vibrate: [100, 50, 100],
            actions: data.actionUrl ? [{ action: 'open', title: 'View' }] : []
        })
    );
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const url = event.notification.data?.url ?? '/';
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(clientList => {
            for (const client of clientList) {
                if (client.url === url && 'focus' in client) return client.focus();
            }
            if (clients.openWindow) return clients.openWindow(url);
        })
    );
});
