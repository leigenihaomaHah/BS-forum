<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 每日签到
    </div>

    <div v-if="!auth.isLoggedIn" class="auth-card" style="max-width: 420px; margin: 48px auto;">
      <h4 class="mb-1">签到需要登录</h4>
      <p class="text-muted mb-3" style="font-size: 13px">登录后即可签到，连续签到还有额外奖励</p>
      <button type="button" class="btn btn-forum w-100" @click="authModal.openLogin()">前往登录</button>
    </div>

    <template v-else>
      <div class="row g-4">
        <!-- Left column: sign-in card + milestone -->
        <div class="col-md-5">
          <!-- Sign-in button card -->
          <div class="panel signin-hero" :class="{ done: status?.todaySignedIn }">
            <div class="panel-header">
              <span class="accent"></span>{{ status?.todaySignedIn ? '今日已签到' : '每日签到' }}
            </div>
            <div class="p-4 text-center">
              <div class="streak-display">
                <span class="streak-num">{{ status?.consecutiveDays || 0 }}</span>
                <span class="streak-label">连续签到天数</span>
              </div>
              <div class="streak-sub text-muted">
                累计签到 {{ status?.totalDays || 0 }} 天
              </div>

              <button
                class="signin-btn mt-3"
                :class="{ signed: status?.todaySignedIn }"
                :disabled="status?.todaySignedIn || signingIn"
                @click="doSignIn"
              >
                <span v-if="signingIn" class="spinner"></span>
                <span v-else-if="status?.todaySignedIn">✓ 今日已签到</span>
                <span v-else>立即签到</span>
              </button>

              <!-- Rewards -->
              <div class="rewards-row mt-3">
                <div class="reward-item">
                  <span class="reward-icon">⭐</span>
                  <span class="reward-val">+{{ rewards.points }}</span>
                  <span class="reward-unit">积分</span>
                </div>
                <div class="reward-plus">+</div>
                <div class="reward-item">
                  <span class="reward-icon">🪙</span>
                  <span class="reward-val">+{{ rewards.coins }}</span>
                  <span class="reward-unit">金币</span>
                </div>
              </div>

              <div v-if="signInResult" class="signin-result mt-3">
                <div class="result-title">签到成功！</div>
                <div class="result-detail">
                  +{{ signInResult.pointsAwarded }} 积分 · +{{ signInResult.coinsAwarded }} 金币
                </div>
                <div v-if="signInResult.consecutiveDays" class="result-streak">
                  连续 {{ signInResult.consecutiveDays }} 天
                </div>
                <div v-if="signInResult.milestoneBonus" class="result-milestone">
                  🎉 达成{{ signInResult.milestoneBonus.label }}！额外 +{{ signInResult.milestoneBonus.pointsBonus }} 积分
                </div>
                <div v-if="signInResult.badge" class="result-badge">
                  {{ signInResult.badge }}
                </div>
                <div class="mt-2" style="font-size:13px">
                  <router-link to="/lottery">去幸运转盘抽奖 →</router-link>
                  <span class="text-muted">（每日首抽免费）</span>
                </div>
              </div>

              <div v-if="signInError" class="text-danger mt-2" style="font-size: 13px">
                {{ signInError }}
              </div>
            </div>
          </div>

          <div class="panel">
            <div class="panel-header"><span class="accent"></span>每日任务</div>
            <div class="p-3">
              <div v-for="t in tasks" :key="t.code" class="task-row">
                <div>
                  <div class="fw-bold" style="font-size:13px">{{ t.title }}</div>
                  <div class="text-muted" style="font-size:12px">{{ t.description }} · {{ t.progress }}/{{ t.target }}</div>
                </div>
                <button
                  class="btn btn-sm btn-forum"
                  :disabled="t.claimed || t.progress < t.target || claiming === t.code"
                  @click="claimTask(t)"
                >{{ t.claimed ? '已领' : `领 +${t.rewardPoints}分` }}</button>
              </div>
              <div v-if="!tasks.length" class="text-muted" style="font-size:13px">加载中…</div>
            </div>
          </div>

          <!-- Milestone progress -->
          <div class="panel">
            <div class="panel-header"><span class="accent"></span>签到里程碑</div>
            <div class="p-3">
              <div
                v-for="m in milestones"
                :key="m.days"
                class="milestone-row"
                :class="{
                  reached: (status?.consecutiveDays || 0) >= m.days,
                  next: nextMilestone?.days === m.days,
                }"
              >
                <div class="ms-icon">{{ (status?.consecutiveDays || 0) >= m.days ? '🏆' : '🎯' }}</div>
                <div class="ms-info">
                  <div class="ms-label">{{ m.label }}</div>
                  <div class="ms-reward">+{{ m.pointsBonus }} 积分{{ m.coinsBonus ? ' · +' + m.coinsBonus + ' 金币' : '' }}</div>
                </div>
                <div class="ms-status">
                  <span v-if="(status?.consecutiveDays || 0) >= m.days" class="ms-done">已达成</span>
                  <span v-else-if="nextMilestone?.days === m.days" class="ms-next">
                    差 {{ m.days - (status?.consecutiveDays || 0) }} 天
                  </span>
                  <span v-else class="ms-locked">未达成</span>
                </div>
              </div>
              <!-- Progress to next milestone -->
              <div v-if="nextMilestone" class="mt-3 milestone-progress-card">
                <div class="d-flex justify-content-between mb-1" style="font-size: 12px">
                  <span>距离「{{ nextMilestone.label }}」</span>
                  <span>{{ status?.consecutiveDays || 0 }} / {{ nextMilestone.days }} 天</span>
                </div>
                <div class="progress" style="height: 8px">
                  <div
                    class="progress-bar"
                    role="progressbar"
                    :style="{
                      width: milestonePct + '%',
                      background: 'linear-gradient(90deg, #e8a54b, #d4880f)'
                    }"
                  ></div>
                </div>
                <div class="text-muted mt-1" style="font-size: 12px">
                  再签到 {{ nextMilestone.daysLeft }} 天即可获得额外奖励
                </div>
              </div>
              <div v-else class="text-center text-muted mt-2" style="font-size: 13px">
                🎉 已达成全部里程碑！
              </div>
            </div>
          </div>
        </div>

        <!-- Right column: calendar -->
        <div class="col-md-7">
          <div class="panel">
            <div class="panel-header">
              <span class="accent"></span>{{ calYear }}年{{ calMonth }}月 签到日历
            </div>
            <div class="p-3">
              <!-- Weekday headers -->
              <div class="cal-grid cal-header">
                <div v-for="d in weekDays" :key="d" class="cal-cell cal-day-name">{{ d }}</div>
              </div>
              <!-- Calendar days -->
              <div class="cal-grid">
                <div
                  v-for="(day, idx) in calDays"
                  :key="idx"
                  class="cal-cell cal-day"
                  :class="{
                    'cal-today': day === calToday,
                    'cal-signed': day && signedSet.has(calDateStr(day)),
                    'cal-empty': !day,
                    'cal-future': day && day > calToday,
                  }"
                >
                  <span v-if="day" class="cal-day-num">{{ day }}</span>
                  <span v-if="day && signedSet.has(calDateStr(day))" class="cal-dot">●</span>
                </div>
              </div>
              <div class="cal-legend mt-2">
                <span class="legend-item"><span class="dot signed"></span> 已签到</span>
                <span class="legend-item"><span class="dot today"></span> 今天</span>
              </div>
            </div>
          </div>

          <!-- Level info banner -->
          <div class="panel">
            <div class="p-3 d-flex align-items-center gap-3">
              <div style="font-size: 28px">{{ auth.user.level >= 5 ? '🌟' : '⭐' }}</div>
              <div>
                <div class="fw-bold" style="font-size: 14px">
                  Lv.{{ auth.user.level }} {{ auth.user.levelName }} 签到特权
                </div>
                <div class="text-muted" style="font-size: 12px">
                  每日签到基础 +5 积分、+2 金币，连续签到有额外加成；达成里程碑另有奖励。
                  签到后来玩 <router-link to="/lottery">幸运转盘</router-link>，每日首抽免费。
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { useToastStore } from '../stores/toast'

