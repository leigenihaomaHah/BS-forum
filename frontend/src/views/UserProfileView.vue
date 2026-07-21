<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 用户资料
    </div>
    <div v-if="loading" class="p-4 text-muted">加载中...</div>
    <div v-else-if="!profile" class="p-4 text-danger">用户不存在</div>
    <div v-else class="row g-4">
      <!-- Profile card -->
      <div class="col-md-5">
        <div class="panel">
          <div class="panel-header"><span class="accent"></span>{{ profile.nickname }} 的资料</div>
          <div class="p-4 text-center">
            <img :src="profile.avatar || defaultAvatar(profile.nickname)" class="profile-avatar mb-2" alt="" />
            <div class="mb-3">
              <span
                class="level-badge"
                :class="{ 'lv-high': profile.level >= 5 }"
                style="font-size: 15px; padding: 4px 12px"
              >Lv.{{ profile.level }} {{ profile.levelName }}</span>
              <span v-if="profile.isVip" class="vip-tag ms-1">{{ profile.vipTierName || 'VIP' }}</span>
              <span class="ms-2 text-muted">@{{ profile.username }}</span>
            </div>
            <div class="follow-row mb-2">
              <span>粉丝 {{ profile.followerCount ?? 0 }} · 关注 {{ profile.followingCount ?? 0 }}</span>
              <button
                v-if="auth.isLoggedIn && auth.user.id !== profile.id"
                class="btn btn-sm ms-2"
                :class="profile.followedByMe ? 'btn-outline-secondary' : 'btn-forum'"
                @click="toggleFollow"
              >{{ profile.followedByMe ? '已关注' : '关注' }}</button>
            </div>
            <div v-if="badges.length" class="badge-row mb-2">
              <span v-for="b in badges.filter(x => x.earnedAt)" :key="b.code" class="ubadge" :title="b.description">{{ b.name }}</span>
            </div>

            <div class="level-progress-card">
              <div class="d-flex justify-content-between mb-1" style="font-size: 12px">
                <span>等级进度</span>
                <span>{{ profile.points }} / {{ nextLevel ? nextLevel.minPoints : profile.points }} 分</span>
              </div>
              <div class="progress" style="height: 10px">
                <div
                  class="progress-bar"
                  role="progressbar"
                  :style="{ width: progressPct + '%', background: currentLevel.level >= 5 ? 'linear-gradient(90deg, #e8a54b, #d4880f)' : 'linear-gradient(90deg, #0d9488, #14b8a6)' }"
                ></div>
              </div>
              <div v-if="nextLevel" class="text-muted mt-1" style="font-size: 12px">
                距离 {{ nextLevel.name }}（Lv.{{ nextLevel.level }}）还差 {{ nextLevel.minPoints - profile.points }} 分
              </div>
              <div v-else class="text-muted mt-1" style="font-size: 12px">
                已达满级，恭喜！
              </div>
            </div>

            <div class="row g-2 mt-3">
              <div class="col-4">
                <div class="stat-card">
                  <div class="stat-label">积分</div>
                  <div class="stat-value">{{ profile.points }}</div>
                </div>
              </div>
              <div class="col-4">
                <div class="stat-card">
                  <div class="stat-label">金币</div>
                  <div class="stat-value">{{ profile.coins }}</div>
                </div>
              </div>
              <div class="col-4">
                <div class="stat-card">
                  <div class="stat-label">连续签到</div>
                  <div class="stat-value">{{ profile.consecutiveDays }} 天</div>
                </div>
              </div>
            </div>

            <div class="text-muted mt-3" style="font-size: 12px">
              注册时间：{{ formatTime(profile.createdAt) }}
            </div>
          </div>
        </div>
      </div>

      <!-- Level benefits -->
      <div class="col-md-7">
        <div class="panel">
          <div class="panel-header"><span class="accent"></span>等级权益</div>
          <div class="p-0">
            <div
              v-for="l in allLevels"
              :key="l.level"
              class="level-benefit-row"
              :class="{ unlocked: profile.level >= l.level, current: profile.level === l.level }"
            >
              <div class="benefit-level-badge" :class="{ 'lv-high': l.level >= 5 }">
                Lv.{{ l.level }}
              </div>
              <div class="benefit-info">
                <div class="benefit-name">{{ l.name }}</div>
                <div class="benefit-points">{{ l.minPoints }} 分</div>
              </div>
              <div class="benefit-perks">
                <span v-for="b in l.benefits" :key="b" class="perk-tag">{{ b }}</span>
              </div>
              <div class="benefit-status">
                <span v-if="profile.level >= l.level" class="status-unlocked">已解锁</span>
                <span v-else-if="profile.level === l.level - 1" class="status-next">下一个</span>
                <span v-else class="status-locked">未解锁</span>
              </div>
            </div>
          </div>
        </div>

        <div class="panel mt-3">
          <div class="panel-header"><span class="accent"></span>
            <div class="tab-nav d-inline-flex gap-2 ms-2">
              <button class="tab-btn" :class="{ active: activeTab === 'purchases' }" @click="activeTab = 'purchases'">购买记录</button>
              <button class="tab-btn" :class="{ active: activeTab === 'favorites' }" @click="activeTab = 'favorites'">收藏</button>
              <button class="tab-btn" :class="{ active: activeTab === 'activity' }" @click="activeTab = 'activity'">动态</button>
            </div>
          </div>

          <div v-if="activeTab === 'purchases'">
            <div v-if="purchasesLoading" class="p-3 text-muted">加载中...</div>
            <div v-else-if="!purchases.length" class="p-3 text-muted">暂无购买记录</div>
            <div v-else class="p-0">
              <div v-for="(pr, idx) in purchases" :key="idx" class="purchase-row">
                <div class="purchase-info">
                  <router-link :to="`/thread/${pr.threadId}`" class="purchase-title">{{ pr.threadTitle }}</router-link>
                  <div class="purchase-meta">{{ pr.forumName }} · {{ formatTime(pr.purchasedAt) }}</div>
                </div>
                <div class="purchase-cost">-{{ pr.coinPrice }} 金币</div>
              </div>
            </div>
          </div>

          <div v-if="activeTab === 'favorites'">
            <div v-if="favLoading" class="p-3 text-muted">加载中...</div>
            <div v-else-if="!favorites.length" class="p-3 text-muted">暂无收藏</div>
            <div v-else class="p-0">
              <div v-for="(f, idx) in favorites" :key="idx" class="purchase-row">
                <div class="purchase-info">
                  <router-link :to="`/thread/${f.id}`" class="purchase-title">{{ f.title }}</router-link>
                  <div class="purchase-meta">{{ f.forumName }} · {{ formatTime(f.createdAt) }}</div>
                </div>
              </div>
            </div>
          </div>

          <div v-if="activeTab === 'activity'">
            <div v-if="actLoading" class="p-3 text-muted">加载中...</div>
            <div v-else-if="!activities.length" class="p-3 text-muted">暂无动态</div>
            <div v-else class="p-0">
              <div v-for="(a, idx) in activities" :key="idx" class="purchase-row">
                <div class="purchase-info">
                  <router-link :to="`/thread/${a.threadId}`" class="purchase-title">{{ a.threadTitle }}</router-link>
                  <div class="purchase-meta">
                    <span :class="a.type === 'thread' ? 'text-accent' : ''">
                      {{ a.type === 'thread' ? '发帖' : '回复' }}
                    </span>
                    · {{ a.forumName }} · {{ formatTime(a.createdAt) }}
                  </div>
                  <div class="activity-snippet">{{ a.content }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import { useAuthStore } from '../stores/auth'
import { getLevels, getLevel, getNextLevel, getLevelProgress } from '../config/levels.js'

const auth = useAuthStore()
const route = useRoute()
const profile = ref(null)
const badges = ref([])
const loading = ref(true)
const allLevels = computed(() => getLevels())
const activeTab = ref('activity')
const purchases = ref([])
const purchasesLoading = ref(false)
const favorites = ref([])
const favLoading = ref(false)
const activities = ref([])
const actLoading = ref(false)

const currentLevel = computed(() => profile.value ? getLevel(profile.value.points) : getLevels()[0])
const nextLevel = computed(() => profile.value ? getNextLevel(profile.value.points) : null)
const progressPct = computed(() => profile.value ? getLevelProgress(profile.value.points) : 0)

function defaultAvatar(nickname) {
  const n = encodeURIComponent((nickname || '?').slice(0, 1))
  return `https://api.dicebear.com/7.x/initials/svg?seed=${n}`
}

function formatTime(iso) {
  const d = new Date(iso)
  return d.toLocaleString()
}

async function loadPurchases() {
  purchasesLoading.value = true
  try {
    const { data } = await api.get(`/users/${route.params.id}/purchases`)
    purchases.value = data
  } catch {
    purchases.value = []
  } finally {
    purchasesLoading.value = false
  }
}

async function loadFavorites() {
  favLoading.value = true
  try {
    const { data } = await api.get(`/users/${route.params.id}/favorites`)
    favorites.value = data
  } catch {
    favorites.value = []
  } finally {
    favLoading.value = false
  }
}

async function loadActivity() {
  actLoading.value = true
  try {
    const { data } = await api.get(`/users/${route.params.id}/activity`)
    activities.value = data
  } catch {
    activities.value = []
  } finally {
    actLoading.value = false
  }
}

async function loadTab(tab) {
  if (tab === 'purchases') await loadPurchases()
  else if (tab === 'favorites') await loadFavorites()
  else await loadActivity()
}

async function load() {
  loading.value = true
  try {
    const { data } = await api.get(`/users/${route.params.id}`)
    profile.value = data
    try {
      const { data: b } = await api.get(`/users/${route.params.id}/badges`)
      badges.value = b
    } catch { badges.value = [] }
  } catch {
    profile.value = null
  } finally {
    loading.value = false
  }
  await loadTab(activeTab.value)
}

async function toggleFollow() {
  try {
    const { data } = await api.post(`/users/${profile.value.id}/follow`)
    profile.value.followedByMe = data.following
    profile.value.followerCount = (profile.value.followerCount || 0) + (data.following ? 1 : -1)
  } catch (e) { alert(e.message) }
}

watch(activeTab, (tab) => loadTab(tab))
onMounted(load)
watch(() => route.params.id, load)
</script>

<style scoped>
.profile-avatar {
  width: 80px;
  height: 80px;
  border-radius: 18px;
  object-fit: cover;
}
.level-progress-card {
  background: #f8fafc;
  border: 1px solid rgba(20, 32, 51, 0.08);
  border-radius: 10px;
  padding: 14px;
}
.stat-card {
  background: #f8fafc;
  border: 1px solid rgba(20, 32, 51, 0.08);
  border-radius: 10px;
  padding: 12px;
  text-align: center;
}
.stat-label {
  font-size: 11px;
  color: #7a869c;
  margin-bottom: 4px;
}
.stat-value {
  font-size: 18px;
  font-weight: 700;
  color: #142033;
}
.level-benefit-row {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 14px 18px;
  border-bottom: 1px solid rgba(20, 32, 51, 0.08);
  transition: background 0.2s ease;
}
.level-benefit-row:last-child {
  border-bottom: none;
}
.level-benefit-row.current {
  background: rgba(13, 148, 136, 0.06);
  box-shadow: inset 3px 0 0 #0d9488;
}
.level-benefit-row.unlocked {
  opacity: 1;
}
.level-benefit-row:not(.unlocked) {
  opacity: 0.5;
}
.benefit-level-badge {
  width: 52px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(13, 148, 136, 0.12);
  color: #0f766e;
  font-size: 12px;
  font-weight: 700;
  border-radius: 8px;
  flex-shrink: 0;
}
.benefit-level-badge.lv-high {
  background: rgba(232, 165, 75, 0.18);
  color: #a16207;
}
.benefit-info {
  min-width: 60px;
  flex-shrink: 0;
}
.benefit-name {
  font-weight: 600;
  font-size: 14px;
}
.benefit-points {
  font-size: 11px;
  color: #7a869c;
}
.benefit-perks {
  flex: 1;
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.perk-tag {
  display: inline-block;
  padding: 2px 8px;
  background: #f0f4f8;
  border-radius: 6px;
  font-size: 11px;
  color: #3d4a63;
  white-space: nowrap;
}
.benefit-status {
  flex-shrink: 0;
  font-size: 12px;
  font-weight: 600;
  min-width: 52px;
  text-align: right;
}
.status-unlocked {
  color: #0d9488;
}
.status-next {
  color: #e8a54b;
}
.status-locked {
  color: #7a869c;
}

/* Purchase history */
.purchase-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 18px;
  border-bottom: 1px solid rgba(20,32,51,0.08);
  transition: background 0.15s;
}
.purchase-row:last-child { border-bottom: none; }
.purchase-row:hover { background: #f8fafc; }
.purchase-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--ink, #142033);
  text-decoration: none;
}
.purchase-title:hover { color: var(--accent, #0d9488); }
.purchase-meta {
  font-size: 11px;
  color: #7a869c;
  margin-top: 2px;
}
.purchase-cost {
  font-size: 14px;
  font-weight: 700;
  color: #dc2626;
  white-space: nowrap;
}

/* Tab navigation */
.tab-btn {
  background: none;
  border: none;
  padding: 4px 10px;
  font-size: 13px;
  font-weight: 600;
  color: #7a869c;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.15s;
}
.tab-btn:hover { color: #142033; background: #f0f4f8; }
.tab-btn.active { color: #0d9488; background: rgba(13,148,136,0.1); }

/* Activity snippet */
.activity-snippet {
  font-size: 12px;
  color: #3d4a63;
  margin-top: 3px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 100%;
}

.text-accent { color: #0d9488; }
.vip-tag {
  display: inline-block;
  font-size: 11px;
  font-weight: 800;
  padding: 2px 6px;
  border-radius: 4px;
  background: #fef3c7;
  color: #b45309;
}
.follow-row { font-size: 13px; color: #64748b; }
.badge-row { display: flex; flex-wrap: wrap; gap: 6px; justify-content: center; }
.ubadge {
  font-size: 11px;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 999px;
  background: rgba(13,148,136,0.1);
  color: #0f766e;
}
</style>
