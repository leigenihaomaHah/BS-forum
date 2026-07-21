<template>
  <Teleport to="body">
    <Transition name="auth-fade">
      <div v-if="open" class="auth-overlay" @click.self="close">
        <div class="auth-modal auth-modal-sm" role="dialog" aria-modal="true" aria-labelledby="login-title">
          <button type="button" class="auth-close" aria-label="关闭" @click="close">×</button>

          <!-- Login form -->
          <template v-if="!showReset">
            <h4 id="login-title" class="mb-1">欢迎回来</h4>
            <p class="text-muted mb-3" style="font-size: 13px">登录后签到、发帖、赚取积分</p>

            <div class="mb-2">
              <label class="form-label">用户名</label>
              <input v-model="username" class="form-control" autocomplete="username" @keyup.enter="submit" />
            </div>
            <div class="mb-3">
              <label class="form-label">密码</label>
              <input v-model="password" type="password" class="form-control" autocomplete="current-password" @keyup.enter="submit" />
            </div>

            <div v-if="error" class="text-danger mb-2" style="font-size:13px">{{ error }}</div>
            <button class="btn btn-forum w-100" :disabled="loading" @click="submit">
              {{ loading ? '登录中...' : '登录' }}
            </button>
            <div class="mt-3 text-muted" style="font-size: 12px">
              没有账号？<a href="#" @click.prevent="authModal.switchToRegister()">注册</a><br />
              <a href="#" class="text-muted" style="font-size:12px" @click.prevent="showReset = true">忘记密码？</a>
            </div>
          </template>

          <!-- Reset password form -->
          <template v-else>
            <h4 class="mb-1">重置密码</h4>
            <p class="text-muted mb-3" style="font-size: 13px">通过用户名和昵称验证身份</p>

            <div class="mb-2">
              <label class="form-label">用户名</label>
              <input v-model="resetUsername" class="form-control" @keyup.enter="doReset" />
            </div>
            <div class="mb-2">
              <label class="form-label">昵称</label>
              <input v-model="resetNickname" class="form-control" @keyup.enter="doReset" />
            </div>
            <div class="mb-3">
              <label class="form-label">新密码</label>
              <input v-model="resetPassword" type="password" class="form-control" @keyup.enter="doReset" />
            </div>

            <div v-if="resetError" class="text-danger mb-2" style="font-size:13px">{{ resetError }}</div>
            <div v-if="resetOk" class="text-success mb-2" style="font-size:13px">{{ resetOk }}</div>
            <button class="btn btn-forum w-100" :disabled="resetting || !!resetOk" @click="doReset">
              {{ resetting ? '处理中...' : '重置密码' }}
            </button>
            <div class="mt-3 text-muted" style="font-size: 12px">
              <a href="#" @click.prevent="cancelReset">返回登录</a>
            </div>
          </template>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import api from '../api/http'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'

const auth = useAuthStore()
const authModal = useAuthModalStore()

const open = computed(() => authModal.mode === 'login')
const username = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)

const showReset = ref(false)
const resetUsername = ref('')
const resetNickname = ref('')
const resetPassword = ref('')
const resetError = ref('')
const resetOk = ref('')
const resetting = ref(false)

function close() {
  authModal.close()
}

watch(open, (v) => {
  if (v) {
    username.value = ''
    password.value = ''
    error.value = ''
    showReset.value = false
    resetUsername.value = ''
    resetNickname.value = ''
    resetPassword.value = ''
    resetError.value = ''
    resetOk.value = ''
    document.body.style.overflow = 'hidden'
  } else if (authModal.mode == null) {
    document.body.style.overflow = ''
  }
})

async function submit() {
  error.value = ''
  loading.value = true
  try {
    await auth.login(username.value, password.value)
    close()
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

function cancelReset() {
  showReset.value = false
  resetError.value = ''
  resetOk.value = ''
}

async function doReset() {
  resetError.value = ''
  resetOk.value = ''
  if (!resetUsername.value.trim()) { resetError.value = '请输入用户名'; return }
  if (!resetNickname.value.trim()) { resetError.value = '请输入昵称'; return }
  if (!resetPassword.value) { resetError.value = '请输入新密码'; return }
  resetting.value = true
  try {
    const { data } = await api.post('/auth/reset-password', {
      username: resetUsername.value,
      nickname: resetNickname.value,
      newPassword: resetPassword.value,
    })
    resetOk.value = data.message || '密码已重置'
    username.value = resetUsername.value
    password.value = ''
  } catch (e) {
    resetError.value = e.response?.data?.message || e.message || '重置失败'
  } finally {
    resetting.value = false
  }
}
</script>
