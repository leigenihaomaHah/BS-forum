<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">沉默用户</h2>
      <div class="search-bar">
        <select v-model.number="days" class="form-control form-control-sm" style="width:140px" @change="load(1)">
          <option :value="7">7 天未活跃</option>
          <option :value="14">14 天未活跃</option>
          <option :value="30">30 天未活跃</option>
          <option :value="60">60 天未活跃</option>
        </select>
        <button class="admin-btn admin-btn-primary" :disabled="loading" @click="load(1)">刷新</button>
        <button
          class="admin-btn admin-btn-outline"
          :disabled="!selected.length || recalling"
          @click="recall"
        >发送召回{{ selected.length ? ` (${selected.length})` : '' }}</button>
      </div>
    </div>

    <div class="admin-panel mb-3 p-3">
      <label class="recall-label">召回文案</label>
      <textarea v-model="content" class="form-control" rows="2" maxlength="500" placeholder="好久不见，回来看看论坛吧～" />
    </div>

    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th style="width:36px">
              <input type="checkbox" :checked="allSelected" @change="toggleAll" />
            </th>
            <th>用户</th>
            <th>等级</th>
            <th>积分/金币</th>
            <th>沉默天数</th>
            <th>最近活跃</th>
            <th>注册</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="u in items" :key="u.id">
            <td><input type="checkbox" :value="u.id" v-model="selected" /></td>
            <td>{{ u.nickname }} <span class="text-muted">({{ u.username }})</span></td>
            <td>Lv.{{ u.level }} {{ u.levelName }}</td>
            <td>{{ u.points }} / {{ u.coins }}</td>
            <td><span class="status-tag tag-warn">{{ u.silentDays }} 天</span></td>
            <td class="text-muted">{{ fmt(u.lastActiveAt) }}</td>
            <td class="text-muted">{{ fmt(u.createdAt) }}</td>
          </tr>
        </tbody>
      </table>
      <div v-if="!items.length && !loading" class="p-3 text-muted">暂无沉默用户</div>
      <PaginationComp v-model="page" :total-pages="totalPages" />
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import api from '../../api/http'
import PaginationComp from '../../components/PaginationComp.vue'
import { useToastStore } from '../../stores/toast'
import { useDialogStore } from '../../stores/dialog'
import { formatTime } from '../../utils/time'

const toast = useToastStore()
const dialog = useDialogStore()
const items = ref([])
const page = ref(1)
const totalPages = ref(1)
const days = ref(7)
const loading = ref(false)
const recalling = ref(false)
const selected = ref([])
const content = ref('好久不见！论坛最近很热闹，回来看看老朋友吧～签到还能领奖励哦。')

const allSelected = computed(() => items.value.length > 0 && selected.value.length === items.value.length)

function fmt(v) {
  return v ? formatTime(v) : '-'
}

function toggleAll(e) {
  selected.value = e.target.checked ? items.value.map((u) => u.id) : []
}

async function load(p = page.value) {
  loading.value = true
  page.value = p
  selected.value = []
  try {
    const { data } = await api.get('/admin/users/silent', {
      params: { days: days.value, page: p, pageSize: 20 },
    })
    items.value = data.items || []
    totalPages.value = Math.max(1, Math.ceil((data.total || 0) / (data.pageSize || 20)))
  } catch (e) {
    toast.error(e.message)
  } finally {
    loading.value = false
  }
}

async function recall() {
  if (!selected.value.length) return
  if (!content.value.trim()) {
    toast.error('请填写召回文案')
    return
  }
  if (!(await dialog.confirm(`向 ${selected.value.length} 名用户发送召回通知？`))) return
  recalling.value = true
  try {
    const { data } = await api.post('/admin/users/recall', {
      ids: selected.value,
      content: content.value.trim(),
    })
    toast.success(data.message || '已发送')
    selected.value = []
  } catch (e) {
    toast.error(e.message)
  } finally {
    recalling.value = false
  }
}

watch(page, (p) => load(p))
onMounted(() => load(1))
</script>

<style scoped>
.recall-label {
  display: block;
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 8px;
  color: #142033;
}
.search-bar {
  display: flex;
  gap: 8px;
  align-items: center;
  flex-wrap: wrap;
}
</style>
