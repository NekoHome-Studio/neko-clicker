// NekoClicker Web 前端。
//
// 这个文件的全部职责：**把服务端推来的快照画出来，把玩家的操作发回去**。
// 它不做任何游戏计算——价格、能不能买得起、进度百分比、格式化后的数字，
// 全都是服务端在 GameSnapshot 里算好的。这不是"偷懒"，是这个项目的架构不变量：
// 浏览器只渲染，引擎只有一个主人（见 GameHost.cs 的注释）。
//
// 传输协议（SnapshotProtocol.cs）：
//   GET  /api/stream    SSE。第一帧一定是全量，之后是增量信封；每 30 秒一次全量对账。
//   POST /api/command   所有会改状态的动作。
//
// 增量信封的形态：
//   { kind: "delta", seq, changed: { cookies: 1.2e13, cookiesText: "12.0 万亿", ... } }
//   { kind: "full",  seq, snapshot: { ...完整快照... } }
//
// 所以本地的 `state` 就是一份"累积出来的快照"。这一点有服务端的契约测试守着
// （WebSnapshotProtocolTests.DeltaMergedIntoSnapshot_EqualsTheFullSnapshot）：
// 累积结果必须与直接取一份全量逐字节相同。

const $ = (selector) => document.querySelector(selector);

const params = new URLSearchParams(location.search);
const packageId = params.get("package");

/** 本地状态：从全量帧开始，之后靠增量帧累积。 */
let state = null;
let seq = 0;
let connected = false;

/**
 * 玩家已经按下"收下"了吗。<b>这只是本地的一格乐观更新</b>（收下到下一帧回来之间
 * 不该再闪一下弹窗），真正的状态在引擎里——见 `renderOffline` 的注释。
 */
let offlineDismissed = false;

/**
 * 显示用的数字。<b>刻意不做"追赶式插值"</b>，而是按当前每秒产量持续累加。
 *
 * 为什么不能"追目标"：服务端每 250ms 才推一帧，追到之后剩下的 150ms 完全静止，
 * 观感就是一跳一跳——而挂机游戏的核心体验恰恰是"看着数字在涨"。
 * 所以这里改成**每帧按 cps 累加**，服务端来帧时只做一次对账。
 *
 * 不做瞬时吸附的理由：吸附会在"服务端比本地略慢"时让数字倒退一格，那比不精确更难看。
 * 只在本地明显领先时收敛一下（说明有买入之类的状态变化），于是显示值永远不会倒退，
 * 而且最迟 250ms 内会被服务端的真实速率纠正。
 */
let shownCookies = 0;
let lastTickAt = performance.now();
let lastFrameAt = 0; // written but never read: the throttle/staleness indicator it was meant for was never finished

// ---------------------------------------------------------------- 传输

const streamUrl = `/api/stream${packageId ? `?package=${encodeURIComponent(packageId)}` : ""}`;
const commandUrl = `/api/command${packageId ? `?package=${encodeURIComponent(packageId)}` : ""}`;

function connect() {
  const source = new EventSource(streamUrl);

  source.onopen = () => {
    connected = true;
    render();
  };

  source.onerror = () => {
    // EventSource 自带重连，这里只反映状态。重连后的第一帧仍是全量，所以状态不会漂。
    connected = false;
    render();
  };

  source.onmessage = (event) => {
    const frame = JSON.parse(event.data);
    if (frame.seq) seq = frame.seq;

    if (frame.kind === "full") {
      state = frame.snapshot;
      shownCookies = state.cookies; // 全量帧直接对齐：新连接 / 每 30 秒对账一次，从这里重新起算
    } else if (frame.kind === "delta") {
      if (typeof frame.changed.cookies === "number" && shownCookies > frame.changed.cookies) {
        shownCookies = frame.changed.cookies; // 只收敛、不吸附（吸附会看到倒退）
      }
      Object.assign(state, frame.changed);
    } else if (frame.kind === "event") {
      toast(frame.payload?.message ?? frame.name);
      return;
    }

    lastFrameAt = performance.now();
    recomputeAffordable();
    render();
  };
}

/**
 * 按服务端的口径重算 canAfford。
 *
 * 为什么前端要算这一步：`canAfford` 是**派生量**（`price <= 钱包`），服务端刻意不把它放进
 * 增量帧——否则 48 条升级各自的 canAfford 会随金钱增长不停翻转，每次推送都得带上整个
 * 28 KB 的 upgrades 数组，增量协议等于白做（实测：全量 47 KB vs 增量 28 KB）。
 *
 * 所以权威值只在全量帧里来，两帧之间由这里补。前端提前点亮一个按钮是可接受的：
 * 服务端仍然校验每一次购买，早点亮最多换来一句"钱不够"，不会产生非法状态。
 * 这条规则与 SnapshotProtocol.RecomputeDerived 是同一份口径，改动要同时改。
 */
