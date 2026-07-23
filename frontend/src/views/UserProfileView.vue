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
            <span class="avatar-frame" :class="'frame-' + (profile.avatarFrame || '')">
              <img :src="profile.avatar || defaultAvatar(profile.nickname)" class="profile-avatar mb-2" alt="" />
            </span>
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
              <router-link :to="`/user/${profile.id}/followers`" class="follow-link">粉丝 {{ profile.followerCount ?? 0 }}</router-link>
              <span> · </span>
              <router-link :to="`/user/${profile.id}/following`" class="follow-link">关注 {{ profile.followingCount ?? 0 }}</router-link>
              <button
                v-if="auth.isLoggedIn && !isSelf"
                class="btn-sm ms-2"
                :class="profile.followedByMe ? 'btn-outline-modern' : 'btn-forum'"
                @click="toggleFollow"
              >{{ profile.followedByMe ? '已关注' : '关注' }}</button>
              <button
                v-if="auth.isLoggedIn && !isSelf"
                class="btn-outline-modern ms-1"
                @click="toggleBlock"
              >{{ blocked ? '已屏蔽' : '屏蔽' }}</button>
              <button
                v-if="auth.isLoggedIn && !isSelf"
                class="btn-forum ms-1"
                @click="router.push(`/messages?userId=${profile.id}`)"
              >发私信</button>
              <router-link
                v-if="isSelf"
                to="/settings"
                class="btn-outline-modern ms-2"
              >编辑资料</router-link>
            </div>
            <div v-if="profile.signature" class="profile-signature mb-2">{{ profile.signature }}</div>
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
              <button
                v-if="showPrivateTabs"
                class="tab-btn"
                :class="{ active: activeTab === 'purchases' }"
                @click="activeTab = 'purchases'"
              >购买记录</button>
              <button
                v-if="showPrivateTabs"
                class="tab-btn"
                :class="{ active: activeTab === 'favorites' }"
                @click="activeTab = 'favorites'"
              >收藏</button>
              <button class="tab-btn" :class="{ active: activeTab === 'activity' }" @click="activeTab = 'activity'">动态</button>
              <button class="tab-btn" :class="{ active: activeTab === 'replies' }" @click="activeTab = 'replies'">回复</button>
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
              <PaginationComp v-model="purchasesPage" :total-pages="purchasesTotalPages" />
            </div>
          </div>

          <div v-if="activeTab === 'favorites'">
            <div v-if="favLoading" class="p-3 text-muted">加载中...</div>
            <div v-else-if="!favorites.length" class="p-3 text-muted">暂无收藏</div>
            <div v-else class="p-0">
              <div v-for="(f, idx) in favorites" :key="idx" class="purchase-row">
                <div class="purchase-info">
                  <router-link :to="`/thread/${f.threadId}`" class="purchase-title">{{ f.title }}</router-link>
                  <div class="purchase-meta">{{ f.forumName }} · {{ formatTime(f.createdAt) }}</div>
                </div>
              </div>
              <PaginationComp v-model="favPage" :total-pages="favTotalPages" />
            </div>
          </div>

          <div v-if="activeTab === 'activity'">
            <div v-if="actLoading" class="p-3 text-muted">加载中...</div>
            <div v-else-if="!activities.length" class="p-3 text-muted">暂无动态</div>
            <div v-else class="p-0">
              <div v-for="(a, idx) in activities" :key="idx" class="purchase-row">
                <div class="purchase-info">
                  <router-link :to="activityLink(a)" class="purchase-title">{{ a.threadTitle }}</router-link>
                  <div class="purchase-meta">
                    <span :class="a.type === 'thread' ? 'text-accent' : ''">
                      {{ a.type === 'thread' ? '发帖' : '回复' }}
                    </span>
                    · {{ a.forumName }} · {{ formatTime(a.createdAt) }}
                  </div>
                  <div class="activity-snippet">{{ a.content }}</div>
                </div>
              </div>
              <PaginationComp v-model="actPage" :total-pages="actTotalPages" />
            </div>
          </div>

          <div v-if="activeTab === 'replies'">
            <div v-if="replyLoading" class="p-3 text-muted">加载中...</div>
            <div v-else-if="!replies.length" class="p-3 text-muted">暂无回复</div>
            <div v-else class="profile-reply-list">
              <router-link
                v-for="(r, idx) in replies"
                :key="idx"
                class="profile-reply-item"
                :to="activityLink(r)"
              >
                <img
                  class="profile-reply-avatar"
                  :src="r.authorAvatar || profile.avatar || defaultAvatar(r.authorNickname || profile.nickname)"
                  alt=""
                />
                <div class="profile-reply-body">
                  <div class="profile-reply-meta">
                    <span class="profile-reply-nick">{{ r.authorNickname || profile.nickname }}</span>
                    <template v-if="r.replyToNickname">
                      <span class="profile-reply-to">回复</span>
                      <span class="profile-reply-nick">{{ r.replyToNickname }}</span>
                    </template>
                    <span class="profile-reply-time">发表于 {{ formatTime(r.createdAt) }}</span>
                  </div>
                  <div class="profile-reply-content">{{ r.content }}</div>
                  <div class="profile-reply-thread">
                    来自：{{ r.forumName }} · {{ r.threadTitle }}
                    <span v-if="r.floor"> · #{{ r.floor }}楼</span>
                  </div>
                </div>
              </router-link>
              <PaginationComp v-model="replyPage" :total-pages="replyTotalPages" />
            </div>
          </div>
        </div>
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import PaginationComp from '../components/PaginationComp.vue'
import { useAuthStore } from '../stores/auth'
import { useToastStore } from '../stores/toast'
import { getLevels, getLevel, getNextLevel, getLevelProgress } from '../config/levels.js'
import { defaultAvatar } from '../utils/avatar.js'
import { formatTime } from '../utils/time.js'

