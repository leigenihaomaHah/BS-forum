<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt;
      <router-link to="/me">个人中心</router-link> &gt; 我的帖子
    </div>

    <div class="panel">
      <div class="panel-header"><span class="accent"></span>我的帖子</div>
      <div v-if="loading" class="p-3 text-muted">加载中...</div>
      <template v-else>
        <div v-if="!items.length" class="p-3 text-muted">暂无帖子</div>
        <div
          v-for="t in items"
          :key="t.id"
          class="thread-row"
          :class="{ 'is-pinned': t.isPinned, 'is-essence': t.isEssence }"
        >
          <div class="title">
            <router-link :to="`/thread/${t.id}`">{{ t.title }}</router-link>
            <span v-if="t.isPinned" class="type-badge type-pin">置顶</span>
            <span v-if="t.isEssence" class="type-badge type-essence">精品</span>
            <span v-if="t.type === 'private'" class="type-badge type-private">私密</span>
            <span v-if="t.type === 'coin'" class="type-badge type-coin">金币</span>
            <div class="meta mt-1">{{ t.forumName || t.authorNickname }}</div>
          </div>
          <div class="meta">{{ t.replyCount }} 回复 / {{ t.views }} 浏览 / {{ t.likeCount }} 赞</div>
          <div class="meta">{{ formatTime(t.createdAt, false) }}</div>
        </div>
        <PaginationComp v-model="page" :total-pages="totalPages" />
      </template>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import PaginationComp from '../components/PaginationComp.vue'
import { formatTime } from '../utils/time.js'

const items = ref([])
const page = ref(1)
const total = ref(0)
const pageSize = 20
const loading = ref(true)
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

async function load(p) {
  page.value = p || 1
  loading.value = true
  try {
    const { data } = await api.get('/me/threads', { params: { page: page.value, pageSize } })
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
watch(page, (p) => { if (p > 0) load(p) })
</script>

<style scoped>
.thread-row {
  display: flex;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid rgba(20,32,51,0.06);
  gap: 12px;
}
.thread-row:last-child { border-bottom: none; }
.thread-row .title { flex: 1; min-width: 0; }
.thread-row .title a {
  color: #142033;
  text-decoration: none;
  font-weight: 600;
  font-size: 14px;
}
.thread-row .title a:hover { color: #0d9488; }
.thread-row .meta {
  font-size: 12px;
  color: #7a869c;
  white-space: nowrap;
  flex-shrink: 0;
}
.type-badge {
  font-size: 10px;
  padding: 1px 5px;
  border-radius: 4px;
  margin-left: 4px;
  vertical-align: middle;
  font-weight: 600;
}
.type-pin { background: #fee2e2; color: #b91c1c; }
.type-essence { background: #fff7ed; color: #c2410c; }
.type-private { background: #f3e8ff; color: #7c3aed; }
.type-coin { background: #fef9c3; color: #a16207; }
</style>
