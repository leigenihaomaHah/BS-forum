# -*- coding: utf-8 -*-
"""一次性灌入演示帖：每版块补足 20–30 帖，每帖 10–30 条混合回帖。"""
from __future__ import annotations

import json
import random
import sqlite3
from datetime import datetime, timedelta
from pathlib import Path

DB = Path(r"D:\BS\BS\backend\forum.db")
SETTING_KEY = "demo_bulk_content_v1"
TINY_PNG = (
    "data:image/png;base64,"
    "iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAFUlEQVR42mNk+M9Qz0AEYBxVSF+FABJADveWkH6oAAAAAElFTkSuQmCC"
)

EXTRA_USERS = [
    ("seed_xiaoming", "小明同学", 3, 280),
    ("seed_xiaohong", "小红酱", 3, 260),
    ("seed_laowang", "老王爱逛", 4, 900),
    ("seed_ahuang", "阿杰数码", 4, 850),
    ("seed_lili", "丽丽探店", 3, 320),
    ("seed_dazhuang", "大壮健身", 3, 240),
    ("seed_momo", "默默吃瓜", 2, 90),
    ("seed_kiki", "Kiki周末", 2, 120),
    ("seed_feifei", "飞飞同城", 3, 300),
    ("seed_bobo", "波波影迷", 3, 350),
    ("seed_nana", "娜娜吃货", 2, 150),
    ("seed_tao", "涛哥分享", 4, 780),
]

