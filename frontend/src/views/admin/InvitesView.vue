<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">邀请码管理</h2>
    </div>
    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>用户ID</th>
            <th>昵称</th>
            <th>邀请码</th>
            <th>已使用</th>
            <th>创建时间</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="inv in invites" :key="inv.userId">
            <td>{{ inv.userId }}</td>
            <td>{{ inv.nickname }}</td>
            <td><code class="invite-code">{{ inv.code }}</code></td>
            <td>{{ inv.usedCount }} 次</td>
            <td class="text-muted" style="font-size:12px">{{ fmt(inv.createdAt) }}</td>
            <td class="ops">
              <button class="admin-btn admin-btn-outline admin-btn-sm" @click="regenerate(inv)">重新生成</button>
            </td>
          </tr>
          <tr v-if="!invites.length">
            <td colspan="6" class="text-muted">暂无邀请码</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'
import { useDialogStore } from '../../stores/dialog'

const toast = useToastStore()
const dialog = useDialogStore()
const invites = ref([])

function fmt(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  return `${d.getFullYear()}-${d.getMonth() + 1}-${d.getDate()}`
}

async function load() {
  try {
    const { data } = await api.get('/admin/invites')
    invites.value = data
  } catch { invites.value = [] }
}

async function regenerate(inv) {
  if (!(await dialog.confirm(`确定重新生成 ${inv.nickname} 的邀请码？旧码将失效。`, { danger: true, confirmText: '重新生成' }))) return
  try {
    const { data } = await api.post(`/admin/invites/${inv.userId}/regenerate`)
    toast.success(data.message || '已重新生成')
    await load()
  } catch (e) { toast.error(e.message) }
}

onMounted(load)
</script>

<style scoped>
.ops { white-space: nowrap; }
.admin-btn-sm { padding: 4px 10px; font-size: 12px; }
.invite-code { font-size: 14px; letter-spacing: 1px; color: #0d9488; font-weight: 600; }
</style>
