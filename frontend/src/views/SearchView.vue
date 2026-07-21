<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 搜索
    </div>

    <div class="panel">
      <div class="panel-header"><span class="accent"></span>搜索结果：{{ query }}</div>

      <div v-if="loading" class="p-3 text-muted">搜索中...</div>
      <div v-else-if="!items.length" class="p-3 text-muted">
        没有找到与「{{ query }}」相关的内容
      </div>
      <template v-else>
        <div v-for="t in items" :key="t.id" class="search-item">
          <div class="search-title">
            <router-link :to="`/thread/${t.id}`" v-html="highlight(t.title)"></router-link>
            <span v-if="t.type === 'private'" class="type-badge type-private">私密</span>
            <span v-if="t.type === 'coin'" class="type-badge type-coin">金币</span>
          </div>
          <div class="search-snippet" v-html="highlight(t.snippet)"></div>
          <div class="search-meta">
            <span class="level-badge" :class="{ 'lv-high': t.authorLevel >= 5 }">Lv.{{ t.authorLevel }}</span>
            {{ t.authorNickname }}
            <span class="sep">·</span>
            {{ t.forumName }}
            <span class="sep">·</span>
            {{ t.replyCount }} 回复 / {{ t.views }} 浏览
            <span class="sep">·</span>
            {{ fmt(t.createdAt) }}
          </div>
        </div>
        <PaginationComp v-model="page" :total-pages="totalPages" />
        <div v-if="total > 0" class="p-2 text-center text-muted" style="font-size:12px">共 {{ total }} 条结果</div>
      </template>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import PaginationComp from '../components/PaginationComp.vue'

const route = useRoute()
const items = ref([])
const total = ref(0)
const page = ref(1)
const loading = ref(true)
const pageSize = 20
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))
const query = ref('')

function escapeHtml(s) {
  return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

function highlight(text) {
  if (!text || !query.value) return escapeHtml(text)
  const escaped = escapeHtml(text)
  const words = query.value.split(/\s+/).filter(Boolean).map(w => w.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))
  if (!words.length) return escaped
  const re = new RegExp('(' + words.join('|') + ')', 'gi')
  return escaped.replace(re, '<mark class="hl">$1</mark>')
}

function fmt(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`
}

async function load(p) {
  page.value = p || 1
  loading.value = true
  try {
    const { data } = await api.get('/search', { params: { q: query.value, page: page.value, pageSize } })
    items.value = data.items
    total.value = data.total
  } catch {
    items.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

watch(() => route.query.q, (q) => {
  if (q) {
    query.value = q
    load(1)
  }
}, { immediate: true })
watch(page, (p) => { if (p > 0) load(p) })
</script>

<style scoped>
.search-item {
  padding: 14px 18px;
  border-bottom: 1px solid var(--line, rgba(20,32,51,0.08));
  transition: background 0.15s;
}
.search-item:last-child { border-bottom: none; }
.search-item:hover { background: #f8fafc; }
.search-title { font-size: 15px; font-weight: 600; }
.search-title a { color: var(--ink, #142033); text-decoration: none; }
.search-title a:hover { color: var(--accent, #0d9488); }
.search-snippet {
  font-size: 13px;
  color: #3d4a63;
  margin-top: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 100%;
}
.search-meta {
  font-size: 12px;
  color: #7a869c;
  margin-top: 6px;
}
.sep { margin: 0 6px; color: #d0d5dd; }
.type-badge {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
  margin-left: 6px;
  vertical-align: middle;
  font-weight: 600;
}
.type-private { background: #f3e8ff; color: #7c3aed; }
.type-coin { background: #fef3c7; color: #b45309; }
:deep(.hl) { background: #fef08a; color: #854d0e; padding: 0 2px; border-radius: 2px; }
</style>
