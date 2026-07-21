<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 开通会员
    </div>

    <div class="panel mb-3">
      <div class="panel-header"><span class="accent"></span>开通会员</div>
      <div class="recharge-intro">
        <p>选择会员档位提交申请 → 按提示转账并备注申请号 → 站长确认到账后系统会<strong>生成卡密并自动兑换开通会员</strong>，卡密会出现在下方「我的申请」中供留存。</p>
        <div v-if="auth.isLoggedIn" class="vip-status">
          当前状态：
          <strong v-if="auth.user.isVip">
            {{ auth.user.vipTierName || 'VIP' }}
            <template v-if="auth.user.vipUntil">至 {{ formatTime(auth.user.vipUntil) }}</template>
            <template v-else>（永久）</template>
          </strong>
          <strong v-else class="text-muted">未开通</strong>
          · 金币 {{ auth.user.coins }}
        </div>
      </div>
    </div>

    <div v-if="!auth.isLoggedIn" class="auth-card" style="max-width:420px;margin:24px auto;">
      <h4 class="mb-2">请先登录</h4>
      <button type="button" class="btn btn-forum w-100" @click="authModal.openLogin()">登录后开通</button>
    </div>

    <template v-else>
      <div class="pkg-grid mb-4">
        <div
          v-for="p in packages"
          :key="p.id"
          class="pkg-card"
          :class="{ featured: p.isLifetime, selected: selectedId === p.id }"
          @click="selectedId = p.id"
        >
          <div class="pkg-badge" v-if="p.isLifetime">推荐</div>
          <div class="pkg-name">{{ p.name }}</div>
          <div class="pkg-duration">{{ p.durationLabel }}</div>
          <div class="pkg-price"><span class="yen">¥</span>{{ formatPrice(p.priceYuan) }}</div>
          <div class="pkg-bonus" v-if="p.bonusCoins">赠 {{ p.bonusCoins }} 金币</div>
          <div class="pkg-desc">{{ p.description }}</div>
          <button
            type="button"
            class="btn btn-forum btn-sm w-100 mt-2"
            :disabled="ordering"
            @click.stop="createOrder(p)"
          >提交开通申请</button>
        </div>
      </div>

      <div v-if="lastPaidWithCard" class="panel mb-3 order-tip paid-tip">
        <div class="panel-header"><span class="accent"></span>已开通 · 申请 #{{ lastPaidWithCard.id }}</div>
        <div class="p-3">
          <p>套餐：<strong>{{ lastPaidWithCard.packageName }}</strong> · ¥{{ formatPrice(lastPaidWithCard.priceYuan) }}</p>
          <p class="mb-2" style="font-size:13px;color:#027a48">卡密已自动兑换，会员已到账。下方卡密仅供留存：</p>
          <div class="card-code-box">
            <code>{{ lastPaidWithCard.cardCode }}</code>
            <button type="button" class="btn btn-sm btn-forum" @click="copyCode(lastPaidWithCard.cardCode)">复制卡密</button>
          </div>
        </div>
      </div>

      <div v-if="lastOrder" class="panel mb-3 order-tip">
        <div class="panel-header"><span class="accent"></span>待处理申请 #{{ lastOrder.id }}</div>
        <div class="p-3">
          <p>套餐：<strong>{{ lastOrder.packageName }}</strong> · ¥{{ formatPrice(lastOrder.priceYuan) }}</p>
          <p class="text-muted" style="font-size:13px">
            请转账至站长公布的收款账户，备注填写：<code>VIP{{ lastOrder.id }}</code>。
            到账后站长确认，系统会生成卡密并自动兑换开通会员。
          </p>
          <button type="button" class="btn btn-sm btn-outline-secondary" @click="cancelOrder(lastOrder.id)">取消申请</button>
        </div>
      </div>

      <div class="panel mb-3">
        <div class="panel-header">
          <span><span class="accent"></span>我的申请</span>
          <button type="button" class="btn btn-sm btn-outline-secondary" @click="loadOrders">刷新</button>
        </div>
        <div v-if="!orders.length" class="p-3 text-muted">暂无申请</div>
        <div v-else class="order-list">
          <div v-for="o in orders" :key="o.id" class="order-row" :class="{ paid: o.status === 'paid' }">
            <div class="order-main">
              <div class="order-title">
                #{{ o.id }} · {{ o.packageName }}
                <span :class="statusClass(o.status)">{{ statusText(o.status) }}</span>
              </div>
              <div class="order-meta">
                ¥{{ formatPrice(o.priceYuan) }} · {{ formatTime(o.paidAt || o.createdAt) }}
              </div>
            </div>
            <div v-if="o.cardCode" class="order-card">
              <div class="order-card-label">卡密（已自动兑换）</div>
              <code class="order-card-code">{{ o.cardCode }}</code>
              <button type="button" class="btn btn-sm btn-outline-secondary" @click="copyCode(o.cardCode)">复制</button>
            </div>
          </div>
        </div>
      </div>
    </template>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import AppLayout from '../components/AppLayout.vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { useToastStore } from '../stores/toast'
import api from '../api/http'

