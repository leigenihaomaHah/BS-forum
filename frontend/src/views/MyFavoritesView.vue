<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; <router-link to="/me">个人中心</router-link> &gt; 我的收藏
    </div>

    <div v-if="!auth.isLoggedIn" class="auth-card" style="max-width: 420px; margin: 48px auto;">
      <h4 class="mb-1">请先登录</h4>
      <p class="text-muted mb-3" style="font-size: 13px">登录后查看和管理收藏</p>
      <button type="button" class="btn btn-forum w-100" @click="authModal.openLogin()">前往登录</button>
    </div>

    <template v-else>
      <div class="fav-layout">
        <!-- Sidebar -->
        <div class="fav-sidebar">
          <div class="fav-sidebar-header">
            <span>收藏分类</span>
            <button class="btn btn-sm btn-forum" @click="showCreateFolder = true" title="新建分类">+</button>
          </div>

          <div class="fav-folder-list">
            <div
              class="fav-folder-item"
              :class="{ active: selectedFolder === undefined }"
              @click="selectFolder(undefined)"
            >全部收藏 <span class="fav-count">{{ totalCount }}</span></div>
            <div
              class="fav-folder-item"
              :class="{ active: selectedFolder === null }"
              @click="selectFolder(null)"
            >未分类 <span class="fav-count">{{ uncategorizedCount }}</span></div>
            <div
              v-for="f in folders"
              :key="f.id"
              class="fav-folder-item"
              :class="{ active: selectedFolder === f.id }"
              @click="selectFolder(f.threadId)"
            >
              <span class="fav-folder-name">{{ f.name }}</span>
              <span class="fav-count">{{ f.count }}</span>
              <div class="fav-folder-actions" @click.stop>
                <button class="fav-folder-action-btn" title="重命名" @click="startRename(f)">✏️</button>
                <button class="fav-folder-action-btn" title="删除" @click="confirmDeleteFolder(f)">🗑️</button>
              </div>
            </div>
          </div>

          <div v-if="loading" class="fav-loading">加载中...</div>
        </div>

        <!-- Main content -->
        <div class="fav-main">
          <div class="fav-toolbar">
            <span class="fav-toolbar-title">{{ currentTitle }}</span>
            <span class="fav-toolbar-count">{{ favorites.length }} 个收藏</span>
          </div>

          <div v-if="loading" class="p-4 text-muted">加载中...</div>
          <div v-else-if="!favorites.length" class="fav-empty">
            <div class="fav-empty-icon">📑</div>
            <div class="fav-empty-text">暂无收藏</div>
            <div class="fav-empty-hint">浏览帖子时点击「收藏」按钮即可添加到此处</div>
          </div>
          <div v-else class="fav-list">
            <div v-for="f in favorites" :key="f.threadId" class="fav-item">
              <div class="fav-item-main">
                <router-link :to="`/thread/${f.threadId}`" class="fav-item-title">{{ f.title }}</router-link>
                <div class="fav-item-meta">{{ f.forumName }} · {{ formatTime(f.createdAt) }}</div>
              </div>
              <div class="fav-item-actions">
                <select
                  class="form-select form-select-sm"
                  :value="f.folderId ?? ''"
                  @change="moveFavorite(f, $event)"
                >
                  <option value="">未分类</option>
                  <option v-for="fol in folders" :key="fol.id" :value="fol.id">{{ fol.name }}</option>
                </select>
                <button class="btn btn-sm btn-outline-secondary ms-1" @click="unfavorite(f)">取消收藏</button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>

    <!-- Create folder modal -->
    <div v-if="showCreateFolder" class="modal-overlay" @click.self="showCreateFolder = false">
      <div class="modal-card" style="max-width: 360px">
        <div class="modal-header">新建分类</div>
        <div class="modal-body">
          <input
            v-model="newFolderName"
            class="form-control"
            placeholder="分类名称"
            maxlength="20"
            @keyup.enter="createFolder"
          />
          <div v-if="folderError" class="text-danger mt-1" style="font-size: 12px">{{ folderError }}</div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-sm btn-outline-secondary" @click="showCreateFolder = false">取消</button>
          <button class="btn btn-sm btn-forum" :disabled="creatingFolder" @click="createFolder">
            {{ creatingFolder ? '创建中...' : '创建' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Rename folder modal -->
    <div v-if="renaming" class="modal-overlay" @click.self="renaming = false">
      <div class="modal-card" style="max-width: 360px">
        <div class="modal-header">重命名分类</div>
        <div class="modal-body">
          <input
            v-model="renameName"
            class="form-control"
            placeholder="分类名称"
            maxlength="20"
            @keyup.enter="doRename"
          />
          <div v-if="folderError" class="text-danger mt-1" style="font-size: 12px">{{ folderError }}</div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-sm btn-outline-secondary" @click="renaming = false">取消</button>
          <button class="btn btn-sm btn-forum" :disabled="renamingFolder" @click="doRename">
            {{ renamingFolder ? '保存中...' : '保存' }}
          </button>
        </div>
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { formatTime } from '../utils/time.js'

const auth = useAuthStore()
const authModal = useAuthModalStore()

const folders = ref([])
const favorites = ref([])
const loading = ref(false)
const selectedFolder = ref(undefined) // undefined=all, null=uncategorized, number=specific

// Create
const showCreateFolder = ref(false)
const newFolderName = ref('')
const creatingFolder = ref(false)

// Rename
const renaming = ref(false)
const renamingFolderId = ref(0)
const renameName = ref('')
const renamingFolder = ref(false)

// Delete
const deletingFolderId = ref(null)

const folderError = ref('')

const totalCount = computed(() => folders.value.reduce((s, f) => s + f.count, 0) + uncategorizedCount.value)
const uncategorizedCount = computed(() => favorites.value.length) // approximate, updated when viewing all

const currentTitle = computed(() => {
  if (selectedFolder.value === undefined) return '全部收藏'
  if (selectedFolder.value === null) return '未分类'
  const f = folders.value.find(x => x.id === selectedFolder.value)
  return f ? f.name : '收藏'
})

function selectFolder(id) {
  selectedFolder.value = id
  loadFavorites()
}

async function loadFolders() {
  try {
    const { data } = await api.get('/me/favorite-folders')
    folders.value = data || []
  } catch { folders.value = [] }
}

async function loadFavorites() {
  loading.value = true
  try {
    const params = {}
    if (selectedFolder.value === null) params.folderId = -1 // send as -1 to represent null/nocategory
    else if (selectedFolder.value !== undefined) params.folderId = selectedFolder.value
    // undefined = all, don't send params
    const { data } = await api.get('/me/favorites', { params })
    favorites.value = data || []
  } catch { favorites.value = [] }
  finally { loading.value = false }
}

async function loadAll() {
  loading.value = true
  await Promise.all([loadFolders(), loadFavorites()])
  loading.value = false
}

// Create folder
async function createFolder() {
  if (!newFolderName.value.trim()) {
    folderError.value = '请输入分类名称'
    return
  }
  creatingFolder.value = true
  folderError.value = ''
  try {
    await api.post('/me/favorite-folders', { name: newFolderName.value.trim() })
    newFolderName.value = ''
    showCreateFolder.value = false
    await loadFolders()
  } catch (e) {
    folderError.value = e.message
  } finally {
    creatingFolder.value = false
  }
}

// Rename folder
function startRename(folder) {
  renamingFolderId.value = folder.id
  renameName.value = folder.name
  renaming.value = true
}

async function doRename() {
  if (!renameName.value.trim()) {
    folderError.value = '请输入分类名称'
    return
  }
  renamingFolder.value = true
  folderError.value = ''
  try {
    await api.put(`/me/favorite-folders/${renamingFolderId.value}`, { name: renameName.value.trim() })
    renaming.value = false
    await loadFolders()
  } catch (e) {
    folderError.value = e.message
  } finally {
    renamingFolder.value = false
  }
}

// Delete folder
async function confirmDeleteFolder(folder) {
  if (deletingFolderId.value === folder.id) {
    // Second click = confirm
    try {
      await api.delete(`/me/favorite-folders/${folder.id}`)
      deletingFolderId.value = null
      if (selectedFolder.value === folder.id) selectedFolder.value = undefined
      await loadAll()
    } catch { }
  } else {
    deletingFolderId.value = folder.id
    setTimeout(() => { deletingFolderId.value = null }, 3000)
  }
}

// Move favorite
async function moveFavorite(fav, event) {
  const val = event.target.value
  const folderId = val === '' ? null : parseInt(val)
  try {
    await api.put(`/me/favorites/${fav.id}/move`, { folderId })
    fav.folderId = folderId
    await loadFolders()
  } catch { }
}

// Unfavorite
async function unfavorite(fav) {
  try {
    await api.post(`/threads/${fav.threadId}/favorite`)
    favorites.value = favorites.value.filter(x => x.threadId !== fav.threadId)
    await loadFolders()
  } catch { }
}

onMounted(loadAll)
</script>

<style scoped>
.fav-layout {
  display: flex;
  gap: 16px;
  min-height: 400px;
}
.fav-sidebar {
  width: 240px;
  flex-shrink: 0;
  background: #fff;
  border: 1px solid rgba(20,32,51,0.08);
  border-radius: 10px;
  overflow: hidden;
}
.fav-sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 14px;
  font-weight: 700;
  font-size: 13px;
  border-bottom: 1px solid rgba(20,32,51,0.08);
}
.fav-folder-list {
  padding: 4px;
}
.fav-folder-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 12px;
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  transition: background 0.1s;
  position: relative;
}
.fav-folder-item:hover {
  background: #f8fafc;
}
.fav-folder-item.active {
  background: rgba(13,148,136,0.08);
  color: #0d9488;
  font-weight: 600;
}
.fav-folder-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.fav-count {
  font-size: 11px;
  color: #94a3b8;
  margin-left: auto;
}
.fav-folder-actions {
  display: none;
  gap: 2px;
  margin-left: 4px;
}
.fav-folder-item:hover .fav-folder-actions {
  display: flex;
}
.fav-folder-action-btn {
  background: none;
  border: none;
  cursor: pointer;
  font-size: 11px;
  padding: 2px;
  line-height: 1;
  opacity: 0.6;
}
.fav-folder-action-btn:hover {
  opacity: 1;
}
.fav-loading {
  padding: 12px;
  font-size: 12px;
  color: #94a3b8;
  text-align: center;
}

