<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 我的订阅
    </div>
    <div class="panel">
      <div class="panel-header"><span class="accent"></span>版块订阅</div>
      <div v-if="loading" class="p-3 text-muted">加载中...</div>
      <div v-else-if="!items.length" class="p-4 text-muted">还没有订阅版块，去版块页点「订阅」吧</div>
      <div v-else>
        <div v-for="s in items" :key="s.forumId" class="sub-row">
          <div>
            <router-link :to="`/forum/${s.forumId}`">{{ s.icon || '' }} {{ s.forumName }}</router-link>
            <div class="meta">
              <span v-if="s.unreadCount" class="unread">{{ s.unreadCount }} 条未读</span>
              <span v-else>暂无未读</span>
            </div>
          </div>
          <div class="ops">
            <button class="btn btn-sm btn-outline-secondary" @click="markRead(s)">标为已读</button>
            <button class="btn btn-sm btn-outline-danger" @click="unsub(s)">取消订阅</button>
          </div>
        </div>
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AppLayout from '../components/AppLayout.vue'
import api from '../api/http'

const items = ref([])
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    const { data } = await api.get('/me/subscriptions')
    items.value = data
  } finally { loading.value = false }
}

async function markRead(s) {
  await api.post(`/forums/${s.forumId}/read`)
  s.unreadCount = 0
}

async function unsub(s) {
  await api.post(`/forums/${s.forumId}/subscribe`)
  await load()
}

onMounted(load)
</script>

<style scoped>
.sub-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border-bottom: 1px solid #f1f5f9;
}
.meta { font-size: 12px; color: #94a3b8; margin-top: 4px; }
.unread { color: #e11d48; font-weight: 700; }
.ops { display: flex; gap: 6px; }
</style>
