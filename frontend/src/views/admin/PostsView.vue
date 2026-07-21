<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">回复管理</h2>
      <div class="search-bar">
        <input v-model="search" class="form-control form-control-sm" placeholder="搜索内容/昵称..." @keyup.enter="load(1)" />
        <button class="admin-btn admin-btn-primary ms-1" @click="load(1)">搜索</button>
      </div>
    </div>
    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>内容</th>
            <th>所在帖子</th>
            <th>楼层</th>
            <th>作者</th>
            <th>时间</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="p in items" :key="p.id">
            <td>{{ p.id }}</td>
            <td class="td-content">{{ p.contentPreview }}</td>
            <td><router-link :to="`/thread/${p.threadId}`" target="_blank" class="post-link">{{ p.threadTitle }}</router-link></td>
            <td>#{{ p.floor }}</td>
            <td><span class="level-badge" :class="{'lv-high': p.authorLevel >= 5}">Lv.{{ p.authorLevel }}</span> {{ p.authorNickname }}</td>
            <td class="text-muted" style="font-size:12px">{{ fmt(p.createdAt) }}</td>
            <td class="ops">
              <router-link :to="`/thread/${p.threadId}`" target="_blank" class="admin-btn admin-btn-outline admin-btn-sm">查看</router-link>
              <button class="admin-btn admin-btn-danger admin-btn-sm" @click="delPost(p)">删除</button>
            </td>
          </tr>
          <tr v-if="!items.length">
            <td colspan="7" class="text-muted">暂无回复</td>
          </tr>
        </tbody>
      </table>
      <PaginationComp v-model="page" :total-pages="totalPages" />
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'
import PaginationComp from '../../components/PaginationComp.vue'

const toast = useToastStore()
const items = ref([])
const page = ref(1)
const total = ref(0)
const pageSize = 20
const search = ref('')
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

function fmt(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  return `${d.getMonth() + 1}-${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

async function load(p) {
  page.value = p || 1
  try {
    const { data } = await api.get('/admin/posts', {
      params: { page: page.value, pageSize, search: search.value || undefined },
    })
    items.value = data.items
    total.value = data.total
  } catch { items.value = [] }
}

async function delPost(p) {
  if (!confirm(`确定删除 #${p.id} 回复？`)) return
  try {
    await api.delete(`/admin/posts/${p.id}`)
    toast.success('已删除')
    await load(page.value)
  } catch (e) { toast.error(e.message) }
}

load(1)
</script>

<style scoped>
.ops { white-space: nowrap; display: flex; gap: 4px; }
.admin-btn-sm { padding: 4px 10px; font-size: 12px; }
.search-bar { display: flex; gap: 6px; align-items: center; }
.td-content { max-width: 300px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 12px; color: #64748b; }
.post-link { color: #0d9488; text-decoration: none; font-size: 12px; }
.post-link:hover { text-decoration: underline; }
</style>