.fav-main {
  flex: 1;
  background: #fff;
  border: 1px solid rgba(20,32,51,0.08);
  border-radius: 10px;
  overflow: hidden;
}
.fav-toolbar {
  display: flex;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid rgba(20,32,51,0.08);
}
.fav-toolbar-title {
  font-weight: 700;
  font-size: 14px;
}
.fav-toolbar-count {
  margin-left: auto;
  font-size: 12px;
  color: #94a3b8;
}

.fav-list {
  padding: 0;
}
.fav-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid rgba(20,32,51,0.06);
  transition: background 0.15s;
}
.fav-item:last-child { border-bottom: none; }
.fav-item:hover { background: #f8fafc; }
.fav-item-main {
  flex: 1;
  min-width: 0;
}
.fav-item-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--ink, #142033);
  text-decoration: none;
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.fav-item-title:hover { color: var(--accent, #0d9488); }
.fav-item-meta {
  font-size: 11px;
  color: #7a869c;
  margin-top: 2px;
}
.fav-item-actions {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
  margin-left: 12px;
}
.fav-item-actions select {
  width: auto;
  font-size: 11px;
  padding: 2px 20px 2px 6px;
  min-width: 80px;
}

.fav-empty {
  text-align: center;
  padding: 60px 20px;
}
.fav-empty-icon { font-size: 40px; margin-bottom: 12px; }
.fav-empty-text { font-size: 15px; font-weight: 600; color: #142033; }
.fav-empty-hint { font-size: 12px; color: #94a3b8; margin-top: 6px; }

/* Modal */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}
.modal-card {
  background: #fff;
  border-radius: 12px;
  width: 90%;
  box-shadow: 0 20px 60px rgba(0,0,0,0.2);
}
.modal-header {
  padding: 16px 18px 0;
  font-weight: 700;
  font-size: 15px;
}
.modal-body { padding: 14px 18px; }
.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 0 18px 16px;
}
</style>
