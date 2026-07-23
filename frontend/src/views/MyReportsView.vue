<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt;
      <router-link to="/me">个人中心</router-link> &gt; 我的举报
    </div>

    <div v-if="!auth.isLoggedIn" class="auth-card" style="max-width: 420px; margin: 48px auto;">
      <h4 class="mb-1">请先登录</h4>
      <button type="button" class="btn btn-forum w-100" @click="authModal.openLogin()">前往登录</button>
    </div>

    <div v-else class="panel">
      <div class="panel-header"><span class="accent"></span>我的举报</div>
      <div v-if="loading" class="p-4 text-muted">加载中...</div>
      <div v-else-if="!items.length" class="p-4 text-muted">暂无举报记录</div>
      <div v-else>
        <div v-for="r in items" :key="r.id" class="report-row">
          <div class="report-top">
            <span class="report-status" :class="'status-' + r.status">{{ statusLabel(r.status) }}</span>
            <span class="report-time">{{ formatTime(r.createdAt) }}</span>
          </div>
          <div class="report-target">
            <span class="text-muted">{{ r.targetType === 'thread' ? '主题' : '回复' }}：</span>
            {{ r.targetTitle || `#${r.targetId}` }}
          </div>
          <div class="report-reason">原因：{{ r.reason }}</div>
          <div v-if="r.handleNote" class="report-note">处理说明：{{ r.handleNote }}</div>
        </div>
        <PaginationComp v-model="page" :total-pages="totalPages" />
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import PaginationComp from '../components/PaginationComp.vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { formatTime } from '../utils/time.js'

const auth = useAuthStore()
const authModal = useAuthModalStore()
const items = ref([])
const page = ref(1)
const total = ref(0)
const loading = ref(false)
const pageSize = 20
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

function statusLabel(s) {
  if (s === 'pending') return '待处理'
  if (s === 'resolved') return '已处理'
  if (s === 'rejected') return '已驳回'
  return s
}

async function load(p) {
  if (!auth.isLoggedIn) return
  if (p) page.value = p
  loading.value = true
  try {
    const { data } = await api.get('/me/reports', { params: { page: page.value, pageSize } })
    items.value = data.items || []
    total.value = data.total || 0
  } catch {
    items.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

watch(page, () => load())
watch(() => auth.isLoggedIn, () => load())
onMounted(() => load())
</script>

<style scoped>
.report-row {
  padding: 14px 16px;
  border-bottom: 1px solid var(--line, rgba(20,32,51,0.08));
}
.report-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}
.report-status {
  font-size: 12px;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 6px;
}
.status-pending { background: #fef3c7; color: #b45309; }
.status-resolved { background: rgba(13,148,136,0.12); color: #0f766e; }
.status-rejected { background: #fee2e2; color: #b91c1c; }
.report-time { font-size: 12px; color: #94a3b8; }
.report-target { font-size: 14px; font-weight: 600; margin-bottom: 4px; }
.report-reason { font-size: 13px; color: #64748b; }
.report-note {
  margin-top: 6px;
  font-size: 12px;
  color: #475569;
  padding: 8px 10px;
  background: #f8fafc;
  border-radius: 8px;
}
</style>
