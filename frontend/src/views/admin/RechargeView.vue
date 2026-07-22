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
        <span>卡密生成</span>
      </div>
      <div class="p-3 d-flex gap-2 flex-wrap align-items-end">
        <div>
          <label class="form-label" style="font-size:12px">套餐</label>
          <select v-model.number="genPackageId" class="form-control form-control-sm" style="min-width:180px">
            <option v-for="p in packages" :key="p.id" :value="p.id">{{ p.name }} · ¥{{ p.priceYuan }}</option>
          </select>
        </div>
        <div>
          <label class="form-label" style="font-size:12px">数量</label>
          <input v-model.number="genCount" type="number" min="1" max="100" class="form-control form-control-sm" style="width:80px" />
        </div>
        <button class="admin-btn admin-btn-primary" :disabled="generating || !genPackageId" @click="generateCards">
          {{ generating ? '生成中...' : '生成卡密' }}
        </button>
      </div>
      <div v-if="generatedCodes.length" class="p-3 pt-0">
        <div class="text-muted mb-2" style="font-size:12px">刚生成 {{ generatedCodes.length }} 张（点击复制）</div>
        <div class="code-list">
          <code v-for="c in generatedCodes" :key="c" class="card-code" @click="copyOne(c)">{{ c }}</code>
        </div>
        <button class="admin-btn admin-btn-outline mt-2" @click="copyAll">全部复制</button>
      </div>
    </div>

    <div class="admin-panel mb-3">
      <div class="admin-panel-hd">
        <span>卡密库存</span>
        <div>
          <button class="admin-btn admin-btn-outline me-1" :class="{ active: cardUsedFilter === '' }" @click="cardUsedFilter=''; loadCards(1)">全部</button>
          <button class="admin-btn admin-btn-outline me-1" :class="{ active: cardUsedFilter === '0' }" @click="cardUsedFilter='0'; loadCards(1)">未使用</button>
          <button class="admin-btn admin-btn-outline" :class="{ active: cardUsedFilter === '1' }" @click="cardUsedFilter='1'; loadCards(1)">已使用</button>
        </div>
      </div>
      <div class="table-responsive">
        <table class="table table-sm mb-0">
          <thead>
            <tr>
              <th>ID</th>
              <th>卡密</th>
              <th>套餐</th>
              <th>状态</th>
              <th>使用者</th>
              <th>创建时间</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="c in cards" :key="c.id">
              <td>{{ c.id }}</td>
              <td>
                <code class="card-code" @click="copyOne(c.code)" title="点击复制">{{ shortCode(c.code) }}</code>
              </td>
              <td>{{ c.packageName }}</td>
              <td>
                <span :class="c.used ? 'text-muted' : 'text-success'">{{ c.used ? '已使用' : '未使用' }}</span>
              </td>
              <td>{{ c.usedByNickname || '-' }}</td>
              <td style="font-size:12px">{{ formatTime(c.createdAt) }}</td>
              <td>
                <button class="admin-btn admin-btn-outline" @click="copyOne(c.code)">复制</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div v-if="!cards.length" class="p-3 text-muted">暂无卡密</div>
      <PaginationComp v-model="cardPage" :total-pages="cardTotalPages" />
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
import { computed, onMounted, ref, watch } from 'vue'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'
import PaginationComp from '../../components/PaginationComp.vue'

const toast = useToastStore()

const orders = ref([])
const packages = ref([])
const statusFilter = ref('pending')
const lastCard = ref('')
const lastMsg = ref('')
const genPackageId = ref(0)
const genCount = ref(10)
const generating = ref(false)
const generatedCodes = ref([])

const cards = ref([])
const cardPage = ref(1)
const cardTotal = ref(0)
const cardPageSize = 20
const cardUsedFilter = ref('0')
const cardTotalPages = computed(() => Math.max(1, Math.ceil(cardTotal.value / cardPageSize)))

async function loadCards(p) {
  if (p) cardPage.value = p
  try {
    const used = cardUsedFilter.value === '' ? undefined : cardUsedFilter.value === '1'
    const { data } = await api.get('/admin/recharge/cards', {
      params: { page: cardPage.value, pageSize: cardPageSize, used },
    })
    cards.value = data.items || []
    cardTotal.value = data.total || 0
  } catch {
    cards.value = []
    cardTotal.value = 0
  }
}

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

async function copyAll() {
  try {
    await navigator.clipboard.writeText(generatedCodes.value.join('\n'))
    toast.success('已全部复制')
  } catch { toast.error('复制失败') }
}

async function loadPackages() {
  const { data } = await api.get('/admin/recharge/packages')
  packages.value = data || []
  if (packages.value.length && !genPackageId.value) genPackageId.value = packages.value[0].id
}

async function generateCards() {
  generating.value = true
  try {
    const { data } = await api.post('/admin/recharge/cards/generate', {
      packageId: genPackageId.value,
      count: genCount.value,
    })
    generatedCodes.value = (data || []).map(c => c.code || c)
    toast.success(`已生成 ${generatedCodes.value.length} 张卡密`)
    await loadCards(1)
  } catch (e) { toast.error(e.message) }
  finally { generating.value = false }
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

onMounted(async () => {
  await Promise.all([loadPackages(), loadOrders(), loadCards(1)])
})
watch(cardPage, () => loadCards())
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
.code-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
  max-height: 200px;
  overflow: auto;
}
.card-code.full { display: block; padding: 10px; font-size: 13px; }
.card-code:hover { background: #e4e7ec; }
.admin-btn.active { background: #12b76a; color: #fff; border-color: #12b76a; }
</style>
