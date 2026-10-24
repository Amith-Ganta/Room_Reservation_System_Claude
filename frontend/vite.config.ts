import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// In development the browser talks to Vite only; /api is forwarded to the ASP.NET Core API.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
})
