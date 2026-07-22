<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 关注动态
    </div>

    <div v-if="!auth.isLoggedIn" class="auth-card" style="max-width: 420px; margin: 48px auto;">
      <h4 class="mb-1">请先登录</h4>
      <p class="text-muted mb-3" style="font-size: 13px">登录并关注用户后，这里会显示他们的新帖</p>
      <button type="button" class="btn btn-forum w-100" @click="authModal.openLogin()">前往登录</button>
    </div>

    <div v-else class="panel">
      <div class="panel-header">
        <span><span class="accent"></span>关注动态</span>
        <span class="text-muted" style="font-size:12px">共 {{ total }} 条</span>
      </div>
      <div v-if="loading" class="p-4 text-muted">加载中...</div>
      <div v-else-if="!items.length" class="p-4 text-muted">
        暂无动态。去用户主页关注感兴趣的人，他们的新帖会出现在这里。
      </div>
      <div v-else>
        <div v-for="f in items" :key="f.threadId + '-' + f.createdAt" class="feed-row">
          <div class="feed-main">
            <router-link :to="`/user/${f.authorId}`" class="feed-author">{{ f.authorNickname }}</router-link>
            <span class="feed-action">发布了</span>
            <router-link :to="`/thread/${f.threadId}`" class="feed-title">{{ f.title }}</router-link>
          </div>
          <div class="feed-meta">{{ f.forumName }} · {{ formatTime(f.createdAt) }}</div>
        </div>
        <PaginationComp v-model="page" :total-pages="totalPages" />
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import AppLayout from '../components/AppLayout.vue'
import PaginationComp from '../components/PaginationComp.vue'
import api from '../api/http'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { formatTime } from '../utils/time.js'

const auth = useAuthStore()
const authModal = useAuthModalStore()
const items = ref([])
const page = ref(1)
const total = ref(0)
const pageSize = 20
const loading = ref(false)
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

async function load(p) {
  if (!auth.isLoggedIn) return
  if (p) page.value = p
  loading.value = true
  try {
    const { data } = await api.get('/feed', { params: { page: page.value, pageSize } })
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
watch(() => auth.isLoggedIn, () => load(1))
watch(page, () => load())
</script>

<style scoped>
.feed-row {
  padding: 14px 16px;
  border-bottom: 1px solid rgba(20, 32, 51, 0.06);
}
.feed-row:last-child { border-bottom: none; }
.feed-main {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 6px;
  font-size: 14px;
}
.feed-author {
  font-weight: 700;
  color: #142033;
  text-decoration: none;
}
.feed-author:hover { color: #0d9488; }
.feed-action { color: #64748b; font-size: 13px; }
.feed-title {
  font-weight: 600;
  color: #0f766e;
  text-decoration: none;
}
.feed-title:hover { text-decoration: underline; }
.feed-meta {
  margin-top: 4px;
  font-size: 12px;
  color: #94a3b8;
}
</style>
