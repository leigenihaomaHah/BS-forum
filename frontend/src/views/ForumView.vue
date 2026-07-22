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
        <div class="sort-tabs">
          <button
            v-for="opt in sortOptions"
            :key="opt.value"
            class="sort-btn"
            :class="{ active: sort === opt.value }"
            @click="setSort(opt.value)"
          >{{ opt.label }}</button>
        </div>
        <form class="forum-search" @submit.prevent="doSearch">
          <input
            v-model="searchInput"
            type="search"
            placeholder="搜索本版帖子…"
            maxlength="80"
            aria-label="搜索本版帖子"
          />
          <button type="submit" class="sort-btn" :disabled="loading">搜索</button>
          <button
            v-if="query"
            type="button"
            class="sort-btn"
            @click="clearSearch"
          >清除</button>
        </form>
      </div>
      <div v-if="query && !loading" class="search-hint">
        本版搜索「{{ query }}」· 共 {{ total }} 条
      </div>
      <div v-if="loading" class="p-3 text-muted">加载中...</div>
      <template v-else>
        <div v-if="!items.length" class="p-3 text-muted">
          {{ query ? `没有找到与「${query}」相关的帖子` : '暂无主题，来发第一帖吧' }}
        </div>
        <template v-else>
          <div class="thread-table-head">
            <div class="col-subject">帖子主题</div>
            <div class="col-author">作者</div>
            <div class="col-stats">回复/查看</div>
            <div class="col-last">最后发表</div>
          </div>
          <div
            v-for="t in items"
            :key="t.id"
            class="thread-table-row"
            :class="{ pinned: t.isPinned, essence: t.isEssence }"
          >
            <div class="col-subject">
              <span class="thread-icon" :class="t.isPinned ? 'icon-pin' : (t.isEssence ? 'icon-essence' : 'icon-normal')" :title="t.isPinned ? '置顶' : (t.isEssence ? '精品' : '普通')">
                {{ t.isPinned ? '▲' : (t.isEssence ? '★' : '▤') }}
              </span>
              <div class="subject-main">
                <div class="subject-line">
                  <span v-if="t.isPinned" class="type-badge type-pin">置顶</span>
                  <span v-if="t.isEssence" class="type-badge type-essence">精品</span>
                  <router-link
                    class="subject-title"
                    :class="{ 'title-pin': t.isPinned, 'title-essence': !t.isPinned && t.isEssence }"
                    :to="threadLink(t)"
                    :target="openNewTab ? '_blank' : undefined"
                  >{{ t.title }}</router-link>
                  <span v-if="t.hasImage" class="img-mark" title="含图片">
                    <svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true">
                      <rect x="1.5" y="2.5" width="13" height="11" rx="1.5" fill="none" stroke="currentColor" stroke-width="1.4"/>
                      <circle cx="5.2" cy="6.2" r="1.2" fill="currentColor"/>
                      <path d="M2.5 12.2l3.2-3.4 2.1 2.1 2.6-3.1 3.1 4.4" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linejoin="round"/>
                    </svg>
                  </span>
                  <span v-if="t.type === 'poll'" class="tag-soft">投票</span>
                  <span v-if="t.type === 'private'" class="tag-soft">私密</span>
                  <span v-if="t.type === 'coin'" class="tag-soft tag-coin">金币</span>
                  <span v-if="t.likeCount > 0" class="like-soft" title="点赞">+{{ t.likeCount }}</span>
                  <span v-if="isNewThread(t.createdAt)" class="new-badge">New</span>
                  <template v-if="pageLinks(t).length">
                    <router-link
                      v-for="plink in pageLinks(t)"
                      :key="plink.p"
                      :to="`/thread/${t.id}?p=${plink.p}`"
                      class="page-link"
                      :target="openNewTab ? '_blank' : undefined"
                    >{{ plink.label }}</router-link>
                  </template>
                </div>
              </div>
            </div>
            <div class="col-author">
              <img class="author-avatar" :src="t.authorAvatar || defaultAvatar(t.authorNickname)" alt="" />
              <div class="author-meta">
                <div class="author-name">{{ t.authorNickname }}</div>
                <div class="author-date">{{ formatDateOnly(t.createdAt) }}</div>
              </div>
            </div>
            <div class="col-stats">
              <div class="stat-replies">{{ formatCount(t.replyCount) }}</div>
              <div class="stat-views">{{ formatCount(t.views) }}</div>
            </div>
            <div class="col-last">
              <div class="last-name">{{ t.lastReplyNickname || t.authorNickname }}</div>
              <div class="last-time">{{ formatListTime(t.lastReplyAt) }}</div>
            </div>
          </div>
        </template>
        <div class="thread-table-tools">
          <label class="new-tab-opt">
            <input v-model="openNewTab" type="checkbox" />
            新窗
          </label>
          <PaginationComp v-model="page" :total-pages="totalPages" />
        </div>
      </template>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import PaginationComp from '../components/PaginationComp.vue'
