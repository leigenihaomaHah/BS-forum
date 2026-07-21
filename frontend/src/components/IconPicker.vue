<template>
  <div class="icon-picker">
    <button
      type="button"
      class="icon-picker-trigger"
      :title="'当前: ' + (modelValue || '未选')"
      @click="open = !open"
    >
      <span class="icon-picker-current">{{ modelValue || '📁' }}</span>
      <span class="icon-picker-caret">▾</span>
    </button>
    <div v-if="open" class="icon-picker-panel">
      <div class="icon-picker-hint">选择图标</div>
      <div class="icon-picker-grid">
        <button
          v-for="icon in icons"
          :key="icon"
          type="button"
          class="icon-picker-item"
          :class="{ active: icon === modelValue }"
          :title="icon"
          @click="pick(icon)"
        >{{ icon }}</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { FORUM_ICON_OPTIONS } from '../config/forumIcons'

defineProps({
  modelValue: { type: String, default: '📁' }
})
const emit = defineEmits(['update:modelValue'])

const icons = FORUM_ICON_OPTIONS
const open = ref(false)

function pick(icon) {
  emit('update:modelValue', icon)
  open.value = false
}

function onDocClick(e) {
  if (!e.target.closest?.('.icon-picker')) open.value = false
}

onMounted(() => document.addEventListener('click', onDocClick))
onBeforeUnmount(() => document.removeEventListener('click', onDocClick))
</script>

<style scoped>
.icon-picker {
  position: relative;
  display: inline-block;
  vertical-align: middle;
  z-index: 2;
}
.icon-picker:has(.icon-picker-panel) {
  z-index: 40;
}
.icon-picker-trigger {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  border: 1px solid #d0d5dd;
  border-radius: 6px;
  background: #fff;
  cursor: pointer;
  line-height: 1.4;
  min-width: 52px;
}
.icon-picker-trigger:hover { border-color: #98a2b3; }
.icon-picker-current { font-size: 18px; }
.icon-picker-caret { font-size: 10px; color: #98a2b3; }
.icon-picker-panel {
  position: absolute;
  z-index: 50;
  top: calc(100% + 4px);
  left: 0;
  width: 280px;
  max-height: 220px;
  overflow: auto;
  padding: 8px;
  background: #fff;
  border: 1px solid #e4e7ec;
  border-radius: 8px;
  box-shadow: 0 8px 24px rgba(16, 24, 40, 0.12);
}
.icon-picker-hint {
  font-size: 11px;
  color: #667085;
  margin-bottom: 6px;
}
.icon-picker-grid {
  display: grid;
  grid-template-columns: repeat(8, 1fr);
  gap: 4px;
}
.icon-picker-item {
  width: 100%;
  aspect-ratio: 1;
  border: 1px solid transparent;
  border-radius: 6px;
  background: #f9fafb;
  font-size: 18px;
  line-height: 1;
  cursor: pointer;
  padding: 0;
}
.icon-picker-item:hover { background: #eef2ff; border-color: #c7d2fe; }
.icon-picker-item.active {
  background: #e0e7ff;
  border-color: #6366f1;
}
</style>
