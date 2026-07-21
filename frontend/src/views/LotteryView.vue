<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 幸运转盘
    </div>

    <div class="lottery">
      <section class="hero">
        <header class="hero-copy">
          <h1 class="title">幸运转盘</h1>
          <p class="lede">每日首抽免费 · 金币与积分奖池</p>
        </header>

        <div v-if="!auth.isLoggedIn" class="gate">
          <p>登录后即可参与</p>
          <button type="button" class="btn-primary" @click="authModal.openLogin()">登录</button>
        </div>

        <template v-else>
          <p class="status-line">
            <span>金币 <b>{{ status?.coins ?? auth.user.coins }}</b></span>
            <span class="dot" aria-hidden="true"></span>
            <span>积分 <b>{{ status?.points ?? auth.user.points }}</b></span>
            <span class="dot" aria-hidden="true"></span>
            <span>今日 <b>{{ status?.spinsToday ?? 0 }}/{{ status?.dailyLimit ?? 10 }}</b></span>
            <span class="dot" aria-hidden="true"></span>
            <span :class="{ accent: status?.freeAvailable || status?.useTicketNext }">
              本次
              <b>
                {{ status?.freeAvailable ? '免费' : status?.useTicketNext ? '转盘券' : (status?.costCoins ?? 5) + ' 金币' }}
              </b>
            </span>
          </p>

          <div class="wheel-stage" :class="{ spinning }">
            <div class="aura" aria-hidden="true"></div>
            <div class="pointer" aria-hidden="true"></div>
            <div class="rim">
              <div
                class="wheel"
                :style="{
                  background: wheelGradient,
                  transform: `rotate(${rotation}deg)`,
                  transition: spinning ? `transform ${spinDuration}ms cubic-bezier(0.12, 0.8, 0.08, 1)` : 'none',
                }"
              >
                <div
                  v-for="(p, i) in prizes"
                  :key="p.id"
                  class="seg"
                  :class="segClass(p)"
                  :style="labelStyle(i)"
                >
                  <template v-if="p.id === 'jackpot'"><b>大奖</b></template>
                  <template v-else-if="p.coins && p.points"><b>+{{ p.coins }}/{{ p.points }}</b></template>
                  <template v-else-if="p.coins"><b>+{{ p.coins }}</b><i>金</i></template>
                  <template v-else-if="p.points"><b>+{{ p.points }}</b><i>分</i></template>
                  <template v-else><b>谢谢</b></template>
                </div>
              </div>
              <button
                class="hub"
                type="button"
                :disabled="!canSpin || spinning"
                :aria-label="spinning ? '抽奖中' : '开始抽奖'"
                @click="doSpin"
              >
                <span class="hub-text">{{ spinning ? '…' : '抽' }}</span>
              </button>
            </div>
          </div>

          <button
            class="btn-primary"
            type="button"
            :disabled="!canSpin || spinning"
            @click="doSpin"
          >
            <template v-if="spinning">转动中</template>
            <template v-else-if="status?.freeAvailable">免费抽一次</template>
            <template v-else-if="blockReason">{{ blockReason }}</template>
            <template v-else>抽一次 · {{ status?.costCoins ?? 5 }} 金币</template>
          </button>

          <p v-if="blockReason && !spinning" class="hint">{{ blockHint }}</p>

          <Transition name="reveal">
            <div v-if="lastResult" class="reveal" :class="{ win: isWin(lastResult) }">
              <p class="reveal-k">{{ isWin(lastResult) ? '中奖' : '未中' }}</p>
              <p class="reveal-v">{{ lastResult.prizeLabel }}</p>
            </div>
          </Transition>
          <p v-if="error" class="err">{{ error }}</p>
        </template>
      </section>

      <aside class="rail">
        <div class="tabs" role="tablist">
          <button
            v-for="t in tabs"
            :key="t.id"
            type="button"
            role="tab"
            class="tab"
            :class="{ on: sideTab === t.id }"
            :aria-selected="sideTab === t.id"
            @click="sideTab = t.id"
          >{{ t.label }}</button>
        </div>

        <div v-show="sideTab === 'feed'" class="rail-body">
          <div v-if="!recent.length" class="empty">暂无中奖记录</div>
          <ul v-else class="feed">
            <li v-for="(r, i) in recent" :key="i">
              <span class="fn">{{ r.nickname }}</span>
              <span class="fp">{{ r.prizeLabel }}</span>
              <span class="ft">{{ timeAgo(r.createdAt) }}</span>
            </li>
          </ul>
        </div>

        <div v-show="sideTab === 'odds'" class="rail-body">
          <ul class="odds">
            <li v-for="p in prizes" :key="'o' + p.id">
              <i :style="{ background: p.color }"></i>
              <span>{{ p.label }}</span>
              <em>{{ probPct(p.weight) }}%</em>
            </li>
          </ul>
        </div>

        <div v-show="sideTab === 'rules'" class="rail-body rules">
          <p>每日<strong>首抽免费</strong>，之后每次 <strong>{{ config?.costCoins ?? 5 }}</strong> 金币。</p>
          <p>每日上限 <strong>{{ config?.dailyLimit ?? 10 }}</strong> 次；连续 {{ config?.pityThreshold ?? 10 }} 次未中触发保底。</p>
          <p>奖池含金币与积分，积分可用于升级。</p>
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