function recomputeAffordable() {
  const wallet = state.cookies ?? 0;
  const premium = state.prestigeChips ?? 0;
  const rows = [...(state.buildings ?? []), ...(state.upgrades ?? [])];

  for (const row of rows) {
    // 哪一行花哪个钱包由服务端说了算（`usesPrestigeCurrency`）。
    // 这里曾经写的是 `row.currency === 1`——枚举序数，成员顺序一变就静默错位：
    // 买得起的行灰着、买不起的行亮着，而控制台里一个错都没有。
    const budget = row.usesPrestigeCurrency ? premium : wallet;
    const price = row.price ?? row.batchPrice ?? Infinity;
    row.canAfford = Boolean(row.isUnlocked) && !row.isMaxed && price <= budget;
  }
}

/** 发一条命令。乐观更新的部分刻意不做——等下一帧回来，状态永远以服务端为准。 */
async function send(type, extra = {}) {
  try {
    const response = await fetch(commandUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ type, ...extra }),
    });
    const result = await response.json();
    if (!result.ok && result.message) toast(result.message, true);
    return result;
  } catch (error) {
    toast(`连不上宿主：${error.message}`, true);
    return { ok: false };
  }
}

// ---------------------------------------------------------------- 渲染

function render() {
  if (!state) return;

  document.title = `${state.title} · NekoClicker`;
  $("#title").textContent = state.title;
  $("#currency-name").textContent = state.currencyName;
  $("#click-action").textContent = state.clickActionName;
  $("#click-icon").textContent = state.currencyIcon;
  $("#cps").textContent = `${state.cpsText}/s`;
  $("#per-click").textContent = `每次 +${state.clickPowerText}`;
  $("#achievements").textContent = `${state.achievementCount} / ${state.achievementTotal}`;
  $("#status-dot").className = connected ? "dot ok" : "dot bad";
  $("#status-text").textContent = connected ? "已连接" : "重连中…";

  renderGoldenCookie();
  renderBuffs();
  renderEra();
  renderBuildings();
  renderUpgrades();
  renderPermanent();
  renderBatch();
  renderChoices();
  reportChoicesShown();
  renderCodex();
  renderAchievements();
  renderNotifications();
  renderOffline();
}

/**
 * 离线收益弹窗（`GameSnapshot.offline`）。
 *
 * 它是**一次性事件**，不是每秒都在的数值：读档补发之后服务端一直带着它，直到有人
 * "看过了"。所以这里刻意不做任何本地去重（localStorage / sessionStorage 都不用）——
 * 去重状态放在前端一定会错，两种写法各有各的错法：
 *   · 拿数值当指纹（时长 + 补发量）：两次离线完全可能一模一样（都离线到上限、产量也没变），
 *     于是第二次永远不弹；
 *   · 每个标签页记一次：刷新不弹了，但新开一个标签页又弹，而"关掉页面明天再来"正是这游戏的主玩法。
 * 真相只有一份，在引擎里（`PendingOfflineProgress`）；"收下"就是让服务端把它清掉，
 * 于是刷新、重连、换标签页拿到的那份快照里都没有它。
 */
function renderOffline() {
  const layer = $("#offline");
  const report = state.offline;
  const show = Boolean(report) && !offlineDismissed;

  layer.classList.toggle("hidden", !show);
  if (!show) return;

  const text = $("#offline-text");
  text.textContent = "";
  const head = document.createElement("span");
  head.textContent = "你不在的这段时间里，它们自己涨了 ";
  const amount = document.createElement("b");
  amount.textContent = `${state.currencyIcon} ${report.cookiesText} ${state.currencyName}`;
  const tail = document.createElement("span");
  tail.textContent = "。";
  text.append(head, amount, tail);

  // 被上限截断时必须说清楚"只补了这么多"，否则玩家会以为自己少拿了钱。
  $("#offline-note").textContent = report.wasCapped
    ? `离线收益有上限：按上限补了 ${report.durationText}，实际离开了 ${duration(report.elapsedSeconds)}。`
    : `补的是 ${report.durationText} 的产量。`;
}

/** 收下：先本地收起来（下一帧回来之前不该闪），再让宿主把那份状态清掉。 */
function dismissOffline() {
  offlineDismissed = true;
  $("#offline").classList.add("hidden");
  send("dismissOffline");
}

/**
 * 纪元面板。这是主循环的出口：只有一个按钮（决策 R1——转生与推进纪元是同一次操作，
 * 否则玩家能在同一层无限刷），未完成时置灰并显示卡在哪一项。
 *
 * `era.progress` 由服务端算好（`AllCondition` 返回**最落后**的子条件），
 * 所以这里的进度条天然就是"最拖后腿的那一项"。
 */
