import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' }
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

api.interceptors.response.use(
  (res) => res,
  (err) => {
    const data = err.response?.data
    const status = err.response?.status
    let message =
      (typeof data === 'string' && data) ||
      data?.message ||
      data?.detail ||
      data?.title ||
      (data?.errors && Object.values(data.errors).flat().join('; ')) ||
      err.message ||
      '请求失败'
    // IIS 405 HTML page — don't dump raw HTML into alert()
    if (typeof message === 'string' && (message.includes('<!DOCTYPE') || message.includes('<html'))) {
      if (status === 405) message = '请求方法不被允许(405)。多为 IIS WebDAV 拦截了 PUT/DELETE，请更新站点 web.config 并回收应用程序池。'
      else message = `服务器错误(${status || '?'})`
    }
    return Promise.reject(new Error(message))
  }
)

export default api
