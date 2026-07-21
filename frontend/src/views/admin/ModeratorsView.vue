<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">版主管理</h2>
    </div>
    <div class="admin-panel p-3 mb-3">
      <div class="d-flex gap-2 flex-wrap align-items-end">
        <div>
          <label class="form-label">版块 ID</label>
          <input v-model.number="forumId" type="number" class="form-control form-control-sm" />
        </div>
        <div>
          <label class="form-label">用户 ID</label>
          <input v-model.number="userId" type="number" class="form-control form-control-sm" />
        </div>
        <button class="admin-btn admin-btn-primary" @click="add">添加版主</button>
      </div>
      <div v-if="error" class="text-danger mt-2" style="font-size:13px">{{ error }}</div>
    </div>
    <div class="admin-panel">
      <table class="admin-table">
        <thead><tr><th>版块</th><th>用户</th><th>操作</th></tr></thead>
        <tbody>
          <tr v-for="m in items" :key="m.forumId + '-' + m.userId">
            <td>{{ m.forumName }} (#{{ m.forumId }})</td>
            <td>{{ m.nickname }} (#{{ m.userId }})</td>
            <td><button class="admin-btn admin-btn-danger" @click="remove(m)">移除</button></td>
          </tr>
        </tbody>
      </table>
      <div v-if="!items.length" class="p-3 text-muted">暂无版主</div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import api from '../../api/http'

const items = ref([])
const forumId = ref(1)
const userId = ref(2)
const error = ref('')

async function load() {
  const { data } = await api.get('/admin/moderators')
  items.value = data
}

async function add() {
  error.value = ''
  try {
    await api.post('/admin/moderators', { forumId: forumId.value, userId: userId.value })
    await load()
  } catch (e) { error.value = e.message }
}

async function remove(m) {
  if (!confirm('确定移除？')) return
  await api.delete('/admin/moderators', { params: { forumId: m.forumId, userId: m.userId } })
  await load()
}

load()
</script>

<style scoped>
.page-header { margin-bottom: 16px; }
.form-label { font-size: 12px; margin-bottom: 2px; }
</style>
