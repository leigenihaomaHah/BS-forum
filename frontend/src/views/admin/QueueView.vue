<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">审核队列</h2>
      <div class="d-flex gap-2">
        <button
          class="admin-btn admin-btn-primary"
          :disabled="!selectedThreadIds.length || batching"
          @click="batchApprove"
        >批量通过{{ selectedThreadIds.length ? ` (${selectedThreadIds.length})` : '' }}</button>
        <button
          class="admin-btn admin-btn-outline"
          :disabled="!selectedThreadIds.length || batching"
          @click="batchReject"
        >批量驳回</button>
        <button class="admin-btn admin-btn-primary" :disabled="loading" @click="load">
          {{ loading ? '刷新中…' : '刷新' }}
        </button>
      </div>
    </div>

    <div class="summary-row">
      <div class="summary-card" :class="{ hot: pendingThreads.length }">
        <div class="summary-num">{{ pendingThreads.length }}{{ threadTotal > pendingThreads.length ? '+' : '' }}</div>
        <div class="summary-label">待审帖</div>
      </div>
      <div class="summary-card" :class="{ hot: pendingReports.length }">
        <div class="summary-num">{{ pendingReports.length }}{{ reportTotal > pendingReports.length ? '+' : '' }}</div>
        <div class="summary-label">待处理举报</div>
      </div>
      <div class="summary-card" :class="{ hot: pendingOrders.length }">
        <div class="summary-num">{{ pendingOrders.length }}{{ orderTotal > pendingOrders.length ? '+' : '' }}</div>
        <div class="summary-label">待确认开通</div>
      </div>
    </div>

    <div v-if="loading" class="admin-panel p-3 text-muted">加载中...</div>
    <div v-else-if="!queueItems.length" class="admin-panel p-4 text-muted">当前无积压，队列为空</div>

    <div v-else class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th style="width:36px">
              <input type="checkbox" :checked="allThreadsSelected" @change="toggleSelectAll" title="全选待审帖" />
            </th>
            <th>类型</th>
            <th>内容</th>
            <th>等待</th>
            <th>时间</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in queueItems" :key="item.key">
            <td>
              <input
                v-if="item.type === 'thread'"
                type="checkbox"
                :value="item.id"
                v-model="selectedThreadIds"
              />
            </td>
            <td><span class="type-tag" :class="item.type">{{ item.typeLabel }}</span></td>
            <td>
              <div class="item-title">{{ item.title }}</div>
              <div class="item-meta">{{ item.meta }}</div>
            </td>
            <td>
              <span :class="{ overdue: item.waitHours >= 2 }">{{ item.waitText }}</span>
            </td>
            <td class="text-muted" style="font-size:12px">{{ fmtTime(item.createdAt) }}</td>
            <td class="ops">
              <template v-if="item.type === 'thread'">
                <button class="admin-btn admin-btn-primary" @click="approveThread(item)">通过</button>
                <button class="admin-btn admin-btn-outline" @click="rejectThread(item)">驳回</button>
                <router-link class="admin-btn admin-btn-outline" :to="`/thread/${item.id}`" target="_blank">查看</router-link>
              </template>
              <template v-else-if="item.type === 'report'">
                <button class="admin-btn admin-btn-primary" @click="handleReport(item, 'resolve')">处理</button>
                <button class="admin-btn admin-btn-outline" @click="handleReport(item, 'reject')">驳回</button>
                <router-link class="admin-btn admin-btn-outline" to="/admin/reports">详情</router-link>
              </template>
              <template v-else-if="item.type === 'recharge'">
                <button class="admin-btn admin-btn-primary" @click="confirmOrder(item)">确认到账</button>
                <button class="admin-btn admin-btn-outline" @click="cancelOrder(item)">取消</button>
                <router-link class="admin-btn admin-btn-outline" to="/admin/recharge">详情</router-link>
              </template>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'
import { useDialogStore } from '../../stores/dialog'