function renderEra() {
  const host = $("#era");
  const era = state.era;

  if (!era) {
    host.classList.add("hidden");
    return;
  }

  host.classList.remove("hidden");
  $("#era-name").textContent = `${era.icon} ${era.name}`;
  $("#era-index").textContent = `第 ${era.index} / ${era.total} 层`;
  $("#era-theme").textContent = era.theme;
  $("#era-bar").style.width = `${Math.round(era.progress * 100)}%`;

  const button = $("#ascend");
  if (era.canAdvance) {
    button.disabled = false;
    button.textContent = era.isFinalEra ? "走到尽头" : era.nextName ? `舍一命 → ${era.nextName}` : "舍一命";
    $("#era-reason").textContent = era.chipsOnAdvance > 0
      ? `可得 ${state.prestigeCurrencyIcon} ${Math.round(era.chipsOnAdvance)} ${state.prestigeCurrencyName}`
      : "本层还不足以提升等级，但故事继续。";
  } else {
    button.disabled = true;
    button.textContent = era.nextIndex ? "还不能舍命" : "已是最后一层";
    $("#era-reason").textContent = era.blockedReason ?? "";
  }
}

/**
 * "玩家看过待答表态了吗"——1.5.0 起这是结局能否落定的**唯一**条件。
 *
 * 引擎侧对应 GameEngine.MarkPendingChoicesShown()，它换掉了以前那段 30 模拟秒的宽限
 * （定时 → 条件）。判据只有一条：**「表态」面板此刻真的显示在屏幕上**
 * （选中的是那个 tab、页面不在后台、并且真的有内容）。
 *
 * 为什么不用标签栏上那个角标当判据：角标只写条数，看见"表态 1"并不等于看见了谁在问、
 * 问什么、有哪些选项——而结局只会落定一次，宁可晚，也不能把"没看到"记成"看到过"。
 * 反过来，"从不打开这个面板就永远拿不到结局"正是这次改动有意接受的性质（CHANGELOG 1.5.0）。
 *
 * 只报一次：报成功的 id 记在这里，之后不再重复发。send 失败时**不记账**，
 * 下一帧会重试——"发丢了却记成发过"会让结局永远落不下来，而且一点痕迹都没有。
 */
const shownChoices = new Set();

function reportChoicesShown() {
  if (!state || document.hidden) return;

  const panel = document.querySelector('[data-panel="choices"]');
  if (!panel || panel.classList.contains("hidden")) return;

  const fresh = (state.pendingChoices ?? [])
    .map((choice) => choice.id)
    .filter((id) => !shownChoices.has(id));
  if (fresh.length === 0) return;

  send("choicesShown").then((result) => {
    if (!result?.ok) return;
    for (const id of fresh) shownChoices.add(id);
  });
}

