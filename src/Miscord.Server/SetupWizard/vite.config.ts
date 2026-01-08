import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  base: '/setup/',
  build: {
    outDir: '../wwwroot/setup',
    emptyOutDir: true,
  },
})
