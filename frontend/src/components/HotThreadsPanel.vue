<template>
  <div class="panel">
    <div class="panel-header">
      <div><span class="accent"></span>热门话题</div>
      <ul class="nav hot-tabs">
        <li class="nav-item" v-for="t in tabs" :key="t.key">
          <a
            class="nav-link"
            href="#"
            :class="{ active: period === t.key }"
            @click.prevent="load(t.key)"
          >{{ t.label }}</a>
        </li>
      </ul>
    </div>
    <div v-if="loading" class="p-3 text-muted">加载中...</div>
    <ul v-else-if="items.length" class="hot-list">
      <li v-for="(item, idx) in items" :key="item.id">
        <span class="hot-rank" :class="{ top: idx < 3 }">{{ idx + 1 }}</span>
        <div>
          <router-link :to="`/thread/${item.id}`">{{ item.title }}</router-link>
          <span v-if="item.type === 'private'" class="type-badge type-private">私密</span>
          <span v-if="item.type === 'coin'" class="type-badge type-coin">金币</span>
          <span v-if="item.isEssence" class="type-badge type-essence">精品</span>
          <div class="text-muted" style="font-size: 12px">
            {{ item.forumName }} ·
            <span class="level-badge">Lv.{{ item.authorLevel }}</span>
            {{ item.authorNickname }}
          </div>
        </div>
        <div class="hot-meta">
          热度 {{ item.heat }} · {{ item.replyCount }} 回复 · {{ item.views }} 浏览
        </div>
      </li>
    </ul>
    <div v-else class="p-3 text-muted">该时间段暂无帖子</div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../api/http'

const tabs = [
  { key: 'day', label: '当日' },
  { key: 'week', label: '本周' },
  { key: 'month', label: '本月' }
]

const period = ref('day')
const items = ref([])
const loading = ref(false)

async function load(p = period.value) {
  period.value = p
  loading.value = true
  try {
    const { data } = await api.get('/hot', { params: { period: p } })
    items.value = data
  } catch {
    items.value = []
  } finally {
    loading.value = false
  }
}

onMounted(() => load('day'))
</script>

<style scoped>
.type-badge {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
  margin-left: 4px;
  vertical-align: middle;
  font-weight: 600;
}
.type-private { background: #f3e8ff; color: #7c3aed; }
.type-coin { background: #fef3c7; color: #b45309; }
.type-essence { background: #fef3c7; color: #b45309; }
</style>
