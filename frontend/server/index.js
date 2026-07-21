import express from 'express'
import cors from 'cors'

const app = express()
app.use(cors())
app.use(express.json({ limit: '50mb' }))

/* ================================================================
   Level system (mirrors frontend config)
   ================================================================ */
const LEVELS = [
  { level: 1,  name: '新手',    minPoints: 0 },
  { level: 2,  name: '入门',    minPoints: 50 },
  { level: 3,  name: '进阶',    minPoints: 200 },
  { level: 4,  name: '精通',    minPoints: 500 },
  { level: 5,  name: '精英',    minPoints: 1000 },
  { level: 6,  name: '大师',    minPoints: 2000 },
  { level: 7,  name: '宗师',    minPoints: 4000 },
  { level: 8,  name: '传奇',    minPoints: 8000 },
  { level: 9,  name: '传说',    minPoints: 15000 },
  { level: 10, name: '殿堂',    minPoints: 30000 },
]

function resolveLevel(points) {
  let matched = LEVELS[0]
  for (const l of LEVELS) {
    if (points >= l.minPoints) matched = l
  }
  return matched
}

/* ================================================================
   Users (test data at various levels)
   ================================================================ */
function seedSignInRecords(daysBack, totalExtra = 0) {
  const records = []
  for (let i = daysBack; i >= 1; i--) {
    const d = new Date()
    d.setDate(d.getDate() - i)
    records.push(d.toISOString().slice(0, 10))
  }
  // Add extra intermittent records for total days
  for (let i = 0; i < totalExtra; i++) {
    const d = new Date()
    d.setDate(d.getDate() - daysBack - Math.floor(Math.random() * 365) - 1)
    records.push(d.toISOString().slice(0, 10))
  }
  return [...new Set(records)].sort()
}

const users = {
  1: {
    id: 1, username: 'admin',    nickname: '管理员',   password: 'admin123',
    points: 9800,  coins: 520, level: 8,  levelName: '传奇',
    createdAt: '2024-01-15T08:00:00Z', nextLevelPoints: 15000,
    signInRecords: seedSignInRecords(45, 120), role: 'super_admin',
  },
  2: {
    id: 2, username: 'alice',    nickname: '爱丽丝',   password: 'alice123',
    points: 1200,  coins: 180, level: 5,  levelName: '精英',
    createdAt: '2024-03-20T10:30:00Z', nextLevelPoints: 2000,
    signInRecords: seedSignInRecords(12, 55), role: 'user',
  },
  3: {
    id: 3, username: 'bob',      nickname: '鲍勃',     password: 'bob123',
    points: 320,   coins: 65,  level: 3,  levelName: '进阶',
    createdAt: '2024-06-01T14:00:00Z', nextLevelPoints: 500,
    signInRecords: seedSignInRecords(5, 28), role: 'user',
  },
  4: {
    id: 4, username: 'charlie',  nickname: '查理',     password: 'charlie123',
    points: 80,    coins: 22,  level: 2,  levelName: '入门',
    createdAt: '2024-09-10T09:00:00Z', nextLevelPoints: 200,
    signInRecords: seedSignInRecords(3, 12), role: 'user',
  },
  5: {
    id: 5, username: 'diana',    nickname: '黛安娜',   password: 'diana123',
    points: 10,    coins: 10,  level: 1,  levelName: '新手',
    createdAt: '2025-01-05T16:00:00Z', nextLevelPoints: 50,
    signInRecords: [], role: 'user',
  },
}

/** Calculate consecutive days (from today or yesterday if today not yet signed in) */
function calcConsecutiveDays(records) {
  if (!records || records.length === 0) return 0
  const set = new Set(records)
  const start = new Date()
  // If today is not signed in, start from yesterday
  if (!set.has(start.toISOString().slice(0, 10))) {
    start.setDate(start.getDate() - 1)
  }
  let count = 0
  const d = new Date(start.toISOString().slice(0, 10))
  while (set.has(d.toISOString().slice(0, 10))) {
    count++
    d.setDate(d.getDate() - 1)
  }
  return count
}

function userPublic(u) {
  const { password, signInRecords, ...rest } = u
  return {
    ...rest,
    consecutiveDays: calcConsecutiveDays(u.signInRecords),
    totalSignInDays: u.signInRecords ? u.signInRecords.length : 0,
    role: u.role || 'user',
    avatar: u.avatar || generateDefaultAvatar(u.nickname),
  }
}

function resolveNextLevelPoints(points) {
  const next = LEVELS.find((l) => points < l.minPoints)
  return next ? next.minPoints : points
}

function refreshLevel(u) {
  const l = resolveLevel(u.points)
  u.level = l.level
  u.levelName = l.name
  u.nextLevelPoints = resolveNextLevelPoints(u.points)
}

/* ================================================================
   Avatar generation
   ================================================================ */
const AVATAR_COLORS = ['#0d9488','#0891b2','#2563eb','#7c3aed','#db2777','#dc2626','#ea580c','#ca8a04','#059669','#0284c7']

