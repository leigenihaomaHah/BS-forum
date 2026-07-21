<template>
  <div class="admin-page">
      <h2 class="page-title">签到统计</h2>

      <div class="row g-4">
        <div class="col-md-4">
          <div class="admin-panel text-center">
            <div class="p-4">
              <div style="font-size:42px;font-weight:800;color:#0d9488">{{ stats.todayCount }}</div>
              <div class="text-muted mt-1">今日签到人数</div>
            </div>
          </div>
        </div>

        <div class="col-md-8">
          <div class="admin-panel">
            <div class="admin-panel-hd">连续签到分布</div>
            <div class="p-3">
              <div v-for="(count, bucket) in stats.consecutiveDist" :key="bucket" class="dist-row">
                <span class="dist-label">{{ bucketLabel(bucket) }}</span>
                <div class="dist-bar-wrap">
                  <div class="dist-bar" :style="{ width: barWidth(count) + '%' }"></div>
                </div>
                <span class="dist-count">{{ count }} 人</span>
              </div>
              <div v-if="!Object.keys(stats.consecutiveDist || {}).length" class="text-muted">暂无数据</div>
            </div>
          </div>
        </div>
      </div>

      <div class="admin-panel mt-4">
        <div class="admin-panel-hd">签到排行（Top 10）</div>
        <table class="admin-table">
          <thead>
            <tr>
              <th>#</th>
              <th>用户</th>
              <th>昵称</th>
              <th>连续签到</th>
              <th>累计签到</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(u, idx) in stats.topUsers" :key="u.userId">
              <td>{{ idx + 1 }}</td>
              <td>{{ u.username }}</td>
              <td>{{ u.nickname }}</td>
              <td>{{ u.consecutiveDays }} 天</td>
              <td>{{ u.totalDays }} 天</td>
            </tr>
          </tbody>
        </table>
        <div v-if="!stats.topUsers?.length" class="p-3 text-muted">暂无数据</div>
      </div>
    </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import api from '../../api/http'

const stats = ref({})
const maxCount = computed(() => Math.max(1, ...Object.values(stats.value.consecutiveDist || {})))

function bucketLabel(bucket) {
  const map = { '0': '未签到', '1-6': '1-6 天', '7-13': '7-13 天', '14-29': '14-29 天', '30+': '30 天以上' }
  return map[bucket] || bucket
}

function barWidth(count) {
  return (count / maxCount.value) * 100
}

onMounted(async () => {
  try {
    const { data } = await api.get('/admin/signin/stats')
    stats.value = data
  } catch { stats.value = {} }
})
</script>

<style scoped>
.dist-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 6px 0;
}
.dist-label {
  width: 80px;
  font-size: 13px;
  flex-shrink: 0;
}
.dist-bar-wrap {
  flex: 1;
  height: 20px;
  background: #f0f2f5;
  border-radius: 10px;
  overflow: hidden;
}
.dist-bar {
  height: 100%;
  background: linear-gradient(90deg, #0d9488, #14b8a6);
  border-radius: 10px;
  transition: width 0.5s ease;
  min-width: 4px;
}
.dist-count {
  width: 50px;
  font-size: 13px;
  text-align: right;
  flex-shrink: 0;
}
</style>
