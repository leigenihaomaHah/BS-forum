<template>
  <div v-if="totalPages > 1" class="d-flex gap-2 justify-content-center align-items-center p-3">
    <button class="btn btn-sm btn-outline-secondary" :disabled="modelValue <= 1" @click="emit('update:modelValue', modelValue - 1)">
      上一页
    </button>
    <span class="text-muted" style="font-size:13px">{{ modelValue }} / {{ totalPages }}</span>
    <button class="btn btn-sm btn-outline-secondary" :disabled="modelValue >= totalPages" @click="emit('update:modelValue', modelValue + 1)">
      下一页
    </button>
  </div>
</template>

<script setup>
defineProps({
  modelValue: { type: Number, required: true },
  totalPages: { type: Number, required: true },
})
const emit = defineEmits(['update:modelValue'])
</script>
