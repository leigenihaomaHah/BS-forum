<template>
  <div class="md-editor" :class="{ 'md-editor-sm': compact }">
    <div class="md-editor-tabs">
      <button
        type="button"
        class="md-tab"
        :class="{ active: tab === 'write' }"
        @click="tab = 'write'"
      >编辑</button>
      <button
        type="button"
        class="md-tab"
        :class="{ active: tab === 'preview' }"
        @click="tab = 'preview'"
      >预览</button>
      <button
        v-if="tab === 'write'"
        type="button"
        class="md-tab md-tab-tool"
        title="插入粗体"
        @click="insert('**', '**')"
      ><b>B</b></button>
      <button
        v-if="tab === 'write'"
        type="button"
        class="md-tab md-tab-tool"
        title="插入斜体"
        @click="insert('*', '*')"
      ><em>I</em></button>
      <button
        v-if="tab === 'write'"
        type="button"
        class="md-tab md-tab-tool"
        title="插入链接"
        @click="insert('[', '](url)')"
      >🔗</button>
      <button
        v-if="tab === 'write'"
        type="button"
        class="md-tab md-tab-tool"
        title="插入代码块"
        @click="insert('```\n', '\n```')"
      >&lt;/&gt;</button>
      <span class="md-editor-hint">{{ hint }}</span>
    </div>

    <div v-if="tab === 'write'" class="md-textarea-wrap">
      <textarea
        ref="ta"
        :value="modelValue"
        @input="onInput"
        @keydown="onKeydown"
        @paste="onPaste"
        @scroll="positionMention"
        class="form-control md-textarea"
        :class="{ 'md-textarea-sm': compact }"
        :placeholder="placeholder"
        :rows="rows"
      ></textarea>

      <div
        v-if="showMention"
        class="mention-dropdown"
        :style="{ top: mentionTop + 'px', left: mentionLeft + 'px' }"
      >
        <div
          v-for="(u, idx) in mentionUsers"
          :key="u.id"
          class="mention-item"
          :class="{ active: mentionIdx === idx }"
          @mousedown.prevent="selectMention(u)"
        >
          <span class="mention-name">@{{ u.nickname || u.username }}</span>
          <span class="mention-uname">{{ u.username }}</span>
        </div>
        <div v-if="!mentionUsers.length && mentionQuery" class="mention-empty">无匹配用户</div>
      </div>
    </div>

    <div v-else class="md-preview-box">
      <MarkdownBody v-if="modelValue" :content="modelValue" />
      <div v-else class="md-preview-empty">暂无内容</div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'
import api from '../api/http'
import MarkdownBody from './MarkdownBody.vue'
import { filesFromClipboard } from '../utils/image.js'

const props = defineProps({
  modelValue: { type: String, default: '' },
  placeholder: { type: String, default: '' },
  rows: { type: Number, default: 8 },
  compact: { type: Boolean, default: false },
  hint: { type: String, default: '' },
})

const emit = defineEmits(['update:modelValue', 'paste-images'])

const tab = ref('write')
const ta = ref(null)

const showMention = ref(false)
const mentionQuery = ref('')
const mentionUsers = ref([])
const mentionIdx = ref(0)
const mentionTop = ref(0)
const mentionLeft = ref(0)
let mentionTimer = null

function onPaste(e) {
  const files = filesFromClipboard(e.clipboardData)
  if (files.length) {
    e.preventDefault()
    emit('paste-images', files)
    return
  }
  const html = e.clipboardData?.getData('text/html')
  const plain = e.clipboardData?.getData('text/plain')
  if (html && plain && !/<img[\s>]/i.test(html)) {
    e.preventDefault()
    const el = ta.value
    if (!el) return
    const start = el.selectionStart
    const end = el.selectionEnd
    const val = props.modelValue
    const newVal = val.substring(0, start) + plain + val.substring(end)
    emit('update:modelValue', newVal)
    requestAnimationFrame(() => {
      el.focus()
      const pos = start + plain.length
      el.setSelectionRange(pos, pos)
    })
  }
}

function focus() {
  ta.value?.focus()
  tab.value = 'write'
}
defineExpose({ focus })

function insert(before, after) {
  const el = ta.value
  if (!el) return
  const start = el.selectionStart
  const end = el.selectionEnd
  const val = props.modelValue
  const selected = val.substring(start, end)
  const text = before + selected + after
  const newVal = val.substring(0, start) + text + val.substring(end)
  emit('update:modelValue', newVal)
  requestAnimationFrame(() => {
    el.focus()
    el.setSelectionRange(start + before.length, start + before.length + selected.length)
  })
}

function getMentionContext(val, cursorPos) {
  const before = val.substring(0, cursorPos)
  const match = before.match(/@([A-Za-z0-9_\u4e00-\u9fa5]{0,20})$/)
  if (!match) return null
  return { query: match[1].toLowerCase(), start: cursorPos - match[0].length }
}

function positionMention() {
  // re-position on scroll
}

