<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">操作日志</h2>
      <div class="filters">
        <select v-model="targetType" class="form-control form-control-sm" style="width:120px" @change="load(1)">
          <option value="">全部类型</option>
          <option value="thread">帖子</option>
          <option value="user">用户</option>
        </select>
        <button class="admin-btn admin-btn-primary ms-1" @click="load(1)">刷新</button>
      </div>
    </div>
    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>操作人</th>
            <th>目标类型</th>
            <th>目标ID</th>
            <th>操作</th>
            <th>原因</th>
            <th>时间</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="log in items" :key="log.id">
            <td>{{ log.adminNickname }}</td>
            <td>{{ log.targetType }}</td>
            <td>{{ log.targetId }}</td>
            <td><span class="action-tag">{{ log.action }}</span></td>
            <td class="text-muted" style="font-size:12px">{{ log.reason || '-' }}</td>
            <td class="text-muted" style="font-size:12px">{{ fmt(log.createdAt) }}</td>
          </tr>
          <tr v-if="!items.length">
            <td colspan="6" class="text-muted">暂无日志</td>
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
import PaginationComp from '../../components/PaginationComp.vue'

const items = ref([])
const page = ref(1)
const total = ref(0)
const pageSize = 20
const targetType = ref('')
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

function fmt(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  return `${d.getFullYear()}-${d.getMonth() + 1}-${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

async function load(p) {
  page.value = p || 1
  try {
    const { data } = await api.get('/admin/moderation-logs', {
      params: { page: page.value, pageSize, targetType: targetType.value || undefined },
    })
    items.value = data.items
    total.value = data.total
  } catch { items.value = [] }
}

load(1)
</script>

<style scoped>
.filters { display: flex; gap: 6px; align-items: center; }
.action-tag {
  display: inline-block; padding: 1px 6px; border-radius: 4px;
  font-size: 11px; font-weight: 600; background: #f0f4f8; color: #3d4a63;
}
</style>
