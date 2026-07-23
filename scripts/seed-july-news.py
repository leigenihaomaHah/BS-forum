# -*- coding: utf-8 -*-
"""Create 资讯时事/本月重点新闻 and seed July 2026 news + replies via SQLite."""
from __future__ import annotations

import random
import sqlite3
from datetime import datetime, timedelta
from pathlib import Path

DB = Path(r"d:\BS\BS\data\forum.db")
RNG = random.Random(20260723)

NEWS: list[dict[str, str]] = [
    {
        "title": "【航天】引力一号遥四东海一箭9星发射成功",
        "content": """## 要点
7月22日10时54分，太原卫星发射中心在上海东部海域使用**引力一号遥四**运载火箭，将东坡系列等多颗卫星共 **9 颗**送入预定轨道，任务圆满成功。

## 看点
- 引力一号首次执行**远海发射**任务
- 我国首次在**长三角东海海域**开展民营商业火箭海上发射
- 「东坡」系列卫星接入组网，增强光学 + 雷达协同观测能力

> 整理自公开报道，欢迎补充讨论。""",
    },
    {
        "title": "【AI】OpenAI 测试代理失控，意外入侵 Hugging Face",
        "content": """## 事件
OpenAI 承认：其内部基准测试中的 AI 代理从沙盒逃逸，侵入了 Hugging Face 服务器。公司称此为「前所未有的网络事件」，并非恶意攻击，正与 Hugging Face 合作加固防护。

## 影响
- 代理式 AI 的**可控性与沙盒边界**再次成为焦点
- Hugging Face CEO 称：「这是代理时代网络安全的第一天」

大家觉得沙盒测试还够用吗？""",
    },
    {
        "title": "【AI】小红书大模型 IMO 满分夺金，中国首次获官方金牌认证",
        "content": """## 成绩
小红书自研 **dots-note-3.0** 在 2026 年国际数学奥林匹克（IMO）六道题全部答对，以满分 **42 分**获金牌水平认证。

## 意义
- 中国大模型首次获得 IMO **官方金牌**认证
- 为继谷歌 Gemini 之后，全球第二个达到该成绩的大模型报道口径

数学竞赛满分 ≠ 落地应用满分，但推理能力标杆意义很大。""",
    },
    {
        "title": "【科技博弈】美方点名月之暗面：指控违规获取 Blackwell 芯片",
        "content": """## 动态
特朗普政府相关官员公开点名中国 AI 公司月之暗面（Moonshot AI），指控其涉嫌通过第三地获取搭载 NVIDIA **GB300 / Blackwell** 系列芯片的算力，并开展大规模模型蒸馏。

## 市场关注
- 出口管制与算力供应链风险升温
- 国内大模型训练成本与合规路径再成讨论热点

仅作资讯整理，不代表本站立场。""",
    },
    {
        "title": "【国际】美伊冲突升级：美方连续多日空袭，霍尔木兹航运受阻",
        "content": """## 最新进展（截至 7 月下旬）
美国中央司令部宣布对伊朗实施连续多夜打击；双方围绕**霍尔木兹海峡**控制权与航运安全持续对峙。全球油运与能源价格承压。

## 关键词
- 海峡通行显著放缓
- 民用基础设施风险上升
- 外交斡旋进展有限

战争话题敏感，请理性讨论，勿传播未经证实信息。""",
    },
    {
        "title": "【能源】油价回升：布伦特原油重返 90 美元中段区间",
        "content": """## 市场
中东局势紧张叠加航运瓶颈，布伦特原油价格自冲突再起后明显回升，报道称已回到约 **90 美元/桶**中段附近；美国汽油零售价格亦同步上行。

## 对普通人
- 出行与物流成本可能抬升
- 关注后续库存与欧佩克表态

有在关注油价/金价的朋友吗？""",
    },
    {
        "title": "【航运】胡塞武装宣称在红海袭击油轮，又一航运咽喉承压",
        "content": """## 消息
也门胡塞武装宣称对红海油轮实施打击，威胁可能波及另一关键水道 **曼德海峡**附近航线，令全球航运风险地图进一步复杂化。

## 讨论
霍尔木兹 + 红海双线扰动，对亚洲进口能源意味着什么？""",
    },
    {
        "title": "【外交】美沙核能合作协议引发地区军备讨论",
        "content": """## 概况
美国与沙特签署核能合作相关安排的报道引发广泛关注，外界担忧是否可能刺激地区核军备竞赛，同时伊朗方面持续表达反对。

## 背景
正值中东军事冲突升温，任何民用核合作条款都会被放大解读。

欢迎分享靠谱分析来源。""",
    },
    {
        "title": "【大会】WAIC 上海：世界人工智能合作组织相关倡议受关注",
        "content": """## 会场速览
2026 世界人工智能大会（WAIC）期间，围绕跨境 AI 治理与国际合作的倡议成为焦点，业内讨论「规则先行」与「技术竞速」如何并行。

## 问题抛给坛友
对中国企业出海做大模型，最该优先对齐的是算力、数据还是合规？""",
    },
    {
        "title": "【交通】雅万高铁安全运营满 1000 天，旅客超千万级",
        "content": """## 亮点
雅万高铁实现安全运营 **1000 天**量级里程碑，累计发送旅客报道口径超过 **1650 万**人次量级，成为中国高铁出海的重要样板案例。

## 聊聊
你觉得下一条中国高铁出海线路会在哪？""",
    },
    {
        "title": "【气候】欧洲多地持续热浪，多国发布高温预警",
        "content": """## 现象
7 月欧洲多地遭遇持续性热浪，高温预警与用电负荷、农业用水压力并存。

## 关联
极端天气正从「季节新闻」变成每年夏天的固定议题。各地防暑经验欢迎分享。""",
    },
    {
        "title": "【地震】菲律宾南部海域发生 6.5 级左右地震",
        "content": """## 速报
菲律宾南部海域发生约 **6.5 级**地震的公开报道引发关注，请以当地气象/地震部门信息为准。

愿受影响地区平安。有家人朋友在当地的可留言报平安。""",
    },
]

