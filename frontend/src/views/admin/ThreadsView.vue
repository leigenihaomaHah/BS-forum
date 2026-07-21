<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">帖子管理</h2>
      <div class="search-bar">
        <select v-model="status" class="form-control form-control-sm" style="width:120px" @change="load(1)">
          <option value="">全部</option>
          <option value="pending">待审核</option>
          <option value="hidden">已拉黑</option>
          <option value="locked">禁回复</option>
          <option value="pinned">已置顶</option>
          <option value="essence">精品</option>
        </select>
        <input v-model="search" class="form-control form-control-sm" placeholder="搜索标题/作者/版块..." @keyup.enter="load(1)" />
        <button class="admin-btn admin-btn-primary ms-1" @click="load(1)">搜索</button>
      </div>
    </div>

    <div class="admin-panel">
      <div v-if="selectedIds.length" class="batch-bar">
        <span class="batch-count">已选 {{ selectedIds.length }} 条</span>
        <button class="admin-btn admin-btn-outline admin-btn-sm" @click="batchAction('hide')">批量拉黑</button>
        <button class="admin-btn admin-btn-outline admin-btn-sm" @click="batchAction('pin')">批量置顶</button>
        <button class="admin-btn admin-btn-danger admin-btn-sm" @click="batchAction('delete')">批量删除</button>
        <button class="admin-btn admin-btn-outline admin-btn-sm" @click="selectedIds = []">取消选择</button>
      </div>
      <table class="admin-table">
        <thead>
          <tr>
            <th><input type="checkbox" :checked="allSelected" @change="toggleAll" /></th>
            <th>标题</th>
            <th>状态</th>
            <th>作者</th>
            <th>版块</th>
            <th>回复</th>
            <th>浏览</th>
            <th>创建时间</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="t in items" :key="t.id">
            <td><input type="checkbox" :value="t.id" v-model="selectedIds" /></td>
            <td>{{ t.id }}</td>
            <td class="td-title"><router-link :to="`/thread/${t.id}`" target="_blank" class="thread-link">{{ t.title }}</router-link></td>
            <td>
              <span v-if="t.pendingReview" class="status-tag tag-warn">待审</span>
              <span v-if="t.isHidden" class="status-tag tag-danger">拉黑</span>
              <span v-if="t.repliesLocked" class="status-tag tag-warn">禁回</span>
              <span v-if="t.isPinned" class="status-tag tag-ok">置顶</span>
              <span v-if="t.isEssence" class="status-tag tag-essence">精品</span>
              <span v-if="!t.pendingReview && !t.isHidden && !t.repliesLocked && !t.isPinned && !t.isEssence" class="text-muted">-</span>
            </td>
            <td>
              <span class="level-badge" :class="{ 'lv-high': t.authorLevel >= 5 }">Lv.{{ t.authorLevel }}</span>
              {{ t.authorNickname }}
            </td>
            <td>{{ t.forumName }}</td>
            <td>{{ t.replyCount }}</td>
            <td>{{ t.views }}</td>
            <td class="text-muted" style="font-size:12px">{{ fmt(t.createdAt) }}</td>
            <td class="ops">
              <button class="admin-btn admin-btn-outline" @click="copyLink(t.id)">链接</button>
              <template v-if="t.pendingReview">
                <button class="admin-btn admin-btn-primary" @click="approve(t)">通过</button>
                <button class="admin-btn admin-btn-danger" @click="reject(t)">驳回</button>
              </template>
              <button class="admin-btn admin-btn-outline" @click="moveThread(t)">移版</button>
              <button class="admin-btn admin-btn-outline" @click="toggleHide(t)">{{ t.isHidden ? '取消拉黑' : '拉黑' }}</button>
              <button class="admin-btn admin-btn-outline" @click="toggleLock(t)">{{ t.repliesLocked ? '解禁回' : '禁回' }}</button>
              <button class="admin-btn admin-btn-outline" @click="togglePin(t)">{{ t.isPinned ? '取消置顶' : '置顶' }}</button>
              <button class="admin-btn admin-btn-outline" @click="toggleEssence(t)">{{ t.isEssence ? '取消精品' : '精品' }}</button>
              <button class="admin-btn admin-btn-danger" @click="delThread(t.id)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-if="!items.length" class="p-3 text-muted">暂无帖子</div>
      <PaginationComp v-model="page" :total-pages="totalPages" />
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'
import PaginationComp from '../../components/PaginationComp.vue'

const toast = useToastStore()

const route = useRoute()
const items = ref([])
const page = ref(1)
const total = ref(0)
const pageSize = ref(20)
const search = ref('')
const allowedStatus = new Set(['hidden', 'locked', 'pinned', 'essence', 'pending'])
const status = ref(allowedStatus.has(String(route.query.status || '')) ? String(route.query.status) : '')
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)))
const selectedIds = ref([])
const forums = ref([])
const allSelected = computed(() => items.value.length > 0 && selectedIds.value.length === items.value.length)

async function loadForums() {
  try {
    const { data } = await api.get('/admin/categories')
    const list = []
    for (const c of data || []) {
      for (const f of c.forums || []) list.push({ id: f.id, name: `${c.name} / ${f.name}` })
    }
    forums.value = list
  } catch { forums.value = [] }
}

