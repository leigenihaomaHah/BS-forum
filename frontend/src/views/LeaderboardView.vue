<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 排行榜
    </div>

    <div class="panel">
      <div class="panel-header"><span class="accent"></span>排行榜</div>
      <div class="p-3">
        <div class="tab-nav mb-3">
          <button
            v-for="m in metrics"
            :key="m.key"
            class="tab-btn"
            :class="{ active: metric === m.key }"
            @click="setMetric(m.key)"
          >{{ m.label }}</button>
        </div>

        <div v-if="metric === 'likes'" class="mb-3">
          <label class="form-label me-2">周期</label>
          <select v-model="period" class="form-control d-inline-block" style="max-width:140px" @change="load">
            <option value="all">全部</option>
            <option value="day">今日</option>
            <option value="week">本周</option>
            <option value="month">本月</option>
          </select>
        </div>

        <div v-if="loading" class="text-muted py-3">加载中...</div>
        <div v-else-if="!items.length" class="text-muted py-3">暂无数据</div>
        <div v-else class="lb-list">
          <div v-for="item in items" :key="item.userId" class="lb-row">
            <span class="lb-rank" :class="{ top: item.rank <= 3 }">{{ item.rank }}</span>
            <router-link :to="`/user/${item.userId}`" class="lb-avatar-wrap">
              <img :src="item.avatar || defaultAvatar(item.nickname)" class="lb-avatar" alt="" />
            </router-link>
            <div class="lb-info">
              <router-link :to="`/user/${item.userId}`" class="lb-nick">{{ item.nickname }}</router-link>
              <span v-if="item.isVip" class="vip-tag">VIP</span>
              <span class="level-badge" :class="{ 'lv-high': item.level >= 5 }">Lv.{{ item.level }} {{ item.levelName }}</span>
            </div>
            <div class="lb-score">{{ item.score }}</div>
          </div>
        </div>
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import { defaultAvatar } from '../utils/avatar.js'

const metrics = [
  { key: 'points', label: '积分' },
  { key: 'coins', label: '金币' },
  { key: 'essence', label: '精华' },
  { key: 'likes', label: '获赞' },
  { key: 'signin', label: '签到' },
]

const metric = ref('points')
const period = ref('all')
const items = ref([])
const loading = ref(false)

function setMetric(m) {
  metric.value = m
  if (m !== 'likes') period.value = 'all'
  load()
}

async function load() {
  loading.value = true
  try {
    const { data } = await api.get('/leaderboard', {
      params: {
        metric: metric.value,
        period: metric.value === 'likes' ? period.value : 'all',
        take: 50,
      },
    })
    items.value = data.items || []
  } catch {
    items.value = []
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.tab-nav { display: flex; flex-wrap: wrap; gap: 6px; }
.tab-btn {
  background: none;
  border: none;
  padding: 6px 14px;
  font-size: 13px;
  font-weight: 600;
  color: #7a869c;
  border-radius: 8px;
  cursor: pointer;
}
.tab-btn:hover { color: #142033; background: #f0f4f8; }
.tab-btn.active { color: #0d9488; background: rgba(13,148,136,0.1); }
.lb-list { border-top: 1px solid var(--line, rgba(20,32,51,0.08)); }
.lb-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 4px;
  border-bottom: 1px solid var(--line, rgba(20,32,51,0.06));
}
.lb-rank {
  width: 32px;
  text-align: center;
  font-weight: 800;
  font-size: 15px;
  color: #94a3b8;
  flex-shrink: 0;
}
.lb-rank.top { color: #e8a54b; }
.lb-avatar-wrap { flex-shrink: 0; }
.lb-avatar {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  object-fit: cover;
}
.lb-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
}
.lb-nick {
  font-weight: 700;
  color: #142033;
  text-decoration: none;
}
.lb-nick:hover { color: #0d9488; }
.lb-score {
  font-size: 16px;
  font-weight: 800;
  color: #0d9488;
  flex-shrink: 0;
}
.vip-tag {
  font-size: 10px;
  font-weight: 800;
  padding: 1px 5px;
  border-radius: 4px;
  background: #fef3c7;
  color: #b45309;
}
</style>
