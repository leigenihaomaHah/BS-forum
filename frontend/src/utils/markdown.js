/**
 * Lightweight Markdown → sanitized HTML for forum posts.
 * Supports: headings, bold/italic, inline code, fenced code, links, lists, quotes, paragraphs.
 */
export function renderMarkdown(src) {
  if (!src) return ''
  let text = String(src).replace(/\r\n/g, '\n')

  // fenced code blocks
  const blocks = []
  text = text.replace(/```([\s\S]*?)```/g, (_, code) => {
    const i = blocks.length
    blocks.push(`<pre class="md-code"><code>${escapeHtml(code.replace(/^\n|\n$/g, ''))}</code></pre>`)
    return `\n%%BLOCK${i}%%\n`
  })

  const lines = text.split('\n')
  const out = []
  let inUl = false
  let inOl = false
  let inQuote = false

  const closeLists = () => {
    if (inUl) { out.push('</ul>'); inUl = false }
    if (inOl) { out.push('</ol>'); inOl = false }
  }
  const closeQuote = () => {
    if (inQuote) { out.push('</blockquote>'); inQuote = false }
  }

  for (const raw of lines) {
    if (/^%%BLOCK\d+%%$/.test(raw.trim())) {
      closeLists(); closeQuote()
      out.push(raw.trim())
      continue
    }

    const h = raw.match(/^(#{1,3})\s+(.+)$/)
    if (h) {
      closeLists(); closeQuote()
      const lv = h[1].length
      out.push(`<h${lv + 2} class="md-h">${inline(h[2])}</h${lv + 2}>`)
      continue
    }

    if (/^>\s?/.test(raw)) {
      closeLists()
      if (!inQuote) { out.push('<blockquote class="md-quote">'); inQuote = true }
      out.push(`<p>${inline(raw.replace(/^>\s?/, ''))}</p>`)
      continue
    }
    closeQuote()

    if (/^[-*]\s+/.test(raw)) {
      if (inOl) { out.push('</ol>'); inOl = false }
      if (!inUl) { out.push('<ul class="md-list">'); inUl = true }
      out.push(`<li>${inline(raw.replace(/^[-*]\s+/, ''))}</li>`)
      continue
    }
    if (/^\d+\.\s+/.test(raw)) {
      if (inUl) { out.push('</ul>'); inUl = false }
      if (!inOl) { out.push('<ol class="md-list">'); inOl = true }
      out.push(`<li>${inline(raw.replace(/^\d+\.\s+/, ''))}</li>`)
      continue
    }
    closeLists()

    if (!raw.trim()) {
      out.push('')
      continue
    }
    out.push(`<p>${inline(raw)}</p>`)
  }
  closeLists(); closeQuote()

  let html = out.join('\n')
  html = html.replace(/%%BLOCK(\d+)%%/g, (_, i) => blocks[Number(i)] || '')
  return html
}

function inline(s) {
  let t = escapeHtml(s)
  t = t.replace(/`([^`]+)`/g, '<code class="md-inline">$1</code>')
  t = t.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
  t = t.replace(/__([^_]+)__/g, '<strong>$1</strong>')
  t = t.replace(/(^|[^*])\*([^*\n]+)\*(?!\*)/g, '$1<em>$2</em>')
  t = t.replace(/\[([^\]]+)\]\((https?:\/\/[^)\s]+|\/[^)\s]*)\)/g, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>')
  // @mentions keep as text (already escaped)
  return t
}

function escapeHtml(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

export function markdownHint() {
  return '支持 Markdown：**粗体** *斜体* `代码` # 标题 - 列表 [文字](链接)'
}
