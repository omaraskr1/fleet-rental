import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.fleetrental.app',
  appName: 'Fleet Rental',

  // Angular 22 emits the browser bundle under dist/<project>/browser.
  webDir: 'dist/frontend/browser',

  plugins: {
    PushNotifications: {
      // Show the OS permission prompt when the app first asks, rather than at
      // launch — users grant far more readily once they've made a booking.
      presentationOptions: ['badge', 'sound', 'alert'],
    },
  },

  server: {
    // Serves the Android WebView from https://localhost so the app runs in a
    // secure context (required by push, geolocation, and crypto APIs). The
    // trade-off is that the request Origin is https://localhost, which the API's
    // CORS allow-list must name explicitly — see Cors:AllowedOrigins.
    androidScheme: 'https',
  },

  ios: {
    // Avoids content sliding under the status bar on notched devices.
    contentInset: 'always',
  },
};

export default config;
