import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

export const useAuthModalStore = defineStore('authModal', () => {
  const mode = ref(null) // 'login' | 'register' | null
  const inviteCode = ref('')

  function openLogin() {
    mode.value = 'login'
  }

  function openRegister(invite = '') {
    inviteCode.value = (invite || '').toString()
    mode.value = 'register'
  }

  function close() {
    mode.value = null
  }

  function switchToLogin() {
    mode.value = 'login'
  }

  function switchToRegister(invite = '') {
    if (invite) inviteCode.value = invite.toString()
    mode.value = 'register'
  }

  if (typeof window !== 'undefined') {
    watch(mode, (v, _, onCleanup) => {
      if (!v) return
      const onKey = (e) => {
        if (e.key === 'Escape') close()
      }
      window.addEventListener('keydown', onKey)
      onCleanup(() => window.removeEventListener('keydown', onKey))
    })
  }

  return { mode, inviteCode, openLogin, openRegister, close, switchToLogin, switchToRegister }
})
