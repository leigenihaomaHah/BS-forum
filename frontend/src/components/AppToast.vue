<template>
  <div class="toast-container">
    <div
      v-for="t in toast.items"
      :key="t.id"
      class="toast-notice"
      :class="'toast-' + t.type"
    >{{ t.msg }}</div>
  </div>
</template>

<script setup>
import { useToastStore } from '../stores/toast'
const toast = useToastStore()
</script>

<style scoped>
.toast-container {
  position: fixed;
  top: 72px;
  right: 20px;
  z-index: 13000;
  display: flex;
  flex-direction: column;
  gap: 8px;
  pointer-events: none;
  max-width: min(360px, calc(100vw - 32px));
}
.toast-notice {
  pointer-events: auto;
  padding: 10px 14px;
  border-radius: 10px;
  font-size: 13px;
  font-weight: 500;
  box-shadow: 0 8px 24px rgba(15, 23, 42, 0.16);
  animation: toast-in 0.25s ease;
  background: #334155;
  color: #fff;
}
.toast-error { background: #dc2626; color: #fff; }
.toast-success { background: #059669; color: #fff; }
.toast-info { background: #334155; color: #fff; }
@keyframes toast-in {
  from { opacity: 0; transform: translateY(-6px); }
  to { opacity: 1; transform: none; }
}
</style>
