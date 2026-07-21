<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 幸运转盘
    </div>

    <div class="lottery">
      <div class="lottery-main panel">
        <div class="panel-header">
          <div><span class="accent"></span>幸运转盘</div>
          <span v-if="status?.freeAvailable" class="tag-free">今日首抽免费</span>
        </div>

        <div class="main-body">
          <div v-if="!auth.isLoggedIn" class="gate">
            <p class="text-muted">请先登录后再参与抽奖</p>
            <button type="button" class="btn btn-forum" @click="authModal.openLogin()">前往登录</button>
          </div>

          <template v-else>
            <div class="stats">
              <div class="stat">
                <span class="stat-label">我的金币</span>
                <span class="stat-num">{{ status?.coins ?? auth.user.coins }}</span>
              </div>
              <div class="stat">
                <span class="stat-label">我的积分</span>
                <span class="stat-num">{{ status?.points ?? auth.user.points }}</span>
              </div>
              <div class="stat">
                <span class="stat-label">今日次数</span>
                <span class="stat-num">{{ status?.spinsToday ?? 0 }}<small>/{{ status?.dailyLimit ?? 10 }}</small></span>
              </div>
              <div class="stat">
                <span class="stat-label">本次消耗</span>
                <span class="stat-num cost" :class="{ free: status?.freeAvailable || status?.useTicketNext }">
                  {{ status?.freeAvailable ? '免费' : status?.useTicketNext ? '转盘券' : (status?.costCoins ?? 5) + ' 金' }}
                </span>
              </div>
            </div>

            <div class="wheel-area">
              <div class="pointer"></div>
              <div class="wheel-frame">
                <div
                  class="wheel"
                  :style="{
                    background: wheelGradient,
                    transform: `rotate(${rotation}deg)`,
                    transition: spinning ? `transform ${spinDuration}ms cubic-bezier(0.12, 0.75, 0.15, 1)` : 'none',
                  }"
                >
                  <div
                    v-for="(p, i) in prizes"
                    :key="p.id"
                    class="seg"
                    :class="segClass(p)"
                    :style="labelStyle(i)"
                  >
                    <template v-if="p.id === 'jackpot'">
                      <b>大奖</b>
                      <span>金+分</span>
                    </template>
                    <template v-else-if="p.coins && p.points">
                      <b>+{{ p.coins }}/{{ p.points }}</b>
                      <span>金/分</span>
                    </template>
                    <template v-else-if="p.coins">
                      <b>+{{ p.coins }}</b>
                      <span>金币</span>
                    </template>
                    <template v-else-if="p.points">
                      <b>+{{ p.points }}</b>
                      <span>积分</span>
                    </template>
                    <template v-else>
                      <b>谢谢</b>
                      <span>参与</span>
                    </template>
                  </div>
                </div>
                <div class="hub" aria-hidden="true"></div>
              </div>
            </div>

            <button
              class="spin-btn"
              type="button"
              :disabled="!canSpin || spinning"
              @click="doSpin"
            >
              <template v-if="spinning">抽奖中…</template>
              <template v-else-if="status?.freeAvailable">免费抽一次</template>
              <template v-else-if="blockReason">{{ blockReason }}</template>
              <template v-else>花费 {{ status?.costCoins ?? 5 }} 金币抽一次</template>
            </button>

            <p v-if="blockReason && !spinning" class="hint">{{ blockHint }}</p>

            <div v-if="lastResult" class="result" :class="{ win: isWin(lastResult) }">
              <strong>{{ isWin(lastResult) ? '恭喜中奖' : '未中奖' }}</strong>
              <span>{{ lastResult.prizeLabel }}</span>
              <span class="result-bal">金币 {{ lastResult.coins }} · 积分 {{ lastResult.points }}</span>
            </div>
            <p v-if="error" class="err">{{ error }}</p>
          </template>
        </div>
      </div>

      <aside class="lottery-side">
        <div class="panel">
          <div class="panel-header"><span class="accent"></span>玩法规则</div>
          <ul class="rules">
            <li>每日<strong>首抽免费</strong></li>
            <li>之后每次 <strong>{{ config?.costCoins ?? 5 }}</strong> 金币</li>
            <li>每日最多 <strong>{{ config?.dailyLimit ?? 10 }}</strong> 次</li>
            <li>连续 {{ config?.pityThreshold ?? 10 }} 次未中触发保底</li>
            <li>奖池含<strong>金币</strong>与<strong>积分</strong>，积分可升级</li>
          </ul>
        </div>

        <div class="panel">
          <div class="panel-header"><span class="accent"></span>奖池概率</div>
          <ul class="odds">
            <li v-for="p in prizes" :key="'o' + p.id">
              <i :style="{ background: p.color }"></i>
              <span>{{ p.label }}</span>
              <em>{{ probPct(p.weight) }}%</em>
            </li>
          </ul>
        </div>

        <div class="panel">
          <div class="panel-header"><span class="accent"></span>近期中奖</div>
          <div class="feed">
            <div v-if="!recent.length" class="text-muted empty">暂无记录</div>
            <div v-for="(r, i) in recent" :key="i" class="feed-row">
              <span class="fn">{{ r.nickname }}</span>
              <span class="fp">{{ r.prizeLabel }}</span>
              <span class="ft">{{ timeAgo(r.createdAt) }}</span>
            </div>
          </div>
        </div>
      </aside>
    </div>
  </AppLayout>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import AppLayout from '../components/AppLayout.vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import api from '../api/http'
