/** 解析接口时间：后端存 UTC，旧响应常缺 Z，按 UTC 解释后再转本地显示。 */
export function parseApiTime(iso) {
  if (!iso) return null
  if (iso instanceof Date) return Number.isNaN(iso.getTime()) ? null : iso
  const s = String(iso).trim()
  if (!s) return null
  const hasTz = /[zZ]$|[+-]\d{2}:?\d{2}$/.test(s)
  const normalized = s.includes('T') ? s : s.replace(' ', 'T')
  const d = new Date(hasTz ? normalized : `${normalized}Z`)
  return Number.isNaN(d.getTime()) ? null : d
}

export function timeAgo(iso) {
  const d = parseApiTime(iso)
  if (!d) return ''
  const diff = Date.now() - d.getTime()
  const secs = Math.floor(diff / 1000)
  if (secs < 5) return '刚刚'
  if (secs < 60) return `${secs}秒前`
  const mins = Math.floor(secs / 60)
  if (mins < 60) return `${mins}分钟前`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `${hours}小时前`
  const days = Math.floor(hours / 24)
  if (days < 30) return `${days}天前`
  return formatTime(iso, false)
}

/** 列表「最后发表」：一天内相对时间，否则月-日 时:分 */
export function formatListTime(iso) {
  const d = parseApiTime(iso)
  if (!d) return ''
  const diff = Date.now() - d.getTime()
  if (diff < 24 * 60 * 60 * 1000) return timeAgo(iso)
  return formatTime(iso, false)
}

export function formatCount(n) {
  const v = Number(n) || 0
  if (v >= 100000000) return `${(v / 100000000).toFixed(1).replace(/\.0$/, '')}亿`
  if (v >= 10000) return `${(v / 10000).toFixed(v >= 100000 ? 0 : 1).replace(/\.0$/, '')}万`
  return String(v)
}

export function formatTime(iso, showYear = true) {
  const d = parseApiTime(iso)
  if (!d) return ''
  const pad = (n) => String(n).padStart(2, '0')
  if (showYear) {
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
  }
  return `${d.getMonth() + 1}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function formatDateOnly(iso) {
  const d = parseApiTime(iso)
  if (!d) return ''
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** 是否视为新帖（24 小时内发布） */
export function isNewThread(iso) {
  const d = parseApiTime(iso)
  if (!d) return false
  return Date.now() - d.getTime() < 24 * 60 * 60 * 1000
}
