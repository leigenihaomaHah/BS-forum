<template>
  <div class="admin-page">
    <div class="page-header">
      <h2 class="page-title">用户管理</h2>
      <div class="search-bar">
        <select v-model="mutedFilter" class="form-control form-control-sm" style="width:120px" @change="load(1)">
          <option value="">全部用户</option>
          <option value="1">已禁言</option>
        </select>
        <input v-model="search" class="form-control form-control-sm" placeholder="搜索用户名/昵称..." @keyup.enter="load(1)" />
        <button class="admin-btn admin-btn-primary ms-1" @click="load(1)">搜索</button>
      </div>
    </div>

    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>用户名</th>
            <th>昵称</th>
            <th>等级</th>
            <th>状态</th>
            <th>会员</th>
            <th>积分</th>
            <th>金币</th>
            <th>发帖</th>
            <th>回帖</th>
            <th>注册时间</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="u in items" :key="u.id">
            <td>{{ u.id }}</td>
            <td>
              <img :src="u.avatar || defaultAvatar(u.nickname)" class="admin-avatar" alt="" />
              {{ u.username }}
            </td>
            <td>{{ u.nickname }}</td>
            <td><span class="level-badge" :class="{ 'lv-high': u.level >= 5 }">Lv.{{ u.level }}</span></td>
            <td>
              <span v-if="u.isMuted" class="status-tag tag-danger" :title="u.muteReason || ''">
                禁言{{ u.mutedUntil ? '至 ' + fmtTime(u.mutedUntil) : '（永久）' }}
              </span>
              <span v-else-if="u.isAdmin" class="status-tag tag-ok">管理员</span>
              <span v-else class="text-muted">正常</span>
            </td>
            <td>
              <span v-if="u.isVip" class="status-tag tag-vip" :title="u.vipUntil ? ('至 ' + fmtTime(u.vipUntil)) : '永久'">
                VIP{{ u.vipUntil ? '' : '·永久' }}
              </span>
              <span v-else class="text-muted">-</span>
            </td>
            <td>{{ u.points }}</td>
            <td>{{ u.coins }}</td>
            <td>{{ u.threadCount ?? 0 }}</td>
            <td>{{ u.replyCount ?? 0 }}</td>
            <td class="text-muted">{{ fmt(u.createdAt) }}</td>
            <td class="ops">
              <button class="admin-btn admin-btn-outline" @click="editUser(u)">编辑</button>
              <button
                v-if="!u.isAdmin"
                class="admin-btn admin-btn-outline"
                @click="u.isMuted ? unmuteUser(u) : openMute(u)"
              >{{ u.isMuted ? '解禁' : '禁言' }}</button>
              <button class="admin-btn admin-btn-danger" @click="delUser(u.id)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-if="!items.length" class="p-3 text-muted">暂无用户</div>
      <PaginationComp v-model="page" :total-pages="totalPages" />
    </div>

    <div v-if="editing" class="modal-overlay" @click.self="editing = null">
      <div class="modal-box">
        <h4>编辑用户 #{{ editing.id }}</h4>
        <div class="modal-body">
          <div class="mb-2">
            <label class="form-label">昵称</label>
            <input v-model="editForm.nickname" class="form-control" />
          </div>
          <div class="mb-2">
            <label class="form-label">积分</label>
            <input v-model.number="editForm.points" type="number" class="form-control" />
          </div>
          <div class="mb-2">
            <label class="form-label">金币</label>
            <input v-model.number="editForm.coins" type="number" class="form-control" />
          </div>
          <div class="mb-2">
            <label class="form-label">密码（留空不修改）</label>
            <input v-model="editForm.password" type="password" class="form-control" placeholder="新密码" autocomplete="new-password" />
          </div>
          <div v-if="editError" class="text-danger mb-2" style="font-size: 13px">{{ editError }}</div>
        </div>
        <div class="modal-actions">
          <button class="btn btn-forum btn-sm" :disabled="saving" @click="saveUser">{{ saving ? '保存中...' : '保存' }}</button>
          <button class="btn btn-outline-modern btn-sm ms-2" @click="editing = null">取消</button>
        </div>
      </div>
    </div>

    <div v-if="muting" class="modal-overlay" @click.self="muting = null">
      <div class="modal-box">
        <h4>禁言用户 #{{ muting.id }} {{ muting.nickname }}</h4>
        <div class="modal-body">
          <div class="mb-2">
            <label class="form-label">天数（留空或 0 = 永久）</label>
            <input v-model.number="muteForm.days" type="number" min="0" class="form-control" placeholder="例如 7" />
          </div>
          <div class="mb-2">
            <label class="form-label">原因</label>
            <input v-model="muteForm.reason" class="form-control" maxlength="100" placeholder="可选" />
          </div>
          <div v-if="muteError" class="text-danger mb-2" style="font-size: 13px">{{ muteError }}</div>
        </div>
        <div class="modal-actions">
          <button class="btn btn-forum btn-sm" :disabled="muteSaving" @click="confirmMute">{{ muteSaving ? '处理中...' : '确认禁言' }}</button>
          <button class="btn btn-outline-modern btn-sm ms-2" @click="muting = null">取消</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'
