<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 私信
    </div>

    <div v-if="!auth.isLoggedIn" class="auth-card" style="max-width: 420px; margin: 48px auto;">
      <h4 class="mb-1">请先登录</h4>
      <p class="text-muted mb-3" style="font-size: 13px">登录后查看与发送私信</p>
      <button type="button" class="btn btn-forum w-100" @click="authModal.openLogin()">前往登录</button>
    </div>

    <div v-else class="panel messages-panel">
      <div class="panel-header pm-header">
        <div class="d-flex align-items-center gap-2">
          <span class="accent"></span>
          <span>私信</span>
          <span v-if="totalUnread" class="header-unread">{{ totalUnread }} 未读</span>
        </div>
        <div class="pm-header-actions">
          <div class="filter-tabs">
            <button type="button" class="filter-tab" :class="{ active: filter === 'all' }" @click="filter = 'all'">全部</button>
            <button type="button" class="filter-tab" :class="{ active: filter === 'unread' }" @click="filter = 'unread'">未读</button>
          </div>
          <button
            type="button"
            class="btn-text"
            :disabled="!totalUnread || markingAll"
            @click="markAllRead"
          >{{ markingAll ? '处理中...' : '全部已读' }}</button>
        </div>
      </div>
      <div class="messages-layout">
        <aside class="conv-list">
          <div v-if="convLoading" class="p-3 text-muted" style="font-size:13px">加载中...</div>
          <div v-else-if="!filteredConversations.length" class="p-3 text-muted" style="font-size:13px">
            {{ filter === 'unread' ? '没有未读私信' : '暂无会话' }}
          </div>
          <button
            v-for="c in filteredConversations"
            :key="c.peerId"
            type="button"
            class="conv-item"
            :class="{ active: peerId === c.peerId, 'has-unread': c.unreadCount > 0 }"
            @click="selectPeer(c.peerId)"
          >
            <img :src="c.peerAvatar || defaultAvatar(c.peerNickname)" class="conv-avatar" alt="" />
            <div class="conv-meta">
              <div class="conv-top">
                <span class="conv-name">{{ c.peerNickname }}</span>
                <span v-if="c.unreadCount" class="conv-unread">{{ c.unreadCount > 99 ? '99+' : c.unreadCount }}</span>
              </div>
              <div class="conv-preview">{{ c.lastContent }}</div>
              <div class="conv-time">{{ timeAgo(c.lastAt) }}</div>
            </div>
          </button>
        </aside>

        <section class="chat-pane">
          <div v-if="!peerId" class="chat-empty text-muted">选择左侧会话，或从用户主页发起私信</div>
          <template v-else>
            <div class="chat-head">
              <router-link :to="`/user/${peerId}`" class="chat-peer">{{ peerName }}</router-link>
              <button
                v-if="peerId"
                type="button"
                class="btn-text"
                :disabled="markingUnread"
                @click="markUnread"
              >{{ markingUnread ? '处理中...' : '标为未读' }}</button>
            </div>
            <div ref="chatBody" class="chat-body">
              <div v-if="msgLoading" class="p-3 text-muted" style="font-size:13px">加载中...</div>
              <template v-else>
                <div
                  v-for="m in messages"
                  :key="m.id"
                  class="bubble-row"
                  :class="{ mine: m.senderId === auth.user.id }"
                >
                  <div class="bubble">
                    <div class="bubble-text">{{ m.content }}</div>
                    <div class="bubble-meta">
                      <span class="bubble-time">{{ formatTime(m.createdAt) }}</span>
                      <span
                        v-if="m.senderId === auth.user.id"
                        class="bubble-read"
                        :class="{ read: m.isRead }"
                      >{{ m.isRead ? '已读' : '未读' }}</span>
                    </div>
                  </div>
                </div>
                <div v-if="!messages.length" class="p-3 text-muted" style="font-size:13px">还没有消息，打个招呼吧</div>
              </template>
            </div>
            <form class="chat-compose" @submit.prevent="send">
              <textarea
                v-model="draft"
                rows="2"
                class="form-control"
                maxlength="1000"
                placeholder="输入私信内容…"
                @keydown.enter.exact.prevent="send"
              />
              <button type="submit" class="btn btn-forum" :disabled="sending || !draft.trim()">
                {{ sending ? '发送中...' : '发送' }}
              </button>
            </form>
            <div v-if="sendError" class="text-danger px-3 pb-2" style="font-size:12px">{{ sendError }}</div>
          </template>
        </section>
      </div>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppLayout from '../components/AppLayout.vue'
