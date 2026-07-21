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

const auth = useAuthStore()
const authModal = useAuthModalStore()
const saving = ref(false)
const saveError = ref('')
const saveSuccess = ref('')
const avatarPreview = ref('')

const form = ref({
  nickname: '',
  password: '',
})

function defaultAvatar(name) {
  const n = encodeURIComponent((name || '?').slice(0, 1))
  return `https://api.dicebear.com/7.x/initials/svg?seed=${n}`
}

onMounted(() => {
  if (auth.user) {
    form.value.nickname = auth.user.nickname
    avatarPreview.value = auth.user.avatar || defaultAvatar(auth.user.nickname)
  }
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
        const ctx = canvas.getContext('2d')
        ctx.drawImage(img, 0, 0, width, height)
        resolve(canvas.toDataURL('image/jpeg', quality))
      }
      img.src = e.target.result
    }
    reader.readAsDataURL(file)
  })
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
    const body = { nickname: form.value.nickname }
    if (form.value.password) body.password = form.value.password
    if (form.value._avatar) body.avatar = form.value._avatar

    const { data } = await api.put('/me/settings', body)
    auth.setUser(data)
    // Update local avatar
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
</style>