/** 表态：待答选择 + 立场轴。选择不阻塞游戏（决策 R6），所以它是一张待办卡片而不是弹窗。 */
function renderChoices() {
  const pending = state.pendingChoices ?? [];
  const badge = $("#choice-badge");

  const hasPending = pending.length > 0;

  if (hasPending) {
    badge.textContent = pending.length;
    badge.classList.remove("hidden");
  } else {
    badge.classList.add("hidden");
  }

  // 「表态」这一页只在真有东西要答的时候存在：标签页的出现本身就是提示，
  // 玩家不必先学会去点一个总是空着的页签。这与引擎那侧是配套的——结局要等玩家
  // 真的"看过"这些表态才允许落定，所以"让它被看见"这件事必须由界面负责。
  const choicesTab = document.querySelector('button[data-tab="choices"]');
  if (choicesTab) {
    const appearing = hasPending && choicesTab.classList.contains("hidden");
    choicesTab.classList.toggle("hidden", !hasPending);
    if (appearing) {
      // 冒出来那一下强调一次；否则"多了个页签"很容易被完全错过。
      choicesTab.classList.add("urgent");
      setTimeout(() => choicesTab.classList.remove("urgent"), 2600);
    }
    if (!hasPending) choicesTab.classList.remove("urgent");
  }

  // 玩家正看着表态页、而表态没了（答完了或这一层结束）→ 换到一个不会突然变空的页。
  if (!hasPending) {
    const choicesPanel = document.querySelector('[data-panel="choices"]');
    if (choicesPanel && !choicesPanel.classList.contains("hidden")) selectTab("upgrades");
  }

  const host = $("#choices");
  host.textContent = "";

  if (pending.length === 0) {
    const note = document.createElement("p");
    note.className = "muted";
    note.textContent = (state.stances ?? []).length > 0
      ? "现在没有待答的表态。表态挂在这一层上，舍命之后就遇不到了。"
      : "这个内容包没有表态机制。";
    host.append(note);
  }

  for (const choice of pending) {
    const card = document.createElement("div");
    card.className = "choice";

    const speaker = document.createElement("div");
    speaker.className = "choice-speaker";
    speaker.textContent = `🗣 ${choice.speaker}`;
    card.append(speaker);

    const prompt = document.createElement("p");
    prompt.className = "choice-prompt";
    prompt.textContent = choice.prompt;
    card.append(prompt);

    const options = document.createElement("div");
    options.className = "choice-options";
    for (const option of choice.options) {
      const button = document.createElement("button");
      button.className = "choice-option";

      const label = document.createElement("b");
      label.textContent = option.label;
      button.append(label);

      if (option.stanceName) {
        const stance = document.createElement("span");
        stance.className = "choice-stance";
        stance.textContent = `${option.stanceIcon} ${option.stanceName} +${option.weight}`;
        button.append(stance);
      }
      if (option.effectSummary) {
        const effect = document.createElement("span");
        effect.className = "choice-effect";
        // 效果摘要是派生文本、不进增量帧，但全量帧里有；缺失时不显示这一行
        effect.textContent = option.effectSummary;
        button.append(effect);
      }

      button.addEventListener("click", () => send("answer", { id: choice.id, optionId: option.id }));
      options.append(button);
    }

    card.append(options);
    host.append(card);
  }

  // 立场轴
  const axis = $("#stances");
  axis.textContent = "";
  const stances = state.stances ?? [];
  if (stances.length > 0) {
    const title = document.createElement("h3");
    title.textContent = "立场轴";
    axis.append(title);

    const total = stances.reduce((sum, s) => sum + s.weight, 0);
    $("#stance-summary").textContent = state.dominantStanceId
      ? `主导：${stances.find((s) => s.isDominant)?.name ?? "—"}`
      : total === 0 ? "还没有表态" : "还没有占上风的立场";

    for (const stance of stances) {
      const row = document.createElement("div");
      row.className = stance.isDominant ? "stance dominant" : "stance";
      row.innerHTML = "";

      const name = document.createElement("span");
      name.className = "stance-name";
      name.textContent = `${stance.icon} ${stance.name}${stance.isDominant ? " ▸" : ""}`;

      const bar = document.createElement("i");
      bar.style.width = `${Math.round(stance.share * 100)}%`;

      const weight = document.createElement("span");
      weight.className = "stance-weight";
      weight.textContent = `${stance.weight}`;

      row.append(name, bar, weight);
      if (stance.costText) row.title = stance.costText;
      axis.append(row);
    }

    if (state.ending) {
      const ending = document.createElement("div");
      ending.className = "ending";
      ending.innerHTML = "";
      const h = document.createElement("h3");
      h.textContent = `${state.ending.icon} 结局：${state.ending.name}`;
      const p = document.createElement("p");
      p.textContent = state.ending.text;
      ending.append(h, p);
      axis.append(ending);
    }
  }
}

/** 图鉴：按剧情线分组，未读到的显示 ??? 但保留释放条件与进度。 */
function renderCodex() {
  const host = $("#codex");
  const codex = state.codex;
  host.textContent = "";

  if (!codex) {
    $("#codex-count").textContent = "";
    const note = document.createElement("p");
    note.className = "muted";
    note.textContent = "这个内容包没有剧情条目。";
    host.append(note);
    return;
  }

  $("#codex-count").textContent = `${codex.totalUnlocked} / ${codex.totalEntries}`;

  for (const line of codex.storylines) {
    const group = document.createElement("div");
    group.className = "storyline";

    const head = document.createElement("h3");
    head.textContent = `${line.icon} ${line.name}`;
    const count = document.createElement("span");
    count.className = "muted";
    count.textContent = ` ${line.unlocked}/${line.total}`;
    head.append(count);
    group.append(head);

    for (const entry of line.entries) {
      const row = document.createElement("div");
      row.className = entry.unlocked ? "lore" : "lore locked";

      const title = document.createElement("b");
      title.textContent = `${entry.icon} ${entry.title}`;
      row.append(title);

      if (entry.unlocked) {
        const body = document.createElement("p");
        body.textContent = entry.body;
        row.append(body);
      } else {
        const hint = document.createElement("p");
        hint.className = "lore-hint";
        hint.textContent = `🔒 ${entry.revealHint}${entry.progressText ? `（${entry.progressText}）` : ""}`;
        row.append(hint);
      }

      group.append(row);
    }

    host.append(group);
  }
}

function renderAchievements() {
  const host = $("#achievement-list");
  const rows = state.achievements ?? [];
  $("#achievement-count").textContent = `${state.achievementCount} / ${state.achievementTotal}`;
  host.textContent = "";

  for (const achievement of rows) {
    const row = document.createElement("div");
    row.className = achievement.unlocked ? "card ach unlocked" : "card ach";

    const name = document.createElement("span");
    name.className = "name";
    name.textContent = `${achievement.icon} ${achievement.name}`;
    row.append(name);

    const desc = document.createElement("span");
    desc.className = "share";
    desc.textContent = achievement.description;
    row.append(desc);

    host.append(row);
  }
}

