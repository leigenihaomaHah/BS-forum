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

          <!-- Reset password：弱校验已关闭，引导联系管理员 -->
          <template v-else>
            <h4 class="mb-1">重置密码</h4>
            <p class="text-muted mb-3" style="font-size: 13px">
              出于安全考虑，已关闭「用户名+昵称」自助找回。请联系站长在管理后台为你重置密码。
            </p>
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

const auth = useAuthStore()
const authModal = useAuthModalStore()

const open = computed(() => authModal.mode === 'login')
const username = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)
const showReset = ref(false)

function close() {
  authModal.close()
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

function cancelReset() {
  showReset.value = false
}
</script>
