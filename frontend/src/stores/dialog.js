import { defineStore } from 'pinia'
import { ref } from 'vue'

/**
 * 统一替代原生 alert / confirm / prompt。
 * 用法：
 *   await dialog.alert('已完成')
 *   if (!(await dialog.confirm('确定删除？', { danger: true }))) return
 *   const v = await dialog.prompt('请填写原因')
 *   if (v === null) return
 */
export const useDialogStore = defineStore('dialog', () => {
  const open = ref(false)
  const mode = ref('alert') // alert | confirm | prompt
  const title = ref('')
  const message = ref('')
  const confirmText = ref('确定')
  const cancelText = ref('取消')
  const danger = ref(false)
  const inputValue = ref('')
  const placeholder = ref('')
  const inputMultiline = ref(false)

  let resolver = null

  function reset() {
    open.value = false
    resolver = null
  }

  function resolve(value) {
    const r = resolver
    reset()
    if (r) r(value)
  }

  function show(next) {
    if (resolver) resolve(mode.value === 'confirm' ? false : null)
    mode.value = next.mode
    title.value = next.title || ''
    message.value = next.message || ''
    confirmText.value = next.confirmText || (next.mode === 'alert' ? '知道了' : '确定')
    cancelText.value = next.cancelText || '取消'
    danger.value = !!next.danger
    inputValue.value = next.defaultValue ?? ''
    placeholder.value = next.placeholder || ''
    inputMultiline.value = !!next.multiline
    open.value = true
    return new Promise((r) => { resolver = r })
  }

  function alert(message, opts = {}) {
    return show({
      mode: 'alert',
      message,
      title: opts.title || '提示',
      confirmText: opts.confirmText || '知道了',
    })
  }

  function confirm(message, opts = {}) {
    return show({
      mode: 'confirm',
      message,
      title: opts.title || '请确认',
      confirmText: opts.confirmText || '确定',
      cancelText: opts.cancelText || '取消',
      danger: opts.danger,
    })
  }

  function prompt(message, opts = {}) {
    const defaultValue = typeof opts === 'string' ? opts : (opts.defaultValue ?? '')
    const rest = typeof opts === 'string' ? {} : opts
    return show({
      mode: 'prompt',
      message,
      title: rest.title || '请输入',
      confirmText: rest.confirmText || '确定',
      cancelText: rest.cancelText || '取消',
      defaultValue,
      placeholder: rest.placeholder || '',
      multiline: rest.multiline,
      danger: rest.danger,
    })
  }

  function onConfirm() {
    if (mode.value === 'confirm') resolve(true)
    else if (mode.value === 'prompt') resolve(inputValue.value)
    else resolve(true)
  }

  function onCancel() {
    if (mode.value === 'confirm') resolve(false)
    else if (mode.value === 'prompt') resolve(null)
    else resolve(true)
  }

  return {
    open, mode, title, message, confirmText, cancelText, danger,
    inputValue, placeholder, inputMultiline,
    alert, confirm, prompt, onConfirm, onCancel,
  }
})