TITLES = {
    "灌水": [
        "今天心情怎么样，来聊聊呗", "有没有人在看这个综艺", "睡前闲聊，明天又要上班了",
        "分享一张今天拍的天空", "突然想养只猫，靠谱吗", "周末有什么计划",
        "发现一个超好玩的小游戏", "最近有没有好听的歌推荐", "中午吃什么纠结症犯了",
        "聊聊最近开心的小事", "夜猫子集合！还没睡的扣 1", "下雨天就适合窝在家里",
        "有没有人一起云撸猫", "吐槽一下今天遇到的奇葩事", "求推荐解压的方式",
        "打卡：今天也要开心呀", "闲来无事，有人聊八卦吗", "分享一个冷知识",
        "加班狗报到，求安慰", "周末去哪玩比较合适", "突然想学一门新技能",
        "大家一般几点睡觉", "有没有好看的短视频账号", "今天空气真好，适合散步",
        "聊聊你最喜欢的季节", "求推荐一款好用的耳机", "有没有人喜欢下雨天",
        "深夜食堂：你在吃什么", "分享一下桌面布置", "今天踩了个小幸运",
    ],
    "新人": [
        "新人报到，请多关照！", "刚注册，怎么快速升级", "自我介绍贴：我来自…",
        "新人提问：发帖有什么规矩", "报到打卡 Day1", "希望能交到朋友",
        "第一次发帖有点紧张", "求各位前辈带带", "新人求推荐常逛版块",
        "报到：热爱生活热爱分享", "请问积分怎么攒比较快", "新人福利有吗",
        "打个招呼，我是…", "想了解论坛的玩法", "报到帖，求回一个",
        "新手上路，勿喷", "来报到啦～", "希望社区越来越热闹",
        "新人求关注求互动", "第一次逛同城区", "请问怎么改头像",
        "报到并求签到攻略", "新人想问版主在吗", "自我介绍：兴趣是…",
        "刚从别的论坛过来", "报到：希望多交流", "新人求组队玩",
        "第一次回帖成功！", "报到打卡求个精华攻略", "新人也能中奖吗",
    ],
    "求助": [
        "求推荐靠谱的笔记软件", "电脑开机慢怎么排查", "求问路由器怎么设置",
        "手机发热严重有解吗", "求一份简历模板", "英语听力怎么练",
        "求推荐入门相机", "搬家公司靠谱吗", "求问医保怎么报销",
        "打印机连不上 WiFi", "求推荐防蓝光眼镜", "睡眠不好有啥办法",
        "求问租房注意事项", "Excel 透视表怎么做", "求推荐儿童绘本",
        "耳机一边没声咋修", "求问签证材料清单", "体重反弹怎么破",
        "求推荐性价比显示器", "论文格式怎么调", "求问驾照科目二技巧",
        "空调不制冷怎么办", "求推荐好用的待办 App", "宠物走失求扩散经验",
        "求问基金定投入门", "键盘进水急救步骤", "求推荐通勤背包",
        "照片怎么批量压缩", "求问信用卡提额技巧", "求一份旅行行李清单",
    ],
    "资源": [
        "整理了一份学习资料索引", "好用的在线工具合集", "开源项目推荐几枚",
        "免费字体下载指路", "设计素材站盘点", "效率软件白名单",
        "电子书阅读器对比", "壁纸分享（每周更新）", "播客订阅清单",
        "地图开源数据入口", "公开课精选链接", "图标库推荐",
        "写作参考书单", "摄影 RAW 处理流程", "配色方案收藏夹",
        "模板网站合集", "插件推荐：浏览器篇", "剪辑转场素材包",
        "字体搭配案例", "UI 组件库对比", "数据集公开站点",
        "文档写作规范模板", "思维导图工具评测", "云同步方案对比",
        "冷门但好用的小工具", "学习路径图分享", "资源失效请留言",
        "本周资源更新汇总", "版权友好素材站", "自用书签导出分享",
    ],
    "数码": [
        "2026 轻薄本怎么选", "手机拍照夜景对比", "机械键盘轴体推荐",
        "显示器 2K 还是 4K", "降噪耳机横评笔记", "NAS 入门避坑",
        "显卡温度多少算正常", "平板能替代笔记本吗", "路由器 Mesh 组网体验",
        "移动硬盘选购建议", "耳机孔消失后的选择", "剪辑主机配置单",
        "蓝牙音箱低音表现", "智能手表续航实测", "扩展坞兼容性问题",
        "SSD 掉盘急救经验", "手机壳会影响信号吗", "键帽 PBT 与 ABS",
        "显示器支架稳不稳", "摄像头隐私盖必要吗", "充电协议混乱吐槽",
        "二手显卡捡漏指南", "麦克风降噪设置", "电脑清灰步骤",
        "Type-C 线材怎么挑", "云手机体验如何", "折叠屏耐用吗",
        "游戏本散热改造", "外接 SSD 速度测试", "桌面理线分享",
    ],
    "影音": [
        "最近在追的剧，有同好吗", "冷门好片求安利", "游戏本月折扣汇总",
        "耳机听感向歌单", "这部电影值得二刷吗", "独立游戏推荐",
        "综艺名场面回顾", "演唱会门票求组队", "动画新番讨论",
        "音质党入坑设备", "剧透慎入：结局讨论", "主机还是 PC",
        "纪录片推荐几部", "短剧真的上头", "音乐节体验分享",
        "字幕组翻译吐槽", "重制版是否值得买", "播客通勤清单",
        "影院观感 vs 家里", "角色曲循环中", "通关感想无剧透",
        "演唱会周边晒单", "怀旧游戏合集", "配乐太绝了",
        "演员演技在线讨论", "片头曲循环停不下来", "二周目是否必要",
        "弹幕文化聊聊", "蓝光碟还有人收吗", "本周观影打卡",
    ],
    "运动": [
        "晨跑打卡第 N 天", "健身房器械怎么排", "羽毛球搭子招募",
        "减脂平台期怎么办", "骑行路线分享", "拉伸动作求纠正",
        "马拉松配速建议", "居家徒手训练计划", "游泳换气技巧",
        "运动手表配速准吗", "护膝有必要吗", "增肌饮食怎么搭",
        "夜跑安全注意点", "瑜伽入门动作", "爬山装备清单",
        "跳绳燃脂真的香", "球鞋缓震对比", "力量训练打卡",
        "拉伸 vs 泡沫轴", "运动后补充什么", "膝盖不适怎么调整",
        "户外徒步避坑", "游泳馆水质吐槽", "骑行码表推荐",
        "HIIT 十分钟够吗", "运动蓝牙耳机", "拉伸后更酸？",
        "团课体验分享", "跑步配速手表设置", "本周运动小结",
    ],
    "美食": [
        "发现一家超好吃的面馆", "周末探店打卡", "自制早餐合集",
        "火锅底料哪个香", "甜品店排队值得吗", "咖啡拉花练习",
        "家常菜失败翻车记录", "夜市小吃安利", "素食餐厅推荐",
        "烘焙新手提问", "烧烤调料配方", "凉皮配方求教",
        "人均 50 吃啥好", "外卖避雷清单", "汤底鲜到眉毛",
        "本地隐藏小馆", "奶茶糖度怎么点", "海鲜怎么处理",
        "早餐车时间表", "甜点低糖替代", "酱料自制分享",
        "探店：环境一般味道绝", "路边摊安全感", "冰箱剩菜改造",
        "咖啡馆适合办公吗", "拉面汤白不白", "烧烤炭火 vs 电烤",
        "周末早市买菜", "甜品拍照光线", "本周吃了啥",
    ],
    "公告": [
        "【公告】社区礼仪与发帖规范", "【公告】积分与等级说明更新",
        "【公告】违规处理公示说明", "【通知】维护窗口预告",
        "【公告】新人引导手册", "【通知】活动奖励发放说明",
        "【公告】版块重组说明", "【通知】举报通道使用指南",
        "【公告】账号安全提醒", "【通知】会员权益调整说明",
        "【公告】广告投放规范", "【通知】签到规则微调",
        "【公告】图片上传大小限制", "【通知】私信功能使用提醒",
        "【公告】精华帖评选标准", "【通知】禁言申诉流程",
        "【公告】同城版块管理细则", "【通知】资源分享版权提醒",
        "【公告】节日活动预告", "【通知】系统升级完成",
        "【公告】版主招募说明", "【通知】数据统计口径说明",
        "【公告】外链发布规范", "【通知】昵称审核规则",
        "【公告】抽奖活动规则", "【通知】充值与卡密说明",
        "【公告】内容审核时效", "【通知】备份与恢复说明",
        "【公告】无障碍访问改进", "【通知】意见收集通道",
    ],
    "建议": [
        "希望增加夜间模式开关", "搜索能不能按版块过滤", "建议优化移动端发帖",
        "能不能加草稿箱提醒", "希望关注动态分页更清晰", "建议增加表情包",
        "通知分类希望更细", "求一个楼中楼折叠优化", "建议热门算法透明一点",
        "希望同城能按距离排序", "建议增加话题聚合页", "私信已读回执可选",
        "希望签到补签功能", "建议版主工具更集中", "图片压缩质量可配置",
        "希望增加投票帖模板", "建议标签可关注", "求一个浏览历史清理",
        "希望商城增加筛选", "建议举报理由自定义", "移动端底栏再精简",
        "希望增加匿名提问区", "建议精华自动归档", "发帖预览再强化",
        "希望等级特权更清晰", "建议增加周报邮件", "搜索结果高亮关键词",
        "希望增加版块订阅角标", "建议评论支持 GIF", "个人主页布局建议",
    ],
}