import api from '../api/http'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { useToastStore } from '../stores/toast'
import { defaultAvatar } from '../utils/avatar.js'
import { formatTime, timeAgo } from '../utils/time.js'

const auth = useAuthStore()
const authModal = useAuthModalStore()
const toast = useToastStore()
const route = useRoute()
const router = useRouter()

const conversations = ref([])
const convLoading = ref(false)
const peerId = ref(null)
const peerName = ref('')
const messages = ref([])
const msgLoading = ref(false)
const draft = ref('')
const sending = ref(false)
const sendError = ref('')
const chatBody = ref(null)
const filter = ref('all')
const markingAll = ref(false)
const markingUnread = ref(false)

const totalUnread = computed(() =>
  conversations.value.reduce((sum, c) => sum + (c.unreadCount || 0), 0)
)

const filteredConversations = computed(() => {
  if (filter.value === 'unread') {
    return conversations.value.filter((c) => c.unreadCount > 0)
  }
  return conversations.value
})

function notifyUnreadChanged() {
  window.dispatchEvent(new Event('forum:refresh-unread'))
}

async function loadConversations() {
  if (!auth.isLoggedIn) return
  convLoading.value = true
  try {
    const { data } = await api.get('/messages/conversations')
    conversations.value = data || []
  } catch {
    conversations.value = []
  } finally {
    convLoading.value = false
  }
}

async function loadThread(id) {
  if (!id || !auth.isLoggedIn) return
  msgLoading.value = true
  sendError.value = ''
  try {
    const { data } = await api.get(`/messages/with/${id}`)
    messages.value = data || []
    const conv = conversations.value.find((c) => c.peerId === id)
    if (conv) {
      peerName.value = conv.peerNickname
      if (conv.unreadCount > 0) {
        conv.unreadCount = 0
        notifyUnreadChanged()
      }
    } else if (messages.value.length) {
      const m = messages.value[0]
      peerName.value = m.senderId === auth.user.id ? m.receiverNickname : m.senderNickname
    } else {
      try {
        const { data: u } = await api.get(`/users/${id}`)
        peerName.value = u.nickname || `用户 #${id}`
      } catch {
        peerName.value = `用户 #${id}`
      }
    }
    await nextTick()
    scrollBottom()
  } catch (e) {
    messages.value = []
    sendError.value = e.message
  } finally {
    msgLoading.value = false
  }
}

function scrollBottom() {
  const el = chatBody.value
  if (el) el.scrollTop = el.scrollHeight
}

function selectPeer(id) {
  peerId.value = id
  router.replace({ path: '/messages', query: { userId: String(id) } })
}

async function send() {
  const content = draft.value.trim()
  if (!content || !peerId.value || sending.value) return
  sending.value = true
  sendError.value = ''
  try {
    const { data } = await api.post('/messages', { receiverId: peerId.value, content })
    messages.value.push(data)
    draft.value = ''
    await loadConversations()
    await nextTick()
    scrollBottom()
  } catch (e) {
    sendError.value = e.message
  } finally {
    sending.value = false
  }
}

async function markAllRead() {
  if (!totalUnread.value || markingAll.value) return
  markingAll.value = true
  try {
    const { data } = await api.put('/messages/read-all')
    conversations.value.forEach((c) => { c.unreadCount = 0 })
    toast.success(data.message || '全部已读')
    notifyUnreadChanged()
  } catch (e) {
    toast.error(e.message)
  } finally {
    markingAll.value = false
  }
}

async function markUnread() {
  if (!peerId.value || markingUnread.value) return
  markingUnread.value = true
  try {
    await api.put(`/messages/with/${peerId.value}/unread`)
    const conv = conversations.value.find((c) => c.peerId === peerId.value)
    if (conv) conv.unreadCount = Math.max(1, conv.unreadCount || 0)
    else await loadConversations()
    toast.success('已标为未读')
    notifyUnreadChanged()
  } catch (e) {
    toast.error(e.message)
  } finally {
    markingUnread.value = false
  }
}

async function openFromQuery() {
  const q = route.query.userId
  if (!q) return
  const id = Number(q)
  if (!id || id === auth.user?.id) return
  peerId.value = id
  await loadThread(id)
}

onMounted(async () => {
  if (!auth.isLoggedIn) return
  await loadConversations()
  await openFromQuery()
  if (!peerId.value && conversations.value.length) {
    peerId.value = conversations.value[0].peerId
    await loadThread(peerId.value)
  }
})

watch(() => route.query.userId, async () => {
  if (!auth.isLoggedIn) return
  await openFromQuery()
})

