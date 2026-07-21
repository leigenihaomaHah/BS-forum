<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 账号设置
    </div>

    <div v-if="!auth.isLoggedIn" class="auth-card" style="max-width: 420px; margin: 48px auto;">
      <h4 class="mb-1">请先登录</h4>
      <p class="text-muted mb-3" style="font-size: 13px">登录后即可修改账号设置</p>
      <button type="button" class="btn btn-forum w-100" @click="authModal.openLogin()">前往登录</button>
    </div>

    <template v-else>
      <div class="row g-4">
        <div class="col-md-4">
          <div class="panel text-center">
            <div class="panel-header"><span class="accent"></span>头像</div>
            <div class="p-4">
              <img :src="avatarPreview" class="settings-avatar mb-3" />
              <label class="btn btn-forum btn-sm d-inline-block">
                <input type="file" accept="image/*" hidden @change="uploadAvatar" />
                上传头像
              </label>
              <div class="text-muted mt-2" style="font-size: 12px">建议 200×200 以上，支持 JPG/PNG</div>
            </div>
          </div>
        </div>

        <div class="col-md-8">
          <div class="panel">
            <div class="panel-header"><span class="accent"></span>基本信息</div>
            <div class="p-4">
              <div class="mb-3">
                <label class="form-label">用户名</label>
                <input :value="auth.user.username" class="form-control" disabled />
                <div class="text-muted" style="font-size: 12px; margin-top: 4px">用户名不可修改</div>
              </div>

              <div class="mb-3">
                <label class="form-label">昵称</label>
                <input v-model="form.nickname" class="form-control" maxlength="20" />
                <div class="text-muted" style="font-size: 12px; margin-top: 4px">修改昵称需消耗一张改名卡（商城购买）</div>
              </div>

              <div class="mb-3">
                <label class="form-label">邮箱（可选）</label>
                <input v-model="form.email" type="email" class="form-control" maxlength="80" placeholder="用于找回联系，暂不发送邮件" />
              </div>

              <div class="mb-3">
                <label class="form-label">新密码</label>
                <input v-model="form.password" type="password" class="form-control" maxlength="32" placeholder="留空则不修改密码" autocomplete="new-password" />
                <div class="text-muted" style="font-size: 12px; margin-top: 4px">8–32 位，需含字母和数字；不修改请留空</div>
              </div>

              <div v-if="saveError" class="text-danger mb-2" style="font-size: 13px">{{ saveError }}</div>
              <div v-if="saveSuccess" class="text-success mb-2" style="font-size: 13px">{{ saveSuccess }}</div>

              <button class="btn btn-forum" :disabled="saving" @click="save">{{ saving ? '保存中...' : '保存修改' }}</button>
            </div>
          </div>

          <div class="panel mt-3">
            <div class="panel-header"><span class="accent"></span>隐私与通知</div>
            <div class="p-4">
              <label class="check-row">
                <input v-model="form.showPurchases" type="checkbox" />
                公开我的购买记录
              </label>
              <label class="check-row">
                <input v-model="form.showFavorites" type="checkbox" />
                公开我的收藏
              </label>
              <label class="check-row">
                <input v-model="form.notifyReply" type="checkbox" />
                被回帖时站内通知
              </label>
              <label class="check-row">
                <input v-model="form.notifyMention" type="checkbox" />
                被 @ 时站内通知
              </label>
              <button class="btn btn-forum btn-sm mt-2" :disabled="saving" @click="save">保存偏好</button>
            </div>
          </div>
        </div>
      </div>

      <div class="panel mt-3">
        <div class="panel-header"><span class="accent"></span>黑名单</div>
        <div class="p-3">
          <div v-if="blockedLoading" class="text-muted" style="font-size:13px">加载中...</div>
          <div v-else-if="!blockedUsers.length" class="text-muted" style="font-size:13px">暂无已屏蔽的用户</div>
          <div v-for="u in blockedUsers" :key="u.id" class="blocked-row">
            <router-link :to="`/user/${u.id}`" class="fw-bold" style="font-size:13px;color:#142033;text-decoration:none">{{ u.nickname }}</router-link>
            <span class="text-muted ms-2" style="font-size:12px">@{{ u.username }}</span>
            <button class="btn btn-sm btn-outline-secondary ms-auto" @click="unblock(u.id)">取消屏蔽</button>
          </div>
        </div>
      </div>
    </template>
  </AppLayout>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { passwordError } from '../utils/password.js'
