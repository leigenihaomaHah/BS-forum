<template>
  <AppLayout>
    <div class="breadcrumb-bar">
      <router-link to="/">首页</router-link> &gt; 发帖
    </div>
    <div class="panel">
      <div class="panel-header"><span class="accent"></span>发表新主题</div>
      <div class="p-3">
        <div v-if="vipDenied" class="text-center py-4">
          <div style="font-size:32px;margin-bottom:8px">🔒</div>
          <div class="fw-bold mb-2">{{ vipDenied }}</div>
          <router-link class="btn-forum" to="/recharge">开通会员</router-link>
        </div>
        <div v-else-if="!auth.isLoggedIn" class="text-muted">
          请先 <a href="#" @click.prevent="authModal.openLogin()">登录</a>
        </div>
        <template v-else>
          <div class="mb-2 text-muted" style="font-size: 12px">
            <span v-if="!canPost">发帖需 Lv.2（积分≥50）。</span>
            <span v-else>发帖成功后 +10 积分！</span>
            当前：Lv.{{ auth.user.level }} {{ auth.user.levelName }}，积分 {{ auth.user.points }}
            <span v-if="nextLv">｜距离 Lv.{{ nextLv.level }} {{ nextLv.name }} 还差 {{ nextLv.minPoints - auth.user.points }} 分</span>
            <span v-if="draftHint" class="draft-hint"> · {{ draftHint }}</span>
          </div>
          <input v-model="title" class="form-control mb-2" maxlength="100" placeholder="标题" />

          <div class="type-selector mb-2">
            <label class="type-option" :class="{ active: threadType === 'public' }">
              <input type="radio" v-model="threadType" value="public" />
              <span class="type-icon">🌍</span>
              <span class="type-label">公开</span>
              <span class="type-desc">所有人可见</span>
            </label>
            <label class="type-option" :class="{ active: threadType === 'private' }">
              <input type="radio" v-model="threadType" value="private" />
              <span class="type-icon">🔒</span>
              <span class="type-label">私密</span>
              <span class="type-desc">仅自己可见</span>
            </label>
            <label class="type-option" :class="{ active: threadType === 'coin' }">
              <input type="radio" v-model="threadType" value="coin" />
              <span class="type-icon">💰</span>
              <span class="type-label">金币购买</span>
              <span class="type-desc">需支付金币查看</span>
            </label>
            <label class="type-option" :class="{ active: threadType === 'poll' }">
              <input type="radio" v-model="threadType" value="poll" />
              <span class="type-icon">📊</span>
              <span class="type-label">投票</span>
              <span class="type-desc">征集意见</span>
            </label>
          </div>
          <div v-if="threadType === 'coin'" class="mb-2">
            <input v-model.number="coinPrice" class="form-control" type="number" min="1" max="9999" placeholder="所需金币数量" />
          </div>
          <div v-if="threadType === 'poll'" class="mb-2">
            <input v-for="(opt, i) in pollOptions" :key="i" v-model="pollOptions[i]" class="form-control mb-1" :placeholder="`选项 ${i + 1}`" />
            <button type="button" class="btn btn-sm btn-outline-secondary" v-if="pollOptions.length < 6" @click="pollOptions.push('')">加选项</button>
          </div>
          <input v-model="tagsInput" class="form-control mb-2" maxlength="40" placeholder="标签，逗号分隔，最多 3 个（可选）" />

          <textarea v-model="content" class="form-control mb-1" rows="8" placeholder="正文内容（可用 @昵称 提醒他人，支持 Markdown）"></textarea>
          <div class="text-muted mb-2" style="font-size:12px">{{ mdHint }}</div>

          <div class="image-upload mb-2">
            <div class="image-preview-list">
              <div v-for="(img, idx) in images" :key="idx" class="image-preview-item">
                <img :src="img" class="preview-thumb" alt="" @click="previewIdx = idx" />
                <button type="button" class="image-remove" @click="removeImage(idx)">&times;</button>
              </div>
              <label v-if="images.length < 8" class="image-add-btn">
                <input type="file" accept="image/*" multiple hidden @change="addImages" />
                <span>+</span>
                <small>{{ images.length }}/8</small>
              </label>
            </div>
            <div class="text-muted" style="font-size: 12px; margin-top: 6px">
              支持 JPG / PNG / GIF，最多 8 张
            </div>
          </div>

          <div v-if="error" class="text-danger mb-2">{{ error }}</div>
          <div v-if="!canPost" class="text-warning mb-2">Lv.2 以上才能发帖，继续加油升级！</div>
          <div v-else-if="auth.user?.isMuted" class="text-warning mb-2">
            账号已被禁言，暂时无法发帖
          </div>
          <div class="actions">
            <button class="btn btn-outline-secondary" :disabled="savingDraft || submitting" @click="saveDraftNow">
              {{ savingDraft ? '保存中...' : '保存草稿' }}
            </button>
            <button class="btn btn-forum" :disabled="submitting || !canPost || auth.user?.isMuted" @click="submit">{{ submitting ? '发布中...' : '发布' }}</button>
          </div>
        </template>
      </div>
    </div>

    <div v-if="previewIdx !== null" class="lightbox-overlay" @click.self="previewIdx = null">
      <button class="lightbox-close" @click="previewIdx = null">&times;</button>
      <button v-if="previewIdx > 0" class="lightbox-nav lightbox-prev" @click="previewIdx--">&lsaquo;</button>
      <img :src="images[previewIdx]" class="lightbox-img" alt="" />
      <button v-if="previewIdx < images.length - 1" class="lightbox-nav lightbox-next" @click="previewIdx++">&rsaquo;</button>
      <div class="lightbox-counter">{{ previewIdx + 1 }} / {{ images.length }}</div>
    </div>
  </AppLayout>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { canCreateThread, getNextLevel } from '../config/levels.js'
