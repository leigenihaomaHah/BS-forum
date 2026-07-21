const AVATAR_COLORS = ['#0d9488','#0891b2','#2563eb','#7c3aed','#db2777','#dc2626','#ea580c','#ca8a04','#059669','#0284c7']

export function defaultAvatar(nickname) {
  const initials = (nickname || '?').replace(/[^\w一-鿿]/g, '').slice(0, 2).toUpperCase() || '?'
  const color = AVATAR_COLORS[[...(nickname || '?')].reduce((s, c) => s + c.charCodeAt(0), 0) % AVATAR_COLORS.length]
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="80" height="80" viewBox="0 0 80 80">
    <rect width="80" height="80" rx="16" fill="${color}"/>
    <text x="40" y="40" text-anchor="middle" dominant-baseline="central" fill="white" font-size="32" font-weight="700" font-family="sans-serif">${initials}</text>
  </svg>`
  return 'data:image/svg+xml,' + encodeURIComponent(svg)
}