import { defaultAvatar } from '../../utils/avatar.js'
import PaginationComp from '../../components/PaginationComp.vue'

const toast = useToastStore()

const route = useRoute()
const items = ref([])
const page = ref(1)
const total = ref(0)
const pageSize = ref(20)
const search = ref('')
const mutedFilter = ref(route.query.muted === '1' ? '1' : '')
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)))

const editing = ref(null)
const editForm = ref({})
const editError = ref('')
const saving = ref(false)

const muting = ref(null)
const muteForm = ref({ days: 7, reason: '' })
const muteError = ref('')
const muteSaving = ref(false)

function fmt(iso) { return iso ? new Date(iso).toLocaleDateString() : '' }
function fmtTime(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  return `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

async function load(p) {
  page.value = p || 1
  try {
    const { data } = await api.get('/admin/users', {
      params: {
        page: page.value,
        pageSize: pageSize.value,
        search: search.value,
        muted: mutedFilter.value === '1' ? true : undefined,
      },
    })
    items.value = data.items
    total.value = data.total
  } catch { items.value = [] }
}

function editUser(u) {
  editing.value = u
  editForm.value = { nickname: u.nickname, points: u.points, coins: u.coins, password: '' }
  editError.value = ''
}

async function saveUser() {
  saving.value = true
  editError.value = ''
  try {
    const body = { nickname: editForm.value.nickname, points: editForm.value.points, coins: editForm.value.coins }
    if (editForm.value.password) body.password = editForm.value.password
    await api.put(`/admin/users/${editing.value.id}`, body)
    editing.value = null
    await load(page.value)
  } catch (e) {
    editError.value = e.message
  } finally {
    saving.value = false
  }
}

function openMute(u) {
  muting.value = u
  muteForm.value = { days: 7, reason: '' }
  muteError.value = ''
}

async function confirmMute() {
  muteSaving.value = true
  muteError.value = ''
  try {
    const days = muteForm.value.days > 0 ? muteForm.value.days : null
    await api.post(`/admin/users/${muting.value.id}/mute`, {
      days,
      reason: muteForm.value.reason || null,
    })
    muting.value = null
    await load(page.value)
  } catch (e) {
    muteError.value = e.message
  } finally {
    muteSaving.value = false
  }
}

async function unmuteUser(u) {
  if (!confirm(`确定解除 ${u.nickname} 的禁言？`)) return
  try {
    await api.post(`/admin/users/${u.id}/unmute`, {})
    await load(page.value)
  } catch (e) { toast.error(e.message) }
}

async function delUser(id) {
  if (!confirm('确定删除此用户？')) return
  try {
    await api.delete(`/admin/users/${id}`)
    await load(page.value)
  } catch (e) { toast.error(e.message) }
}

load(1)
watch(page, (p) => { if (p > 0) load(p) })
</script>

<style scoped>
.admin-avatar {
  width: 24px;
  height: 24px;
  border-radius: 6px;
  vertical-align: middle;
  margin-right: 4px;
  object-fit: cover;
}

.search-bar {
  display: flex;
  align-items: center;
  gap: 6px;
}
.search-bar input { width: 200px; }
.ops { display: flex; flex-wrap: wrap; gap: 4px; }
.status-tag {
  display: inline-block;
  padding: 1px 6px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
}
.tag-danger { background: rgba(220,38,38,0.12); color: #b91c1c; }
.tag-ok { background: rgba(13,148,136,0.12); color: #0f766e; }
.tag-vip { background: rgba(245,158,11,0.15); color: #b45309; font-weight: 600; }
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
  width: 420px;
  max-width: 90vw;
  box-shadow: 0 20px 60px rgba(0,0,0,0.15);
}
.modal-box h4 { font-weight: 700; margin-bottom: 16px; }
.modal-actions { display: flex; justify-content: flex-end; margin-top: 16px; }
</style>