async function onInput(e) {
  const val = e.target.value
  emit('update:modelValue', val)
  const pos = e.target.selectionStart
  const ctx = getMentionContext(val, pos)
  if (ctx) {
    mentionQuery.value = ctx.query
    if (ctx.query.length >= 1) {
      clearTimeout(mentionTimer)
      mentionTimer = setTimeout(async () => {
        try {
          const { data } = await api.get('/users/search', { params: { q: ctx.query, limit: 6 } })
          mentionUsers.value = data || []
          mentionIdx.value = 0
          showMention.value = true
          // position dropdown
          const coords = getCaretCoords(e.target)
          mentionTop.value = coords.top + 24
          mentionLeft.value = coords.left
        } catch {
          showMention.value = false
        }
      }, 200)
      return
    }
  }
  showMention.value = false
}

function onKeydown(e) {
  if (!showMention.value) return
  if (e.key === 'ArrowDown') {
    e.preventDefault()
    mentionIdx.value = Math.min(mentionIdx.value + 1, mentionUsers.value.length - 1)
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    mentionIdx.value = Math.max(mentionIdx.value - 1, 0)
  } else if (e.key === 'Enter' || e.key === 'Tab') {
    if (mentionUsers.value[mentionIdx.value]) {
      e.preventDefault()
      selectMention(mentionUsers.value[mentionIdx.value])
    }
  } else if (e.key === 'Escape') {
    showMention.value = false
  }
}

function selectMention(user) {
  const el = ta.value
  if (!el) return
  const val = props.modelValue
  const pos = el.selectionStart
  const before = val.substring(0, pos)
  const match = before.match(/@([A-Za-z0-9_\u4e00-\u9fa5]{0,20})$/)
  if (!match) return
  const start = pos - match[0].length
  const name = user.nickname || user.username
  const newVal = val.substring(0, start) + `@${name} ` + val.substring(pos)
  emit('update:modelValue', newVal)
  showMention.value = false
  requestAnimationFrame(() => {
    el.focus()
    const newPos = start + name.length + 2
    el.setSelectionRange(newPos, newPos)
  })
}

function getCaretCoords(textarea) {
  const rect = textarea.getBoundingClientRect()
  const style = window.getComputedStyle(textarea)
  // approximate caret position based on scroll and line height
  return { top: rect.top + 40 - textarea.scrollTop + (textarea.scrollTop > 0 ? 0 : 0), left: rect.left + 12 }
}
</script>

<style scoped>
.md-editor {
  border: 1px solid rgba(20,32,51,0.12);
  border-radius: 10px;
  overflow: visible;
  background: #fff;
  position: relative;
  z-index: 1;
}
.md-editor:focus-within {
  z-index: 30;
}
.md-editor-tabs {
  display: flex;
  align-items: center;
  gap: 2px;
  padding: 6px 8px;
  background: #f8fafc;
  border-bottom: 1px solid rgba(20,32,51,0.08);
  border-radius: 10px 10px 0 0;
  overflow: hidden;
}
.md-tab {
  padding: 4px 14px;
  border: none;
  background: transparent;
  font-size: 13px;
  font-weight: 600;
  color: #7a869c;
  cursor: pointer;
  border-radius: 6px;
  transition: all 0.15s;
}
.md-tab:hover {
  color: #142033;
  background: rgba(20,32,51,0.06);
}
.md-tab.active {
  color: #0d9488;
  background: rgba(13,148,136,0.1);
}
.md-tab-tool {
  padding: 4px 8px;
  font-size: 14px;
  font-weight: 600;
  color: #5a6a85;
}
.md-tab-tool:hover {
  color: #0d9488;
}
.md-editor-hint {
  margin-left: auto;
  font-size: 11px;
  color: #94a3b8;
}
.md-textarea-wrap {
  position: relative;
}
.md-textarea {
  border: none !important;
  border-radius: 0 !important;
  resize: vertical;
  min-height: 160px;
  font-size: 14px;
  line-height: 1.7;
  padding: 12px 14px;
}
.md-textarea:focus {
  box-shadow: none !important;
}
.md-textarea-sm {
  min-height: 100px;
  font-size: 13px;
}
.md-preview-box {
  padding: 12px 14px;
  min-height: 120px;
}
.md-preview-empty {
  color: #94a3b8;
  font-size: 13px;
  text-align: center;
  padding: 32px 0;
}
.mention-dropdown {
  position: absolute;
  z-index: 200;
  background: #fff;
  border: 1px solid rgba(20,32,51,0.12);
  border-radius: 8px;
  box-shadow: 0 8px 24px rgba(0,0,0,0.12);
  min-width: 180px;
  max-height: 200px;
  overflow-y: auto;
}
.mention-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  cursor: pointer;
  font-size: 13px;
  transition: background 0.1s;
}
.mention-item:hover,
.mention-item.active {
  background: rgba(13,148,136,0.08);
}
.mention-name {
  font-weight: 600;
  color: #142033;
}
.mention-uname {
  font-size: 11px;
  color: #94a3b8;
}
.mention-empty {
  padding: 10px 12px;
  color: #94a3b8;
  font-size: 12px;
}
</style>
