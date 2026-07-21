import api from '../api/http'
import { compressImage } from './image'

export async function uploadImages(items, maxDim = 1920, quality = 0.85) {
  if (!items.length) return []

  const formData = new FormData()
  for (const item of items) {
    let blob
    let name = `img_${Date.now()}.jpg`
    if (typeof item === 'string') {
      blob = dataUrlToBlob(item)
    } else {
      const dataUrl = await compressImage(item, maxDim, quality)
      blob = dataUrlToBlob(dataUrl)
      name = item.name.includes('.png') ? `img_${Date.now()}.png` : name
    }
    formData.append('files', blob, name)
  }

  const { data } = await api.post('/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data.urls || []
}

function dataUrlToBlob(dataUrl) {
  const [meta, b64] = dataUrl.split(',', 2)
  const mime = meta.match(/:(.*?);/)?.[1] || 'image/jpeg'
  const bin = atob(b64)
  const arr = new Uint8Array(bin.length)
  for (let i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i)
  return new Blob([arr], { type: mime })
}
