<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">会员开通申请</h2>
      <button class="admin-btn admin-btn-outline" @click="loadOrders">刷新</button>
    </div>

    <div class="admin-panel mb-3 p-3 text-muted" style="font-size:13px">
      流程：用户前台提交开通申请 → 本页出现一条记录 → 确认收款后点「确认到账」→
      系统生成卡密并<strong>自动兑换开通会员</strong>。用户可在开通页「我的申请」查看卡密留存。
    </div>

    <div class="admin-panel mb-3">
      <div class="admin-panel-hd">
        <span>申请列表</span>
        <div>
          <button class="admin-btn admin-btn-outline me-1" :class="{ active: statusFilter === 'pending' }" @click="statusFilter='pending'; loadOrders()">待处理</button>
          <button class="admin-btn admin-btn-outline me-1" :class="{ active: statusFilter === 'paid' }" @click="statusFilter='paid'; loadOrders()">已开通</button>
          <button class="admin-btn admin-btn-outline" :class="{ active: !statusFilter }" @click="statusFilter=''; loadOrders()">全部</button>
        </div>
      </div>
      <div class="table-responsive">
        <table class="table table-sm mb-0">
          <thead>
            <tr>
              <th>申请号</th>
              <th>用户</th>
              <th>账号</th>
              <th>套餐</th>
              <th>金额</th>
              <th>状态</th>
              <th>卡密</th>
              <th>时间</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="o in orders" :key="o.id">
              <td>#{{ o.id }}</td>
              <td>
                <strong>{{ o.nickname || '-' }}</strong>
                <span class="text-muted"> #{{ o.userId }}</span>
              </td>
              <td>{{ o.username || '-' }}</td>
              <td>{{ o.packageName }}</td>
              <td>¥{{ o.priceYuan }}</td>
              <td>{{ statusText(o.status) }}</td>
              <td style="max-width:220px">
                <code
                  v-if="o.cardCode"
                  class="card-code"
                  @click="copyOne(o.cardCode)"
                  title="点击复制（用户侧也可查看）"
                >{{ shortCode(o.cardCode) }}</code>
                <span v-else class="text-muted">-</span>
              </td>
              <td style="font-size:12px">{{ formatTime(o.paidAt || o.createdAt) }}</td>
              <td>
                <button
                  v-if="o.status === 'pending'"
                  class="admin-btn admin-btn-primary me-1"
                  @click="confirmOrder(o)"
                >确认到账</button>
                <button
                  v-if="o.status === 'pending'"
                  class="admin-btn admin-btn-outline me-1"
                  @click="cancelOrder(o.id)"
                >取消</button>
                <button
                  v-if="o.cardCode"
                  class="admin-btn admin-btn-outline"
                  @click="copyOne(o.cardCode)"
                >复制卡密</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div v-if="!orders.length" class="p-3 text-muted">
        {{ statusFilter === 'pending' ? '暂无待处理申请' : '暂无记录' }}
      </div>
    </div>

    <div v-if="lastMsg" class="admin-panel p-3">
      <strong>{{ lastMsg }}</strong>
      <div v-if="lastCard" class="mt-2 text-muted" style="font-size:13px">
        卡密已自动兑换到该用户，会员已开通。后台仅作核对：
      </div>
      <div v-if="lastCard" class="mt-2">
        <code class="card-code full" @click="copyOne(lastCard)">{{ lastCard }}</code>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'

const toast = useToastStore()

const orders = ref([])
const statusFilter = ref('pending')
const lastCard = ref('')
const lastMsg = ref('')

function formatTime(iso) {
  if (!iso) return '-'
  try { return new Date(iso).toLocaleString() } catch { return iso }
}

function statusText(s) {
  return ({ pending: '待处理', paid: '已开通', cancelled: '已取消' })[s] || s
}

function shortCode(code) {
  if (!code || code.length <= 16) return code
  return code.slice(0, 8) + '…' + code.slice(-8)
}

async function copyOne(code) {
  try {
    await navigator.clipboard.writeText(code)
    toast.success('卡密已复制')
  } catch {
    prompt('请手动复制卡密', code)
  }
}

async function loadOrders() {
  const { data } = await api.get('/admin/recharge/orders', {
    params: { page: 1, pageSize: 50, status: statusFilter.value || undefined }
  })
  orders.value = data?.items || []
}

async function confirmOrder(o) {
  if (!confirm(`确认收到 ¥${o.priceYuan}？\n将为「${o.nickname}」生成卡密并自动兑换「${o.packageName}」。`)) return
  try {
    const { data } = await api.post(`/admin/recharge/orders/${o.id}/confirm`, { reason: 'manual_confirm' })
    lastCard.value = data.cardCode || data.order?.cardCode || ''
    lastMsg.value = data.message || '已生成卡密并自动兑换'
    await loadOrders()
    toast.success(lastMsg.value)
  } catch (e) { toast.error(e.message) }
}

async function cancelOrder(id) {
  if (!confirm('取消该申请？')) return
  try {
    await api.post(`/admin/recharge/orders/${id}/cancel`)
    await loadOrders()
  } catch (e) { toast.error(e.message) }
}

onMounted(loadOrders)
</script>

<style scoped>

.card-code {
  background: #f2f4f7;
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 12px;
  cursor: pointer;
  word-break: break-all;
}
.card-code.full { display: block; padding: 10px; font-size: 13px; }
.card-code:hover { background: #e4e7ec; }
.admin-btn.active { background: #12b76a; color: #fff; border-color: #12b76a; }
</style>