const toast = useToastStore()
const dialog = useDialogStore()
const loading = ref(false)
const batching = ref(false)
const selectedThreadIds = ref([])
const pendingThreads = ref([])
const pendingReports = ref([])
const pendingOrders = ref([])
const threadTotal = ref(0)
const reportTotal = ref(0)
const orderTotal = ref(0)

const allThreadsSelected = computed(() => {
  const ids = pendingThreads.value.map((t) => t.id)
  return ids.length > 0 && ids.every((id) => selectedThreadIds.value.includes(id))
})

function toggleSelectAll(e) {
  if (e.target.checked) selectedThreadIds.value = pendingThreads.value.map((t) => t.id)
  else selectedThreadIds.value = []
}

function waitInfo(iso) {
  if (!iso) return { hours: 0, text: '-' }
  const ms = Date.now() - new Date(iso).getTime()
  const hours = Math.max(0, ms / 3600000)
  if (hours < 1) return { hours, text: `${Math.max(1, Math.round(hours * 60))} 分钟` }
  if (hours < 24) return { hours, text: `${hours.toFixed(1)} 小时` }
  return { hours, text: `${(hours / 24).toFixed(1)} 天` }
}

function fmtTime(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  const pad = (n) => String(n).padStart(2, '0')
  return `${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

const queueItems = computed(() => {
  const list = []
  for (const t of pendingThreads.value) {
    const w = waitInfo(t.createdAt)
    list.push({
      key: `t-${t.id}`,
      type: 'thread',
      typeLabel: '待审帖',
      id: t.id,
      title: t.title,
      meta: `${t.forumName || ''} · ${t.authorNickname || ''}`,
      createdAt: t.createdAt,
      waitHours: w.hours,
      waitText: w.text,
      raw: t,
    })
  }
  for (const r of pendingReports.value) {
    const w = waitInfo(r.createdAt)
    list.push({
      key: `r-${r.id}`,
      type: 'report',
      typeLabel: '举报',
      id: r.id,
      title: r.reason || '举报',
      meta: `${r.targetType} #${r.targetId} · ${r.reporterNickname || ''}`,
      createdAt: r.createdAt,
      waitHours: w.hours,
      waitText: w.text,
      raw: r,
    })
  }
  for (const o of pendingOrders.value) {
    const w = waitInfo(o.createdAt)
    list.push({
      key: `o-${o.id}`,
      type: 'recharge',
      typeLabel: '开通',
      id: o.id,
      title: `${o.nickname || o.username} · ${o.packageName}`,
      meta: `¥${o.priceYuan} · 申请 #${o.id}`,
      createdAt: o.createdAt,
      waitHours: w.hours,
      waitText: w.text,
      raw: o,
    })
  }
  return list.sort((a, b) => b.waitHours - a.waitHours)
})

async function load() {
  loading.value = true
  try {
    const [th, rp, od] = await Promise.all([
      api.get('/admin/threads', { params: { status: 'pending', page: 1, pageSize: 20 } }),
      api.get('/admin/reports', { params: { status: 'pending', page: 1, pageSize: 20 } }),
      api.get('/admin/recharge/orders', { params: { status: 'pending', page: 1, pageSize: 20 } }),
    ])
    pendingThreads.value = th.data?.items || []
    threadTotal.value = th.data?.total || pendingThreads.value.length
    pendingReports.value = rp.data?.items || rp.data || []
    reportTotal.value = rp.data?.total || pendingReports.value.length
    pendingOrders.value = od.data?.items || []
    orderTotal.value = od.data?.total || pendingOrders.value.length
    selectedThreadIds.value = selectedThreadIds.value.filter((id) =>
      pendingThreads.value.some((t) => t.id === id))
  } catch (e) {
    toast.error(e.message || '加载失败')
  } finally {
    loading.value = false
  }
}

