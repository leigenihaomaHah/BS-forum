<template>
  <div class="admin-page">
    <h2 class="page-title">等级配置</h2>

    <div class="admin-panel">
      <table class="admin-table">
        <thead>
          <tr>
            <th>等级</th>
            <th>名称</th>
            <th>所需积分</th>
            <th>权益</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="l in levels" :key="l.level">
            <td><span class="level-badge" :class="{ 'lv-high': l.level >= 5 }">Lv.{{ l.level }}</span></td>
            <td class="fw-medium">{{ l.name }}</td>
            <td>{{ l.minPoints.toLocaleString() }} 分</td>
            <td>
              <span v-for="b in l.benefits" :key="b" class="benefit-tag">{{ b }}</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="admin-panel mt-3" style="background:#f8fafc">
      <div class="p-3 text-muted" style="font-size:13px">
        等级规则由后端 <code>LevelRules</code> 表提供（GET /api/levels），前端只读展示。
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import api from '../../api/http'
import { getLevels, setLevels } from '../../config/levels.js'

const levels = ref(getLevels())

onMounted(async () => {
  try {
    const { data } = await api.get('/levels')
    setLevels(data)
    levels.value = getLevels()
  } catch {
    levels.value = getLevels()
  }
})
</script>

<style scoped>
.benefit-tag {
  display: inline-block;
  padding: 2px 8px;
  background: #f0f4f8;
  border-radius: 6px;
  font-size: 11px;
  color: #3d4a63;
  margin: 1px 2px;
  white-space: nowrap;
}
</style>