import { timeAgo } from '../utils/time.js'

const COLORS = {
  miss: '#cbd5e1',
  p5: '#a5b4fc',
  c3: '#7dd3fc',
  p10: '#c4b5fd',
  c5: '#6ee7b7',
  p20: '#818cf8',
  c8: '#fde047',
  p30: '#6366f1',
  c15: '#fdba74',
  c50: '#fda4af',
  p80: '#4f46e5',
  jackpot: '#f43f5e',
}

const auth = useAuthStore()
const authModal = useAuthModalStore()
const config = ref(null)
const status = ref(null)
const recent = ref([])
const prizes = ref([])
const spinning = ref(false)
const rotation = ref(0)
const spinDuration = 4200
const lastResult = ref(null)
const error = ref('')

const totalWeight = computed(() => prizes.value.reduce((s, p) => s + p.weight, 0) || 1)

const wheelGradient = computed(() => {
  if (!prizes.value.length) return '#e2e8f0'
  const n = prizes.value.length
  const step = 360 / n
  return `conic-gradient(from 0deg, ${prizes.value.map((p, i) =>
    `${p.color} ${i * step}deg ${(i + 1) * step}deg`
  ).join(', ')})`
})

const canSpin = computed(() => {
  if (!auth.isLoggedIn || !status.value) return false
  if (status.value.isMuted) return false
  if (status.value.remainingSpins <= 0) return false
  if (status.value.freeAvailable || status.value.useTicketNext) return true
  if (status.value.coins < status.value.costCoins) return false
  return true
})

const blockReason = computed(() => {
  if (!status.value) return ''
  if (status.value.isMuted) return '账号已禁言'
  if (status.value.remainingSpins <= 0) return '今日次数已用完'
  if (!status.value.freeAvailable && !status.value.useTicketNext && status.value.coins < status.value.costCoins) {
    return '金币不足'
  }
  return ''
})

const blockHint = computed(() => {
  if (!status.value) return ''
  if (status.value.remainingSpins <= 0) return '明天 0 点刷新次数，记得回来领免费抽'
  if (!status.value.freeAvailable && !status.value.useTicketNext && status.value.coins < status.value.costCoins) {
    return `还差 ${status.value.costCoins - status.value.coins} 金币，可先去签到或商城买转盘券`
  }
  if (status.value.isMuted) return '解除禁言后可继续抽奖'
  return ''
})

function isWin(r) {
  return (r?.prizeCoins || 0) > 0 || (r?.prizePoints || 0) > 0
}

function segClass(p) {
  if (p.id === 'jackpot') return 'seg-jackpot'
  if (p.points && !p.coins) return 'seg-points'
  if (p.coins && !p.points) return 'seg-coins'
  return ''
}

function probPct(w) {
  return ((w / totalWeight.value) * 100).toFixed(1)
}