/**
 * 日志（`GameSnapshot.notifications`）。
 *
 * 引擎把"发生了什么"一路推到这里：买东西、成就解锁、金猫出现、舍命、存档失败……
 * 在此之前前端**一处都没画过它**，这些消息只活在服务端的列表里。
 *
 * 刻意只做到"渲染出来"：**没有未读游标**——那需要新增状态与公开字段，
 * 是另一件事（见 STATUS §8.2）。排序取新在上：服务端按发生顺序 append，
 * 玩家想先看到的显然是最近那条。
 *
 * `kind` 是**数字**（宿主的 JsonSerializerOptions 没有开枚举字符串转换器，见 SnapshotProtocol.cs），
 * 顺序与 `NotificationKind` 的声明一致：0=Info / 1=Success / 2=Warning / 3=Rare。
 */
const NOTE_KINDS = ["info", "success", "warning", "rare"];

function renderNotifications() {
  const host = $("#log");
  const notes = [...(state.notifications ?? [])].reverse();

  $("#log-count").textContent = notes.length > 0 ? `最近 ${notes.length} 条` : "";
  host.textContent = "";

  if (notes.length === 0) {
    const empty = document.createElement("p");
    empty.className = "muted";
    empty.textContent = "还没有消息。买东西、解锁成就、抓到金猫都会记在这里。";
    host.append(empty);
    return;
  }

  for (const item of notes) {
    const row = document.createElement("div");
    row.className = `note ${NOTE_KINDS[item.kind] ?? "info"}`;

    const icon = document.createElement("span");
    icon.className = "note-icon";
    icon.textContent = item.icon || "•";
    row.append(icon);

    const text = document.createElement("span");
    text.className = "note-text";
    text.textContent = item.message;
    row.append(text);

    const time = document.createElement("span");
    time.className = "note-time";
    time.textContent = relativeGameTime(state.playTimeSeconds - item.timestamp);
    row.append(time);

    host.append(row);
  }
}

/** 把"多少游戏秒之前"说成人话。`playTimeSeconds` 只增不减（舍命也不清零），所以差值不会为负。 */
function relativeGameTime(seconds) {
  if (!(seconds >= 1)) return "刚刚";
  if (seconds < 60) return `${Math.floor(seconds)} 秒前`;
  if (seconds < 3600) return `${Math.floor(seconds / 60)} 分钟前`;
  if (seconds < 86400) return `${Math.floor(seconds / 3600)} 小时前`;
  return `${Math.floor(seconds / 86400)} 天前`;
}

/**
 * 一段时长本身（`3h 00m` / `12m 30s` / `4.2s`）——与引擎 `NumFormat.Duration` 同口径，
 * 连进位都照抄（浮点累加会算出 59.9999 分钟，不处理就会出现 "24m 60s" 这种像 bug 的读数）。
 *
 * 为什么前端也要有一份：服务端只给"计入收益的那段"（受上限截断），而弹窗在说明
 * "被截断了"时还要说出**实际离开了多久**，那个数只有快照里的 `elapsedSeconds`。
 */
function duration(seconds) {
  if (!(seconds > 0)) return "0s";
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  if (seconds < 3600) {
    let minutes = Math.floor(seconds / 60);
    let rest = Math.round(seconds - minutes * 60);
    if (rest >= 60) { rest -= 60; minutes += 1; }
    return `${minutes}m ${String(rest).padStart(2, "0")}s`;
  }
  if (seconds < 86400) {
    let hours = Math.floor(seconds / 3600);
    let minutes = Math.round((seconds - hours * 3600) / 60);
    if (minutes >= 60) { minutes -= 60; hours += 1; }
    return `${hours}h ${String(minutes).padStart(2, "0")}m`;
  }
  let days = Math.floor(seconds / 86400);
  let hours = Math.round((seconds - days * 86400) / 3600);
  if (hours >= 24) { hours -= 24; days += 1; }
  return `${days}d ${hours}h`;
}

/**
 * 每帧把显示值按当前每秒产量往前推，然后重画。
 *
 * 与"追一个每 250ms 才动的目标"相比，这样每个帧都有变化，数字是**连续在跑**的；
 * 服务端来帧只负责纠正速率，不负责制造位移。
 */
function animate(now) {
  const elapsed = Math.min(0.25, Math.max(0, (now - lastTickAt) / 1000));
  lastTickAt = now;

  if (state) {
    const rate = state.cookiesPerSecond ?? 0;
    if (rate > 0) shownCookies += rate * elapsed;

    // 本地不能比服务端领先太多（可能刚买了东西 / 切了包）。
    // 超过"两帧的量"就按比例收敛，避免长时间虚高；正常挂机时这个分支不会触发。
    const server = state.cookies ?? 0;
    const tolerance = Math.max(1e-6, rate * 0.5);
    if (shownCookies > server + tolerance) {
      shownCookies = server + (shownCookies - server) * 0.5;
    } else if (shownCookies < server) {
      shownCookies = server; // 服务端更靠前（点击、离线补发）：直接跟上，不倒退
    }

    $("#cookies").textContent = formatCookies(shownCookies);
  }

  requestAnimationFrame(animate);
}

