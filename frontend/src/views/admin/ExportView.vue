<template>
  <div class="admin-page">
    <h2 class="page-title">数据导出</h2>

    <div class="admin-panel">
      <div class="p-3">
        <div class="export-row">
          <div>
            <div class="fw-medium">用户数据</div>
            <div class="text-muted" style="font-size:12px">导出所有用户的 ID、用户名、昵称、等级、积分、金币、注册时间等信息为 CSV</div>
          </div>
          <button class="admin-btn admin-btn-primary" :disabled="exporting" @click="exportUsers">{{ exporting ? '导出中...' : '导出用户' }}</button>
        </div>
        <div class="export-row">
          <div>
            <div class="fw-medium">帖子数据</div>
            <div class="text-muted" style="font-size:12px">导出所有帖子的 ID、标题、类型、作者、版块、回复数、浏览数等信息为 CSV</div>
          </div>
          <button class="admin-btn admin-btn-primary" :disabled="exporting" @click="exportThreads">{{ exporting ? '导出中...' : '导出帖子' }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import api from '../../api/http'
import { useToastStore } from '../../stores/toast'

const toast = useToastStore()
const exporting = ref(false)

async function exportUsers() {
  exporting.value = true
  try {
    const { data } = await api.get('/admin/export/users', { responseType: 'blob' })
    downloadBlob(data, `users_${new Date().toISOString().slice(0, 10)}.csv`)
    toast.success('用户数据已导出')
  } catch (e) { toast.error('导出失败：' + e.message) }
  finally { exporting.value = false }
}

async function exportThreads() {
  exporting.value = true
  try {
    const { data } = await api.get('/admin/export/threads', { responseType: 'blob' })
    downloadBlob(data, `threads_${new Date().toISOString().slice(0, 10)}.csv`)
    toast.success('帖子数据已导出')
  } catch (e) { toast.error('导出失败：' + e.message) }
  finally { exporting.value = false }
}

function downloadBlob(blob, filename) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<style scoped>
.export-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 16px 0;
  border-bottom: 1px solid #f0f2f5;
}
.export-row:last-child { border-bottom: none; }
</style>