/** 鲜艳青绿 / 暖金 / 珊瑚，保持可读对比 */
const COLORS = {
  miss: '#dfe8f0',
  p5: '#7ee0d3',
  c3: '#ffd166',
  p10: '#2dd4bf',
  c5: '#ffb020',
  p20: '#14b8a6',
  c8: '#f59e0b',
  p30: '#0d9488',
  c15: '#fb923c',
  c50: '#f97316',
  p80: '#06b6d4',
  jackpot: '#ef4444',
}

const tabs = [
  { id: 'feed', label: '近期' },
  { id: 'odds', label: '奖池' },
  { id: 'rules', label: '规则' },
]

const auth = useAuthStore()
const authModal = useAuthModalStore()
const config = ref(null)
const status = ref(null)
const recent = ref([])
const prizes = ref([])
const spinning = ref(false)
const rotation = ref(0)
const spinDuration = 4400
const lastResult = ref(null)
const error = ref('')
const sideTab = ref('feed')

const totalWeight = computed(() => prizes.value.reduce((s, p) => s + p.weight, 0) || 1)

const wheelGradient = computed(() => {
  if (!prizes.value.length) return '#e2e8f0'
  const n = prizes.value.length
  const step = 360 / n
  return `conic-gradient(from -90deg, ${prizes.value.map((p, i) =>
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
  if (status.value.remainingSpins <= 0) return '明天 0 点刷新次数'
  if (!status.value.freeAvailable && !status.value.useTicketNext && status.value.coins < status.value.costCoins) {
    return `还差 ${status.value.costCoins - status.value.coins} 金币`
  }
  if (status.value.isMuted) return '解除禁言后可继续'
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
  const radius = n > 10 ? 108 : n > 8 ? 114 : 122
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
    const jitter = (Math.random() - 0.5) * step * 0.28
    const targetMod = mod360(360 - (idx * step + step / 2 + jitter))
    const base = mod360(rotation.value)
    let delta = mod360(targetMod - base)
    if (delta < 40) delta += 360
    rotation.value = rotation.value + 6 * 360 + delta
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
  gap: 28px;
  align-items: start;
  min-height: 70vh;
}
@media (max-width: 960px) {
  .lottery { grid-template-columns: 1fr; gap: 20px; min-height: 0; }
}

/* ——— Hero stage ——— */
.hero {
  position: relative;
  padding: clamp(32px, 5vw, 56px) clamp(20px, 4vw, 40px) clamp(40px, 5vw, 64px);
  border-radius: 20px;
  overflow: hidden;
  text-align: center;
  border: 1px solid rgba(13, 148, 136, 0.12);
  background:
    radial-gradient(ellipse 85% 50% at 50% -8%, rgba(45, 212, 191, 0.35), transparent 58%),
    radial-gradient(ellipse 55% 45% at 100% 80%, rgba(251, 146, 60, 0.22), transparent 55%),
    radial-gradient(ellipse 50% 40% at 0% 70%, rgba(6, 182, 212, 0.18), transparent 50%),
    linear-gradient(180deg, #ecfffb 0%, #fffaf3 45%, #fff7ed 100%);
}

.hero-copy {
  margin-bottom: 28px;
  animation: fade-up 0.7s cubic-bezier(0.22, 1, 0.36, 1) both;
}
.title {
  margin: 0;
  font-family: var(--font-display, 'Outfit', sans-serif);
  font-size: clamp(36px, 6vw, 52px);
  font-weight: 700;
  letter-spacing: -0.03em;
  line-height: 1.05;
  background: linear-gradient(120deg, #0f766e 0%, #0891b2 45%, #ea580c 100%);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.lede {
  margin: 10px 0 0;
  font-size: 15px;
  color: #5b6b7c;
  letter-spacing: 0.02em;
}

.status-line {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  align-items: center;
  gap: 8px 0;
  margin: 0 auto 36px;
  max-width: 520px;
  font-size: 13px;
  color: #5b6b7c;
  animation: fade-up 0.7s 0.08s cubic-bezier(0.22, 1, 0.36, 1) both;
}
.status-line b {
  font-family: var(--font-display, 'Outfit', sans-serif);
  font-weight: 700;
  color: #0f172a;
  margin-left: 4px;
}
.status-line .accent b { color: #0d9488; }
.dot {
  width: 4px;
  height: 4px;
  margin: 0 12px;
  border-radius: 50%;
  background: #2dd4bf;
}

/* ——— Wheel ——— */
.wheel-stage {
  position: relative;
  width: min(400px, 88vw);
  height: min(400px, 88vw);
  margin: 0 auto 36px;
  animation: fade-up 0.8s 0.12s cubic-bezier(0.22, 1, 0.36, 1) both;
}
.aura {
  position: absolute;
  inset: 4%;
  border-radius: 50%;
  background:
    radial-gradient(circle, rgba(255, 176, 32, 0.45), transparent 55%),
    radial-gradient(circle at 70% 30%, rgba(45, 212, 191, 0.4), transparent 50%);
  animation: aura 3.2s ease-in-out infinite;
  pointer-events: none;
}
.wheel-stage.spinning .aura {
  animation: aura-fast 0.85s ease-in-out infinite;
}
@keyframes aura {
  0%, 100% { opacity: 0.55; transform: scale(1); }
  50% { opacity: 1; transform: scale(1.06); }
}
@keyframes aura-fast {
  0%, 100% { opacity: 0.7; }
  50% { opacity: 1; }
}

.pointer {
  position: absolute;
  top: 2px;
  left: 50%;
  z-index: 6;
  width: 0;
  height: 0;
  margin-left: -10px;
  border-left: 10px solid transparent;
  border-right: 10px solid transparent;
  border-top: 20px solid #ef4444;
  filter: drop-shadow(0 3px 6px rgba(239, 68, 68, 0.35));
}
.wheel-stage.spinning .pointer {
  animation: nudge 0.32s ease-in-out infinite alternate;
  transform-origin: 50% 0;
}
@keyframes nudge {
  from { transform: rotate(-5deg); }
  to { transform: rotate(5deg); }
}

.rim {
  position: absolute;
  inset: 18px;
  border-radius: 50%;
  padding: 9px;
  background: conic-gradient(
    from 0deg,
    #14b8a6,
    #fbbf24,
    #fb923c,
    #22d3ee,
    #14b8a6
  );
  box-shadow:
    0 20px 44px rgba(13, 148, 136, 0.28),
    0 8px 20px rgba(251, 146, 60, 0.18),
    inset 0 1px 0 rgba(255, 255, 255, 0.45);
}
.wheel {
  width: 100%;
  height: 100%;
  border-radius: 50%;
  position: relative;
  border: 3px solid #fff;
  box-shadow: inset 0 0 0 1px rgba(15, 23, 42, 0.06);
}

.seg {
  position: absolute;
  left: 50%;
  top: 50%;
  width: 52px;
  margin-left: -26px;
  margin-top: -12px;
  text-align: center;
  pointer-events: none;
  color: #0f172a;
  line-height: 1.05;
  text-shadow: 0 1px 0 rgba(255, 255, 255, 0.45);
}
.seg b {
  display: block;
  font-size: 12px;
  font-weight: 800;
  font-family: var(--font-display, 'Outfit', sans-serif);
}
.seg i {
  font-style: normal;
  font-size: 9px;
  font-weight: 700;
  opacity: 0.8;
}
.seg-points { color: #0f766e; }
.seg-coins { color: #b45309; }
.seg-jackpot { color: #b91c1c; }
.seg-jackpot b { font-size: 13px; }

.hub {
  position: absolute;
  left: 50%;
  top: 50%;
  z-index: 4;
  width: 74px;
  height: 74px;
  transform: translate(-50%, -50%);
  border-radius: 50%;
  border: 3px solid #fff;
  background: linear-gradient(145deg, #14b8a6 0%, #0d9488 55%, #0891b2 100%);
  color: #fff;
  cursor: pointer;
  display: grid;
  place-items: center;
  padding: 0;
  box-shadow: 0 10px 28px rgba(13, 148, 136, 0.4);
  transition: transform 0.2s ease, box-shadow 0.2s ease, filter 0.2s ease;
}
.hub:hover:not(:disabled) {
  transform: translate(-50%, -50%) scale(1.06);
  filter: brightness(1.08);
  box-shadow: 0 12px 32px rgba(251, 146, 60, 0.35);
}
.hub:disabled {
  cursor: not-allowed;
  opacity: 0.45;
  filter: grayscale(0.3);
}
.hub-text {
  font-family: var(--font-display, 'Outfit', sans-serif);
  font-size: 20px;
  font-weight: 800;
  letter-spacing: 0.06em;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.15);
}

.btn-primary {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 200px;
  padding: 13px 28px;
  border: none;
  border-radius: 999px;
  font-size: 14px;
  font-weight: 700;
  letter-spacing: 0.02em;
  color: #fff;
  background: linear-gradient(135deg, #14b8a6 0%, #0d9488 50%, #f59e0b 160%);
  box-shadow: 0 10px 24px rgba(13, 148, 136, 0.32);
  cursor: pointer;
  transition: transform 0.15s ease, box-shadow 0.15s ease, filter 0.15s ease;
}
.btn-primary:hover:not(:disabled) {
  filter: brightness(1.06);
  transform: translateY(-1px);
  box-shadow: 0 14px 28px rgba(245, 158, 11, 0.28);
}
.btn-primary:disabled {
  background: #c5ced8;
  box-shadow: none;
  color: #64748b;
  cursor: not-allowed;
  transform: none;
  filter: none;
}

.hint {
  margin: 12px 0 0;
  font-size: 12px;
  color: var(--muted, #7a869c);
}

.reveal {
  margin-top: 28px;
  min-height: 56px;
}
.reveal-k {
  margin: 0;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.14em;
  text-transform: uppercase;
  color: var(--muted, #7a869c);
}
.reveal.win .reveal-k { color: #0d9488; }
.reveal-v {
  margin: 6px 0 0;
  font-family: var(--font-display, 'Outfit', sans-serif);
  font-size: clamp(22px, 3vw, 28px);
  font-weight: 700;
  letter-spacing: -0.02em;
  background: linear-gradient(120deg, #0d9488, #ea580c);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.reveal:not(.win) .reveal-v {
  background: none;
  color: #334155;
  -webkit-background-clip: unset;
  background-clip: unset;
}

.reveal-enter-active {
  animation: fade-up 0.5s cubic-bezier(0.22, 1, 0.36, 1);
}
@keyframes fade-up {
  from { opacity: 0; transform: translateY(14px); }
  to { opacity: 1; transform: none; }
}

.err {
  margin-top: 12px;
  font-size: 13px;
  color: var(--danger, #e35d5d);
}
.gate {
  padding: 64px 16px;
  color: var(--muted, #7a869c);
}
.gate p { margin: 0 0 20px; font-size: 15px; }

/* ——— Side rail ——— */
.rail {
  padding: 8px 4px 8px 8px;
  position: sticky;
  top: 16px;
}
.tabs {
  display: flex;
  gap: 4px;
  margin-bottom: 18px;
  border-bottom: 1px solid rgba(20, 32, 51, 0.08);
}
.tab {
  flex: 1;
  padding: 10px 8px;
  border: none;
  background: transparent;
  font-size: 13px;
  font-weight: 600;
  color: var(--muted, #7a869c);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  margin-bottom: -1px;
  transition: color 0.15s ease, border-color 0.15s ease;
}
.tab:hover { color: var(--ink, #142033); }
.tab.on {
  color: #0d9488;
  border-bottom-color: #14b8a6;
}

.rail-body {
  min-height: 180px;
  animation: fade-up 0.35s ease both;
}
.empty {
  font-size: 13px;
  color: var(--muted, #7a869c);
  padding: 12px 0;
}

.feed, .odds {
  list-style: none;
  margin: 0;
  padding: 0;
}
.feed li,
.odds li {
  display: grid;
  grid-template-columns: 1fr auto auto;
  gap: 8px;
  align-items: center;
  padding: 10px 0;
  font-size: 13px;
  border-bottom: 1px solid rgba(20, 32, 51, 0.05);
}
.odds li { grid-template-columns: 8px 1fr auto; }
.fn {
  font-weight: 600;
  color: var(--ink, #142033);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.fp { color: var(--accent-deep, #0f766e); font-weight: 600; font-size: 12px; }
.ft { color: var(--muted, #7a869c); font-size: 11px; }
.odds i {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}
.odds em {
  font-style: normal;
  font-family: var(--font-display, 'Outfit', sans-serif);
  font-weight: 700;
  font-size: 12px;
  color: var(--ink, #142033);
}
.rules p {
  margin: 0 0 14px;
  font-size: 13px;
  line-height: 1.7;
  color: var(--ink-soft, #3d4a63);
}
.rules p:last-child { margin-bottom: 0; }
.rules strong { color: var(--ink, #142033); }
</style>
