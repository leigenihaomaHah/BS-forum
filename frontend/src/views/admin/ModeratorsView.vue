<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">版主管理</h2>
    </div>

    <div class="admin-panel mod-panel">
      <div class="mod-form">
        <div class="field">
          <label class="field-label">版块</label>
          <select v-model.number="forumId" class="form-control form-control-sm field-control">
            <option :value="0" disabled>选择版块</option>
            <option v-for="f in forums" :key="f.id" :value="f.id">{{ f.categoryName }} / {{ f.name }}</option>
          </select>
        </div>

        <div class="field user-pick">
          <label class="field-label">用户</label>
          <input
            v-model="userQuery"
            class="form-control form-control-sm field-control"
            placeholder="搜索用户名/昵称"
            @input="onSearch"
          />
          <div v-if="userHits.length" class="user-drop">
            <button
              v-for="u in userHits"
              :key="u.id"
              type="button"
              class="user-hit"
              @click="pickUser(u)"
            >{{ u.nickname }} (@{{ u.username }}) #{{ u.id }}</button>
          </div>
        </div>

        <div class="field field-action">
          <span class="field-label field-label-spacer" aria-hidden="true">&nbsp;</span>
          <button
            class="admin-btn admin-btn-primary"
            type="button"
            :disabled="!forumId || !userId"
            @click="add"
          >添加版主</button>
        </div>
      </div>

      <p v-if="pickedUser" class="picked">已选：{{ pickedUser.nickname }} #{{ pickedUser.id }}</p>
      <p v-if="error" class="form-error">{{ error }}</p>
    </div>

    <div class="admin-panel">
      <table class="admin-table">
        <thead><tr><th>版块</th><th>用户</th><th>操作</th></tr></thead>
        <tbody>
          <tr v-for="m in items" :key="m.forumId + '-' + m.userId">
            <td>{{ m.forumName }} (#{{ m.forumId }})</td>
            <td>{{ m.nickname }} (#{{ m.userId }})</td>
            <td><button class="admin-btn admin-btn-danger" type="button" @click="remove(m)">移除</button></td>
          </tr>
        </tbody>
      </table>
      <div v-if="!items.length" class="p-3 text-muted">暂无版主</div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../../api/http'
import { useDialogStore } from '../../stores/dialog'

const dialog = useDialogStore()
const items = ref([])
const forums = ref([])
const forumId = ref(0)
const userId = ref(0)
const userQuery = ref('')
const userHits = ref([])
const pickedUser = ref(null)
const error = ref('')
let searchTimer = null

async function loadForums() {
  const { data } = await api.get('/categories')
  const list = []
  for (const c of data || []) {
    for (const f of c.forums || []) {
      list.push({ id: f.id, name: f.name, categoryName: c.name })
    }
  }
  forums.value = list
  if (list.length && !forumId.value) forumId.value = list[0].id
}

async function load() {
  const { data } = await api.get('/admin/moderators')
  items.value = data
}

function onSearch() {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(async () => {
    const q = userQuery.value.trim()
    if (q.length < 1) { userHits.value = []; return }
    try {
      const { data } = await api.get('/users/search', { params: { q, limit: 8 } })
      userHits.value = data || []
    } catch { userHits.value = [] }
  }, 250)
}

function pickUser(u) {
  pickedUser.value = u
  userId.value = u.id
  userQuery.value = u.nickname
  userHits.value = []
}

async function add() {
  error.value = ''
  try {
    await api.post('/admin/moderators', { forumId: forumId.value, userId: userId.value })
    pickedUser.value = null
    userId.value = 0
    userQuery.value = ''
    await load()
  } catch (e) { error.value = e.message }
}

async function remove(m) {
  if (!(await dialog.confirm('确定移除？', { danger: true, confirmText: '移除' }))) return
  await api.delete('/admin/moderators', { params: { forumId: m.forumId, userId: m.userId } })
  await load()
}

onMounted(async () => {
  await Promise.all([loadForums(), load()])
})
</script>

<style scoped>
.mod-panel {
  padding: 16px 18px;
  margin-bottom: 16px;
  overflow: visible;
  position: relative;
  z-index: 5;
}

.mod-form {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 12px 14px;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.field-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--muted, #7a869c);
  line-height: 1.2;
  margin: 0;
}

.field-label-spacer {
  visibility: hidden;
  user-select: none;
}

.field-control {
  min-width: 200px;
  height: 34px;
}

.user-pick {
  position: relative;
  min-width: 220px;
}

.user-pick .field-control {
  min-width: 220px;
}

.field-action .admin-btn {
  height: 34px;
  white-space: nowrap;
}

.user-drop {
  position: absolute;
  z-index: 50;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  max-height: 220px;
  overflow: auto;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
}

.user-hit {
  display: block;
  width: 100%;
  text-align: left;
  border: none;
  background: transparent;
  padding: 8px 10px;
  font-size: 13px;
  cursor: pointer;
}

.user-hit:hover {
  background: #f1f5f9;
}

.picked {
  margin: 10px 0 0;
  font-size: 12px;
  color: var(--muted, #7a869c);
}

.form-error {
  margin: 8px 0 0;
  font-size: 13px;
  color: var(--danger, #e35d5d);
}
</style>
