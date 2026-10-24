import type { CapacitorConfig } from '@capacitor/cli';

// Android & iOS apps package the built React app (dist/).
// Set VITE_API_URL in .env.production to the public API address before building for mobile,
// e.g. VITE_API_URL=https://meal.yourcompany.com
const config: CapacitorConfig = {
  appId: 'com.company.mealmanagement',
  appName: 'Meal Management',
  webDir: 'dist',
  backgroundColor: '#faf7ee',
  android: { allowMixedContent: false },
  ios: { contentInset: 'automatic' },
};

export default config;
