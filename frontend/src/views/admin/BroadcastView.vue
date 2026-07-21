<template>
  <div class="admin-page">
    <h2 class="page-title">广播通知</h2>
    <div class="admin-panel">
      <div class="p-3">
        <div class="mb-2">
          <label class="form-label fw-medium">通知内容</label>
          <textarea v-model="content" class="form-control" rows="4" maxlength="500" placeholder="请输入通知内容（最多 500 字）"></textarea>
        </div>
        <div class="mb-3">
          <label class="form-label fw-medium">发送范围</label>
          <div class="d-flex gap-3">
            <label class="d-flex align-items-center gap-1">
              <input type="radio" v-model="scope" value="all" />
              所有用户
            </label>
            <label class="d-flex align-items-center gap-1">
              <input type="radio" v-model="scope" value="single" />
              指定用户
            </label>
          </div>
          <div v-if="scope === 'single'" class="mt-1">
            <input v-model.number="targetUserId" class="form-control form-control-sm" type="number" placeholder="用户ID" style="width:120px" />
          </div>
        </div>
        <div v-if="error" class="text-danger mb-2" style="font-size:13px">{{ error }}</div>
        <button class="admin-btn admin-btn-primary" :disabled="sending" @click="send">
          {{ sending ? '发送中...' : '发送通知' }}
        </button>
        <span v-if="sent" class="text-success ms-2" style="font-size:13px">已发送</span>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'

const toast = useToastStore()
const content = ref('')
const scope = ref('all')
const targetUserId = ref(null)
const sending = ref(false)
const sent = ref(false)
const error = ref('')

async function send() {
  error.value = ''
  sent.value = false
  if (!content.value.trim()) {
    error.value = '请输入通知内容'
    return
  }
  sending.value = true
  try {
    await api.post('/admin/notifications/broadcast', {
      content: content.value.trim(),
      userId: scope.value === 'single' ? targetUserId.value : null,
    })
    sent.value = true
    content.value = ''
    toast.success('通知已发送')
  } catch (e) {
    error.value = e.response?.data?.message || e.message || '发送失败'
  } finally {
    sending.value = false
  }
}
</script>
