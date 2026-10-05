# URL 上的加密载荷：跳层令牌与分享链接（方案）

> 需求原话：「写一下不同层级的切换，url密码解密」；两次追问后确认是两件事：
> **(A) 链接里带一段密文，宿主解密后授权一次纪元（era）跳转**；
> **(B) 分享链接：把存档塞进链接、用密码保护，收件人打开链接、输入密码，宿主解密并导入**。
>
> 本文回答四件事：**今天已经有什么**（§1，带 `文件:行` 证据）、
> **一份存档到底塞不塞得进 URL**（§2，实测数字）、**要做成什么形状**（§3~§9）、
> **分几刀、这一刀做到哪**（§12）。
>
> **本文的归属与它不抢的两份文档**：
> - `WEB_DEBUG_GATE_PLAN.md` 仍然拥有**明文调试门**（`NEKO_DEBUG_KEY` + `?password=` + `?epoch=N`）。
>   本文给它加了**另一扇门**（令牌门），**一个字节都不改那扇门**（§4.4 有复核清单）。
> - `SAVE_TRANSFER_PLAN.md` 仍然拥有**存档信封与导入的九道闸**。分享链接**不新造导入路径**：
>   解密出来的就是 `SaveManager.Export()` 的原文，交给**同一个** `SaveManager.Import`（§5.3）。
> - 本文拥有的是**新东西**：URL 上的加密载荷本身——线格式、KDF、AEAD、大小闸、URL 卫生。
>   这三件事此前没有归属，所以它是一份新文档，而不是塞进上面两份里抢它们的题材。
>
> 状态：**方案 + 第一刀已落地（`2026-10-06`，见 §13 的落地记录）**。

---

## 1. 今天有什么（先量，再设计）

### 1.1 调试门：`NEKO_DEBUG_KEY` + `?epoch=N`（**不许被本文削弱**）

实现全在宿主侧（`WEB_DEBUG_GATE_PLAN.md` 开头的状态行写明「引擎一行未改」）。
**下面这些 `Program.cs` 的行号是改动之前（`5863dc1`）的**：第一刀落地之后那个文件长了约 270 行，
`Program.cs` 里凡是 `?k=` 那一段之外的代码都还在原处、只是往后挪了（§4.4 有"一个字节都没改"的复核清单）。

| 行为 | 证据 |
|---|---|
| 中间件**必须排在静态文件之前**（否则 `/` 先被 `UseDefaultFiles` 端成 `index.html`） | `games/hosts/Web/Program.cs:76-80` |
| 只认 `GET /` 与 `/index.html`；**没有 `epoch` 就一字不改地走原路** | `Program.cs:370-380` |
| ① 密钥装置本身不在：**403** + `"调试门未启用：本机没有设置环境变量 NEKO_DEBUG_KEY。"` | `Program.cs:385-395` |
| ② 密钥不对：**403** + `"调试密钥不对。"`（只区分"没配"与"配错"，不回显正确值） | `Program.cs:398-407` |
| 密钥比较方式：`string.Equals(query["password"].ToString(), key, StringComparison.Ordinal)` —— **序数比较**，没有 Trim、没有大小写折叠 | `Program.cs:398` |
| 密钥来源：只从环境变量名 `NEKO_DEBUG_KEY` 读（仓库里不存在任何密钥值） | `Program.cs:30`、`Program.cs:385` |
| ③ 密钥对了**才**允许碰会话（被拒的请求不该把内容包加载进内存） | `Program.cs:409-423` |
| ④ 解析与范围校验都在**游戏线程**上做，所以 400 里带的一定是这个包**真实**的范围 | `Program.cs:426-440`（`JumpToEraAsync`） |
| ⑤ 成功：200，正文回报 `skipped: ["era_inheritance","era_history","prestige_settlement"]` 与 `autosave: "disabled"` | `Program.cs:32-34`、`Program.cs:443-454` |
| **跳层 = 直接改 `GameState.Era`**，跳过跨层继承 / 层历史 / 转生结算 | `GameHost.cs:435-488`（注释在 `:416-434`） |
| **调试模式禁一切落盘**：自动存档关掉 + `SaveAsync` / `ExportAsync` / `ImportAsync` / 退出存档四条路各自拒绝 | `GameHost.cs:474-475`、`:334`、`:362`、`:403`、`:520-523` |
| 启动时把"门开没开"印在控制台（**永远不打印密钥本身**） | `Program.cs:95-99` |

**这道门今天就是"URL 上的密码"，只不过密码是明文写在 URL 里的**（§4.3 说明本文为什么还要另一种形态）。

### 1.2 存档搬运：信封 + 九道闸（**不许被本文绕过**）

| 能力 | 证据 |
|---|---|
| 信封字段：`Format` / `FormatVersion` / `PackId` / `SaveVersion` / `FrameworkVersion` / `ExportedAt` / `Checksum` / `Save` | `engine/core/Persistence/SaveTransfer.cs:112-183` |
| 校验和 = `sha256:` + **对 `Save` 那个字符串的 UTF-8 字节**求 hash | `SaveTransfer.cs:140-145` |
| 尺寸闸 = `MaxTransferChars = 1 << 20`（1 MiB），**在 JSON 解析之前** | `SaveTransfer.cs:120-127`、`:200-216` |
| **12 个失败类别**（每种都能被单独断言） | `SaveTransfer.cs:17-54` |
| 导入顺序：尺寸 → 信封 → 版本 → 校验和 → 归属 → 结构 → 一致性 → 落盘 → 应用 | `SaveManager.cs:174-212`（注释）、`:213-326`（实现） |
| 坏输入**在碰磁盘之前**被拦；落盘失败时旧存档与 `.bak` 一个字节不动 | `SaveManager.cs:274-291`、`SAVE_TRANSFER_PLAN.md` §3 |
| 导出文本走 `CommandOutcome.Text` 回浏览器（与给人看的 `Message` 分开） | `GameHost.cs:19-27`、`Program.cs:200-205` |

