<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 积分商城
    </div>
    <div class="panel">
      <div class="panel-header">
        <div><span class="accent"></span>积分商城</div>
        <div v-if="auth.isLoggedIn" class="assets">
          积分 {{ auth.user.points }} · 金币 {{ auth.user.coins }}
          <span v-if="auth.user.isVip" class="vip">VIP</span>
          <span v-if="auth.user.lotteryTickets">券 {{ auth.user.lotteryTickets }}</span>
        </div>
      </div>
      <div class="p-3">
        <div v-if="!auth.isLoggedIn" class="text-muted mb-3">登录后可购买</div>
        <div class="shop-grid">
          <div v-for="item in items" :key="item.id" class="shop-card">
            <div class="name">{{ item.name }}</div>
            <div class="desc">{{ item.description }}</div>
            <div class="price">
              {{ item.price }} {{ item.currency === 'coins' ? '金币' : '积分' }}
            </div>
            <button class="btn btn-forum btn-sm" :disabled="!auth.isLoggedIn || buying === item.id" @click="buy(item)">
              {{ buying === item.id ? '购买中…' : '购买' }}
            </button>
          </div>
        </div>
        <div v-if="msg" class="mt-3" :class="err ? 'text-danger' : 'text-success'" style="font-size:13px">{{ msg }}</div>
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AppLayout from '../components/AppLayout.vue'
import { useAuthStore } from '../stores/auth'
import api from '../api/http'

const auth = useAuthStore()
const items = ref([])
const buying = ref(null)
const msg = ref('')
const err = ref(false)

async function load() {
  const { data } = await api.get('/shop')
  items.value = data
}

async function buy(item) {
  buying.value = item.id
  msg.value = ''
  err.value = false
  try {
    const { data } = await api.post(`/shop/${item.id}/buy`)
    msg.value = data.message
    await auth.fetchMe()
  } catch (e) {
    err.value = true
    msg.value = e.message
  } finally {
    buying.value = null
  }
}

onMounted(load)
</script>

<style scoped>
.assets { font-size: 12px; color: #64748b; display: flex; gap: 8px; align-items: center; }
.vip { background: #fef3c7; color: #b45309; padding: 1px 6px; border-radius: 4px; font-weight: 700; }
.shop-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 12px;
}
.shop-card {
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 14px;
  background: #fafbfc;
}
.name { font-weight: 700; margin-bottom: 6px; }
.desc { font-size: 12px; color: #64748b; min-height: 36px; margin-bottom: 10px; }
.price { font-weight: 700; color: #0f766e; margin-bottom: 10px; }
</style>