/**
 * 大数格式化。与服务端 <c>NumFormat</c> 的口径一致（short scale + 中文单位），
 * 但**不用服务端文本当模板**——那正是上一版跳动的根因：模板只在"数量级没变"时可用，
 * 而数字每跨一个数量级（999 → 1000）就会卡住一拍再跳一下。
 *
 * 精度按量级递减：观感上"12.34 千"和"12.3 千"没有区别，但少一位就少一次无意义的重绘。
 */
function formatCookies(value) {
  const units = ["", "千", "百万", "十亿", "万亿", "千万亿", "百京", "千京"];
  let index = 0;
  let scaled = Math.abs(value);

  while (scaled >= 1000 && index < units.length - 1) {
    scaled /= 1000;
    index++;
  }

  if (index === 0) return Math.floor(value).toLocaleString("zh-CN");

  const digits = scaled < 10 ? 3 : scaled < 100 ? 2 : 1;
  return `${value < 0 ? "-" : ""}${scaled.toFixed(digits)} ${units[index]}`;
}

function renderGoldenCookie() {
  const host = $("#golden");
  const live = state.goldenCookies ?? [];
  host.textContent = "";

  if (live.length === 0) {
    host.classList.add("hidden");
    return;
  }

  host.classList.remove("hidden");
  for (const cat of live) {
    const button = document.createElement("button");
    button.className = "golden-cat";
    button.textContent = "🐱";
    button.title = `抓住它！（还剩 ${cat.remainingSeconds.toFixed(1)} 秒）`;
    button.style.left = `${Math.min(92, Math.max(2, cat.x * 100))}%`;
    button.style.top = `${Math.min(88, Math.max(2, cat.y * 100))}%`;
    button.addEventListener("click", () => send("grabGolden", { id: cat.instanceId }));
    host.append(button);
  }
}

function renderBuffs() {
  const host = $("#buffs");
  const buffs = state.buffs ?? [];
  host.textContent = "";

  if (buffs.length === 0) {
    host.classList.add("hidden");
    return;
  }

  host.classList.remove("hidden");
  for (const buff of buffs) {
    const chip = document.createElement("span");
    chip.className = buff.isDebuff ? "chip bad" : "chip good";
    chip.textContent = `${buff.icon} ${buff.name}${buff.stacks > 1 ? ` ×${buff.stacks}` : ""} ${Math.ceil(buff.remainingSeconds)}s`;
    chip.title = buff.description;
    host.append(chip);
  }
}

function renderBuildings() {
  const host = $("#buildings");
  const rows = (state.buildings ?? []).filter((b) => b.isVisible);
  host.textContent = "";

  for (const building of rows) {
    const card = document.createElement("button");
    card.className = "card";
    if (!building.isUnlocked) card.classList.add("locked");
    else if (building.canAfford) card.classList.add("affordable");

    const name = document.createElement("span");
    name.className = "name";
    name.textContent = building.isUnlocked ? `${building.icon} ${building.name}` : `🔒 ${building.name}`;
    card.append(name);

    const owned = document.createElement("span");
    owned.className = "owned";
    owned.textContent = building.owned;
    card.append(owned);

    const price = document.createElement("span");
    price.className = "price";
    if (building.isUnlocked) {
      const bulk = building.batchAmount > 1 ? ` ×${building.batchAmount}` : "";
      price.textContent = `${state.currencyIcon} ${number(building.batchPrice)}${bulk}`;
    } else {
      price.textContent = `${building.unlockHint}（${percent(building.unlockProgress)}）`;
    }
    card.append(price);

    if (building.isUnlocked && building.owned > 0) {
      const share = document.createElement("span");
      share.className = "share";
      share.textContent = `${number(building.cpsContribution)}/s · 占 ${percent(building.cpsShare)}`;
      card.append(share);
    }

    if (building.isUnlocked) {
      card.addEventListener("click", () => send("buy", { id: building.id }));
    } else {
      card.disabled = true;
    }

    host.append(card);
  }
}

/**
 * 普通升级列表。**永久线不在这里**——它们有自己的面板（见 renderPermanent），
 * 否则一条线会被两处渲染，而且"哪个钱包付钱"会在同一张列表里混着。
 *
 * 这里曾经有一个 `slice(0, 40)`：咖啡馆包 48 条升级，末尾几条会**无声地**少掉。
 * 那是这一层最不该有的失败形态（数据都在，界面上什么都不说），所以直接去掉——
 * 四十来张卡片对浏览器不是负担。
 */
