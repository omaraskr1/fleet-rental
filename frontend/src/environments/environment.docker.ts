export const environment = {
  production: true,

  // Relative, not absolute: nginx (see frontend/nginx.conf) reverse-proxies
  // /api to the backend container, so this works whether the stack is
  // reached via localhost or a LAN address — no host baked in at build time.
  apiBaseUrl: '/api',

  // Launching with one customer: auto-selects this company on startup so
  // nobody sees the multi-company picker. Set to '' to bring the picker back
  // once a second company signs up.
  defaultCompanyCode: 'demo-fleet',
};