const auth = useAuthStore()
const toast = useToastStore()
const route = useRoute()
const router = useRouter()
const profile = ref(null)
const badges = ref([])
const loading = ref(true)
const allLevels = computed(() => getLevels())
const activeTab = ref('activity')
const pageSize = 10

const purchases = ref([])
const purchasesLoading = ref(false)
const purchasesPage = ref(1)
const purchasesTotal = ref(0)
const purchasesTotalPages = computed(() => Math.max(1, Math.ceil(purchasesTotal.value / pageSize)))

const favorites = ref([])
const favLoading = ref(false)
const favPage = ref(1)
const favTotal = ref(0)
const favTotalPages = computed(() => Math.max(1, Math.ceil(favTotal.value / pageSize)))

const activities = ref([])
const actLoading = ref(false)
const actPage = ref(1)
const actTotal = ref(0)
const actTotalPages = computed(() => Math.max(1, Math.ceil(actTotal.value / pageSize)))

const replies = ref([])
const replyLoading = ref(false)
const replyPage = ref(1)
const replyTotal = ref(0)
const replyTotalPages = computed(() => Math.max(1, Math.ceil(replyTotal.value / pageSize)))

const isSelf = computed(() => auth.user?.id === Number(route.params.id))
const showPrivateTabs = computed(() => isSelf.value)

const currentLevel = computed(() => profile.value ? getLevel(profile.value.points) : getLevels()[0])
const nextLevel = computed(() => profile.value ? getNextLevel(profile.value.points) : null)
const progressPct = computed(() => profile.value ? getLevelProgress(profile.value.points) : 0)

function activityLink(a) {
  if (a?.floor > 1) return `/thread/${a.threadId}?floor=${a.floor}`
  return `/thread/${a.threadId}`
}

async function loadPurchases(p) {
  if (p) purchasesPage.value = p
  purchasesLoading.value = true
  try {
    const { data } = await api.get(`/users/${route.params.id}/purchases`, {
      params: { page: purchasesPage.value, pageSize },
    })
    purchases.value = data.items || []
    purchasesTotal.value = data.total || 0
  } catch {
    purchases.value = []
    purchasesTotal.value = 0
  } finally {
    purchasesLoading.value = false
  }
}

async function loadFavorites(p) {
  if (p) favPage.value = p
  favLoading.value = true
  try {
    const { data } = await api.get(`/users/${route.params.id}/favorites`, {
      params: { page: favPage.value, pageSize },
    })
    favorites.value = data.items || []
    favTotal.value = data.total || 0
  } catch {
    favorites.value = []
    favTotal.value = 0
  } finally {
    favLoading.value = false
  }
}

async function loadActivity(p) {
  if (p) actPage.value = p
  actLoading.value = true
  try {
    const { data } = await api.get(`/users/${route.params.id}/activity`, {
      params: { page: actPage.value, pageSize },
    })
    activities.value = data.items || []
    actTotal.value = data.total || 0
  } catch {
    activities.value = []
    actTotal.value = 0
  } finally {
    actLoading.value = false
  }
}