function mod360(d) {
  return ((d % 360) + 360) % 360
}

function labelStyle(i) {
  const n = prizes.value.length || 1
  const step = 360 / n
  const angle = i * step + step / 2
  const radius = n > 10 ? 86 : n > 8 ? 90 : 100
  return { transform: `rotate(${angle}deg) translateY(-${radius}px) rotate(${-angle}deg)` }
}

async function loadConfig() {
  const { data } = await api.get('/lottery/config')
  config.value = data
  prizes.value = (data.prizes || []).map(p => ({
    ...p,
    coins: p.coins || 0,
    points: p.points || 0,
    color: COLORS[p.id] || p.color,
  }))
}

async function loadStatus() {
  if (!auth.isLoggedIn) { status.value = null; return }
  const { data } = await api.get('/lottery/status')
  status.value = data
  if (auth.user) auth.setUser({ ...auth.user, coins: data.coins, points: data.points })
}

async function loadRecent() {
  const { data } = await api.get('/lottery/recent', { params: { take: 12 } })
  recent.value = data
}

function prizeIndex(prizeId) {
  const i = prizes.value.findIndex(p => p.id === prizeId)
  return i >= 0 ? i : 0
}

async function doSpin() {
  if (!canSpin.value || spinning.value) return
  error.value = ''
  lastResult.value = null
  spinning.value = true
  try {
    const { data } = await api.post('/lottery/spin')
    const n = prizes.value.length || 1
    const step = 360 / n
    const idx = prizeIndex(data.prizeId)
    // 停在扇区正中，轻微抖动但不压线
    const jitter = (Math.random() - 0.5) * step * 0.3
    const targetMod = mod360(360 - (idx * step + step / 2 + jitter))
    const base = mod360(rotation.value)
    let delta = mod360(targetMod - base)
    if (delta < 40) delta += 360
    rotation.value = rotation.value + 5 * 360 + delta
    await new Promise(r => setTimeout(r, spinDuration + 80))
    lastResult.value = data
    status.value = {
      ...status.value,
      coins: data.coins,
      points: data.points,
      spinsToday: data.spinsToday,
      freeAvailable: data.freeAvailable,
      remainingSpins: data.remainingSpins,
      lotteryTickets: data.lotteryTickets,
      useTicketNext: !data.freeAvailable && data.lotteryTickets > 0,
    }
    if (auth.user) {
      auth.setUser({
        ...auth.user,
        coins: data.coins,
        points: data.points,
        lotteryTickets: data.lotteryTickets,
      })
      await auth.fetchMe().catch(() => {})
    }
    await loadRecent()
  } catch (e) {
    error.value = e.message
    await loadStatus()
  } finally {
    spinning.value = false
  }
}

onMounted(async () => {
  try {
    await loadConfig()
    await Promise.all([loadStatus(), loadRecent()])
  } catch (e) {
    error.value = e.message
  }
})
</script>

