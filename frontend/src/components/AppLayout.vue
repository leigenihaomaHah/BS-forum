<template>
  <div class="forum-wrap">
    <header class="site-header">
      <div class="util-bar">
        <div class="util-slogan">发现社区 · 分享观点 · 一起成长</div>
        <div v-if="auth.isLoggedIn" class="util-mid">
          <span class="level-badge" :class="{ 'lv-high': auth.user.level >= 5 }">
            Lv.{{ auth.user.level }} {{ auth.user.levelName }}
          </span>
          <span class="asset-chip">积分 {{ auth.user.points }}</span>
          <span class="asset-chip">金币 {{ auth.user.coins }}</span>
          <router-link to="/recharge" class="recharge-link">开通会员</router-link>
          <span v-if="nextLevelInfo" class="next-level-hint">
            <span class="hint-text">距 Lv.{{ nextLevelInfo.level }} 差 {{ nextLevelInfo.minPoints - auth.user.points }} 分</span>
            <div class="mini-progress">
              <div class="mini-progress-bar" :style="{ width: levelProgressPct + '%' }"></div>
            </div>
          </span>
        </div>
        <div v-if="auth.isLoggedIn" class="user-assets">
          <router-link to="/messages" class="pm-link" title="私信">
            私信
            <span v-if="pmUnread > 0" class="notif-badge pm-badge">{{ pmUnread > 99 ? '99+' : pmUnread }}</span>
          </router-link>
          <div class="notif-wrapper">
            <a href="#" @click.prevent="toggleNotif" class="notif-bell" title="通知">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.73 21a2 2 0 0 1-3.46 0"/></svg>
              <span v-if="unreadCount > 0" class="notif-badge">{{ unreadCount > 99 ? '99+' : unreadCount }}</span>
            </a>
            <div v-if="showNotif" class="notif-dropdown">
              <div class="notif-dropdown-header">
                <span>通知</span>
                <button type="button" class="notif-all-read" @click.stop="markAllRead">全部已读</button>
              </div>
              <div v-if="notifLoading" class="p-2 text-muted ta-center" style="font-size:12px">加载中...</div>
              <div v-else-if="!notifications.length" class="p-2 text-muted ta-center" style="font-size:12px">暂无通知</div>
              <template v-else>
                <div
                  v-for="n in notifications.slice(0, 12)"
                  :key="n.id"
                  class="notif-item"
                  :class="{ unread: !n.read }"
                  @click="markRead(n)"
                >
                  <div class="notif-item-title">
                    <span v-if="n.type === 'reply'">💬</span>
                    <span v-else-if="n.type === 'tip'">💰</span>
                    <span v-else-if="n.type === 'mention'">📣</span>
                    <span v-else-if="n.type === 'forum'">📌</span>
                    <span v-else-if="n.type === 'follow'">👤</span>
                    <span v-else>🔔</span>
                    <router-link
                      v-if="n.type === 'follow'"
                      :to="`/user/${n.fromUserId}`"
                      @click.stop
                    >{{ n.fromNickname }}</router-link>
                    <router-link
                      v-else-if="n.threadId"
                      :to="threadLink(n)"
                      @click.stop
                    >{{ n.threadTitle || '查看' }}</router-link>
                    <span v-else>{{ n.content }}</span>
                  </div>
                  <div class="notif-item-body">{{ n.fromNickname }} {{ n.content }}</div>
                  <div class="notif-item-time">{{ timeAgo(n.createdAt) }}</div>
                </div>
              </template>
              <div class="notif-footer">
                <router-link to="/me" @click="showNotif = false">个人中心</router-link>
                <router-link to="/notifications" @click="showNotif = false">消息中心</router-link>
                <router-link to="/drafts" @click="showNotif = false">草稿箱</router-link>
              </div>
            </div>
          </div>

          <div class="user-menu-wrap">
            <button type="button" class="user-menu-trigger" @click.stop="toggleUserMenu">
              <span class="avatar-frame" :class="'frame-' + (auth.user.avatarFrame || '')">
                <img :src="auth.user.avatar || defaultAvatar(auth.user.nickname)" class="header-avatar" alt="" />
              </span>
              <span class="user-menu-name">{{ auth.user.nickname }}</span>
              <span class="user-menu-caret">▾</span>
            </button>
            <div v-if="showUserMenu" class="user-menu-dropdown" @click.stop>
              <router-link :to="`/user/${auth.user.id}`" @click="showUserMenu = false">我的主页</router-link>
              <router-link to="/me" @click="showUserMenu = false">个人中心</router-link>
              <router-link to="/settings" @click="showUserMenu = false">账号设置</router-link>
              <router-link v-if="isAdmin" to="/admin" class="admin-link" @click="showUserMenu = false">管理后台</router-link>
              <a href="#" @click.prevent="auth.logout(); showUserMenu = false">退出登录</a>
            </div>
          </div>
        </div>
        <div v-else class="user-assets">
          <a href="#" @click.prevent="authModal.openLogin()">登录</a>
          <a href="#" class="btn-text-accent" @click.prevent="authModal.openRegister()">注册</a>
        </div>
      </div>

      <div class="brand-row">
        <div>
          <router-link to="/" class="brand-logo">BS<span>Forum</span></router-link>
          <div class="brand-tagline">综合社区 · 现代论坛体验</div>
        </div>
        <form class="search-box" @submit.prevent="doSearch">
          <input v-model="searchQuery" type="text" placeholder="搜索话题、帖子..." />
          <button type="submit">搜索</button>
        </form>
      </div>

      <nav class="main-nav">
        <router-link to="/" :class="{ active: $route.name === 'home' }">首页</router-link>
        <router-link to="/feed" :class="{ active: $route.name === 'feed' }">关注</router-link>
        <router-link to="/sign-in" :class="{ active: $route.name === 'signin' }">每日签到</router-link>
        <router-link to="/shop" :class="{ active: $route.name === 'shop' }">积分商城</router-link>
        <router-link to="/invite" :class="{ active: $route.name === 'invite' }">邀请注册</router-link>
        <router-link to="/lottery" :class="{ active: $route.name === 'lottery' }">幸运转盘</router-link>
      </nav>
    </header>

    <div class="sub-nav">
      <div class="links">
        <router-link to="/">首页</router-link>
        <a
          v-for="cat in navCategories"
          :key="cat.id"
          :href="`#cat-${cat.id}`"
          @click.prevent="scrollToCategory(cat.id)"
        >{{ cat.name }}</a>
      </div>
      <div>积分决定等级 · 发帖需 Lv.2</div>
    </div>

    <main class="content-pad">
      <slot />
    </main>

    <div class="footer-bar">
      BS Forum · .NET Core + Vue3 · 演示账号 admin / admin123
    </div>

    <div class="toast-container">
      <div
        v-for="t in toast.items"
        :key="t.id"
        class="toast-notice"
        :class="'toast-' + t.type"
      >{{ t.msg }}</div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { useToastStore } from '../stores/toast'
