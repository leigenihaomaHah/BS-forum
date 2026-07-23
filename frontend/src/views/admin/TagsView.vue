<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">标签管理</h2>
    </div>
    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>标签名</th>
            <th>使用次数</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="t in tags" :key="t.id">
            <td>
              <input v-if="editingId === t.id" v-model="editName" class="form-control form-control-sm" maxlength="20" />
              <span v-else class="fw-medium">#{{ t.name }}</span>
            </td>
            <td>{{ t.count }}</td>
            <td class="ops">
              <template v-if="editingId === t.id">
                <button class="admin-btn admin-btn-primary admin-btn-sm" :disabled="saving" @click="saveRename(t)">保存</button>
                <button class="admin-btn admin-btn-outline admin-btn-sm" @click="editingId = null">取消</button>
              </template>
              <template v-else>
                <button class="admin-btn admin-btn-outline admin-btn-sm" @click="startEdit(t)">重命名</button>
                <button class="admin-btn admin-btn-danger admin-btn-sm" @click="delTag(t)">删除</button>
              </template>
            </td>
          </tr>
          <tr v-if="!tags.length">
            <td colspan="3" class="text-muted">暂无标签</td>
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
const tags = ref([])
const editingId = ref(null)
const editName = ref('')
const saving = ref(false)

async function load() {
  try {
    const { data } = await api.get('/admin/tags')
    tags.value = data
  } catch { tags.value = [] }
}

function startEdit(t) {
  editingId.value = t.id
  editName.value = t.name
}

async function saveRename(t) {
  if (!editName.value.trim()) return
  saving.value = true
  try {
    await api.put(`/admin/tags/${t.id}`, { name: editName.value.trim() })
    toast.success('已重命名')
    editingId.value = null
    await load()
  } catch (e) { toast.error(e.message) }
  finally { saving.value = false }
}

async function delTag(t) {
  if (!(await dialog.confirm(`确定删除标签 "#${t.name}"？`, { danger: true, confirmText: '删除' }))) return
  try {
    await api.delete(`/admin/tags/${t.id}`)
    toast.success('已删除')
    await load()
  } catch (e) { toast.error(e.message) }
}

onMounted(load)
</script>

<style scoped>
.ops { white-space: nowrap; display: flex; gap: 4px; }
.admin-btn-sm { padding: 4px 10px; font-size: 12px; }
</style>
