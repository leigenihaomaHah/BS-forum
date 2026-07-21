<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">举报处理</h2>
      <select v-model="status" class="form-control form-control-sm" style="width:120px" @change="load(1)">
        <option value="pending">待处理</option>
        <option value="resolved">已处理</option>
        <option value="rejected">已驳回</option>
        <option value="">全部</option>
      </select>
    </div>
    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>ID</th><th>类型</th><th>目标</th><th>原因</th><th>举报人</th><th>状态</th><th>时间</th><th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in items" :key="r.id">
            <td>{{ r.id }}</td>
            <td>{{ r.targetType }}</td>
            <td>{{ r.targetId }}</td>
            <td>{{ r.reason }}</td>
            <td>{{ r.reporterNickname }}</td>
            <td>{{ r.status }}</td>
            <td class="text-muted" style="font-size:12px">{{ fmt(r.createdAt) }}</td>
            <td class="ops" v-if="r.status === 'pending'">
              <button class="admin-btn admin-btn-outline" @click="handle(r, 'resolve')">通过</button>
              <button class="admin-btn admin-btn-outline" @click="handle(r, 'reject')">驳回</button>
              <button v-if="r.targetType === 'thread'" class="admin-btn admin-btn-danger" @click="handle(r, 'hide_thread')">拉黑帖</button>
              <button v-if="r.targetType === 'user'" class="admin-btn admin-btn-danger" @click="handle(r, 'mute_user')">禁言</button>
            </td>
            <td v-else>-</td>
          </tr>
        </tbody>
      </table>
      <div v-if="!items.length" class="p-3 text-muted">暂无</div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'

const toast = useToastStore()

const items = ref([])
const status = ref('pending')

function fmt(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  return `${d.getMonth() + 1}-${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

async function load() {
  const { data } = await api.get('/admin/reports', { params: { status: status.value || undefined, pageSize: 50 } })
  items.value = data.items
}

async function handle(r, action) {
  const note = prompt('处理备注（可空）') ?? ''
  try {
    await api.post(`/admin/reports/${r.id}/handle`, { action, note: note || null })
    await load()
  } catch (e) { toast.error(e.message) }
}

load()
</script>

<style scoped>
.ops { display: flex; flex-wrap: wrap; gap: 4px; }

</style>