async function loadReplies(p) {
  if (p) replyPage.value = p
  replyLoading.value = true
  try {
    const { data } = await api.get(`/users/${route.params.id}/activity`, {
      params: { page: replyPage.value, pageSize, type: 'reply' },
    })
    replies.value = data.items || []
    replyTotal.value = data.total || 0
  } catch {
    replies.value = []
    replyTotal.value = 0
  } finally {
    replyLoading.value = false
  }
}

async function loadTab(tab, resetPage = false) {
  if (tab === 'purchases') {
    if (!showPrivateTabs.value) return
    if (resetPage && purchasesPage.value !== 1) purchasesPage.value = 1
    else await loadPurchases()
  } else if (tab === 'favorites') {
    if (!showPrivateTabs.value) return
    if (resetPage && favPage.value !== 1) favPage.value = 1
    else await loadFavorites()
  } else if (tab === 'replies') {
    if (resetPage && replyPage.value !== 1) replyPage.value = 1
    else await loadReplies()
  } else if (resetPage && actPage.value !== 1) {
    actPage.value = 1
  } else {
    await loadActivity()
  }
}

async function load() {
  loading.value = true
  try {
    const { data } = await api.get(`/users/${route.params.id}`)
    profile.value = data
    blocked.value = !!data.blockedByMe
    try {
      const { data: b } = await api.get(`/users/${route.params.id}/badges`)
      badges.value = b
    } catch { badges.value = [] }
  } catch {
    profile.value = null
  } finally {
    loading.value = false
  }
  if (!showPrivateTabs.value && (activeTab.value === 'purchases' || activeTab.value === 'favorites')) {
    activeTab.value = 'activity'
  }
  await loadTab(activeTab.value, true)
}

async function toggleFollow() {
  try {
    const { data } = await api.post(`/users/${profile.value.id}/follow`)
    profile.value.followedByMe = data.following
    profile.value.followerCount = (profile.value.followerCount || 0) + (data.following ? 1 : -1)
  } catch (e) { toast.error(e.message) }
}

const blocked = ref(false)

async function toggleBlock() {
  if (blocked.value) {
    try {
      await api.delete(`/users/${profile.value.id}/block`)
      blocked.value = false
      toast.success('已取消屏蔽')
    } catch (e) { toast.error(e.message) }
  } else {
    try {
      await api.post(`/users/${profile.value.id}/block`)
      blocked.value = true
      toast.success('已屏蔽该用户')
    } catch (e) { toast.error(e.message) }
  }
}

watch(activeTab, (tab) => loadTab(tab, true))
watch(purchasesPage, () => { if (activeTab.value === 'purchases') loadPurchases() })
watch(favPage, () => { if (activeTab.value === 'favorites') loadFavorites() })
watch(actPage, () => { if (activeTab.value === 'activity') loadActivity() })
watch(replyPage, () => { if (activeTab.value === 'replies') loadReplies() })
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

.profile-reply-list {
  padding: 4px 0 8px;
}
.profile-reply-item {
  display: flex;
  gap: 12px;
  padding: 16px 18px;
  border-bottom: 1px solid rgba(20, 32, 51, 0.06);
  text-decoration: none;
  color: inherit;
  transition: background 0.15s;
}
.profile-reply-item:hover {
  background: rgba(13, 148, 136, 0.03);
}
.profile-reply-item:last-child {
  border-bottom: none;
}
.profile-reply-avatar {
  width: 40px;
  height: 40px;
  border-radius: 8px;
  object-fit: cover;
  flex-shrink: 0;
  margin-top: 2px;
}
.profile-reply-body {
  flex: 1;
  min-width: 0;
}
.profile-reply-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 6px;
  margin-bottom: 8px;
  font-size: 13px;
  line-height: 1.5;
}
.profile-reply-nick {
  font-weight: 700;
  color: #2563eb;
}
.profile-reply-to {
  color: #94a3b8;
  font-weight: 400;
}
.profile-reply-time {
  color: #94a3b8;
  font-size: 12px;
}
.profile-reply-content {
  color: #1e293b;
  font-size: 14px;
  line-height: 1.7;
  white-space: pre-wrap;
  word-break: break-word;
}
.profile-reply-thread {
  margin-top: 8px;
  font-size: 12px;
  color: #94a3b8;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.profile-reply-item:hover .profile-reply-thread {
  color: #0d9488;
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
.follow-link { color: #64748b; text-decoration: none; font-weight: 600; }
.follow-link:hover { color: #0d9488; }
.profile-signature {
  font-size: 13px;
  color: #64748b;
  font-style: italic;
  padding: 8px 12px;
  background: #f8fafc;
  border-radius: 8px;
}
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
