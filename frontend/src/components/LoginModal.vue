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
              <a href="#" class="text-muted" style="font-size:12px" @click.prevent="openReset">忘记密码？</a>
            </div>
          </template>

          <!-- Reset password -->
          <template v-else>
            <h4 class="mb-1">重置密码</h4>
            <p class="text-muted mb-3" style="font-size: 13px">验证用户名与昵称后设置新密码</p>

            <div class="mb-2">
              <label class="form-label">用户名</label>
              <input v-model="resetUsername" class="form-control" autocomplete="username" />
            </div>
            <div class="mb-2">
              <label class="form-label">昵称</label>
              <input v-model="resetNickname" class="form-control" />
            </div>
            <div class="mb-2">
              <label class="form-label">新密码</label>
              <input v-model="resetPassword" type="password" class="form-control" maxlength="32" autocomplete="new-password" />
            </div>
            <div class="mb-2">
              <label class="form-label">验证码</label>
              <div class="captcha-row">
                <input
                  v-model="captchaCode"
                  class="form-control"
                  maxlength="6"
                  autocomplete="off"
                  placeholder="输入图中字符"
                />
                <button type="button" class="captcha-img" title="点击刷新" :disabled="captchaLoading" @click="loadCaptcha">
                  <img v-if="captchaSrc" :src="captchaSrc" alt="验证码" />
                  <span v-else class="captcha-loading">加载中</span>
                </button>
              </div>
            </div>

            <div v-if="error" class="text-danger mb-2" style="font-size:13px">{{ error }}</div>
            <button class="btn btn-forum w-100 mb-2" :disabled="resetLoading" @click="submitReset">
              {{ resetLoading ? '提交中...' : '重置密码' }}
            </button>
            <button class="btn btn-outline-modern w-100" @click="cancelReset">返回登录</button>
          </template>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { useDialogStore } from '../stores/dialog'
import api from '../api/http'
import { passwordError } from '../utils/password.js'

const auth = useAuthStore()
const authModal = useAuthModalStore()
const dialog = useDialogStore()

const open = computed(() => authModal.mode === 'login')
const username = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)
const showReset = ref(false)

const resetUsername = ref('')
const resetNickname = ref('')
const resetPassword = ref('')
const captchaId = ref('')
const captchaCode = ref('')
const captchaSrc = ref('')
const captchaLoading = ref(false)
const resetLoading = ref(false)

function close() {
  authModal.close()
}

async function loadCaptcha() {
  captchaLoading.value = true
  try {
    const { data } = await api.get('/auth/captcha')
    captchaId.value = data.captchaId
    captchaSrc.value = `data:image/svg+xml;base64,${data.imageBase64}`
    captchaCode.value = ''
  } catch (e) {
    error.value = e.message || '验证码加载失败'
  } finally {
    captchaLoading.value = false
  }
}

function openReset() {
  showReset.value = true
  error.value = ''
  resetUsername.value = username.value
  resetNickname.value = ''
  resetPassword.value = ''
  loadCaptcha()
}

watch(open, (v) => {
  if (v) {
    username.value = ''
    password.value = ''
    error.value = ''
    showReset.value = false
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

async function submitReset() {
  error.value = ''
  const pe = passwordError(resetPassword.value)
  if (pe) {
    error.value = pe
    return
  }
  if (!captchaCode.value.trim()) {
    error.value = '请填写验证码'
    return
  }
  resetLoading.value = true
  try {
    await api.post('/auth/reset-password', {
      username: resetUsername.value.trim(),
      nickname: resetNickname.value.trim(),
      newPassword: resetPassword.value,
      captchaId: captchaId.value,
      captchaCode: captchaCode.value.trim(),
    })
    showReset.value = false
    error.value = ''
    username.value = resetUsername.value.trim()
    password.value = ''
    await dialog.alert('密码已重置，请使用新密码登录')
  } catch (e) {
    error.value = e.message
    await loadCaptcha()
  } finally {
    resetLoading.value = false
  }
}

function cancelReset() {
  showReset.value = false
  error.value = ''
}
</script>
