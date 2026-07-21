<template>
  <div class="forum-section" :id="`cat-${category.id}`">
    <div class="forum-section-hd" @click="open = !open">
      <div>
        <span class="section-accent"></span>
        <span class="me-1">{{ category.icon }}</span>
        {{ category.name }}
      </div>
      <span class="toggle-icon">{{ open ? '−' : '+' }}</span>
    </div>
    <div v-show="open" class="forum-grid">
      <div
        v-for="forum in category.forums"
        :key="forum.id"
        class="forum-item"
        :class="{ full: forum.fullWidth, locked: forum.locked }"
      >
        <div class="forum-icon">{{ forum.icon || '📁' }}</div>
        <div style="flex: 1; min-width: 0">
          <div class="forum-title">
            <router-link :to="`/forum/${forum.id}`" :class="{ 'locked-link': forum.locked }" :title="forum.accessLabel">
              {{ forum.name }}
            </router-link>
            <span v-if="forum.todayThreadCount && !forum.locked" class="today">({{ forum.todayThreadCount }})</span>
            <span v-if="forum.minVipTier > 0" class="vip-gate" :class="{ need: forum.locked }">
              {{ forum.locked ? '🔒' : '✦' }} {{ forum.accessLabel || '会员可见' }}
            </span>
          </div>
          <div class="forum-stats">
            主题 {{ forum.threadCount }} · 帖数 {{ forum.postCount }}
          </div>
          <div v-if="forum.description" class="forum-desc">{{ forum.description }}</div>
          <div v-if="forum.locked" class="forum-lock-hint">
            开通对应会员后可进入 ·
            <router-link to="/recharge">去开通</router-link>
          </div>
        </div>
        <div v-if="!forum.locked && forum.latestThread" class="forum-latest">
          <router-link :to="`/thread/${forum.latestThread.id}`">
            {{ forum.latestThread.title }}
          </router-link>
          <div class="text-muted mt-1">{{ formatTime(forum.latestThread.createdAt) }}</div>
          <div>
            <span class="level-badge">Lv.{{ forum.latestThread.authorLevel }}</span>
            {{ forum.latestThread.authorNickname }}
          </div>
        </div>
        <div v-else-if="forum.locked" class="forum-latest text-muted lock-latest">会员专区</div>
        <div v-else class="forum-latest text-muted">暂无帖子</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'

defineProps({
  category: { type: Object, required: true }
})

const open = ref(true)

function formatTime(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getMonth() + 1}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}
</script>

<style scoped>
.forum-item.locked {
  opacity: 0.92;
  background: linear-gradient(135deg, #fffbeb 0%, #ffffff 60%);
}
.vip-gate {
  display: inline-block;
  margin-left: 8px;
  padding: 1px 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 600;
  color: #0f766e;
  background: #ccfbf1;
  vertical-align: middle;
}
.vip-gate.need {
  color: #b45309;
  background: #fef3c7;
}
.forum-lock-hint {
  margin-top: 4px;
  font-size: 12px;
  color: #92400e;
}
.forum-lock-hint a {
  color: #0d9488;
  font-weight: 600;
}
.locked-link {
  color: #92400e;
}
.lock-latest {
  align-self: center;
  font-size: 13px;
}
</style>
