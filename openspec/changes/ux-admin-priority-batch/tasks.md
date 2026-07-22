## 1. 顶栏与导航

- [x] 1.1 将积分/金币/等级进度移入 `AppLayout` 用户菜单，顶栏保留通知/私信/头像
- [x] 1.2 主导航增加「玩法」分组，迁入签到/商城/邀请/转盘
- [x] 1.3 窄屏下验证顶栏不换行、菜单可点

## 2. 用户充值卡与屏蔽

- [x] 2.1 在 `RechargeView` 增加卡密兑换表单并对接 redeem API
- [x] 2.2 在 `SettingsView` 增加屏蔽列表加载与解除（已有，补 toast/样式）
- [x] 2.3 兑换/解除成功使用 toast 提示并刷新状态

## 3. 管理驾驶舱与卡密

- [x] 3.1 扩展 admin stats：昨日对比字段（签到/注册/发帖/回复等）
- [x] 3.2 `DashboardView` 展示同比与待办积压提示
- [x] 3.3 `admin/RechargeView` 增加卡密库存列表（含掩码与状态）

## 4. 样式统一

- [x] 4.1 资料页等残留 `btn-outline-secondary` 改为 `btn-outline-modern` / `btn-forum`
- [ ] 4.2 快速回归：首页、设置、充值、管理驾驶舱、充值管理
