<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link>
      <span v-if="thread"> / <router-link :to="`/forum/${thread.forumId}`">{{ thread.forumName }}</router-link></span>
      <span> / 帖子</span>
    </div>

    <div v-if="loading" class="p-4 text-muted">加载中...</div>
    <div v-else-if="vipDenied" class="panel restricted-panel">
      <div class="restricted-inner">
        <div class="restricted-icon">🔒</div>
        <div class="restricted-title">会员专区</div>
        <div class="restricted-desc">{{ vipDenied }}</div>
        <router-link class="btn-forum mt-3 d-inline-block" to="/recharge">开通会员</router-link>
      </div>
    </div>
    <div v-else-if="!thread" class="p-4 text-danger">帖子不存在</div>
    <template v-else-if="thread.restricted">
      <div class="panel restricted-panel">
        <div class="restricted-inner">
          <div class="restricted-icon">🔒</div>
          <div class="restricted-title">此帖子仅作者可见</div>
          <div class="restricted-desc">作者已将此帖子设置为私密内容</div>
        </div>
      </div>
    </template>
    <template v-else>
      <div class="panel mb-3">
        <div class="panel-header">
          <div v-if="!editingThread">
            <span class="accent"></span>{{ thread.title }}
            <span v-if="thread.isPinned" class="type-badge type-pin">置顶</span>
            <span v-if="thread.isEssence" class="type-badge type-essence">精品</span>
            <span v-if="thread.repliesLocked" class="type-badge type-locked">禁回</span>
            <router-link
              v-for="tag in (thread.tags || [])"
              :key="tag"
              :to="`/tag/${encodeURIComponent(tag)}`"
              class="type-badge type-tag"
            >#{{ tag }}</router-link>
          </div>
          <div v-else class="thread-edit-title">
            <input v-model="editThreadTitle" class="form-control" maxlength="100" placeholder="标题" />
          </div>
          <div class="d-flex gap-2 align-items-center flex-wrap">
            <button
              v-if="thread.canEdit && !editingThread"
              class="btn-outline-modern"
              @click="startEditThread"
            >编辑主题</button>
            <button v-if="auth.isLoggedIn" class="btn-outline-modern" @click="reportThread">举报</button>
            <span class="text-muted" style="font-size: 12px; font-weight: 400">
              {{ thread.views }} 浏览 · {{ thread.replyCount }} 回复 · {{ thread.likeCount }} 赞
            </span>
            <button class="btn-outline-modern" :class="{ active: favorited }" @click="toggleFavorite">
              {{ favorited ? '已收藏' : '收藏' }}
            </button>
            <button
              class="btn-outline-modern"
              :disabled="thread.repliesLocked || auth.user?.isMuted"
              @click="showTipInput = !showTipInput"
            >
              打赏
            </button>
            <button class="btn-outline-modern" :disabled="thread.likedByMe || thread.repliesLocked" @click="like">
              {{ thread.likedByMe ? '已赞' : '点赞' }}
            </button>
            <button class="btn-outline-modern" @click="shareThread">分享</button>
          </div>
        </div>
      </div>

      <div v-if="thread.canModerate" class="panel mb-3 mod-bar">
        <span class="mod-label">版主操作</span>
        <button class="btn-outline-modern" @click="modAction(thread.isPinned ? 'unpin' : 'pin')">
          {{ thread.isPinned ? '取消置顶' : '置顶' }}
        </button>
        <button class="btn-outline-modern" @click="modAction(thread.repliesLocked ? 'unlock-replies' : 'lock-replies')">
          {{ thread.repliesLocked ? '解除禁回' : '禁止回复' }}
        </button>
        <button class="btn-outline-modern danger" @click="modAction('hide')">隐藏本帖</button>
      </div>

      <div v-if="thread.repliesLocked" class="panel mb-3 p-3">
        <div class="text-warning" style="font-size:13px;font-weight:600">本帖已禁止回复</div>
      </div>

      <div v-if="showTipInput && !thread.repliesLocked" class="panel mb-3 p-3">
        <div v-if="auth.user?.isMuted" class="text-warning" style="font-size:13px">账号已被禁言，暂时无法打赏</div>
        <div v-else class="d-flex gap-2 align-items-center flex-wrap">
          <span style="font-size:13px;font-weight:600">打赏楼主</span>
          <input v-model.number="tipAmount" type="number" min="1" class="form-control" style="width:100px" placeholder="金币" />
          <button class="btn btn-forum btn-sm" :disabled="tipping" @click="tipThread">{{ tipping ? '处理中...' : '打赏' }}</button>
          <span v-if="tipError" class="text-danger" style="font-size:12px">{{ tipError }}</span>
          <span v-if="tipSuccess" class="text-success" style="font-size:12px">{{ tipSuccess }}</span>
        </div>
      </div>

      <div v-if="thread.poll" class="panel mb-3 poll-panel">
        <div class="poll-head">
          <div class="poll-title">投票</div>
          <div class="poll-meta">
            <span>共 {{ thread.poll.totalVotes }} 票</span>
            <span v-if="thread.poll.myOptionId">· 你已投票</span>
            <span v-else-if="!auth.isLoggedIn">· 登录后可投票</span>
            <span v-else>· 点击选项投票</span>
          </div>
        </div>
        <div class="poll-options">
          <button
            v-for="opt in thread.poll.options"
            :key="opt.id"
            type="button"
            class="poll-option"
            :class="{
              voted: thread.poll.myOptionId === opt.id,
              leading: isLeading(opt),
              disabled: !!thread.poll.myOptionId || !auth.isLoggedIn || voting,
            }"
            :disabled="!!thread.poll.myOptionId || !auth.isLoggedIn || voting"
            @click="vote(opt.id)"
          >
            <div class="poll-fill" :style="{ width: (thread.poll.myOptionId || thread.poll.totalVotes > 0 ? pollPct(opt) : 0) + '%' }"></div>
            <div class="poll-row">
              <span class="poll-text">
                <span v-if="thread.poll.myOptionId === opt.id" class="poll-check">✓</span>
                {{ opt.text }}
              </span>
              <span class="poll-stats">
                <span class="poll-count">{{ opt.voteCount }}</span>
                <span class="poll-pct">{{ pollPct(opt) }}%</span>
              </span>
            </div>
          </button>
        </div>
      </div>

      <div v-for="post in posts" :key="post.id" class="post-card" :class="{ 'post-deleted': post.deleted }">
        <div class="post-side">
          <router-link :to="`/user/${post.author.id}`">
            <img :src="post.author.avatar || defaultAvatar(post.author.nickname)" class="post-avatar" />
          </router-link>
          <div class="fw-bold mb-1" style="font-size: 13px">
            <router-link :to="`/user/${post.author.id}`">{{ post.author.nickname }}</router-link>
          </div>
          <span
            class="level-badge"
            :class="{ 'lv-high': post.author.level >= 5 }"
          >Lv.{{ post.author.level }} {{ post.author.levelName }}</span>
          <div class="text-muted mt-2" style="font-size: 12px">积分 {{ post.author.points }}</div>
        </div>
        <div class="post-body">
          <div class="post-floor">
            #{{ post.floor }} · {{ formatTime(post.createdAt) }}
            <span v-if="post.editedAt" class="text-muted" style="font-size:11px">（已编辑）</span>
            <span class="post-actions">
              <button
                v-if="auth.user?.id === post.author.id && !post.deleted && post.floor > 1"
                class="post-action-btn"
                @click="startEdit(post)"
              >编辑</button>
              <button
                v-if="thread.canEdit && !post.deleted && post.floor === 1 && !editingThread"
                class="post-action-btn"
                @click="startEditThread"
              >编辑</button>
              <button v-if="auth.user?.id === post.author.id && !post.deleted && post.floor > 1" class="post-action-btn" @click="deletePost(post)">删除</button>
              <button v-if="auth.isLoggedIn && !post.deleted" class="post-action-btn" @click="quotePost(post)">引用</button>
            </span>
          </div>

          <!-- Edit mode (reply) -->
          <template v-if="editingPost?.id === post.id && !(editingThread && post.floor === 1)">
            <MarkdownEditor v-model="editContent" class="mb-2" :compact="true" :rows="4" :hint="mdHint" />
            <div class="d-flex gap-2 mb-2">
              <button class="btn btn-forum btn-sm" :disabled="savingEdit" @click="saveEdit(post)">{{ savingEdit ? '保存中...' : '保存' }}</button>
              <button class="btn btn-outline-modern btn-sm" @click="cancelEdit">取消</button>
            </div>
          </template>

          <!-- Edit theme (floor 1) -->
          <template v-else-if="editingThread && post.floor === 1">
            <MarkdownEditor v-model="editThreadContent" class="mb-2" :rows="6" :hint="mdHint" />
            <div v-if="editThreadError" class="text-danger mb-2" style="font-size:12px">{{ editThreadError }}</div>
            <div class="d-flex gap-2 mb-2">
              <button class="btn btn-forum btn-sm" :disabled="savingThread" @click="saveThread">{{ savingThread ? '保存中...' : '保存主题' }}</button>
              <button class="btn btn-outline-modern btn-sm" @click="cancelEditThread">取消</button>
            </div>
          </template>

          <!-- Display mode -->
          <template v-else>
            <div v-if="post.deleted" class="text-muted" style="font-style:italic">该回复已被删除</div>
            <div v-else-if="post.hidden" class="hidden-post-box">
              <div class="hidden-post-icon">💰</div>
              <div class="hidden-post-text">此内容需购买后查看</div>
              <div class="hidden-post-price">花费 {{ thread.coinPrice }} 金币购买后可查看完整内容及参与回复</div>
              <div v-if="!auth.isLoggedIn" class="text-muted mt-1" style="font-size:13px">
                请先 <a href="#" @click.prevent="authModal.openLogin()">登录</a> 后购买
              </div>
              <button v-else :disabled="purchasing" class="btn btn-forum btn-sm mt-1" @click="purchaseThread">
                {{ purchasing ? '处理中...' : `花费 ${thread.coinPrice} 金币购买` }}
              </button>
              <div v-if="purchaseError" class="text-danger mt-1" style="font-size:12px">{{ purchaseError }}</div>
            </div>
            <div v-else>
              <div v-if="post.replyToFloor" class="quote-box mb-2">
                引用 #{{ post.replyToFloor }} {{ post.replyToNickname }}：{{ post.replyToContent }}
              </div>
              <MarkdownBody :content="post.content" />
            </div>
          </template>

          <!-- Image grid -->
          <div v-if="post.images?.length && !post.deleted" class="img-grid">
            <div
              v-for="(img, idx) in post.images"
              :key="idx"
              class="img-grid-item"
              @click="openLightbox(post.images, idx)"
            >
              <img :src="img" alt="" loading="lazy" />
              <div v-if="post.images.length > 4 && idx === 3" class="img-more">
                +{{ post.images.length - 4 }}
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-if="postTotalPages > 1" class="p-3 d-flex gap-2 justify-content-center align-items-center">
        <button class="btn btn-sm btn-outline-secondary" :disabled="postPage <= 1 || loadingPosts" @click="loadPosts(postPage - 1)">上一页</button>
        <span class="text-muted" style="font-size:13px">{{ postPage }} / {{ postTotalPages }}（{{ postTotal }} 回复）</span>
        <button class="btn btn-sm btn-outline-secondary" :disabled="postPage >= postTotalPages || loadingPosts" @click="loadPosts(postPage + 1)">下一页</button>
      </div>

      <div v-if="!thread.repliesLocked" class="panel">
        <div class="panel-header"><span class="accent"></span>发表回复</div>
        <div class="p-3">
          <div v-if="!auth.isLoggedIn" class="text-muted">
            请先 <a href="#" @click.prevent="authModal.openLogin()">登录</a> 后回帖（回帖 +2 积分、+2 金币）
          </div>
          <div v-else-if="auth.user?.isMuted" class="text-warning">
            账号已被禁言{{ auth.user.mutedUntil ? '，至 ' + new Date(auth.user.mutedUntil).toLocaleString() : '' }}，暂时无法回帖
          </div>
          <template v-else>
            <div v-if="replyTo" class="quote-pending mb-2">
              引用 #{{ replyTo.floor }} {{ replyTo.author.nickname }}
              <button type="button" class="btn-link" @click="replyTo = null">取消</button>
            </div>
            <MarkdownEditor ref="replyTextarea" v-model="reply" class="mb-2" :compact="true" :rows="4" :hint="mdHint + ' · 纯文字至少 5 字'" placeholder="至少 5 个字… 可用 @昵称 提醒，支持 Markdown" />

            <div class="reply-images mb-2">
              <div class="reply-img-list">
                <div v-for="(img, idx) in replyImages" :key="idx" class="reply-img-item">
                  <img :src="img" class="reply-img-thumb" alt="" />
                  <button type="button" class="reply-img-remove" @click="replyImages.splice(idx, 1)">&times;</button>
                </div>
                <label v-if="replyImages.length < 8" class="reply-img-add">
                  <input type="file" accept="image/*" multiple hidden @change="addReplyImages" />
                  <span>+</span>
                </label>
              </div>
              <div class="text-muted" style="font-size:12px;margin-top:6px">支持 JPG / PNG / GIF，最多 8 张</div>
            </div>

            <div v-if="error" class="text-danger mb-2">{{ error }}</div>
            <button class="btn btn-forum" :disabled="submitting" @click="submitReply">{{ submitting ? '提交中...' : '提交回复' }}</button>
            <span class="text-muted ms-2" style="font-size:12px">回帖 +2 积分、+2 金币 · 间隔 {{ replyCooldown }} 秒 · 日限 20 次</span>
          </template>
        </div>
      </div>
    </template>

    <!-- Lightbox -->
    <div v-if="lightboxOpen" class="lightbox-overlay" @click.self="lightboxOpen = false">
      <button class="lightbox-close" @click="lightboxOpen = false">&times;</button>
      <button v-if="lightboxIdx > 0" class="lightbox-nav lightbox-prev" @click="lightboxIdx--">&lsaquo;</button>
      <img :src="lightboxImages[lightboxIdx]" class="lightbox-img" />
      <button v-if="lightboxIdx < lightboxImages.length - 1" class="lightbox-nav lightbox-next" @click="lightboxIdx++">&rsaquo;</button>
      <div class="lightbox-counter">{{ lightboxIdx + 1 }} / {{ lightboxImages.length }}</div>
    </div>
  </AppLayout>
