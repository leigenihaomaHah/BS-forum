<template>
  <div v-if="auth.isLoggedIn" class="panel mb-3">
    <div class="panel-header">
      <span><span class="accent"></span>关注动态</span>
      <router-link to="/feed" class="more-link">查看全部</router-link>
    </div>
    <div v-if="loading" class="p-3 text-muted" style="font-size:13px">加载中...</div>
    <ul v-else-if="items.length" class="feed-list">
      <li v-for="f in items" :key="f.threadId + '-' + f.createdAt" class="feed-item">
        <router-link :to="`/user/${f.authorId}`" class="feed-avatar-wrap" :class="frameClass(f)">
          <img
            class="feed-avatar"
            :src="f.authorAvatar || defaultAvatar(f.authorNickname)"
            :alt="f.authorNickname"
          />
        </router-link>
        <div class="feed-body">
          <div class="feed-line">
            <router-link :to="`/user/${f.authorId}`" class="feed-author">{{ f.authorNickname }}</router-link>
            <span v-if="f.authorIsVip" class="feed-vip">VIP</span>
            <span class="level-badge" :class="{ 'lv-high': f.authorLevel >= 5 }">Lv.{{ f.authorLevel }}</span>
            <span class="feed-action">发布了</span>
            <span v-if="f.isPinned" class="type-badge type-pin">置顶</span>
            <span v-if="f.isEssence" class="type-badge type-essence">精品</span>
            <router-link :to="`/thread/${f.threadId}`" class="feed-title">{{ f.title }}</router-link>
          </div>
          <div class="feed-meta">
            <span>{{ f.forumName }}</span>
            <span>{{ formatTime(f.createdAt) }}</span>
            <span>{{ f.replyCount }} 回复</span>
            <span>{{ f.views }} 浏览</span>
            <span v-if="f.likeCount > 0">{{ f.likeCount }} 赞</span>
          </div>
        </div>
      </li>
    </ul>
    <div v-else class="p-3 text-muted" style="font-size:13px">关注用户后，这里会显示他们的新帖</div>
  </div>
</template>

<script setup>
import { onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import api from '../api/http'
import { defaultAvatar } from '../utils/avatar.js'
import { formatTime } from '../utils/time.js'

const auth = useAuthStore()
const items = ref([])
const loading = ref(false)

function frameClass(f) {
  const frame = f.authorAvatarFrame
  if (!frame) return ''
  return `avatar-frame frame-${frame}`
}

async function load() {
  if (!auth.isLoggedIn) { items.value = []; return }
  loading.value = true
  try {
    const { data } = await api.get('/feed', { params: { page: 1, pageSize: 8 } })
    items.value = data.items || []
  } catch { items.value = [] }
  finally { loading.value = false }
}

watch(() => auth.isLoggedIn, load)
onMounted(load)
</script>

<style scoped>
.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.more-link {
  font-size: 12px;
  font-weight: 600;
  color: #0d9488;
  text-decoration: none;
}
.feed-list {
  list-style: none;
  margin: 0;
  padding: 4px 0;
}
.feed-item {
  display: flex;
  gap: 12px;
  align-items: flex-start;
  padding: 12px 16px;
  border-bottom: 1px solid #f1f5f9;
}
.feed-item:last-child { border-bottom: none; }
.feed-avatar-wrap {
  flex-shrink: 0;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  overflow: hidden;
  display: block;
  background: #f1f5f9;
}
.feed-avatar-wrap.avatar-frame {
  border-radius: 50%;
  padding: 2px;
}
.feed-avatar {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
  border-radius: 50%;
}
.feed-body { min-width: 0; flex: 1; }
.feed-line {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 5px 7px;
  font-size: 13px;
  line-height: 1.45;
}
.feed-author {
  font-weight: 700;
  color: #142033;
  text-decoration: none;
}
.feed-author:hover { color: #0d9488; }
.feed-vip {
  font-size: 10px;
  font-weight: 700;
  color: #b45309;
  background: #fff7ed;
  border: 1px solid #fdba74;
  border-radius: 3px;
  padding: 0 4px;
  line-height: 1.6;
}
.feed-action { color: #64748b; }
.feed-title {
  font-weight: 600;
  color: #0f766e;
  text-decoration: none;
  word-break: break-word;
}
.feed-title:hover { text-decoration: underline; }
.feed-meta {
  margin-top: 5px;
  display: flex;
  flex-wrap: wrap;
  gap: 4px 10px;
  font-size: 12px;
  color: #94a3b8;
}
</style>
