<template>
  <AppLayout>
    <div class="breadcrumb-bar">首页 / 论坛</div>
    <BannerCarousel />
    <EssencePanel />
    <SubscriptionUnreadPanel />
    <FeedPanel />
    <HotThreadsPanel />
    <div v-if="loading" class="p-4 text-muted">加载版块中...</div>
    <div v-else-if="error" class="p-4 text-danger">{{ error }}</div>
    <ForumSection
      v-for="cat in categories"
      :key="cat.id"
      :category="cat"
    />
  </AppLayout>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../api/http'
import AppLayout from '../components/AppLayout.vue'
import BannerCarousel from '../components/BannerCarousel.vue'
import EssencePanel from '../components/EssencePanel.vue'
import SubscriptionUnreadPanel from '../components/SubscriptionUnreadPanel.vue'
import FeedPanel from '../components/FeedPanel.vue'
import HotThreadsPanel from '../components/HotThreadsPanel.vue'
import ForumSection from '../components/ForumSection.vue'

const categories = ref([])
const loading = ref(true)
const error = ref('')

onMounted(async () => {
  try {
    const { data } = await api.get('/categories')
    categories.value = data
  } catch (e) {
    error.value = e.message || '加载失败，请确认后端已启动'
  } finally {
    loading.value = false
  }
})
</script>
