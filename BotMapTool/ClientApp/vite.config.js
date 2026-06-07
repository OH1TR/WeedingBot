import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    // In dev, forward MML proxy and SignalR hub requests to the ASP.NET Core backend.
    proxy: {
      '/mml': 'http://localhost:5292',
      '/bothub': {
        target: 'http://localhost:5292',
        ws: true
      }
    }
  },
  build: {
    // Production build goes straight to the backend's static files folder.
    outDir: '../wwwroot',
    emptyOutDir: true
  }
})
