<template>
  <div v-if="auth.isLoggedIn" class="panel mb-3">
    <div class="panel-header">
      <span><span class="accent"></span>关注动态</span>
      <router-link to="/feed" class="more-link">查看全部</router-link>
    </div>
    <div v-if="loading" class="p-3 text-muted" style="font-size:13px">加载中...</div>
    <ul v-else-if="items.length" class="feed-list">
      <li v-for="f in items" :key="f.threadId + '-' + f.createdAt">
        <router-link :to="`/user/${f.authorId}`">{{ f.authorNickname }}</router-link>
        发布了
        <router-link :to="`/thread/${f.threadId}`">{{ f.title }}</router-link>
        <span class="meta">{{ f.forumName }}</span>
      </li>
    </ul>
    <div v-else class="p-3 text-muted" style="font-size:13px">关注用户后，这里会显示他们的新帖</div>
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
    const { data } = await api.get('/feed', { params: { page: 1, pageSize: 10 } })
    items.value = data.items || []
  } catch { items.value = [] }
  finally { loading.value = false }
}

watch(() => auth.isLoggedIn, load)
onMounted(load)
</script>

<style scoped>
.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.more-link {
  font-size: 12px;
  font-weight: 600;
  color: #0d9488;
  text-decoration: none;
}
.feed-list { list-style: none; margin: 0; padding: 8px 0; }
.feed-list li {
  padding: 10px 16px;
  border-bottom: 1px solid #f1f5f9;
  font-size: 13px;
}
.meta { color: #94a3b8; margin-left: 6px; font-size: 12px; }
</style>