async function approve(t) {
  try {
    await api.post(`/admin/threads/${t.id}/approve`)
    toast.success('已通过')
    await load(page.value)
  } catch (e) { toast.error(e.message) }
}

async function reject(t) {
  const reason = prompt('驳回原因（可空）')
  if (reason === null) return
  try {
    await api.post(`/admin/threads/${t.id}/reject`, { reason: reason || null })
    toast.success('已驳回')
    await load(page.value)
  } catch (e) { toast.error(e.message) }
}

async function moveThread(t) {
  if (!forums.value.length) await loadForums()
  const options = forums.value.map(f => `${f.id}=${f.name}`).join('\n')
  const input = prompt(`移到哪个版块？输入版块 ID：\n${options}`, String(t.forumId || ''))
  if (input === null) return
  const forumId = Number(input)
  if (!forumId) { toast.error('无效版块'); return }
  try {
    await api.post(`/admin/threads/${t.id}/move`, { forumId })
    toast.success('已移动')
    await load(page.value)
  } catch (e) { toast.error(e.message) }
}

function toggleAll() {
  selectedIds.value = allSelected.value ? [] : items.value.map(t => t.id)
}

function fmt(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  return `${d.getMonth() + 1}-${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

async function load(p) {
  page.value = p || 1
  try {
    const { data } = await api.get('/admin/threads', {
      params: { page: page.value, pageSize: pageSize.value, search: search.value, status: status.value || undefined },
    })
    items.value = data.items
    total.value = data.total
  } catch { items.value = [] }
}

async function copyLink(id) {
  const url = `${window.location.origin}/thread/${id}`
  try {
    await navigator.clipboard.writeText(url)
    toast.success('已复制链接：' + url)
  } catch {
    prompt('复制链接', url)
  }
}

async function toggleHide(t) {
  const reason = prompt(t.isHidden ? '取消拉黑原因（可空）' : '拉黑原因（可空）') ?? undefined
  if (reason === null) return
  try {
    await api.post(`/admin/threads/${t.id}/${t.isHidden ? 'unhide' : 'hide'}`, { reason: reason || null })
    await load(page.value)
  } catch (e) { toast.error(e.message) }
}

async function toggleLock(t) {
  const reason = prompt(t.repliesLocked ? '解除禁回原因（可空）' : '禁止回复原因（可空）') ?? undefined
  if (reason === null) return
  try {
    await api.post(`/admin/threads/${t.id}/${t.repliesLocked ? 'unlock-replies' : 'lock-replies'}`, { reason: reason || null })
    await load(page.value)
  } catch (e) { toast.error(e.message) }
}

async function togglePin(t) {
  try {
    await api.post(`/admin/threads/${t.id}/${t.isPinned ? 'unpin' : 'pin'}`, {})
    await load(page.value)
  } catch (e) { toast.error(e.message) }
}

async function toggleEssence(t) {
  const reason = prompt(t.isEssence ? '取消精品原因（可空）' : '设为精品原因（可空）')
  if (reason === null) return
  try {
    const { data } = await api.post(`/admin/threads/${t.id}/${t.isEssence ? 'unessence' : 'essence'}`, { reason: reason || null })
    if (data?.message) toast.success(data.message)
    await load(page.value)
  } catch (e) { toast.error(e.message) }
}

async function delThread(id) {
  if (!confirm('确定删除此帖？')) return
  try {
    await api.delete(`/admin/threads/${id}`)
    await load(page.value)
  } catch (e) { toast.error(e.message) }
}

async function batchAction(action) {
  const labels = { hide: '拉黑', pin: '置顶', delete: '删除' }
  if (!confirm(`确定对 ${selectedIds.value.length} 个帖子执行"${labels[action] || action}"操作？`)) return
  try {
    const { data } = await api.post('/admin/threads/batch', { ids: [...selectedIds.value], action })
    toast.success(`批量操作完成`)
    selectedIds.value = []
    await load(page.value)
  } catch (e) { toast.error(e.message) }
}

loadForums()
load(1)
watch(page, (p) => { if (p > 0) load(p) })
</script>

<style scoped>
.ops { white-space: nowrap; display: flex; flex-wrap: wrap; gap: 4px; }
.status-tag {
  display: inline-block;
  padding: 1px 6px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
  margin-right: 2px;
}
.tag-danger { background: rgba(220,38,38,0.12); color: #b91c1c; }
.tag-warn { background: rgba(234,179,8,0.15); color: #a16207; }
.tag-ok { background: rgba(13,148,136,0.12); color: #0f766e; }
.tag-essence { background: rgba(217,119,6,0.14); color: #b45309; }
.search-bar { display: flex; gap: 6px; align-items: center; }
.thread-link { color: #142033; text-decoration: none; }
.thread-link:hover { color: #0d9488; text-decoration: underline; }
.batch-bar {
  display: flex; align-items: center; gap: 8px; padding: 8px 12px;
  background: #f0fdf4; border-bottom: 1px solid #bbf7d0;
}
.batch-count { font-size: 13px; font-weight: 600; color: #166534; }
.admin-btn-sm { padding: 4px 10px; font-size: 12px; }
</style>
