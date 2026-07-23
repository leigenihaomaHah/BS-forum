<template>
  <div class="admin-wrap">
    <header class="admin-topbar">
      <div class="topbar-left">
        <router-link to="/admin" class="topbar-brand">BS Forum 管理后台</router-link>
        <span class="topbar-badge">v1.0</span>
      </div>
      <div class="topbar-right">
        <span v-if="auth.user" class="topbar-user">
          <span class="level-badge" :class="{ 'lv-high': auth.user.level >= 5 }">Lv.{{ auth.user.level }}</span>
          {{ auth.user.nickname }}
        </span>
        <router-link to="/" class="topbar-link">返回前台</router-link>
        <a href="#" class="topbar-link" @click.prevent="auth.logout()">退出</a>
      </div>
    </header>

    <div class="admin-body">
      <aside class="admin-sidebar" :class="{ collapsed: sidebarCollapsed }">
        <div class="sidebar-toolbar">
          <button
            type="button"
            class="sidebar-toggle"
            :title="sidebarCollapsed ? '展开导航' : '收起导航'"
            :aria-label="sidebarCollapsed ? '展开导航' : '收起导航'"
            @click="toggleSidebar"
          >
            <span class="toggle-bars" aria-hidden="true">
              <i /><i /><i />
            </span>
          </button>
        </div>

        <nav class="sidebar-nav">
          <section
            v-for="group in navGroups"
            :key="group.name"
            class="nav-group"
            :class="{ open: expanded[group.name] || sidebarCollapsed }"
          >
            <button
              type="button"
              class="nav-group-title"
              :title="group.name"
              :aria-expanded="!!(expanded[group.name] || sidebarCollapsed)"
              @click="toggleGroup(group.name)"
            >
              <span class="group-chevron" aria-hidden="true" />
              <span class="group-name">{{ group.name }}</span>
              <span class="group-count">{{ group.items.length }}</span>
            </button>

            <div class="nav-group-body">
              <router-link
                v-for="item in group.items"
                :key="item.to"
                :to="item.to"
                class="nav-item"
                :title="item.label"
                :class="{ active: isActive(item.to) }"
              >
                <span class="nav-icon" aria-hidden="true">{{ item.icon }}</span>
                <span class="nav-label">{{ item.label }}</span>
                <span class="nav-tip">{{ item.label }}</span>
              </router-link>
            </div>
          </section>
        </nav>
      </aside>

      <main class="admin-content">
        <router-view />
      </main>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const route = useRoute()

const COLLAPSE_KEY = 'admin_sidebar_groups'
const SIDEBAR_KEY = 'admin_sidebar_collapsed'

function loadCollapseState() {
  try {
    const saved = localStorage.getItem(COLLAPSE_KEY)
    return saved ? JSON.parse(saved) : null
  } catch {
    return null
  }
}

const navGroups = [
  {
    name: '运营',
    items: [
      { to: '/admin', icon: '📊', label: '运营看板' },
      { to: '/admin/queue', icon: '⚡', label: '审核队列' },
      { to: '/admin/sensitive-words', icon: '🛡', label: '敏感词' },
      { to: '/admin/silent-users', icon: '💤', label: '沉默用户' },
      { to: '/admin/signin', icon: '✅', label: '签到统计' },
      { to: '/admin/broadcast', icon: '📢', label: '广播通知' },
    ],
  },
  {
    name: '内容管理',
    items: [
      { to: '/admin/threads', icon: '📝', label: '帖子管理' },
      { to: '/admin/posts', icon: '💬', label: '回复管理' },
      { to: '/admin/forums', icon: '📋', label: '版块管理' },
      { to: '/admin/tags', icon: '🏷️', label: '标签管理' },
      { to: '/admin/banners', icon: '🖼️', label: '首页广告' },
    ],
  },
  {
    name: '用户管理',
    items: [
      { to: '/admin/users', icon: '👥', label: '用户管理' },
      { to: '/admin/roles', icon: '🔐', label: '角色管理' },
      { to: '/admin/moderators', icon: '🛡️', label: '版主管理' },
      { to: '/admin/levels', icon: '🏅', label: '等级配置' },
      { to: '/admin/invites', icon: '📨', label: '邀请码' },
    ],
  },
  {
    name: '财务',
    items: [
      { to: '/admin/recharge', icon: '💎', label: '会员申请' },
      { to: '/admin/shop', icon: '🛒', label: '商城商品' },
      { to: '/admin/ledger', icon: '💰', label: '流水查询' },
    ],
  },
  {
    name: '审计',
    items: [
      { to: '/admin/reports', icon: '🚩', label: '举报处理' },
      { to: '/admin/modlogs', icon: '📋', label: '操作日志' },
      { to: '/admin/export', icon: '📥', label: '数据导出' },
    ],
  },
  {
    name: '系统',
    items: [
      { to: '/admin/settings', icon: '⚙️', label: '站点设置' },
    ],
  },
]