### 1.3 两个宿主怎么接命令

- Web：`Program.DispatchAsync`（`Program.cs:210-353`）把 `{"type":…}` 翻成 `GameHost.ExecuteAsync`，
  `CommandOutcome` 的四个字段（`ok`/`message`/`seq`/`text`）原样进 `/api/command` 的 JSON（`Program.cs:199-206`）。
  前端 `send()`（`app.js:160-176`）拿到它，`quiet: true` 时不弹提示条而是把长句留在窗口里（`app.js:153-159`）。
- 终端：没有 URL，也没有弹窗。"窗口"= 一次**文件路径提示**（`Demo.Cli/GameSession.cs:556`、
  `InteractiveLoop.cs:439`，方案 `SAVE_TRANSFER_PLAN.md` §4.2）。**URL 对终端没有意义**（§8 说清等价物）。

### 1.4 今天没有的

- URL 上**任何**加密载荷：`?password=` 是**明文**密钥，且**没有有效期、没有范围**——一次泄漏 = 永久可用。
- 任何"把存档放进链接"的路：导出/导入只有**复制粘贴**与**下载 .json** 两条（`index.html:73-95`）。
- 任何密码输入框：`wwwroot/index.html` 里今天**一个 `<input>` 都没有**（只有 `<textarea>` 与按钮）。
- 任何 URL → 命令的通路：前端只从 URL 读 `package`（`app.js:22-23`）与 `#tab=`（`app.js:1935`），
  **从不上报 URL 里的东西给宿主**。

---

## 2. 一份存档塞得进 URL 吗（实测）

### 2.1 实测：真实 `saves/*.json` 逐份跑一遍编解码

一次性探针（`engine/tests/ShareLinkProbe.cs`，跑完即删；探针只**读**真实存档）：
每份存档 → `SaveTransfer.Wrap` 得到**整份导出文本** → `ShareLinkCodec.Protect` 得到链接长度
（gzip → AES-GCM → base64url，含 37 字节头 + 16 字节 tag）：

| 存档 | 本体 | 导出文本 | **链接** |
|---|---|---|---|
| `lab.json` | 831 | 1495 | **1008** |
| `cafe.json` | 840 | 1505 | **998** |
| `neko.json` | 846 | 1511 | **1006** |
| `company.json` | 1519 | 2592 | **1438** |
| `apocalypse.json` | 2157 | 3638 | **1748** |
| `ninelines.json` | 3718 | 6203 | **2258** |

**结论：今天仓库里所有真实存档都装得进链接**，约 **1.0~2.3 KB**（导出文本的 **~36%**）。
链接上限 `MaxLinkChars = 8000`，所以余量约 **3.5 倍**；按同一压缩率外推，
8000 字符大约对应 **2.2 万字符的导出文本**（≈ 4 倍于目前最大那份）。
（这条外推是**外推**，不是实测——真到那一天，产出端那道闸会拒绝，而拒绝是可测的：
`ShareLinkTests.ShareLink_FitsATypicalSaveAndRefusesAHugeOne` 里那半条用的是一段**压不动**的文本。）

### 2.2 浏览器与"实用"上限

- 规范上 URL 没有硬上限，但**实用**上限来自三处：老 IE 的 2083 字符、很多聊天工具/SNS 的链接
  预览与换行截断、以及人肉复制时的可读性。**本文取 8000 字符当硬闸**
  （`ShareLinkCodec.MaxLinkChars`），并**把它当成一条会响的闸而不是一句建议**（§6.3）。
- 超过闸时不"截断到刚好"——**截断的链接必然解密失败**，那种失败看起来像"密码不对"，
  会把人带去查一个不存在的问题（正是 `WEB_DEBUG_GATE_PLAN` §5 要消灭的失败形态）。
  所以：**不产出链接**，并明说"改用复制粘贴导出"。

### 2.3 诚实边界

**不是所有存档都能变成链接。** 今天最大的真实存档导出 6203 字符、链接 2258 字符，
但**它随进度增长**（建筑/升级/成就/表态/结局/层历史都会进存档），重度玩家的存档会超过那道闸。
那时**没有**可用的链接——这是物理限制，不是实现没做完。
本文的方案是：(a) 能塞就塞，把真实长度报出来；(b) 塞不下就**明确拒绝**并把复制粘贴那条路指出来。

---

## 3. 密码学方案（只用 BCL）

**一条都不能省的自我约束：不发明密码、不用 ECB、不自己写 KDF、不自己拼 MAC。**
用到的全是 BCL 里的既有原语：

