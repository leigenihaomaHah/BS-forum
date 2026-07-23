<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">商城商品</h2>
      <button class="admin-btn admin-btn-primary" @click="openEdit(null)">新增商品</button>
    </div>
    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>ID</th><th>SKU</th><th>名称</th><th>类型</th><th>货币</th><th>价格</th><th>排序</th><th>状态</th><th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="i in items" :key="i.id">
            <td>{{ i.id }}</td>
            <td><code>{{ i.sku }}</code></td>
            <td>{{ i.name }}</td>
            <td>{{ i.itemType }}</td>
            <td>{{ i.currency }}</td>
            <td>{{ i.price }}</td>
            <td>{{ i.sortOrder }}</td>
            <td>
              <span class="status-tag" :class="i.enabled ? 'tag-ok' : 'tag-off'">{{ i.enabled ? '上架' : '下架' }}</span>
            </td>
            <td class="ops">
              <button class="admin-btn admin-btn-outline" @click="openEdit(i)">编辑</button>
              <button class="admin-btn admin-btn-danger" @click="remove(i)">下架</button>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-if="!items.length" class="p-3 text-muted">暂无商品</div>
    </div>

    <div v-if="editing" class="modal-overlay" @click.self="editing = null">
      <div class="modal-box">
        <h4>{{ form.id ? '编辑商品' : '新增商品' }}</h4>
        <div class="modal-body">
          <div class="mb-2"><label class="form-label">SKU</label><input v-model="form.sku" class="form-control" :disabled="!!form.id" /></div>
          <div class="mb-2"><label class="form-label">名称</label><input v-model="form.name" class="form-control" /></div>
          <div class="mb-2"><label class="form-label">描述</label><input v-model="form.description" class="form-control" /></div>
          <div class="mb-2">
            <label class="form-label">类型</label>
            <select v-model="form.itemType" class="form-control">
              <option value="lottery_ticket">转盘券</option>
              <option value="avatar_frame">头像框</option>
              <option value="rename_card">改名卡</option>
              <option value="vip_30">VIP 天数</option>
            </select>
          </div>
          <div class="mb-2">
            <label class="form-label">货币</label>
            <select v-model="form.currency" class="form-control">
              <option value="coins">金币</option>
              <option value="points">积分</option>
            </select>
          </div>
          <div class="mb-2"><label class="form-label">价格</label><input v-model.number="form.price" type="number" min="0" class="form-control" /></div>
          <div class="mb-2"><label class="form-label">Meta（如头像框 gold / VIP天数 30）</label><input v-model="form.meta" class="form-control" /></div>
          <div class="mb-2"><label class="form-label">排序</label><input v-model.number="form.sortOrder" type="number" class="form-control" /></div>
          <label class="mb-2 d-block"><input v-model="form.enabled" type="checkbox" class="me-1" />上架</label>
          <div v-if="error" class="text-danger mb-2" style="font-size:13px">{{ error }}</div>
        </div>
        <div class="modal-actions">
          <button class="btn btn-forum btn-sm" :disabled="saving" @click="save">保存</button>
          <button class="btn btn-outline-modern btn-sm ms-2" @click="editing = null">取消</button>
        </div>
      </div>
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
const items = ref([])
const editing = ref(null)
const saving = ref(false)
const error = ref('')
const form = ref({})

async function load() {
  const { data } = await api.get('/admin/shop')
  items.value = data || []
}

function openEdit(item) {
  error.value = ''
  editing.value = true
  form.value = item
    ? { ...item }
    : { sku: '', name: '', description: '', itemType: 'lottery_ticket', currency: 'coins', price: 10, meta: '1', sortOrder: 0, enabled: true }
}

async function save() {
  saving.value = true
  error.value = ''
  try {
    const body = {
      sku: form.value.sku,
      name: form.value.name,
      description: form.value.description,
      currency: form.value.currency,
      price: form.value.price,
      itemType: form.value.itemType,
      meta: form.value.meta,
      enabled: form.value.enabled,
      sortOrder: form.value.sortOrder,
    }
    if (form.value.id) await api.put(`/admin/shop/${form.value.id}`, body)
    else await api.post('/admin/shop', body)
    editing.value = null
    await load()
    toast.success('已保存')
  } catch (e) { error.value = e.message }
  finally { saving.value = false }
}

async function remove(i) {
  if (!(await dialog.confirm(`下架「${i.name}」？`, { danger: true, confirmText: '下架' }))) return
  try {
    await api.delete(`/admin/shop/${i.id}`)
    await load()
  } catch (e) { toast.error(e.message) }
}

onMounted(load)
</script>

<style scoped>
.ops { display: flex; gap: 4px; }
.status-tag { font-size: 11px; padding: 1px 6px; border-radius: 4px; font-weight: 600; }
.tag-ok { background: rgba(13,148,136,0.12); color: #0f766e; }
.tag-off { background: #f1f5f9; color: #64748b; }
.modal-overlay {
  position: fixed; inset: 0; background: rgba(0,0,0,0.4);
  display: flex; align-items: center; justify-content: center; z-index: 999;
}
.modal-box {
  background: #fff; border-radius: 16px; padding: 24px; width: 440px; max-width: 92vw;
}
.modal-actions { display: flex; justify-content: flex-end; margin-top: 12px; }
.form-label { font-size: 12px; font-weight: 600; }
</style>
