<template>
  <div class="admin-page board">
    <div class="board-top">
      <div>
        <h2 class="page-title mb-0">运营看板</h2>
        <p class="board-sub">论坛全局一览 · 健康度 · 待办 · 内容与经济脉搏</p>
      </div>
      <div class="board-top-right">
        <span class="board-clock">{{ refreshedAt || '—' }}</span>
        <button class="admin-btn admin-btn-primary" :disabled="loading" @click="load">
          {{ loading ? '刷新中…' : '刷新' }}
        </button>
      </div>
    </div>

    <div v-if="alertItems.length" class="alert-strip">
      <strong>需关注：</strong>
      <router-link v-for="a in alertItems" :key="a.to" :to="a.to">{{ a.text }}</router-link>
    </div>

    <!-- KPI -->
    <div class="kpi-row">
      <router-link to="/admin/users" class="kpi-card">
        <div class="kpi-label">用户</div>
        <div class="kpi-num">{{ s.totalUsers ?? '—' }}</div>
        <div class="kpi-meta">
          今日注册 {{ s.todayRegistrations ?? 0 }}
          <span :class="deltaClass(s.todayRegistrations, s.yesterdayRegistrations)">{{ deltaText(s.todayRegistrations, s.yesterdayRegistrations) }}</span>
          · 禁言 {{ s.mutedUsers ?? 0 }}
        </div>
      </router-link>
      <router-link to="/admin/threads" class="kpi-card">
        <div class="kpi-label">内容</div>
        <div class="kpi-num">{{ s.totalThreads ?? '—' }}</div>
        <div class="kpi-meta">
          今日新帖 {{ s.todayThreads ?? 0 }}
          <span :class="deltaClass(s.todayThreads, s.yesterdayThreads)">{{ deltaText(s.todayThreads, s.yesterdayThreads) }}</span>
          · 回复 {{ s.todayReplies ?? 0 }}
          <span :class="deltaClass(s.todayReplies, s.yesterdayReplies)">{{ deltaText(s.todayReplies, s.yesterdayReplies) }}</span>
        </div>
      </router-link>
      <router-link to="/admin/signin" class="kpi-card">
        <div class="kpi-label">今日活跃</div>
        <div class="kpi-num">{{ s.todayActiveUsers ?? s.todayActive ?? '—' }}</div>
        <div class="kpi-meta">
          较昨日
          <span :class="deltaClass(s.todayActiveUsers, s.yesterdayActiveUsers)">{{ deltaText(s.todayActiveUsers, s.yesterdayActiveUsers) }}</span>
          · 签到 {{ s.todaySignIns ?? 0 }}
          <span :class="deltaClass(s.todaySignIns, s.yesterdaySignIns)">{{ deltaText(s.todaySignIns, s.yesterdaySignIns) }}</span>
        </div>
      </router-link>
      <router-link to="/shop" class="kpi-card">
        <div class="kpi-label">经济</div>
        <div class="kpi-num" :class="{ neg: (s.todayCoinDelta ?? 0) < 0 }">
          {{ formatDelta(s.todayCoinDelta) }}
        </div>
        <div class="kpi-meta">转盘 {{ s.todayLotterySpins ?? 0 }} 次 · 商城 {{ s.todayShopOrders ?? 0 }} 笔</div>
      </router-link>
      <router-link to="/admin/reports" class="kpi-card kpi-risk">
        <div class="kpi-label">风险待办</div>
        <div class="kpi-num">{{ (s.pendingReports || 0) + (s.pendingReviewThreads || 0) }}</div>
        <div class="kpi-meta">举报 {{ s.pendingReports ?? 0 }} · 待审帖 {{ s.pendingReviewThreads ?? 0 }}</div>
      </router-link>
    </div>

    <!-- Main grid -->
    <div class="main-grid">
      <div class="admin-panel todo-panel">
        <div class="admin-panel-hd">
          待办队列
          <span v-if="todoTotal > 0" class="hd-badge">{{ todoTotal }}</span>
        </div>
        <div v-if="todoTotal === 0" class="todo-empty">当前无积压，社区状态良好</div>
        <div v-else class="todo-sections">
          <div v-if="s.pendingReviewThreads" class="todo-block">
            <div class="todo-block-hd">
              <router-link to="/admin/threads?status=pending">待审帖 · {{ s.pendingReviewThreads }}</router-link>
            </div>
            <div class="p-2 text-muted" style="font-size:12px">请尽快处理，避免用户等待过久</div>
          </div>
          <div v-if="s.pendingReports" class="todo-block">
            <div class="todo-block-hd">
              <router-link to="/admin/reports">待处理举报 · {{ s.pendingReports }}</router-link>
            </div>
            <ul>
              <li v-for="r in (s.todoReports || [])" :key="'r'+r.id">
                <span class="todo-tag">{{ r.targetType }}</span>
                {{ r.reason }}
                <span class="todo-meta">{{ r.reporterNickname }} · {{ fmtTime(r.createdAt) }}</span>
              </li>
            </ul>
          </div>
          <div v-if="s.hiddenThreads" class="todo-block">
            <div class="todo-block-hd">
              <router-link to="/admin/threads?status=hidden">已隐藏帖 · {{ s.hiddenThreads }}</router-link>
            </div>
            <ul>
              <li v-for="t in (s.todoHidden || [])" :key="'h'+t.id">
                <router-link :to="`/thread/${t.id}`" target="_blank">{{ t.title }}</router-link>
                <span class="todo-meta">{{ t.forumName }} · {{ t.authorNickname }}</span>
              </li>
            </ul>
          </div>
          <div v-if="s.mutedUsers" class="todo-block">
            <div class="todo-block-hd">
              <router-link to="/admin/users?muted=1">禁言中 · {{ s.mutedUsers }}</router-link>
            </div>
            <ul>
              <li v-for="u in (s.todoMuted || [])" :key="'m'+u.id">
                {{ u.nickname }} <span class="todo-meta">@{{ u.username }} · {{ u.muteReason || '无原因' }}</span>
              </li>
            </ul>
          </div>
          <div v-if="s.lockedThreads" class="todo-block">
            <div class="todo-block-hd">
              <router-link to="/admin/threads?status=locked">禁回复 · {{ s.lockedThreads }}</router-link>
            </div>
            <ul>
              <li v-for="t in (s.todoLocked || [])" :key="'l'+t.id">
                <router-link :to="`/thread/${t.id}`" target="_blank">{{ t.title }}</router-link>
                <span class="todo-meta">{{ t.forumName }}</span>
              </li>
            </ul>
          </div>
        </div>
      </div>

      <div class="admin-panel">
        <div class="admin-panel-hd">近 7 日趋势</div>
        <div class="chart-legend">
          <span class="lg lg-reg">注册</span>
          <span class="lg lg-sign">签到</span>
          <span class="lg lg-thread">新帖</span>
        </div>
        <div class="chart-wrap">
          <canvas ref="chartCanvas"></canvas>
          <div v-if="!hasTrend" class="chart-empty">暂无数据</div>
        </div>
      </div>

      <div class="admin-panel">
        <div class="admin-panel-hd">
          版块热力
          <router-link to="/admin/forums" class="hd-link">管理</router-link>
        </div>
        <div v-if="!(s.forumHeat || []).length" class="p-3 text-muted">暂无版块</div>
        <div v-else class="heat-list">
          <div v-for="f in s.forumHeat" :key="f.forumId" class="heat-row">
            <div class="heat-name">{{ f.name }}</div>
            <div class="heat-bar-wrap">
              <div class="heat-bar" :style="{ width: heatPct(f) + '%' }"></div>
            </div>
            <div class="heat-stats">
              <span>{{ f.threadCount }} 帖</span>
              <span>今日 {{ f.todayThreads }}</span>
              <span>订阅 {{ f.subscriberCount }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Bottom pulse -->
    <div class="pulse-grid">
      <div class="admin-panel">
        <div class="admin-panel-hd">内容脉搏</div>
        <div class="panel-scroll">
          <div class="pulse-kpis">
            <div><b>{{ s.essenceCount ?? 0 }}</b><span>精品</span></div>
            <div><b>{{ s.pinnedCount ?? 0 }}</b><span>置顶</span></div>
            <div><b>{{ s.totalPosts ?? 0 }}</b><span>总回复</span></div>
          </div>
          <div class="pulse-section-title">热门帖</div>
          <ul class="pulse-list" v-if="s.hotThreads?.length">
            <li v-for="t in s.hotThreads" :key="t.id">
              <router-link :to="`/thread/${t.id}`" target="_blank">{{ t.title }}</router-link>
              <span class="todo-meta">{{ t.forumName }} · {{ t.views }} 浏览 · {{ t.replyCount }} 回</span>
            </li>
          </ul>
          <div v-else class="p-2 text-muted" style="font-size:12px">暂无</div>
          <div class="pulse-section-title">最近注册</div>
          <ul class="pulse-list" v-if="s.recentUsers?.length">
            <li v-for="u in s.recentUsers" :key="u.id">
              <router-link to="/admin/users">{{ u.nickname }}</router-link>
              <span class="todo-meta">@{{ u.username }} · Lv.{{ u.level }} · {{ fmtDate(u.createdAt) }}</span>
            </li>
          </ul>
        </div>
      </div>

      <div class="admin-panel">
        <div class="admin-panel-hd">经济脉搏</div>
        <div class="panel-scroll">
          <div class="econ-grid">
            <div class="econ-item">
              <div class="econ-label">今日转盘</div>
              <div class="econ-val">{{ s.todayLotterySpins ?? 0 }} 次</div>
              <div class="econ-sub">发出 {{ s.todayLotteryOutCoins ?? 0 }} · 消耗 {{ s.todayLotteryCostCoins ?? 0 }}</div>
            </div>
            <div class="econ-item">
              <div class="econ-label">今日商城</div>
              <div class="econ-val">{{ s.todayShopOrders ?? 0 }} 笔</div>
              <div class="econ-sub">金币净变动 {{ formatDelta(s.todayCoinDelta) }}</div>
            </div>
            <div class="econ-item">
              <div class="econ-label">VIP 有效</div>
              <div class="econ-val">{{ s.vipUsers ?? 0 }}</div>
              <div class="econ-sub">抽奖券存量 {{ s.lotteryTicketStock ?? 0 }}</div>
            </div>
          </div>
        </div>
      </div>

      <div class="admin-panel">
        <div class="admin-panel-hd">最近管理操作</div>
        <div class="panel-scroll">
          <ul class="pulse-list" v-if="s.recentModLogs?.length">
            <li v-for="m in s.recentModLogs" :key="m.id">
              <span class="mod-action">{{ actionLabel(m.action) }}</span>
              <span>{{ m.adminNickname }}</span>
              <router-link
                v-if="m.targetType === 'thread'"
                :to="`/admin/threads`"
              >帖 #{{ m.targetId }}</router-link>
              <router-link
                v-else-if="m.targetType === 'user'"
                to="/admin/users"
              >用户 #{{ m.targetId }}</router-link>
              <span v-else>{{ m.targetType }} #{{ m.targetId }}</span>
              <span class="todo-meta">{{ fmtTime(m.createdAt) }}{{ m.reason ? ' · ' + m.reason : '' }}</span>
            </li>
          </ul>
          <div v-else class="p-3 text-muted">暂无操作记录</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
import api from '../../api/http'
import { Chart, LineController, LineElement, PointElement, CategoryScale, LinearScale, Filler, Tooltip, Legend } from 'chart.js'

Chart.register(LineController, LineElement, PointElement, CategoryScale, LinearScale, Filler, Tooltip, Legend)

const s = ref({})
const loading = ref(false)
const refreshedAt = ref('')
const chartCanvas = ref(null)
let chartInstance = null

const hasTrend = computed(() => (s.value.dailyRegistrations || []).length > 0)
const todoTotal = computed(() =>
  (s.value.pendingReports || 0) +
  (s.value.pendingReviewThreads || 0) +
  (s.value.hiddenThreads || 0) +
  (s.value.mutedUsers || 0) +
  (s.value.lockedThreads || 0)
)

const alertItems = computed(() => {
  const items = []
  const pending = (s.value.pendingReports || 0) + (s.value.pendingReviewThreads || 0)
  if (pending > 0) {
    items.push({ to: '/admin/queue', text: `${pending} 项待处理（进审核队列）` })
  }
  if (s.value.pendingReports > 0) {
    items.push({ to: '/admin/reports', text: `${s.value.pendingReports} 条待处理举报` })
  }
  if (s.value.pendingReviewThreads > 0) {
    items.push({ to: '/admin/threads?status=pending', text: `${s.value.pendingReviewThreads} 帖待审核` })
  }
  if (s.value.mutedUsers > 10) {
    items.push({ to: '/admin/users?muted=1', text: `${s.value.mutedUsers} 人禁言中` })
  }
  return items
})

function deltaText(today, yesterday) {
  if (today == null || yesterday == null) return ''
  const d = today - yesterday
  if (d === 0) return '· 持平'
  return d > 0 ? `· 较昨日 ↑${d}` : `· 较昨日 ↓${Math.abs(d)}`
}

function deltaClass(today, yesterday) {
  if (today == null || yesterday == null) return ''
  return today >= yesterday ? 'up' : 'down'
}

function formatDelta(n) {
  if (n == null) return '—'
  if (n > 0) return `+${n}`
  return String(n)
}

function heatPct(f) {
  const list = s.value.forumHeat || []
  const max = Math.max(...list.map((x) => x.threadCount || 0), 1)
  return Math.round(((f.threadCount || 0) / max) * 100)
}

function fmtDate(iso) {
  if (!iso) return ''
  return new Date(iso).toLocaleDateString()
}

function fmtTime(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  const pad = (n) => String(n).padStart(2, '0')
  return `${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function actionLabel(a) {
  return ({
    hide: '隐藏帖', unhide: '取消隐藏',
    lock_replies: '禁回复', unlock_replies: '开回复',
    pin: '置顶', unpin: '取消置顶',
    essence: '加精', unessence: '取消精品',
    mute: '禁言', unmute: '解禁',
  })[a] || a
}

function drawChart() {
  if (!chartCanvas.value) return
  if (chartInstance) {
    chartInstance.destroy()
    chartInstance = null
  }
  const regs = s.value.dailyRegistrations || []
  const signs = s.value.weeklyActivity || []
  const threads = s.value.dailyNewThreads || []
  if (!regs.length) return

  chartInstance = new Chart(chartCanvas.value, {
    type: 'line',
    data: {
      labels: regs.map((d) => d.day),
      datasets: [
        {
          label: '注册',
          data: regs.map((d) => d.count),
          borderColor: '#e8a54b',
          backgroundColor: 'rgba(232,165,75,0.12)',
          fill: true,
          tension: 0.35,
          pointRadius: 3,
        },
        {
          label: '签到',
          data: signs.map((d) => d.count),
          borderColor: '#0d9488',
          backgroundColor: 'rgba(13,148,136,0.10)',
          fill: true,
          tension: 0.35,
          pointRadius: 3,
        },
        {
          label: '新帖',
          data: threads.map((d) => d.count),
          borderColor: '#3b82f6',
          backgroundColor: 'rgba(59,130,246,0.08)',
          fill: true,
          tension: 0.35,
          pointRadius: 3,
        },
      ],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: 'index', intersect: false },
      plugins: {
        legend: { display: false },
        tooltip: {
          backgroundColor: '#142033',
          padding: 10,
          cornerRadius: 8,
        },
      },
      scales: {
        x: {
          grid: { display: false },
          ticks: { font: { size: 11 }, color: '#7a869c' },
        },
        y: {
          beginAtZero: true,
          grid: { color: 'rgba(20,32,51,0.06)' },
          ticks: { font: { size: 11 }, color: '#7a869c', stepSize: 1 },
        },
      },
    },
  })
}

async function load() {
  loading.value = true
  try {
    const { data } = await api.get('/admin/stats')
    s.value = data
    refreshedAt.value = new Date().toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
    await nextTick()
    drawChart()
  } catch {
    /* ignore */
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await load()
})

onUnmounted(() => {
  if (chartInstance) chartInstance.destroy()
})
</script>

<style scoped>
.board-top {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
  margin-bottom: 18px;
}
.alert-strip {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px 14px;
  margin-bottom: 14px;
  padding: 10px 14px;
  border-radius: 10px;
  background: rgba(225, 29, 72, 0.08);
  border: 1px solid rgba(225, 29, 72, 0.18);
  color: #9f1239;
  font-size: 13px;
}
.alert-strip a {
  color: #be123c;
  font-weight: 700;
  text-decoration: none;
}
.alert-strip a:hover { text-decoration: underline; }
.board-sub {
  margin: 4px 0 0;
  font-size: 13px;
  color: #7a869c;
}
.board-top-right {
  display: flex;
  align-items: center;
  gap: 12px;
}
.board-clock {
  font-size: 12px;
  color: #94a3b8;
  font-variant-numeric: tabular-nums;
}

.kpi-row {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 12px;
  margin-bottom: 16px;
}
.kpi-card {
  display: block;
  background: #fff;
  border: 1px solid rgba(20, 32, 51, 0.08);
  border-radius: 14px;
  padding: 16px 18px;
  text-decoration: none;
  color: inherit;
  box-shadow: 0 8px 24px rgba(20, 32, 51, 0.04);
  transition: border-color 0.15s, box-shadow 0.15s, transform 0.15s;
}
.kpi-card:hover {
  border-color: #0d9488;
  box-shadow: 0 12px 28px rgba(13, 148, 136, 0.1);
  transform: translateY(-1px);
}
.kpi-risk:hover { border-color: #e11d48; }
.kpi-label {
  font-size: 12px;
  font-weight: 600;
  color: #7a869c;
  margin-bottom: 6px;
}
.kpi-num {
  font-size: 28px;
  font-weight: 800;
  color: #142033;
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
}
.kpi-num.neg { color: #e11d48; }
.kpi-meta {
  margin-top: 8px;
  font-size: 12px;
  color: #94a3b8;
}
.kpi-meta .up { color: #0d9488; }
.kpi-meta .down { color: #e11d48; }

.main-grid {
  display: grid;
  grid-template-columns: 1.1fr 1.2fr 1fr;
  gap: 14px;
  margin-bottom: 14px;
  align-items: start;
}
.main-grid > .admin-panel {
  height: 420px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.main-grid > .admin-panel > .admin-panel-hd {
  flex-shrink: 0;
}
.todo-empty {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  text-align: center;
  color: #0d9488;
  font-size: 13px;
  font-weight: 600;
}
.todo-sections {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 4px 0 8px;
}
.todo-block { padding: 8px 16px 4px; border-bottom: 1px solid #f1f5f9; }
.todo-block:last-child { border-bottom: none; }
.todo-block-hd {
  font-size: 12px;
  font-weight: 700;
  margin-bottom: 6px;
}
.todo-block-hd a { color: #142033; text-decoration: none; }
.todo-block-hd a:hover { color: #0d9488; }
.todo-block ul {
  list-style: none;
  margin: 0;
  padding: 0;
}
.todo-block li {
  font-size: 13px;
  padding: 4px 0;
  color: #3d4a63;
}
.todo-block li a { color: #142033; text-decoration: none; }
.todo-block li a:hover { color: #0d9488; }
.todo-tag {
  display: inline-block;
  font-size: 10px;
  font-weight: 700;
  padding: 1px 6px;
  border-radius: 4px;
  background: #fef3c7;
  color: #b45309;
  margin-right: 4px;
}
.todo-meta {
  display: block;
  font-size: 11px;
  color: #94a3b8;
  margin-top: 1px;
}
.hd-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 18px;
  height: 18px;
  padding: 0 5px;
  border-radius: 9px;
  background: #e11d48;
  color: #fff;
  font-size: 11px;
  font-weight: 700;
  margin-left: 6px;
}
.hd-link {
  margin-left: auto;
  font-size: 12px;
  font-weight: 600;
  color: #0d9488;
  text-decoration: none;
}
.admin-panel-hd {
  display: flex;
  align-items: center;
}

.chart-legend {
  display: flex;
  gap: 14px;
  padding: 8px 16px 0;
  font-size: 11px;
  font-weight: 600;
  color: #7a869c;
  flex-shrink: 0;
}
.lg::before {
  content: '';
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  margin-right: 5px;
  vertical-align: middle;
}
.lg-reg::before { background: #e8a54b; }
.lg-sign::before { background: #0d9488; }
.lg-thread::before { background: #3b82f6; }
.chart-wrap {
  position: relative;
  flex: 1;
  min-height: 0;
  padding: 8px 16px 12px;
}
.chart-wrap canvas {
  width: 100% !important;
  height: 100% !important;
  max-height: 220px;
}
.chart-empty {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #7a869c;
  font-size: 13px;
}

.heat-list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 8px 16px 14px;
}
.heat-row { margin-bottom: 12px; }
.heat-row:last-child { margin-bottom: 0; }
.heat-name {
  font-size: 13px;
  font-weight: 600;
  color: #142033;
  margin-bottom: 4px;
}
.heat-bar-wrap {
  height: 6px;
  background: #eef2f6;
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 4px;
}
.heat-bar {
  height: 100%;
  background: linear-gradient(90deg, #0d9488, #14b8a6);
  border-radius: 4px;
  min-width: 2px;
}
.heat-stats {
  display: flex;
  gap: 10px;
  font-size: 11px;
  color: #94a3b8;
}

.pulse-grid {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  gap: 14px;
  align-items: start;
}
.pulse-grid > .admin-panel {
  height: 420px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.pulse-grid > .admin-panel > .admin-panel-hd {
  flex-shrink: 0;
}
.panel-scroll {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}
.pulse-kpis {
  display: flex;
  gap: 8px;
  padding: 12px 16px;
  border-bottom: 1px solid #f1f5f9;
}
.pulse-kpis > div {
  flex: 1;
  text-align: center;
  background: #f8fafc;
  border-radius: 8px;
  padding: 10px 6px;
}
.pulse-kpis b {
  display: block;
  font-size: 18px;
  color: #142033;
}
.pulse-kpis span {
  font-size: 11px;
  color: #7a869c;
}
.pulse-section-title {
  padding: 10px 16px 4px;
  font-size: 12px;
  font-weight: 700;
  color: #7a869c;
}
.pulse-list {
  list-style: none;
  margin: 0;
  padding: 0 16px 12px;
}
.pulse-list li {
  padding: 6px 0;
  border-bottom: 1px solid #f8fafc;
  font-size: 13px;
}
.pulse-list li:last-child { border-bottom: none; }
.pulse-list a { color: #142033; text-decoration: none; font-weight: 600; }
.pulse-list a:hover { color: #0d9488; }
.mod-action {
  display: inline-block;
  font-size: 11px;
  font-weight: 700;
  color: #0f766e;
  background: rgba(13, 148, 136, 0.1);
  padding: 1px 6px;
  border-radius: 4px;
  margin-right: 4px;
}

.econ-grid {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 14px 16px;
}
.econ-item {
  padding: 12px;
  background: #f8fafc;
  border-radius: 10px;
}
.econ-label { font-size: 12px; color: #7a869c; font-weight: 600; }
.econ-val { font-size: 20px; font-weight: 800; color: #142033; margin-top: 2px; }
.econ-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }

@media (max-width: 1200px) {
  .kpi-row { grid-template-columns: repeat(3, 1fr); }
  .main-grid, .pulse-grid { grid-template-columns: 1fr; }
  .main-grid > .admin-panel,
  .pulse-grid > .admin-panel { height: 360px; }
}
@media (max-width: 720px) {
  .kpi-row { grid-template-columns: 1fr 1fr; }
  .board-top { flex-direction: column; }
}
</style>