| 角色 | 用什么 | 为什么 |
|---|---|---|
| 口令拉伸（KDF） | `Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32)` | 输入是**人选的短口令**（低熵），必须用**慢** KDF；HKDF 是给高熵密钥做扩展的，用在这里等于没有拉伸 |
| 对称加密 + 认证 | `AesGcm`（**AEAD**，128-bit tag） | 一条原语同时给机密性与完整性；没有填充 ⇒ 没有 padding oracle；**不是** CBC/ECB + 自拼 HMAC |
| 压缩 | `System.IO.Compression.GZipStream`（加密**之前**） | 明文**全部是我们自己生成的**，不与攻击者可控内容混在一起压缩 ⇒ 没有 CRIME/BREACH 那类"压缩侧信道"前提 |
| 编码 | base64url（`-`/`_`，去掉 `=`） | URL query 里不需要再转义；`+`/`/`/`=` 在聊天工具与 Markdown 里会被吃掉或被转义 |
| 盐 / 随机数 | `RandomNumberGenerator.GetBytes(16)` / `(12)` | 每份链接**各一次**，绝不复用（GCM 下 nonce 复用是灾难性的） |

### 3.1 线格式（版本化，先定死再实现）

base64url 之前是一段定长头 + 密文 + tag：

```
偏移  长度  内容
0     4     magic "NKL" + 版本字节 '1'        ← 版本化：以后换方案就换这个字节
4     1     kind：'e' = 跳层令牌 / 's' = 分享链接
5     4     迭代数（uint32，大端）
9     16    盐 salt
25    12    nonce
37    n     密文 ‖ 16 字节 GCM tag
```

- **盐、nonce、迭代数都在链接里、都是明文**——这是正常做法（它们不是秘密），
  它们的作用分别是"同一口令在不同链接上派生出不同密钥"、"每条链接一个不重复的计数器"、
  "KDF 要跑多慢"。**安全性不依赖把它们藏起来。**
- **`AssociatedData`（AEAD 认证的额外数据）= 第 0~36 字节整段头**。
  于是**你关心的元数据也被 tag 覆盖**：格式版本、用途（kind）、迭代数、盐、nonce
  任何一个被改，解密都会以认证失败告终，而不是"悄悄换了个用途"。
- 载荷本身（§3.4）在密文里，**它同样被 tag 覆盖**。
- tag 固定 16 字节（`AesGcm` 的默认长度，**不做截断**）。

### 3.2 迭代数与它的闸

- 默认 `210_000`（PBKDF2-HMAC-SHA256，OWASP 2023 的建议量级）。
- 解密端**强制区间** `[100_000, 4_000_000]`：
  - 下界：拦住"某个旧工具/手改的链接把 KDF 降到 1 轮"——那等于没有拉伸；
  - 上界：拦住"一条链接逼宿主跑几十秒 KDF"的拒绝服务形态。
  - 两者都以**类别明确**的失败拒绝，不静默照跑（`ShareLinkFailure.IterationsOutOfRange`）。
- 迭代数被 AAD 覆盖，所以**它也改不动**；两端各自校验只是为了把"链接坏了"说得更准。

### 3.3 口令的最短长度

- **硬闸**：`MinPasswordChars = 4`，更短的口令直接拒绝（消息里说清"链接里只有这一层保护"）。
- **软提示**：短于 12 个字符时，成功消息里**照样生成链接**，但带一句
  "这个密码偏短，链接若落到别人手里可以被离线暴力枚举"。
  理由：长度是使用者唯一能自己控制的杠杆，**把这件事说出来**比替使用者做决定更符合本仓库的脾气。

### 3.4 载荷（明文 JSON，压缩后加密）

```json
{
  "Format": "neko-share",
  "FormatVersion": 1,
  "Kind": "save",              // 或 "era"
  "PackId": "cafe",            // 令牌 = 这枚令牌只对哪个包有效；分享链接 = 信息性
  "Era": 7,                    // 仅 "era"
  "ExpiresAt": "2026-10-12T00:00:00.0000000+00:00",  // 仅 "era"；可为 null
  "Save": "{ … 整份导出文本 … }"  // 仅 "save"
}
```

- `Format` / `FormatVersion` **是载荷自己的轴**，与信封（`neko-save`）、存档格式
  （`SaveSerializer.CurrentVersion`）**各是各的**——三条轴混成一个数字，
  以后"我只换了个包装"与"老存档读不出来了"就会变成同一件事（同 `SAVE_TRANSFER_PLAN.md` §2.1 的取舍）。
- 载荷 JSON 的**大小上限** `MaxPayloadChars = 256 KiB`：压缩**之后**与解压**之后**各查一次，
  且解压时**按上限截断读取**（读满上限+1 字节就判太大）——防的是"压缩炸弹"。
  1 MiB 那道既有闸（`SaveTransfer.MaxTransferChars`）在解密之后仍然会再拦一次，两道都在。

### 3.5 失败类别（每一种都能被单独断言）

`NotALink`（不是 base64url / 太短 / magic 不对）、`UnsupportedVersion`（版本字节不是 `1`）、
`UnexpectedPurpose`（`kind` 与调用方期望的不符）、`IterationsOutOfRange`、`PasswordTooShort`、
`WrongPasswordOrTampered`（**GCM tag 失败**）、`TooLarge`（链接或载荷超限）、
`CorruptPayload`（解开了但不是合法载荷 JSON / 缺字段）、`Expired`、`ForeignPack`、`EraOutOfRange`。

**必须写在消息里的一句**：`WrongPasswordOrTampered` 的文案是
「密码不对，**或者**这份链接被改过/被截断过」——AEAD **在原理上分不开**这两种情况
（tag 失败就是 tag 失败）。假装分得开就是编一个不存在的事实。

---

## 4. 用途 A：跳层令牌（`?k=…`）

### 4.1 它授权什么

