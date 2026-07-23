<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 关注动态
    </div>

    <div v-if="!auth.isLoggedIn" class="auth-card" style="max-width: 420px; margin: 48px auto;">
      <h4 class="mb-1">请先登录</h4>
      <p class="text-muted mb-3" style="font-size: 13px">登录并关注用户后，这里会显示他们的新帖</p>
      <button type="button" class="btn btn-forum w-100" @click="authModal.openLogin()">前往登录</button>
    </div>

    <div v-else class="panel">
      <div class="panel-header">
        <span><span class="accent"></span>关注动态</span>
        <span class="text-muted" style="font-size:12px">共 {{ total }} 条</span>
      </div>
      <div v-if="loading" class="p-4 text-muted">加载中...</div>
      <div v-else-if="!items.length" class="p-4 text-muted">
        暂无动态。去用户主页关注感兴趣的人，他们的新帖会出现在这里。
      </div>
      <div v-else>
        <div v-for="f in items" :key="f.threadId + '-' + f.createdAt" class="feed-row">
          <router-link :to="`/user/${f.authorId}`" class="feed-avatar-wrap" :class="frameClass(f)">
            <img
              class="feed-avatar"
              :src="f.authorAvatar || defaultAvatar(f.authorNickname)"
              :alt="f.authorNickname"
            />
          </router-link>
          <div class="feed-content">
            <div class="feed-user">
              <router-link :to="`/user/${f.authorId}`" class="feed-author">{{ f.authorNickname }}</router-link>
              <span v-if="f.authorIsVip" class="feed-vip">VIP</span>
              <span class="level-badge" :class="{ 'lv-high': f.authorLevel >= 5 }">Lv.{{ f.authorLevel }}</span>
              <span class="feed-action">发布了主题</span>
            </div>
            <div class="feed-title-row">
              <span v-if="f.isPinned" class="type-badge type-pin">置顶</span>
              <span v-if="f.isEssence" class="type-badge type-essence">精品</span>
              <span v-if="f.type === 'private'" class="type-badge type-private">私密</span>
              <span v-if="f.type === 'coin'" class="type-badge type-coin">金币</span>
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
        </div>
        <PaginationComp v-model="page" :total-pages="totalPages" />
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import AppLayout from '../components/AppLayout.vue'
import PaginationComp from '../components/PaginationComp.vue'
import api from '../api/http'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { formatTime } from '../utils/time.js'
import { defaultAvatar } from '../utils/avatar.js'

const auth = useAuthStore()
const authModal = useAuthModalStore()
const items = ref([])
const page = ref(1)
const total = ref(0)
const pageSize = 20
const loading = ref(false)
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

function frameClass(f) {
  const frame = f.authorAvatarFrame
  if (!frame) return ''
  return `avatar-frame frame-${frame}`
}

async function load(p) {
  if (!auth.isLoggedIn) return
  if (p) page.value = p
  loading.value = true
  try {
    const { data } = await api.get('/feed', { params: { page: page.value, pageSize } })
    items.value = data.items || []
    total.value = data.total || 0
  } catch {
    items.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

onMounted(() => load(1))
watch(() => auth.isLoggedIn, () => load(1))
watch(page, () => load())
</script>

<style scoped>
.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.feed-row {
  display: flex;
  gap: 14px;
  align-items: flex-start;
  padding: 16px 18px;
  border-bottom: 1px solid rgba(20, 32, 51, 0.06);
}
.feed-row:last-child { border-bottom: none; }
.feed-avatar-wrap {
  flex-shrink: 0;
  width: 48px;
  height: 48px;
  border-radius: 50%;
  overflow: hidden;
  display: block;
  background: #f1f5f9;
}
.feed-avatar-wrap.avatar-frame {
  padding: 2px;
}
.feed-avatar {
  width: 100%;
  height: 100%;
  object-fit: cover;
  border-radius: 50%;
  display: block;
}
.feed-content { min-width: 0; flex: 1; }
.feed-user {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px 8px;
  margin-bottom: 6px;
}
.feed-author {
  font-weight: 700;
  font-size: 14px;
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
  padding: 0 5px;
  line-height: 1.6;
}
.feed-action {
  color: #64748b;
  font-size: 13px;
}
.feed-title-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px 8px;
}
.feed-title {
  font-size: 15px;
  font-weight: 600;
  color: #0f766e;
  text-decoration: none;
  line-height: 1.4;
  word-break: break-word;
}
.feed-title:hover { text-decoration: underline; }
.feed-meta {
  margin-top: 8px;
  display: flex;
  flex-wrap: wrap;
  gap: 4px 12px;
  font-size: 12px;
  color: #94a3b8;
}
</style>
