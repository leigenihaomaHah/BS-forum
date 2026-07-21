<template>
  <div v-if="items.length" class="panel mb-3">
    <div class="panel-header">
      <div><span class="accent"></span>精品推荐</div>
    </div>
    <ul class="essence-list">
      <li v-for="item in items" :key="item.id">
        <span class="essence-mark">精</span>
        <div class="essence-body">
          <router-link :to="`/thread/${item.id}`">{{ item.title }}</router-link>
          <div class="meta">
            {{ item.forumName }} ·
            <span class="level-badge">Lv.{{ item.authorLevel }}</span>
            {{ item.authorNickname }} ·
            {{ item.replyCount }} 回复 · {{ item.views }} 浏览
          </div>
        </div>
      </li>
    </ul>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../api/http'

const items = ref([])

onMounted(async () => {
  try {
    const { data } = await api.get('/essence', { params: { take: 8 } })
    items.value = data
  } catch {
    items.value = []
  }
})
</script>

<style scoped>
.essence-list {
  list-style: none;
  margin: 0;
  padding: 8px 0;
}
.essence-list li {
  display: flex;
  gap: 10px;
  align-items: flex-start;
  padding: 10px 16px;
  border-bottom: 1px solid #f1f5f9;
}
.essence-list li:last-child { border-bottom: none; }
.essence-mark {
  flex-shrink: 0;
  width: 22px;
  height: 22px;
  border-radius: 4px;
  background: #fef3c7;
  color: #b45309;
  font-size: 12px;
  font-weight: 800;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-top: 2px;
}
.essence-body a {
  font-weight: 600;
  color: #0f172a;
  text-decoration: none;
}
.essence-body a:hover { color: #0d9488; }
.meta {
  margin-top: 4px;
  font-size: 12px;
  color: #94a3b8;
}
</style>
