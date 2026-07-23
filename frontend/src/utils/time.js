/** 全站按北京时间（Asia/Shanghai）解析与展示。 */

const BEIJING = 'Asia/Shanghai'

function hasExplicitTz(s) {
  return /[zZ]$|[+-]\d{2}:?\d{2}$/.test(s)
}

/** 将接口时间解析为 Date（内部瞬时正确，展示用北京时区格式化）。 */
export function parseApiTime(iso) {
  if (!iso) return null
  if (iso instanceof Date) return Number.isNaN(iso.getTime()) ? null : iso
  const s = String(iso).trim()
  if (!s) return null
  const normalized = s.includes('T') ? s : s.replace(' ', 'T')
  // 无时区：按北京墙钟；有 Z/+00：按标注时区（再格式化为北京时间）
  const d = new Date(hasExplicitTz(normalized) ? normalized : `${normalized}+08:00`)
  return Number.isNaN(d.getTime()) ? null : d
}

function pad(n) {
  return String(n).padStart(2, '0')
}

/** 用北京时间取年月日时分 */
function beijingParts(d) {
  const fmt = new Intl.DateTimeFormat('en-CA', {
    timeZone: BEIJING,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
  const parts = Object.fromEntries(fmt.formatToParts(d).filter((p) => p.type !== 'literal').map((p) => [p.type, p.value]))
  return {
    year: parts.year,
    month: parts.month,
    day: parts.day,
    hour: parts.hour === '24' ? '00' : parts.hour,
    minute: parts.minute,
  }
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

/** 列表「最后发表」：一天内相对时间，否则月-日 时:分（北京时间） */
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
  const p = beijingParts(d)
  if (showYear) {
    return `${p.year}-${p.month}-${p.day} ${p.hour}:${p.minute}`
  }
  return `${Number(p.month)}-${p.day} ${p.hour}:${p.minute}`
}

export function formatDateOnly(iso) {
  const d = parseApiTime(iso)
  if (!d) return ''
  const p = beijingParts(d)
  return `${p.year}-${p.month}-${p.day}`
}

/** 是否视为新帖（24 小时内发布） */
export function isNewThread(iso) {
  const d = parseApiTime(iso)
  if (!d) return false
  return Date.now() - d.getTime() < 24 * 60 * 60 * 1000
}
