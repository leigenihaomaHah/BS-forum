<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 个人中心
    </div>

    <div v-if="!auth.isLoggedIn" class="auth-card" style="max-width: 420px; margin: 48px auto;">
      <h4 class="mb-1">请先登录</h4>
      <p class="text-muted mb-3" style="font-size: 13px">登录后查看草稿、历史、订阅与收藏</p>
      <button type="button" class="btn btn-forum w-100" @click="authModal.openLogin()">前往登录</button>
    </div>

    <template v-else>
      <div class="panel mb-3">
        <div class="panel-header"><span class="accent"></span>个人中心</div>
        <div class="hub-profile">
          <img :src="auth.user.avatar || defaultAvatar(auth.user.nickname)" class="hub-avatar" alt="" />
          <div>
            <div class="hub-name">
              <router-link :to="`/user/${auth.user.id}`">{{ auth.user.nickname }}</router-link>
              <span class="level-badge">Lv.{{ auth.user.level }} {{ auth.user.levelName }}</span>
            </div>
            <div class="hub-assets">积分 {{ auth.user.points }} · 金币 {{ auth.user.coins }}</div>
          </div>
          <router-link to="/settings" class="btn btn-sm btn-outline-secondary ms-auto">账号设置</router-link>
        </div>
      </div>

      <div class="hub-grid">
        <router-link v-for="card in cards" :key="card.to" :to="card.to" class="hub-card">
          <div class="hub-card-top">
            <span class="hub-card-title">{{ card.title }}</span>
            <span v-if="card.badge" class="hub-badge">{{ card.badge }}</span>
          </div>
          <div class="hub-card-desc">{{ card.desc }}</div>
          <div class="hub-card-stat">{{ card.stat }}</div>
        </router-link>
      </div>
    </template>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import AppLayout from '../components/AppLayout.vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import api from '../api/http'
import { defaultAvatar } from '../utils/avatar.js'

const auth = useAuthStore()
const authModal = useAuthModalStore()
const stats = ref({
  drafts: 0,
  history: 0,
  subscriptions: 0,
  favorites: 0,
  unread: 0,
})

const cards = computed(() => [
  {
    to: '/notifications',
    title: '消息中心',
    desc: '回复、关注、订阅与系统通知',
    stat: stats.value.unread ? `${stats.value.unread} 条未读` : '暂无未读',
    badge: stats.value.unread || 0,
  },
  {
    to: '/drafts',
    title: '草稿箱',
    desc: '未发布的帖子自动保存在这里',
    stat: `${stats.value.drafts} 篇草稿`,
  },
  {
    to: '/history',
    title: '浏览历史',
    desc: '最近看过的帖子',
    stat: `${stats.value.history} 条记录`,
  },
  {
    to: '/subscriptions',
    title: '版块订阅',
    desc: '订阅版块的新帖更新',
    stat: `${stats.value.subscriptions} 个版块`,
  },
  {
    to: '/me/favorites',
    title: '我的收藏',
    desc: '分类管理收藏的帖子',
    stat: `${stats.value.favorites} 个收藏`,
  },
  {
    to: '/me/threads',
    title: '我的帖子',
    desc: '查看自己发布的所有主题',
    stat: `${stats.value.myThreads} 篇帖子`,
  },
  {
    to: '/sign-in',
    title: '签到与任务',
    desc: '每日签到、任务奖励与徽章',
    stat: '去完成',
  },
  {
    to: '/recharge',
    title: '开通会员',
    desc: '月 / 季 / 年 / 永久 VIP',
    stat: auth.user?.isVip ? '已是 VIP' : '去开通',
  },
])

async function load() {
  if (!auth.isLoggedIn) return
  try {
    const [d, h, s, f, n, t] = await Promise.all([
      api.get('/me/drafts'),
      api.get('/me/history', { params: { take: 100 } }),
      api.get('/me/subscriptions'),
      api.get(`/users/${auth.user.id}/favorites`),
      api.get('/me/notifications/summary'),
      api.get('/me/threads', { params: { page: 1, pageSize: 1 } }),
    ])
    stats.value = {
      drafts: d.data?.length || 0,
      history: h.data?.length || 0,
      subscriptions: s.data?.length || 0,
      favorites: f.data?.length || 0,
      unread: n.data?.totalUnread || 0,
      myThreads: t.data?.total || 0,
    }
  } catch { console.warn('loadStats failed') }
}

watch(() => auth.isLoggedIn, load)
onMounted(load)
</script>

<style scoped>
.hub-profile {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 18px 16px;
}
.hub-avatar {
  width: 56px;
  height: 56px;
  border-radius: 50%;
  object-fit: cover;
}
.hub-name {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 16px;
  font-weight: 700;
}
.hub-name a { color: #142033; text-decoration: none; }
.hub-name a:hover { color: #0d9488; }
.hub-assets { font-size: 12px; color: #7a869c; margin-top: 4px; }
.level-badge {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 999px;
  background: #ecfdf5;
  color: #0f766e;
  font-weight: 600;
}
.hub-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
}
.hub-card {
  display: block;
  background: #fff;
  border: 1px solid rgba(20, 32, 51, 0.08);
  border-radius: 12px;
  padding: 16px;
  text-decoration: none;
  color: inherit;
  transition: border-color 0.15s, box-shadow 0.15s;
}
.hub-card:hover {
  border-color: #0d9488;
  box-shadow: 0 6px 18px rgba(13, 148, 136, 0.08);
}
.hub-card-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}
.hub-card-title { font-size: 15px; font-weight: 700; color: #142033; }
.hub-badge {
  min-width: 20px;
  height: 20px;
  padding: 0 6px;
  border-radius: 999px;
  background: #e11d48;
  color: #fff;
  font-size: 11px;
  font-weight: 700;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.hub-card-desc { font-size: 12px; color: #7a869c; line-height: 1.5; }
.hub-card-stat {
  margin-top: 12px;
  font-size: 13px;
  font-weight: 600;
  color: #0d9488;
}
@media (max-width: 900px) {
  .hub-grid { grid-template-columns: repeat(2, 1fr); }
}
@media (max-width: 560px) {
  .hub-grid { grid-template-columns: 1fr; }
  .hub-profile { flex-wrap: wrap; }
  .ms-auto { margin-left: 0 !important; }
}
</style>
