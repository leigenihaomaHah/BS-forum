<template>
  <div v-if="items.length" class="banner-carousel mb-3">
    <div class="banner-track" @mouseenter="pause" @mouseleave="resume">
      <a
        v-for="(b, i) in items"
        :key="b.id"
        class="banner-slide"
        :class="{ active: i === index }"
        :href="b.linkUrl || undefined"
        :target="isExternal(b.linkUrl) ? '_blank' : undefined"
        :rel="isExternal(b.linkUrl) ? 'noopener' : undefined"
        @click="onClick($event, b)"
      >
        <img :src="b.imageUrl" :alt="b.title" />
        <div v-if="b.title" class="banner-caption">{{ b.title }}</div>
      </a>
      <button v-if="items.length > 1" type="button" class="banner-nav prev" @click.stop="prev">‹</button>
      <button v-if="items.length > 1" type="button" class="banner-nav next" @click.stop="next">›</button>
    </div>
    <div v-if="items.length > 1" class="banner-dots">
      <button
        v-for="(b, i) in items"
        :key="'d'+b.id"
        type="button"
        class="dot"
        :class="{ active: i === index }"
        @click="go(i)"
      />
    </div>
  </div>
</template>

<script setup>
import { onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '../api/http'

const router = useRouter()
const items = ref([])
const index = ref(0)
let timer = null

function isExternal(url) {
  return !!url && /^https?:\/\//i.test(url)
}

function go(i) {
  index.value = i
}

function next() {
  if (!items.value.length) return
  index.value = (index.value + 1) % items.value.length
}

function prev() {
  if (!items.value.length) return
  index.value = (index.value - 1 + items.value.length) % items.value.length
}

function onClick(e, b) {
  const url = b.linkUrl
  if (!url) {
    e.preventDefault()
    return
  }
  if (url.startsWith('/') && !isExternal(url)) {
    e.preventDefault()
    router.push(url)
  }
}

function pause() {
  clearInterval(timer)
  timer = null
}

function resume() {
  pause()
  if (items.value.length > 1) {
    timer = setInterval(next, 4500)
  }
}

onMounted(async () => {
  try {
    const { data } = await api.get('/banners')
    items.value = data || []
    resume()
  } catch {
    items.value = []
  }
})

onUnmounted(pause)
</script>

<style scoped>
.banner-carousel {
  border-radius: 14px;
  overflow: hidden;
  border: 1px solid rgba(20, 32, 51, 0.08);
  background: #fff;
  box-shadow: 0 8px 24px rgba(20, 32, 51, 0.05);
}
.banner-track {
  position: relative;
  width: 100%;
  height: clamp(160px, 20vw, 220px);
  background: #e8edf3;
}
.banner-slide {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.45s ease;
  display: block;
  text-decoration: none;
  color: inherit;
}
.banner-slide.active {
  opacity: 1;
  pointer-events: auto;
  z-index: 1;
}
.banner-slide img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center;
  display: block;
}
.banner-caption {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  padding: 10px 16px;
  font-size: 13px;
  font-weight: 600;
  color: #fff;
  background: linear-gradient(transparent, rgba(15, 23, 42, 0.55));
}
.banner-nav {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  z-index: 2;
  width: 32px;
  height: 32px;
  border: none;
  border-radius: 50%;
  background: rgba(15, 23, 42, 0.35);
  color: #fff;
  font-size: 20px;
  line-height: 1;
  cursor: pointer;
  opacity: 0;
  transition: opacity 0.2s, background 0.2s;
}
.banner-track:hover .banner-nav { opacity: 1; }
.banner-nav:hover { background: rgba(15, 23, 42, 0.55); }
.banner-nav.prev { left: 10px; }
.banner-nav.next { right: 10px; }
.banner-dots {
  display: flex;
  justify-content: center;
  gap: 6px;
  padding: 8px 0 10px;
  background: #fff;
}
.dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  border: none;
  padding: 0;
  background: #cbd5e1;
  cursor: pointer;
}
.dot.active { background: #0d9488; width: 16px; border-radius: 4px; }
</style>
