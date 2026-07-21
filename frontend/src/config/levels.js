/**
 * Level helpers. Rules are loaded from .NET GET /api/levels.
 * FALLBACK_LEVELS mirrors backend seed (6 tiers) for offline bootstrap.
 */
export const FALLBACK_LEVELS = [
  { level: 1, name: '见习会员', minPoints: 0, benefits: ['浏览帖子', '每日签到', '发表回复'] },
  { level: 2, name: '正式会员', minPoints: 50, benefits: ['浏览帖子', '每日签到', '发表新帖', '发表回复'] },
  { level: 3, name: '活跃会员', minPoints: 200, benefits: ['+ 以上所有', '金色标识起始'] },
  { level: 4, name: '资深会员', minPoints: 800, benefits: ['+ 以上所有', '更高热度权重'] },
  { level: 5, name: '金牌会员', minPoints: 2000, benefits: ['+ 以上所有', '金牌标识'] },
  { level: 6, name: '论坛元老', minPoints: 5000, benefits: ['+ 以上所有', '管理后台入口（管理员）'] },
]

/** @type {typeof FALLBACK_LEVELS} */
let cachedLevels = FALLBACK_LEVELS.map((l) => ({ ...l, benefits: [...l.benefits] }))

/** @deprecated use getLevels() — kept for admin LevelsView compat during load */
export const LEVELS = cachedLevels

export function getLevels() {
  return cachedLevels
}

export function setLevels(list) {
  if (!Array.isArray(list) || !list.length) return
  cachedLevels = list.map((l) => ({
    id: l.id,
    level: l.level,
    name: l.name,
    minPoints: l.minPoints,
    benefits: l.benefits || [],
  }))
  // keep LEVELS array identity updated for any direct imports
  LEVELS.splice(0, LEVELS.length, ...cachedLevels)
}

export function getLevel(points) {
  const levels = getLevels()
  let matched = levels[0]
  for (const l of levels) {
    if (points >= l.minPoints) matched = l
  }
  return matched
}

export function getNextLevel(points) {
  const levels = getLevels()
  const idx = levels.findIndex((l) => points < l.minPoints)
  if (idx === -1) return null
  return levels[idx]
}

export function getLevelProgress(points) {
  const current = getLevel(points)
  const next = getNextLevel(points)
  if (!next) return 100
  const range = next.minPoints - current.minPoints
  if (range <= 0) return 100
  const progress = ((points - current.minPoints) / range) * 100
  return Math.min(100, Math.round(progress))
}

export function canCreateThread(level) {
  return level >= 2
}

export function isAdminUser(user) {
  if (!user) return false
  if (user.isAdmin === true) return true
  const role = (user.role || '').toLowerCase()
  return role === 'admin' || role === 'super_admin'
}
