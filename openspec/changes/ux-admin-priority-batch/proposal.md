## 为什么

论坛功能已较完整，但用户侧顶栏过挤、导航偏玩法、部分关键路径（卡密兑换、屏蔽管理）缺失；管理侧能看数字却难做决策（缺同比、预警、卡密库存）。先落地「立刻做」一批，快速提升日常体验与运营可控性。

## 变更内容

- 顶栏瘦身：积分/金币/等级进度收入头像菜单；顶栏保留品牌、搜索、发帖入口、通知、私信、头像
- 主导航重排：一级突出首页/社区主线；签到/商城/邀请/转盘归入「玩法」
- 用户充值卡自助兑换入口（对接已有 redeem API）
- 设置页增加屏蔽用户管理（查看/解除）
- 残留 Bootstrap 描边按钮统一为论坛圆角样式
- 管理驾驶舱：关键指标同比（较昨日）、待办积压提示、活跃口径说明/修正
- 管理充值：卡密库存列表（对接已有 cards API）

## 功能 (Capabilities)

### 新增功能

- `header-nav-ia`: 顶栏与主导航信息架构调整
- `card-redeem`: 用户侧充值卡兑换
- `block-management`: 用户屏蔽列表管理
- `admin-dashboard-insights`: 管理驾驶舱同比与预警增强
- `admin-card-inventory`: 管理端卡密库存浏览

### 修改功能

- （无既有 openspec 规范；本仓库以代码为准）

## 影响

- 前端：`AppLayout.vue`、`RechargeView.vue`、`SettingsView.vue`、`admin/DashboardView.vue`、`admin/RechargeView.vue`、相关 CSS
- 后端：可能扩展 `GET /admin/stats` 增加昨日对比字段；卡密列表若已存在则仅接 UI
- 无破坏性 API 变更预期（仅增量字段）
