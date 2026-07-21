import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../views/HomeView.vue'
import { isAdminUser } from '../config/levels.js'
import { useAuthModalStore } from '../stores/authModal'

const routes = [
  { path: '/', name: 'home', component: HomeView },
  { path: '/forum/:id', name: 'forum', component: () => import('../views/ForumView.vue') },
  { path: '/thread/:id', name: 'thread', component: () => import('../views/ThreadView.vue') },
  { path: '/forum/:id/new', name: 'create', component: () => import('../views/CreateThreadView.vue') },
  { path: '/login', name: 'login', component: { template: '<div/>' }, meta: { authModal: 'login' } },
  { path: '/register', name: 'register', component: { template: '<div/>' }, meta: { authModal: 'register' } },
  { path: '/sign-in', name: 'signin', component: () => import('../views/SignInView.vue') },
  { path: '/lottery', name: 'lottery', component: () => import('../views/LotteryView.vue') },
  { path: '/invite', name: 'invite', component: () => import('../views/InviteView.vue') },
  { path: '/shop', name: 'shop', component: () => import('../views/ShopView.vue') },
  { path: '/recharge', name: 'recharge', component: () => import('../views/RechargeView.vue') },
  { path: '/notifications', name: 'notifications', component: () => import('../views/NotificationsView.vue') },
  { path: '/history', name: 'history', component: () => import('../views/HistoryView.vue') },
  { path: '/drafts', name: 'drafts', component: () => import('../views/DraftsView.vue') },
  { path: '/subscriptions', name: 'subscriptions', component: () => import('../views/SubscriptionsView.vue') },
  { path: '/me', name: 'me', component: () => import('../views/MeHubView.vue') },
  { path: '/tag/:name', name: 'tag', component: () => import('../views/TagView.vue') },
  { path: '/user/:id', name: 'user', component: () => import('../views/UserProfileView.vue') },
  { path: '/settings', name: 'settings', component: () => import('../views/SettingsView.vue') },
  { path: '/search', name: 'search', component: () => import('../views/SearchView.vue') },
  {
    path: '/admin',
    component: () => import('../components/AdminLayout.vue'),
    beforeEnter: adminGuard,
    children: [
      { path: '', name: 'admin-dashboard', component: () => import('../views/admin/DashboardView.vue') },
      { path: 'users', name: 'admin-users', component: () => import('../views/admin/UsersView.vue') },
      { path: 'forums', name: 'admin-forums', component: () => import('../views/admin/ForumsView.vue') },
      { path: 'threads', name: 'admin-threads', component: () => import('../views/admin/ThreadsView.vue') },
      { path: 'levels', name: 'admin-levels', component: () => import('../views/admin/LevelsView.vue') },
      { path: 'signin', name: 'admin-signin', component: () => import('../views/admin/SigninStatsView.vue') },
      { path: 'roles', name: 'admin-roles', component: () => import('../views/admin/RolesView.vue') },
      { path: 'reports', name: 'admin-reports', component: () => import('../views/admin/ReportsView.vue') },
      { path: 'moderators', name: 'admin-moderators', component: () => import('../views/admin/ModeratorsView.vue') },
      { path: 'banners', name: 'admin-banners', component: () => import('../views/admin/BannersView.vue') },
      { path: 'recharge', name: 'admin-recharge', component: () => import('../views/admin/RechargeView.vue') },
    ],
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior() {
    return { top: 0 }
  }
})

router.beforeEach((to, from) => {
  if (to.meta.authModal === 'login') {
    useAuthModalStore().openLogin()
    return from.name ? false : { path: '/', replace: true }
  }
  if (to.meta.authModal === 'register') {
    useAuthModalStore().openRegister(to.query.invite?.toString() || '')
    return from.name ? false : { path: '/', replace: true }
  }
})

function adminGuard(to, from, next) {
  const token = localStorage.getItem('token')
  if (!token) return next('/login')
  const user = JSON.parse(localStorage.getItem('user') || 'null')
  if (!isAdminUser(user)) return next('/')
  next()
}

export default router