<style scoped>
.lottery {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 280px;
  gap: 16px;
  align-items: start;
}
@media (max-width: 900px) {
  .lottery { grid-template-columns: 1fr; }
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.tag-free {
  font-size: 12px;
  font-weight: 600;
  color: #0f766e;
  background: rgba(13, 148, 136, 0.1);
  padding: 2px 10px;
  border-radius: 999px;
}

.main-body { padding: 20px 16px 24px; }

.stats {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 8px;
  max-width: 480px;
  margin: 0 auto 20px;
}
@media (max-width: 520px) {
  .stats { grid-template-columns: repeat(2, 1fr); }
}
.stat {
  text-align: center;
  padding: 10px 6px;
  border: 1px solid #e8ecef;
  border-radius: 10px;
  background: #fafbfc;
}
.stat-label {
  display: block;
  font-size: 11px;
  color: #94a3b8;
  margin-bottom: 4px;
}
.stat-num {
  font-size: 18px;
  font-weight: 700;
  color: #0f172a;
}
.stat-num small {
  font-size: 12px;
  font-weight: 500;
  color: #94a3b8;
}
.stat-num.cost { color: #b45309; font-size: 16px; }
.stat-num.cost.free { color: #0f766e; }

.wheel-area {
  position: relative;
  width: 300px;
  height: 300px;
  margin: 0 auto 20px;
}
.pointer {
  position: absolute;
  top: -2px;
  left: 50%;
  z-index: 3;
  transform: translateX(-50%);
  width: 0;
  height: 0;
  border-left: 11px solid transparent;
  border-right: 11px solid transparent;
  border-top: 20px solid #e11d48;
  filter: drop-shadow(0 2px 2px rgba(0,0,0,0.15));
}
.wheel-frame {
  position: absolute;
  inset: 8px;
  border-radius: 50%;
  padding: 6px;
  background: #0f172a;
  box-shadow: 0 8px 24px rgba(15, 23, 42, 0.12);
}
.wheel {
  width: 100%;
  height: 100%;
  border-radius: 50%;
  position: relative;
  border: 2px solid rgba(255,255,255,0.5);
}
.seg {
  position: absolute;
  left: 50%;
  top: 50%;
  width: 52px;
  margin-left: -26px;
  margin-top: -14px;
  text-align: center;
  pointer-events: none;
  color: #0f172a;
  line-height: 1.05;
}
.seg b {
  display: block;
  font-size: 13px;
  font-weight: 800;
}
.seg span {
  font-size: 10px;
  font-weight: 600;
}
.seg-points { color: #312e81; }
.seg-jackpot { color: #881337; }
.seg-jackpot b { font-size: 12px; }
.hub {
  position: absolute;
  left: 50%;
  top: 50%;
  width: 48px;
  height: 48px;
  margin: -24px 0 0 -24px;
  border-radius: 50%;
  background: #0f172a;
  border: 3px solid #fff;
  box-shadow: 0 2px 8px rgba(0,0,0,0.15);
  z-index: 2;
  pointer-events: none;
}

.spin-btn {
  display: block;
  width: 100%;
  max-width: 320px;
  margin: 0 auto;
  padding: 12px 16px;
  border: none;
  border-radius: 10px;
  font-size: 15px;
  font-weight: 700;
  color: #fff;
  background: #0d9488;
  cursor: pointer;
}
.spin-btn:hover:not(:disabled) { background: #0f766e; }
.spin-btn:disabled {
  background: #cbd5e1;
  color: #64748b;
  cursor: not-allowed;
}

.hint {
  text-align: center;
  margin: 10px 0 0;
  font-size: 12px;
  color: #94a3b8;
}
.result {
  max-width: 360px;
  margin: 16px auto 0;
  padding: 12px 14px;
  border-radius: 10px;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  text-align: center;
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 13px;
  color: #475569;
}
.result.win {
  background: rgba(13, 148, 136, 0.08);
  border-color: rgba(13, 148, 136, 0.25);
  color: #0f766e;
}
.result strong { font-size: 15px; }
.result-bal { font-size: 12px; opacity: 0.85; }
.err {
  text-align: center;
  margin-top: 10px;
  font-size: 13px;
  color: #dc2626;
}
.gate {
  text-align: center;
  padding: 48px 16px;
}

.lottery-side {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.rules, .odds {
  list-style: none;
  margin: 0;
  padding: 12px 16px 16px;
  font-size: 13px;
  color: #475569;
  line-height: 1.7;
}
.odds li {
  display: grid;
  grid-template-columns: 10px 1fr auto;
  gap: 8px;
  align-items: center;
  padding: 5px 0;
  border-bottom: 1px solid #f1f5f9;
}
.odds li:last-child { border-bottom: none; }
.odds i {
  width: 10px;
  height: 10px;
  border-radius: 50%;
}
.odds em {
  font-style: normal;
  font-weight: 700;
  font-size: 12px;
  color: #0f172a;
}
.feed { padding: 4px 16px 12px; }
.empty { font-size: 13px; padding: 8px 0; }
.feed-row {
  display: grid;
  grid-template-columns: 1fr auto auto;
  gap: 8px;
  font-size: 12px;
  padding: 7px 0;
  border-bottom: 1px solid #f1f5f9;
}
.fn {
  font-weight: 600;
  color: #0f172a;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.fp { color: #0d9488; font-weight: 700; }
.ft { color: #94a3b8; }
</style>