import { useAuthStore } from '../stores/auth'
import { useToastStore } from '../stores/toast'
import { formatCount, formatDateOnly, formatListTime, isNewThread } from '../utils/time.js'
import { defaultAvatar } from '../utils/avatar.js'

const route = useRoute()
const router = useRouter()
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
const openNewTab = ref(false)
const postPageSize = 20
const searchInput = ref(route.query.q?.toString() || '')
const query = ref(route.query.q?.toString()?.trim() || '')
const sortOptions = [
  { value: 'latest', label: '最新回复' },
  { value: 'newest', label: '最新发布' },
  { value: 'hot', label: '最热' },
  { value: 'essence', label: '精华' },
  { value: 'replies', label: '最多回复' },
]

function threadLink(t) {
  return `/thread/${t.id}`
}

/** 长帖分页快捷入口：至少 3 页才显示，避免单独一个「2」换行难看 */
function pageLinks(t) {
  const pages = Math.ceil((Number(t.replyCount) + 1) / postPageSize)
  if (pages < 3) return []
  const maxShow = 4
  const links = []
  for (let i = 2; i <= Math.min(pages, maxShow); i++) {
    links.push({ p: i, label: String(i) })
  }
  if (pages > maxShow) {
    links.push({ p: pages, label: `..${pages}` })
  }
  return links
}

function setSort(v) {
  if (sort.value === v) return
  sort.value = v
  load(1)
}

function syncSearchQuery(q) {
  const next = { ...route.query }
  if (q) next.q = q
  else delete next.q
  router.replace({ query: next })
}

function doSearch() {
  const q = searchInput.value.trim()
  query.value = q
  syncSearchQuery(q)
  load(1)
}

function clearSearch() {
  searchInput.value = ''
  query.value = ''
  syncSearchQuery('')
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
  items.value = []
  const id = route.params.id
  const params = { page: p, pageSize, sort: sort.value }
  if (query.value) params.q = query.value
  try {
    const tasks = [
      api.get(`/forums/${id}/threads`, { params }),
    ]
    if (!forum.value || String(forum.value.id) !== String(id)) {
      tasks.unshift(api.get(`/forums/${id}`))
    }
    const results = await Promise.all(tasks)
    if (results.length === 2) {
      forum.value = results[0].data
      items.value = results[1].data.items
      total.value = results[1].data.total
    } else {
      items.value = results[0].data.items
      total.value = results[0].data.total
    }
    await loadSub(id)
  } catch (e) {
    forum.value = null
    const msg = e.response?.data?.message || e.message || '无法访问该版块'
    denied.value = msg
  } finally {
    loading.value = false
  }
}

onMounted(() => load(1))
watch(() => route.params.id, () => {
  searchInput.value = ''
  query.value = ''
  forum.value = null
  load(1)
})
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
  gap: 10px;
  padding: 10px 14px;
  border-bottom: 1px solid rgba(20,32,51,0.08);
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
}
.sort-tabs {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}
.forum-search {
  display: flex;
  gap: 6px;
  align-items: center;
  flex: 1;
  min-width: 200px;
  max-width: 360px;
  margin-left: auto;
}
.forum-search input {
  flex: 1;
  min-width: 0;
  height: 30px;
  padding: 0 10px;
  border: 1px solid rgba(20,32,51,0.12);
  border-radius: 6px;
  font-size: 13px;
  color: #142033;
  background: #fff;
  outline: none;
}
.forum-search input:focus {
  border-color: #0d9488;
  box-shadow: 0 0 0 3px rgba(13, 148, 136, 0.12);
}
.search-hint {
  padding: 8px 14px;
  font-size: 12px;
  color: #5a6a85;
  background: rgba(13, 148, 136, 0.06);
  border-bottom: 1px solid rgba(20,32,51,0.06);
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
  white-space: nowrap;
}
.sort-btn:hover:not(:disabled) {
  border-color: #0d9488;
  color: #0d9488;
}
.sort-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
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

