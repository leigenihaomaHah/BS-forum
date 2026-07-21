<template>
  <div v-if="auth.isLoggedIn && items.length" class="panel mb-3">
    <div class="panel-header"><span class="accent"></span>关注动态</div>
    <ul class="feed-list">
      <li v-for="f in items" :key="f.threadId + '-' + f.createdAt">
        <router-link :to="`/user/${f.authorId}`">{{ f.authorNickname }}</router-link>
        发布了
        <router-link :to="`/thread/${f.threadId}`">{{ f.title }}</router-link>
        <span class="meta">{{ f.forumName }}</span>
      </li>
    </ul>
    <div v-if="!items.length" class="p-3 text-muted" style="font-size:13px">关注用户后，这里会显示他们的新帖</div>
  </div>
</template>

<script setup>
import { onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import api from '../api/http'

const auth = useAuthStore()
const items = ref([])

async function load() {
  if (!auth.isLoggedIn) { items.value = []; return }
  try {
    const { data } = await api.get('/feed', { params: { take: 10 } })
    items.value = data
  } catch { items.value = [] }
}

watch(() => auth.isLoggedIn, load)
onMounted(load)
</script>

<style scoped>
.feed-list { list-style: none; margin: 0; padding: 8px 0; }
.feed-list li {
  padding: 10px 16px;
  border-bottom: 1px solid #f1f5f9;
  font-size: 13px;
}
.meta { color: #94a3b8; margin-left: 6px; font-size: 12px; }
</style>
