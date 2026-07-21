<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">首页广告</h2>
      <button class="admin-btn admin-btn-primary" @click="openCreate">新增广告</button>
    </div>

    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>预览</th>
            <th>标题</th>
            <th>链接</th>
            <th>排序</th>
            <th>状态</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="b in items" :key="b.id">
            <td><img :src="b.imageUrl" class="thumb" alt="" /></td>
            <td>{{ b.title }}</td>
            <td class="td-link">{{ b.linkUrl || '—' }}</td>
            <td>{{ b.sortOrder }}</td>
            <td>
              <span class="status-tag" :class="b.enabled ? 'tag-ok' : 'tag-off'">
                {{ b.enabled ? '启用' : '停用' }}
              </span>
            </td>
            <td class="ops">
              <button class="admin-btn admin-btn-outline" @click="openEdit(b)">编辑</button>
              <button class="admin-btn admin-btn-danger" @click="remove(b)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-if="!items.length" class="p-3 text-muted">暂无广告，点击「新增广告」添加，将显示在首页精品推荐上方</div>
    </div>

    <div v-if="editing" class="modal-overlay" @click.self="editing = null">
      <div class="modal-box">
        <h4>{{ form.id ? `编辑广告 #${form.id}` : '新增广告' }}</h4>
        <div class="modal-body">
          <div class="mb-2">
            <label class="form-label">标题</label>
            <input v-model="form.title" class="form-control" maxlength="60" placeholder="广告标题" />
          </div>
          <div class="mb-2">
            <label class="form-label">图片</label>
            <div class="upload-row">
              <label class="admin-btn admin-btn-outline">
                上传图片
                <input type="file" accept="image/*" hidden @change="onFile" />
              </label>
              <input v-model="form.imageUrl" class="form-control" placeholder="或粘贴图片 URL" />
            </div>
            <img v-if="form.imageUrl" :src="form.imageUrl" class="preview" alt="" />
            <div class="hint">建议比例约 3.5:1，宽 1200px，JPG/PNG</div>
          </div>
          <div class="mb-2">
            <label class="form-label">点击跳转</label>
            <input v-model="form.linkUrl" class="form-control" maxlength="300" placeholder="如 /sign-in 或 https://..." />
          </div>
          <div class="mb-2 row-2">
            <div>
              <label class="form-label">排序（小靠前）</label>
              <input v-model.number="form.sortOrder" type="number" class="form-control" />
            </div>
            <div>
              <label class="form-label">状态</label>
              <select v-model="form.enabled" class="form-control">
                <option :value="true">启用</option>
                <option :value="false">停用</option>
              </select>
            </div>
          </div>
          <div v-if="error" class="text-danger mb-2" style="font-size:13px">{{ error }}</div>
        </div>
        <div class="modal-actions">
          <button class="btn btn-forum btn-sm" :disabled="saving" @click="save">{{ saving ? '保存中...' : '保存' }}</button>
          <button class="btn btn-outline-modern btn-sm ms-2" @click="editing = null">取消</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../../api/http'

const items = ref([])
const editing = ref(null)
const saving = ref(false)
const error = ref('')
const form = ref({
  id: null,
  title: '',
  imageUrl: '',
  linkUrl: '',
  sortOrder: 0,
  enabled: true,
})

async function load() {
  try {
    const { data } = await api.get('/admin/banners')
    items.value = data
  } catch {
    items.value = []
  }
}

function openCreate() {
  form.value = { id: null, title: '', imageUrl: '', linkUrl: '', sortOrder: items.value.length + 1, enabled: true }
  error.value = ''
  editing.value = true
}

function openEdit(b) {
  form.value = {
    id: b.id,
    title: b.title,
    imageUrl: b.imageUrl,
    linkUrl: b.linkUrl || '',
    sortOrder: b.sortOrder,
    enabled: b.enabled,
  }
  error.value = ''
  editing.value = true
}

function compressImage(file, maxDim, quality) {
  return new Promise((resolve) => {
    const reader = new FileReader()
    reader.onload = (e) => {
      const img = new Image()
      img.onload = () => {
        let { width, height } = img
        if (width > maxDim || height > maxDim) {
          const ratio = Math.min(maxDim / width, maxDim / height)
          width = Math.round(width * ratio)
          height = Math.round(height * ratio)
        }
        const canvas = document.createElement('canvas')
        canvas.width = width
        canvas.height = height
        canvas.getContext('2d').drawImage(img, 0, 0, width, height)
        resolve(canvas.toDataURL('image/jpeg', quality))
      }
      img.src = e.target.result
    }
    reader.readAsDataURL(file)
  })
}

async function onFile(e) {
  const file = e.target.files?.[0]
  e.target.value = ''
  if (!file) return
  if (file.size > 8 * 1024 * 1024) {
    error.value = '图片请小于 8MB'
    return
  }
  form.value.imageUrl = await compressImage(file, 1400, 0.82)
}

async function save() {
  error.value = ''
  if (!form.value.title.trim()) {
    error.value = '请填写标题'
    return
  }
  if (!form.value.imageUrl.trim()) {
    error.value = '请上传或填写图片'
    return
  }
  saving.value = true
  try {
    const body = {
      title: form.value.title,
      imageUrl: form.value.imageUrl,
      linkUrl: form.value.linkUrl || null,
      sortOrder: Number(form.value.sortOrder) || 0,
      enabled: !!form.value.enabled,
    }
    if (form.value.id) {
      await api.put(`/admin/banners/${form.value.id}`, body)
    } else {
      await api.post('/admin/banners', body)
    }
    editing.value = null
    await load()
  } catch (e) {
    error.value = e.response?.data?.message || e.message || '保存失败'
  } finally {
    saving.value = false
  }
}

async function remove(b) {
  if (!confirm(`删除广告「${b.title}」？`)) return
  try {
    await api.delete(`/admin/banners/${b.id}`)
    await load()
  } catch (e) {
    alert(e.response?.data?.message || e.message)
  }
}

onMounted(load)
</script>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.thumb {
  width: 96px;
  height: 36px;
  object-fit: cover;
  border-radius: 6px;
  background: #eef2f6;
}
.td-link {
  max-width: 180px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12px;
  color: #64748b;
}
.ops { display: flex; gap: 4px; }
.status-tag {
  display: inline-block;
  padding: 1px 8px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
}
.tag-ok { background: rgba(13,148,136,0.12); color: #0f766e; }
.tag-off { background: #f1f5f9; color: #94a3b8; }
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 999;
}
.modal-box {
  background: #fff;
  border-radius: 16px;
  padding: 24px;
  width: 520px;
  max-width: 92vw;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: 0 20px 60px rgba(0,0,0,0.15);
}
.modal-box h4 { font-weight: 700; margin-bottom: 16px; }
.modal-actions { display: flex; justify-content: flex-end; margin-top: 12px; }
.upload-row {
  display: flex;
  gap: 8px;
  align-items: center;
}
.upload-row .form-control { flex: 1; }
.preview {
  margin-top: 8px;
  width: 100%;
  max-height: 140px;
  object-fit: cover;
  border-radius: 8px;
  border: 1px solid #e2e8f0;
}
.hint { font-size: 12px; color: #94a3b8; margin-top: 4px; }
.row-2 {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}
.form-label { font-size: 12px; font-weight: 600; color: #64748b; margin-bottom: 4px; display: block; }
</style>
