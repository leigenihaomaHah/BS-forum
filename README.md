# BS 综合社区（.NET Core + Vue3）

仿经典 BBS 排版的前后端分离论坛：分区两列子版、日/周/月热帖、积分 / 金币 / 会员等级。

**后端以 ASP.NET Core 为准**；`frontend/server` 仅为历史 mock，日常开发请使用 .NET API。

## 技术栈

- 后端：ASP.NET Core 8 Web API、EF Core、SQLite、JWT、BCrypt
- 前端：Vue 3、Vite、Pinia、Vue Router、Bootstrap 5

## 快速启动

### 1. 后端

```bash
cd backend
dotnet run
```

API 默认：`http://localhost:5000`  
Swagger：`http://localhost:5000/swagger`

首次启动会自动建库并写入种子数据。

### 2. 前端

```bash
cd frontend
npm install
npm run dev
```

访问：`http://localhost:5173`（已代理 `/api` 到后端）

## 演示账号

| 用户名 | 密码 | 说明 |
|--------|------|------|
| admin | admin123 | 站长 / 管理员 |
| demo | demo123 | 正式会员，可发帖 |
| newbie | newbie123 | 见习会员，需攒积分到 50 才能发帖 |

## 已实现能力

- 分区 / 版块 / 帖子 / 回复 / 点赞
- 注册登录、账号设置（昵称 / 密码 / 头像）、每日签到与里程碑
- 搜索、通知（回帖提醒）、用户动态
- 热帖（日 / 周 / 月）、六级等级体系（后端 `LevelRules`）
- 管理后台：统计、用户、角色、版块、帖子、签到统计、等级只读

## 规则摘要

- **积分**：发帖 +10（日限 5）、回帖 +2（日限 20）、签到 +5 起、被赞 +1
- **金币**：注册赠 10；签到额外给金币
- **等级**：由积分自动换算（见习→正式→活跃→资深→金牌→元老）
- **发帖门槛**：Lv.2（积分 ≥ 50）
- **热度**：`replies*3 + views*0.1 + likes*5 + freshBonus`

## 目录

```
backend/   # ForumApi（权威 API）
frontend/  # Vue 应用
```

## IIS 一键发布

开发机双击根目录 `publish-iis.bat`（详见 `IIS部署说明.txt`）。

```bat
publish-iis.bat
publish-iis.bat "C:\inetpub\wwwroot\BSForum"
```

服务器需安装 [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/permalink/dotnetcore-current-windows-runtime-bundle-installer)，应用程序池选「无托管代码」，并给站点目录写权限（SQLite）。