function renderUpgrades() {
  const host = $("#upgrades");
  const rows = (state.upgrades ?? []).filter((u) => u.isVisible && !u.isPermanent);
  const available = rows.filter((u) => u.isAvailable);
  host.textContent = "";

  $("#upgrade-count").textContent = available.length > 0 ? `${available.length} 项可买` : "暂无可买";

  for (const upgrade of rows) {
    const card = document.createElement("button");
    card.className = "card compact";
    if (upgrade.isMaxed) card.classList.add("maxed");
    else if (!upgrade.isUnlocked) card.classList.add("locked");
    else if (upgrade.canAfford) card.classList.add("affordable");

    const name = document.createElement("span");
    name.className = "name";
    name.textContent = upgrade.isUnlocked ? `${upgrade.icon} ${upgrade.name}` : `🔒 ${upgrade.name}`;
    if (upgrade.owned > 0) name.textContent += ` ×${upgrade.owned}`;
    card.append(name);

    const price = document.createElement("span");
    price.className = "price";
    // 图标也由服务端给（`currencyIcon`），不再按枚举序数在前端两选一
    price.textContent = upgrade.isUnlocked ? `${upgrade.currencyIcon} ${number(upgrade.price)}` : percent(upgrade.unlockProgress);
    card.append(price);

    if (upgrade.effectSummary) {
      const effect = document.createElement("span");
      effect.className = "effect";
      effect.textContent = upgrade.effectSummary;
      card.append(effect);
    }

    if (upgrade.isUnlocked && !upgrade.isMaxed) {
      card.addEventListener("click", () => send("upgrade", { id: upgrade.id }));
    } else {
      card.disabled = true;
    }

    host.append(card);
  }
}

/**
 * 永久升级线：转生之后仍然保留的那条线（服务端 `isPermanent`），也是转生货币唯一的去处。
 *
 * 两条刻意的设计：
 *   · **不服从 `hiddenUntilUnlocked`**（与「升级」面板相反）。这条线的存在本身要在玩家
 *     第一次舍命之前就能看见——否则他不知道该攒什么，而"舍一命换转生货币、再拿它买永久升级"
 *     正是这游戏的主循环。锁着的行照样列出来，带解锁条件与进度（与图鉴里未读条目的做法一致）。
 *   · 钱包、货币名、货币图标全部取服务端字段（`usesPrestigeCurrency` / `currencyName` /
 *     `currencyIcon`），前端不认识 `UpgradeCurrency` 这种东西。
 *
 * 内容包没有这条线时（`isPermanent` 一行都没有），整个标签会被隐藏；留在「永久」标签上时会
 * 自动退回「升级」，免得停在一张空面板上。
 */
function renderPermanent() {
  const rows = (state.upgrades ?? []).filter((u) => u.isPermanent);
  const tab = $("#tab-permanent");
  const panel = document.querySelector('[data-panel="permanent"]');

  tab.classList.toggle("hidden", rows.length === 0);
  if (rows.length === 0) {
    if (panel && !panel.classList.contains("hidden")) selectTab("upgrades");
    return;
  }

  const owned = rows.filter((u) => u.owned > 0).length;
  const affordable = rows.filter((u) => u.isAvailable && u.canAfford).length;
  const icon = state.prestigeCurrencyIcon ?? "";
  const name = state.prestigeCurrencyName ?? "";

  const badge = $("#permanent-badge");
  badge.textContent = affordable;
  badge.classList.toggle("hidden", affordable === 0);

  $("#permanent-count").textContent = `已买 ${owned} / ${rows.length} 项`;
  $("#permanent-wallet").textContent = `${icon} ${number(state.prestigeChips ?? 0)} ${name}`;

  const preview = state.prestige ?? {};
  $("#permanent-hint").textContent = preview.canAscend
    ? `现在舍一命可得 ${icon} ${number(preview.chipsOnAscend ?? 0)} ${name}；这条线上的东西转生之后仍然在。`
    : `这条线上的东西转生之后仍然在。${name}来自「舍一命」`
      + `${state.era?.progressText ? `——本层进度 ${state.era.progressText}` : "（完成本层主线即可）"}。`;

  const host = $("#permanent");
  host.textContent = "";

  for (const row of rows) {
    const card = document.createElement("button");
    card.className = "card compact";
    if (row.isMaxed) card.classList.add("maxed");
    else if (!row.isUnlocked) card.classList.add("locked");
    else if (row.canAfford) card.classList.add("affordable");

    const label = document.createElement("span");
    label.className = "name";
    label.textContent = row.isUnlocked ? `${row.icon} ${row.name}` : `🔒 ${row.name}`;
    if (row.maxPurchases > 1) label.textContent += ` ${row.owned} / ${row.maxPurchases}`;
    else if (row.owned > 0) label.textContent += " ✔";
    card.append(label);

    const price = document.createElement("span");
    price.className = "price";
    price.textContent = row.isUnlocked ? `${row.currencyIcon} ${number(row.price)}` : percent(row.unlockProgress);
    card.append(price);

    const note = document.createElement("span");
    note.className = "effect";
    note.textContent = row.isUnlocked ? row.description : `${row.unlockHint}（${percent(row.unlockProgress)}）`;
    card.append(note);

    if (row.isUnlocked && !row.isMaxed) {
      card.addEventListener("click", () => send("upgrade", { id: row.id }));
    } else {
      card.disabled = true;
    }

    host.append(card);
  }
}

