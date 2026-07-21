<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> / {{ forum?.name || (denied ? '会员专区' : '版块') }}
    </div>

    <div v-if="denied" class="panel vip-denied">
      <div class="vip-denied-inner">
        <div class="vip-denied-icon">🔒</div>
        <div class="vip-denied-title">该版块需要会员权限</div>
        <div class="vip-denied-desc">{{ denied }}</div>
        <router-link class="btn-forum mt-3" to="/recharge">开通会员</router-link>
      </div>
    </div>

    <div v-else class="panel">
      <div class="panel-header">
        <div><span class="accent"></span>{{ forum?.name || '加载中...' }}</div>
        <div class="header-actions" v-if="forum">
          <button
            v-if="auth.isLoggedIn"
            class="btn-outline-modern"
            @click="toggleSub"
          >{{ subscribed ? '已订阅' : '订阅版块' }}</button>
          <router-link class="btn-forum" :to="`/forum/${forum.id}/new`">发帖</router-link>
        </div>
      </div>

      <div class="sort-bar">
        <button
          v-for="opt in sortOptions"
          :key="opt.value"
          class="sort-btn"
          :class="{ active: sort === opt.value }"
          @click="setSort(opt.value)"
        >{{ opt.label }}</button>
      </div>
      <div v-if="loading" class="p-3 text-muted">加载中...</div>
      <template v-else>
        <div v-if="!items.length" class="p-3 text-muted">暂无主题，来发第一帖吧</div>
        <div v-for="t in items" :key="t.id" class="thread-row">
          <div class="title">
            <router-link :to="`/thread/${t.id}`">{{ t.title }}</router-link>
            <span v-if="t.isPinned" class="type-badge type-pin">置顶</span>
            <span v-if="t.isEssence" class="type-badge type-essence">精品</span>
            <span v-if="t.type === 'private'" class="type-badge type-private">私密</span>
            <span v-if="t.type === 'coin'" class="type-badge type-coin">金币</span>
            <div class="meta mt-1">
              <span class="level-badge">Lv.{{ t.authorLevel }}</span>
              {{ t.authorNickname }}
            </div>
          </div>
          <div class="meta">{{ t.replyCount }} 回复 / {{ t.views }} 浏览</div>
          <div class="meta">{{ formatTime(t.lastReplyAt, false) }}</div>
        </div>
        <PaginationComp v-model="page" :total-pages="totalPages" />
      </template>
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
import { formatTime } from '../utils/time.js'

const route = useRoute()
const auth = useAuthStore()
const toast = useToastStore()
const forum = ref(null)
const items = ref([])
const page = ref(1)
const total = ref(0)
const pageSize = 20
const loading = ref(true)
const subscribed = ref(false)
const denied = ref('')
const sort = ref('latest')
const sortOptions = [
  { value: 'latest', label: '最新回复' },
  { value: 'newest', label: '最新发布' },
  { value: 'hot', label: '最热' },
  { value: 'essence', label: '精华' },
  { value: 'replies', label: '最多回复' },
]

function setSort(v) {
  if (sort.value === v) return
  sort.value = v
  load(1)
}
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

async function loadSub(id) {
  if (!auth.isLoggedIn) {
    subscribed.value = false
    return
  }
  try {
    const { data } = await api.get(`/forums/${id}/subscribed`)
    subscribed.value = !!data.subscribed
    await api.post(`/forums/${id}/read`)
  } catch {
    subscribed.value = false
  }
}

async function toggleSub() {
  if (!forum.value) return
  try {
    const { data } = await api.post(`/forums/${forum.value.id}/subscribe`)
    subscribed.value = !!data.subscribed
  } catch (e) {
    toast.error(e.response?.data?.message || '操作失败')
  }
}

async function load(p = 1) {
  loading.value = true
  page.value = p
  denied.value = ''
  forum.value = null
  items.value = []
  const id = route.params.id
  try {
    const [f, t] = await Promise.all([
      api.get(`/forums/${id}`),
      api.get(`/forums/${id}/threads`, { params: { page: p, pageSize, sort: sort.value } })
    ])
    forum.value = f.data
    items.value = t.data.items
    total.value = t.data.total
    await loadSub(id)
  } catch (e) {
    const msg = e.response?.data?.message || e.message || '无法访问该版块'
    if (e.response?.status === 403 || String(msg).includes('会员')) {
      denied.value = msg
    } else {
      denied.value = msg
    }
  } finally {
    loading.value = false
  }
}

onMounted(() => load(1))
watch(() => route.params.id, () => load(1))
watch(page, (p) => { if (p > 0) load(p) })
</script>

<style scoped>
.thread-author-avatar {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  object-fit: cover;
  vertical-align: middle;
  margin-right: 4px;
}
.vip-denied {
  min-height: 280px;
  display: flex;
  align-items: center;
  justify-content: center;
}
.vip-denied-inner {
  text-align: center;
  padding: 40px 24px;
}
.vip-denied-icon {
  font-size: 40px;
  margin-bottom: 12px;
}
.vip-denied-title {
  font-size: 18px;
  font-weight: 700;
  color: #142033;
  margin-bottom: 8px;
}
.sort-bar {
  display: flex;
  gap: 4px;
  padding: 10px 14px;
  border-bottom: 1px solid rgba(20,32,51,0.08);
  flex-wrap: wrap;
}
.sort-btn {
  padding: 4px 12px;
  border: 1px solid rgba(20,32,51,0.12);
  border-radius: 6px;
  background: #fff;
  color: #5a6a85;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s;
}
.sort-btn:hover {
  border-color: #0d9488;
  color: #0d9488;
}
.sort-btn.active {
  background: #0d9488;
  border-color: #0d9488;
  color: #fff;
}
.vip-denied-desc {
  color: #7a869c;
  font-size: 14px;
  max-width: 360px;
  margin: 0 auto;
}
</style>