function generateDefaultAvatar(nickname) {
  const initials = nickname.replace(/[^\w一-鿿]/g, '').slice(0, 2).toUpperCase() || '?'
  const colorIdx = [...nickname].reduce((s, c) => s + c.charCodeAt(0), 0) % AVATAR_COLORS.length
  const color = AVATAR_COLORS[colorIdx]
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="80" height="80" viewBox="0 0 80 80">
    <rect width="80" height="80" rx="16" fill="${color}"/>
    <text x="40" y="40" text-anchor="middle" dominant-baseline="central" fill="white" font-size="32" font-weight="700" font-family="sans-serif">${initials}</text>
  </svg>`
  return 'data:image/svg+xml,' + encodeURIComponent(svg)
}

for (const u of Object.values(users)) {
  u.avatar = generateDefaultAvatar(u.nickname)
}

/* ================================================================
   Categories & Forums
   ================================================================ */
let threadIdCounter = 1000
let postIdCounter = 10000

const categories = [
  {
    id: 1, name: '综合交流', icon: '💬', forums: [
      {
        id: 1, name: '闲聊茶馆', icon: '☕', description: '天南海北，无所不谈',
        threadCount: 8, postCount: 42, todayThreadCount: 2,
        latestThread: { id: 101, title: '大家今天心情怎么样？', createdAt: '2026-07-17T09:15:00Z', authorLevel: 5, authorNickname: '爱丽丝' },
      },
      {
        id: 2, name: '技术讨论', icon: '💻', description: '编程、科技、数码产品',
        threadCount: 6, postCount: 38, todayThreadCount: 1,
        latestThread: { id: 105, title: 'Vue 3 大家用得怎么样？', createdAt: '2026-07-17T08:30:00Z', authorLevel: 8, authorNickname: '管理员' },
      },
    ]
  },
  {
    id: 2, name: '同城生活', icon: '🏙️', forums: [
      {
        id: 3, name: '美食探店', icon: '🍜', description: '分享好吃的店',
        threadCount: 12, postCount: 65, todayThreadCount: 0, fullWidth: true,
        latestThread: { id: 108, title: '公司楼下的兰州拉面涨价了', createdAt: '2026-07-16T12:00:00Z', authorLevel: 3, authorNickname: '鲍勃' },
      },
    ]
  },
  {
    id: 3, name: '兴趣爱好', icon: '🎯', forums: [
      {
        id: 4, name: '摄影天地', icon: '📷', description: '摄影作品交流',
        threadCount: 5, postCount: 27, todayThreadCount: 0,
        latestThread: { id: 110, title: '今天拍到了很美的晚霞', createdAt: '2026-07-15T19:30:00Z', authorLevel: 2, authorNickname: '查理' },
      },
      {
        id: 5, name: '游戏讨论', icon: '🎮', description: '主机、PC、手游',
        threadCount: 9, postCount: 53, todayThreadCount: 1,
        latestThread: { id: 112, title: '黑神话悟空二周目通关感想', createdAt: '2026-07-17T07:00:00Z', authorLevel: 5, authorNickname: '爱丽丝' },
      },
    ]
  },
  {
    id: 4, name: '站务公告', icon: '📢', forums: [
      {
        id: 6, name: '公告区', icon: '📋', description: '论坛官方公告',
        threadCount: 3, postCount: 15, todayThreadCount: 0,
        latestThread: { id: 115, title: '论坛等级制度全面升级', createdAt: '2026-07-14T10:00:00Z', authorLevel: 8, authorNickname: '管理员' },
      },
    ]
  },
]

/* ================================================================
   Threads
   ================================================================ */
const threads = {
  101: {
    id: 101, forumId: 1, forumName: '闲聊茶馆', title: '大家今天心情怎么样？',
    type: 'public',
    views: 342, replyCount: 5, likeCount: 12, likedByMe: false,
    authorLevel: 5, authorNickname: '爱丽丝',
    posts: [
      { id: 1001, floor: 1, content: '今天天气真好，出门转了一圈，心情不错！你们呢？', createdAt: '2026-07-17T09:15:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
      { id: 1002, floor: 2, content: '还行，就是有点热🥵', createdAt: '2026-07-17T09:30:00Z',
        author: { id: 3, nickname: '鲍勃', level: 3, levelName: '进阶', points: 320 } },
      { id: 1003, floor: 3, content: '刚下班，累死了😮‍💨', createdAt: '2026-07-17T10:00:00Z',
        author: { id: 4, nickname: '查理', level: 2, levelName: '入门', points: 80 } },
      { id: 1004, floor: 4, content: '楼上加油！周末就要到了', createdAt: '2026-07-17T11:20:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
      { id: 1005, floor: 5, content: '在家躺平最舒服了', createdAt: '2026-07-17T14:00:00Z',
        author: { id: 5, nickname: '黛安娜', level: 1, levelName: '新手', points: 10 } },
    ],
  },
  105: {
    id: 105, forumId: 2, forumName: '技术讨论', title: 'Vue 3 大家用得怎么样？',
    type: 'public',
    views: 521, replyCount: 5, likeCount: 28, likedByMe: false,
    authorLevel: 8, authorNickname: '管理员',
    posts: [
      { id: 1010, floor: 1, content: 'Vue 3 的 Composition API 写起来比 Options API 舒服太多了，代码复用性也更好。', createdAt: '2026-07-17T08:30:00Z',
        author: { id: 1, nickname: '管理员', level: 8, levelName: '传奇', points: 9800 },
        images: [
          'https://picsum.photos/seed/vue1/800/600',
          'https://picsum.photos/seed/vue2/800/500',
          'https://picsum.photos/seed/vue3/600/800',
        ] },
      { id: 1011, floor: 2, content: '确实，script setup 语法糖真香', createdAt: '2026-07-17T08:45:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
      { id: 1012, floor: 3, content: '我从 React 转过来的，感觉 Vue 的上手门槛确实低一些', createdAt: '2026-07-17T09:00:00Z',
        author: { id: 3, nickname: '鲍勃', level: 3, levelName: '进阶', points: 320 } },
      { id: 1013, floor: 4, content: '请问大家用 Vite 还是 Webpack？', createdAt: '2026-07-17T10:30:00Z',
        author: { id: 4, nickname: '查理', level: 2, levelName: '入门', points: 80 } },
      { id: 1014, floor: 5, content: 'Vite 啊，快太多了，开发体验完全不一样', createdAt: '2026-07-17T11:00:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
    ],
  },
  // Threads for other forums
  108: {
    id: 108, forumId: 3, forumName: '美食探店', title: '公司楼下的兰州拉面涨价了',
    type: 'coin', coinPrice: 5,
    views: 198, replyCount: 3, likeCount: 7, likedByMe: false,
    authorLevel: 3, authorNickname: '鲍勃',
    posts: [
      { id: 1020, floor: 1, content: '从15涨到18了，虽然还是好吃但有点肉疼', createdAt: '2026-07-16T12:00:00Z',
        author: { id: 3, nickname: '鲍勃', level: 3, levelName: '进阶', points: 320 },
        images: ['https://picsum.photos/seed/noodles/800/600'] },
      { id: 1021, floor: 2, content: '我们这边早就20了😅', createdAt: '2026-07-16T14:00:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
      { id: 1022, floor: 3, content: '食堂党路过，每月省不少钱', createdAt: '2026-07-16T18:00:00Z',
        author: { id: 5, nickname: '黛安娜', level: 1, levelName: '新手', points: 10 } },
    ],
  },
  110: {
    id: 110, forumId: 4, forumName: '摄影天地', title: '今天拍到了很美的晚霞',
    type: 'private',
    views: 89, replyCount: 2, likeCount: 15, likedByMe: false,
    authorLevel: 2, authorNickname: '查理',
    posts: [
      { id: 1030, floor: 1, content: '在楼顶拍的，用了手机的专业模式，色彩太惊艳了！大家可以试试傍晚6点左右去高处拍。', createdAt: '2026-07-15T19:30:00Z',
        author: { id: 4, nickname: '查理', level: 2, levelName: '入门', points: 80 },
        images: [
          'https://picsum.photos/seed/sunset1/800/600',
          'https://picsum.photos/seed/sunset2/800/600',
        ] },
      { id: 1031, floor: 2, content: '赞！晚霞确实是最容易出片的题材', createdAt: '2026-07-15T20:00:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
    ],
  },
  112: {
    id: 112, forumId: 5, forumName: '游戏讨论', title: '黑神话悟空二周目通关感想',
    type: 'public',
    views: 456, replyCount: 4, likeCount: 32, likedByMe: false,
    authorLevel: 5, authorNickname: '爱丽丝',
    posts: [
      { id: 1040, floor: 1, content: '二周目终于全成就了！隐藏结局的演出真的太震撼了。\n\n有几个建议给还在打的坛友：\n1. 一定要去第二章的隐藏地图\n2. 法术流和近战流各通一次体验完全不同\n3. 最后一场 boss 战打了一个小时才过😂', createdAt: '2026-07-17T07:00:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
      { id: 1041, floor: 2, content: '大佬牛逼！我才到第三章', createdAt: '2026-07-17T08:00:00Z',
        author: { id: 4, nickname: '查理', level: 2, levelName: '入门', points: 80 } },
      { id: 1042, floor: 3, content: '杨戬打了三个小时的默默飘过...', createdAt: '2026-07-17T09:30:00Z',
        author: { id: 3, nickname: '鲍勃', level: 3, levelName: '进阶', points: 320 } },
      { id: 1043, floor: 4, content: '刚入手，请问新手推荐什么流派？', createdAt: '2026-07-17T10:45:00Z',
        author: { id: 5, nickname: '黛安娜', level: 1, levelName: '新手', points: 10 } },
    ],
  },
  115: {
    id: 115, forumId: 6, forumName: '公告区', title: '论坛等级制度全面升级',
    type: 'public',
    views: 1023, replyCount: 8, likeCount: 45, likedByMe: false,
    authorLevel: 8, authorNickname: '管理员',
    posts: [
      { id: 1050, floor: 1, content: '各位坛友，我们升级了等级制度！\n\n新等级体系从 Lv.1 到 Lv.10，每个等级都有不同的权限和福利：\n\n- Lv.2 开始可以发帖\n- Lv.3 可以上传图片\n- Lv.5 签到奖励翻倍\n- Lv.8 获得管理权限\n\n详细等级说明请查看个人资料页。祝大家在社区玩得开心！', createdAt: '2026-07-14T10:00:00Z',
        author: { id: 1, nickname: '管理员', level: 8, levelName: '传奇', points: 9800 } },
      { id: 1051, floor: 2, content: '支持！努力升级中💪', createdAt: '2026-07-14T11:00:00Z',
        author: { id: 3, nickname: '鲍勃', level: 3, levelName: '进阶', points: 320 } },
      { id: 1052, floor: 3, content: 'Lv.10 的要求好高啊，30000 积分', createdAt: '2026-07-14T14:00:00Z',
        author: { id: 4, nickname: '查理', level: 2, levelName: '入门', points: 80 } },
      { id: 1053, floor: 4, content: '慢慢来嘛，重在参与', createdAt: '2026-07-15T09:00:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
      { id: 1054, floor: 5, content: '新等级标识好好看！', createdAt: '2026-07-15T16:00:00Z',
        author: { id: 5, nickname: '黛安娜', level: 1, levelName: '新手', points: 10 } },
      { id: 1055, floor: 6, content: '请问积分怎么赚比较快？', createdAt: '2026-07-16T08:00:00Z',
        author: { id: 4, nickname: '查理', level: 2, levelName: '入门', points: 80 } },
      { id: 1056, floor: 7, content: '每天签到 + 发回帖，坚持最重要', createdAt: '2026-07-16T10:00:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
      { id: 1057, floor: 8, content: '已经连续签到 45 天了，坚持就是胜利！', createdAt: '2026-07-17T08:00:00Z',
        author: { id: 1, nickname: '管理员', level: 8, levelName: '传奇', points: 9800 } },
    ],
  },
  // Extra threads to fill up forum 1 (闲聊茶馆)
  102: {
    id: 102, forumId: 1, forumName: '闲聊茶馆', title: '周末有什么好去处推荐？',
    type: 'public',
    views: 156, replyCount: 4, likeCount: 8, likedByMe: false,
    authorLevel: 2, authorNickname: '查理',
    posts: [
      { id: 1060, floor: 1, content: '最近周末都不知道去哪，大家有什么推荐吗？', createdAt: '2026-07-12T10:00:00Z',
        author: { id: 4, nickname: '查理', level: 2, levelName: '入门', points: 80 } },
      { id: 1061, floor: 2, content: '去爬山吧，最近天气不错', createdAt: '2026-07-12T11:00:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
      { id: 1062, floor: 3, content: '在家打游戏不香吗😄', createdAt: '2026-07-12T14:00:00Z',
        author: { id: 3, nickname: '鲍勃', level: 3, levelName: '进阶', points: 320 } },
      { id: 1063, floor: 4, content: '图书馆看书，安静又凉快', createdAt: '2026-07-13T09:00:00Z',
        author: { id: 5, nickname: '黛安娜', level: 1, levelName: '新手', points: 10 } },
    ],
  },
  103: {
    id: 103, forumId: 1, forumName: '闲聊茶馆', title: '大家平时用什么输入法？',
    type: 'public',
    views: 89, replyCount: 3, likeCount: 5, likedByMe: false,
    authorLevel: 3, authorNickname: '鲍勃',
    posts: [
      { id: 1070, floor: 1, content: '我一直用搜狗，词库比较大', createdAt: '2026-07-10T08:00:00Z',
        author: { id: 3, nickname: '鲍勃', level: 3, levelName: '进阶', points: 320 } },
      { id: 1071, floor: 2, content: '微软拼音自带就够用了', createdAt: '2026-07-10T09:30:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
      { id: 1072, floor: 3, content: '小狼毫，定制党的最爱', createdAt: '2026-07-10T11:00:00Z',
        author: { id: 1, nickname: '管理员', level: 8, levelName: '传奇', points: 9800 } },
    ],
  },
  // Extra thread for forum 2 (技术讨论)
  106: {
    id: 106, forumId: 2, forumName: '技术讨论', title: '推荐几个好用的 VS Code 插件',
    type: 'public',
    views: 267, replyCount: 4, likeCount: 18, likedByMe: false,
    authorLevel: 5, authorNickname: '爱丽丝',
    posts: [
      { id: 1080, floor: 1, content: '1. GitHub Copilot - AI 编程助手\n2. Error Lens - 错误显示更直观\n3. Thunder Client - 轻量 API 测试\n4. GitLens - Git 历史可视化\n5. Pretty TypeScript Errors - 错误提示更友好', createdAt: '2026-07-11T14:00:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
      { id: 1081, floor: 2, content: 'Markdown Preview Enhanced 也很棒', createdAt: '2026-07-11T15:00:00Z',
        author: { id: 3, nickname: '鲍勃', level: 3, levelName: '进阶', points: 320 } },
      { id: 1082, floor: 3, content: 'Material Icon Theme 必备，看着舒服', createdAt: '2026-07-11T16:30:00Z',
        author: { id: 4, nickname: '查理', level: 2, levelName: '入门', points: 80 } },
      { id: 1083, floor: 4, content: '装了 Copilot 后就回不去了😂', createdAt: '2026-07-12T08:00:00Z',
        author: { id: 1, nickname: '管理员', level: 8, levelName: '传奇', points: 9800 } },
    ],
  },
}
// Extra threads for forum 5 (游戏讨论)
const extraThreads = [
  { id: 113, forumId: 5, forumName: '游戏讨论', title: '老头环 DLC 到底值不值得买？',
    type: 'public',
    views: 312, replyCount: 6, likeCount: 22, likedByMe: false,
    authorLevel: 3, authorNickname: '鲍勃',
    posts: [
      { id: 1090, floor: 1, content: '如题，犹豫中，玩过的朋友说说感受？', createdAt: '2026-07-13T10:00:00Z',
        author: { id: 3, nickname: '鲍勃', level: 3, levelName: '进阶', points: 320 } },
      { id: 1091, floor: 2, content: '买！内容量相当于一个中型游戏了', createdAt: '2026-07-13T11:00:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
      { id: 1092, floor: 3, content: '难度比本体高不少，要有心理准备', createdAt: '2026-07-13T14:00:00Z',
        author: { id: 1, nickname: '管理员', level: 8, levelName: '传奇', points: 9800 } },
    ],
  },
  { id: 114, forumId: 5, forumName: '游戏讨论', title: '求推荐休闲类游戏',
    type: 'public',
    views: 145, replyCount: 3, likeCount: 10, likedByMe: false,
    authorLevel: 1, authorNickname: '黛安娜',
    posts: [
      { id: 1095, floor: 1, content: '最近工作压力大，想找点轻松的游戏玩', createdAt: '2026-07-15T20:00:00Z',
        author: { id: 5, nickname: '黛安娜', level: 1, levelName: '新手', points: 10 } },
      { id: 1096, floor: 2, content: '星露谷物语！治愈神作', createdAt: '2026-07-15T21:00:00Z',
        author: { id: 4, nickname: '查理', level: 2, levelName: '入门', points: 80 } },
      { id: 1097, floor: 3, content: '动物森友会也不错，很放松', createdAt: '2026-07-16T09:00:00Z',
        author: { id: 2, nickname: '爱丽丝', level: 5, levelName: '精英', points: 1200 } },
    ],
  },
]
for (const t of extraThreads) threads[t.id] = t

// Populate authorId for seed threads from first post's author.id
for (const t of Object.values(threads)) {
  if (!t.authorId && t.posts?.[0]?.author?.id) {
    t.authorId = t.posts[0].author.id
  }
}

/* ================================================================
   Hot threads data
   ================================================================ */
const hotPools = {
  day: [
    { id: 101, title: '大家今天心情怎么样？', type: 'public', forumName: '闲聊茶馆', heat: 86, replyCount: 5, views: 342, authorLevel: 5, authorNickname: '爱丽丝' },
    { id: 112, title: '黑神话悟空二周目通关感想', type: 'public', forumName: '游戏讨论', heat: 92, replyCount: 4, views: 456, authorLevel: 5, authorNickname: '爱丽丝' },
    { id: 105, title: 'Vue 3 大家用得怎么样？', type: 'public', forumName: '技术讨论', heat: 78, replyCount: 5, views: 521, authorLevel: 8, authorNickname: '管理员' },
  ],
  week: [
    { id: 115, title: '论坛等级制度全面升级', type: 'public', forumName: '公告区', heat: 98, replyCount: 8, views: 1023, authorLevel: 8, authorNickname: '管理员' },
    { id: 105, title: 'Vue 3 大家用得怎么样？', type: 'public', forumName: '技术讨论', heat: 85, replyCount: 5, views: 521, authorLevel: 8, authorNickname: '管理员' },
    { id: 106, title: '推荐几个好用的 VS Code 插件', type: 'public', forumName: '技术讨论', heat: 79, replyCount: 4, views: 267, authorLevel: 5, authorNickname: '爱丽丝' },
    { id: 113, title: '老头环 DLC 到底值不值得买？', type: 'public', forumName: '游戏讨论', heat: 74, replyCount: 6, views: 312, authorLevel: 3, authorNickname: '鲍勃' },
  ],
  month: [
    { id: 115, title: '论坛等级制度全面升级', type: 'public', forumName: '公告区', heat: 100, replyCount: 8, views: 1023, authorLevel: 8, authorNickname: '管理员' },
    { id: 105, title: 'Vue 3 大家用得怎么样？', type: 'public', forumName: '技术讨论', heat: 91, replyCount: 5, views: 521, authorLevel: 8, authorNickname: '管理员' },
    { id: 108, title: '公司楼下的兰州拉面涨价了', type: 'coin', forumName: '美食探店', heat: 65, replyCount: 3, views: 198, authorLevel: 3, authorNickname: '鲍勃' },
  ],
}

// Build per-forum thread lists
function getThreadsForForum(forumId) {
  return Object.values(threads).filter((t) => t.forumId === forumId)
}

// Thread access control
const purchasedAccess = new Map() // threadId -> Set<userId>
const purchaseHistory = new Map() // userId -> [{ threadId, threadTitle, forumName, coinPrice, purchasedAt }]

// Favorites
const userFavorites = new Map() // userId -> Set<threadId>

// Tips
const tipRecords = new Map() // threadId -> [{ userId, amount, createdAt }]

// Notifications
const userNotifications = new Map() // userId -> [{ id, type, threadId, threadTitle, fromNickname, content, createdAt, read }]
let notifIdCounter = 10000

function addNotification(uid, data) {
  if (!userNotifications.has(uid)) userNotifications.set(uid, [])
  userNotifications.get(uid).unshift({ id: ++notifIdCounter, ...data, read: false, createdAt: new Date().toISOString() })
}

function canAccessThread(user, thread) {
  if (thread.type === 'public') return true
  if (user && thread.authorId === user.id) return true
  if (user && user.role === 'super_admin') return true
  if (thread.type === 'coin' && user && purchasedAccess.get(thread.id)?.has(user.id)) return true
  return false
}

// Track who is "logged in" (simple token-based session)
let currentToken = null
let currentUserId = null

const tokens = new Map() // token -> userId

function generateToken() {
  return Math.random().toString(36).substring(2) + Math.random().toString(36).substring(2)
}

/* ================================================================
   API Routes
   ================================================================ */

// --- Categories ---
app.get('/api/categories', (_req, res) => {
  res.json(categories)
})

// --- Forums ---
app.get('/api/forums/:id', (req, res) => {
  for (const cat of categories) {
    const forum = cat.forums.find((f) => f.id === Number(req.params.id))
    if (forum) return res.json(forum)
  }
  res.status(404).json({ message: '版块不存在' })
})

// --- Forum threads (paginated) ---
app.get('/api/forums/:id/threads', (req, res) => {
  const forumId = Number(req.params.id)
  const page = Number(req.query.page) || 1
  const pageSize = Number(req.query.pageSize) || 20
  let all = getThreadsForForum(forumId)
  const total = all.length
  const start = (page - 1) * pageSize
  const items = all.slice(start, start + pageSize).map((t) => {
    const author = t.authorId ? users[t.authorId] : null
    return {
      id: t.id, title: t.title, type: t.type || 'public', replyCount: t.replyCount, views: t.views,
      authorLevel: t.authorLevel, authorNickname: t.authorNickname,
      authorAvatar: author ? author.avatar : generateDefaultAvatar(t.authorNickname),
      lastReplyAt: t.posts[t.posts.length - 1]?.createdAt || t.posts[0]?.createdAt,
    }
  })
  res.json({ items, total })
})

// --- Single thread with posts ---
app.get('/api/threads/:id', (req, res) => {
  const t = threads[Number(req.params.id)]
  if (!t) return res.status(404).json({ message: '帖子不存在' })

  // Determine current user
  let currentUser = null
  const auth = req.headers.authorization
  if (auth && auth.startsWith('Bearer ')) {
    const uid = tokens.get(auth.slice(7))
    if (uid) currentUser = users[uid]
  }

  t.views += 1

  // Access control —— private: full restriction, coin: show structure but hide content
  if (!canAccessThread(currentUser, t)) {
    if (t.type === 'coin') {
      // Coin thread: show thread structure + posts but hide content
      return res.json({
        ...t,
        hiddenPosts: true,
        posts: t.posts.map((p) => {
          const authorUser = p.author?.id ? users[p.author.id] : null
          return {
            id: p.id, floor: p.floor, createdAt: p.createdAt,
            hidden: true, content: null, images: [], quote: null,
            author: {
              ...p.author,
              avatar: authorUser ? authorUser.avatar : generateDefaultAvatar(p.author?.nickname || '?'),
            },
          }
        }),
      })
    }
    // Private thread: minimal info, no posts
    return res.json({
      id: t.id, forumId: t.forumId, forumName: t.forumName,
      title: t.title, type: t.type, coinPrice: t.coinPrice,
      authorId: t.authorId, authorLevel: t.authorLevel, authorNickname: t.authorNickname,
      views: t.views, replyCount: t.replyCount, likeCount: t.likeCount, likedByMe: false,
      restricted: true, posts: [],
    })
  }

  // Enrich post authors with avatar
  const enriched = {
    ...t,
    posts: t.posts.map((p) => {
      const authorUser = p.author?.id ? users[p.author.id] : null
      return {
        ...p,
        author: {
          ...p.author,
          avatar: authorUser ? authorUser.avatar : generateDefaultAvatar(p.author?.nickname || '?'),
        },
      }
    }),
  }
  res.json(enriched)
})

// --- Edit thread title ---
app.put('/api/threads/:id', (req, res) => {
  const user = currentUserId ? users[currentUserId] : null
  if (!user) return res.status(401).json({ message: '请先登录' })

  const t = threads[Number(req.params.id)]
  if (!t) return res.status(404).json({ message: '帖子不存在' })
  if (t.authorId !== user.id && user.role !== 'super_admin') return res.status(403).json({ message: '无权编辑' })

  if (req.body.title) t.title = req.body.title
  res.json(t)
})

// --- Create thread ---
app.post('/api/threads', (req, res) => {
  const user = currentUserId ? users[currentUserId] : null
  if (!user) return res.status(401).json({ message: '请先登录' })
  if (user.level < 2) return res.status(403).json({ message: 'Lv.2 以上才能发帖（积分 ≥ 50）' })

  threadIdCounter++
  postIdCounter++
  const now = new Date().toISOString()
  const newThread = {
    id: threadIdCounter,
    forumId: req.body.forumId,
    forumName: '',
    title: req.body.title,
    type: req.body.type || 'public',
    coinPrice: req.body.type === 'coin' ? (req.body.coinPrice || 0) : 0,
    views: 0, replyCount: 0, likeCount: 0, likedByMe: false,
    authorId: user.id, authorLevel: user.level, authorNickname: user.nickname,
    posts: [{
      id: postIdCounter, floor: 1,
      content: req.body.content,
      images: req.body.images || [],
      createdAt: now,
      author: { id: user.id, nickname: user.nickname, level: user.level, levelName: user.levelName, points: user.points, avatar: user.avatar },
    }],
  }
  // Find forum name
  for (const cat of categories) {
    const forum = cat.forums.find((f) => f.id === req.body.forumId)
    if (forum) {
      newThread.forumName = forum.name
      forum.threadCount += 1
      forum.postCount += 1
      break
    }
  }

  threads[newThread.id] = newThread

  // Award points
  user.points += 10
  refreshLevel(user)

  res.json(newThread)
})

// --- Reply ---
app.post('/api/threads/:id/replies', (req, res) => {
  const user = currentUserId ? users[currentUserId] : null
  if (!user) return res.status(401).json({ message: '请先登录' })

  const t = threads[Number(req.params.id)]
  if (!t) return res.status(404).json({ message: '帖子不存在' })
  if (!canAccessThread(user, t)) return res.status(403).json({ message: '无权回复此帖子' })

  postIdCounter++
  const now = new Date().toISOString()
  const newPost = {
    id: postIdCounter,
    floor: t.posts.length + 1,
    content: req.body.content,
    images: req.body.images || [],
    createdAt: now,
    author: { id: user.id, nickname: user.nickname, level: user.level, levelName: user.levelName, points: user.points, avatar: user.avatar },
  }
  t.posts.push(newPost)
  t.replyCount += 1

  user.points += 2
  refreshLevel(user)

  // Notify thread author
  if (t.authorId !== user.id) {
    addNotification(t.authorId, {
      type: 'reply',
      threadId: t.id,
      threadTitle: t.title,
      fromNickname: user.nickname,
      content: req.body.content?.slice(0, 100),
    })
  }

  res.json(newPost)
})

// --- Edit post ---
app.put('/api/posts/:id', (req, res) => {
  const user = currentUserId ? users[currentUserId] : null
  if (!user) return res.status(401).json({ message: '请先登录' })

  for (const t of Object.values(threads)) {
    const post = t.posts.find((p) => p.id === Number(req.params.id))
    if (!post) continue
    if (post.author.id !== user.id && user.role !== 'super_admin') {
      return res.status(403).json({ message: '无权编辑' })
    }
    post.content = req.body.content
    post.editedAt = new Date().toISOString()
    return res.json(post)
  }
  res.status(404).json({ message: '回复不存在' })
})

// --- Delete post ---
app.delete('/api/posts/:id', (req, res) => {
  const user = currentUserId ? users[currentUserId] : null
  if (!user) return res.status(401).json({ message: '请先登录' })

  for (const t of Object.values(threads)) {
    const idx = t.posts.findIndex((p) => p.id === Number(req.params.id))
    if (idx === -1) continue
    const post = t.posts[idx]
    if (post.author.id !== user.id && user.role !== 'super_admin') {
      return res.status(403).json({ message: '无权删除' })
    }
    post.content = '该回复已被删除'
    post.images = []
    post.deleted = true
    return res.json({ message: '已删除' })
  }
  res.status(404).json({ message: '回复不存在' })
})

// --- Like ---
app.post('/api/threads/:id/like', (req, res) => {
  const user = currentUserId ? users[currentUserId] : null
  if (!user) return res.status(401).json({ message: '请先登录' })

  const t = threads[Number(req.params.id)]
  if (!t) return res.status(404).json({ message: '帖子不存在' })
  if (t.likedByMe) return res.status(400).json({ message: '已经赞过了' })

  t.likedByMe = true
  t.likeCount += 1
  res.json({ likeCount: t.likeCount })
})

// --- Purchase thread access ---
app.post('/api/threads/:id/purchase', (req, res) => {
  const user = currentUserId ? users[currentUserId] : null
  if (!user) return res.status(401).json({ message: '请先登录' })

  const t = threads[Number(req.params.id)]
  if (!t) return res.status(404).json({ message: '帖子不存在' })
  if (t.type !== 'coin') return res.status(400).json({ message: '此帖子无需购买' })
  if (t.authorId === user.id) return res.status(400).json({ message: '无需购买自己的帖子' })

  const bought = purchasedAccess.get(t.id)
  if (bought?.has(user.id)) return res.status(400).json({ message: '已经购买过了' })

  if (user.coins < t.coinPrice) return res.status(400).json({ message: `金币不足，需要 ${t.coinPrice} 金币` })

  user.coins -= t.coinPrice
  if (!purchasedAccess.has(t.id)) purchasedAccess.set(t.id, new Set())
  purchasedAccess.get(t.id).add(user.id)

  // Record purchase history
  if (!purchaseHistory.has(user.id)) purchaseHistory.set(user.id, [])
  purchaseHistory.get(user.id).push({
    threadId: t.id,
    threadTitle: t.title,
    forumName: t.forumName,
    coinPrice: t.coinPrice,
    purchasedAt: new Date().toISOString(),
  })

  res.json({ message: '购买成功', coins: user.coins })
})

// --- Favorite toggle ---
app.post('/api/threads/:id/favorite', (req, res) => {
  const user = currentUserId ? users[currentUserId] : null
  if (!user) return res.status(401).json({ message: '请先登录' })

  const t = threads[Number(req.params.id)]
  if (!t) return res.status(404).json({ message: '帖子不存在' })

  if (!userFavorites.has(user.id)) userFavorites.set(user.id, new Set())
  const set = userFavorites.get(user.id)

  if (set.has(t.id)) {
    set.delete(t.id)
    return res.json({ favorited: false, message: '已取消收藏' })
  } else {
    set.add(t.id)
    return res.json({ favorited: true, message: '已收藏' })
  }
})

// --- List favorites for user ---
app.get('/api/users/:id/favorites', (req, res) => {
  const uid = Number(req.params.id)
  const set = userFavorites.get(uid)
  if (!set || set.size === 0) return res.json([])

  const items = []
  for (const tid of set) {
    const t = threads[tid]
    if (t) {
      items.push({
        id: t.id, title: t.title, type: t.type || 'public',
        forumName: t.forumName, forumId: t.forumId,
        replyCount: t.replyCount, views: t.views,
        authorLevel: t.authorLevel, authorNickname: t.authorNickname,
        createdAt: t.posts[0]?.createdAt,
      })
    }
  }
  res.json(items)
})

// --- Tip thread ---
app.post('/api/threads/:id/tip', (req, res) => {
  const user = currentUserId ? users[currentUserId] : null
  if (!user) return res.status(401).json({ message: '请先登录' })

  const t = threads[Number(req.params.id)]
  if (!t) return res.status(404).json({ message: '帖子不存在' })
  if (t.authorId === user.id) return res.status(400).json({ message: '不能打赏自己的帖子' })

  const amount = Number(req.body.amount) || 0
  if (amount <= 0) return res.status(400).json({ message: '请输入有效的金币数量' })
  if (user.coins < amount) return res.status(400).json({ message: `金币不足，需要 ${amount} 金币` })

  user.coins -= amount
  const author = users[t.authorId]
  if (author) author.coins += amount

  if (!tipRecords.has(t.id)) tipRecords.set(t.id, [])
  tipRecords.get(t.id).push({
    userId: user.id,
    nickname: user.nickname,
    amount,
    createdAt: new Date().toISOString(),
  })

  // Notify author
  addNotification(t.authorId, {
    type: 'tip',
    threadId: t.id,
    threadTitle: t.title,
    fromNickname: user.nickname,
    content: `打赏了 ${amount} 金币`,
  })

  res.json({ message: `打赏成功，送出 ${amount} 金币`, coins: user.coins })
})

// --- Hot threads ---
app.get('/api/hot', (req, res) => {
  const period = req.query.period || 'day'
  res.json(hotPools[period] || [])
})

// --- Global search ---
app.get('/api/search', (req, res) => {
  const q = (req.query.q || '').trim().toLowerCase()
  if (!q) return res.json({ items: [], total: 0 })

  const results = []
  for (const t of Object.values(threads)) {
    if (t.title.toLowerCase().includes(q) || t.posts?.some((p) => p.content.toLowerCase().includes(q))) {
      results.push({
        id: t.id,
        title: t.title,
        type: t.type || 'public',
        forumId: t.forumId,
        forumName: t.forumName,
        authorId: t.authorId,
        authorNickname: t.authorNickname,
        authorLevel: t.authorLevel,
        replyCount: t.replyCount,
        views: t.views,
        likeCount: t.likeCount,
        createdAt: t.posts?.[0]?.createdAt,
        snippet: t.posts?.[0]?.content?.slice(0, 120) || '',
      })
    }
  }

  const page = Number(req.query.page) || 1
  const pageSize = Number(req.query.pageSize) || 20
  const total = results.length
  const items = results.slice((page - 1) * pageSize, page * pageSize)
  res.json({ items, total })
})

// --- Auth: Login ---
app.post('/api/auth/login', (req, res) => {
  const { username, password } = req.body
  const user = Object.values(users).find((u) => u.username === username && u.password === password)
  if (!user) return res.status(401).json({ message: '用户名或密码错误' })

  const token = generateToken()
  tokens.set(token, user.id)
  currentToken = token
  currentUserId = user.id
  refreshLevel(user)
  res.json({ token, user: userPublic(user) })
})

// --- Auth: Register ---
app.post('/api/auth/register', (req, res) => {
  const { username, password, nickname } = req.body
  if (Object.values(users).some((u) => u.username === username)) {
    return res.status(409).json({ message: '用户名已存在' })
  }
  const maxId = Math.max(...Object.keys(users).map(Number))
  const newUser = {
    id: maxId + 1, username, nickname: nickname || username,
    password, points: 10, coins: 10, level: 1, levelName: '新手',
    createdAt: new Date().toISOString(),
    nextLevelPoints: 50,
    signInRecords: [new Date().toISOString().slice(0, 10)],
    role: 'user',
  }
  users[newUser.id] = newUser
  refreshLevel(newUser)

  const token = generateToken()
  tokens.set(token, newUser.id)
  currentToken = token
  currentUserId = newUser.id

  res.json({ token, user: userPublic(newUser) })
})

// --- Get current user ---
app.get('/api/me', (req, res) => {
  const auth = req.headers.authorization
  if (!auth || !auth.startsWith('Bearer ')) return res.status(401).json({ message: '未登录' })
  const token = auth.slice(7)
  const uid = tokens.get(token)
  if (!uid) return res.status(401).json({ message: '登录已过期' })

  // Sync global state
  currentToken = token
  currentUserId = uid

  const user = users[uid]
  if (!user) return res.status(401).json({ message: '用户不存在' })
  refreshLevel(user)
  res.json(userPublic(user))
})

// --- Update settings ---
app.put('/api/me/settings', (req, res) => {
  const auth = req.headers.authorization
  if (!auth || !auth.startsWith('Bearer ')) return res.status(401).json({ message: '未登录' })
  const token = auth.slice(7)
  const uid = tokens.get(token)
  if (!uid) return res.status(401).json({ message: '登录已过期' })

  const user = users[uid]
  if (!user) return res.status(401).json({ message: '用户不存在' })

  const { nickname, password, avatar } = req.body
  if (nickname !== undefined) user.nickname = nickname
  if (password !== undefined && password) user.password = password
  if (avatar !== undefined) user.avatar = avatar

  refreshLevel(user)
  res.json(userPublic(user))
})

// --- Daily sign-in milestones ---
const SIGN_IN_MILESTONES = [
  { days: 7,  pointsBonus: 5,  coinsBonus: 0,  label: '连续 7 天' },
  { days: 14, pointsBonus: 10, coinsBonus: 2,  label: '连续 14 天' },
  { days: 21, pointsBonus: 15, coinsBonus: 3,  label: '连续 21 天' },
  { days: 30, pointsBonus: 30, coinsBonus: 5,  label: '连续 30 天（满贯）' },
]

const MILESTONE_BADGES = {
  30: '🏅 签到满贯',
}

// --- GET sign-in status ---
app.get('/api/me/sign-in-status', (req, res) => {
  const auth = req.headers.authorization
  if (!auth || !auth.startsWith('Bearer ')) return res.status(401).json({ message: '未登录' })
  const token = auth.slice(7)
  const uid = tokens.get(token)
  if (!uid) return res.status(401).json({ message: '登录已过期' })

  const user = users[uid]
  const today = new Date().toISOString().slice(0, 10)
  const records = user.signInRecords || []
  const todaySignedIn = records.includes(today)
  const consecutiveDays = calcConsecutiveDays(records)

  // This month's sign-in dates
  const now = new Date()
  const year = now.getFullYear()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const thisMonth = records.filter((d) => d.startsWith(`${year}-${month}`))

  // Next milestone
  let nextMilestone = null
  for (const m of SIGN_IN_MILESTONES) {
    if (consecutiveDays < m.days) {
      nextMilestone = { ...m, daysLeft: m.days - consecutiveDays }
      break
    }
  }

  // Rewards for today
  const multiplier = user.level >= 5 ? 2 : 1
  const basePoints = 10 * multiplier
  const baseCoins = 2 * multiplier

  res.json({
    todaySignedIn,
    consecutiveDays,
    totalDays: records.length,
    thisMonth,
    todayRewards: { points: basePoints, coins: baseCoins },
    nextMilestone,
  })
})

// --- POST daily sign-in ---
app.post('/api/me/sign-in', (req, res) => {
  const auth = req.headers.authorization
  if (!auth || !auth.startsWith('Bearer ')) return res.status(401).json({ message: '未登录' })
  const token = auth.slice(7)
  const uid = tokens.get(token)
  if (!uid) return res.status(401).json({ message: '登录已过期' })

  const user = users[uid]
  const today = new Date().toISOString().slice(0, 10)
  if ((user.signInRecords || []).includes(today)) {
    return res.status(400).json({ message: '今日已签到，明天再来吧' })
  }

  // Level 5+ gets double
  const multiplier = user.level >= 5 ? 2 : 1
  const pointsAwarded = 10 * multiplier
  const coinsAwarded = 2 * multiplier

  user.points += pointsAwarded
  user.coins += coinsAwarded

  // Record sign-in
  if (!user.signInRecords) user.signInRecords = []
  user.signInRecords.push(today)
  user.signInRecords.sort()

  const consecutiveDays = calcConsecutiveDays(user.signInRecords)

  // Check milestones (using consecutive days AFTER this sign-in)
  let milestoneBonus = null
  for (const m of SIGN_IN_MILESTONES) {
    if (consecutiveDays === m.days) {
      milestoneBonus = m
      break
    }
  }
  if (milestoneBonus) {
    user.points += milestoneBonus.pointsBonus
    user.coins += milestoneBonus.coinsBonus
  }

  refreshLevel(user)

  res.json({
    pointsAwarded,
    coinsAwarded,
    consecutiveDays,
    totalDays: user.signInRecords.length,
    milestoneBonus,
    badge: MILESTONE_BADGES[consecutiveDays] || null,
    user: userPublic(user),
  })
})

// --- User profile ---
app.get('/api/users/:id', (req, res) => {
  const user = users[Number(req.params.id)]
  if (!user) return res.status(404).json({ message: '用户不存在' })
  refreshLevel(user)
  res.json(userPublic(user))
})

// --- Purchase history ---
app.get('/api/users/:id/purchases', (req, res) => {
  const uid = Number(req.params.id)
  res.json(purchaseHistory.get(uid) || [])
})

// --- User activity ---
app.get('/api/users/:id/activity', (req, res) => {
  const uid = Number(req.params.id)
  const activity = []

  for (const t of Object.values(threads)) {
    for (const p of t.posts) {
      if (p.author?.id !== uid) continue
      activity.push({
        type: p.floor === 1 ? 'thread' : 'reply',
        threadId: t.id,
        threadTitle: t.title,
        forumName: t.forumName,
        content: p.content?.slice(0, 200),
        createdAt: p.createdAt,
      })
    }
  }

  activity.sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))
  res.json(activity)
})

// --- Notifications ---
app.get('/api/me/notifications', (req, res) => {
  const auth = req.headers.authorization
  if (!auth || !auth.startsWith('Bearer ')) return res.status(401).json({ message: '未登录' })
  const token = auth.slice(7)
  const uid = tokens.get(token)
  if (!uid) return res.status(401).json({ message: '登录已过期' })

  res.json(userNotifications.get(uid) || [])
})

// --- Mark notification read ---
app.put('/api/me/notifications/:id/read', (req, res) => {
  const auth = req.headers.authorization
  if (!auth || !auth.startsWith('Bearer ')) return res.status(401).json({ message: '未登录' })
  const token = auth.slice(7)
  const uid = tokens.get(token)
  if (!uid) return res.status(401).json({ message: '登录已过期' })

  const list = userNotifications.get(uid)
  if (!list) return res.status(404).json({ message: '通知不存在' })

  const nid = Number(req.params.id)
  const notif = list.find((n) => n.id === nid)
  if (!notif) return res.status(404).json({ message: '通知不存在' })

  notif.read = true
  res.json({ message: '已标记为已读' })
})

/* ================================================================
   Admin API (level >= 8 required)
   ================================================================ */
function requireAdmin(req) {
  const auth = req.headers.authorization
  if (!auth || !auth.startsWith('Bearer ')) return { error: 401, message: '未登录' }
  const token = auth.slice(7)
  const uid = tokens.get(token)
  if (!uid) return { error: 401, message: '登录已过期' }
  const user = users[uid]
  if (!user || user.role !== 'super_admin') return { error: 403, message: '权限不足' }
  return { user }
}

// --- Dashboard stats ---
app.get('/api/admin/stats', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  const allThreads = Object.values(threads)
  const totalPosts = allThreads.reduce((sum, t) => sum + t.posts.length, 0)
  const totalUsers = Object.keys(users).length
  const totalForums = categories.reduce((sum, c) => sum + c.forums.length, 0)

  // Today's sign-ins
  const today = new Date().toISOString().slice(0, 10)
  let todaySignIns = 0
  for (const u of Object.values(users)) {
    if (u.signInRecords?.includes(today)) todaySignIns++
  }

  // Recent registrations
  const recentUsers = Object.values(users)
    .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))
    .slice(0, 5)
    .map((u) => ({ id: u.id, username: u.username, nickname: u.nickname, level: u.level, levelName: u.levelName, createdAt: u.createdAt }))

  // Hot threads
  const hotThreads = allThreads
    .sort((a, b) => b.views - a.views)
    .slice(0, 5)
    .map((t) => ({ id: t.id, title: t.title, forumName: t.forumName, views: t.views, replyCount: t.replyCount }))

  // Weekly activity (sign-ins per day for last 7 days)
  const weeklyActivity = []
  const dailyRegistrations = []
  const dailyActive = []
  for (let i = 6; i >= 0; i--) {
    const d = new Date()
    d.setDate(d.getDate() - i)
    const dateStr = d.toISOString().slice(0, 10)
    const dayLabel = d.toLocaleDateString('zh-CN', { weekday: 'short' })

    // Sign-ins
    let signInCount = 0
    // Registrations
    let regCount = 0
    // Active (users who signed in on this date)
    let activeCount = 0
    for (const u of Object.values(users)) {
      if (u.signInRecords?.includes(dateStr)) {
        signInCount++
        activeCount++
      }
      // Check if user registered on this date
      if (u.createdAt && u.createdAt.slice(0, 10) === dateStr) regCount++
    }

    weeklyActivity.push({ date: dateStr, day: dayLabel, count: signInCount })
    dailyRegistrations.push({ date: dateStr, day: dayLabel, count: regCount })
    dailyActive.push({ date: dateStr, day: dayLabel, count: activeCount })
  }

  const todayRegistrations = dailyRegistrations.find(d => d.date === today)?.count || 0
  const todayActive = dailyActive.find(d => d.date === today)?.count || 0

  res.json({
    totalUsers,
    totalThreads: allThreads.length,
    totalPosts,
    totalForums,
    todaySignIns,
    todayRegistrations,
    todayActive,
    recentUsers,
    hotThreads,
    weeklyActivity,
    dailyRegistrations,
    dailyActive,
  })
})

// --- User list (paginated) ---
app.get('/api/admin/users', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  const page = Number(req.query.page) || 1
  const pageSize = Number(req.query.pageSize) || 20
  const search = (req.query.search || '').toLowerCase()

  let list = Object.values(users)
  if (search) {
    list = list.filter((u) => u.username.includes(search) || u.nickname.includes(search))
  }

  const total = list.length
  const start = (page - 1) * pageSize

  // Pre-compute user thread/reply stats
  const threadCountByUser = {}
  const replyCountByUser = {}
  for (const t of Object.values(threads)) {
    for (const p of t.posts) {
      const uid = p.author?.id
      if (!uid) continue
      if (p.floor === 1) {
        threadCountByUser[uid] = (threadCountByUser[uid] || 0) + 1
      } else {
        replyCountByUser[uid] = (replyCountByUser[uid] || 0) + 1
      }
    }
  }

  const items = list.slice(start, start + pageSize).map((u) => ({
    id: u.id, username: u.username, nickname: u.nickname,
    level: u.level, levelName: u.levelName,
    points: u.points, coins: u.coins,
    consecutiveDays: calcConsecutiveDays(u.signInRecords),
    totalDays: u.signInRecords ? u.signInRecords.length : 0,
    threadCount: threadCountByUser[u.id] || 0,
    replyCount: replyCountByUser[u.id] || 0,
    createdAt: u.createdAt,
    role: u.role || 'user',
  }))

  res.json({ items, total, page, pageSize })
})

// --- Update user ---
app.put('/api/admin/users/:id', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  const user = users[Number(req.params.id)]
  if (!user) return res.status(404).json({ message: '用户不存在' })

  const { points, coins, nickname, password } = req.body
  if (points !== undefined) user.points = points
  if (coins !== undefined) user.coins = coins
  if (nickname !== undefined) user.nickname = nickname
  if (password !== undefined) user.password = password
  refreshLevel(user)

  res.json({ message: '更新成功', user: { id: user.id, username: user.username, nickname: user.nickname, level: user.level, levelName: user.levelName, points: user.points, coins: user.coins } })
})

// --- Delete user ---
app.delete('/api/admin/users/:id', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  const id = Number(req.params.id)
  if (id === a.user.id) return res.status(400).json({ message: '不能删除自己' })
  if (!users[id]) return res.status(404).json({ message: '用户不存在' })
  delete users[id]
  res.json({ message: '已删除' })
})

// --- Update user role ---
app.put('/api/admin/users/:id/role', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  const target = users[Number(req.params.id)]
  if (!target) return res.status(404).json({ message: '用户不存在' })
  if (target.id === a.user.id) return res.status(400).json({ message: '不能修改自己的角色' })

  const { role } = req.body
  if (role !== 'super_admin' && role !== 'user') return res.status(400).json({ message: '无效的角色' })

  target.role = role
  res.json({ message: '角色更新成功', user: { id: target.id, username: target.username, role: target.role } })
})

// --- All threads list ---
app.get('/api/admin/threads', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  const page = Number(req.query.page) || 1
  const pageSize = Number(req.query.pageSize) || 20
  const search = (req.query.search || '').toLowerCase().trim()

  let all = Object.values(threads)
  if (search) {
    all = all.filter((t) =>
      t.title.toLowerCase().includes(search) ||
      (t.authorNickname || '').toLowerCase().includes(search) ||
      (t.forumName || '').toLowerCase().includes(search)
    )
  }

  const total = all.length
  const items = all.slice((page - 1) * pageSize, page * pageSize).map((t) => ({
    id: t.id, title: t.title, forumName: t.forumName,
    authorNickname: t.authorNickname, authorLevel: t.authorLevel,
    replyCount: t.replyCount, views: t.views, likeCount: t.likeCount,
    createdAt: t.posts[0]?.createdAt,
  }))

  res.json({ items, total, page, pageSize })
})

// --- Delete thread ---
app.delete('/api/admin/threads/:id', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  const id = Number(req.params.id)
  if (!threads[id]) return res.status(404).json({ message: '帖子不存在' })
  // Decrement forum counts
  const t = threads[id]
  for (const cat of categories) {
    const forum = cat.forums.find((f) => f.id === t.forumId)
    if (forum) {
      forum.threadCount = Math.max(0, forum.threadCount - 1)
      forum.postCount = Math.max(0, forum.postCount - t.posts.length)
      break
    }
  }
  delete threads[id]
  res.json({ message: '已删除' })
})

// --- Categories CRUD ---
app.get('/api/admin/categories', (_req, res) => {
  res.json(categories)
})

app.post('/api/admin/categories', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  const maxId = categories.length ? Math.max(...categories.map((c) => c.id)) : 0
  const newCat = {
    id: maxId + 1,
    name: req.body.name || '新分类',
    icon: req.body.icon || '📁',
    forums: [],
  }
  categories.push(newCat)
  res.json(newCat)
})

app.put('/api/admin/categories/:id', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  const cat = categories.find((c) => c.id === Number(req.params.id))
  if (!cat) return res.status(404).json({ message: '分类不存在' })
  if (req.body.name) cat.name = req.body.name
  if (req.body.icon) cat.icon = req.body.icon
  res.json(cat)
})

app.delete('/api/admin/categories/:id', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  const idx = categories.findIndex((c) => c.id === Number(req.params.id))
  if (idx === -1) return res.status(404).json({ message: '分类不存在' })
  categories.splice(idx, 1)
  res.json({ message: '已删除' })
})

// --- Forums CRUD ---
app.post('/api/admin/forums', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  const cat = categories.find((c) => c.id === Number(req.body.categoryId))
  if (!cat) return res.status(404).json({ message: '分类不存在' })

  const maxId = Math.max(...categories.flatMap((c) => c.forums.map((f) => f.id)))
  const newForum = {
    id: maxId + 1,
    name: req.body.name || '新版块',
    icon: req.body.icon || '📁',
    description: req.body.description || '',
    threadCount: 0,
    postCount: 0,
    todayThreadCount: 0,
    latestThread: null,
  }
  cat.forums.push(newForum)
  res.json(newForum)
})

app.put('/api/admin/forums/:id', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  let forum = null
  for (const cat of categories) {
    forum = cat.forums.find((f) => f.id === Number(req.params.id))
    if (forum) break
  }
  if (!forum) return res.status(404).json({ message: '版块不存在' })
  if (req.body.name) forum.name = req.body.name
  if (req.body.icon) forum.icon = req.body.icon
  if (req.body.description !== undefined) forum.description = req.body.description
  res.json(forum)
})

app.delete('/api/admin/forums/:id', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  for (const cat of categories) {
    const idx = cat.forums.findIndex((f) => f.id === Number(req.params.id))
    if (idx !== -1) {
      cat.forums.splice(idx, 1)
      return res.json({ message: '已删除' })
    }
  }
  res.status(404).json({ message: '版块不存在' })
})

// --- Sign-in stats ---
app.get('/api/admin/signin/stats', (req, res) => {
  const a = requireAdmin(req)
  if (a.error) return res.status(a.error).json({ message: a.message })

  const today = new Date().toISOString().slice(0, 10)
  let todayCount = 0
  const consecutiveDist = {}
  const totalDaysList = []

  for (const u of Object.values(users)) {
    if (u.signInRecords?.includes(today)) todayCount++
    const cons = calcConsecutiveDays(u.signInRecords)
    const total = u.signInRecords ? u.signInRecords.length : 0
    const bucket = cons >= 30 ? '30+' : cons >= 14 ? '14-29' : cons >= 7 ? '7-13' : cons >= 1 ? '1-6' : '0'
    consecutiveDist[bucket] = (consecutiveDist[bucket] || 0) + 1
    totalDaysList.push({ userId: u.id, username: u.username, nickname: u.nickname, consecutiveDays: cons, totalDays: total })
  }

  // Top sign-in users
  const topUsers = totalDaysList.sort((a, b) => b.totalDays - a.totalDays).slice(0, 10)

  res.json({
    todayCount,
    consecutiveDist,
    topUsers,
  })
})

/* ================================================================
   Start
   ================================================================ */
const PORT = process.env.MOCK_PORT || 5001
app.listen(PORT, () => {
  console.log(`Mock server running on http://localhost:${PORT}`)
  console.log('Test accounts:')
  Object.values(users).forEach((u) => {
    console.log(`  ${u.username} / ${u.password}  (Lv.${u.level} ${u.levelName}, ${u.points} pts)`)
  })
})