const auth = useAuthStore()
const authModal = useAuthModalStore()
const toast = useToastStore()
const signingIn = ref(false)
const signInResult = ref(null)
const signInError = ref('')

const status = computed(() => auth.signInStatus)

const rewards = computed(() => {
  const r = status.value?.todayRewards
  if (r) return { points: r.points, coins: r.coins }
  return { points: 5, coins: 2 }
})

const nextMilestone = computed(() => status.value?.nextMilestone || null)
const milestonePct = computed(() => {
  if (!nextMilestone.value || !status.value) return 0
  return Math.min(100, Math.round(((status.value.consecutiveDays || 0) / nextMilestone.value.days) * 100))
})

const milestones = [
  { days: 7, pointsBonus: 5, coinsBonus: 0, label: '连续 7 天' },
  { days: 14, pointsBonus: 10, coinsBonus: 2, label: '连续 14 天' },
  { days: 21, pointsBonus: 15, coinsBonus: 3, label: '连续 21 天' },
  { days: 30, pointsBonus: 30, coinsBonus: 5, label: '连续 30 天（满贯）' },
]

const tasks = ref([])
const claiming = ref('')

async function loadTasks() {
  if (!auth.isLoggedIn) { tasks.value = []; return }
  try {
    const { data } = await api.get('/tasks')
    tasks.value = data
  } catch { tasks.value = [] }
}

