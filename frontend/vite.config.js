import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// 固定 4xxx，避开其它项目常用的 5xxx
export default defineConfig({
  plugins: [vue()],
  server: {
    host: '127.0.0.1',
    port: 4173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:4080',
        changeOrigin: true
      }
    }
  }
})