import { markdownHint } from '../utils/markdown.js'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const authModal = useAuthModalStore()
const mdHint = markdownHint()
const title = ref('')
const content = ref('')
const threadType = ref('public')
const coinPrice = ref(5)
const pollOptions = ref(['', ''])
const tagsInput = ref('')
const images = ref([])
const error = ref('')
const vipDenied = ref('')
const submitting = ref(false)
const previewIdx = ref(null)
const draftId = ref(null)
const draftHint = ref('')
const savingDraft = ref(false)
let autosaveTimer = null
let skipAutosave = true

const canPost = computed(() => auth.user && canCreateThread(auth.user.level))
const nextLv = computed(() => auth.user ? getNextLevel(auth.user.points) : null)
const forumId = computed(() => Number(route.params.id))

function applyDraft(d) {
  draftId.value = d.id
  title.value = d.title || ''
  content.value = d.content || ''
  threadType.value = d.type || 'public'
  coinPrice.value = d.coinPrice || 5
  tagsInput.value = (d.tags || []).join(', ')
  pollOptions.value = (d.pollOptions && d.pollOptions.length >= 2) ? [...d.pollOptions] : ['', '']
  images.value = d.images || []
  draftHint.value = '已恢复草稿'
}

async function checkForumAccess() {
  vipDenied.value = ''
  try {
    await api.get(`/forums/${forumId.value}`)
  } catch (e) {
    const msg = e.response?.data?.message || e.message || ''
    if (e.response?.status === 403 || String(msg).includes('会员')) {
      vipDenied.value = msg || '该版块需要会员权限才能发帖'
    }
  }
}

async function loadDraft() {
  await checkForumAccess()
  if (vipDenied.value || !auth.isLoggedIn) return
  try {
    if (route.query.draft) {
      const { data } = await api.get(`/me/drafts/${route.query.draft}`)
      applyDraft(data)
    } else {
      try {
        const { data } = await api.get(`/me/drafts/forum/${forumId.value}`)
        if (data?.id) applyDraft(data)
      } catch {
        // 404 = no draft
      }
    }
  } catch {
    draftHint.value = ''
  } finally {
    skipAutosave = false
  }
}

function draftPayload() {
  return {
    id: draftId.value || undefined,
    forumId: forumId.value,
    title: title.value,
    content: content.value,
    type: threadType.value,
    coinPrice: threadType.value === 'coin' ? Number(coinPrice.value) : 0,
    tags: tagsInput.value.split(/[,，]/).map(t => t.trim()).filter(Boolean).slice(0, 3),
    pollOptions: threadType.value === 'poll' ? pollOptions.value.map(o => o.trim()).filter(Boolean) : null,
    images: images.value,
  }
}

async function saveDraftNow() {
  if (!auth.isLoggedIn) return
  if (!title.value.trim() && !content.value.trim() && !images.value.length) {
    draftHint.value = '写点内容再保存'
    return
  }
  savingDraft.value = true
  try {
    const { data } = await api.post('/me/drafts', draftPayload())
    draftId.value = data.id
    draftHint.value = '草稿已保存'
  } catch (e) {
    draftHint.value = e.response?.data?.message || '保存失败'
  } finally {
    savingDraft.value = false
  }
}

function scheduleAutosave() {
  if (skipAutosave || !auth.isLoggedIn || submitting.value) return
  if (!title.value.trim() && !content.value.trim()) return
  clearTimeout(autosaveTimer)
  autosaveTimer = setTimeout(async () => {
    try {
      const { data } = await api.post('/me/drafts', draftPayload())
      draftId.value = data.id
      draftHint.value = '已自动保存'
    } catch {}
  }, 2500)
}

watch([title, content, threadType, coinPrice, tagsInput, pollOptions, images], scheduleAutosave, { deep: true })

