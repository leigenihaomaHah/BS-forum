<template>
  <RouterView />
  <LoginModal />
  <RegisterModal />
</template>

<script setup>
import LoginModal from './components/LoginModal.vue'
import RegisterModal from './components/RegisterModal.vue'
</script>