async function claimTask(t) {
  claiming.value = t.code
  try {
    await api.post(`/tasks/${t.code}/claim`)
    await auth.fetchMe()
    await loadTasks()
  } catch (e) { toast.error(e.message) }
  finally { claiming.value = '' }
}

// Calendar logic
const weekDays = ['日', '一', '二', '三', '四', '五', '六']
const now = new Date()
const calYear = now.getFullYear()
const calMonth = now.getMonth() + 1
const calToday = now.getDate()

const signedSet = computed(() => {
  return new Set(status.value?.thisMonth || [])
})

function calDateStr(day) {
  const m = String(calMonth).padStart(2, '0')
  const d = String(day).padStart(2, '0')
  return `${calYear}-${m}-${d}`
}

const calDays = computed(() => {
  const firstDay = new Date(calYear, calMonth - 1, 1).getDay()
  const daysInMonth = new Date(calYear, calMonth, 0).getDate()
  const cells = []
  for (let i = 0; i < firstDay; i++) cells.push(null)
  for (let d = 1; d <= daysInMonth; d++) cells.push(d)
  return cells
})

async function doSignIn() {
  if (signingIn.value || status.value?.todaySignedIn) return
  signingIn.value = true
  signInResult.value = null
  signInError.value = ''
  try {
    const data = await auth.signIn()
    signInResult.value = data
    // Refresh full status
    await auth.fetchSignInStatus()
    await loadTasks()
  } catch (e) {
    signInError.value = e.message
  } finally {
    signingIn.value = false
  }
}

onMounted(() => {
  if (auth.isLoggedIn) {
    auth.fetchSignInStatus()
    loadTasks()
  }
})
</script>

<style scoped>
.signin-hero {
  transition: all 0.3s ease;
}
.signin-hero.done {
  border-color: rgba(13, 148, 136, 0.3);
}
.task-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  padding: 10px 0;
  border-bottom: 1px solid #f1f5f9;
}
.task-row:last-child { border-bottom: none; }

.streak-display {
  display: flex;
  flex-direction: column;
  align-items: center;
}
.streak-num {
  font-size: 56px;
  font-weight: 800;
  font-family: 'Outfit', sans-serif;
  line-height: 1;
  background: linear-gradient(135deg, #0d9488, #14b8a6);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.streak-label {
  font-size: 13px;
  color: #7a869c;
  margin-top: 2px;
}
.streak-sub {
  font-size: 12px;
  margin-top: 4px;
}

.signin-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-width: 180px;
  padding: 14px 32px;
  border: none;
  border-radius: 999px;
  background: linear-gradient(135deg, #0d9488, #14b8a6);
  color: #fff;
  font-size: 17px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.25s ease;
  box-shadow: 0 4px 16px rgba(13, 148, 136, 0.3);
}
.signin-btn:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 6px 24px rgba(13, 148, 136, 0.4);
}
.signin-btn:active:not(:disabled) {
  transform: translateY(0);
}
.signin-btn:disabled {
  cursor: default;
  opacity: 0.6;
}
.signin-btn.signed {
  background: #e8edf3;
  color: #3d4a63;
  box-shadow: none;
}

.spinner {
  width: 20px;
  height: 20px;
  border: 3px solid rgba(255,255,255,0.3);
  border-top-color: #fff;
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
}
@keyframes spin {
  to { transform: rotate(360deg); }
}

