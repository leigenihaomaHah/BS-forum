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
          <label class="setting-label">免审最低等级</label>
          <input v-model.number="settings.review_exempt_min_level" class="form-control form-control-sm" type="number" min="1" max="10" style="width:100px" />
          <span class="text-muted" style="font-size:12px">达到该等级自动免审（管理员始终免审）</span>
        </div>
        <div class="setting-row">
          <label class="setting-label">敏感词命中</label>
          <select v-model="settings.sensitive_hit_action" class="form-control form-control-sm" style="width:160px">
            <option value="mask_review">替换 *** 并进审</option>
            <option value="mask">仅替换 ***</option>
            <option value="block">直接拦截</option>
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

        <h3 class="setting-section">限时积分活动</h3>
        <div class="setting-row">
          <label class="setting-label">活动开关</label>
          <select v-model="settings.points_event_enabled" class="form-control form-control-sm" style="width:120px">
            <option value="1">开启</option>
            <option value="0">关闭</option>
          </select>
        </div>
        <div class="setting-row">
          <label class="setting-label">活动名称</label>
          <input v-model="settings.points_event_name" class="form-control form-control-sm" style="width:200px" />
        </div>
        <div class="setting-row">
          <label class="setting-label">积分倍数</label>
          <input v-model.number="settings.points_event_multiplier" class="form-control form-control-sm" type="number" min="1" max="10" style="width:100px" />
          <span class="text-muted" style="font-size:12px">发帖/回帖/签到/点赞等积分 ×N</span>
        </div>
        <div class="setting-row">
          <label class="setting-label">开始时间</label>
          <input v-model="settings.points_event_start" class="form-control form-control-sm" type="datetime-local" style="width:220px" />
        </div>
        <div class="setting-row">
          <label class="setting-label">结束时间</label>
          <input v-model="settings.points_event_end" class="form-control form-control-sm" type="datetime-local" style="width:220px" />
        </div>

        <h3 class="setting-section">付费置顶</h3>
        <div class="setting-row">
          <label class="setting-label">付费置顶</label>
          <select v-model="settings.paid_pin_enabled" class="form-control form-control-sm" style="width:120px">
            <option value="1">开启</option>
            <option value="0">关闭</option>
          </select>
        </div>
        <div class="setting-row">
          <label class="setting-label">置顶费用（金币）</label>
          <input v-model.number="settings.paid_pin_cost_coins" class="form-control form-control-sm" type="number" min="1" style="width:100px" />
        </div>
        <div class="setting-row">
          <label class="setting-label">置顶时长（小时）</label>
          <input v-model.number="settings.paid_pin_hours" class="form-control form-control-sm" type="number" min="1" max="168" style="width:100px" />
        </div>

        <div class="setting-row">
          <label class="setting-label">转盘单次金币</label>
          <input v-model.number="settings.lottery_cost_coins" class="form-control form-control-sm" type="number" min="1" style="width:100px" />
        </div>
        <div class="setting-row">
          <label class="setting-label">转盘每日上限</label>
          <input v-model.number="settings.lottery_daily_limit" class="form-control form-control-sm" type="number" min="1" style="width:100px" />
        </div>
        <div class="setting-row">
          <label class="setting-label">转盘保底次数</label>
          <input v-model.number="settings.lottery_pity" class="form-control form-control-sm" type="number" min="1" style="width:100px" />
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
  review_exempt_min_level: 4,
  sensitive_hit_action: 'mask_review',
  max_replies_per_day: 20,
  max_file_size_mb: 10,
  default_points: 0,
  default_coins: 0,
  lottery_cost_coins: 5,
  lottery_daily_limit: 10,
  lottery_pity: 10,
  points_event_enabled: '0',
  points_event_name: '限时双倍积分',
  points_event_multiplier: 2,
  points_event_start: '',
  points_event_end: '',
  paid_pin_enabled: '1',
  paid_pin_cost_coins: 20,
  paid_pin_hours: 24,
})

function toLocalInput(v) {
  if (!v) return ''
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return String(v).slice(0, 16)
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

async function load() {
  try {
    const { data } = await api.get('/admin/settings')
    Object.assign(settings, {
      allow_register: data.allow_register ?? '1',
      require_review: data.require_review ?? '0',
      review_exempt_min_level: Number(data.review_exempt_min_level) || 4,
      sensitive_hit_action: data.sensitive_hit_action || 'mask_review',
      max_replies_per_day: Number(data.max_replies_per_day) || 20,
      max_file_size_mb: Number(data.max_file_size_mb) || 10,
      default_points: Number(data.default_points) || 0,
      default_coins: Number(data.default_coins) || 0,
      lottery_cost_coins: Number(data.lottery_cost_coins) || 5,
      lottery_daily_limit: Number(data.lottery_daily_limit) || 10,
      lottery_pity: Number(data.lottery_pity) || 10,
      points_event_enabled: data.points_event_enabled ?? '0',
      points_event_name: data.points_event_name || '限时双倍积分',
      points_event_multiplier: Number(data.points_event_multiplier) || 2,
      points_event_start: toLocalInput(data.points_event_start),
      points_event_end: toLocalInput(data.points_event_end),
      paid_pin_enabled: data.paid_pin_enabled ?? '1',
      paid_pin_cost_coins: Number(data.paid_pin_cost_coins) || 20,
      paid_pin_hours: Number(data.paid_pin_hours) || 24,
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
.setting-section {
  margin: 20px 0 8px;
  font-size: 14px;
  font-weight: 700;
  color: #0f766e;
}
</style>
