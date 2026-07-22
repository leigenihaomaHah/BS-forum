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
          <router-link to="/settings" class="btn-outline-modern ms-auto">账号设置</router-link>
        </div>
      </div>

      <div class="panel mb-3 today-panel">
        <div class="panel-header">
          <span><span class="accent"></span>今日面板</span>
          <router-link to="/sign-in" class="today-link">去签到/任务</router-link>
        </div>
        <div class="today-grid">
          <router-link to="/sign-in" class="today-chip" :class="{ done: today.signedIn }">
            <div class="chip-label">签到</div>
            <div class="chip-value">{{ today.signedIn ? `已签 · ${today.streak} 天` : '未签到' }}</div>
          </router-link>
          <router-link to="/sign-in" class="today-chip" :class="{ alert: today.claimable > 0 }">
            <div class="chip-label">待领任务</div>
            <div class="chip-value">{{ today.claimable }} 个</div>
          </router-link>
          <router-link to="/messages" class="today-chip" :class="{ alert: stats.pmUnread > 0 }">
            <div class="chip-label">未读私信</div>
            <div class="chip-value">{{ stats.pmUnread }}</div>
          </router-link>
          <router-link to="/notifications" class="today-chip" :class="{ alert: stats.unread > 0 }">
            <div class="chip-label">未读通知</div>
            <div class="chip-value">{{ stats.unread }}</div>
          </router-link>
          <router-link to="/drafts" class="today-chip" :class="{ alert: stats.drafts > 0 }">
            <div class="chip-label">草稿</div>
            <div class="chip-value">{{ stats.drafts }} 篇</div>
          </router-link>
          <router-link to="/feed" class="today-chip">
            <div class="chip-label">关注动态</div>
            <div class="chip-value">去看看</div>
          </router-link>
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
  myThreads: 0,
  pmUnread: 0,
})
const today = ref({
  signedIn: false,
  streak: 0,
  claimable: 0,
})

const cards = computed(() => [
  {
    to: '/feed',
    title: '关注动态',
    desc: '看看关注的人最近发了什么',
    stat: '查看时间线',
  },
  {
    to: '/messages',
    title: '私信',
    desc: '与其他用户一对一沟通',
    stat: stats.value.pmUnread ? `${stats.value.pmUnread} 条未读` : '查看会话',
    badge: stats.value.pmUnread || 0,
  },
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
    to: '/shop',
    title: '积分商城',
    desc: '头像框、改名卡、转盘券',
    stat: '去兑换',
  },
  {
    to: '/lottery',
    title: '幸运转盘',
    desc: '消耗金币或券抽奖',
    stat: '去抽奖',
  },
  {
    to: '/sign-in',
    title: '签到与任务',
    desc: '每日签到、任务奖励与徽章',
    stat: today.value.claimable ? `${today.value.claimable} 个可领取` : '去完成',
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
    const [d, h, s, f, n, t, pm, sign, tasks] = await Promise.all([
      api.get('/me/drafts'),
      api.get('/me/history', { params: { take: 100 } }),
      api.get('/me/subscriptions'),
      api.get('/me/favorites'),
      api.get('/me/notifications/summary'),
      api.get('/me/threads', { params: { page: 1, pageSize: 1 } }),
      api.get('/messages/unread-count'),
      api.get('/me/sign-in-status').catch(() => ({ data: null })),
      api.get('/tasks').catch(() => ({ data: [] })),
    ])
    stats.value = {
      drafts: d.data?.length || 0,
      history: h.data?.length || 0,
      subscriptions: s.data?.length || 0,
      favorites: f.data?.length || 0,
      unread: n.data?.totalUnread || 0,
      myThreads: t.data?.total || 0,
      pmUnread: pm.data?.count || 0,
    }
    const taskList = tasks.data || []
    today.value = {
      signedIn: !!(sign.data?.todaySignedIn),
      streak: sign.data?.consecutiveDays || 0,
      claimable: taskList.filter((x) => !x.claimed && x.progress >= x.target).length,
    }
  } catch { /* ignore */ }
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
.hub-name a {
  color: #142033;
  text-decoration: none;
}
.hub-assets {
  margin-top: 4px;
  font-size: 13px;
  color: #7a869c;
}
.today-panel .panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.today-link {
  font-size: 12px;
  font-weight: 600;
  color: #0d9488;
  text-decoration: none;
}
.today-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 10px;
  padding: 14px 16px 16px;
}
.today-chip {
  display: block;
  padding: 12px;
  border-radius: 12px;
  background: #f8fafc;
  border: 1px solid rgba(20, 32, 51, 0.06);
  text-decoration: none;
  transition: border-color 0.15s, background 0.15s;
}
.today-chip:hover {
  border-color: rgba(13, 148, 136, 0.35);
  background: rgba(13, 148, 136, 0.04);
}
.today-chip.done {
  background: rgba(13, 148, 136, 0.08);
  border-color: rgba(13, 148, 136, 0.2);
}
.today-chip.alert {
  background: rgba(225, 29, 72, 0.05);
  border-color: rgba(225, 29, 72, 0.18);
}
.chip-label {
  font-size: 12px;
  color: #7a869c;
}
.chip-value {
  margin-top: 4px;
  font-size: 15px;
  font-weight: 700;
  color: #142033;
}
.hub-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 12px;
}
.hub-card {
  display: block;
  padding: 16px;
  background: #fff;
  border: 1px solid rgba(20, 32, 51, 0.08);
  border-radius: 14px;
  text-decoration: none;
  transition: box-shadow 0.15s, border-color 0.15s;
}
.hub-card:hover {
  border-color: rgba(13, 148, 136, 0.35);
  box-shadow: 0 8px 24px rgba(20, 32, 51, 0.06);
}
.hub-card-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}
.hub-card-title {
  font-size: 15px;
  font-weight: 700;
  color: #142033;
}
.hub-badge {
  min-width: 18px;
  height: 18px;
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
.hub-card-desc {
  margin-top: 6px;
  font-size: 12px;
  color: #7a869c;
}
.hub-card-stat {
  margin-top: 10px;
  font-size: 13px;
  font-weight: 600;
  color: #0d9488;
}
@media (max-width: 768px) {
  .today-grid { grid-template-columns: repeat(2, 1fr); }
}
</style>