const defaultExpanded = {}
navGroups.forEach((g) => { defaultExpanded[g.name] = true })

const expanded = ref(loadCollapseState() ?? defaultExpanded)
const sidebarCollapsed = ref(localStorage.getItem(SIDEBAR_KEY) === '1')

function isActive(to) {
  if (to === '/admin') return route.path === '/admin'
  return route.path === to || route.path.startsWith(`${to}/`)
}

function toggleGroup(name) {
  if (sidebarCollapsed.value) return
  expanded.value[name] = !expanded.value[name]
  localStorage.setItem(COLLAPSE_KEY, JSON.stringify(expanded.value))
}

function toggleSidebar() {
  sidebarCollapsed.value = !sidebarCollapsed.value
  localStorage.setItem(SIDEBAR_KEY, sidebarCollapsed.value ? '1' : '0')
}
</script>

<style scoped>
.admin-wrap {
  min-height: 100vh;
  background: #f0f2f5;
  display: flex;
  flex-direction: column;
}
.admin-topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  height: 56px;
  background: #142033;
  color: #fff;
  flex-shrink: 0;
  z-index: 20;
}
.topbar-left {
  display: flex;
  align-items: center;
  gap: 10px;
}
.topbar-brand {
  font-family: 'Outfit', sans-serif;
  font-size: 18px;
  font-weight: 700;
  color: #fff;
  text-decoration: none;
}
.topbar-badge {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.12);
  color: rgba(255, 255, 255, 0.6);
}
.topbar-right {
  display: flex;
  align-items: center;
  gap: 16px;
}
.topbar-user {
  font-size: 13px;
  display: flex;
  align-items: center;
  gap: 6px;
}
.topbar-link {
  font-size: 13px;
  color: rgba(255, 255, 255, 0.7);
  text-decoration: none;
  transition: color 0.2s;
}
.topbar-link:hover {
  color: #fff;
}
.admin-body {
  display: flex;
  flex: 1;
  min-height: 0;
}

/* —— Sidebar —— */
.admin-sidebar {
  --side-w: 228px;
  --side-w-collapsed: 68px;
  width: var(--side-w);
  background:
    radial-gradient(120% 60% at 0% 0%, rgba(13, 148, 136, 0.06), transparent 55%),
    linear-gradient(180deg, #ffffff 0%, #f7fafc 100%);
  border-right: 1px solid #e5e7eb;
  padding: 8px 0 16px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  transition: width 0.24s cubic-bezier(0.22, 1, 0.36, 1);
  overflow: visible;
  position: relative;
  z-index: 10;
}
.admin-sidebar.collapsed {
  width: var(--side-w-collapsed);
}

.sidebar-toolbar {
  display: flex;
  justify-content: flex-end;
  padding: 4px 12px 10px;
}
.admin-sidebar.collapsed .sidebar-toolbar {
  justify-content: center;
  padding: 4px 0 10px;
}

.sidebar-toggle {
  width: 32px;
  height: 32px;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  background: #fff;
  color: #64748b;
  cursor: pointer;
  display: grid;
  place-items: center;
  box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04);
  transition: border-color 0.15s, background 0.15s, color 0.15s, transform 0.15s;
}
.sidebar-toggle:hover {
  color: #0f766e;
  border-color: #99f6e4;
  background: #f0fdfa;
}
.sidebar-toggle:active {
  transform: scale(0.96);
}
.toggle-bars {
  display: flex;
  flex-direction: column;
  gap: 3px;
  width: 14px;
}
.toggle-bars i {
  display: block;
  height: 2px;
  border-radius: 2px;
  background: currentColor;
  transition: transform 0.22s ease, width 0.22s ease, opacity 0.22s ease;
}
.toggle-bars i:nth-child(1),
.toggle-bars i:nth-child(3) {
  width: 14px;
}
.toggle-bars i:nth-child(2) {
  width: 10px;
}
.admin-sidebar.collapsed .toggle-bars i:nth-child(1) {
  transform: translateY(5px) rotate(45deg);
}
.admin-sidebar.collapsed .toggle-bars i:nth-child(2) {
  opacity: 0;
  width: 0;
}
.admin-sidebar.collapsed .toggle-bars i:nth-child(3) {
  transform: translateY(-5px) rotate(-45deg);
}

.sidebar-nav {
  display: flex;
  flex-direction: column;
  gap: 4px;
  overflow-y: auto;
  overflow-x: hidden;
  flex: 1;
  padding: 0 8px 8px;
  scrollbar-width: thin;
}
.admin-sidebar.collapsed .sidebar-nav {
  padding: 0 6px 8px;
  overflow: visible;
}