```
http://127.0.0.1:5273/?package=lab&k=<base64url 密文>
```

解密出来的载荷必须是 `Kind = "era"`，且：

1. `PackId` 与 URL 上的 `package` 相符（不符 → **403**，正文里两个包名都报出来）；
2. 有 `ExpiresAt` 时必须在有效期内（过期 → **403**，正文带上过期时刻）；
3. `Era` 交给**既有的** `GameHost.JumpToEraAsync(string)` 做解析与范围校验
   （`GameHost.cs:435-488`）——越界仍然是 **400**，正文里仍然带这个包**真实**的
   `validRange` 与那份 `skipped` 清单。

### 4.2 宿主用哪把钥匙解密、令牌怎么铸

- 钥匙**只从环境变量 `NEKO_URL_KEY` 来**（与 `NEKO_DEBUG_KEY` **分开**：
  两扇门不该共用一把钥匙，也不该因为设了一把就以为另一把也开了）。仓库里不存在任何密钥值。
- 铸造由同一支二进制提供（**不写第二个实现**）：

  ```
  $env:NEKO_URL_KEY = '…'
  web\neko-clicker-web.exe --mint-link --package lab --era 7 --expires-hours 24
  → http://127.0.0.1:5273/?package=lab&k=<…>
  ```

  没设 `NEKO_URL_KEY` 时**拒绝铸造**并说明原因（不产出一个"宿主解不开"的链接）。

### 4.3 为什么它不是"安全"，以及它比明文门强在哪

**先说不是的那一半**：钥匙在宿主的环境里。**能读到那把钥匙的人本来就能改这台机器上的存档、
跑无头模拟、直接改代码**——门挡不住他。所以这道门**不是权限系统**，
它挡的是"普通玩家误触调试功能"与"链接意外泄漏"。这句话必须写在文档与代码注释里，
否则下一个人会以为它比实际更强（同 `WEB_DEBUG_GATE_PLAN.md` §6）。

**它比明文门强的地方只有两条，但都是真的**：

1. **URL 里不再出现可复用的钥匙**。明文门的一行
   `?package=lab&password=<密钥>&epoch=7` 落进浏览器历史 / 截图 / 聊天记录，
   泄漏的是**那把钥匙本身**（于是所有包、所有层都开了）；令牌泄漏的只是
   **一次针对某个包某一层的跳转**，到期即废。
2. **范围与有效期是密文的一部分**（被 AAD/tag 覆盖），改不动：把 `7` 改成 `99` 只会得到
   「密码不对，或者这份链接被改过」；把 `ExpiresAt` 往后改同样。

### 4.4 明文门一个字节都不改（复核清单）

- `NEKO_DEBUG_KEY`、`?password=`、`?epoch=`、403/400/200 的正文形状、`_debugMode` 的语义、
  "调试模式禁一切落盘"的四条路：**全部不动**。
- 中间件的新判据是**加一条**：`if (!query.ContainsKey("epoch") && !query.ContainsKey("k")) return false;`
  ——不带参数的普通请求仍然一字不改地走原路（`WEB_DEBUG_GATE_PLAN.md` §7.5 那条回归底线）。
- 两扇门**互不授权**：只有 `NEKO_DEBUG_KEY` 时 `?k=` 得到「令牌门未启用」；
  只有 `NEKO_URL_KEY` 时 `?epoch=` 得到【既有的】「调试门未启用」。
- **不做一次性令牌**：那需要宿主侧状态（已用集合、清理、并发），而令牌本来就不是安全边界。
  有效期是"能过期"，不是"只能用一次"——这句话写在 §12 的"明确不做"里。

---

## 5. 用途 B：分享链接（`?share=…`）

### 5.1 生产（发件人）

1. 打开「存档」窗口（`index.html:66-97` 的第三张 sheet，**没有第二套弹窗**）；
2. 在**新增的**「分享链接」一节里输入一个**自己**定的密码；
3. 按「生成分享链接」→ 前端发 `POST /api/command {"type":"shareLink","password":"…"}`；
4. 宿主在**游戏线程**上取一份 `SaveManager.Export()` 原文（与 `export` 命令同一条路，
   **不写盘**）→ `ShareLinkCodec.Protect(password, SaveShare, packId, exportText, …)`；
5. 链接文本走**既有的** `CommandOutcome.Text` 回到前端（协议不加字段），
   显示在一个只读框里 + 「复制」按钮；长度一并报出来。

**密码不进 URL**：它在 POST 请求体里（本机回环 HTTP）。前端**不把它写进任何 DOM 属性**之外的持久位置，
宿主**不日志它、不回显它、不进异常消息**（§6）。

### 5.2 消费（收件人）

打开 `http://127.0.0.1:5273/?package=cafe&share=<密文>` 之后：

1. 前端检测到 `share` 参数 → **自动打开「存档」窗口**并显示"输入密码"那一行
   （**不自动尝试**，也不猜密码：`app.js` 里今天也没有任何宿主会话之外的凭据来源）；
2. 收件人输入密码 → `POST /api/command {"type":"importShare","token":"…","password":"…"}`；
3. 宿主解密 → 得到导出文本 → **原样交给 `SaveManager.Import`**（§5.3）；
4. 结果用**引擎自己那句话**显示在 `#import-result`（已有的第二个实时区域，`index.html:91`）；
5. 成功之后前端 `history.replaceState` 把 `share` 从地址栏剥掉（减小"又被复制出去"的机会），
   并重新导出一次（与 `importSave` 的既有行为一致，`app.js:766-767`）。

