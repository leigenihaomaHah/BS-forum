import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '../api/http'

export const useAuthStore = defineStore('auth', () => {
  const token = ref(localStorage.getItem('token') || '')
  const user = ref(JSON.parse(localStorage.getItem('user') || 'null'))
  const signInStatus = ref(null)

  const isLoggedIn = computed(() => !!token.value && !!user.value)

  function persist() {
    if (token.value) localStorage.setItem('token', token.value)
    else localStorage.removeItem('token')
    if (user.value) localStorage.setItem('user', JSON.stringify(user.value))
    else localStorage.removeItem('user')
  }

  async function login(username, password) {
    const { data } = await api.post('/auth/login', { username, password })
    token.value = data.token
    user.value = data.user
    persist()
    return data
  }

  async function register(username, password, nickname, inviteCode, captchaId, captchaCode) {
    const { data } = await api.post('/auth/register', {
      username,
      password,
      nickname,
      inviteCode: inviteCode || null,
      captchaId,
      captchaCode,
    })
    token.value = data.token
    user.value = data.user
    persist()
    return data
  }

  async function fetchMe() {
    if (!token.value) return
    const { data } = await api.get('/me')
    user.value = data
    persist()
  }

  async function signIn() {
    const { data } = await api.post('/me/sign-in')
    user.value = data.user
    signInStatus.value = {
      todaySignedIn: true,
      consecutiveDays: data.consecutiveDays,
      totalDays: data.totalDays,
      milestoneBonus: data.milestoneBonus,
      badge: data.badge,
    }
    persist()
    return data
  }

  async function fetchSignInStatus() {
    if (!token.value) {
      signInStatus.value = null
      return
    }
    try {
      const { data } = await api.get('/me/sign-in-status')
      signInStatus.value = data
    } catch {
      signInStatus.value = null
    }
  }

  function logout() {
    token.value = ''
    user.value = null
    signInStatus.value = null
    persist()
  }

  function applySession(t, u) {
    token.value = t
    user.value = u
    signInStatus.value = null
    persist()
  }

  function setUser(u) {
    user.value = u
    persist()
  }

  return { token, user, signInStatus, isLoggedIn, login, register, fetchMe, signIn, fetchSignInStatus, logout, applySession, setUser }
})