async function batchApprove() {
  if (!selectedThreadIds.value.length) return
  if (!(await dialog.confirm(`确认批量通过 ${selectedThreadIds.value.length} 条待审帖？`))) return
  batching.value = true
  try {
    const { data } = await api.post('/admin/threads/batch-approve', { ids: selectedThreadIds.value })
    toast.success(data.message || '批量通过完成')
    selectedThreadIds.value = []
    await load()
  } catch (e) { toast.error(e.message) }
  finally { batching.value = false }
}

async function batchReject() {
  if (!selectedThreadIds.value.length) return
  const reason = await dialog.prompt('批量驳回原因（可选）')
  if (reason === null) return
  batching.value = true
  try {
    const { data } = await api.post('/admin/threads/batch-reject', {
      ids: selectedThreadIds.value,
      reason: reason || null,
    })
    toast.success(data.message || '批量驳回完成')
    selectedThreadIds.value = []
    await load()
  } catch (e) { toast.error(e.message) }
  finally { batching.value = false }
}

async function approveThread(item) {
  try {
    await api.post(`/admin/threads/${item.id}/approve`)
    toast.success('已通过')
    await load()
  } catch (e) { toast.error(e.message) }
}

async function rejectThread(item) {
  const reason = await dialog.prompt('驳回原因（可选）')
  if (reason === null) return
  try {
    await api.post(`/admin/threads/${item.id}/reject`, { reason: reason || null })
    toast.success('已驳回')
    await load()
  } catch (e) { toast.error(e.message) }
}

async function handleReport(item, action) {
  const note = await dialog.prompt(action === 'resolve' ? '处理备注（可选）' : '驳回原因（可选）')
  if (note === null) return
  try {
    await api.post(`/admin/reports/${item.id}/handle`, { action, note: note || null })
    toast.success(action === 'resolve' ? '已处理' : '已驳回')
    await load()
  } catch (e) { toast.error(e.message) }
}

async function confirmOrder(item) {
  const o = item.raw
  if (!(await dialog.confirm(`确认收到 ¥${o.priceYuan}？将为「${o.nickname}」开通「${o.packageName}」。`))) return
  try {
    const { data } = await api.post(`/admin/recharge/orders/${o.id}/confirm`, { reason: 'queue_confirm' })
    toast.success(data.message || '已确认并开通')
    await load()
  } catch (e) { toast.error(e.message) }
}

async function cancelOrder(item) {
  if (!(await dialog.confirm('取消该开通申请？', { danger: true, confirmText: '取消' }))) return
  try {
    await api.post(`/admin/recharge/orders/${item.id}/cancel`)
    toast.success('已取消')
    await load()
  } catch (e) { toast.error(e.message) }
}

onMounted(load)
</script>

<style scoped>
.summary-row {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
  margin-bottom: 16px;
}
.summary-card {
  background: #fff;
  border: 1px solid rgba(20, 32, 51, 0.08);
  border-radius: 12px;
  padding: 14px 16px;
}
.summary-card.hot {
  border-color: rgba(225, 29, 72, 0.25);
  background: rgba(225, 29, 72, 0.04);
}
.summary-num {
  font-size: 28px;
  font-weight: 800;
  color: #142033;
  line-height: 1;
}
.summary-label {
  margin-top: 6px;
  font-size: 13px;
  color: #7a869c;
}
.type-tag {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 700;
}
.type-tag.thread { background: rgba(13, 148, 136, 0.12); color: #0f766e; }
.type-tag.report { background: rgba(225, 29, 72, 0.12); color: #be123c; }
.type-tag.recharge { background: rgba(232, 165, 75, 0.2); color: #a16207; }
.item-title { font-weight: 600; font-size: 13px; color: #142033; }
.item-meta { font-size: 12px; color: #94a3b8; margin-top: 2px; }
.overdue { color: #e11d48; font-weight: 700; }
.ops { display: flex; flex-wrap: wrap; gap: 4px; }
@media (max-width: 900px) {
  .summary-row { grid-template-columns: 1fr; }
}
</style>