CITY_KEYS = {
    "北京": ("北京", "帝都", "地铁", "环球影城", "烤鸭"),
    "上海": ("上海", "魔都", "外滩", "迪士尼", "生煎"),
    "广州": ("广州", "羊城", "早茶", "珠江夜游", "煲仔饭"),
    "深圳": ("深圳", "鹏城", "科技园", "海边", "肠粉"),
    "杭州": ("杭州", "天堂", "西湖", "龙井", "东坡肉"),
    "成都": ("成都", "蓉城", "火锅", "宽窄巷子", "串串"),
}

CONTENTS = [
    "想和大家聊聊这个话题，欢迎补充经验。最近自己踩了不少坑，整理如下，仅供参考。",
    "先说结论：因人而异，但有几条通用建议。下面分开说优缺点，欢迎拍砖。",
    "周末闲下来整理的笔记，可能有疏漏，发现错漏请留言，我会更新主楼。",
    "不是广告，纯分享使用感受。预算和场景不同，结果可能不一样，请结合自身情况。",
    "抛个问题给各位老司机：如果是你会怎么选？评论区见真章。",
    "图文并茂更好，但今天先文字版。有需要我再补图。",
    "最近状态一般，写点东西记录一下。也当抛砖引玉。",
    "这份清单是我自己在用的，欢迎大家继续往下盖楼补充。",
]

