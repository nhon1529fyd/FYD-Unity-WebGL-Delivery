const CACHE_PREFIX = {{{ JSON.stringify(COMPANY_NAME + "-" + PRODUCT_NAME) }}};
const CACHE_VERSION = {{{ JSON.stringify(PRODUCT_VERSION) }}};
const SHELL_CACHE = `${CACHE_PREFIX}-${CACHE_VERSION}-shell`;

// Chỉ cache lớp vỏ nhẹ. Các file Unity nặng (.data/.wasm/.framework.js)
// được quản lý bởi HTTP cache + Unity Data Caching để tránh lưu trùng hai lần.
const SHELL_RESOURCES = [
  "TemplateData/style.css",
  "TemplateData/fyd-template.js",
  "TemplateData/Bg1_1.webp",
  "TemplateData/favicon.ico",
  "manifest.webmanifest"
];

self.addEventListener("install", (event) => {
  self.skipWaiting();
  event.waitUntil((async () => {
    const cache = await caches.open(SHELL_CACHE);
    await Promise.allSettled(
      SHELL_RESOURCES.map((resource) => cache.add(new Request(resource, { cache: "reload" })))
    );
  })());
});

self.addEventListener("activate", (event) => {
  event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(
      keys
        .filter((key) => key.startsWith(CACHE_PREFIX) && key !== SHELL_CACHE)
        .map((key) => caches.delete(key))
    );
    await self.clients.claim();
  })());
});

async function networkFirst(request) {
  const cache = await caches.open(SHELL_CACHE);
  try {
    const response = await fetch(request, { cache: "no-cache" });
    if (response && response.ok) await cache.put(request, response.clone());
    return response;
  } catch (error) {
    return (await cache.match(request)) || (await cache.match("index.html")) || Response.error();
  }
}

async function staleWhileRevalidate(request) {
  const cache = await caches.open(SHELL_CACHE);
  const cached = await cache.match(request);
  const networkPromise = fetch(request)
    .then((response) => {
      if (response && response.ok) cache.put(request, response.clone());
      return response;
    })
    .catch(() => null);

  return cached || (await networkPromise) || Response.error();
}

self.addEventListener("fetch", (event) => {
  if (event.request.method !== "GET") return;
  if (event.request.headers.has("range")) return;

  const url = new URL(event.request.url);
  if (url.origin !== self.location.origin) return;

  if (event.request.mode === "navigate") {
    event.respondWith(networkFirst(event.request));
    return;
  }

  // Không can thiệp vào file Build nặng để tránh trùng cache với Unity Data Caching.
  if (url.pathname.includes("/Build/") || url.pathname.includes("/StreamingAssets/")) {
    return;
  }

  if (
    url.pathname.includes("/TemplateData/") ||
    url.pathname.endsWith("/manifest.webmanifest") ||
    url.pathname.endsWith("/ServiceWorker.js")
  ) {
    event.respondWith(staleWhileRevalidate(event.request));
  }
});
