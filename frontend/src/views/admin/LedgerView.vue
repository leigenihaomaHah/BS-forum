<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">积分/金币流水</h2>
      <div class="filters">
        <input v-model="userId" class="form-control form-control-sm" type="number" placeholder="用户ID" style="width:100px" @keyup.enter="load(1)" />
        <select v-model="type" class="form-control form-control-sm" style="width:100px" @change="load(1)">
          <option value="">全部</option>
          <option value="point">积分</option>
          <option value="coin">金币</option>
        </select>
        <button class="admin-btn admin-btn-primary ms-1" @click="load(1)">查询</button>
      </div>
    </div>
    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>用户</th>
            <th>类型</th>
            <th>变动</th>
            <th>原因</th>
            <th>时间</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="e in items" :key="e.id + e.type">
            <td>{{ e.id }}</td>
            <td>{{ e.nickname }}</td>
            <td><span :class="e.type === 'coin' ? 'tag-coin' : 'tag-point'">{{ e.type === 'coin' ? '金币' : '积分' }}</span></td>
            <td :class="e.delta >= 0 ? 'text-success' : 'text-danger'" class="fw-bold">{{ e.delta >= 0 ? '+' : '' }}{{ e.delta }}</td>
            <td style="font-size:12px">{{ e.reason }}</td>
            <td class="text-muted" style="font-size:12px">{{ fmt(e.createdAt) }}</td>
          </tr>
          <tr v-if="!items.length">
            <td colspan="6" class="text-muted">暂无记录</td>
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
const userId = ref('')
const type = ref('')
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

function fmt(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  return `${d.getMonth() + 1}-${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

async function load(p) {
  page.value = p || 1
  try {
    const { data } = await api.get('/admin/ledger', {
      params: {
        page: page.value, pageSize,
        userId: userId.value || undefined,
        type: type.value || undefined,
      },
    })
    items.value = data.items
    total.value = data.total
  } catch { items.value = [] }
}

load(1)
</script>

<style scoped>
.filters { display: flex; gap: 6px; align-items: center; }
.tag-coin, .tag-point {
  display: inline-block; padding: 1px 6px; border-radius: 4px; font-size: 11px; font-weight: 600;
}
.tag-coin { background: rgba(234,179,8,0.15); color: #a16207; }
.tag-point { background: rgba(13,148,136,0.12); color: #0f766e; }
</style>
