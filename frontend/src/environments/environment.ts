export const environment = {
  production: false,

  // The dev API. On a physical Android device this must be the machine's LAN
  // address (e.g. http://192.168.1.20:5180/api) — localhost there resolves to
  // the phone itself, not to your development machine.
  apiBaseUrl: 'http://localhost:5180/api',

  // Launching with one customer: auto-selects this company on startup so
  // nobody sees the multi-company picker. Set to '' to bring the picker back
  // once a second company signs up.
  defaultCompanyCode: 'demo-fleet',
};
