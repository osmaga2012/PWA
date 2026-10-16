// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });


self.addEventListener('push', event => {
    const payload = event.data ? event.data.json() : {};
    const title = payload.Title || payload.title || 'Aviso de CRM';
    const message = payload.Message || payload.message || payload.body || 'Tienes un trámite pendiente.';
    const url = payload.Url || payload.url || '/';

    event.waitUntil(
        self.registration.showNotification(title, {
            body: message,
            icon: new URL('icon-192.png', self.registration.scope).href,
            badge: new URL('icon-192.png', self.registration.scope).href,
            vibrate: [100, 50, 100],
            data: { url }
        })
    );
});

// // 1. Escuchar cuando el servidor WebPush de .NET manda un mensaje
// self.addEventListener('push2', function (event) {
//     console.log('[Service Worker Dev] ¡Se ha detectado un evento Push entrante!');

//     if (!event.data) {
//         console.warn('[Service Worker Dev] El evento Push no contiene datos (payload vacío).');
//         return;
//     }

//     try {
//         // Almacenamos primero el texto plano para ver qué viene de C# si el JSON falla
//         const rawText = event.data.text();
//         console.log('[Service Worker Dev] Texto bruto recibido del servidor:', rawText);

//         // Intentamos parsear manualmente
//         const data = JSON.parse(rawText);
//         console.log('[Service Worker Dev] JSON parseado correctamente:', data);

//         // Mapeo flexible: soporta tanto "Title" como "title"
//         const title = data.Title || data.title || 'Aviso de MarAvisa';

//         // Mapeo flexible para el cuerpo: soporta Message, message, Body o body
//         const message = data.Message || data.message || data.Body || data.body || 'Tienes un trámite pendiente.';

//         const redirectionUrl = data.Url || data.url || '/';

//         const options = {
//             body: message,
//             icon: '/icons/icon-192x192.png',
//             badge: '/icons/badge-72x72.png',
//             vibrate: [100, 50, 100],
//             data: {
//                 url: redirectionUrl
//             }
//         };

//         event.waitUntil(
//             self.registration.showNotification(title, options)
//                 .then(() => console.log('[Service Worker Dev] showNotification ejecutado con éxito.'))
//                 .catch(err => console.error('[Service Worker Dev] Error en el showNotification nativo del navegador:', err))
//         );

//     } catch (error) {
//         // Si el backend envió texto plano o un JSON malformado, caemos aquí en vez de romper el flujo
//         console.error('[Service Worker Dev] Error crítico al procesar el JSON del Push:', error);

//         // Salvavidas: mostramos el mensaje como texto plano para no perder la alerta
//         const emergenciaText = event.data.text();
//         const options = {
//             body: emergenciaText,
//             icon: '/icons/icon-192x192.png',
//             badge: '/icons/badge-72x72.png',
//             data: { url: '/' }
//         };

//         event.waitUntil(
//             self.registration.showNotification('Aviso de MarAvisa (Texto Plano)', options)
//         );
//     }
// });

// 2. Escuchar cuando el usuario hace CLICK en la notificación
self.addEventListener('notificationclick', function (event) {
    event.notification.close();

    const urlAAbriir = new URL(event.notification.data?.url || '.', self.registration.scope).href;

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (windowClients) {
            for (let i = 0; i < windowClients.length; i++) {
                let client = windowClients[i];
                if (client.url === urlAAbriir && 'focus' in client) {
                    return client.focus();
                }
            }
            if (clients.openWindow) {
                return clients.openWindow(urlAAbriir);
            }
        })
    );
});