const auth = useAuthStore()
const authModal = useAuthModalStore()
const toast = useToastStore()
const packages = ref([])
const orders = ref([])
const selectedId = ref(null)
const lastOrder = ref(null)
const ordering = ref(false)

const lastPaidWithCard = computed(() =>
  orders.value.find((o) => o.status === 'paid' && o.cardCode) || null
)

function formatPrice(v) {
  const n = Number(v)
  return Number.isFinite(n) ? (Math.round(n * 100) / 100).toString() : v
}

function formatTime(iso) {
  if (!iso) return '-'
  try { return new Date(iso).toLocaleString() } catch { return iso }
}

function statusText(s) {
  return ({ pending: '待确认', paid: '已开通', cancelled: '已取消' })[s] || s
}

function statusClass(s) {
  return ({ pending: 'st-pending', paid: 'st-paid', cancelled: 'st-cancel' })[s] || ''
}

async function copyCode(code) {
  if (!code) return
  try {
    await navigator.clipboard.writeText(code)
    toast.success('卡密已复制')
  } catch {
    prompt('请手动复制卡密', code)
  }
}

async function loadPackages() {
  const { data } = await api.get('/recharge/packages')
  packages.value = data || []
  if (!selectedId.value && packages.value.length) selectedId.value = packages.value[0].id
}

async function loadOrders() {
  if (!auth.isLoggedIn) return
  const { data } = await api.get('/recharge/orders/mine')
  orders.value = data || []
  lastOrder.value = orders.value.find((o) => o.status === 'pending') || null
  if (orders.value.some((o) => o.status === 'paid')) {
    try { await auth.fetchMe() } catch { /* ignore */ }
  }
}

async function createOrder(pkg) {
  ordering.value = true
  try {
    const { data } = await api.post('/recharge/orders', { packageId: pkg.id })
    lastOrder.value = data
    await loadOrders()
    toast.info(`已提交申请 #${data.id}，请转账并备注 VIP${data.id}`)
  } catch (e) {
    toast.error(e.message)
  } finally {
    ordering.value = false
  }
}

async function cancelOrder(id) {
  if (!confirm('确定取消该申请？')) return
  try {
    await api.post(`/recharge/orders/${id}/cancel`)
    await loadOrders()
  } catch (e) { toast.error(e.message) }
}

onMounted(async () => {
  try { await loadPackages() } catch { packages.value = [] }
  try { await loadOrders() } catch { orders.value = [] }
})
</script>

<style scoped>
.recharge-intro { padding: 12px 16px; font-size: 14px; color: #475467; }
.vip-status { margin-top: 8px; font-size: 13px; }
.pkg-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 14px;
}
.pkg-card {
  position: relative;
  background: #fff;
  border: 1px solid #e4e7ec;
  border-radius: 12px;
  padding: 16px;
  cursor: pointer;
  transition: border-color .15s, box-shadow .15s;
}
.pkg-card:hover, .pkg-card.selected { border-color: #12b76a; box-shadow: 0 6px 18px rgba(18,183,106,.12); }
.pkg-card.featured { border-color: #f79009; }
.pkg-badge {
  position: absolute; top: 10px; right: 10px;
  background: #f79009; color: #fff; font-size: 11px;
  padding: 2px 8px; border-radius: 999px;
}
.pkg-name { font-size: 18px; font-weight: 700; color: #101828; }
.pkg-duration { font-size: 12px; color: #667085; margin: 4px 0 10px; }
.pkg-price { font-size: 28px; font-weight: 800; color: #12b76a; line-height: 1; }
.pkg-price .yen { font-size: 16px; margin-right: 2px; }
.pkg-bonus { margin-top: 6px; font-size: 12px; color: #b54708; }
.pkg-desc { margin-top: 10px; font-size: 12px; color: #667085; min-height: 36px; }
.order-tip code { background: #f2f4f7; padding: 2px 6px; border-radius: 4px; }
.paid-tip { border-color: #a6f4c5; }
.card-code-box {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  align-items: flex-start;
}
.card-code-box code {
  flex: 1;
  min-width: 200px;
  display: block;
  background: #f2f4f7;
  padding: 10px 12px;
  border-radius: 8px;
  font-size: 12px;
  word-break: break-all;
  line-height: 1.5;
}
.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.order-list { padding: 4px 0; }
.order-row {
  padding: 12px 16px;
  border-bottom: 1px solid #f2f4f7;
}
.order-row:last-child { border-bottom: none; }
.order-row.paid { background: #f6fef9; }
.order-title { font-weight: 600; color: #101828; display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.order-meta { margin-top: 4px; font-size: 12px; color: #667085; }
.order-card {
  margin-top: 10px;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: flex-start;
}
.order-card-label {
  font-size: 12px;
  font-weight: 600;
  color: #027a48;
  padding-top: 6px;
}
.order-card-code {
  flex: 1;
  min-width: 180px;
  background: #fff;
  border: 1px solid #e4e7ec;
  padding: 8px 10px;
  border-radius: 6px;
  font-size: 11px;
  word-break: break-all;
  line-height: 1.45;
}
.st-pending { color: #b54708; font-weight: 600; font-size: 12px; }
.st-paid { color: #027a48; font-weight: 600; font-size: 12px; }
.st-cancel { color: #98a2b3; font-size: 12px; }
</style>
