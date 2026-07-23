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
              <template v-if="thread.tipCoins > 0"> · 打赏 {{ thread.tipCoins }} 金币</template>
            </span>
            <button class="btn-outline-modern" :class="{ active: favorited }" @click="toggleFavorite">
              {{ favorited ? '已收藏' : '收藏' }}
            </button>
            <button
              v-if="canTipAuthor"
              class="btn-outline-modern tip-btn"
              @click="openTipPanel"
            >
              打赏楼主
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
        <button class="btn-outline-modern" @click="modAction(thread.isEssence ? 'unessence' : 'essence')">
          {{ thread.isEssence ? '取消精品' : '设为精品' }}
        </button>
        <button class="btn-outline-modern" @click="modMove">移动版块</button>
        <button class="btn-outline-modern danger" @click="modAction('hide')">隐藏本帖</button>
      </div>

      <div v-if="thread.repliesLocked" class="panel mb-3 p-3">
        <div class="text-warning" style="font-size:13px;font-weight:600">本帖已禁止回复</div>
      </div>

      <div v-if="showTipInput" class="panel mb-3 tip-panel">
        <div class="tip-panel-title">打赏楼主「{{ thread.author?.nickname }}」</div>
        <div v-if="!auth.isLoggedIn" class="text-muted" style="font-size:13px">
          请先 <a href="#" @click.prevent="authModal.openLogin()">登录</a> 后再打赏
        </div>
        <div v-else-if="auth.user?.isMuted" class="text-warning" style="font-size:13px">账号已被禁言，暂时无法打赏</div>
        <div v-else class="tip-panel-body">
          <div class="tip-presets">
            <button
              v-for="n in tipPresets"
              :key="n"
              type="button"
              class="tip-chip"
              :class="{ active: tipAmount === n }"
              @click="tipAmount = n"
            >{{ n }} 金币</button>
          </div>
          <div class="tip-custom">
            <input v-model.number="tipAmount" type="number" min="1" max="9999" class="form-control tip-input" />
            <button class="btn btn-forum btn-sm" :disabled="tipping || !tipAmount" @click="tipThread">
              {{ tipping ? '处理中...' : '确认打赏' }}
            </button>
            <span class="tip-balance">余额 {{ auth.user?.coins ?? 0 }} 金币</span>
          </div>
          <div v-if="tipError" class="text-danger tip-msg">{{ tipError }}</div>
          <div v-if="tipSuccess" class="text-success tip-msg">{{ tipSuccess }}</div>
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

      <div v-if="filterAuthorId" class="author-filter-bar mb-3">
        正在只看该作者的回复
        <button type="button" class="btn-link" @click="filterAuthorId = null">显示全部</button>
      </div>

      <div
        v-for="post in rootPosts"
        :key="post.id"
        :id="`floor-${post.floor}`"
        class="post-card"
        :class="{ 'post-deleted': post.deleted }"
      >
        <aside class="post-side">
          <router-link :to="`/user/${post.author.id}`" class="post-side-avatar">
            <span class="avatar-frame" :class="'frame-' + (post.author.avatarFrame || '')">
              <img :src="post.author.avatar || defaultAvatar(post.author.nickname)" class="post-avatar" alt="" />
            </span>
          </router-link>
          <ul class="post-side-stats">
            <li><span>等级</span><b>Lv.{{ post.author.level }}</b></li>
            <li><span>帖子</span><b>{{ post.author.postCount ?? 0 }}</b></li>
            <li><span>精华</span><b>{{ post.author.essenceCount ?? 0 }}</b></li>
            <li><span>积分</span><b>{{ post.author.points }}</b></li>
            <li v-if="post.author.createdAt"><span>注册</span><b>{{ formatDateOnly(post.author.createdAt) }}</b></li>
          </ul>
        </aside>

        <div class="post-main">
          <div class="post-head">
            <div class="post-head-left">
              <router-link class="post-nick" :to="`/user/${post.author.id}`">{{ post.author.nickname }}</router-link>
              <span
                class="level-badge"
                :class="{ 'lv-high': post.author.level >= 5 }"
              >Lv.{{ post.author.level }} {{ post.author.levelName }}</span>
              <span class="post-time">发表于 {{ formatTime(post.createdAt) }}</span>
              <span v-if="post.editedAt" class="post-edited">（已编辑）</span>
            </div>
            <div class="post-head-right">
              <button type="button" class="post-head-link" @click="filterAuthorId = post.author.id">只看该作者</button>
              <span class="post-floor-tag">#{{ post.floor }}楼</span>
            </div>
          </div>

          <div class="post-content">
            <template v-if="editingPost?.id === post.id && !(editingThread && post.floor === 1)">
              <MarkdownEditor v-model="editContent" class="mb-2" :compact="true" :rows="4" :hint="mdHint" />
              <div class="d-flex gap-2 mb-2">
                <button class="btn btn-forum btn-sm" :disabled="savingEdit" @click="saveEdit(post)">{{ savingEdit ? '保存中...' : '保存' }}</button>
                <button class="btn btn-outline-modern btn-sm" @click="cancelEdit">取消</button>
              </div>
            </template>
            <template v-else-if="editingThread && post.floor === 1">
              <MarkdownEditor v-model="editThreadContent" class="mb-2" :rows="6" :hint="mdHint" />
              <div v-if="editThreadError" class="text-danger mb-2" style="font-size:12px">{{ editThreadError }}</div>
              <div class="d-flex gap-2 mb-2">
                <button class="btn btn-forum btn-sm" :disabled="savingThread" @click="saveThread">{{ savingThread ? '保存中...' : '保存主题' }}</button>
                <button class="btn btn-outline-modern btn-sm" @click="cancelEditThread">取消</button>
              </div>
            </template>
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
                  引用
                  <a href="#" @click.prevent="scrollToFloor(post.replyToFloor)">#{{ post.replyToFloor }}</a>
                  {{ post.replyToNickname }}：{{ post.replyToContent }}
                </div>
                <MarkdownBody v-if="postDisplayContent(post)" :content="postDisplayContent(post)" />
              </div>
            </template>

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

          <div class="post-foot">
            <button
              v-if="auth.user?.id === post.author.id && !post.deleted && post.floor > 1"
              type="button"
              class="post-foot-btn"
              @click="startEdit(post)"
            >编辑</button>
            <button
              v-if="thread.canEdit && !post.deleted && post.floor === 1 && !editingThread"
              type="button"
              class="post-foot-btn"
              @click="startEditThread"
            >编辑</button>
            <button
              v-if="auth.user?.id === post.author.id && !post.deleted && post.floor > 1"
              type="button"
              class="post-foot-btn"
              @click="deletePost(post)"
            >删除</button>
            <button
              v-if="auth.isLoggedIn && !post.deleted && !thread.repliesLocked"
              type="button"
              class="post-foot-btn"
              @click="toggleInlineReply(post)"
            >回复{{ childrenOf(post.id).length ? `(${childrenOf(post.id).length})` : '' }}</button>
            <button
              v-if="auth.isLoggedIn && !post.deleted"
              type="button"
              class="post-foot-btn"
              @click="reportPost(post)"
            >举报</button>
            <button
              v-if="canTipAuthor && post.floor === 1 && !post.deleted"
              type="button"
              class="post-foot-btn tip-foot"
              @click="openTipPanel"
            >打赏楼主</button>
          </div>

          <!-- Nested replies -->
          <div v-if="childrenOf(post.id).length || inlineReplyParentId === post.id" class="nested-box">
            <div
              v-for="child in visibleChildren(post.id)"
              :key="child.id"
              :id="`floor-${child.floor}`"
              class="nested-item"
              :class="{ 'nested-deleted': child.deleted }"
            >
              <img
                class="nested-avatar"
                :src="child.author.avatar || defaultAvatar(child.author.nickname)"
                alt=""
              />
              <div class="nested-body">
                <div class="nested-meta">
                  <router-link class="nested-nick" :to="`/user/${child.author.id}`">{{ child.author.nickname }}</router-link>
                  <span class="nested-reply-to">回复</span>
                  <router-link
                    v-if="replyTargetAuthorId(child, post)"
                    class="nested-nick"
                    :to="`/user/${replyTargetAuthorId(child, post)}`"
                  >{{ replyTargetName(child, post) }}</router-link>
                  <span v-else class="nested-nick-plain">{{ replyTargetName(child, post) }}</span>
                  <span class="nested-time">发表于 {{ formatTime(child.createdAt) }}</span>
                </div>
                <div v-if="child.deleted" class="nested-text text-muted" style="font-style:italic">该回复已被删除</div>
                <div v-else class="nested-text">
                  <MarkdownBody v-if="postDisplayContent(child)" :content="postDisplayContent(child)" />
                </div>
                <div v-if="child.images?.length && !child.deleted" class="img-grid nested-imgs">
                  <div
                    v-for="(img, idx) in child.images"
                    :key="idx"
                    class="img-grid-item"
                    @click="openLightbox(child.images, idx)"
                  >
                    <img :src="img" alt="" loading="lazy" />
                  </div>
                </div>
                <div class="nested-actions">
                  <button
                    v-if="auth.isLoggedIn && !child.deleted && !thread.repliesLocked"
                    type="button"
                    class="post-foot-btn"
                    @click="toggleInlineReply(child, post.id)"
                  >回复</button>
                  <button
                    v-if="auth.isLoggedIn && !child.deleted"
                    type="button"
                    class="post-foot-btn"
                    @click="reportPost(child)"
                  >举报</button>
                </div>
              </div>
            </div>

            <button
              v-if="hiddenChildCount(post.id) > 0 && !isNestedExpanded(post.id)"
              type="button"
              class="nested-more"
              @click="expandNested(post.id)"
            >查看更多（{{ hiddenChildCount(post.id) }} 条）</button>
            <button
              v-else-if="isNestedExpanded(post.id) && childrenOf(post.id).length > NESTED_PREVIEW"
              type="button"
              class="nested-more"
              @click="collapseNested(post.id)"
            >收起</button>

            <div v-if="inlineReplyParentId === post.id" class="inline-reply">
              <div v-if="!auth.isLoggedIn" class="text-muted" style="font-size:13px">
                请先 <a href="#" @click.prevent="authModal.openLogin()">登录</a> 后回复
              </div>
              <div v-else-if="auth.user?.isMuted" class="text-warning" style="font-size:13px">账号已被禁言，暂时无法回帖</div>
              <template v-else>
                <div v-if="inlineReplyTo" class="quote-pending mb-2">
                  回复 #{{ inlineReplyTo.floor }} {{ inlineReplyTo.author.nickname }}
                  <button type="button" class="btn-link" @click="setInlineTargetToRoot(post)">改为回复本楼</button>
                </div>
                <MarkdownEditor
                  v-model="inlineReply"
                  class="mb-2"
                  :compact="true"
                  :rows="3"
                  :hint="mdHint"
                  placeholder="说点什么…（可 Ctrl+V 粘贴图片）"
                  @paste-images="onInlinePasteImages"
                />
                <div class="reply-images mb-2" tabindex="0" @paste="onInlineImageAreaPaste">
                  <div class="reply-img-list">
                    <div v-for="(img, idx) in inlineImages" :key="idx" class="reply-img-item">
                      <img :src="img" class="reply-img-thumb" alt="" />
                      <button type="button" class="reply-img-remove" @click="inlineImages.splice(idx, 1)">&times;</button>
                    </div>
                    <label v-if="inlineImages.length < 8" class="reply-img-add">
                      <input type="file" accept="image/*" multiple hidden @change="addInlineImages" />
                      <span>+</span>
                    </label>
                  </div>
                </div>
                <div v-if="inlineError" class="text-danger mb-2" style="font-size:12px">{{ inlineError }}</div>
                <div class="d-flex gap-2 align-items-center">
                  <button class="btn btn-forum btn-sm" :disabled="inlineSubmitting" @click="submitInlineReply(post)">
                    {{ inlineSubmitting ? '提交中...' : '发表' }}
                  </button>
                  <button type="button" class="btn btn-outline-modern btn-sm" @click="closeInlineReply">取消</button>
                </div>
              </template>
            </div>
            <div v-else-if="auth.isLoggedIn && !thread.repliesLocked && !post.deleted" class="nested-say">
              <button type="button" class="btn-say" @click="toggleInlineReply(post)">我也来说说</button>
            </div>
          </div>
        </div>
      </div>

      <div v-if="postTotalPages > 1" class="p-3 d-flex gap-2 justify-content-center align-items-center">
        <button class="btn btn-sm btn-outline-secondary" :disabled="postPage <= 1 || loadingPosts" @click="loadPosts(postPage - 1)">上一页</button>
        <span class="text-muted" style="font-size:13px">{{ postPage }} / {{ postTotalPages }}（{{ postTotal }} 回复）</span>
        <button class="btn btn-sm btn-outline-secondary" :disabled="postPage >= postTotalPages || loadingPosts" @click="loadPosts(postPage + 1)">下一页</button>
      </div>

      <div id="thread-reply-box" v-if="!thread.repliesLocked" class="panel">
        <div class="panel-header"><span class="accent"></span>发表回复</div>
        <div class="p-3">
          <div v-if="!auth.isLoggedIn" class="text-muted">
            请先 <a href="#" @click.prevent="authModal.openLogin()">登录</a> 后回帖（回帖 +2 积分、+2 金币）
          </div>
          <div v-else-if="auth.user?.isMuted" class="text-warning">
            账号已被禁言{{ auth.user.mutedUntil ? '，至 ' + formatTime(auth.user.mutedUntil) : '' }}，暂时无法回帖
          </div>
          <template v-else>
            <div v-if="replyTo" class="quote-pending mb-2">
              引用 #{{ replyTo.floor }} {{ replyTo.author.nickname }}
              <button type="button" class="btn-link" @click="replyTo = null">取消</button>
            </div>
            <MarkdownEditor
              ref="replyTextarea"
              v-model="reply"
              class="mb-2"
              :compact="true"
              :rows="4"
              :hint="mdHint + ' · 纯文字至少 5 字'"
              placeholder="至少 5 个字… 可用 @昵称 提醒，支持 Markdown；可 Ctrl+V 粘贴图片"
              @paste-images="onReplyPasteImages"
            />

            <div class="reply-images mb-2" tabindex="0" @paste="onReplyImageAreaPaste">
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
              <div class="text-muted" style="font-size:12px;margin-top:6px">支持 JPG / PNG / GIF，最多 8 张 · 可 Ctrl+V 粘贴截图</div>
            </div>

            <div v-if="error" class="text-danger mb-2">{{ error }}</div>
            <button class="btn btn-forum" :disabled="submitting" @click="submitReply">{{ submitting ? '提交中...' : '提交回复' }}</button>
            <span class="text-muted ms-2" style="font-size:12px">回帖 +2 积分、+2 金币 · 间隔 {{ replyCooldown }} 秒 · 日限 20 次</span>
          </template>
        </div>
      </div>

      <Teleport to="body">
        <div class="thread-float-nav" aria-label="快速跳转">
          <button type="button" title="回到顶部" @click="scrollPageTop">↑</button>
          <button type="button" title="去底部回帖" @click="scrollPageBottom">↓</button>
        </div>
      </Teleport>
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
import { onMounted, ref, watch, computed, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import MarkdownBody from '../components/MarkdownBody.vue'
import MarkdownEditor from '../components/MarkdownEditor.vue'
import { useAuthStore } from '../stores/auth'
import { useAuthModalStore } from '../stores/authModal'
import { useToastStore } from '../stores/toast'
import { markdownHint } from '../utils/markdown.js'
import { compressImage, filesFromClipboard } from '../utils/image.js'
import { uploadImages } from '../utils/upload.js'
import { formatTime, formatDateOnly } from '../utils/time.js'
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
const filterAuthorId = ref(null)

const inlineReplyParentId = ref(null)
const inlineReplyTo = ref(null)
const inlineReply = ref('')
const inlineImages = ref([])
const inlineError = ref('')
const inlineSubmitting = ref(false)

const visiblePosts = computed(() => {
  if (!filterAuthorId.value) return posts.value
  return posts.value.filter((p) => p.author?.id === filterAuthorId.value)
})

const postIdSet = computed(() => new Set(visiblePosts.value.map((p) => p.id)))

/** 主楼：无引用，或引用目标不在本页列表中 */
const rootPosts = computed(() =>
  visiblePosts.value.filter((p) => !p.replyToPostId || !postIdSet.value.has(p.replyToPostId))
)

function childrenOf(rootId) {
  const byParent = new Map()
  for (const p of visiblePosts.value) {
    if (!p.replyToPostId) continue
    if (!byParent.has(p.replyToPostId)) byParent.set(p.replyToPostId, [])
    byParent.get(p.replyToPostId).push(p)
  }
  const out = []
  const queue = [...(byParent.get(rootId) || [])]
  const seen = new Set()
  while (queue.length) {
    const cur = queue.shift()
    if (seen.has(cur.id)) continue
    seen.add(cur.id)
    out.push(cur)
    for (const next of byParent.get(cur.id) || []) queue.push(next)
  }
  out.sort((a, b) => a.floor - b.floor)
  return out
}

const NESTED_PREVIEW = 5
const nestedExpanded = ref({})

function isNestedExpanded(rootId) {
  return !!nestedExpanded.value[rootId]
}

function visibleChildren(rootId) {
  const all = childrenOf(rootId)
  if (isNestedExpanded(rootId) || all.length <= NESTED_PREVIEW) return all
  return all.slice(0, NESTED_PREVIEW)
}

function hiddenChildCount(rootId) {
  return Math.max(0, childrenOf(rootId).length - NESTED_PREVIEW)
}

function expandNested(rootId) {
  nestedExpanded.value = { ...nestedExpanded.value, [rootId]: true }
}

function collapseNested(rootId) {
  nestedExpanded.value = { ...nestedExpanded.value, [rootId]: false }
}

/** 楼中楼「回复谁」：始终展示目标昵称 */
function replyTargetName(child, rootPost) {
  if (child.replyToNickname) return child.replyToNickname
  return rootPost?.author?.nickname || '楼主'
}

function replyTargetAuthorId(child, rootPost) {
  if (child.replyToPostId) {
    const target = posts.value.find((p) => p.id === child.replyToPostId)
    if (target?.author?.id) return target.author.id
  }
  return rootPost?.author?.id || null
}

function setInlineTargetToRoot(rootPost) {
  inlineReplyTo.value = rootPost
}

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
const tipAmount = ref(5)
const tipping = ref(false)
const tipError = ref('')
const tipSuccess = ref('')
const tipPresets = [1, 5, 10, 50, 100]

const canTipAuthor = computed(() => {
  if (!thread.value) return false
  if (!auth.isLoggedIn) return true // 显示入口，面板内引导登录
  return auth.user?.id !== thread.value.author?.id
})

function openTipPanel() {
  tipError.value = ''
  tipSuccess.value = ''
  showTipInput.value = true
  nextTick(() => {
    document.querySelector('.tip-panel')?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
  })
}
const modBusy = ref(false)

function openLightbox(images, idx) {
  lightboxImages.value = images
  lightboxIdx.value = idx
  lightboxOpen.value = true
}

/** 粘贴图时部分客户端会留下「[图片]」占位文案，有附图时不展示 */
function postDisplayContent(post) {
  const c = (post?.content || '').trim()
  if (post?.images?.length && /^\[图片\](\s*\[图片\])*$/.test(c)) return ''
  return post?.content || ''
}

function normalizeReplyContent(text, hasImages) {
  const t = (text || '').trim()
  if (hasImages && /^\[图片\](\s*\[图片\])*$/.test(t)) return ''
  return text
}

function addReplyImages(e) {
  appendToImageList(replyImages, Array.from(e.target.files || []), (msg) => { error.value = msg })
  e.target.value = ''
}

function addInlineImages(e) {
  appendToImageList(inlineImages, Array.from(e.target.files || []), (msg) => { inlineError.value = msg })
  e.target.value = ''
}

function onReplyPasteImages(files) {
  appendToImageList(replyImages, files, (msg) => { error.value = msg })
}

function onInlinePasteImages(files) {
  appendToImageList(inlineImages, files, (msg) => { inlineError.value = msg })
}

function onReplyImageAreaPaste(e) {
  const files = filesFromClipboard(e.clipboardData)
  if (!files.length) return
  e.preventDefault()
  appendToImageList(replyImages, files, (msg) => { error.value = msg })
}

function onInlineImageAreaPaste(e) {
  const files = filesFromClipboard(e.clipboardData)
  if (!files.length) return
  e.preventDefault()
  appendToImageList(inlineImages, files, (msg) => { inlineError.value = msg })
}

function appendToImageList(listRef, files, setError) {
  if (!files?.length) return
  const remaining = 8 - listRef.value.length
  if (remaining <= 0) {
    setError?.('最多上传 8 张图片')
    return
  }
  for (const file of files.slice(0, remaining)) {
    if (file.size > 10 * 1024 * 1024) {
      setError?.(`"${file.name || '图片'}" 超过 10MB 限制`)
      continue
    }
    compressImage(file, 1920, 0.85).then((dataUrl) => {
      if (listRef.value.length >= 8) return
      listRef.value.push(dataUrl)
    })
  }
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
  filterAuthorId.value = null
  nestedExpanded.value = {}
  closeInlineReply()
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
    hide: '隐藏', essence: '设为精品', unessence: '取消精品',
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

async function modMove() {
  if (modBusy.value) return
  try {
    const { data } = await api.get('/categories')
    const list = []
    for (const c of data || []) {
      for (const f of c.forums || []) list.push({ id: f.id, name: `${c.name}/${f.name}` })
    }
    const tip = list.map(f => `${f.id} ${f.name}`).join('\n')
    const input = prompt(`输入目标版块 ID：\n${tip}`, String(thread.value.forumId))
    if (input === null) return
    const forumId = Number(input)
    if (!forumId) { toast.error('无效版块'); return }
    modBusy.value = true
    await api.post(`/threads/${route.params.id}/mod/move`, { forumId })
    toast.success('已移动')
    await load()
  } catch (e) {
    toast.error(e.response?.data?.message || e.message || '移动失败')
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
    post.deleted = true
    post.content = ''
    post.images = []
    thread.value.replyCount = Math.max(0, thread.value.replyCount - 1)
  } catch (e) {
    error.value = e.message
  }
}

function quotePost(post) {
  toggleInlineReply(post)
}

function toggleInlineReply(post, nestUnderRootId = null) {
  if (!auth.isLoggedIn) {
    authModal.openLogin()
    return
  }
  const rootId = nestUnderRootId || post.id
  // 若点的是子回复，挂在主楼下打开，并标记回复对象
  const parentRoot = nestUnderRootId
    ? posts.value.find((p) => p.id === nestUnderRootId)
    : post
  if (inlineReplyParentId.value === (parentRoot?.id || rootId) && inlineReplyTo.value?.id === post.id) {
    closeInlineReply()
    return
  }
  inlineReplyParentId.value = parentRoot?.id || post.id
  inlineReplyTo.value = post
  inlineReply.value = ''
  inlineImages.value = []
  inlineError.value = ''
}

function closeInlineReply() {
  inlineReplyParentId.value = null
  inlineReplyTo.value = null
  inlineReply.value = ''
  inlineImages.value = []
  inlineError.value = ''
}

async function submitInlineReply(rootPost) {
  inlineError.value = ''
  const text = inlineReply.value.trim()
  if (!text && !inlineImages.value.length) {
    inlineError.value = '请输入内容或添加图片'
    return
  }
  if (!inlineImages.value.length && text.length < minReplyLength) {
    inlineError.value = `回帖内容至少 ${minReplyLength} 个字`
    return
  }
  inlineSubmitting.value = true
  let images = []
  if (inlineImages.value.length > 0) {
    try {
      images = await uploadImages([...inlineImages.value])
    } catch (e) {
      inlineError.value = '图片上传失败：' + e.message
      inlineSubmitting.value = false
      return
    }
  }
  try {
    const target = inlineReplyTo.value || rootPost
    const { data } = await api.post(`/threads/${route.params.id}/replies`, {
      content: normalizeReplyContent(inlineReply.value, images.length > 0),
      images,
      replyToPostId: target.id,
    })
    posts.value.push(data)
    postTotal.value += 1
    thread.value.replyCount += 1
    closeInlineReply()
    // 保持楼中楼展开：再打开空表单可选；这里关闭即可
    await auth.fetchMe()
  } catch (e) {
    inlineError.value = e.message
  } finally {
    inlineSubmitting.value = false
  }
}

function scrollToFloor(floor) {
  const el = document.getElementById(`floor-${floor}`)
  if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

function scrollPageTop() {
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

function scrollPageBottom() {
  const box = document.getElementById('thread-reply-box')
  if (box) box.scrollIntoView({ behavior: 'smooth', block: 'start' })
  else window.scrollTo({ top: document.documentElement.scrollHeight, behavior: 'smooth' })
}

async function reportPost(post) {
  const reason = prompt('请填写举报原因')
  if (!reason || reason.trim().length < 2) return
  try {
    await api.post('/reports', { targetType: 'post', targetId: post.id, reason: reason.trim() })
    toast.success('举报已提交')
  } catch (e) {
    toast.error(e.message)
  }
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
      content: normalizeReplyContent(reply.value, images.length > 0),
      images,
      replyToPostId: replyTo.value?.id || null,
    })
    posts.value.push(data)
    postTotal.value += 1
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
    authModal.openLogin()
    return
  }
  const amount = Number(tipAmount.value)
  if (!Number.isFinite(amount) || amount < 1) {
    tipError.value = '请输入有效的金币数量'
    return
  }
  tipping.value = true
  tipError.value = ''
  tipSuccess.value = ''
  try {
    const { data } = await api.post(`/threads/${route.params.id}/tip`, { amount })
    tipSuccess.value = data.message
    if (thread.value) {
      thread.value.tipCoins = (thread.value.tipCoins || 0) + amount
      thread.value.tipCount = (thread.value.tipCount || 0) + 1
    }
    await auth.fetchMe()
    toast.success(data.message)
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

onMounted(async () => {
  await load()
  const startPage = Number(route.query.p) || 1
  if (startPage > 1 && thread.value && !thread.value.restricted) {
    await loadPosts(startPage)
  }
  const floor = Number(route.query.floor) || Number(String(route.hash || '').replace(/^#floor-/, ''))
  if (floor > 0) {
    setTimeout(() => scrollToFloor(floor), 80)
  }
})
watch(() => route.params.id, load)
watch(() => route.query.p, async (p) => {
  const n = Number(p) || 1
  if (thread.value && !thread.value.restricted && n !== postPage.value) {
    await loadPosts(n)
  }
})
</script>

<style scoped>
/* Avatar (nested / fallback) */
.post-avatar {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  object-fit: cover;
  display: block;
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
.tip-btn {
  color: #c2410c;
  border-color: #fdba74;
}
.tip-btn:hover {
  background: #fff7ed;
}
.tip-panel {
  padding: 16px 18px;
  border: 1px solid #fed7aa;
  background: linear-gradient(180deg, #fffbeb 0%, #fff 70%);
}
.tip-panel-title {
  font-size: 15px;
  font-weight: 700;
  color: #9a3412;
  margin-bottom: 12px;
}
.tip-presets {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 12px;
}
.tip-chip {
  border: 1px solid #fdba74;
  background: #fff;
  color: #c2410c;
  border-radius: 999px;
  padding: 6px 12px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
}
.tip-chip.active,
.tip-chip:hover {
  background: #c2410c;
  border-color: #c2410c;
  color: #fff;
}
.tip-custom {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}
.tip-input {
  width: 110px;
  max-width: 100%;
}
.tip-balance {
  font-size: 12px;
  color: #78716c;
}
.tip-msg {
  margin-top: 10px;
  font-size: 13px;
}
.post-foot-btn.tip-foot {
  color: #c2410c;
}
</style>
