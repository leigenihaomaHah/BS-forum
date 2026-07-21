<template>
  <div v-if="auth.isLoggedIn" class="panel mb-3">
    <div class="panel-header">
      <div><span class="accent"></span>订阅更新</div>
      <router-link to="/subscriptions" class="more">管理</router-link>
    </div>
    <div v-if="loading" class="p-3 text-muted" style="font-size:13px">加载中...</div>
    <div v-else-if="!items.length" class="p-3 text-muted" style="font-size:13px">
      订阅感兴趣的版块后，新帖会出现在这里
    </div>
    <ul v-else class="unread-list">
      <li v-for="t in items" :key="t.threadId">
        <span class="tag">{{ t.forumName }}</span>
        <router-link :to="`/thread/${t.threadId}`">{{ t.title }}</router-link>
        <span class="meta">{{ t.authorNickname }}</span>
      </li>
    </ul>
  </div>
</template>

<script setup>
import { onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import api from '../api/http'

const auth = useAuthStore()
const items = ref([])
const loading = ref(false)

async function load() {
  if (!auth.isLoggedIn) { items.value = []; return }
  loading.value = true
  try {
    const { data } = await api.get('/me/subscriptions/unread', { params: { take: 8 } })
    items.value = data
  } catch { items.value = [] }
  finally { loading.value = false }
}

watch(() => auth.isLoggedIn, load)
onMounted(load)
</script>

<style scoped>
.more { font-size: 12px; color: #0d9488; text-decoration: none; }
.unread-list { list-style: none; margin: 0; padding: 8px 0; }
.unread-list li {
  display: flex;
  gap: 8px;
  align-items: baseline;
  padding: 8px 16px;
  border-bottom: 1px solid #f1f5f9;
  font-size: 13px;
}
.tag {
  flex-shrink: 0;
  font-size: 11px;
  font-weight: 700;
  color: #0f766e;
  background: rgba(13,148,136,0.1);
  padding: 1px 6px;
  border-radius: 4px;
}
.meta { margin-left: auto; color: #94a3b8; font-size: 12px; }
</style>
