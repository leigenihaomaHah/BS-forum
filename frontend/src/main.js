import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { useAuthStore } from './stores/auth'
import api from './api/http'
import { setLevels } from './config/levels.js'
import 'bootstrap/dist/css/bootstrap.min.css'
import 'bootstrap-icons/font/bootstrap-icons.css'
import './assets/forum.css'
import './assets/admin.css'

const app = createApp(App)
const pinia = createPinia()
app.use(pinia)
app.use(router)

async function bootstrap() {
  try {
    const { data } = await api.get('/levels')
    setLevels(data)
  } catch {
    // keep FALLBACK_LEVELS
  }

  const auth = useAuthStore(pinia)
  if (auth.token) {
    await auth.fetchMe().catch(() => auth.logout())
  }

  app.mount('#app')
}

bootstrap()
