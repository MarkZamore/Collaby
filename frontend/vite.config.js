import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';

const repoRoot = fileURLToPath(new URL('..', import.meta.url));
const adminRoot = fileURLToPath(new URL('../panels/admin/src', import.meta.url));
const sharedRoot = fileURLToPath(new URL('./src/shared', import.meta.url));
const proxyTarget = process.env.VITE_PROXY_TARGET || 'http://localhost:8080';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@admin': adminRoot,
      '@shared': sharedRoot,
      'react/jsx-runtime': fileURLToPath(new URL('./node_modules/react/jsx-runtime.js', import.meta.url)),
      'react/jsx-dev-runtime': fileURLToPath(new URL('./node_modules/react/jsx-dev-runtime.js', import.meta.url)),
      react: fileURLToPath(new URL('./node_modules/react/index.js', import.meta.url)),
      'react-router-dom': fileURLToPath(new URL('./node_modules/react-router-dom/dist/index.mjs', import.meta.url)),
      'lucide-react': fileURLToPath(new URL('./node_modules/lucide-react/dist/esm/lucide-react.mjs', import.meta.url)),
      '@microsoft/signalr': fileURLToPath(new URL('./node_modules/@microsoft/signalr/dist/browser/signalr.min.js', import.meta.url)),
    },
  },
  server: {
    fs: {
      allow: [repoRoot],
    },
    host: '0.0.0.0',
    port: 3000,
    proxy: {
      '/api/auth': {
        target: proxyTarget,
        changeOrigin: true,
      },
      '/api/users': {
        target: proxyTarget,
        changeOrigin: true,
      },
      '/api/management': {
        target: proxyTarget,
        changeOrigin: true,
        ws: true,
      },
      '/api/messenger': {
        target: proxyTarget,
        changeOrigin: true,
        ws: true,
      },
      '/api/calls': {
        target: proxyTarget,
        changeOrigin: true,
        ws: true,
      },
      '/api/gamification': {
        target: proxyTarget,
        changeOrigin: true,
      },
      '/api/analytics': {
        target: proxyTarget,
        changeOrigin: true,
      },
      '/api/ai': {
        target: proxyTarget,
        changeOrigin: true,
      },
      '/hubs': {
        target: proxyTarget,
        changeOrigin: true,
        ws: true,
      },
    },
  },
  preview: {
    host: '0.0.0.0',
    port: 4173,
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('@microsoft/signalr')) {
            return 'vendor-signalr';
          }

          if (id.includes('chart.js') || id.includes('react-chartjs-2')) {
            return 'vendor-charts';
          }

          if (id.includes('driver.js')) {
            return 'vendor-driver';
          }
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    passWithNoTests: true,
    setupFiles: ['./src/test/setup.js'],
  },
});
