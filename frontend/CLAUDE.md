# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Tech Stack

- **Vue 3** (Composition API, `<script setup>` SFCs) + **Vite 8**
- **Vue Router 4** (lazy-loaded routes, `createWebHistory`)
- **Pinia 4** (composition-style stores)
- **Axios** (HTTP client with interceptors for auth & error handling)
- **Bootstrap 5** (CSS only — no JS components) + **Bootstrap Icons**
- No TypeScript, no test runner, no linter

## Commands

```sh
npm run dev       # Start dev server on port 5173, proxies /api → localhost:5000
npm run build     # Production build
npm run preview   # Preview production build
```

## Project Structure

```
src/
  api/http.js           # Axios instance: baseURL /api, JWT Bearer interceptor, error normalization
  router/index.js       # 7 routes — home, forum/:id, thread/:id, forum/:id/new, login, register, user/:id
  stores/auth.js        # Pinia auth store: token + user persisted to localStorage, login/register/fetchMe/signIn/logout
  components/
    AppLayout.vue       # Shell layout — sticky header, nav, sub-nav, footer, toast system, slot for page content
    ForumSection.vue    # Collapsible category grid with forum list + latest thread preview
    HotThreadsPanel.vue # Tabbed hot threads (day/week/month), ranked list with heat scores
  views/
    HomeView.vue        # Categories list + hot threads panel
    ForumView.vue       # Paginated thread list for a forum (20/page)
    ThreadView.vue      # Post list + reply form + like button
    CreateThreadView.vue# New thread form (requires Lv.2)
    LoginView.vue       # Login form, demo accounts shown
    RegisterView.vue    # Registration form
    UserProfileView.vue # Profile card with level progress bar
  assets/
    forum.css           # All custom styles, CSS custom properties design system, responsive
    hero.png
```

## Architecture Notes

- **No component library**: All UI is hand-rolled CSS in `forum.css` using CSS custom properties (`--ink`, `--accent`, etc.)
- **Error handling**: Axios interceptor extracts `response.data.message` and rejects with `Error`, views catch and display via local `error` ref
- **Auth flow**: On app init, if token exists in localStorage, `fetchMe()` is called to validate; if it fails, user is logged out
- **Route param reactivity**: Views watch `route.params.id` to reload data when navigating between same-component routes
- **API**: All endpoints are under `/api/*`, proxied to `http://localhost:5000` in dev. No TypeScript types — API response shapes are assumed inline.
- **No test suite**: No testing infrastructure is configured.
