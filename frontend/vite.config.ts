import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Das gebaute Frontend wird direkt in das wwwroot des Backends geschrieben,
// damit die veröffentlichte EXE es als statische Weboberfläche ausliefern kann.
export default defineConfig({
  plugins: [react()],
  base: './',
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': 'http://127.0.0.1:5187'
    }
  },
  build: {
    outDir: '../backend/wwwroot',
    emptyOutDir: true
  }
})
