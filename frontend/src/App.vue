<template>
  <RouterView />
  <LoginModal />
  <RegisterModal />
  <AppDialog />
  <AppToast />
</template>

<script setup>
import LoginModal from './components/LoginModal.vue'
import RegisterModal from './components/RegisterModal.vue'
import AppDialog from './components/AppDialog.vue'
import AppToast from './components/AppToast.vue'
</script>