REPLY_NORMAL = [
    "说得很有道理，学到了。", "同感，我也遇到过类似情况。", "马克一下，回头细看。",
    "哈哈这楼有意思。", "支持楼主，继续更新！", "求后续，别坑啊。",
    "这个角度我没想到。", "已收藏，感谢分享。", "路过顶一下。",
    "写得好细，点赞。", "有链接或图就更完美了。", "我也投一票这个方案。",
    "本地人表示认可。", "新手表示非常有用。", "先点个赞再慢慢看。",
    "周末可以试试这个。", "和我上周的经历一模一样。", "建议再补一点对比。",
    "哈哈哈笑死，太真实了。", "稳，这个可以冲。",
]

REPLY_QUOTE = [
    "回复楼上：完全同意你的看法。", "引用一下，这个细节很关键。", "接着你的话说，我补充一点。",
    "对你提到的点很好奇，能展开吗？", "你说的那个我试过，确实有效。", "顺着这个思路，我再问一句。",
    "楼上一语中的。", "感谢提醒，差点忽略了。", "你这个例子举得很好。",
    "不同意全部，但同意这一段。",
]

REPLY_IMAGE = [
    "附图说明一下～", "上图，大家看看像不像。", "随手拍了一张参考。",
    "对比图来了。", "现场实拍，供参考。", "菜单/界面截图。",
    "效果如图。", "位置大概是这样。",
]


def city_titles(city, nick, a, b, c):
    return [
        f"{city}周末好去处求推荐", f"{nick}通勤吐槽大会", f"{a}沿线美食分享",
        f"本地人私藏：{b}", f"{c}哪家更正宗", f"{city}租房避坑经验",
        f"{city}天气突然变脸", f"同城周末组局：{a}", f"{city}咖啡店白名单",
        f"夜景拍照点：{b}", f"{city}早高峰生存指南", f"本地超市打折情报",
        f"{city}亲子友好场馆", f"求问{city}驾照换证", f"{c}探店记录",
        f"{city}跑步路线分享", f"同城二手交易注意", f"{a}附近午餐",
        f"{city}博物馆预约攻略", f"周末市集打卡", f"{b}排队还值得吗",
        f"{city}外卖高峰避雷", f"同城宠物友好店", f"{city}雨天交通提醒",
        f"本地花市时光", f"{c}分量测评", f"{city}摄影约拍地点",
        f"同城羽毛球搭子", f"{city}新开商场初探", f"本周{city}见闻",
    ]


def picsum(seed: int) -> str:
    return f"https://picsum.photos/seed/bs{abs(seed) % 100000}/640/420"


def fmt(dt: datetime) -> str:
    # Match EF Core SQLite style with fractional seconds
    return dt.strftime("%Y-%m-%d %H:%M:%S.") + f"{dt.microsecond // 100:07d}"


def pick_titles(forum_name: str, count: int) -> list[str]:
    pool = None
    for key, titles in TITLES.items():
        if key in forum_name:
            pool = list(titles)
            break
    if pool is None:
        for key, args in CITY_KEYS.items():
            if key in forum_name:
                pool = city_titles(*args)
                break
    if pool is None:
        pool = list(TITLES["灌水"])
    random.shuffle(pool)
    out = pool[: min(count, len(pool))]
    while len(out) < count:
        out.append(f"{forum_name}日常分享 #{len(out)+1} · {datetime.utcnow():%m%d}-{random.randint(100,999)}")
    return out


