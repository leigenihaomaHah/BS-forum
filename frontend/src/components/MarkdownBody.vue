<template>
  <div class="md-body" v-html="html"></div>
</template>

<script setup>
import { computed } from 'vue'
import { renderMarkdown } from '../utils/markdown.js'

const props = defineProps({
  content: { type: String, default: '' },
})

const html = computed(() => renderMarkdown(props.content))
</script>

<style>
.md-body {
  line-height: 1.75;
  overflow-wrap: anywhere;
  word-break: break-word;
  color: #142033;
  font-size: 14px;
  max-width: 100%;
}
.md-body > :first-child { margin-top: 0; }
.md-body > :last-child { margin-bottom: 0; }
.md-body p { margin: 0 0 0.75em; }
.md-body .md-h { margin: 0.9em 0 0.45em; font-weight: 700; line-height: 1.3; }
.md-body h3.md-h { font-size: 1.25em; }
.md-body h4.md-h { font-size: 1.1em; }
.md-body h5.md-h { font-size: 1em; }
.md-body .md-list { margin: 0 0 0.75em; padding-left: 1.4em; }
.md-body .md-list li { margin: 0.2em 0; }
.md-body .md-quote {
  margin: 0 0 0.75em;
  padding: 8px 12px;
  border-left: 3px solid #0d9488;
  background: #f8fafc;
  color: #475569;
  border-radius: 0 8px 8px 0;
}
.md-body .md-quote p { margin: 0.25em 0; }
.md-body .md-inline {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.9em;
  background: #f1f5f9;
  padding: 1px 5px;
  border-radius: 4px;
}
.md-body .md-code {
  margin: 0 0 0.75em;
  padding: 12px 14px;
  background: #0f172a;
  color: #e2e8f0;
  border-radius: 8px;
  overflow-x: auto;
  font-size: 12.5px;
  line-height: 1.55;
}
.md-body .md-code code {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}
.md-body a { color: #0d9488; text-decoration: none; }
.md-body a:hover { text-decoration: underline; }
.md-body strong { font-weight: 700; }
</style>
