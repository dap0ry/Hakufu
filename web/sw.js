// La web ya no es una app instalable: este service worker solo existe para
// borrar la caché y darse de baja en los navegadores que instalaron la
// versión anterior (lector web). No quitar hasta que nadie la tenga.
self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', (event) => {
  event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys.map((k) => caches.delete(k)));
    await self.registration.unregister();
    const clients = await self.clients.matchAll({ type: 'window' });
    clients.forEach((c) => c.navigate(c.url));
  })());
});
