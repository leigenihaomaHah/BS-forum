<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">帖子管理</h2>
      <div class="search-bar">
        <select v-model="status" class="form-control form-control-sm" style="width:120px" @change="load(1)">
          <option value="">全部</option>
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
      <table class="admin-table">
        <thead>
          <tr>
            <th>ID</th>
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
            <td>{{ t.id }}</td>
            <td class="td-title">{{ t.title }}</td>
            <td>
              <span v-if="t.isHidden" class="status-tag tag-danger">拉黑</span>
              <span v-if="t.repliesLocked" class="status-tag tag-warn">禁回</span>
              <span v-if="t.isPinned" class="status-tag tag-ok">置顶</span>
              <span v-if="t.isEssence" class="status-tag tag-essence">精品</span>
              <span v-if="!t.isHidden && !t.repliesLocked && !t.isPinned && !t.isEssence" class="text-muted">-</span>
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
      <div class="admin-pagination">
        <div class="pagination-info">共 {{ total }} 条</div>
        <div class="pagination-ctrl">
          <button :disabled="page <= 1" @click="load(page - 1)">上一页</button>
          <span>{{ page }} / {{ totalPages }}</span>
          <button :disabled="page >= totalPages" @click="load(page + 1)">下一页</button>
        </div>
        <div class="pagination-size">
          <select v-model.number="pageSize" @change="load(1)">
            <option :value="10">10条/页</option>
            <option :value="20">20条/页</option>
            <option :value="50">50条/页</option>
          </select>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../api/http'

const route = useRoute()
const items = ref([])
const page = ref(1)
const total = ref(0)
const pageSize = ref(20)
const search = ref('')
const allowedStatus = new Set(['hidden', 'locked', 'pinned', 'essence'])
const status = ref(allowedStatus.has(String(route.query.status || '')) ? String(route.query.status) : '')
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)))

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
    alert('已复制链接：' + url)
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
  } catch (e) { alert(e.message) }
}

async function toggleLock(t) {
  const reason = prompt(t.repliesLocked ? '解除禁回原因（可空）' : '禁止回复原因（可空）') ?? undefined
  if (reason === null) return
  try {
    await api.post(`/admin/threads/${t.id}/${t.repliesLocked ? 'unlock-replies' : 'lock-replies'}`, { reason: reason || null })
    await load(page.value)
  } catch (e) { alert(e.message) }
}

async function togglePin(t) {
  try {
    await api.post(`/admin/threads/${t.id}/${t.isPinned ? 'unpin' : 'pin'}`, {})
    await load(page.value)
  } catch (e) { alert(e.message) }
}

async function toggleEssence(t) {
  const reason = prompt(t.isEssence ? '取消精品原因（可空）' : '设为精品原因（可空）')
  if (reason === null) return
  try {
    const { data } = await api.post(`/admin/threads/${t.id}/${t.isEssence ? 'unessence' : 'essence'}`, { reason: reason || null })
    if (data?.message) alert(data.message)
    await load(page.value)
  } catch (e) { alert(e.message) }
}

async function delThread(id) {
  if (!confirm('确定删除此帖？')) return
  try {
    await api.delete(`/admin/threads/${id}`)
    await load(page.value)
  } catch (e) { alert(e.message) }
}

load(1)
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
</style>
