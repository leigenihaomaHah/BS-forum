<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 邀请注册
    </div>
    <div class="panel" style="max-width:560px;margin:0 auto">
      <div class="panel-header"><span class="accent"></span>邀请好友</div>
      <div class="p-4" v-if="!auth.isLoggedIn">
        <p class="text-muted">请先 <a href="#" @click.prevent="authModal.openLogin()">登录</a></p>
      </div>
      <div class="p-4" v-else>
        <p class="desc">好友通过你的邀请码注册，双方都有奖励：你获得 <strong>+{{ info?.rewardPoints || 10 }} 积分</strong>、<strong>+{{ info?.rewardCoins || 20 }} 金币</strong> 和 <strong>1 张转盘券</strong>；好友额外 <strong>+5 金币</strong>。</p>
        <div class="code-box" v-if="info">
          <div class="label">我的邀请码</div>
          <div class="code">{{ info.inviteCode }}</div>
          <button class="btn btn-forum btn-sm mt-2" @click="copy">复制邀请链接</button>
        </div>
        <div class="stat mt-3">已成功邀请 <strong>{{ info?.inviteCount ?? 0 }}</strong> 人</div>
        <div v-if="msg" class="text-success mt-2" style="font-size:13px">{{ msg }}</div>
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AppLayout from '../components/AppLayout.vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import api from '../api/http'

const auth = useAuthStore()
const authModal = useAuthModalStore()
const info = ref(null)
const msg = ref('')

async function load() {
  if (!auth.isLoggedIn) return
  const { data } = await api.get('/invite')
  info.value = data
}

async function copy() {
  const url = `${window.location.origin}/register?invite=${info.value.inviteCode}`
  try {
    await navigator.clipboard.writeText(url)
    msg.value = '已复制：' + url
  } catch {
    prompt('复制链接', url)
  }
}

onMounted(load)
</script>

<style scoped>
.desc { font-size: 14px; color: #475569; line-height: 1.6; }
.code-box {
  margin-top: 16px;
  padding: 16px;
  border-radius: 12px;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  text-align: center;
}
.label { font-size: 12px; color: #94a3b8; }
.code {
  font-size: 28px;
  font-weight: 800;
  letter-spacing: 0.08em;
  color: #0f766e;
  margin-top: 6px;
}
.stat { font-size: 13px; color: #64748b; }
</style>