watch(peerId, async (id, prev) => {
  if (id && id !== prev) await loadThread(id)
})
</script>

<style scoped>
.messages-panel { overflow: hidden; }
.pm-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
.pm-header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}
.header-unread {
  font-size: 11px;
  font-weight: 700;
  color: #e11d48;
  background: rgba(225, 29, 72, 0.1);
  padding: 2px 8px;
  border-radius: 999px;
}
.filter-tabs {
  display: inline-flex;
  background: #f1f5f9;
  border-radius: 999px;
  padding: 2px;
}
.filter-tab {
  border: none;
  background: transparent;
  padding: 4px 12px;
  font-size: 12px;
  font-weight: 600;
  color: #64748b;
  border-radius: 999px;
  cursor: pointer;
}
.filter-tab.active {
  background: #fff;
  color: #0d9488;
  box-shadow: 0 1px 2px rgba(20, 32, 51, 0.08);
}
.btn-text {
  border: none;
  background: transparent;
  color: #0d9488;
  font-size: 12px;
  font-weight: 600;
  padding: 4px 6px;
  cursor: pointer;
}
.btn-text:disabled {
  opacity: 0.45;
  cursor: default;
}
.messages-layout {
  display: grid;
  grid-template-columns: 280px 1fr;
  min-height: 480px;
  max-height: 70vh;
}
.conv-list {
  border-right: 1px solid rgba(20, 32, 51, 0.08);
  overflow-y: auto;
  background: #f8fafc;
}
.conv-item {
  display: flex;
  gap: 10px;
  width: 100%;
  padding: 12px 14px;
  border: none;
  border-bottom: 1px solid rgba(20, 32, 51, 0.06);
  background: transparent;
  text-align: left;
  cursor: pointer;
  transition: background 0.15s;
}
.conv-item:hover { background: #fff; }
.conv-item.active {
  background: #fff;
  box-shadow: inset 3px 0 0 #0d9488;
}
.conv-item.has-unread .conv-name,
.conv-item.has-unread .conv-preview {
  font-weight: 700;
  color: #142033;
}
.conv-item.has-unread {
  background: rgba(13, 148, 136, 0.04);
}
.conv-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  object-fit: cover;
  flex-shrink: 0;
}
.conv-meta { min-width: 0; flex: 1; }
.conv-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 6px;
}
.conv-name {
  font-size: 13px;
  font-weight: 700;
  color: #142033;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.conv-unread {
  min-width: 18px;
  height: 18px;
  padding: 0 5px;
  border-radius: 999px;
  background: #e11d48;
  color: #fff;
  font-size: 10px;
  font-weight: 700;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.conv-preview {
  font-size: 12px;
  color: #64748b;
  margin-top: 2px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.conv-time { font-size: 11px; color: #94a3b8; margin-top: 2px; }
.chat-pane {
  display: flex;
  flex-direction: column;
  min-width: 0;
}
.chat-empty {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
}
.chat-head {
  padding: 12px 16px;
  border-bottom: 1px solid rgba(20, 32, 51, 0.08);
  font-weight: 700;
  font-size: 14px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}
.chat-peer { color: #142033; text-decoration: none; }
.chat-peer:hover { color: #0d9488; }
.chat-body {
  flex: 1;
  overflow-y: auto;
  padding: 14px 16px;
  background: #fff;
}
.bubble-row {
  display: flex;
  margin-bottom: 10px;
}
.bubble-row.mine { justify-content: flex-end; }
.bubble {
  max-width: 75%;
  padding: 8px 12px;
  border-radius: 12px;
  background: #f1f5f9;
}
.bubble-row.mine .bubble {
  background: rgba(13, 148, 136, 0.12);
}
.bubble-text {
  font-size: 13px;
  color: #142033;
  white-space: pre-wrap;
  word-break: break-word;
}
.bubble-meta {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 4px;
}
.bubble-time {
  font-size: 10px;
  color: #94a3b8;
}
.bubble-read {
  font-size: 10px;
  font-weight: 600;
  color: #94a3b8;
}
.bubble-read.read {
  color: #0d9488;
}
.chat-compose {
  display: flex;
  gap: 8px;
  padding: 12px 14px;
  border-top: 1px solid rgba(20, 32, 51, 0.08);
  align-items: flex-end;
}
.chat-compose textarea { resize: none; flex: 1; }
@media (max-width: 768px) {
  .messages-layout {
    grid-template-columns: 1fr;
    max-height: none;
  }
  .conv-list {
    max-height: 200px;
    border-right: none;
    border-bottom: 1px solid rgba(20, 32, 51, 0.08);
  }
}
</style>
