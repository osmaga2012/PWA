// Variable global para capturar el evento de instalación de la PWA
let deferredPrompt;

window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferredPrompt = e;
});

window.addEventListener('appinstalled', () => {
    deferredPrompt = null;
});

// --- GESTOR PWA ---
window.pwaManager = {
    isInstalled: () => {
        return window.matchMedia('(display-mode: standalone)').matches ||
            window.navigator.standalone === true;
    },

    canInstall: () => {
        return deferredPrompt !== undefined;
    },

    triggerInstall: async () => {
        if (deferredPrompt) {
            deferredPrompt.prompt();
            const { outcome } = await deferredPrompt.userChoice;
            deferredPrompt = null;
            return outcome;
        }
        return 'unavailable';
    }
};

// --- GESTOR NOTIFICACIONES PUSH (ORGANIZADO PARA BLAZOR) ---
window.notificationManager = {
    // Configuración de URLs del API.
    // Se prioriza una URL explícita si la inyectamos desde el host.
    baseUrl: window.__crmApiBaseUrl || null,
    baseUrlDev: "https://localhost:7254",
    baseUrlProd: "https://crm-api-myhv.onrender.com",
    lastError: null,

    setLastError: function (message, detail) {
        this.lastError = detail ? `${message}: ${detail}` : message;
        console.error('[Push]', this.lastError);
    },

    clearLastError: function () {
        this.lastError = null;
    },

    getLastError: function () {
        return this.lastError;
    },

    getApiBaseUrl: function () {
        if (this.baseUrl) {
            return this.baseUrl;
        }

        const isLocal =
            window.location.hostname === "localhost" ||
            window.location.hostname === "127.0.0.1";
        const isPrivateNetwork =
            /^10\./.test(window.location.hostname) ||
            /^192\.168\./.test(window.location.hostname) ||
            /^172\.(1[6-9]|2\d|3[01])\./.test(window.location.hostname);

        if (isLocal) {
            return this.baseUrlDev;
        }

        if (isPrivateNetwork) {
            const apiPort = window.location.protocol === "https:" ? "7254" : "5245";
            return `${window.location.protocol}//${window.location.hostname}:${apiPort}`;
        }

        return this.baseUrlProd;
    },

    buildApiUrl: function (path) {
        const baseUrl = this.getApiBaseUrl().replace(/\/$/, "");
        const normalizedPath = path.startsWith("/") ? path : `/${path}`;
        return `${baseUrl}${normalizedPath}`;
    },

    getStatus: () => {
        if (!('Notification' in window)) return 'unsupported';
        return Notification.permission;
    },

    requestPermission: async () => {
        if (!('Notification' in window)) return 'unsupported';
        return await Notification.requestPermission();
    },

    isPushActive: async () => {
        try {
            if (!('serviceWorker' in navigator) || !('PushManager' in window)) return false;

            const registration = await navigator.serviceWorker.ready;
            const subscription = await registration.pushManager.getSubscription();
            return subscription !== null;
        } catch (e) {
            console.error('Error al verificar suscripción activa:', e);
            return false;
        }
    },

    waitForServiceWorkerReady: async function () {
        return await Promise.race([
            navigator.serviceWorker.ready,
            new Promise((_, reject) =>
                setTimeout(
                    () => reject(new Error('El service worker no está listo. Revisa que la PWA esté publicada bajo la ruta correcta.')),
                    10000))
        ]);
    },

    // Reemplazado por el flujo real conectado a tus endpoints /api/push/...
    subscribePush: async function () {
        this.clearLastError();
        try {
            if (!('Notification' in window)) {
                this.setLastError('Este navegador no soporta notificaciones.');
                return false;
            }

            if (!('serviceWorker' in navigator)) {
                this.setLastError('Este navegador no soporta service workers.');
                return false;
            }

            if (!('PushManager' in window)) {
                this.setLastError('Este navegador no soporta PushManager.');
                return false;
            }

            const registration = await this.waitForServiceWorkerReady();

            const endpoint = this.buildApiUrl("/api/push/public-key");
            console.log("Solicitando clave pública a:", endpoint);

            // 1. Obtener la clave pública desde el grupo /api/push mapeado en C#
            const responseVapid = await fetch(endpoint);
            if (!responseVapid.ok) {
                const errorBody = await responseVapid.text().catch(() => '');
                this.setLastError(`No se pudo obtener la clave VAPID de la API (${responseVapid.status})`, errorBody || responseVapid.statusText);
                return false;
            }

            const data = await responseVapid.json();
            const vapidPublicKeyFromApi = data.publicKey;
            if (!vapidPublicKeyFromApi) {
                this.setLastError('La API no devolvió una clave pública VAPID válida.');
                return false;
            }

            // 2. Suscribir al PushManager del navegador
            const opciones = {
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(vapidPublicKeyFromApi)
            };

            const suscripcionExistente = await registration.pushManager.getSubscription();
            const suscripcion = suscripcionExistente ?? await registration.pushManager.subscribe(opciones);
            console.log('Suscripción nativa obtenida.');

            // 3. Adaptar la estructura al 'PushSubscriptionDto' de tu API (Aplanado)
            const subJson = suscripcion.toJSON();
            if (!subJson.endpoint || !subJson.keys?.p256dh || !subJson.keys?.auth) {
                this.setLastError('La suscripción generada por el navegador está incompleta.');
                return false;
            }

            const pushSubscriptionDto = {
                endpoint: subJson.endpoint,
                p256dh: subJson.keys.p256dh,
                auth: subJson.keys.auth
            };

            // 4. Enviar al endpoint seguro del grupo /api/push
            const token = localStorage.getItem('access_token'); // Clave JWT de tu AuthService
            if (!token) {
                this.setLastError('No hay token de sesión para registrar la suscripción push.');
                return false;
            }

            const responseSave = await fetch(this.buildApiUrl("/api/push/save-subscription"), {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify(pushSubscriptionDto)
            });

            if (responseSave.ok) {
                console.log('Suscripción registrada con éxito en el Minimal API.');
                return true;
            } else {
                const errorBody = await responseSave.text().catch(() => '');
                this.setLastError(`Error al guardar la suscripción en el servidor (${responseSave.status})`, errorBody || responseSave.statusText);
                return false;
            }

        } catch (error) {
            this.setLastError('Error en el flujo de suscripción WebPush', error?.message || error);
            return false;
        }
    },

    resetPush: async () => {
        try {
            const registration = await navigator.serviceWorker.ready;
            const subscription = await registration.pushManager.getSubscription();
            if (subscription) {
                const exito = await subscription.unsubscribe();
                console.log('Suscripción eliminada del navegador con éxito.');
                return exito; // Devuelve true si se desuscribió correctamente
            }
            return true;
        } catch (e) {
            console.error('Error al desuscribir:', e);
            return false;
        }
    }
};

// --- FUNCIÓN AUXILIAR ENCODING VAPID ---
function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/\-/g, '+').replace(/_/g, '/');
    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);
    for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}