.nav-group {
  margin-top: 2px;
}
.nav-group-title {
  width: 100%;
  padding: 8px 10px;
  margin: 0;
  border: 0;
  background: transparent;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: #94a3b8;
  cursor: pointer;
  user-select: none;
  display: flex;
  align-items: center;
  gap: 8px;
  border-radius: 10px;
  transition: color 0.15s, background 0.15s;
}
.nav-group-title:hover {
  color: #475569;
  background: rgba(15, 23, 42, 0.035);
}
.group-chevron {
  width: 8px;
  height: 8px;
  border-right: 1.5px solid currentColor;
  border-bottom: 1.5px solid currentColor;
  transform: rotate(-45deg);
  transition: transform 0.2s ease;
  flex-shrink: 0;
  opacity: 0.7;
}
.nav-group.open .group-chevron {
  transform: rotate(45deg);
}
.group-name {
  flex: 1;
  text-align: left;
  white-space: nowrap;
  overflow: hidden;
}
.group-count {
  font-size: 10px;
  font-weight: 600;
  min-width: 18px;
  height: 18px;
  padding: 0 5px;
  border-radius: 999px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: #f1f5f9;
  color: #94a3b8;
}
.admin-sidebar.collapsed .group-name,
.admin-sidebar.collapsed .group-count,
.admin-sidebar.collapsed .group-chevron {
  display: none;
}
.admin-sidebar.collapsed .nav-group-title {
  justify-content: center;
  padding: 6px 0;
  pointer-events: none;
  height: 8px;
  margin: 6px 10px 2px;
  border-radius: 0;
  background: transparent;
  position: relative;
}
.admin-sidebar.collapsed .nav-group-title::after {
  content: '';
  display: block;
  width: 18px;
  height: 1px;
  background: #e2e8f0;
}

.admin-sidebar:not(.collapsed) .nav-group-body {
  display: flex;
  flex-direction: column;
  gap: 2px;
  max-height: 0;
  opacity: 0;
  overflow: hidden;
  transform: translateY(-4px);
  transition: max-height 0.26s ease, opacity 0.18s ease, transform 0.18s ease;
  padding-bottom: 0;
}
.admin-sidebar:not(.collapsed) .nav-group.open .nav-group-body {
  max-height: 360px;
  opacity: 1;
  transform: none;
  padding-bottom: 4px;
}
.admin-sidebar.collapsed .nav-group-body {
  display: flex;
  flex-direction: column;
  gap: 2px;
  max-height: none;
  opacity: 1;
  overflow: visible;
  transform: none;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 12px;
  color: #334155;
  text-decoration: none;
  font-size: 13px;
  font-weight: 500;
  transition: background 0.15s, color 0.15s, box-shadow 0.15s;
  border-radius: 10px;
  position: relative;
}
.admin-sidebar.collapsed .nav-item {
  justify-content: center;
  padding: 10px 0;
}
.nav-item:hover {
  background: #f1f5f9;
  color: #0f172a;
}
.nav-item.active {
  background: rgba(13, 148, 136, 0.12);
  color: #0f766e;
  font-weight: 600;
  box-shadow: inset 3px 0 0 #0d9488;
}
.admin-sidebar.collapsed .nav-item.active {
  box-shadow: none;
  background: rgba(13, 148, 136, 0.16);
}
.nav-icon {
  font-size: 16px;
  width: 22px;
  text-align: center;
  flex-shrink: 0;
  line-height: 1;
}
.nav-label {
  font-size: 13px;
  white-space: nowrap;
  overflow: hidden;
}
.admin-sidebar.collapsed .nav-label {
  display: none;
}

/* floating tip when collapsed */
.nav-tip {
  display: none;
}
.admin-sidebar.collapsed .nav-tip {
  display: none;
  position: absolute;
  left: calc(100% + 10px);
  top: 50%;
  transform: translateY(-50%);
  background: #0f172a;
  color: #fff;
  font-size: 12px;
  font-weight: 500;
  padding: 6px 10px;
  border-radius: 8px;
  white-space: nowrap;
  pointer-events: none;
  box-shadow: 0 8px 20px rgba(15, 23, 42, 0.18);
  z-index: 40;
}
.admin-sidebar.collapsed .nav-tip::before {
  content: '';
  position: absolute;
  right: 100%;
  top: 50%;
  transform: translateY(-50%);
  border: 5px solid transparent;
  border-right-color: #0f172a;
}
.admin-sidebar.collapsed .nav-item:hover .nav-tip {
  display: block;
}

.admin-content {
  flex: 1;
  padding: 24px;
  overflow-y: auto;
}
</style>