.thread-table-head,
.thread-table-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 132px 64px 112px;
  gap: 12px;
  align-items: center;
  padding: 9px 16px;
}

.thread-table-head {
  background: transparent;
  border-bottom: 1px solid var(--line, rgba(20,32,51,0.08));
  color: var(--muted, #7a869c);
  font-size: 12px;
  font-weight: 600;
}

.thread-table-row {
  border-bottom: 1px solid rgba(20, 32, 51, 0.045);
  transition: background 0.15s;
}
.thread-table-row:hover { background: rgba(13, 148, 136, 0.03); }
.thread-table-row.pinned { background: transparent; }

.col-subject {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  min-width: 0;
}
.thread-icon {
  flex-shrink: 0;
  width: 16px;
  text-align: center;
  margin-top: 3px;
  font-size: 11px;
  line-height: 1.4;
  opacity: 0.85;
}
.icon-pin { color: #d97706; font-weight: 700; }
.icon-essence { color: #ea580c; }
.icon-normal { color: #94a3b8; }

.subject-main { min-width: 0; flex: 1; }
.subject-line {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 5px 7px;
}
.subject-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--ink, #142033);
  text-decoration: none;
  line-height: 1.45;
  word-break: break-word;
}
.subject-title:hover { color: var(--accent, #0d9488); }
.subject-title.title-pin { color: var(--accent-deep, #0f766e); font-weight: 600; }
.subject-title.title-essence { color: #b45309; }

.img-mark {
  display: inline-flex;
  align-items: center;
  color: var(--accent, #0d9488);
  opacity: 0.85;
  vertical-align: middle;
  line-height: 0;
}
.img-mark:hover { opacity: 1; }

.tag-soft {
  font-size: 11px;
  color: var(--muted, #7a869c);
  line-height: 1;
}
.tag-soft.tag-coin { color: #ca8a04; }

.like-soft {
  font-size: 11px;
  font-weight: 600;
  color: #0f766e;
  opacity: 0.8;
}
.new-badge {
  color: #ef4444;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.02em;
}
.page-link {
  font-size: 11px;
  color: var(--muted, #7a869c);
  text-decoration: none;
  padding: 0 3px;
  border-radius: 3px;
  line-height: 1.5;
}
.page-link:hover {
  color: var(--accent, #0d9488);
  background: var(--accent-soft, rgba(13,148,136,0.12));
}

.col-author {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}
.author-avatar {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  object-fit: cover;
  flex-shrink: 0;
  opacity: 0.95;
}
.author-meta { min-width: 0; }
.author-name {
  font-size: 12px;
  color: var(--ink-soft, #3d4a63);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.author-date {
  font-size: 11px;
  color: var(--muted, #7a869c);
}

.col-stats {
  text-align: center;
  line-height: 1.3;
}
.stat-replies {
  font-size: 13px;
  font-weight: 600;
  color: var(--accent-deep, #0f766e);
}
.stat-views {
  font-size: 11px;
  color: var(--muted, #7a869c);
}

.col-last {
  text-align: left;
  min-width: 0;
  line-height: 1.3;
}
.last-name {
  font-size: 12px;
  color: var(--ink-soft, #3d4a63);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.last-time {
  font-size: 11px;
  color: var(--muted, #7a869c);
}

.thread-table-tools {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 16px 12px;
  flex-wrap: wrap;
}
.new-tab-opt {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--muted, #7a869c);
  user-select: none;
  cursor: pointer;
}

@media (max-width: 720px) {
  .forum-search {
    max-width: none;
    width: 100%;
    margin-left: 0;
  }
}

@media (max-width: 860px) {
  .thread-table-head { display: none; }
  .thread-table-row {
    grid-template-columns: 1fr 1fr;
    grid-template-areas:
      "subject subject"
      "author stats"
      "last last";
    gap: 6px 10px;
    padding: 12px 14px;
  }
  .col-subject { grid-area: subject; }
  .col-author { grid-area: author; }
  .col-stats { grid-area: stats; text-align: right; }
  .col-last {
    grid-area: last;
    display: flex;
    gap: 8px;
    align-items: baseline;
  }
  .last-time::before { content: '· '; }
}
</style>
