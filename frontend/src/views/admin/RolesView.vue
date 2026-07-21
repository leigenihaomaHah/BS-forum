<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">角色管理</h2>
    </div>

    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>用户名</th>
            <th>昵称</th>
            <th>等级</th>
            <th>当前角色</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="u in items" :key="u.id">
            <td>{{ u.id }}</td>
            <td>{{ u.username }}</td>
            <td>{{ u.nickname }}</td>
            <td><span class="level-badge" :class="{ 'lv-high': u.level >= 5 }">Lv.{{ u.level }}</span></td>
            <td>
              <span class="role-badge" :class="isAdminRole(u) ? 'role-admin' : 'role-user'">
                {{ isAdminRole(u) ? '管理员' : '普通用户' }}
              </span>
            </td>
            <td>
              <button
                v-if="!isAdminRole(u)"
                class="admin-btn admin-btn-warning"
                @click="setRole(u.id, 'admin')"
              >设为管理员</button>
              <button
                v-else-if="u.id !== currentUserId"
                class="admin-btn admin-btn-outline"
                @click="setRole(u.id, 'user')"
              >降级为普通用户</button>
              <span v-else class="text-muted" style="font-size:12px">当前账号</span>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-if="!items.length" class="p-3 text-muted">暂无数据</div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../../api/http'
import { useAuthStore } from '../../stores/auth'
import { isAdminUser } from '../../config/levels.js'

const auth = useAuthStore()
const currentUserId = auth.user?.id
const items = ref([])

function isAdminRole(u) {
  return isAdminUser(u)
}

async function load() {
  try {
    const { data } = await api.get('/admin/users', { params: { page: 1, pageSize: 100 } })
    items.value = data.items
  } catch { items.value = [] }
}

async function setRole(id, role) {
  const action = role === 'admin' ? '提升为管理员' : '降级为普通用户'
  if (!confirm(`确定将用户 #${id} ${action}？`)) return
  try {
    await api.put(`/admin/users/${id}/role`, { role })
    await load()
  } catch (e) { alert(e.message) }
}

onMounted(load)
</script>

<style scoped>
.role-badge {
  display: inline-block;
  padding: 3px 10px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 600;
}
.role-admin {
  background: rgba(232, 165, 75, 0.15);
  color: #a16207;
}
.role-user {
  background: #f0f2f5;
  color: #3d4a63;
}
</style>
