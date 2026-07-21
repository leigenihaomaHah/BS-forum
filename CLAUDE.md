# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在此仓库中工作时提供指导。

## 技术栈

- **后端**: ASP.NET Core 8 Web API, EF Core 8, SQLite (WAL 模式), JWT Bearer 认证, BCrypt, Swagger
- **前端**: Vue 3 (Composition API, `<script setup>` SFC), Vite 8, Pinia 4, Vue Router 4, Axios, Bootstrap 5 (仅 CSS)
- **两端均无 TypeScript、无测试框架、无 linter**

## 命令

### 后端
```sh
cd backend && dotnet run              # 启动 API，地址 localhost:5000
cd backend && dotnet run --launch-profile Production  # 生产配置启动
```
开发环境下 Swagger 地址 `http://localhost:5000/swagger`。

### 前端
```sh
cd frontend && npm run dev       # 开发服务器 :5173，代理 /api -> localhost:5000
cd frontend && npm run build     # 生产构建
cd frontend && npm run preview   # 预览生产构建
```

### IIS 发布
```sh
./publish-iis.bat                     # 构建并发布到默认 IIS 路径
./publish-iis.bat "C:\自定义\路径"
```

## 架构

### 项目布局
```
backend/        # ForumApi - ASP.NET Core 8 Web API
frontend/       # Vue 3 SPA
publish/        # IIS 发布输出
scripts/        # 部署脚本
Images/         # 截图/素材
```

### 后端结构

```
backend/
  Program.cs            # 应用启动：DI、中间件管道、数据库初始化（WAL + 写探针）
  Controllers/          # API 控制器（REST 风格，/api/* 路由）
  Services/             # 业务逻辑层（Controller -> Service）
  Models/               # EF Core 实体类
  Data/
    AppDbContext.cs      # EF Core DbContext，Fluent API 配置
    DbSeeder.cs          # 种子数据 + 数据库迁移（SQLite 下使用 Ensure* 方法）
  Dtos/Dtos.cs          # 所有请求/响应 DTO（C# record），集中在一个文件
  Helpers/              # JwtHelper、VipAccess
```

**Controller-Service 模式**: Controller 解析 HTTP 请求，调用 Service 方法，返回结果。Service 包含所有业务逻辑。两者统一返回 `(result, error)` 元组，`error != null` 表示失败。

**关键 API 路由**: `api/auth`（注册/登录）、`api/me`（资料/设置/签到/通知）、`api/threads`（CRUD/回复/点赞/购买/收藏/打赏）、`api/forums`（分类/帖子列表）、`api/admin`（用户/帖子/版块/横幅/管理操作）、`api/community`（关注/动态/标签/商城/抽奖/举报）、`api/recharge`（VIP 套餐/订单/充值卡）

**数据库**: SQLite，使用 `EnsureCreatedAsync()` + 手写 `CREATE TABLE IF NOT EXISTS` / `ALTER TABLE ADD COLUMN` 方式迁移（非 EF Core Migration）。启用 WAL 模式 + busy timeout 保障并发。

**认证**: JWT Bearer，`RoleClaimType = ClaimTypes.Role`，管理员角色 = "Admin"。`JwtHelper.GetUserId(User)` 从 claims 中提取当前用户 ID（匿名请求返回 null）。

### 前端结构

```
frontend/src/
  main.js               # 引导：初始化 Pinia、Router、获取等级配置、验证 token
  App.vue               # 根组件
  router/index.js       # 路由（懒加载），/admin 受 adminGuard() 保护
  api/http.js           # Axios 实例：baseURL /api，JWT 拦截器，错误规范化
  stores/               # Pinia 状态管理（auth.js、authModal.js）
  views/                # 页面组件
  components/           # 布局、面板、弹窗、通用组件
  config/               # levels.js、forumIcons.js
  utils/                # markdown.js、password.js
  assets/               # forum.css、admin.css、hero.png
```

**认证流程**: token + 用户信息持久化到 localStorage。应用初始化时，如果存在 token 则调用 `fetchMe()` 验证；验证失败则自动登出。Axios 拦截器自动为所有请求添加 `Bearer` token。

**错误处理**: Axios 拦截器提取 `response.data.message`（含 ASP.NET 错误格式降级 + IIS 405 HTML 检测）。视图组件通过局部 `error` ref 展示错误信息。

**样式**: 所有自定义 CSS 集中在 `forum.css`，使用 CSS 自定义属性设计系统（`--ink`、`--accent` 等）。无组件库，全部手写。

## 演示账号

| 用户名 | 密码 | 等级 |
|--------|------|------|
| admin  | admin123 | 管理员（Lv.6） |
| demo   | demo123  | Lv.2（正式会员） |
| newbie | newbie123 | Lv.1（见习会员） |

## 关键约定

- API 错误统一返回 `{ "message": "..." }` 及对应 HTTP 状态码
- 前端 API 调用使用 `try/catch`，通过 `error.message` 显示错误
- 热度公式: `回复数*3 + 浏览数*0.1 + 点赞数*5 + 新鲜度加成`
- 等级规则: Lv.1=0、Lv.2=50、Lv.3=200、Lv.4=800、Lv.5=2000、Lv.6=5000 积分
- 所有 DTO 使用 C# record 类型（非 class）
- `/api/threads/{id}` 为帖子相关路由；`/api/admin` 需要 Admin 角色
- `JwtHelper.GetUserId(User)` 从 claims 中提取当前用户（匿名返回 null）
