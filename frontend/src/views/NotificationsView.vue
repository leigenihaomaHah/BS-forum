<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 消息中心
    </div>
    <div class="panel">
      <div class="panel-header">
        <div><span class="accent"></span>消息中心</div>
        <button class="btn btn-sm btn-outline-secondary" :disabled="!list.length" @click="markAll">全部已读</button>
      </div>
      <div class="tabs">
        <button v-for="t in tabs" :key="t.key" class="tab" :class="{ active: type === t.key }" @click="switchType(t.key)">
          {{ t.label }}
          <span v-if="badge(t.key)" class="dot">{{ badge(t.key) }}</span>
        </button>
      </div>
      <div v-if="loading" class="p-3 text-muted">加载中...</div>
      <div v-else-if="!list.length" class="p-4 text-muted">暂无消息</div>
      <div v-else class="msg-list">
        <div
          v-for="n in list"
          :key="n.id"
          class="msg-item"
          :class="{ unread: !n.read }"
          @click="open(n)"
        >
          <div class="msg-top">
            <span class="msg-type">{{ typeLabel(n.type) }}</span>
            <span class="msg-time">{{ timeAgo(n.createdAt) }}</span>
          </div>
          <div class="msg-title">
            <template v-if="n.type === 'follow'">
              <router-link :to="`/user/${n.fromUserId}`" @click.stop>{{ n.fromNickname }}</router-link>
              {{ n.content }}
            </template>
            <template v-else>
              <router-link v-if="n.threadId" :to="`/thread/${n.threadId}`" @click.stop>{{ n.threadTitle || '查看详情' }}</router-link>
              <span v-else>{{ n.threadTitle || '系统消息' }}</span>
            </template>
          </div>
          <div class="msg-body">{{ n.fromNickname && n.type !== 'follow' ? n.fromNickname + ' · ' : '' }}{{ n.content }}</div>
        </div>
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import AppLayout from '../components/AppLayout.vue'
import api from '../api/http'

const router = useRouter()
const tabs = [
  { key: 'all', label: '全部' },
  { key: 'interact', label: '互动' },
  { key: 'forum', label: '订阅' },
  { key: 'follow', label: '关注' },
  { key: 'system', label: '系统' },
]
const type = ref('all')
const list = ref([])
const summary = ref(null)
const loading = ref(false)

function badge(key) {
  if (!summary.value) return 0
  if (key === 'all') return summary.value.totalUnread
  if (key === 'interact') return summary.value.replyUnread + summary.value.mentionUnread + summary.value.tipUnread
  if (key === 'forum') return summary.value.forumUnread
  if (key === 'follow') return summary.value.followUnread
  if (key === 'system') return summary.value.systemUnread
  return 0
}

function typeLabel(t) {
  return ({ reply: '回复', mention: '提及', tip: '打赏', forum: '订阅', follow: '关注', system: '系统' })[t] || t
}

function timeAgo(iso) {
  const sec = Math.floor((Date.now() - new Date(iso).getTime()) / 1000)
  if (sec < 60) return '刚刚'
  if (sec < 3600) return `${Math.floor(sec / 60)} 分钟前`
  if (sec < 86400) return `${Math.floor(sec / 3600)} 小时前`
  return `${Math.floor(sec / 86400)} 天前`
}

async function loadSummary() {
  const { data } = await api.get('/me/notifications/summary')
  summary.value = data
}

async function load() {
  loading.value = true
  try {
    const { data } = await api.get('/me/notifications', { params: { type: type.value === 'all' ? undefined : type.value } })
    list.value = data
  } finally {
    loading.value = false
  }
}

async function switchType(k) {
  type.value = k
  await load()
}

async function markAll() {
  await api.put('/me/notifications/read-all', null, { params: { type: type.value === 'all' ? undefined : type.value } })
  await Promise.all([load(), loadSummary()])
}

async function open(n) {
  if (!n.read) {
    try { await api.put(`/me/notifications/${n.id}/read`) } catch { /* ignore */ }
    n.read = true
    await loadSummary()
  }
  if (n.type === 'follow' && n.fromUserId) router.push(`/user/${n.fromUserId}`)
  else if (n.threadId) router.push(`/thread/${n.threadId}`)
}

onMounted(async () => {
  await Promise.all([loadSummary(), load()])
})
</script>

<style scoped>
.tabs {
  display: flex;
  gap: 6px;
  padding: 12px 16px 0;
  flex-wrap: wrap;
  border-bottom: 1px solid #f1f5f9;
}
.tab {
  border: none;
  background: #f8fafc;
  border-radius: 999px;
  padding: 6px 12px;
  font-size: 13px;
  font-weight: 600;
  color: #64748b;
  cursor: pointer;
}
.tab.active { background: rgba(13,148,136,0.12); color: #0f766e; }
.dot {
  margin-left: 4px;
  background: #e11d48;
  color: #fff;
  border-radius: 999px;
  font-size: 10px;
  padding: 0 5px;
}
.msg-list { padding: 8px 0; }
.msg-item {
  padding: 12px 16px;
  border-bottom: 1px solid #f1f5f9;
  cursor: pointer;
}
.msg-item.unread { background: rgba(13,148,136,0.04); }
.msg-top { display: flex; justify-content: space-between; font-size: 12px; color: #94a3b8; margin-bottom: 4px; }
.msg-type { font-weight: 700; color: #0f766e; }
.msg-title { font-weight: 600; font-size: 14px; }
.msg-body { font-size: 13px; color: #64748b; margin-top: 4px; }
</style>