### 5.3 复用既有导入路径（**这是本节最要紧的一条**）

解密出来的字符串**就是** `SaveManager.Export()` 的原文，于是消费路径是：

```
ShareLinkCodec.Open(password, …)   →   text
text                               →   SaveManager.Import(text)   ← 同一个九道闸
```

- **不新造解析器**、不"从载荷里各字段重建一份存档"、不绕过校验和。
  载荷里带的是**整份导出文本**（含 `neko-save` 信封、包标识、校验和），
  代价是链接比"只带裸存档"大一点（§2），换来的是**导入那一侧一行新语义都没有**。
- **密码错 / 密文被改 ⇒ 一个字节都不动**：解密失败发生在 `SaveManager.Import` **之前**，
  所以磁盘、`.bak`、当前会话三者都没被碰过——这正是 1.10.0 那套设计已经保证的形状
  （`SAVE_TRANSFER_PLAN.md` §3.1），本文只是把"坏输入"的范围从"粘贴的文本"扩到"解不开的密文"。
- **调试会话（跳层过）不产链接、也不收链接**：与 `SaveAsync`/`ExportAsync`/`ImportAsync`
  四条路同一条规矩（`GameHost.cs:334/362/403`）——跳层只在内存里，
  而链接是**耐久且会离开这台机器**的东西。

---

## 6. URL 卫生（写下来，别只放在脑子里）

1. **密码永远不进 URL。** 生产走 POST 请求体；消费时密码同样在请求体里。
   URL 里只有密文（`?share=` / `?k=`），它是**没有密码就解不开的字节**。
2. **不日志密码、不日志派生密钥、不把它写进异常消息。** 消息里只出现"密码长度"这类计数（§3.3）。
3. **链接能做什么 / 不能做什么（对拿到链接的人）**：
   - **能**：把这段密文原样贴回宿主、让宿主尝试解密（那就是设计用途）；
   - **能**：离线暴力枚举口令——**这就是口令保护链接的固有弱点**，PBKDF2 的 210k 次迭代
     只是把每次猜测变慢，不是消除它。所以 §3.3 才要把"密码偏短"说出来。
   - **不能**：从链接里读出存档内容、读出密码、或伪造另一条能解开的链接（没有钥匙）。
4. **长度闸**：`MaxLinkChars = 8000`（§2.2）。**产出端**超限就拒绝生成（并指路复制粘贴导出）；
   **消费端**超限就以 `TooLarge` 拒绝——一条被粘贴得七零八落的超长链接不该让宿主先做几 MB 的 base64 解码。
5. **地址栏残留**：消费之后把 `share` 参数从 URL 里去掉（§5.2 第 5 步）。
   `k` 参数**保留**（它本来就是要被看见、被书签的东西），但它是**一次跳转**、有有效期。

---

## 7. UI：复用那张 sheet

- **入口**：不新增页签、不新增按钮——「存档」按钮打开的那张 sheet（`index.html:66-97`）里
  **加一节**「分享链接」，位置在「导出 / 导入」之后、「存档文件」之前。
- **控件**（全部落在既有的 `.sheet-row` / `.ghost` / `.muted` / `save-text` 样式里，**不新增弹窗体系**）：
  - `input#share-password[type=password]` + `button#share-make`（生成）；
  - `textarea#share-link[readonly]`（只读，整份复制）+ `button#share-copy`；
  - `p#share-note`（长度 / 拒绝原因，宿主原话）。
  - 消费侧：`div#share-open`（一行：`input#share-open-password[type=password]` + `button#share-open-go`），
    只在 URL 带 `share` 时显示，Enter 等同于点按钮。
- **这是页面上第一个 `<input>`**（今天一个都没有），所以：
  `type="password"`（**不在屏幕上显示密码**）、`autocomplete="new-password"`（生产）/
  `autocomplete="off"`（消费）、`aria-label` 一律给（读屏要能念出这是干什么的）。
- **失败一律留在窗口里**（`#share-note` / `#import-result`），不靠 2.6 秒就消失的提示条——
  与 W13 那三条命令同一取舍（`app.js:153-159`）。

---

## 8. 终端宿主的诚实答案

**URL 在终端里没有对应物。** 终端没有浏览器、没有地址栏、没有"打开一个链接"这个动作：

- **用途 A（跳层令牌）在终端不存在**：终端没有 URL 入口，也没有 `epoch` 门。
  它会得到的东西是**既有的** `--seed` / 无头模拟那条路，与本文无关。
- **用途 B（分享链接）在终端的等价物 = "把链接文本当成一次粘贴"**：
  终端已经有一条 `I`（导入：提示路径），而 `SAVE_TRANSFER_PLAN.md` §4.2 把它定位成
  "终端里的窗口就是一次路径提示"。
  **本文不在终端里做链接**：一条 1~2.5 KB 的 base64url 文本要人用键盘敲进去是不现实的，
  而"打开链接"这个动作在那里根本不存在。**诚实的处置是：终端继续用文件路径那条路**
  （导出到文件 → 传文件 → `I` 导入），并在本文里写明这是有意为之，不是漏做。
- 若将来真要给终端一条"密码保护的文件"，那也应该是**文件格式**的事（加密 `.neko-share` 文件），
  与 URL 无关——登记为欠账，不在这两刀里。

---

## 9. 存档兼容与状态归属

