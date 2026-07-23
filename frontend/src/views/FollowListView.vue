<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link>
      &gt;
      <router-link v-if="profile" :to="`/user/${userId}`">{{ profile.nickname }}</router-link>
      <span v-else>用户</span>
      &gt; {{ isFollowers ? '粉丝' : '关注' }}
    </div>

    <div class="panel">
      <div class="panel-header">
        <span><span class="accent"></span>{{ isFollowers ? '粉丝列表' : '关注列表' }}</span>
        <div class="tab-nav d-inline-flex gap-2 ms-2">
          <router-link
            :to="`/user/${userId}/followers`"
            class="tab-btn"
            :class="{ active: isFollowers }"
          >粉丝</router-link>
          <router-link
            :to="`/user/${userId}/following`"
            class="tab-btn"
            :class="{ active: !isFollowers }"
          >关注</router-link>
        </div>
      </div>

      <div v-if="loading" class="p-4 text-muted">加载中...</div>
      <div v-else-if="!items.length" class="p-4 text-muted">暂无{{ isFollowers ? '粉丝' : '关注' }}</div>
      <div v-else>
        <div v-for="u in items" :key="u.id" class="follow-row">
          <router-link :to="`/user/${u.id}`">
            <img :src="u.avatar || defaultAvatar(u.nickname)" class="follow-avatar" alt="" />
          </router-link>
          <div class="follow-info">
            <router-link :to="`/user/${u.id}`" class="follow-nick">{{ u.nickname }}</router-link>
            <span class="level-badge" :class="{ 'lv-high': u.level >= 5 }">Lv.{{ u.level }} {{ u.levelName }}</span>
            <span v-if="u.followedByMe && u.followsMe" class="follow-badge mutual">互相关注</span>
            <span v-else-if="u.followsMe" class="follow-badge">关注了你</span>
            <span v-else-if="u.followedByMe" class="follow-badge">已关注</span>
          </div>
          <button
            v-if="auth.isLoggedIn && auth.user.id !== u.id"
            class="btn-sm ms-auto"
            :class="u.followedByMe ? 'btn-outline-modern' : 'btn-forum'"
            :disabled="toggling === u.id"
            @click="toggleFollow(u)"
          >{{ u.followedByMe ? '已关注' : '关注' }}</button>
        </div>
        <PaginationComp v-model="page" :total-pages="totalPages" />
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import PaginationComp from '../components/PaginationComp.vue'
import { useAuthStore } from '../stores/auth'
import { useToastStore } from '../stores/toast'
import { defaultAvatar } from '../utils/avatar.js'

const route = useRoute()
const auth = useAuthStore()
const toast = useToastStore()

const userId = computed(() => route.params.id)
const isFollowers = computed(() => route.name === 'user-followers')
const profile = ref(null)
const items = ref([])
const page = ref(1)
const total = ref(0)
const loading = ref(false)
const toggling = ref(null)
const pageSize = 20
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

async function loadProfile() {
  try {
    const { data } = await api.get(`/users/${userId.value}`)
    profile.value = data
  } catch {
    profile.value = null
  }
}

async function load(p) {
  if (p) page.value = p
  loading.value = true
  const endpoint = isFollowers.value ? 'followers' : 'following'
  try {
    const { data } = await api.get(`/users/${userId.value}/${endpoint}`, {
      params: { page: page.value, pageSize },
    })
    items.value = data.items || []
    total.value = data.total || 0
  } catch {
    items.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

async function toggleFollow(u) {
  toggling.value = u.id
  try {
    const { data } = await api.post(`/users/${u.id}/follow`)
    u.followedByMe = data.following
  } catch (e) {
    toast.error(e.message)
  } finally {
    toggling.value = null
  }
}

watch(page, () => load())
watch(() => [route.params.id, route.name], () => {
  page.value = 1
  loadProfile()
  load()
})

onMounted(() => {
  loadProfile()
  load()
})
</script>

<style scoped>
.tab-btn {
  padding: 4px 12px;
  font-size: 13px;
  font-weight: 600;
  color: #7a869c;
  border-radius: 6px;
  text-decoration: none;
}
.tab-btn:hover { color: #142033; background: #f0f4f8; }
.tab-btn.active { color: #0d9488; background: rgba(13,148,136,0.1); }
.follow-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--line, rgba(20,32,51,0.08));
}
.follow-avatar {
  width: 44px;
  height: 44px;
  border-radius: 10px;
  object-fit: cover;
}
.follow-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
}
.follow-nick {
  font-weight: 700;
  color: #142033;
  text-decoration: none;
}
.follow-nick:hover { color: #0d9488; }
.follow-badge {
  font-size: 11px;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 999px;
  background: #f0f4f8;
  color: #64748b;
}
.follow-badge.mutual {
  background: rgba(13,148,136,0.12);
  color: #0f766e;
}
</style>
