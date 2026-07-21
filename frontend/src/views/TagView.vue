<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 标签 &gt; #{{ tagName }}
    </div>
    <div class="panel">
      <div class="panel-header">
        <div><span class="accent"></span>标签：#{{ tagName }}</div>
        <span class="text-muted" style="font-size:12px">共 {{ total }} 篇</span>
      </div>
      <div v-if="loading" class="p-3 text-muted">加载中...</div>
      <div v-else-if="!items.length" class="p-4 text-muted">该标签下暂无帖子</div>
      <template v-else>
        <div v-for="t in items" :key="t.id" class="tag-row">
          <div class="title">
            <router-link :to="`/thread/${t.id}`">{{ t.title }}</router-link>
            <span v-if="t.isEssence" class="type-badge type-essence">精品</span>
            <div class="meta mt-1">
              <span class="level-badge">Lv.{{ t.authorLevel }}</span>
              {{ t.authorNickname }}
              <span class="sep">·</span>
              <router-link :to="`/forum/${t.forumId}`">{{ t.forumName }}</router-link>
            </div>
          </div>
          <div class="meta">{{ t.replyCount }} 回复 / {{ t.views }} 浏览</div>
          <div class="meta">{{ formatTime(t.lastReplyAt) }}</div>
        </div>
        <div v-if="totalPages > 1" class="p-3 d-flex gap-2 justify-content-center">
          <button class="btn btn-sm btn-outline-secondary" :disabled="page <= 1" @click="load(page - 1)">上一页</button>
          <span class="align-self-center text-muted">{{ page }} / {{ totalPages }}</span>
          <button class="btn btn-sm btn-outline-secondary" :disabled="page >= totalPages" @click="load(page + 1)">下一页</button>
        </div>
      </template>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'

const route = useRoute()
const items = ref([])
const page = ref(1)
const total = ref(0)
const pageSize = 20
const loading = ref(true)
const tagName = computed(() => decodeURIComponent(String(route.params.name || '')))
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

function formatTime(iso) {
  const d = new Date(iso)
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getMonth() + 1}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

async function load(p = 1) {
  loading.value = true
  page.value = p
  try {
    const { data } = await api.get(`/tags/${encodeURIComponent(tagName.value)}`, {
      params: { page: p, pageSize },
    })
    items.value = data.items || []
    total.value = data.total || 0
  } catch {
    items.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

onMounted(() => load(1))
watch(() => route.params.name, () => load(1))
</script>

<style scoped>
.tag-row {
  display: grid;
  grid-template-columns: 1fr auto auto;
  gap: 16px;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid #f1f5f9;
}
.title a { color: #142033; text-decoration: none; font-weight: 600; }
.title a:hover { color: #0d9488; }
.meta { font-size: 12px; color: #94a3b8; }
.meta a { color: #64748b; text-decoration: none; }
.meta a:hover { color: #0d9488; }
.sep { margin: 0 4px; }
.type-badge {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
  margin-left: 6px;
  font-weight: 600;
}
.type-essence { background: #fff7ed; color: #c2410c; border: 1px solid #fdba74; }
.level-badge {
  display: inline-block;
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
  background: #ecfdf5;
  color: #0f766e;
  margin-right: 4px;
}
@media (max-width: 640px) {
  .tag-row { grid-template-columns: 1fr; gap: 4px; }
}
</style>