function compressImage(file, maxDim, quality) {
  return new Promise((resolve) => {
    const reader = new FileReader()
    reader.onload = (e) => {
      const img = new Image()
      img.onload = () => {
        let { width, height } = img
        if (width > maxDim || height > maxDim) {
          const ratio = Math.min(maxDim / width, maxDim / height)
          width = Math.round(width * ratio)
          height = Math.round(height * ratio)
        }
        const canvas = document.createElement('canvas')
        canvas.width = width
        canvas.height = height
        const ctx = canvas.getContext('2d')
        ctx.drawImage(img, 0, 0, width, height)
        resolve(canvas.toDataURL('image/jpeg', quality))
      }
      img.src = e.target.result
    }
    reader.readAsDataURL(file)
  })
}

function addImages(e) {
  const files = Array.from(e.target.files || [])
  const remaining = 8 - images.value.length
  for (const file of files.slice(0, remaining)) {
    if (file.size > 10 * 1024 * 1024) {
      error.value = `"${file.name}" 超过 10MB 限制`
      continue
    }
    compressImage(file, 1920, 0.85).then((dataUrl) => {
      images.value.push(dataUrl)
    })
  }
  e.target.value = ''
}

function removeImage(idx) {
  images.value.splice(idx, 1)
}

async function submit() {
  error.value = ''
  if (!title.value.trim()) {
    error.value = '请填写标题'
    return
  }
  if (!content.value.trim() && !images.value.length) {
    error.value = '请填写内容或添加图片'
    return
  }
  if (threadType.value === 'coin' && (!coinPrice.value || coinPrice.value < 1)) {
    error.value = '请设置金币价格'
    return
  }
  if (threadType.value === 'poll') {
    const opts = pollOptions.value.map(o => o.trim()).filter(Boolean)
    if (opts.length < 2) {
      error.value = '投票至少需要 2 个选项'
      return
    }
  }
  submitting.value = true
  clearTimeout(autosaveTimer)
  try {
    const tags = tagsInput.value.split(/[,，]/).map(t => t.trim()).filter(Boolean).slice(0, 3)
    const { data } = await api.post('/threads', {
      forumId: forumId.value,
      title: title.value,
      content: content.value,
      images: images.value,
      type: threadType.value,
      coinPrice: threadType.value === 'coin' ? Number(coinPrice.value) : 0,
      tags,
      pollOptions: threadType.value === 'poll' ? pollOptions.value.map(o => o.trim()).filter(Boolean) : null,
    })
    await auth.fetchMe()
    router.push(`/thread/${data.id}`)
  } catch (e) {
    error.value = e.message
  } finally {
    submitting.value = false
  }
}

onMounted(loadDraft)
onUnmounted(() => clearTimeout(autosaveTimer))
</script>

<style scoped>
.type-selector {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 8px;
}
.type-option {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  padding: 10px 8px;
  border: 1px solid var(--line, rgba(20,32,51,0.12));
  border-radius: 10px;
  cursor: pointer;
  background: #fff;
  transition: border-color 0.15s, background 0.15s;
}
.type-option input { display: none; }
.type-option.active {
  border-color: #0d9488;
  background: rgba(13,148,136,0.06);
}
.type-icon { font-size: 20px; }
.type-label { font-size: 13px; font-weight: 700; color: #142033; }
.type-desc { font-size: 11px; color: #7a869c; }
.image-upload { user-select: none; }
.image-preview-list {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}
.image-preview-item {
  position: relative;
  width: 100px;
  height: 100px;
  border-radius: 10px;
  overflow: hidden;
  border: 1px solid var(--line, rgba(20,32,51,0.08));
}
.preview-thumb {
  width: 100%;
  height: 100%;
  object-fit: cover;
  cursor: pointer;
}
.image-remove {
  position: absolute;
  top: 4px;
  right: 4px;
  width: 22px;
  height: 22px;
  border: none;
  border-radius: 50%;
  background: rgba(0,0,0,0.55);
  color: #fff;
  line-height: 22px;
  cursor: pointer;
}
.image-add-btn {
  width: 100px;
  height: 100px;
  border: 1px dashed var(--line, rgba(20,32,51,0.2));
  border-radius: 10px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: #7a869c;
  gap: 2px;
}
.image-add-btn span { font-size: 28px; line-height: 1; }
.lightbox-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.85);
  z-index: 2000;
  display: flex;
  align-items: center;
  justify-content: center;
}
.lightbox-img {
  max-width: 90vw;
  max-height: 85vh;
  object-fit: contain;
}
.lightbox-close {
  position: absolute;
  top: 16px;
  right: 20px;
  background: none;
  border: none;
  color: #fff;
  font-size: 36px;
  cursor: pointer;
}
.lightbox-nav {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  background: rgba(255,255,255,0.15);
  border: none;
  color: #fff;
  font-size: 40px;
  width: 48px;
  height: 48px;
  border-radius: 50%;
  cursor: pointer;
}
.lightbox-prev { left: 20px; }
.lightbox-next { right: 20px; }
.lightbox-counter {
  position: absolute;
  bottom: 20px;
  color: #fff;
  font-size: 13px;
}
.actions { display: flex; gap: 10px; align-items: center; }
.draft-hint { color: #0d9488; }
</style>