import api from '../api/http'
import { getNextLevel, getLevelProgress, isAdminUser } from '../config/levels.js'
import { timeAgo } from '../utils/time.js'
import { defaultAvatar } from '../utils/avatar.js'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const authModal = useAuthModalStore()
const toast = useToastStore()
const searchQuery = ref('')
const navCategories = ref([])
const isAdmin = computed(() => isAdminUser(auth.user))
const showUserMenu = ref(false)

function toggleUserMenu() {
  showUserMenu.value = !showUserMenu.value
  if (showUserMenu.value) {
    showNotif.value = false
  }
}

function scrollToCategory(id) {
  if (route.name !== 'home') {
    router.push({ name: 'home', hash: `#cat-${id}` })
    return
  }
  const el = document.getElementById(`cat-${id}`)
  if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

// Notifications
const showNotif = ref(false)
const notifications = ref([])
const notifLoading = ref(false)
const unreadCount = ref(0)
const pmUnread = ref(0)
let notifTimer = null

function threadLink(n) {
  if (!n?.threadId) return '/'
  if (n.floor) return `/thread/${n.threadId}?floor=${n.floor}`
  if (n.postId) return `/thread/${n.threadId}`
  return `/thread/${n.threadId}`
}

async function refreshUnread() {
  if (!auth.isLoggedIn) return
  try {
    const [sumRes, pmRes] = await Promise.all([
      api.get('/me/notifications/summary'),
      api.get('/messages/unread-count'),
    ])
    unreadCount.value = sumRes.data.totalUnread
    pmUnread.value = pmRes.data?.count || 0
  } catch { console.warn('refreshUnread failed') }
}

async function loadNotifications() {
  if (!auth.isLoggedIn) return
  try {
    const [listRes, sumRes] = await Promise.all([
      api.get('/me/notifications'),
      api.get('/me/notifications/summary'),
    ])
    notifications.value = listRes.data
    unreadCount.value = sumRes.data.totalUnread
  } catch { console.warn('loadNotifications failed') }
}

async function toggleNotif() {
  showNotif.value = !showNotif.value
  if (showNotif.value) {
    showUserMenu.value = false
    notifLoading.value = true
    await loadNotifications()
    notifLoading.value = false
  }
}

async function markRead(n) {
  if (n.read) return
  try {
    await api.put(`/me/notifications/${n.id}/read`)
    n.read = true
    unreadCount.value = Math.max(0, unreadCount.value - 1)
  } catch { console.warn('markRead failed') }
}

async function markAllRead() {
  try {
    await api.put('/me/notifications/read-all')
    notifications.value.forEach((n) => { n.read = true })
    unreadCount.value = 0
  } catch { console.warn('markAllRead failed') }
}

async function loadNavCategories() {
  try {
    const { data } = await api.get('/categories')
    navCategories.value = (data || []).map((c) => ({ id: c.id, name: c.name }))
  } catch {
    navCategories.value = []
  }
}

function startNotifPolling() {
  clearInterval(notifTimer)
  if (!auth.isLoggedIn) return
  refreshUnread()
  notifTimer = setInterval(refreshUnread, 30000)
}

function closeOverlays(e) {
  if (!e.target.closest('.notif-wrapper')) showNotif.value = false
  if (!e.target.closest('.user-menu-wrap')) showUserMenu.value = false
}

onMounted(() => {
  loadNavCategories()
  startNotifPolling()
  document.addEventListener('click', closeOverlays)
  window.addEventListener('forum:refresh-unread', refreshUnread)
})

watch(() => auth.isLoggedIn, startNotifPolling)

onUnmounted(() => {
  clearInterval(notifTimer)
  document.removeEventListener('click', closeOverlays)
  window.removeEventListener('forum:refresh-unread', refreshUnread)
})

const nextLevelInfo = computed(() => auth.user ? getNextLevel(auth.user.points) : null)
const levelProgressPct = computed(() => auth.user ? getLevelProgress(auth.user.points) : 0)

function doSearch() {
  const q = searchQuery.value.trim()
  if (q) router.push(`/search?q=${encodeURIComponent(q)}`)
}
</script>

<style scoped>
.header-avatar {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  vertical-align: middle;
  object-fit: cover;
  display: block;
}
.user-menu-wrap { position: relative; z-index: 25; }
.user-menu-trigger {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: none;
  background: transparent;
  padding: 2px 4px;
  cursor: pointer;
  color: var(--ink-soft);
  font-size: 13px;
  font-weight: 600;
}
.user-menu-name {
  max-width: 96px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.user-menu-caret { font-size: 10px; opacity: 0.7; }
.user-menu-dropdown {
  position: absolute;
  right: 0;
  top: calc(100% + 8px);
  width: 200px;
  background: #fff;
  border: 1px solid rgba(20, 32, 51, 0.1);
  border-radius: 12px;
  box-shadow: 0 12px 32px rgba(20, 32, 51, 0.12);
  padding: 8px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.user-menu-dropdown a {
  display: block;
  padding: 8px 10px;
  border-radius: 8px;
  color: #142033 !important;
  text-decoration: none !important;
  font-size: 13px;
  font-weight: 500;
  margin-left: 0 !important;
}
.user-menu-dropdown a:hover { background: #f8fafc; }
.next-level-hint {
  display: inline-flex;
  flex-direction: column;
  gap: 3px;
  min-width: 110px;
  max-width: 150px;
  flex-shrink: 1;
}
.pm-link {
  position: relative;
  display: inline-flex;
  align-items: center;
  color: #7a869c !important;
  text-decoration: none !important;
  font-size: 13px;
  font-weight: 600;
  padding: 2px 8px;
  margin-right: 2px;
}
.pm-link:hover { color: #142033 !important; }
.pm-badge { position: absolute; top: -6px; right: -4px; }
.hint-text {
  font-size: 11px;
  color: #7a869c;
  white-space: nowrap;
}
.mini-progress {
  height: 4px;
  background: #e8edf3;
  border-radius: 4px;
  overflow: hidden;
}
.mini-progress-bar {
  height: 100%;
  background: linear-gradient(90deg, #0d9488, #14b8a6);
  border-radius: 4px;
  transition: width 0.5s ease;
}
.admin-link {
  color: #e8a54b !important;
  font-weight: 700 !important;
}
.recharge-link {
  color: #12b76a !important;
  font-weight: 700 !important;
  text-decoration: none !important;
  white-space: nowrap;
}
.recharge-link:hover { text-decoration: underline !important; }

/* Notification bell & dropdown */
.notif-wrapper { position: relative; display: inline-flex; align-items: center; z-index: 20; }
.notif-bell {
  position: relative;
  display: inline-flex;
  align-items: center;
  color: #7a869c !important;
  text-decoration: none !important;
  padding: 2px 6px;
  transition: color 0.15s;
}
.notif-bell:hover { color: #142033 !important; }
.notif-badge {
  position: absolute;
  top: -4px;
  right: -2px;
  min-width: 16px;
  height: 16px;
  padding: 0 4px;
  border-radius: 8px;
  background: #dc2626;
  color: #fff;
  font-size: 10px;
  font-weight: 700;
  line-height: 16px;
  text-align: center;
}
.notif-dropdown {
  position: absolute;
  top: 100%;
  right: 0;
  width: 340px;
  max-height: 420px;
  overflow-y: auto;
  background: #fff;
  border: 1px solid rgba(20,32,51,0.10);
  border-radius: 10px;
  box-shadow: 0 12px 32px rgba(0,0,0,0.14);
  z-index: 3000;
  margin-top: 6px;
}
.notif-dropdown-header {
  padding: 10px 14px;
  font-size: 13px;
  font-weight: 700;
  color: #142033;
  border-bottom: 1px solid rgba(20,32,51,0.08);
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.notif-all-read {
  border: none;
  background: none;
  color: #0d9488;
  font-size: 12px;
  cursor: pointer;
  padding: 0;
}
.notif-footer {
  display: flex;
  justify-content: space-around;
  padding: 10px 8px;
  border-top: 1px solid rgba(20,32,51,0.08);
  font-size: 12px;
}
.notif-footer a { color: #0d9488; text-decoration: none; }
.notif-footer a:hover { text-decoration: underline; }
.notif-item {
  padding: 10px 14px;
  border-bottom: 1px solid rgba(20,32,51,0.06);
  cursor: pointer;
  transition: background 0.1s;
}
.notif-item:last-child { border-bottom: none; }
.notif-item:hover { background: #f8fafc; }
.notif-item.unread { background: rgba(13,148,136,0.04); }
.notif-item-title { font-size: 13px; font-weight: 600; }
.notif-item-title a { color: #142033; text-decoration: none; }
.notif-item-title a:hover { color: #0d9488; }
.notif-item-body { font-size: 12px; color: #3d4a63; margin-top: 2px; }
.notif-item-time { font-size: 11px; color: #7a869c; margin-top: 2px; }
.ta-center { text-align: center; }
.toast-container {
  position: fixed;
  top: 16px;
  right: 16px;
  z-index: 99999;
  display: flex;
  flex-direction: column;
  gap: 8px;
  pointer-events: none;
}
.toast-notice {
  pointer-events: auto;
  padding: 10px 20px;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 600;
  box-shadow: 0 4px 16px rgba(0,0,0,0.14);
  animation: toast-in 0.25s ease;
  background: #142033;
  color: #fff;
}
.toast-error { background: #dc2626; color: #fff; }
.toast-success { background: #059669; color: #fff; }
@keyframes toast-in {
  from { opacity: 0; transform: translateY(-8px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>
