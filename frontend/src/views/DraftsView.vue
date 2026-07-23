<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 草稿箱
    </div>
    <div class="panel">
      <div class="panel-header"><span class="accent"></span>草稿箱</div>
      <div v-if="loading" class="p-3 text-muted">加载中...</div>
      <div v-else-if="!items.length" class="p-4 text-muted">暂无草稿，发帖时会自动保存</div>
      <div v-else>
        <div v-for="d in items" :key="d.id" class="draft-row">
          <div>
            <router-link :to="`/forum/${d.forumId}/new?draft=${d.id}`">{{ d.title || '（无标题）' }}</router-link>
            <div class="meta">版块 #{{ d.forumId }} · {{ d.type }} · {{ timeAgo(d.updatedAt) }}</div>
          </div>
          <button class="btn btn-sm btn-outline-danger" @click="remove(d.id)">删除</button>
        </div>
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AppLayout from '../components/AppLayout.vue'
import api from '../api/http'
import { useDialogStore } from '../stores/dialog'
import { timeAgo } from '../utils/time.js'

const dialog = useDialogStore()
const items = ref([])
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    const { data } = await api.get('/me/drafts')
    items.value = data
  } finally { loading.value = false }
}

async function remove(id) {
  if (!(await dialog.confirm('删除该草稿？', { danger: true, confirmText: '删除' }))) return
  await api.delete(`/me/drafts/${id}`)
  await load()
}

onMounted(load)
</script>

<style scoped>
.draft-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border-bottom: 1px solid #f1f5f9;
}
.meta { font-size: 12px; color: #94a3b8; margin-top: 4px; }
</style>
