/* Manifest version: q0vygaOG */
// Service Worker - version published optimized
const CACHE_VERSION = 'v1.0.4-scope-assets';
const CACHE_NAME = `crm-pwa-cache-${CACHE_VERSION}`;
const OFFLINE_URL = 'offline.html';
const INDEX_URL = new URL('index.html', self.registration.scope).href;
const SCOPE_PATH = new URL(self.registration.scope).pathname.replace(/^\/|\/$/g, '');

// This file is injected by Blazor in production with the full framework asset list.
try {
    importScripts(new URL('service-worker-assets.js', self.registration.scope).href);
} catch (error) {
    console.warn('[Service Worker] Could not load service-worker-assets.js. Offline precache will be skipped.', error);
}

const getAssetsToCache = () => {
    const assets = self.assetsManifest?.assets;
    if (!Array.isArray(assets)) {
        console.warn('[Service Worker] Assets manifest is not available. Skipping precache.');
        return [INDEX_URL];
    }

    return [
        INDEX_URL,
        ...assets
        .map(asset => asset.url)
        .filter(url => Boolean(url))
        .map(url => {
            const normalizedUrl = SCOPE_PATH && url.startsWith(`${SCOPE_PATH}/`)
                ? url.substring(SCOPE_PATH.length + 1)
                : url;

            return new URL(normalizedUrl, self.registration.scope).href;
        })
    ];
};

const getAppShellResponse = async () => {
    const cachedIndex = await caches.match(INDEX_URL);
    if (cachedIndex) {
        return cachedIndex;
    }

    try {
        const networkIndex = await fetch(INDEX_URL, { cache: 'no-store' });
        if (networkIndex && networkIndex.ok) {
            const responseClone = networkIndex.clone();
            const cache = await caches.open(CACHE_NAME);
            await cache.put(INDEX_URL, responseClone);
            return networkIndex;
        }
    } catch (error) {
        console.warn('[Service Worker] Could not fetch app shell.', error);
    }

    return new Response(
        '<!doctype html><html lang="es"><head><meta charset="utf-8"><title>CRM PWA</title></head><body>La aplicacion no esta disponible sin conexion. Vuelve a intentarlo.</body></html>',
        { headers: { 'Content-Type': 'text/html; charset=utf-8' } }
    );
};

self.addEventListener('install', event => {
    console.log('[Service Worker] Installing published version...');
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => {
                const assetsToCache = [...new Set(getAssetsToCache())];
                console.log('[Service Worker] Precaching application assets');
                return Promise.allSettled(assetsToCache.map(url => cache.add(url)))
                    .then(results => {
                        const failedAssets = results.filter(result => result.status === 'rejected').length;
                        if (failedAssets > 0) {
                            console.warn(`[Service Worker] ${failedAssets} assets could not be precached.`);
                        }
                    });
            })
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', event => {
    console.log('[Service Worker] Activating published version...');
    event.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames
                    .filter(name => name.startsWith('crm-pwa-cache-') && name !== CACHE_NAME)
                    .map(name => caches.delete(name))
            );
        }).then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') {
        return;
    }

    if (event.request.url.includes('/api/')) {
        return;
    }

    if (event.request.mode === 'navigate') {
        event.respondWith(
            fetch(event.request)
                .then(response => {
                    const responseUrl = new URL(response.url);
                    const scopeUrl = new URL(self.registration.scope);
                    const isInsideScope = responseUrl.href.startsWith(scopeUrl.href);

                    return response && response.ok && isInsideScope
                        ? response
                        : getAppShellResponse();
                })
                .catch(() => getAppShellResponse())
        );
        return;
    }

    event.respondWith(
        caches.match(event.request)
            .then(cachedResponse => {
                if (cachedResponse) {
                    return cachedResponse;
                }

                return fetch(event.request)
                    .then(response => {
                        if (response && response.status === 200) {
                            const responseClone = response.clone();
                            caches.open(CACHE_NAME).then(cache => {
                                cache.put(event.request, responseClone);
                            });
                        }
                        return response;
                    })
                    .catch(() => {
                        return caches.match(event.request)
                            .then(fallbackResponse => fallbackResponse || Response.error());
                    });
            })
    );
});

// Receive WebPush messages from the .NET backend.
self.addEventListener('push', function (event) {
    if (!event.data) return;

    const data = event.data.json();
    const title = data.Title || data.title || 'Aviso de CRM';
    const body = data.Message || data.message || data.body || 'Tienes un trámite pendiente.';
    const url = data.Url || data.url || '/';

    const options = {
        body,
        icon: new URL('icon-192.png', self.registration.scope).href,
        badge: new URL('icon-192.png', self.registration.scope).href,
        vibrate: [100, 50, 100],
        data: {
            url
        }
    };

    event.waitUntil(
        self.registration.showNotification(title, options)
    );
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();

    const urlToOpen = new URL(event.notification.data?.url || '.', self.registration.scope).href;

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (windowClients) {
            for (let i = 0; i < windowClients.length; i++) {
                const client = windowClients[i];
                if (client.url === urlToOpen && 'focus' in client) {
                    return client.focus();
                }
            }

            if (clients.openWindow) {
                return clients.openWindow(urlToOpen);
            }
        })
    );
});
