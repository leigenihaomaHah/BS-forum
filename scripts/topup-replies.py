# -*- coding: utf-8 -*-
"""把回复数不足 10 的旧帖补到 10–30。"""
import json
import random
import sqlite3
from datetime import datetime, timedelta
from pathlib import Path

DB = Path(r"D:\BS\BS\backend\forum.db")
TINY_PNG = (
    "data:image/png;base64,"
    "iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAFUlEQVR42mNk+M9Qz0AEYBxVSF+FABJADveWkH6oAAAAAElFTkSuQmCC"
)
REPLY_NORMAL = ["说得很有道理，学到了。", "同感，我也遇到过类似情况。", "马克一下，回头细看。", "支持楼主，继续更新！", "路过顶一下。"]
REPLY_QUOTE = ["回复楼上：完全同意你的看法。", "引用一下，这个细节很关键。", "楼上一语中的。"]
REPLY_IMAGE = ["附图说明一下～", "上图，大家看看像不像。", "现场实拍，供参考。"]


def picsum(seed):
    return f"https://picsum.photos/seed/bs{abs(seed) % 100000}/640/420"


def fmt(dt):
    return dt.strftime("%Y-%m-%d %H:%M:%S.") + f"{dt.microsecond // 100:07d}"


def main():
    conn = sqlite3.connect(str(DB), timeout=60)
    conn.execute("PRAGMA busy_timeout=60000")
    cur = conn.cursor()
    users = [r[0] for r in cur.execute("SELECT Id FROM Users LIMIT 50")]
    rows = cur.execute(
        "SELECT Id, ReplyCount, LastReplyAt FROM Threads WHERE ReplyCount < 10"
    ).fetchall()
    print(f"Topping up {len(rows)} threads")
    for tid, rc, last in rows:
        target = random.randint(10, 30)
        need = target - rc
        posts = [r[0] for r in cur.execute(
            "SELECT Id FROM Posts WHERE ThreadId=? AND IFNULL(IsDeleted,0)=0 ORDER BY Floor", (tid,)
        )]
        try:
            last_at = datetime.fromisoformat(str(last).split(".")[0])
        except Exception:
            last_at = datetime.utcnow()
        floor = rc + 2
        for i in range(need):
            last_at = last_at + timedelta(minutes=random.randint(3, 90))
            roll = random.random()
            reply_to = None
            images = None
            if roll < 0.22 and posts:
                reply_to = random.choice(posts)
                content = random.choice(REPLY_QUOTE)
            elif roll < 0.4:
                content = random.choice(REPLY_IMAGE)
                images = json.dumps([picsum(tid * 50 + i)], ensure_ascii=False)
            else:
                content = random.choice(REPLY_NORMAL)
            cur.execute(
                """INSERT INTO Posts (ThreadId, AuthorId, Content, Floor, CreatedAt, ImagesJson, ReplyToPostId, IsDeleted)
                   VALUES (?,?,?,?,?,?,?,0)""",
                (tid, random.choice(users), content, floor + i, fmt(last_at), images, reply_to),
            )
            posts.append(cur.lastrowid)
        cur.execute(
            "UPDATE Threads SET ReplyCount=?, LastReplyAt=? WHERE Id=?",
            (target, fmt(last_at), tid),
        )
    conn.commit()
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
    conn.commit()
    print("min/max/avg", cur.execute("select min(ReplyCount), max(ReplyCount), avg(ReplyCount) from Threads").fetchone())
    print("under 10", cur.execute("select count(*) from Threads where ReplyCount < 10").fetchone()[0])
    conn.close()


if __name__ == "__main__":
    main()
