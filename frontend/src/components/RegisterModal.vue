<template>
  <Teleport to="body">
    <Transition name="auth-fade">
      <div v-if="open" class="auth-overlay" @click.self="close">
        <div class="auth-modal" role="dialog" aria-modal="true" aria-labelledby="register-title">
          <button type="button" class="auth-close" aria-label="关闭" @click="close">×</button>
          <h4 id="register-title" class="mb-1">加入社区</h4>
          <p class="text-muted mb-3" style="font-size: 13px">注册即赠 10 金币，开启你的等级之路</p>

          <div class="mb-2">
            <label class="form-label">用户名</label>
            <input
              v-model="username"
              class="form-control"
              maxlength="20"
              autocomplete="username"
              placeholder="3–20 位，字母/数字/下划线"
              @keyup.enter="submit"
            />
          </div>

          <div class="mb-2">
            <label class="form-label">昵称（可选）</label>
            <input v-model="nickname" class="form-control" maxlength="20" placeholder="不填则使用用户名" />
          </div>

          <div class="mb-2">
            <label class="form-label">密码</label>
            <input
              v-model="password"
              type="password"
              class="form-control"
              maxlength="32"
              autocomplete="new-password"
              placeholder="8–32 位，需含字母和数字"
              @keyup.enter="submit"
            />
            <ul class="pwd-rules">
              <li :class="{ ok: rules.len }">长度 8–32 位</li>
              <li :class="{ ok: rules.letter }">包含字母</li>
              <li :class="{ ok: rules.digit }">包含数字</li>
              <li :class="{ ok: rules.noSpace }">不含空格</li>
            </ul>
          </div>

          <div class="mb-2">
            <label class="form-label">确认密码</label>
            <input
              v-model="password2"
              type="password"
              class="form-control"
              maxlength="32"
              autocomplete="new-password"
              placeholder="再次输入密码"
              @keyup.enter="submit"
            />
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
                @keyup.enter="submit"
              />
              <button type="button" class="captcha-img" title="点击刷新" :disabled="captchaLoading" @click="loadCaptcha">
                <img v-if="captchaSrc" :src="captchaSrc" alt="验证码" />
                <span v-else class="captcha-loading">加载中</span>
              </button>
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">邀请码（可选）</label>
            <input v-model="inviteCodeLocal" class="form-control" placeholder="填写可得额外金币" />
          </div>

          <div v-if="error" class="text-danger mb-2" style="font-size:13px">{{ error }}</div>
          <button class="btn btn-forum w-100" :disabled="loading" @click="submit">
            {{ loading ? '注册中...' : '注册并登录' }}
          </button>
          <div class="mt-3 text-muted" style="font-size: 12px">
            已有账号？<a href="#" @click.prevent="authModal.switchToLogin()">登录</a>
            · 注册赠送 10 金币
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import api from '../api/http'
import { checkPasswordRules, passwordError } from '../utils/password.js'

const auth = useAuthStore()
const authModal = useAuthModalStore()

const open = computed(() => authModal.mode === 'register')
const username = ref('')
const nickname = ref('')
const password = ref('')
const password2 = ref('')
const inviteCodeLocal = ref('')
const captchaId = ref('')
const captchaCode = ref('')
const captchaSrc = ref('')
const captchaLoading = ref(false)
const error = ref('')
const loading = ref(false)

const rules = computed(() => checkPasswordRules(password.value))

function resetForm() {
  username.value = ''
  nickname.value = ''
  password.value = ''
  password2.value = ''
  captchaCode.value = ''
  captchaSrc.value = ''
  captchaId.value = ''
  error.value = ''
  inviteCodeLocal.value = authModal.inviteCode || ''
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

function close() {
  authModal.close()
}

watch(open, (v) => {
  if (v) {
    resetForm()
    loadCaptcha()
    document.body.style.overflow = 'hidden'
  } else if (!authModal.mode) {
    document.body.style.overflow = ''
  }
})

async function submit() {
  error.value = ''
  const u = username.value.trim()
  if (u.length < 3 || u.length > 20) {
    error.value = '用户名长度需为 3-20 个字符'
    return
  }
  if (!/^[a-zA-Z0-9_]+$/.test(u)) {
    error.value = '用户名仅支持字母、数字和下划线'
    return
  }
  const pe = passwordError(password.value)
  if (pe) {
    error.value = pe
    return
  }
  if (password.value !== password2.value) {
    error.value = '两次输入的密码不一致'
    return
  }
  if (!captchaCode.value.trim()) {
    error.value = '请填写验证码'
    return
  }

  loading.value = true
  try {
    await auth.register(
      u,
      password.value,
      nickname.value || null,
      inviteCodeLocal.value || null,
      captchaId.value,
      captchaCode.value.trim(),
    )
    close()
  } catch (e) {
    error.value = e.message
    await loadCaptcha()
  } finally {
    loading.value = false
  }
}
</script>
