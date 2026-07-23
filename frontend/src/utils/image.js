export function compressImage(file, maxDim, quality) {
  return new Promise((resolve) => {
    const reader = new FileReader()
    reader.onload = (e) => {
      const img = new Image()
      img.onload = () => {
        let { width, height } = img
        if (width > maxDim || height > maxDim) {
          const ratio = Math.min(maxDim / width, maxDim / height)
          width = Math.round(width * ratio)
          height = Math.round(height * ratio)
        }
        const canvas = document.createElement('canvas')
        canvas.width = width
        canvas.height = height
        const ctx = canvas.getContext('2d')
        ctx.drawImage(img, 0, 0, width, height)
        resolve(canvas.toDataURL('image/jpeg', quality))
      }
      img.src = e.target.result
    }
    reader.readAsDataURL(file)
  })
}

/** 从剪贴板事件取出图片文件（Ctrl+V / 右键粘贴） */
export function filesFromClipboard(clipboardData) {
  if (!clipboardData) return []
  const out = []
  const seen = new Set()
  const push = (f) => {
    if (!f || !f.type?.startsWith('image/') || seen.has(f)) return
    seen.add(f)
    out.push(f)
  }
  for (const f of clipboardData.files || []) push(f)
  if (out.length) return out
  for (const item of clipboardData.items || []) {
    if (item.kind === 'file' && item.type?.startsWith('image/')) {
      push(item.getAsFile())
    }
  }
  return out
}