</template>

<script setup>
import { onMounted, ref, watch, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import MarkdownBody from '../components/MarkdownBody.vue'
import MarkdownEditor from '../components/MarkdownEditor.vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { useToastStore } from '../stores/toast'
import { markdownHint } from '../utils/markdown.js'
import { compressImage } from '../utils/image.js'
import { uploadImages } from '../utils/upload.js'
import { formatTime } from '../utils/time.js'
import { defaultAvatar } from '../utils/avatar.js'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const authModal = useAuthModalStore()
const toast = useToastStore()
const thread = ref(null)
const posts = ref([])
const postPage = ref(1)
const postTotal = ref(0)
const postPageSize = 20
const postTotalPages = computed(() => Math.max(1, Math.ceil(postTotal.value / postPageSize)))
const loading = ref(true)
const loadingPosts = ref(false)
const vipDenied = ref('')
const reply = ref('')
const replyImages = ref([])
const error = ref('')
const submitting = ref(false)
const replyCooldown = 15
const minReplyLength = 5
const replyTextarea = ref(null)
const replyTo = ref(null)
const voting = ref(false)
const mdHint = markdownHint()

const lightboxOpen = ref(false)
const lightboxImages = ref([])
const lightboxIdx = ref(0)

const editingPost = ref(null)
const editContent = ref('')
const savingEdit = ref(false)

const editingThread = ref(false)
const editThreadTitle = ref('')
const editThreadContent = ref('')
const savingThread = ref(false)
const editThreadError = ref('')

const purchasing = ref(false)
const purchaseError = ref('')
const favorited = ref(false)
const showTipInput = ref(false)
const tipAmount = ref(1)
const tipping = ref(false)
const tipError = ref('')
const tipSuccess = ref('')
const modBusy = ref(false)

function openLightbox(images, idx) {
  lightboxImages.value = images
  lightboxIdx.value = idx
  lightboxOpen.value = true
}

function addReplyImages(e) {
  const files = Array.from(e.target.files || [])
  const remaining = 8 - replyImages.value.length
  for (const file of files.slice(0, remaining)) {
    if (file.size > 10 * 1024 * 1024) {
      error.value = `"${file.name}" 超过 10MB 限制`
      continue
    }
    compressImage(file, 1920, 0.85).then((dataUrl) => {
      replyImages.value.push(dataUrl)
    })
  }
  e.target.value = ''
}

async function loadPosts(p = 1) {
  if (!thread.value || thread.value.restricted) return
  loadingPosts.value = true
  postPage.value = p
  try {
    const { data } = await api.get(`/threads/${route.params.id}/posts`, { params: { page: p, pageSize: postPageSize } })
    posts.value = data.items || []
    postTotal.value = data.total || 0
  } catch {
    posts.value = []
    postTotal.value = 0
  } finally {
    loadingPosts.value = false
  }
}

async function load() {
  loading.value = true
  vipDenied.value = ''
  posts.value = []
  try {
    const { data } = await api.get(`/threads/${route.params.id}`)
    thread.value = data
    favorited.value = !!data.favorited
    if (!data.restricted) await loadPosts(1)
  } catch (e) {
    thread.value = null
    const msg = e.response?.data?.message || e.message || ''
    if (e.response?.status === 403 || String(msg).includes('会员')) {
      vipDenied.value = msg || '该帖子所在版块需要会员权限'
    }
  } finally {
    loading.value = false
  }
}

function startEdit(post) {
  if (post.floor === 1 && thread.value?.canEdit) {
    startEditThread()
    return
  }
  editingPost.value = post
  editContent.value = post.content
}

function cancelEdit() {
  editingPost.value = null
  editContent.value = ''
}

function startEditThread() {
  const first = posts.value.find((p) => p.floor === 1)
  editingThread.value = true
  editThreadTitle.value = thread.value.title
  editThreadContent.value = first?.content || ''
  editThreadError.value = ''
  cancelEdit()
}

function cancelEditThread() {
  editingThread.value = false
  editThreadError.value = ''
}

async function saveThread() {
  editThreadError.value = ''
  if (!editThreadTitle.value.trim()) {
    editThreadError.value = '请填写标题'
    return
  }
  savingThread.value = true
  try {
    const first = posts.value.find((p) => p.floor === 1)
    const { data } = await api.put(`/threads/${route.params.id}`, {
      title: editThreadTitle.value,
      content: editThreadContent.value,
      images: first?.images || [],
    })
    thread.value = data
    favorited.value = !!data.favorited
    editingThread.value = false
  } catch (e) {
    editThreadError.value = e.response?.data?.message || e.message || '保存失败'
  } finally {
    savingThread.value = false
  }
}

async function modAction(action) {
  if (modBusy.value) return
  const labels = {
    pin: '置顶', unpin: '取消置顶',
    'lock-replies': '禁止回复', 'unlock-replies': '解除禁回',
    hide: '隐藏',
  }
  const reason = prompt(`${labels[action] || action}原因（可空）`)
  if (reason === null) return
  if (action === 'hide' && !confirm('隐藏后普通用户将看不到本帖，确定？')) return
  modBusy.value = true
  try {
    await api.post(`/threads/${route.params.id}/mod/${action}`, { reason: reason || null })
    if (action === 'hide') {
      router.push(thread.value.forumId ? `/forum/${thread.value.forumId}` : '/')
      return
    }
    await load()
  } catch (e) {
    toast.error(e.response?.data?.message || e.message || '操作失败')
  } finally {
    modBusy.value = false
  }
}

async function saveEdit(post) {
  savingEdit.value = true
  try {
    await api.put(`/posts/${post.id}`, { content: editContent.value })
    post.content = editContent.value
    post.editedAt = new Date().toISOString()
    editingPost.value = null
  } catch (e) {
    error.value = e.message
  } finally {
    savingEdit.value = false
  }
}

async function deletePost(post) {
  if (!confirm('确定删除此回复？')) return
  try {
    await api.delete(`/posts/${post.id}`)
    posts.value = posts.value.filter((p) => p.id !== post.id)
    thread.value.replyCount = Math.max(0, thread.value.replyCount - 1)
  } catch (e) {
    error.value = e.message
  }
}

function quotePost(post) {
  replyTo.value = post
  replyTextarea.value?.focus()
}

function pollPct(opt) {
  const total = thread.value?.poll?.totalVotes || 0
  if (!total) return 0
  return Math.round((opt.voteCount / total) * 100)
}

function isLeading(opt) {
  const poll = thread.value?.poll
  if (!poll?.totalVotes) return false
  const max = Math.max(...poll.options.map((o) => o.voteCount))
  return opt.voteCount === max && opt.voteCount > 0
}

async function vote(optionId) {
  voting.value = true
  try {
    const { data } = await api.post(`/threads/${route.params.id}/vote`, { optionId })
    thread.value.poll = data
  } catch (e) { toast.error(e.message) }
  finally { voting.value = false }
}

async function reportThread() {
  const reason = prompt('请填写举报原因')
  if (!reason) return
  try {
    await api.post('/reports', { targetType: 'thread', targetId: Number(route.params.id), reason })
    toast.success('举报已提交')
  } catch (e) { toast.error(e.message) }
}

async function submitReply() {
  error.value = ''
  const text = reply.value.trim()
  if (!text && !replyImages.value.length) {
    error.value = '请输入内容或添加图片'
    return
  }
  if (!replyImages.value.length && text.length < minReplyLength) {
    error.value = `回帖内容至少 ${minReplyLength} 个字`
    return
  }
  submitting.value = true
  let images = []
  if (replyImages.value.length > 0) {
    try {
      images = await uploadImages([...replyImages.value])
    } catch (e) {
      error.value = '图片上传失败：' + e.message
      submitting.value = false
      return
    }
  }
  try {
    const { data } = await api.post(`/threads/${route.params.id}/replies`, {
      content: reply.value,
      images,
      replyToPostId: replyTo.value?.id || null,
    })
    posts.value.push(data)
    thread.value.replyCount += 1
    reply.value = ''
    replyImages.value = []
    replyTo.value = null
    await auth.fetchMe()
  } catch (e) {
    error.value = e.message
  } finally {
    submitting.value = false
  }
}

async function purchaseThread() {
  purchasing.value = true
  purchaseError.value = ''
  try {
    await api.post(`/threads/${route.params.id}/purchase`)
    await auth.fetchMe()
    await load()
  } catch (e) {
    purchaseError.value = e.message
  } finally {
    purchasing.value = false
  }
}

async function toggleFavorite() {
  if (!auth.isLoggedIn) {
    error.value = '请先登录'
    return
  }
  try {
    const { data } = await api.post(`/threads/${route.params.id}/favorite`)
    favorited.value = data.favorited
  } catch (e) {
    error.value = e.message
  }
}

async function tipThread() {
  if (!auth.isLoggedIn) {
    tipError.value = '请先登录'
    return
  }
  tipping.value = true
  tipError.value = ''
  tipSuccess.value = ''
  try {
    const { data } = await api.post(`/threads/${route.params.id}/tip`, { amount: tipAmount.value })
    tipSuccess.value = data.message
    await auth.fetchMe()
  } catch (e) {
    tipError.value = e.message
  } finally {
    tipping.value = false
  }
}

async function like() {
  try {
    await api.post(`/threads/${route.params.id}/like`)
    thread.value.likedByMe = true
    thread.value.likeCount += 1
  } catch (e) {
    error.value = e.message
  }
}

function shareThread() {
  const url = window.location.origin + '/thread/' + route.params.id
  if (navigator.share) {
    navigator.share({ title: thread.value?.title || '', url }).catch(() => {})
  } else {
    navigator.clipboard.writeText(url).then(() => {
      toast.success('链接已复制到剪贴板')
    }).catch(() => {
      toast.error('复制失败，请手动复制地址栏链接')
    })
  }
}

onMounted(load)
watch(() => route.params.id, load)
</script>

<style scoped>
/* Avatar */
.post-avatar {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  object-fit: cover;
  display: block;
  margin-bottom: 8px;
}

/* Restricted panel */
.restricted-panel { padding: 60px 20px; }
.restricted-inner {
  text-align: center;
  max-width: 380px;
  margin: 0 auto;
}
.restricted-icon { font-size: 48px; margin-bottom: 12px; }
.restricted-title {
  font-size: 18px;
  font-weight: 700;
  color: var(--ink, #142033);
  margin-bottom: 8px;
}
.restricted-desc {
  font-size: 13px;
  color: var(--muted, #7a869c);
}

/* Hidden post (coin thread, not purchased) */
.hidden-post-box {
  text-align: center;
  padding: 28px 16px;
  background: #fafbfc;
  border: 1px dashed var(--line, rgba(20,32,51,0.12));
  border-radius: 10px;
}
.hidden-post-icon { font-size: 32px; margin-bottom: 6px; }
.hidden-post-text {
  font-size: 15px;
  font-weight: 600;
  color: var(--ink, #142033);
  margin-bottom: 4px;
}
.hidden-post-price {
  font-size: 12px;
  color: var(--muted, #7a869c);
  margin-bottom: 6px;
}

/* Post actions */
.post-actions {
  float: right;
  display: flex;
  gap: 2px;
}
.post-action-btn {
  background: none;
  border: none;
  color: var(--muted, #7a869c);
  font-size: 12px;
  cursor: pointer;
  padding: 2px 6px;
  border-radius: 4px;
  transition: all 0.15s;
}
.post-action-btn:hover {
  color: var(--accent, #0d9488);
  background: rgba(13,148,136,0.08);
}

/* Quote block */
.quote-block {
  margin: 0 0 8px 0;
  padding: 8px 12px;
  background: #f8fafc;
  border-left: 3px solid var(--accent, #0d9488);
  border-radius: 0 6px 6px 0;
  color: #3d4a63;
  font-size: 13px;
  white-space: pre-wrap;
}

/* Deleted post */
.post-deleted {
  opacity: 0.6;
}

/* Image Grid — natural aspect ratio, no crop */
.img-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
}
.img-grid-item {
  position: relative;
  cursor: pointer;
  border-radius: 8px;
  overflow: hidden;
  flex: 0 0 auto;
  max-width: 100%;
  background: #f0f2f5;
}
.img-grid-item img {
  display: block;
  max-width: 100%;
  height: auto;
  max-height: 400px;
  object-fit: contain;
  transition: opacity 0.2s;
}
.img-grid-item:hover img {
  opacity: 0.92;
}
.img-more {
  position: absolute;
  inset: 0;
  background: rgba(0,0,0,0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 28px;
  font-weight: 700;
}

/* Reply image upload */
.reply-images { user-select: none; }
.reply-img-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.reply-img-item {
  position: relative;
  width: 64px;
  height: 64px;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid var(--line, rgba(20,32,51,0.08));
}
.reply-img-thumb {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.reply-img-remove {
  position: absolute;
  top: 2px;
  right: 2px;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  border: none;
  background: rgba(0,0,0,0.5);
  color: #fff;
  font-size: 14px;
  line-height: 1;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}
.reply-img-add {
  width: 64px;
  height: 64px;
  border: 2px dashed var(--line, rgba(20,32,51,0.12));
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: var(--muted, #7a869c);
  font-size: 24px;
  transition: border-color 0.2s;
}
.reply-img-add:hover {
  border-color: var(--accent, #0d9488);
  color: var(--accent, #0d9488);
}

/* Lightbox */
.lightbox-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.88);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
}
.lightbox-img {
  max-width: 88vw;
  max-height: 88vh;
  object-fit: contain;
  border-radius: 8px;
}
.lightbox-close {
  position: absolute;
  top: 20px;
  right: 24px;
  background: none;
  border: none;
  color: #fff;
  font-size: 36px;
  cursor: pointer;
  opacity: 0.7;
  transition: opacity 0.2s;
}
.lightbox-close:hover { opacity: 1; }
.lightbox-nav {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  background: rgba(255,255,255,0.1);
  border: none;
  color: #fff;
  font-size: 44px;
  width: 52px;
  height: 52px;
  border-radius: 50%;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.2s;
}
.lightbox-nav:hover { background: rgba(255,255,255,0.2); }
.lightbox-prev { left: 16px; }
.lightbox-next { right: 16px; }
.lightbox-counter {
  position: absolute;
  bottom: 24px;
  left: 50%;
  transform: translateX(-50%);
  color: rgba(255,255,255,0.5);
  font-size: 14px;
}
.type-badge {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
  margin-left: 8px;
  vertical-align: middle;
  font-weight: 600;
}
.type-pin { background: #fee2e2; color: #b91c1c; }
.type-essence { background: #fff7ed; color: #c2410c; }
.type-locked { background: #fef3c7; color: #b45309; }
.quote-box, .quote-pending {
  font-size: 12px;
  color: #64748b;
  background: #f8fafc;
  border-left: 3px solid #0d9488;
  padding: 8px 10px;
  border-radius: 0 6px 6px 0;
}
.btn-link {
  border: none;
  background: none;
  color: #0d9488;
  font-size: 12px;
  cursor: pointer;
  margin-left: 8px;
}
.poll-panel { padding: 16px 18px 18px; }
.poll-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 12px;
  margin-bottom: 14px;
}
.poll-title {
  font-size: 15px;
  font-weight: 700;
  color: #142033;
}
.poll-meta { font-size: 12px; color: #7a869c; }
.poll-options { display: flex; flex-direction: column; gap: 10px; }
.poll-option {
  position: relative;
  display: block;
  width: 100%;
  border: 1px solid rgba(20, 32, 51, 0.1);
  border-radius: 10px;
  background: #f8fafc;
  padding: 0;
  overflow: hidden;
  text-align: left;
  cursor: pointer;
  transition: border-color 0.15s, box-shadow 0.15s;
}
.poll-option:hover:not(.disabled) {
  border-color: #0d9488;
  box-shadow: 0 0 0 3px rgba(13, 148, 136, 0.08);
}
.poll-option.disabled { cursor: default; }
.poll-option.voted {
  border-color: #0d9488;
  background: rgba(13, 148, 136, 0.04);
}
.poll-fill {
  position: absolute;
  inset: 0 auto 0 0;
  background: rgba(13, 148, 136, 0.14);
  transition: width 0.35s ease;
  pointer-events: none;
}
.poll-option.leading .poll-fill {
  background: rgba(13, 148, 136, 0.22);
}
.poll-row {
  position: relative;
  z-index: 1;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 12px 14px;
  min-height: 46px;
}
.poll-text {
  font-size: 14px;
  font-weight: 600;
  color: #142033;
  display: flex;
  align-items: center;
  gap: 6px;
}
.poll-check {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: #0d9488;
  color: #fff;
  font-size: 11px;
  font-weight: 700;
}
.poll-stats {
  display: flex;
  align-items: baseline;
  gap: 8px;
  flex-shrink: 0;
  font-variant-numeric: tabular-nums;
}
.poll-count { font-size: 13px; font-weight: 700; color: #142033; }
.poll-pct { font-size: 12px; color: #7a869c; min-width: 36px; text-align: right; }
.type-tag {
  text-decoration: none;
  background: #ecfeff;
  color: #0f766e;
  border: 1px solid #a5f3fc;
}
.type-tag:hover { background: #cffafe; color: #0d9488; }
.mod-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  background: #fffbeb;
  border: 1px solid #fde68a;
}
.mod-label {
  font-size: 12px;
  font-weight: 700;
  color: #b45309;
  margin-right: 4px;
}
.btn-outline-modern.danger {
  color: #b91c1c;
  border-color: #fecaca;
}
.btn-outline-modern.danger:hover {
  background: #fef2f2;
}
.md-hint {
  font-size: 11px;
  color: #94a3b8;
}
.thread-edit-title {
  flex: 1;
  min-width: 200px;
  margin-right: 12px;
}
</style>
