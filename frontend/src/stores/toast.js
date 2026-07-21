import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useToastStore = defineStore('toast', () => {
  const items = ref([])
  let nextId = 0

  function addToast(msg, type = 'info', duration = 2500) {
    const id = ++nextId
    items.value.push({ id, msg, type })
    setTimeout(() => {
      const idx = items.value.findIndex((t) => t.id === id)
      if (idx !== -1) items.value.splice(idx, 1)
    }, duration)
  }

  function success(msg) { addToast(msg, 'success') }
  function error(msg) { addToast(msg, 'error', 4000) }
  function info(msg) { addToast(msg, 'info') }

  return { items, addToast, success, error, info }
})