import { compressImage } from '../utils/image.js'
import { defaultAvatar } from '../utils/avatar.js'

const auth = useAuthStore()
const authModal = useAuthModalStore()
const saving = ref(false)
const saveError = ref('')
const saveSuccess = ref('')
const avatarPreview = ref('')

const form = ref({
  nickname: '',
  password: '',
  email: '',
  showPurchases: false,
  showFavorites: false,
  notifyReply: true,
  notifyMention: true,
})

const blockedUsers = ref([])
const blockedLoading = ref(false)

async function loadBlocked() {
  if (!auth.isLoggedIn) return
  blockedLoading.value = true
  try {
    const { data } = await api.get('/users/blocked')
    blockedUsers.value = data || []
  } catch { blockedUsers.value = [] }
  finally { blockedLoading.value = false }
}

async function unblock(id) {
  try {
    await api.delete(`/users/${id}/block`)
    blockedUsers.value = blockedUsers.value.filter(u => u.id !== id)
  } catch (e) { saveError.value = e.message }
}

onMounted(() => {
  if (auth.user) {
    form.value.nickname = auth.user.nickname
    form.value.email = auth.user.email || ''
    form.value.showPurchases = !!auth.user.showPurchases
    form.value.showFavorites = !!auth.user.showFavorites
    form.value.notifyReply = auth.user.notifyReply !== false
    form.value.notifyMention = auth.user.notifyMention !== false
    avatarPreview.value = auth.user.avatar || defaultAvatar(auth.user.nickname)
  }
  loadBlocked()
})

function uploadAvatar(e) {
  const file = e.target.files?.[0]
  if (!file) return
  if (file.size > 5 * 1024 * 1024) {
    saveError.value = '头像不能超过 5MB'
    return
  }
  compressImage(file, 256, 0.8).then((dataUrl) => {
    avatarPreview.value = dataUrl
    form.value._avatar = dataUrl
  })
  e.target.value = ''
}

async function save() {
  saveError.value = ''
  saveSuccess.value = ''
  if (form.value.password) {
    const pe = passwordError(form.value.password)
    if (pe) {
      saveError.value = pe
      return
    }
  }
  saving.value = true
  try {
    const body = {
      nickname: form.value.nickname,
      email: form.value.email || null,
      showPurchases: form.value.showPurchases,
      showFavorites: form.value.showFavorites,
      notifyReply: form.value.notifyReply,
      notifyMention: form.value.notifyMention,
    }
    if (form.value.password) body.password = form.value.password
    if (form.value._avatar) body.avatar = form.value._avatar

    const { data } = await api.put('/me/settings', body)
    auth.setUser(data)
    if (data.avatar) avatarPreview.value = data.avatar
    saveSuccess.value = '保存成功！'
    form.value.password = ''
  } catch (e) {
    saveError.value = e.message
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.settings-avatar {
  width: 120px;
  height: 120px;
  border-radius: 24px;
  object-fit: cover;
  border: 3px solid var(--line, rgba(20,32,51,0.08));
}
.form-label {
  font-weight: 600;
  font-size: 13px;
  color: var(--ink, #142033);
  margin-bottom: 4px;
}
.check-row {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  margin-bottom: 10px;
  cursor: pointer;
}
.blocked-row {
  display: flex;
  align-items: center;
  padding: 8px 0;
  border-bottom: 1px solid var(--line, #f0f2f5);
}
</style>