- **存档格式一个字节都不动**：`SaveSerializer.CurrentVersion` 保持 `1`，不加 `ISaveMigration`，
  `SaveData` 不加字段。链接里装的是**导出文本**，而导出文本在 1.10.0 就是存档本体的信封化
  （`SAVE_TRANSFER_PLAN.md` §6）。
- **不落任何新状态**：不写新文件、不建令牌库、不改 `saves/` 的布局。
  唯一的"状态"是链接自己携带的载荷（无状态设计 ⇒ 没有"令牌表坏了"这种新故障面）。
- **不新增存档槽位**、不动 `.bak` 语义（导入仍然由 `FileStorage.Write` 负责原子替换与备份）。

---

## 10. 版本判定（用证据，不靠感觉）

**结论：不升版本。这一刀全部落在宿主与前端，`[未发布]` 桶里见。**

证据与规矩：

1. 公开 API 快照**只扫 `NekoClicker.Core`**（`engine/tests/Program.cs:7-11` 的
   `--public-api` 模式从 `typeof(GameEngine).Assembly` 取；
   `engine/tests/PublicApiSnapshot.cs` 持有 `engine/core/PublicApi.txt`）。
   本文的新类型（`ShareLinkCodec` 等）落在 **`games/hosts/Web/ShareLink.cs`**——宿主代码
   （经**共享源码**与 `GameHost.cs` 一样进测试项目，见
   `engine/tests/NekoClicker.Core.Tests.csproj`），**不在快照射程里**
   ⇒ 快照逐字节不变（用 `tools/public-api.ps1` 复核，见 §13）。
   **为什么不放 `games/hosts/Shared/`**：那一层是"两个宿主都编同一份"的共用件
   （埋点 / 仓库根定位），而这套编解码**只有 Web 宿主用得到**（§8：终端不做链接），
   放进去只会让"谁在用这份代码"变得含糊。
2. 先例：调试门本身就是宿主功能，`WEB_DEBUG_GATE_PLAN.md` §8 明说
   「**不动 core**：这是宿主功能，不加公开成员、不升版本、不重生成快照」。
   W13/W14（导出/导入窗口）同样"只改宿主与前端 ⇒ 不升版本"
   （`SAVE_TRANSFER_PLAN.md` §9 的「公开表面：一个字节都没动」那一行）。
   本文与那两次是**同一个形状**。
3. `VERSIONING.md` 的规矩：minor 的条件是"公开 API 只增不改"。
   本文**没有公开 API 变化** ⇒ 它不是 minor；也不是 patch（不改内容/数值/文案），
   而是**宿主功能**——按仓库既有约定进 `CHANGELOG.md` 的 `[未发布]` 桶。
4. **反过来说清楚**：如果哪天要把这套编解码搬进 `engine/core`（比如终端也要用），
   那一次迁移**必须**按 minor 走完整个清单（升 `Directory.Build.props` 三处 →
   CHANGELOG 带日期条目 → **重新构建之后**再跑 `tools/public-api.ps1` → 「当前版本」字样扫一遍）。
   本文不做这件事，并且把这条留成一句明文，免得下一个人随手搬。

**用例数**：`TestCountDriftTests` 现在守着**文档里 11 处「当前用例数」**
（`engine/tests/TestCountDriftTests.cs:44-57` 那张 Slot 表：README ×3、engine/README、
games/README、STATUS ×2、`.github/workflows/ci.yml`、`tools/api-test.ps1`、`VERSIONING.md` §5、
`games/docs/ROADMAP.md`）。所以本刀新增 C# 守卫之后，这 **11 处必须改成测出来的真数**——
**不许估**（`TestCountDriftTests.cs:111-125` 会逐处点名）。

---

## 11. 守卫与判别力

新增 `engine/tests/ShareLinkTests.cs`（宿主的 `GameHost` 走共享源码进测试项目，
先例见 `engine/tests/NekoClicker.Core.Tests.csproj:49-65` 与 `SaveTransferHostTests`）：

| # | 用例 | 断言什么 |
|---|---|---|
| 1 | 往返 | 真实引擎 → `Export()` 原文 → `Protect` → `Open` → 文本**逐字节相同** → 再进一次 `SaveManager.Import` 成功 |
| 2 | 密码错 | `WrongPasswordOrTampered`；**磁盘与 `.bak` 逐字节不变**；会话一个字段不变 |
| 3 | 密文被改 | 翻一个密文字节 ⇒ tag 失败（同上"什么都没动"） |
| 4 | 头被改 | 翻 `kind` / 迭代数 / 版本字节 ⇒ 分别得到 `UnexpectedPurpose` / `IterationsOutOfRange` / `UnsupportedVersion`（证明 AAD 真的在起作用） |
| 5 | 迭代数区间 | 1 轮与 1 亿轮都被拒（下界/上界各自一条） |
| 6 | 链接超长 | 产出端拒绝并指路导出；消费端 `TooLarge`（各一条） |
| 7 | 载荷超大 | 压缩炸弹：解压读满上限即失败，**不**把内存吃掉 |
| 8 | 令牌：放行 | 正确钥匙 + 正确包 + 未过期 ⇒ `Kind=era`、`Era` 正确 |
| 9 | 令牌：拒绝 | 钥匙错 / 包不符 / 过期 / `Era` 越界，四条各自一条 |
| 10 | 口令太短 | `< 4` 拒绝，消息里说明"链接里只有这一层保护" |
| 11 | 宿主生产/消费 | `GameHost.CreateShareLinkAsync` → 另一台 `GameHost.ImportShareAsync` ⇒ 两边 `engine.Save()` 文本相同（**证明走的是既有 Import**） |
| 12 | 调试会话 | 跳层后的会话**不产链接、不收链接**（与 `ExportAsync`/`ImportAsync` 同一条规矩） |
| 13 | 长度实测 | 对真实 `saves/*.json` 量一次链接长度并**打印**；小存档断言在闸内，最大的那份按实测数字断言（§2 回填） |

