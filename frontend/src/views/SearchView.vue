<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 搜索
    </div>

    <div class="panel mb-3">
      <div class="panel-header"><span class="accent"></span>搜索</div>
      <div class="p-3 search-bar-row">
        <input
          v-model="query"
          class="form-control"
          placeholder="输入关键词…"
          @keyup.enter="load(1)"
        />
        <select v-model="forumId" class="form-control" style="max-width:180px">
          <option value="">全部版块</option>
          <option v-for="f in forums" :key="f.id" :value="String(f.id)">{{ f.name }}</option>
        </select>
        <select v-model="type" class="form-control" style="max-width:120px">
          <option value="">全部类型</option>
          <option value="public">公开</option>
          <option value="coin">金币</option>
          <option value="poll">投票</option>
        </select>
        <input v-model="fromDate" type="date" class="form-control" style="max-width:150px" title="起始日期" />
        <input v-model="toDate" type="date" class="form-control" style="max-width:150px" title="结束日期" />
        <button class="btn btn-forum" @click="load(1)">搜索</button>
      </div>
    </div>

    <div class="panel">
      <div class="panel-header"><span class="accent"></span>搜索结果：{{ query || '（请输入关键词）' }}</div>

      <div v-if="loading" class="p-3 text-muted">搜索中...</div>
      <div v-else-if="!query" class="p-3 text-muted">输入关键词开始搜索</div>
      <div v-else-if="!items.length" class="p-3 text-muted">
        没有找到与「{{ query }}」相关的内容
      </div>
      <template v-else>
        <div v-for="t in items" :key="t.id" class="search-item">
          <div class="search-title">
            <router-link :to="`/thread/${t.id}`" v-html="highlight(t.title)"></router-link>
            <span v-if="t.type === 'coin'" class="type-badge type-coin">金币</span>
            <span v-if="t.type === 'poll'" class="type-badge">投票</span>
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
import { useRoute, useRouter } from 'vue-router'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import PaginationComp from '../components/PaginationComp.vue'

const route = useRoute()
const router = useRouter()
const items = ref([])
const forums = ref([])
const total = ref(0)
const page = ref(1)
const loading = ref(false)
const pageSize = 20
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))
const query = ref(route.query.q?.toString() || '')
const forumId = ref(route.query.forumId?.toString() || '')
const type = ref(route.query.type?.toString() || '')
const fromDate = ref(route.query.from?.toString() || '')
const toDate = ref(route.query.to?.toString() || '')

function escapeHtml(s) {
  return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
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

async function loadForums() {
  try {
    const { data } = await api.get('/categories')
    const list = []
    for (const c of data || []) {
      for (const f of c.forums || []) list.push({ id: f.id, name: f.name })
    }
    forums.value = list
  } catch { forums.value = [] }
}

async function load(p) {
  page.value = p || 1
  if (!query.value.trim()) {
    items.value = []
    total.value = 0
    return
  }
  loading.value = true
  router.replace({
    query: {
      q: query.value,
      forumId: forumId.value || undefined,
      type: type.value || undefined,
      from: fromDate.value || undefined,
      to: toDate.value || undefined,
      page: page.value > 1 ? String(page.value) : undefined,
    },
  })
  try {
    const { data } = await api.get('/search', {
      params: {
        q: query.value,
        page: page.value,
        pageSize,
        forumId: forumId.value || undefined,
        type: type.value || undefined,
        from: fromDate.value || undefined,
        to: toDate.value || undefined,
      },
    })
    items.value = data.items
    total.value = data.total
  } catch {
    items.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

watch(page, (p) => { if (p > 0 && query.value) load(p) })

onMounted(async () => {
  await loadForums()
  if (query.value) await load(Number(route.query.page) || 1)
})
</script>

<style scoped>
.search-bar-row {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.search-item {
  padding: 14px 16px;
  border-bottom: 1px solid var(--line, #f1f5f9);
  max-width: 100%;
  overflow: hidden;
}
.search-title {
  font-weight: 600;
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
}
.search-snippet {
  font-size: 13px;
  color: #64748b;
  margin-bottom: 6px;
  overflow-wrap: anywhere;
  word-break: break-word;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.search-meta { font-size: 12px; color: #94a3b8; }
.sep { margin: 0 4px; }
:deep(mark.hl) {
  background: rgba(13, 148, 136, 0.2);
  color: inherit;
  padding: 0 2px;
  border-radius: 2px;
}
</style>
