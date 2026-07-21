<template>
  <div class="admin-page">
    <h2 class="page-title">等级配置</h2>

    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>等级</th>
            <th>名称</th>
            <th>所需积分</th>
            <th>权益</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="l in levels" :key="l.id || l.level">
            <td><span class="level-badge" :class="{ 'lv-high': l.level >= 5 }">Lv.{{ l.level }}</span></td>
            <td>
              <input v-if="editingId === l.id" v-model="editName" class="form-control form-control-sm" maxlength="20" />
              <span v-else class="fw-medium">{{ l.name }}</span>
            </td>
            <td>
              <input v-if="editingId === l.id" v-model.number="editMinPoints" class="form-control form-control-sm" type="number" min="0" style="width:100px" />
              <span v-else>{{ l.minPoints.toLocaleString() }} 分</span>
            </td>
            <td>
              <span v-for="b in l.benefits" :key="b" class="benefit-tag">{{ b }}</span>
            </td>
            <td class="ops">
              <template v-if="editingId === l.id">
                <button class="admin-btn admin-btn-primary admin-btn-sm" :disabled="saving" @click="save(l)">{{ saving ? '保存中...' : '保存' }}</button>
                <button class="admin-btn admin-btn-outline admin-btn-sm" @click="cancelEdit">取消</button>
              </template>
              <button v-else class="admin-btn admin-btn-outline admin-btn-sm" @click="startEdit(l)">编辑</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="error" class="text-danger mt-2" style="font-size:13px">{{ error }}</div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'
import { getLevels, setLevels } from '../../config/levels.js'

const toast = useToastStore()
const levels = ref(getLevels())
const editingId = ref(null)
const editName = ref('')
const editMinPoints = ref(0)
const saving = ref(false)
const error = ref('')

function startEdit(l) {
  editingId.value = l.id
  editName.value = l.name
  editMinPoints.value = l.minPoints
  error.value = ''
}

function cancelEdit() {
  editingId.value = null
  editName.value = ''
  editMinPoints.value = 0
}

async function save(l) {
  saving.value = true
  error.value = ''
  try {
    await api.put(`/admin/levels/${l.id}`, {
      name: editName.value || l.name,
      minPoints: editMinPoints.value,
    })
    // Refetch all levels
    const { data } = await api.get('/levels')
    setLevels(data)
    levels.value = getLevels()
    editingId.value = null
    toast.success('等级配置已更新')
  } catch (e) {
    error.value = e.response?.data?.message || e.message || '保存失败'
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  try {
    const { data } = await api.get('/levels')
    setLevels(data)
    levels.value = getLevels()
  } catch {
    levels.value = getLevels()
  }
})
</script>

<style scoped>
.ops { white-space: nowrap; }
.benefit-tag {
  display: inline-block;
  padding: 2px 8px;
  background: #f0f4f8;
  border-radius: 6px;
  font-size: 11px;
  color: #3d4a63;
  margin: 1px 2px;
  white-space: nowrap;
}
.admin-btn-sm { padding: 4px 10px; font-size: 12px; }
</style>