.rewards-row {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
}
.reward-item {
  display: flex;
  align-items: center;
  gap: 4px;
  background: #f8fafc;
  border: 1px solid rgba(20, 32, 51, 0.08);
  border-radius: 10px;
  padding: 8px 14px;
}
.reward-icon {
  font-size: 16px;
}
.reward-val {
  font-weight: 700;
  font-size: 16px;
  color: #142033;
}
.reward-unit {
  font-size: 12px;
  color: #7a869c;
}
.reward-plus {
  color: #7a869c;
  font-weight: 700;
}

.multiplier-badge {
  display: inline-block;
  padding: 4px 12px;
  border-radius: 999px;
  background: rgba(232, 165, 75, 0.15);
  color: #a16207;
  font-size: 12px;
  font-weight: 600;
}

/* Sign-in result animation */
.signin-result {
  padding: 12px;
  border-radius: 12px;
  background: rgba(13, 148, 136, 0.08);
  border: 1px solid rgba(13, 148, 136, 0.2);
  animation: fadeInUp 0.4s ease;
}
@keyframes fadeInUp {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: none; }
}
.result-title {
  font-weight: 700;
  font-size: 15px;
  color: #0d9488;
}
.result-detail {
  font-size: 13px;
  color: #142033;
  margin-top: 2px;
}
.result-streak {
  font-size: 12px;
  color: #0d9488;
  font-weight: 600;
  margin-top: 2px;
}
.result-milestone {
  margin-top: 6px;
  padding: 6px 10px;
  border-radius: 8px;
  background: rgba(232, 165, 75, 0.15);
  color: #a16207;
  font-size: 12px;
  font-weight: 600;
}
.result-badge {
  margin-top: 6px;
  font-size: 14px;
}

/* Milestones */
.milestone-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  border-radius: 10px;
  margin-bottom: 6px;
  transition: background 0.2s ease;
}
.milestone-row.reached {
  opacity: 0.65;
}
.milestone-row.next {
  background: rgba(232, 165, 75, 0.08);
}
.ms-icon {
  font-size: 20px;
  width: 32px;
  text-align: center;
  flex-shrink: 0;
}
.ms-info {
  flex: 1;
}
.ms-label {
  font-weight: 600;
  font-size: 13px;
}
.ms-reward {
  font-size: 11px;
  color: #7a869c;
}
.ms-status {
  flex-shrink: 0;
  font-size: 12px;
  font-weight: 600;
}
.ms-done {
  color: #0d9488;
}
.ms-next {
  color: #e8a54b;
}
.ms-locked {
  color: #7a869c;
}
.milestone-progress-card {
  background: #f8fafc;
  border: 1px solid rgba(20, 32, 51, 0.08);
  border-radius: 10px;
  padding: 12px;
}

/* Calendar */
.cal-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 2px;
}
.cal-header {
  margin-bottom: 4px;
}
.cal-cell {
  text-align: center;
  padding: 4px 2px;
  font-size: 13px;
}
.cal-day-name {
  font-weight: 600;
  color: #7a869c;
  font-size: 12px;
  padding: 6px 2px;
}
.cal-day {
  position: relative;
  min-height: 44px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border-radius: 10px;
  transition: background 0.2s ease;
}
.cal-day:not(.cal-empty):not(.cal-future):hover {
  background: #f0f4f8;
}
.cal-day-num {
  font-weight: 500;
  font-size: 14px;
  color: #142033;
  z-index: 1;
}
.cal-today .cal-day-num {
  font-weight: 800;
  color: #fff;
  background: #0d9488;
  width: 30px;
  height: 30px;
  line-height: 30px;
  border-radius: 50%;
}
.cal-signed .cal-dot {
  font-size: 8px;
  color: #0d9488;
  position: absolute;
  bottom: 4px;
  z-index: 1;
}
.cal-today.cal-signed .cal-dot {
  color: #fff;
}
.cal-empty {
  visibility: hidden;
}
.cal-future {
  opacity: 0.35;
}
.cal-legend {
  display: flex;
  gap: 16px;
  justify-content: center;
}
.legend-item {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: #7a869c;
}
.dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}
.dot.signed {
  background: #0d9488;
}
.dot.today {
  background: #0d9488;
  box-shadow: 0 0 0 3px rgba(13, 148, 136, 0.2);
}
</style>