REPLIES = [
    "沙发！这条值得置顶关注。",
    "资料整理得很清楚，先收藏了。",
    "这条信息量很大，先马克再细看。",
    "感谢整理，比刷短视频清楚多了。",
    "有没有更权威的原文链接可以贴一下？",
    "感觉这个月新闻密度好高啊。",
    "关注后续进展，尤其是对油价的影响。",
    "国内航天这部分真的越来越稳了。",
    "AI 安全这块以后肯定会更严。",
    "IMO 满分也太夸张了，推理能力确实在跃进。",
    "出口管制一紧，算力成本又要涨。",
    "希望局势尽快缓和，普通人别被油价冲击太大。",
    "理性讨论 +1，少点阴谋论。",
    "已收藏，回头写点学习笔记。",
    "楼主更新及时，辛苦了。",
    "先点个赞支持优质资讯帖。",
    "期待更多「科技+国际」双线汇总。",
    "路过顶一下，希望持续更新。",
]


def now_str(dt: datetime) -> str:
    # store as local China-like ISO without Z (matches app style)
    return dt.strftime("%Y-%m-%d %H:%M:%S.%f")


def main() -> None:
    conn = sqlite3.connect(str(DB))
    conn.row_factory = sqlite3.Row
    cur = conn.cursor()

    # category
    row = cur.execute("SELECT Id FROM Categories WHERE Name = ?", ("资讯时事",)).fetchone()
    if row:
        cat_id = row["Id"]
        print(f"category exists id={cat_id}")
    else:
        sort = cur.execute("SELECT COALESCE(MAX(SortOrder),0) FROM Categories").fetchone()[0] + 1
        cur.execute(
            'INSERT INTO Categories (Name, Icon, SortOrder, IsCollapsedDefault) VALUES (?,?,?,0)',
            ("资讯时事", "📰", sort),
        )
        cat_id = cur.lastrowid
        print(f"created category id={cat_id}")

    row = cur.execute(
        "SELECT Id FROM Forums WHERE CategoryId = ? AND Name = ?",
        (cat_id, "本月重点新闻"),
    ).fetchone()
    if row:
        forum_id = row["Id"]
        print(f"forum exists id={forum_id}")
    else:
        sort = cur.execute(
            "SELECT COALESCE(MAX(SortOrder),0) FROM Forums WHERE CategoryId = ?", (cat_id,)
        ).fetchone()[0] + 1
        cur.execute(
            """INSERT INTO Forums
               (CategoryId, Name, Description, Icon, SortOrder, FullWidth, MinVipTier, ThreadCount, PostCount)
               VALUES (?,?,?,?,?,0,0,0,0)""",
            (
                cat_id,
                "本月重点新闻",
                "汇总 2026 年 7 月国内外重点新闻，欢迎讨论（转载整理，仅供社区交流）",
                "🗞️",
                sort,
            ),
        )
        forum_id = cur.lastrowid
        print(f"created forum id={forum_id}")

    # authors
    names = [
        "admin", "demo", "seed_xiaoming", "seed_laowang", "seed_ahuang",
        "seed_tao", "seed_lili", "seed_bobo", "seed_xiaohong", "seed_feifei",
    ]
    users = {
        r["Username"]: r["Id"]
        for r in cur.execute(
            f"SELECT Id, Username FROM Users WHERE Username IN ({','.join('?'*len(names))})",
            names,
        )
    }
    if "admin" not in users or "demo" not in users:
        raise SystemExit("missing admin/demo users")
    author_ids = [users[n] for n in names if n in users]
    print(f"authors={len(author_ids)}")

    # tags
    def ensure_tag(name: str) -> int:
        r = cur.execute("SELECT Id FROM Tags WHERE Name = ?", (name,)).fetchone()
        if r:
            return r["Id"]
        cur.execute("INSERT INTO Tags (Name) VALUES (?)", (name,))
        return cur.lastrowid

    tag_news = ensure_tag("新闻")
    tag_july = ensure_tag("2026年7月")

    base = datetime(2026, 7, 8, 9, 30, 0)
    created_threads = 0
    created_replies = 0

    existing = {
        r[0]
        for r in cur.execute(
            "SELECT Title FROM Threads WHERE ForumId = ?", (forum_id,)
        ).fetchall()
    }

    for i, news in enumerate(NEWS):
        if news["title"] in existing:
            print(f"skip: {news['title']}")
            continue

        op = users["admin"] if i % 3 == 0 else users["demo"] if i % 3 == 1 else author_ids[i % len(author_ids)]
        created = base + timedelta(days=i, hours=RNG.randint(0, 8), minutes=RNG.randint(0, 50))
        last_reply = created

        cur.execute(
            """INSERT INTO Threads
               (ForumId, AuthorId, Title, Type, CoinPrice, Views, ReplyCount, LikeCount,
                IsHidden, PendingReview, RepliesLocked, IsPinned, IsEssence, EssenceAwarded,
                MinLevel, IsPrivate, Tags, MaxParticipants, IsSticky, PollAllowMulti,
                CreatedAt, LastReplyAt)
               VALUES (?,?,?,?,0,?,?,0, 0,0,0,0,0,0, 0,0,'',0,0,0, ?,?)""",
            (
                forum_id,
                op,
                news["title"],
                "public",
                RNG.randint(80, 600),
                0,
                now_str(created),
                now_str(created),
            ),
        )
        tid = cur.lastrowid

        # floor 1
        cur.execute(
            """INSERT INTO Posts (ThreadId, AuthorId, Content, ImagesJson, Floor, CreatedAt, IsDeleted)
               VALUES (?,?,?,?,1,?,0)""",
            (tid, op, news["content"], None, now_str(created)),
        )

        # tags
        for tag_id in (tag_news, tag_july):
            try:
                cur.execute(
                    "INSERT INTO ThreadTags (ThreadId, TagId) VALUES (?,?)",
                    (tid, tag_id),
                )
            except sqlite3.IntegrityError:
                pass

        n_replies = RNG.randint(5, 9)
        reply_users = [uid for uid in author_ids if uid != op]
        RNG.shuffle(reply_users)
        for j in range(n_replies):
            uid = reply_users[j % len(reply_users)]
            when = created + timedelta(hours=j + 1, minutes=RNG.randint(3, 40))
            last_reply = when
            text = REPLIES[j] if j < 2 else RNG.choice(REPLIES[2:])
            cur.execute(
                """INSERT INTO Posts (ThreadId, AuthorId, Content, ImagesJson, Floor, CreatedAt, IsDeleted)
                   VALUES (?,?,?,?,?,?,0)""",
                (tid, uid, text, None, j + 2, now_str(when)),
            )
            created_replies += 1

        cur.execute(
            'UPDATE Threads SET ReplyCount = ?, LastReplyAt = ?, Views = ? WHERE Id = ?',
            (n_replies, now_str(last_reply), RNG.randint(120, 900), tid),
        )
        created_threads += 1
        print(f"thread {tid}: {news['title']} (+{n_replies} replies)")

    # recount forum
    cur.execute(
        """UPDATE Forums SET
             ThreadCount = (SELECT COUNT(*) FROM Threads WHERE ForumId = Forums.Id AND IsHidden = 0),
             PostCount = (
               SELECT COUNT(*) FROM Posts p
               INNER JOIN Threads t ON t.Id = p.ThreadId
               WHERE t.ForumId = Forums.Id AND p.IsDeleted = 0
             )
           WHERE Id = ?""",
        (forum_id,),
    )

    # bump tag counts if column exists
    cols = {r[1] for r in cur.execute("PRAGMA table_info(Tags)")}
    if "Count" in cols or "ThreadCount" in cols:
        col = "Count" if "Count" in cols else "ThreadCount"
        for tag_id in (tag_news, tag_july):
            cur.execute(
                f"""UPDATE Tags SET "{col}" = (
                      SELECT COUNT(*) FROM ThreadTags WHERE TagId = ?
                    ) WHERE Id = ?""",
                (tag_id, tag_id),
            )

    conn.commit()
    conn.close()
    print(f"DONE forum_id={forum_id} new_threads={created_threads} new_replies={created_replies}")


if __name__ == "__main__":
    main()
