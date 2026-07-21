<template>
  <div class="admin-page">
    <h2 class="page-title">站点设置</h2>

    <div class="admin-panel">
      <div class="p-3">
        <div class="setting-row">
          <label class="setting-label">注册开关</label>
          <select v-model="settings.allow_register" class="form-control form-control-sm" style="width:120px">
            <option value="1">开启</option>
            <option value="0">关闭</option>
          </select>
        </div>
        <div class="setting-row">
          <label class="setting-label">发帖审核</label>
          <select v-model="settings.require_review" class="form-control form-control-sm" style="width:120px">
            <option value="1">开启</option>
            <option value="0">关闭</option>
          </select>
        </div>
        <div class="setting-row">
          <label class="setting-label">每日回复上限</label>
          <input v-model.number="settings.max_replies_per_day" class="form-control form-control-sm" type="number" min="0" style="width:100px" />
        </div>
        <div class="setting-row">
          <label class="setting-label">附件大小限制 (MB)</label>
          <input v-model.number="settings.max_file_size_mb" class="form-control form-control-sm" type="number" min="1" max="50" style="width:100px" />
        </div>
        <div class="setting-row">
          <label class="setting-label">新用户初始积分</label>
          <input v-model.number="settings.default_points" class="form-control form-control-sm" type="number" min="0" style="width:100px" />
        </div>
        <div class="setting-row">
          <label class="setting-label">新用户初始金币</label>
          <input v-model.number="settings.default_coins" class="form-control form-control-sm" type="number" min="0" style="width:100px" />
        </div>
        <div class="mt-3">
          <button class="admin-btn admin-btn-primary" :disabled="saving" @click="save">{{ saving ? '保存中...' : '保存设置' }}</button>
          <span v-if="saved" class="text-success ms-2" style="font-size:13px">已保存</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, reactive, ref } from 'vue'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'

const toast = useToastStore()
const saving = ref(false)
const saved = ref(false)
const settings = reactive({
  allow_register: '1',
  require_review: '0',
  max_replies_per_day: 20,
  max_file_size_mb: 10,
  default_points: 0,
  default_coins: 0,
})

async function load() {
  try {
    const { data } = await api.get('/admin/settings')
    Object.assign(settings, {
      allow_register: data.allow_register ?? '1',
      require_review: data.require_review ?? '0',
      max_replies_per_day: Number(data.max_replies_per_day) || 20,
      max_file_size_mb: Number(data.max_file_size_mb) || 10,
      default_points: Number(data.default_points) || 0,
      default_coins: Number(data.default_coins) || 0,
    })
  } catch { /* use defaults */ }
}

async function save() {
  saving.value = true
  saved.value = false
  try {
    await api.put('/admin/settings', { settings: { ...settings } })
    saved.value = true
    toast.success('设置已保存')
  } catch (e) { toast.error(e.message) }
  finally { saving.value = false }
}

onMounted(load)
</script>

<style scoped>
.setting-row {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 10px 0;
  border-bottom: 1px solid #f0f2f5;
}
.setting-label {
  width: 140px;
  font-size: 13px;
  font-weight: 600;
  color: #142033;
  flex-shrink: 0;
}
</style>