**判别力（故障注入，至少证明一条真的会红）——已做三处，红色原文与结论见 §13 末表**：
- 把 AAD 从 `Encrypt/Decrypt` 里去掉（传 `null`）⇒ 第 4 条必须变红（否则 AAD 是装饰）；
- 删掉产出端那道链接长度闸 ⇒ 第 13 条必须变红；
- 前端"顺手试一次空密码" ⇒ `web-smoke` §28 那条必须变红。

**落地形态是 12 条**（上表第 1~12 条 + 长度实测；第 2 与第 3 条合成了
`ShareLink_WrongPasswordAndTamperedCiphertextBothFailLoudly`，"磁盘什么都没动"那一半落在
`WebHost_WrongSharePasswordTouchesNothing` 上）。

**前端与端到端**：
- `tools/web-smoke.mjs`：新增一节断言"生成的链接**不含密码**"、"只读框里是宿主回的原文"、
  "URL 带 `share` 时密码行出现且**不自动发命令**"、"消费成功后 `share` 参数从地址栏消失"、
  "`input` 是 `type=password`"。
- `tools/api-test.ps1`：真起宿主，用 `--mint-link` 铸一枚令牌 → 打真 URL → 200；
  错钥匙 → 403；过期 → 403；`?epoch=` 那条既有断言**保持不变**（回归底线）。

**真实文件不动**：整轮只读 `saves/`；所有用例指向临时目录；
结束时给出 size + mtime + sha256（`SAVE_TRANSFER_PLAN.md` §8/§9 那份表的格式）。

---

## 12. 分刀、本次范围、明确不做、留给人的问题

| 刀 | 内容 | 状态 |
|---|---|---|
| **一** | `ShareLinkCodec`（KDF/AEAD/压缩/base64url/大小闸）+ 跳层令牌（`?k=`、`NEKO_URL_KEY`、`--mint-link`）+ 分享链接（`shareLink` / `importShare` 两条命令 + `/api/share`… 实为 `/api/command` 的两条命令）+ 窗口里那一节 + C# 守卫 | **本刀** |
| 二 | `api-test.ps1` 的端到端段 + `web-smoke` 的新一节 + 启动横幅/帮助文案的版本字样 | 待做（§11 已列清单） |
| 三 | 若终端真的需要"密码保护的文件"：那是一件**文件格式**的事，另开 | 待做（不在本刀，理由见 §8） |

**明确不做**：

- **不做一次性令牌**（§4.4）。
- **不做"链接缩短"**（不引入任何外部服务；零依赖是硬约束）。
- **不做加密的文件格式**（§8 末尾）。
- **不把编解码搬进 `engine/core`**（§10 第 4 条；搬 = minor + 全套发布仪式）。
- **不改明文调试门、不改导入的九道闸**（§1.1、§1.2、§4.4）。

**留给人的问题**（这些是"要不要"的问题，不是"能不能"）：

1. **8000 字符这个闸合适吗？** 它来自"老 IE 2083 / 聊天工具截断 / 人肉复制"这三件事的折中，
   而不是某个规范。真实使用中若发现链接总是被截断，这个数该往下调（而不是往上）。
2. **密码偏短时的软提示够不够？** 现在只提示、不拒绝（`< 4` 才拒绝）。要不要直接拒到 8？
3. **令牌的默认有效期**：`--mint-link` 默认**不设过期**（不传 `--expires-hours` 就是永不过期）。
   要不要给一个默认值（比如 24 小时）？本文选了"默认不过期"，理由是它和明文门一样是
   "本机调试用"，而**过期需要人多做一次决定**；但这一条完全可以反过来。

---

## 13. 落地记录（第一刀，2026-10-06）

同一棵隔离 worktree（`.tmp/wt-share`，从 **`5863dc1`** 起）、分支
**`share-link-url-crypto`**。所有数字都是**测出来的**。

| 项 | 数字 | 怎么来的 |
|---|---|---|
| 基线（`5863dc1`） | `-Strict` **571** | 动手前在**同一棵 worktree** 里跑 `tools/build.ps1 -SkipWebSmoke` |
| 落地后 | **583**（+12） | 新增 `engine/tests/ShareLinkTests.cs` |
| 前端冒烟 | **208 → 224**（+16） | `tools/web-smoke.mjs` 新 §28；§24 那条"恰好两条实时区域"改成"恰好四条" |
| 公开表面 | **一个字节都没动** | 新代码全在 `games/hosts/Web/` 与 `wwwroot/`；`tools/public-api.ps1` 复核 |
| 版本 | **不升**（停在 `[未发布]`） | 判据见 §10：API 快照不变 ⇒ 不是 minor；宿主功能 ⇒ 按既有约定进 `[未发布]` |
| 真实存档长度 | 见 §2.1（1008~2258 字符） | 一次性探针跑真实 `saves/*.json`，跑完**已删除** |
| 仓库真实 `saves/` | **本轮一个字节都没写** | 全部用例指向 `/tmp` 下的临时目录；文件指纹见下 |

**落地的文件**：