def main():
    random.seed()
    conn = sqlite3.connect(str(DB), timeout=60)
    conn.execute("PRAGMA busy_timeout=60000")
    conn.execute("PRAGMA journal_mode=WAL")
    cur = conn.cursor()

    flag = cur.execute("SELECT Value FROM SiteSettings WHERE Key=?", (SETTING_KEY,)).fetchone()
    if flag and flag[0] == "1":
        print("Already seeded (demo_bulk_content_v1=1). Delete setting to re-run.")
        # still print stats
        print_stats(cur)
        return

    demo_hash = cur.execute("SELECT PasswordHash FROM Users WHERE Username='demo'").fetchone()
    if not demo_hash:
        raise SystemExit("demo user missing")
    demo_hash = demo_hash[0]

    # ensure columns exist for soft-delete etc.
    cols = {r[1] for r in cur.execute("PRAGMA table_info('Posts')")}
    user_cols = {r[1] for r in cur.execute("PRAGMA table_info('Users')")}

    for username, nick, level, points in EXTRA_USERS:
        exists = cur.execute("SELECT Id FROM Users WHERE Username=?", (username,)).fetchone()
        if exists:
            continue
        now = fmt(datetime.utcnow() - timedelta(days=random.randint(30, 200)))
        invite = f"SEED{username.upper()}"
        cur.execute(
            """INSERT INTO Users
               (Username, PasswordHash, Nickname, Points, Coins, Level,
                ConsecutiveSignInDays, CreatedAt, IsAdmin, IsMuted, InviteCode,
                IsVip, LotteryTickets, VipTier, ShowPurchases, ShowFavorites,
                NotifyReply, NotifyMention)
               VALUES (?,?,?,?,?,?,0,?,0,0,?,0,0,0,0,0,1,1)""",
            (username, demo_hash, nick, points, 40 + random.randint(0, 80), level, now, invite),
        )
    conn.commit()

    names = [u[0] for u in EXTRA_USERS] + ["admin", "demo", "newbie"]
    placeholders = ",".join("?" * len(names))
    user_ids = [r[0] for r in cur.execute(f"SELECT Id FROM Users WHERE Username IN ({placeholders})", names)]
    if not user_ids:
        raise SystemExit("no users")

    forums = cur.execute("SELECT Id, Name FROM Forums ORDER BY Id").fetchall()
    total_threads = 0
    total_posts = 0

    for forum_id, forum_name in forums:
        existing = cur.execute(
            "SELECT COUNT(*) FROM Threads WHERE ForumId=? AND IsHidden=0", (forum_id,)
        ).fetchone()[0]
        target = random.randint(20, 30)
        need = max(0, target - existing)
        print(f"Forum {forum_id} {forum_name}: have {existing}, add {need} (target {target})")
        if need == 0:
            continue

        titles = pick_titles(forum_name, need)
        for title in titles:
            author = random.choice(user_ids)
            created = datetime.utcnow() - timedelta(days=random.randint(0, 45), minutes=random.randint(0, 24 * 60))
            seed_key = random.randint(1, 100_000)
            views = random.randint(20, 800)
            likes = random.randint(0, 40)
            reply_n = random.randint(10, 30)

            cur.execute(
                """INSERT INTO Threads
                   (ForumId, AuthorId, Title, Type, CoinPrice, Views, ReplyCount, LikeCount,
                    IsHidden, PendingReview, RepliesLocked, IsPinned, IsEssence, EssenceAwarded,
                    CreatedAt, LastReplyAt)
                   VALUES (?,?,?,'public',0,?,?,?,0,0,0,0,0,0,?,?)""",
                (forum_id, author, title, views, reply_n, likes, fmt(created), fmt(created)),
            )
            thread_id = cur.lastrowid
            total_threads += 1

            first_images = None
            if random.random() < 0.25:
                imgs = [picsum(seed_key)]
                if random.random() < 0.5:
                    imgs.append(TINY_PNG)
                first_images = json.dumps(imgs, ensure_ascii=False)

            cur.execute(
                """INSERT INTO Posts (ThreadId, AuthorId, Content, Floor, CreatedAt, ImagesJson, ReplyToPostId, IsDeleted)
                   VALUES (?,?,?,1,?,?,NULL,0)""",
                (thread_id, author, random.choice(CONTENTS), fmt(created), first_images),
            )
            first_id = cur.lastrowid
            post_ids = [first_id]
            total_posts += 1
            last_at = created

            for i in range(reply_n):
                last_at = last_at + timedelta(minutes=random.randint(3, 180))
                reply_author = random.choice(user_ids)
                roll = random.random()
                reply_to = None
                images = None
                if roll < 0.22:
                    reply_to = random.choice(post_ids)
                    content = random.choice(REPLY_QUOTE)
                elif roll < 0.40:
                    content = random.choice(REPLY_IMAGE)
                    imgs = [picsum(seed_key * 100 + i + 3)]
                    if random.random() < 0.35:
                        imgs.append(TINY_PNG)
                    images = json.dumps(imgs, ensure_ascii=False)
                else:
                    content = random.choice(REPLY_NORMAL)
                    if random.random() < 0.08:
                        images = json.dumps([picsum(seed_key + i + 99)], ensure_ascii=False)

                cur.execute(
                    """INSERT INTO Posts
                       (ThreadId, AuthorId, Content, Floor, CreatedAt, ImagesJson, ReplyToPostId, IsDeleted)
                       VALUES (?,?,?,?,?,?,?,0)""",
                    (thread_id, reply_author, content, i + 2, fmt(last_at), images, reply_to),
                )
                post_ids.append(cur.lastrowid)
                total_posts += 1

            cur.execute(
                "UPDATE Threads SET LastReplyAt=? WHERE Id=?",
                (fmt(last_at), thread_id),
            )

        conn.commit()
        print(f"  committed forum {forum_id}")

    # recalc counts
    cur.execute(
        """
        UPDATE Forums SET
          ThreadCount = (SELECT COALESCE(COUNT(*),0) FROM Threads t WHERE t.ForumId=Forums.Id AND t.IsHidden=0),
          PostCount = (
            SELECT COALESCE(COUNT(*),0) FROM Posts p
            INNER JOIN Threads t ON t.Id=p.ThreadId
            WHERE t.ForumId=Forums.Id AND IFNULL(p.IsDeleted,0)=0
          )
        """
    )
    cur.execute(
        """
        UPDATE Threads SET ReplyCount = (
          SELECT COALESCE(COUNT(*),0) FROM Posts
          WHERE Posts.ThreadId=Threads.Id AND Posts.Floor>1 AND IFNULL(Posts.IsDeleted,0)=0
        )
        """
    )

    existing_flag = cur.execute("SELECT Id FROM SiteSettings WHERE Key=?", (SETTING_KEY,)).fetchone()
    if existing_flag:
        cur.execute("UPDATE SiteSettings SET Value='1' WHERE Key=?", (SETTING_KEY,))
    else:
        cur.execute("INSERT INTO SiteSettings (Key, Value) VALUES (?, '1')", (SETTING_KEY,))

    conn.commit()
    print(f"Done. Added ~{total_threads} threads, ~{total_posts} posts (incl. OP).")
    print_stats(cur)
    conn.close()


def print_stats(cur):
    print("\nPer-forum thread counts:")
    for row in cur.execute(
        """SELECT f.Id, f.Name, COUNT(t.Id),
                  COALESCE(AVG(t.ReplyCount),0),
                  SUM(CASE WHEN t.Id IS NOT NULL THEN 1 ELSE 0 END)
           FROM Forums f
           LEFT JOIN Threads t ON t.ForumId=f.Id AND t.IsHidden=0
           GROUP BY f.Id ORDER BY f.Id"""
    ):
        print(f"  [{row[0]}] threads={row[2]} avg_replies={row[3]:.1f}")

    q = cur.execute(
        "SELECT COUNT(*) FROM Posts WHERE ReplyToPostId IS NOT NULL"
    ).fetchone()[0]
    img = cur.execute(
        "SELECT COUNT(*) FROM Posts WHERE ImagesJson IS NOT NULL AND ImagesJson != '' AND ImagesJson != '[]'"
    ).fetchone()[0]
    print(f"Quote replies: {q}, Image posts: {img}")


if __name__ == "__main__":
    main()
