<template>
  <div class="home-pulse-bar" aria-label="站点动态">
    <template v-if="pulse">
      <div class="pulse-card pulse-today">
        <div class="pulse-label">今日新帖</div>
        <div class="pulse-nums">
          <span class="pulse-main">{{ pulse.todayThreads }}</span>
          <span class="pulse-unit">帖</span>
          <span class="pulse-slash">/</span>
          <span class="pulse-reply">{{ pulse.todayReplies }}</span>
          <span class="pulse-unit">回</span>
        </div>
      </div>
      <div class="pulse-card">
        <div class="pulse-label">昨日</div>
        <div class="pulse-nums">
          <span class="pulse-main">{{ pulse.yesterdayThreads }}</span>
          <span class="pulse-unit">帖</span>
          <span class="pulse-slash">/</span>
          <span class="pulse-reply">{{ pulse.yesterdayReplies }}</span>
          <span class="pulse-unit">回</span>
        </div>
      </div>
      <div
        v-if="delta != null"
        class="pulse-card pulse-cmp"
        :class="delta >= 0 ? 'up' : 'down'"
        :title="'较昨日主题 ' + (delta >= 0 ? '+' : '') + delta"
      >
        <div class="pulse-label">较昨日</div>
        <div class="pulse-nums">
          <span class="pulse-main">{{ delta >= 0 ? '+' : '' }}{{ delta }}</span>
        </div>
      </div>
    </template>
    <span v-else-if="loading" class="pulse-muted">统计加载中…</span>
    <span v-else class="pulse-muted">统计暂不可用（请重启后端）</span>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import api from '../api/http'

const pulse = ref(null)
const loading = ref(true)

const delta = computed(() => {
  if (!pulse.value) return null
  return pulse.value.todayThreads - pulse.value.yesterdayThreads
})

onMounted(async () => {
  try {
    const { data } = await api.get('/stats/pulse')
    pulse.value = data
  } catch {
    pulse.value = null
  } finally {
    loading.value = false
  }
})
</script>
