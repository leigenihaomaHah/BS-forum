<template>
  <div class="admin-page">
      <div class="page-header">
        <h2 class="page-title">版块管理</h2>
        <button class="btn btn-forum btn-sm" @click="addCategory">新增分类</button>
      </div>

      <div v-for="cat in categories" :key="cat.id" class="admin-panel mb-3">
        <div class="admin-panel-hd">
          <div class="edit-row">
            <template v-if="cat._editing">
              <IconPicker v-model="cat._icon" />
              <input v-model="cat._name" class="form-control form-control-sm name-input" @keyup.enter="saveCategory(cat)" />
              <button class="admin-btn admin-btn-primary" @click="saveCategory(cat)">保存</button>
              <button class="admin-btn admin-btn-outline" @click="cat._editing = false">取消</button>
            </template>
            <template v-else>
              <span class="me-1">{{ cat.icon }}</span>
              <span>{{ cat.name }}</span>
            </template>
          </div>
          <div>
            <button class="admin-btn admin-btn-outline me-1" @click="startEditCategory(cat)">编辑</button>
            <button class="admin-btn admin-btn-danger" @click="delCategory(cat.id)">删除</button>
          </div>
        </div>
        <div class="p-3">
          <div v-for="forum in cat.forums" :key="forum.id" class="forum-row">
            <div class="forum-row-info">
              <template v-if="forum._editing">
                <div class="edit-row">
                  <IconPicker v-model="forum._icon" />
                  <input v-model="forum._name" class="form-control form-control-sm name-input" placeholder="版块名称" />
                  <input v-model="forum._description" class="form-control form-control-sm desc-input" placeholder="简介（可选）" />
                  <select v-model.number="forum._minVipTier" class="form-select form-select-sm tier-select">
                    <option :value="0">所有人可见</option>
                    <option :value="1">月会员及以上</option>
                    <option :value="2">季会员及以上</option>
                    <option :value="3">年会员及以上</option>
                    <option :value="4">仅永久会员</option>
                  </select>
                </div>
              </template>
              <template v-else>
                <span>{{ forum.icon }}</span>
                <span class="fw-medium ms-1">{{ forum.name }}</span>
                <span class="text-muted ms-2" style="font-size:12px">{{ forum.description }}</span>
                <span v-if="forum.minVipTier > 0" class="access-tag ms-2">{{ forum.accessLabel || tierLabel(forum.minVipTier) }}</span>
              </template>
            </div>
            <div class="forum-row-actions">
              <template v-if="forum._editing">
                <button class="admin-btn admin-btn-primary me-1" @click="saveForum(forum)">保存</button>
                <button class="admin-btn admin-btn-outline me-1" @click="forum._editing = false">取消</button>
              </template>
              <template v-else>
                <button class="admin-btn admin-btn-outline me-1" @click="startEditForum(forum)">编辑</button>
              </template>
              <button class="admin-btn admin-btn-danger" @click="delForum(forum.id)">删除</button>
            </div>
          </div>
          <button class="btn btn-sm btn-outline-modern mt-2" @click="addForum(cat.id)">+ 添加版块</button>
        </div>
      </div>

      <div v-if="!categories.length" class="admin-panel p-3 text-muted">暂无分类</div>
    </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../../api/http'
import IconPicker from '../../components/IconPicker.vue'

const categories = ref([])

async function load() {
  try {
    const { data } = await api.get('/admin/categories')
    categories.value = (data || []).map((c) => ({
      ...c,
      _editing: false,
      _name: c.name,
      _icon: c.icon || '📁',
      forums: (c.forums || []).map((f) => ({
        ...f,
        _editing: false,
        _name: f.name,
        _icon: f.icon || '📁',
        _description: f.description || '',
        _minVipTier: f.minVipTier || 0
      }))
    }))
  } catch { categories.value = [] }
}

function tierLabel(tier) {
  return ({ 1: '月会员可见', 2: '季会员可见', 3: '年会员可见', 4: '永久会员可见' })[tier] || '会员可见'
}

function startEditCategory(cat) {
  cat._editing = true
  cat._name = cat.name
  cat._icon = cat.icon || '📁'
}

async function saveCategory(cat) {
  try {
    const { data } = await api.put(`/admin/categories/${cat.id}`, { name: cat._name, icon: cat._icon })
    cat.name = data.name
    cat.icon = data.icon
    cat._editing = false
  } catch (e) { alert(e.message) }
}

async function addCategory() {
  try {
    const { data } = await api.post('/admin/categories', { name: '新分类', icon: '📁' })
    categories.value.push({
      ...data,
      _editing: true,
      _name: data.name,
      _icon: data.icon || '📁',
      forums: data.forums || []
    })
  } catch (e) { alert(e.message || '新增分类失败') }
}

async function delCategory(id) {
  if (!confirm('确定删除此分类及其所有版块？')) return
  try {
    await api.delete(`/admin/categories/${id}`)
    categories.value = categories.value.filter((c) => c.id !== id)
  } catch (e) { alert(e.message) }
}

function startEditForum(forum) {
  forum._editing = true
  forum._name = forum.name
  forum._icon = forum.icon || '📁'
  forum._description = forum.description || ''
  forum._minVipTier = forum.minVipTier || 0
}

async function saveForum(forum) {
  try {
    const { data } = await api.put(`/admin/forums/${forum.id}`, {
      name: forum._name,
      icon: forum._icon,
      description: forum._description,
      minVipTier: forum._minVipTier
    })
    forum.name = data.name
    forum.icon = data.icon
    forum.description = data.description
    forum.minVipTier = data.minVipTier
    forum.accessLabel = data.accessLabel
    forum._editing = false
  } catch (e) { alert(e.message) }
}

async function addForum(categoryId) {
  try {
    const { data } = await api.post('/admin/forums', {
      categoryId, name: '新版块', icon: '📁', description: '', minVipTier: 0
    })
    const cat = categories.value.find((c) => c.id === categoryId)
    if (cat) {
      cat.forums.push({
        ...data,
        _editing: true,
        _name: data.name,
        _icon: data.icon || '📁',
        _description: data.description || '',
        _minVipTier: data.minVipTier || 0
      })
    }
  } catch (e) { alert(e.message) }
}

async function delForum(id) {
  if (!confirm('确定删除此版块？')) return
  try {
    await api.delete(`/admin/forums/${id}`)
    for (const cat of categories.value) {
      cat.forums = cat.forums.filter((f) => f.id !== id)
    }
  } catch (e) { alert(e.message) }
}

onMounted(load)
</script>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}
.forum-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 0;
  border-bottom: 1px solid #f0f0f0;
}
.forum-row:last-child { border-bottom: none; }
.forum-row-info { flex: 1; min-width: 0; }
.forum-row-actions { flex-shrink: 0; }
.edit-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}
.name-input { width: 160px; }
.desc-input { width: 200px; max-width: 40vw; }
.tier-select { width: 150px; }
.access-tag {
  display: inline-block;
  padding: 1px 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 600;
  color: #b45309;
  background: #fef3c7;
}
</style>
