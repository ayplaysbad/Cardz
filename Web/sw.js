const CACHE_NAME = 'scribblewars-shell-20260708133414';

function isUnityRuntimeRequest(requestUrl) {
  try {
    const url = new URL(requestUrl);
    return url.origin === self.location.origin
      && (url.pathname.includes('/Build/')
        || url.pathname.includes('/StreamingAssets/')
        || url.pathname.endsWith('.data')
        || url.pathname.endsWith('.wasm')
        || url.pathname.endsWith('.js'));
  } catch (error) {
    return false;
  }
}

self.addEventListener('install', event => {
  event.waitUntil(self.skipWaiting());
});

self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys
      .filter(key => (key.startsWith('cardz-') || key.startsWith('scribblewars-')) && key !== CACHE_NAME)
      .map(key => caches.delete(key)));
    await self.clients.claim();
  })());
});

self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET') {
    return;
  }

  if (isUnityRuntimeRequest(event.request.url)) {
    event.respondWith(fetch(event.request, { cache: 'no-store' }));
    return;
  }

  event.respondWith((async () => {
    const cache = await caches.open(CACHE_NAME);
    try {
      const response = await fetch(event.request, { cache: 'no-store' });
      const contentType = response && response.headers ? (response.headers.get('content-type') || '') : '';
      if (response
        && response.ok
        && event.request.url.startsWith(self.location.origin)
        && !isUnityRuntimeRequest(event.request.url)
        && (contentType.includes('text/html')
          || contentType.includes('text/css')
          || contentType.includes('javascript')
          || contentType.includes('image/')
          || contentType.includes('application/manifest+json')))
      {
        cache.put(event.request, response.clone());
      }
      return response;
    } catch (error) {
      const cached = await cache.match(event.request, { ignoreSearch: true });
      if (cached) {
        return cached;
      }
      throw error;
    }
  })());
});
