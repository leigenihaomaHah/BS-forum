<template>
  <div class="admin-wrap">
    <!-- Top bar -->
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
      <!-- Sidebar -->
      <aside class="admin-sidebar">
        <nav class="sidebar-nav">
          <router-link
            v-for="item in navItems"
            :key="item.to"
            :to="item.to"
            class="nav-item"
            :class="{ active: $route.path === item.to || ($route.path.startsWith(item.to + '/') && item.to !== '/admin') }"
          >
            <span class="nav-icon">{{ item.icon }}</span>
            <span class="nav-label">{{ item.label }}</span>
          </router-link>
        </nav>
      </aside>

      <!-- Content -->
      <main class="admin-content">
        <router-view />
      </main>
    </div>
  </div>
</template>

<script setup>
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()

const navItems = [
  { to: '/admin', icon: '📊', label: '运营看板' },
  { to: '/admin/users', icon: '👥', label: '用户管理' },
  { to: '/admin/forums', icon: '📋', label: '版块管理' },
  { to: '/admin/threads', icon: '📝', label: '帖子管理' },
  { to: '/admin/levels', icon: '🏅', label: '等级配置' },
  { to: '/admin/signin', icon: '✅', label: '签到统计' },
  { to: '/admin/roles', icon: '🔐', label: '角色管理' },
  { to: '/admin/reports', icon: '🚩', label: '举报处理' },
  { to: '/admin/moderators', icon: '🛡️', label: '版主管理' },
  { to: '/admin/banners', icon: '🖼️', label: '首页广告' },
  { to: '/admin/recharge', icon: '💎', label: '会员申请' },
  { to: '/admin/shop', icon: '🛒', label: '商城商品' },
  { to: '/admin/tags', icon: '🏷️', label: '标签管理' },
  { to: '/admin/posts', icon: '💬', label: '回复管理' },
  { to: '/admin/ledger', icon: '📊', label: '流水查询' },
  { to: '/admin/invites', icon: '📨', label: '邀请码' },
  { to: '/admin/settings', icon: '⚙️', label: '站点设置' },
  { to: '/admin/export', icon: '📥', label: '数据导出' },
  { to: '/admin/broadcast', icon: '📢', label: '广播通知' },
  { to: '/admin/modlogs', icon: '📋', label: '操作日志' },
]
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
  background: rgba(255,255,255,0.12);
  color: rgba(255,255,255,0.6);
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
  color: rgba(255,255,255,0.7);
  text-decoration: none;
  transition: color 0.2s;
}
.topbar-link:hover {
  color: #fff;
}
.admin-body {
  display: flex;
  flex: 1;
}
.admin-sidebar {
  width: 200px;
  background: #fff;
  border-right: 1px solid #e8e8e8;
  padding: 12px 0;
  flex-shrink: 0;
}
.sidebar-nav {
  display: flex;
  flex-direction: column;
}
.nav-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 20px;
  color: #3d4a63;
  text-decoration: none;
  font-size: 14px;
  font-weight: 500;
  transition: all 0.2s;
  border-left: 3px solid transparent;
}
.nav-item:hover {
  background: #f5f7fa;
  color: #142033;
}
.nav-item.active {
  background: rgba(13, 148, 136, 0.08);
  color: #0d9488;
  border-left-color: #0d9488;
  font-weight: 600;
}
.nav-icon {
  font-size: 16px;
  width: 20px;
  text-align: center;
}
.nav-label {
  font-size: 13px;
}
.admin-content {
  flex: 1;
  padding: 24px;
  overflow-y: auto;
}
</style>
