export const environment = {
  production: true,

  // Relative, not absolute: nginx (see frontend/nginx.conf) reverse-proxies
  // /api to the backend container, so this works whether the stack is
  // reached via localhost or a LAN address — no host baked in at build time.
  apiBaseUrl: '/api',
};
