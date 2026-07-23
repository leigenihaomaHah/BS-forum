# -*- coding: utf-8 -*-
import json
import urllib.request
import sys

sys.stdout.reconfigure(encoding="utf-8")
base = "http://127.0.0.1:4080/api"


def req(method, path, data=None, token=None):
    headers = {"Content-Type": "application/json; charset=utf-8"}
    if token:
        headers["Authorization"] = "Bearer " + token
    body = None
    if data is not None:
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
    r = urllib.request.Request(base + path, data=body, headers=headers, method=method)
    with urllib.request.urlopen(r) as resp:
        raw = resp.read().decode("utf-8")
        return json.loads(raw) if raw else None


login = req("POST", "/auth/login", {"username": "admin", "password": "admin123"})
token = login["token"]
print("ok login", login["user"]["username"], login["user"]["isAdmin"])

# Fix category / forum names (no emoji to avoid some console issues; icon optional)
cat = req("PUT", "/admin/categories/12", {"name": "财经投资", "icon": "💰"}, token)
print("category", cat.get("id"), cat.get("name"))

forum = req(
    "PUT",
    "/admin/forums/17",
    {
        "name": "股票研报",
        "icon": "📈",
        "description": "股市行情分析、个股与行业研报交流",
    },
    token,
)
print("forum", forum.get("id"), forum.get("name"), forum.get("description"))

# Delete broken empty thread if API supports, else just post new
# Check admin delete
try:
    urllib.request.urlopen(
        urllib.request.Request(
            base + "/admin/threads/384/delete",
            data=json.dumps({"reason": "empty content"}).encode("utf-8"),
            headers={
                "Authorization": "Bearer " + token,
                "Content-Type": "application/json",
            },
            method="POST",
        )
    )
    print("deleted thread 384")
except Exception as e:
    print("delete skip", e)

today = "2026年7月23日"
title = f"【{today}】A股早盘研报：结构性行情延续，关注科技与红利两条主线"
content = f"""## 一、市场概况

{today} 开盘前展望：外围市场情绪偏稳，国内政策与中报预期共同主导短期定价。预计指数仍以震荡分化为主，成交额若维持万亿上方，结构性机会仍优于全面进攻。

## 二、主线研判

**1. 科技成长**  
人工智能、算力与先进制造链条仍是资金交易密度最高的方向。建议关注业绩兑现与订单能见度，回避纯主题炒作标的。

**2. 高股息红利**  
在利率环境与机构配置需求支撑下，银行、公用事业、能源等红利资产具备底仓价值，适合作为组合稳定器。

**3. 消费与中报**  
中报窗口临近，具备业绩超预期潜力的细分消费、医药龙头可能出现阶段性修复行情。

## 三、风险提示

- 外部宏观与汇率波动带来的风险偏好扰动  
- 题材股退潮后的流动性分层加剧  
- 个股业绩不及预期的回撤风险  

## 四、操作建议

仓位建议维持中性偏积极，核心仓位配置业绩确定性强的龙头，卫星仓位控制在主题弹性品种，严格执行止盈止损。

> 本研报仅供论坛交流讨论，不构成任何投资建议。股市有风险，入市需谨慎。
"""

thread = req(
    "POST",
    "/threads",
    {
        "forumId": 17,
        "title": title,
        "content": content,
        "type": "normal",
        "tags": ["股市", "研报", "A股"],
    },
    token,
)
print("thread", thread.get("id"), thread.get("title"))
print("posts", len(thread.get("posts") or []))
if thread.get("posts"):
    print("content_len", len(thread["posts"][0].get("content") or ""))

# verify list
cats = req("GET", "/categories", token=token)
for c in cats:
    if c["id"] == 12 or any(f["id"] == 17 for f in c.get("forums") or []):
        print("VERIFY cat", c["id"], c["name"])
        for f in c.get("forums") or []:
            if f["id"] == 17:
                print("VERIFY forum", f["id"], f["name"], f["description"])
