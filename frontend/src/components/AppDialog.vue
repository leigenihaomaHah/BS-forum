<template>
  <Teleport to="body">
    <Transition name="dlg-fade">
      <div
        v-if="dialog.open"
        class="dlg-overlay"
        role="presentation"
        @click.self="dialog.onCancel()"
      >
        <div
          class="dlg-modal"
          role="dialog"
          aria-modal="true"
          :aria-labelledby="dialog.title ? 'dlg-title' : undefined"
          @keydown.esc.prevent="dialog.onCancel()"
        >
          <h4 v-if="dialog.title" id="dlg-title" class="dlg-title">{{ dialog.title }}</h4>
          <p class="dlg-message">{{ dialog.message }}</p>

          <div v-if="dialog.mode === 'prompt'" class="dlg-field">
            <textarea
              v-if="dialog.inputMultiline"
              ref="inputEl"
              v-model="dialog.inputValue"
              class="form-control"
              rows="3"
              :placeholder="dialog.placeholder"
              @keydown.ctrl.enter.prevent="dialog.onConfirm()"
            />
            <input
              v-else
              ref="inputEl"
              v-model="dialog.inputValue"
              class="form-control"
              :placeholder="dialog.placeholder"
              @keyup.enter="dialog.onConfirm()"
            />
          </div>

          <div class="dlg-actions">
            <button
              v-if="dialog.mode !== 'alert'"
              type="button"
              class="btn btn-outline-modern"
              @click="dialog.onCancel()"
            >{{ dialog.cancelText }}</button>
            <button
              type="button"
              class="btn"
              :class="dialog.danger ? 'btn-danger-modern' : 'btn-forum'"
              @click="dialog.onConfirm()"
            >{{ dialog.confirmText }}</button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup>
import { nextTick, ref, watch } from 'vue'
import { useDialogStore } from '../stores/dialog'

const dialog = useDialogStore()
const inputEl = ref(null)

watch(
  () => dialog.open,
  async (v) => {
    if (!v) return
    await nextTick()
    if (dialog.mode === 'prompt') {
      inputEl.value?.focus?.()
      inputEl.value?.select?.()
    }
  },
)
</script>

<style scoped>
.dlg-overlay {
  position: fixed;
  inset: 0;
  z-index: 12000;
  background: rgba(15, 23, 42, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
}
.dlg-modal {
  width: min(420px, 100%);
  background: #fff;
  border-radius: 14px;
  padding: 22px 22px 18px;
  box-shadow: 0 20px 50px rgba(15, 23, 42, 0.22);
  border: 1px solid #e5e7eb;
}
.dlg-title {
  margin: 0 0 8px;
  font-size: 17px;
  font-weight: 700;
  color: #142033;
}
.dlg-message {
  margin: 0;
  font-size: 14px;
  line-height: 1.55;
  color: #475569;
  white-space: pre-wrap;
  word-break: break-word;
}
.dlg-field {
  margin-top: 14px;
}
.dlg-field .form-control {
  font-size: 14px;
}
.dlg-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 18px;
}
.btn-danger-modern {
  background: #dc2626;
  color: #fff;
  border: 1px solid #dc2626;
  border-radius: 8px;
  padding: 6px 14px;
  font-size: 13px;
  font-weight: 600;
}
.btn-danger-modern:hover {
  background: #b91c1c;
  border-color: #b91c1c;
  color: #fff;
}

.dlg-fade-enter-active,
.dlg-fade-leave-active {
  transition: opacity 0.18s ease;
}
.dlg-fade-enter-active .dlg-modal,
.dlg-fade-leave-active .dlg-modal {
  transition: transform 0.18s ease, opacity 0.18s ease;
}
.dlg-fade-enter-from,
.dlg-fade-leave-to {
  opacity: 0;
}
.dlg-fade-enter-from .dlg-modal,
.dlg-fade-leave-to .dlg-modal {
  opacity: 0;
  transform: translateY(8px) scale(0.98);
}
</style>
