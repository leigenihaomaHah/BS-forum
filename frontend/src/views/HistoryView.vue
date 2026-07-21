<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 浏览历史
    </div>
    <div class="panel">
      <div class="panel-header">
        <div><span class="accent"></span>浏览历史</div>
        <button class="btn btn-sm btn-outline-secondary" :disabled="!items.length" @click="clear">清空</button>
      </div>
      <div v-if="loading" class="p-3 text-muted">加载中...</div>
      <div v-else-if="!items.length" class="p-4 text-muted">暂无浏览记录，去看看帖子吧</div>
      <div v-else>
        <div v-for="h in items" :key="h.threadId + h.viewedAt" class="hist-row">
          <div>
            <router-link :to="`/thread/${h.threadId}`">{{ h.title }}</router-link>
            <div class="meta">{{ h.forumName }} · {{ h.authorNickname }} · {{ h.replyCount }} 回复</div>
          </div>
          <div class="time">{{ timeAgo(h.viewedAt) }}</div>
        </div>
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AppLayout from '../components/AppLayout.vue'
import api from '../api/http'
import { timeAgo } from '../utils/time.js'

const items = ref([])
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    const { data } = await api.get('/me/history', { params: { take: 50 } })
    items.value = data
  } finally { loading.value = false }
}

async function clear() {
  if (!confirm('确定清空浏览历史？')) return
  await api.delete('/me/history')
  items.value = []
}

onMounted(load)
</script>

<style scoped>
.hist-row {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 16px;
  border-bottom: 1px solid #f1f5f9;
}
.meta { font-size: 12px; color: #94a3b8; margin-top: 4px; }
.time { font-size: 12px; color: #94a3b8; white-space: nowrap; }
</style>
