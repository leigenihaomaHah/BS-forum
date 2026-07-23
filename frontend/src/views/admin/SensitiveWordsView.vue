<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">敏感词管理</h2>
      <button class="admin-btn admin-btn-primary" @click="openCreate">新增</button>
    </div>

    <div class="admin-panel mb-3 p-3" style="display:flex;gap:10px;align-items:center;flex-wrap:wrap">
      <input v-model="q" class="form-control form-control-sm" style="width:200px" placeholder="搜索敏感词…" @keyup.enter="load" />
      <button class="admin-btn admin-btn-outline" @click="load">搜索</button>
      <span class="text-muted" style="font-size:12px">共 {{ total }} 条 · 命中后替换为 ***，并可强制进审</span>
    </div>

    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>词语</th>
            <th>分类</th>
            <th>状态</th>
            <th>添加时间</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="w in items" :key="w.id">
            <td>{{ w.word }}</td>
            <td>{{ w.category === 'ad' ? '广告' : '敏感' }}</td>
            <td>{{ w.enabled ? '启用' : '停用' }}</td>
            <td class="text-muted" style="font-size:12px">{{ fmt(w.createdAt) }}</td>
            <td class="ops">
              <button class="admin-btn admin-btn-outline" @click="edit(w)">编辑</button>
              <button class="admin-btn admin-btn-danger" @click="remove(w)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-if="!items.length" class="p-3 text-muted">暂无敏感词</div>
    </div>

    <div v-if="showForm" class="admin-panel mt-3 p-3" style="max-width:420px">
      <div class="mb-2">
        <label class="form-label">词语</label>
        <input v-model="form.word" class="form-control" maxlength="40" />
      </div>
      <div class="mb-2">
        <label class="form-label">分类</label>
        <select v-model="form.category" class="form-control">
          <option value="sensitive">敏感</option>
          <option value="ad">广告</option>
        </select>
      </div>
      <div class="mb-3">
        <label><input type="checkbox" v-model="form.enabled" /> 启用</label>
      </div>
      <div class="d-flex gap-2">
        <button class="admin-btn admin-btn-primary" :disabled="saving" @click="save">{{ saving ? '保存中…' : '保存' }}</button>
        <button class="admin-btn admin-btn-outline" @click="showForm = false">取消</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'
import { useDialogStore } from '../../stores/dialog'
import { formatTime } from '../../utils/time.js'

const toast = useToastStore()
const dialog = useDialogStore()
const items = ref([])
const total = ref(0)
const q = ref('')
const showForm = ref(false)
const saving = ref(false)
const editingId = ref(null)
const form = ref({ word: '', category: 'sensitive', enabled: true })

function fmt(iso) {
  return formatTime(iso)
}

async function load() {
  try {
    const { data } = await api.get('/admin/sensitive-words', { params: { page: 1, pageSize: 100, q: q.value || undefined } })
    items.value = data.items || []
    total.value = data.total || 0
  } catch (e) {
    toast.error(e.message)
  }
}

function openCreate() {
  editingId.value = null
  form.value = { word: '', category: 'sensitive', enabled: true }
  showForm.value = true
}

function edit(w) {
  editingId.value = w.id
  form.value = { word: w.word, category: w.category, enabled: w.enabled }
  showForm.value = true
}

async function save() {
  saving.value = true
  try {
    if (editingId.value) {
      await api.put(`/admin/sensitive-words/${editingId.value}`, form.value)
      toast.success('已更新')
    } else {
      await api.post('/admin/sensitive-words', form.value)
      toast.success('已添加')
    }
    showForm.value = false
    await load()
  } catch (e) {
    toast.error(e.message)
  } finally {
    saving.value = false
  }
}

async function remove(w) {
  if (!(await dialog.confirm(`删除敏感词「${w.word}」？`, { danger: true, confirmText: '删除' }))) return
  try {
    await api.delete(`/admin/sensitive-words/${w.id}`)
    toast.success('已删除')
    await load()
  } catch (e) {
    toast.error(e.message)
  }
}

onMounted(load)
</script>