function renderBatch() {
  const modes = ["buy1", "buy10", "buy100", "buymax"];
  const labels = { buy1: "×1", buy10: "×10", buy100: "×100", buymax: "买满" };
  const current = (state.mode ?? "").toLowerCase();

  const host = $("#batch");
  host.textContent = "";
  for (const mode of modes) {
    const button = document.createElement("button");
    button.textContent = labels[mode];
    // mode 是个枚举名（Buy1 / Buy10 …），服务端推来的是 camelCase
    if (current === mode) button.className = "active";
    button.addEventListener("click", () => send("mode", { mode }));
    host.append(button);
  }
}

// ---------------------------------------------------------------- 小工具

/** 数值格式化：前端只做"大数缩写"，与服务端 NumFormat 的口径一致即可。 */
function number(value) {
  if (value === undefined || value === null) return "—";
  if (value < 1000) return value.toFixed(0);
  const units = ["", "千", "百万", "十亿", "万亿", "千万亿", "百京"];
  let index = 0;
  let scaled = value;
  while (scaled >= 1000 && index < units.length - 1) {
    scaled /= 1000;
    index++;
  }
  return `${scaled.toFixed(scaled < 10 ? 2 : scaled < 100 ? 1 : 0)}${units[index]}`;
}

function percent(ratio) {
  if (ratio === undefined || ratio === null) return "—";
  return `${Math.round(ratio * 100)}%`;
}

let toastTimer = null;
function toast(message, bad = false) {
  const host = $("#toast");
  host.textContent = message;
  host.className = bad ? "toast bad show" : "toast show";
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => host.classList.remove("show"), 2600);
}

// ---------------------------------------------------------------- 事件

$("#big-cat").addEventListener("click", () => send("click"));
$("#save").addEventListener("click", () => send("save"));
$("#ascend").addEventListener("click", () => send("ascend"));

// 离线弹窗：按钮、点遮罩空白处、Esc、空格都能收下它。
// 遮罩上的点击要认准"点在遮罩本身"——点在卡片里的任何地方都不该关掉。
$("#offline-ok").addEventListener("click", dismissOffline);
$("#offline").addEventListener("click", (event) => {
  if (event.target.id === "offline") dismissOffline();
});

// 面板切换：按钮 + URL hash 双向同步，于是可以把 #tab=codex 直接发给别人
function selectTab(name) {
  for (const button of document.querySelectorAll("#tabs button")) {
    button.classList.toggle("active", button.dataset.tab === name);
  }
  for (const panel of document.querySelectorAll("[data-panel]")) {
    panel.classList.toggle("hidden", panel.dataset.panel !== name);
  }
  if (location.hash !== `#tab=${name}`) history.replaceState(null, "", `#tab=${name}`);

  // 切到「表态」就是"玩家要求看这些表态"这一刻：立刻报告，不等下一帧
  // （下一帧最多 250ms 之后才来，而结局的落定判定就在这中间跑）。
  if (name === "choices") reportChoicesShown();
}

// 页面从后台回到前台：面板这几秒里看不见，此刻才真的被玩家看到。
document.addEventListener("visibilitychange", () => {
  if (!document.hidden) reportChoicesShown();
});

$("#tabs").addEventListener("click", (event) => {
  const button = event.target.closest("button[data-tab]");
  if (button) selectTab(button.dataset.tab);
});

document.addEventListener("keydown", (event) => {
  if (event.target.tagName === "INPUT") return;

  // 离线弹窗开着时，空格/Esc 先用来收下它：否则玩家按空格想关弹窗，
  // 结果既没关掉又多点了一下（两个都是意外）。
  const offlineOpen = !$("#offline").classList.contains("hidden");
  if (offlineOpen && (event.code === "Escape" || event.code === "Space")) {
    event.preventDefault();
    dismissOffline();
    return;
  }

  if (event.code === "Space") {
    event.preventDefault();
    send("click");
  }
});

// ---------------------------------------------------------------- 启动

const initialTab = new URLSearchParams(location.hash.replace(/^#/, "")).get("tab");
if (initialTab) selectTab(initialTab);

connect();
requestAnimationFrame(animate);