- 新增 `games/hosts/Web/ShareLink.cs`（编解码 + 用途 + 失败类别 + 结果记录）；
- 新增 `engine/tests/ShareLinkTests.cs`（12 条守卫）；
- 改 `games/hosts/Web/GameHost.cs`（`CreateShareLinkAsync` / `ImportShareAsync`）；
- 改 `games/hosts/Web/Program.cs`（`NEKO_URL_KEY`、`--mint-link`、中间件的 `?k=` 分流、
  `shareLink` / `importShare` 两条命令、启动横幅多一行）；
- 改 `games/hosts/Web/wwwroot/{index.html,app.js,app.css}`（同一张 sheet 里新增一节 + 打开那一行）；
- 改 `engine/tests/NekoClicker.Core.Tests.csproj`（共享源码多一份 `ShareLink.cs`）；
- 改 `tools/web-smoke.mjs`（§28 + 桩件支持整条 URL 的 `replaceState` + 实时区域计数）。

**守卫怎么证明有判别力**（下面三处注入**都真跑过**，跑完还原；红色原文照抄）：

| 注入 | 结果（红色原文） |
|---|---|
| ① `AesGcm` 的 `associatedData` 传 `null`（AAD 变成装饰） | **2 红 / 12**：`ShareLink_HeaderMetadataIsAuthenticated` → `拦住它的必须是 tag。｜期望 <WrongPasswordOrTampered>，实际 <EraOutOfRange>。`（把用途字节从 `'s'` 改成 `'e'` 之后**居然解开了**，只是载荷里没有 `Era`——这正是"没有 AAD 时头是装饰品"的样子）；`ShareLink_RejectsOversizedLinksAndPayloads` → `期望 <TooLarge>，实际 <WrongPasswordOrTampered>`（手写线格式造的那段链接是**按真格式**认证的，所以第②个红其实是"格式漂移"的第二个证据） |
| ② 删掉产出端那道链接长度闸（`if (link.Length > MaxLinkChars)` → `if (false && …)`） | **1 红 / 12**：`ShareLink_FitsATypicalSaveAndRefusesAHugeOne` → `装不下的存档必须当场拒绝——截断的链接看起来像「密码不对」，会把人带去查不存在的问题。` |
| ③ `wwwroot/app.js` 的 `openIncomingShare` 里顺手发一次空密码（"自动尝试"最可能被写成的形状） | **3 红 / 224**（`tools/web-smoke.mjs`）：`不自动尝试任何密码：一条 importShare 都不许先发出去 — 不该自动试密码: 期望 0，实际 1`、`密码为空时按「导入」不发命令 — 空密码不该发命令: 期望 0，实际 1`、`导入…地址栏里的 share 被抹掉 — 发出去的 importShare 条数: 期望 1，实际 2` |

**没做的注入**（诚实边界：这几条**应该**有判别力，但本轮**没有**跑过，所以不写进上表）：
"把 `SaveManager.Import` 换成解密成功就 `Apply`"（该红的是"磁盘一个字节没动"那一条）、
"失败时也调 `stripShareFromUrl`"（该红的是 §28 的"地址栏保持不动"）。

**端到端手工探针（真宿主、真 HTTP；**不是**留下的守卫）**：在**另一台**临时宿主上
（端口 5399、临时存档根、`NEKO_URL_KEY='probe-url-key-2f9c'`、`NEKO_DEBUG_KEY` 未设）跑过一遍，
跑完按 PID 收掉自己的进程：

| 请求 | 实测 |
|---|---|
| `?package=lab&k=<真令牌>`（`--mint-link --package lab --era 2` 铸的，204 字符） | **200**：`gate=token`、`from=1`、`to=2`、`validRange 1..7`、`skipped=[…]`、`autosave="disabled"` |
| `?package=cafe&k=<同一枚>` | **403** `failure=ForeignPack`（正文里两个包名都在） |
| 令牌末 4 个字符改掉 | **403** `failure=WrongPasswordOrTampered` |
| `?k=not-a-link` | **403** `failure=NotALink` |
| `?package=lab&epoch=1&k=…`（两扇门同时带） | **400**「两扇独立的门，一次只能用一扇」 |
| `?package=lab&password=x&epoch=1`（**明文门**，`NEKO_DEBUG_KEY` 未设） | **403**「调试门未启用」——**既有那扇门的行为一个字节没变** |
| 跳层之后看存档根 | **一个文件都没有**（`autosave` 关着；进程收掉之后仍然是 0） |

**没验的（诚实边界，与 `SAVE_TRANSFER_PLAN` §8/§9 同一格式）**：

- **真人眼睛**：本机结构性起不了浏览器（`OPEN_WORK` §0.6）。两张新结果行的观感、
  密码框与「生成」按钮并排的排布、窄屏上那个额外的只读框——只有人能判。
- **`tools/api-test.ps1` 里没有留下端到端的那几条检查**：上面那张表是一次**手工探针**，
  跑完就没了。真宿主那 65 项既有检查验的是**明文**门与别的端点，所以"令牌门会跳层"
  这件事**在自动闸门里**今天只有 C# 那一层 + 共享源码的覆盖。把它做成常驻检查是**第二刀**。
- **`--mint-link` 的层号校验**在铸造时会对这个包**真实**的层数查一次，但那一条**没有**用例
  （它在 `Program.cs` 里，而 `Program.cs` 不进测试项目的共享源码清单——要验它就得像
  `api-test.ps1` 那样真起宿主，见上一条）。
