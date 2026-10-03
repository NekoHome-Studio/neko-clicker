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
 * 三条不变量（改这一段之前先读一遍）：
 *   1. **只增不减**。唯一的例外是服务端自己倒退了（买入 / 转生 / 换存档）——
 *      那时一次性落到新的真值，是一次干净的下降，不是"超调再拉回"的来回。
 *   2. 领先服务端不超过 `tolerance`（半秒的产量 = 两个推送周期，见 animate 里的注释）。
 *   3. 撞到上限时**停下来等**，绝不回拉：回拉就是倒退，而倒退正是"数字来回跳"的根因。
 *
 * 历史（别再写回去）：这里曾经是 `shownCookies = server + (shownCookies - server) * 0.5`——
 * 每一拍先按 cps 外推冲过头，再只拉回一半，下一拍又冲出去。那是一个**阻尼振荡器**：
 * 服务端每来一帧数字就往下弹一次、然后继续往上爬，看起来就是数字绕着真值来回跳。
 */
let shownCookies = 0;

/**
 * 上一次从服务端收到的钱包值。判断"服务端自己倒退了"用它，**不要**用 `shownCookies > server`：
 * 显示值本来就该领先服务端（那正是外推存在的意义），所以"显示值 > 服务端"是每一帧都成立的
 * 常态，不是倒退的证据——按它收敛等于每 250ms 把数字往下按一次。
 */
let lastServerCookies = null;

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
      reconcileCookies(); // 新连接 / 每 30 秒对账一次：从这里重新起算
    } else if (frame.kind === "delta") {
      Object.assign(state, frame.changed);
      reconcileCookies();
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
 * 服务端来帧时的对账。**只做两件事**，而且都不产生"局部回拉"：
 *
 *   1. 还没有历史（第一帧）：直接对齐真值。
 *   2. 服务端自己倒退了（买入 / 转生 / 换了存档）：这是唯一被允许的倒退，
 *      一步落到新的真值——一次干净的下降。
 *
 * 其余情况（服务端持平或前进）这里**什么都不做**：显示值归 animate() 管，
 * 它只往前爬；领先太多时它停下来等，绝不回拉。
 *
 * 为什么不再照抄 `shownCookies > frame.changed.cookies` 那个判断：显示值本来就该领先
 * 服务端（那正是外推的意义），所以"显示值 > 服务端"是**每一帧都成立的常态**，
 * 不是"服务端倒退了"的证据。按它收敛，等于每 250ms 把已经画上去的数字往下按一次。
 */
function reconcileCookies() {
  const server = state.cookies ?? 0;
  const previous = lastServerCookies;
  lastServerCookies = server;

  if (previous === null || server < previous) shownCookies = server;
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
  // 两张 sheet 的次序在这里定：离线收益先算，表态那张才知道自己要不要让位
  // （判据就是离线那张此刻的 class，见 renderChoicesSheet 的规则 3）。
  renderOffline();
  renderChoicesSheet();
  reportChoicesShown();
  renderCodex();
  renderAchievements();
  renderNotifications();
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
  // 离线那张一关，表态这张立刻回来（两张 sheet 的次序规则 3）——
  // 不等下一帧：结局的落定判定就跑在这中间，而玩家此刻已经能看见表态了。
  renderChoicesSheet();
  reportChoicesShown();
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
 * 待答表态：从页签改成了一张 sheet（小窗口）。三条规则，都在下面这段里执行：
 *
 * 1. **它自己冒出来。** 有待答表态时，玩家不需要先去发现并点开哪个页签——提示本身就是内容。
 *    这与引擎那侧配套：**结局要等这些表态被答完才允许落定**（1.6.0，见
 *    `GameEngine.CheckEnding`），所以"让玩家看见并答得掉它们"这件事必须由界面负责。
 *    1.5.0 到 1.6.0 之间引擎等的是"被展示过"（`MarkPendingChoicesShown`）；那条规则已经换掉，
 *    展示记录现在只是诊断信号（见 `reportChoicesShown`）。
 * 2. **可以收起，但不会丢。** 收起后只剩下页面底部那个药丸（写着还有几项待答），点它就回来。
 *    玩家按过"收起"的那一批不再自己弹回来（否则收起等于没收起）；而**没被收起过**的一批
 *    （新触发的、或刚读档进来的）出现时，sheet 会自己回来。
 *    **收起不等于处理完**：只要还没作答，结局就一直等着——药丸是让玩家能回来答的那条退路。
 * 3. **两张 sheet 不打架：离线收益先讲完。** 它是读档补发的一次性事件（"你不在的时候发生了什么"
 *    在时间上先于现在），而且只能被"收下"一次；叠在它上面的表态 sheet 会落在遮罩底下，
 *    等于玩家点不到。所以离线收益开着时表态这张（连同药丸）先让位，它一关，
 *    表态立刻自己回来——不需要玩家再做一次操作。
 */
const shownChoices = new Set();

/** 玩家按过"收起"的那批待答 id：同一批不再自己弹回来，新的一批会（见规则 2）。 */
const collapsedChoices = new Set();

/** 这张 sheet 现在该不该显示；`pinned` = 玩家自己点开只为看立场/结局，没有待答也要留住。 */
let choicesSheetOpen = false;
let choicesSheetPinned = false;

/** 离线收益那张 sheet 此刻是不是开着（表态是否要让位的唯一判据）。 */
function offlineSheetOpen() {
  return !$("#offline").classList.contains("hidden");
}

/**
 * 玩家自己把 sheet 叫回来：底部药丸，或 hero 里那个常驻的立场按钮。
 * 待答为空时（点开只为看立场轴/结局）把它钉住，否则"点开看一眼"会被下一帧立刻关掉。
 */
function openChoicesSheet() {
  if (!state) return;
  choicesSheetOpen = true;
  choicesSheetPinned = (state.pendingChoices ?? []).length === 0;
  renderChoicesSheet();
  reportChoicesShown();
}

/**
 * 收起（"收起，稍后再答" / 右上角 × / 点遮罩 / Esc）。
 * **这不是关闭**：表态还在引擎里挂着，药丸会一直在，点它就回来。收起只对**这一批**生效。
 */
function collapseChoicesSheet() {
  choicesSheetOpen = false;
  choicesSheetPinned = false;
  for (const choice of state?.pendingChoices ?? []) collapsedChoices.add(choice.id);
  renderChoicesSheet();
}

/**
 * 收起之后的药丸：**只要还有待答就一直在**，它是表态唯一的入口。
 * 它只写条数——而"看到条数"不等于"看到了谁在问、问什么、有哪些选项"，
 * 所以它**不**触发 choicesShown（见 reportChoicesShown 的注释）。
 */
function renderChoicesPill(count, visible) {
  const pill = $("#choices-pill");
  if (!pill) return;

  const appearing = visible && pill.classList.contains("hidden");
  pill.classList.toggle("hidden", !visible);

  if (!visible) {
    pill.classList.remove("urgent");
    return;
  }

  const label = `🗣 还有 ${count} 项表态`;
  // 每帧都重建的话，文字节点会被反复换掉；只在真的变了才写。
  if (pill.textContent !== label) pill.textContent = label;
  pill.title = "点开作答";

  if (appearing) {
    // 冒出来那一下强调一次：它出现只可能是因为玩家刚把 sheet 收起。
    pill.classList.add("urgent");
    setTimeout(() => pill.classList.remove("urgent"), 2600);
  }
}

/**
 * 立场轴与结局的常驻入口（hero 里那个按钮）。
 *
 * 为什么需要它：药丸只在"还有待答"时存在（规则 2），所以答完最后一条之后，
 * 那张 sheet 就再也没有别的入口了——而玩家恰恰是在**答完之后**才想回头看
 * "我偏向了哪一边""结局说了什么"。没有这个按钮，结局文本会随着最后一条作答一起消失。
 * 所以它不是页签，也不是"待办提示"：它写着当前主导立场，随时可以点开那张 sheet。
 * 既没有立场、也没有结局的内容包（11 个包里有 8 个）整个按钮隐藏。
 */
function renderStanceEntry() {
  const button = $("#stances-open");
  if (!button) return;

  const stances = state.stances ?? [];
  const dominant = stances.find((stance) => stance.isDominant);
  const has = stances.length > 0 || Boolean(state.ending);

  button.classList.toggle("hidden", !has);
  if (!has) return;

  const label = dominant ? `${dominant.icon} ${dominant.name}` : "立场";
  if (button.textContent !== label) button.textContent = label;
  button.title = dominant ? `看立场轴与结局（当前主导：${dominant.name}）` : "看立场轴与结局";
}

/**
 * "玩家看过待答表态了吗"——1.5.0 到 1.6.0 之间这是结局能否落定的**唯一**条件，
 * **1.6.0 起它只是诊断信号**：结局等的是玩家把表态**答完**（见 `sendAnswer` 那条 `answer`
 * 命令与引擎的 `EndingSystem.Check`）。
 *
 * 那为什么还报？因为它记下的是**每条表态到底露过面没有**，而新规则下"结局一直没落定"
 * 是一件真实可能的事（玩家收起 sheet 之后一直不答就一直不落定）。**"从没看到"与
 * "看到了却一直没答"是两种完全不同的原因**，没有这份记录就分不出来。
 * 判据仍然只有一条：**那张画着选项的 sheet 此刻真的显示在屏幕上**（没有收起、
 * 页面不在后台、并且真的有内容要答）——记录要可信，就不能在没画出来时报"画出来了"。
 *
 * 为什么药丸不算：它只写条数，看见"还有 1 项表态"并不等于看见了谁在问、问什么、
 * 有哪些选项。收起（变成药丸）之后也不再报告：那批表态此刻并不在玩家眼前。
 *
 * 只报一次：报成功的 id 记在这里，之后不再重复发。send 失败时**不记账**，
 * 下一帧会重试——"发丢了却记成发过"会让这份诊断记录说谎。
 */
function reportChoicesShown() {
  if (!state || document.hidden) return;

  const layer = $("#choices-sheet");
  if (!layer || layer.classList.contains("hidden")) return;
  if ((state.pendingChoices ?? []).length === 0) return;

  const fresh = state.pendingChoices
    .map((choice) => choice.id)
    .filter((id) => !shownChoices.has(id));
  if (fresh.length === 0) return;

  send("choicesShown").then((result) => {
    if (!result?.ok) return;
    for (const id of fresh) shownChoices.add(id);
  });
}

/** 表态 sheet：待答选择 + 立场轴 + 结局。选择不阻塞游戏（决策 R6），所以它不拦着谁。 */
function renderChoicesSheet() {
  if (!state) return;

  const pending = state.pendingChoices ?? [];

  // 规则 1：这一批里还有没被收起过的 → 自己冒出来。
  if (pending.some((choice) => !collapsedChoices.has(choice.id))) {
    choicesSheetOpen = true;
    choicesSheetPinned = false;
  }
  // 一旦真的有东西要答，"钉住"就失效：答完最后一条时它必须自己收起（规则 2）。
  if (pending.length > 0) choicesSheetPinned = false;
  // 规则 2 的另一半：答完就收。玩家自己点开看立场（当时没有待答）时不动它。
  if (pending.length === 0 && !choicesSheetPinned) choicesSheetOpen = false;

  // 规则 3：离线收益先讲完，表态这张让位（dismissOffline 里一关就立刻重画，不等下一帧）。
  const show = choicesSheetOpen && !offlineSheetOpen();

  const layer = $("#choices-sheet");
  if (!layer) return;
  layer.classList.toggle("hidden", !show);

  // 药丸：收起之后唯一的入口。离线那张开着时也不显示——它落在遮罩底下，点也点不到。
  renderChoicesPill(pending.length, pending.length > 0 && !show && !offlineSheetOpen());
  renderStanceEntry();

  if (!show) return;

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

  // 立场轴：它在 sheet 里，因为"我偏向哪边"正是这些表态累积出来的结果。
  const stances = state.stances ?? [];
  const total = stances.reduce((sum, stance) => sum + stance.weight, 0);
  // 这句说明没有立场轴时也要写（否则会留着上一批的旧文字）。
  $("#stance-summary").textContent = stances.length === 0
    ? (state.ending ? "已落定" : "")
    : state.dominantStanceId
      ? `主导：${stances.find((s) => s.isDominant)?.name ?? "—"}`
      : total === 0 ? "还没有表态" : "还没有占上风的立场";

  // 底部那颗按钮：还有待答时是"收起"（表态不会丢，只是变成药丸），没有待答时就是"关闭"。
  $("#choices-later").textContent = pending.length > 0 ? "收起，稍后再答" : "关闭";

  const axis = $("#stances");
  axis.textContent = "";
  if (stances.length > 0) {
    const title = document.createElement("h3");
    title.textContent = "立场轴";
    axis.append(title);

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
  }

  // 结局：**不挂在立场轴里面**。11 个内容包里有 6 个（末日 / 文明 / 赛博 / 梦境 / 神 / 图书馆）
  // 只有结局、没有立场轴（`Choices.cs` / `Stances.cs` 都没有），原先那层 `if (stances.length > 0)`
  // 让这 6 个包的终局文本一次都没显示过。结局是走到了才有的东西，它跟立场轴一样属于这张 sheet。
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
 *
 * 这里的所有修正都是**单向**的：向前跳（服务端更靠前时）、向前爬、以及由
 * reconcileCookies() 在服务端自己倒退时落下去。除此之外一步都不往后走——
 * 显示值领先服务端的量由 `tolerance` 封顶，撞到顶是**停下来等**，不是拉回来。
 */
function animate(now) {
  const elapsed = Math.min(0.25, Math.max(0, (now - lastTickAt) / 1000));
  lastTickAt = now;

  if (state) {
    const rate = state.cookiesPerSecond ?? 0;
    const server = state.cookies ?? 0;

    // 允许领先"半秒的产量"。服务端 4 Hz 推帧（GameHost.PushIntervalSeconds = 0.25），
    // 所以这正好是**两帧的量**：够吸收 SSE 投递抖动、时钟量化和一帧的积压，
    // 又不至于让数字长时间挂在明显高于真值的地方。
    const tolerance = Math.max(1e-6, rate * 0.5);

    if (shownCookies < server) {
      shownCookies = server; // 服务端更靠前（点击、离线补发、重连）：直接跟上，不倒退
    } else if (rate > 0 && shownCookies < server + tolerance) {
      // 只往前爬，并且一步都不越过上限：越过上限的部分留给"下一拍停住"去消化。
      shownCookies = Math.min(server + tolerance, shownCookies + rate * elapsed);
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

/**
 * 玩家展开了哪几条建筑故事。**按建筑 id 记在 JS 里，而不是留在 DOM 上。**
 *
 * 为什么状态必须活在这里：建筑列表每一帧都会被过一遍（服务端 4 Hz 推快照），而
 * "我展开了哪一条"是玩家的操作、不是服务端的状态——快照里没有它，也不该有它。
 * 把这个 Set 当成唯一真相、重画时按 id 重新套用，展开的框才不会在下一帧自己合上。
 * 与 `collapsedChoices`（被收起过的那批表态）是同一个模式。
 */
const openStories = new Set();

/**
 * 每个建筑一行的 DOM 节点，按 id 复用。
 *
 * 为什么不像别的列表那样每帧 `textContent = ""` 重建：`.list` 是个会滚动的容器
 * （`max-height: 26rem; overflow-y: auto`），而**清空重建会让"滚动位置还在不在"
 * 完全失去保证**——内容高度归零的那一瞬，浏览器可以把 `scrollTop` 夹回 0；挂机时
 * 列表每 250ms 自己跳回顶部。焦点同理：被重画掉的按钮会把键盘焦点丢回 body，
 * 而"键盘展开一条故事"正需要焦点留住。复用节点，这两件事都不必去赌。
 */
const buildingRows = new Map();

/** 一行建筑的元素 id：展开按钮的 `aria-controls` 指向它。 */
function storyId(buildingId) {
  return `building-story-${buildingId}`;
}

/**
 * 把一段作者写的文本切段：**空行**分段，段内的单个换行保留（CSS `white-space: pre-line`）。
 *
 * 今天 11 个包 104 座建筑的说明都是**一行**，所以每条现在只会得到一个 `<p>`——
 * 这里做的不是"把一句话撑成几段"，而是"文案将来写长了要有地方放"。
 * 内容一个字都不在这里编：快照里是什么就画什么。
 */
function paragraphs(text) {
  return String(text ?? "")
    .split(/\r?\n\s*\r?\n/)
    .map((part) => part.trim())
    .filter(Boolean);
}

/**
 * 建一行建筑的 DOM。**只建一次**，之后每帧只改文字与类（见 `buildingRows`）。
 *
 * 一行 = 左边的 📖 按钮（展开故事）+ 买卡片 + 展开在下面的故事框：
 *
 *   <div class="building">
 *     <button class="story-toggle" aria-expanded aria-controls>📖</button>
 *     <button class="card">…</button>          ← 还是"点一下买"
 *     <div class="story" role="region" id="building-story-<id>">…</div>
 *   </div>
 *
 * 展开按钮是**另一个控件**而不是卡片的一部分：真 `<button>` 不能嵌在真 `<button>` 里
 * （那既是非法标记、也是屏幕阅读器读不清的东西），而且这两个手势本来就不该共用一个手势。
 */
function createBuildingRow(building) {
  const root = document.createElement("div");
  root.className = "building";
  root.dataset.building = building.id;

  const prose = paragraphs(building.description);

  // 展开按钮：真 <button>，所以 Tab 停得到、回车/空格由浏览器自带激活。
  // 没有说明文本时**不给这个按钮**：一个点开空空如也的 📖 是在骗人。
  // （引擎那条不变量保证每座建筑的 description 非空，见 ContentTextFileTests，
  //  所以这是形状守卫而不是活路径。）
  const toggle = prose.length > 0 ? document.createElement("button") : null;
  if (toggle) {
    toggle.type = "button";
    toggle.className = "story-toggle";
    toggle.textContent = "📖";
    toggle.setAttribute("aria-expanded", "false");
    toggle.setAttribute("aria-controls", storyId(building.id));
    toggle.title = `看《${building.name}》的故事`;
    toggle.setAttribute("aria-label", toggle.title);
    toggle.addEventListener("click", () => toggleStory(building.id));
  }

  // 卡片本体：**还是买**，与改动前逐字相同的行为。
  const card = document.createElement("button");
  card.className = "card";
  card.addEventListener("click", () => {
    if (card.disabled) return; // 锁着的行不发货（真 DOM 里 disabled 的按钮本来就点不动）
    send("buy", { id: building.id });
  });

  const name = document.createElement("span");
  name.className = "name";
  const owned = document.createElement("span");
  owned.className = "owned";
  const price = document.createElement("span");
  price.className = "price";
  const share = document.createElement("span");
  share.className = "share hidden";
  card.append(name, owned, price, share);

  // 故事框：内联展开在卡片下面，**不是第二套弹窗**——形态沿用卡片那一套
  // （同底色 / 同圆角 / 同左边框语言，见 app.css 的「建筑行」一节）。
  const story = prose.length > 0 ? document.createElement("div") : null;
  if (story) {
    story.className = "story hidden";
    story.id = storyId(building.id);
    // 屏幕阅读器要读得到：给这块一个名字（role=region + aria-label，与 sheet 上的
    // aria-modal / aria-labelledby 是同一套做法）。
    story.setAttribute("role", "region");
    story.setAttribute("aria-label", `《${building.name}》的故事`);
    for (const part of prose) {
      const text = document.createElement("p");
      text.textContent = part;
      story.append(text);
    }
  }

  root.append(...[toggle, card, story].filter(Boolean));
  return { root, card, toggle, story, name, owned, price, share };
}

/** 一帧一次的行内更新：只写"会变的东西"，节点本身不换。 */
function updateBuildingRow(node, building) {
  const unlocked = Boolean(building.isUnlocked);

  // affordable / locked 这套类名与批量档位的语义一个没动（批次价格由服务端给）。
  node.card.disabled = !unlocked;
  node.card.classList.toggle("locked", !unlocked);
  node.card.classList.toggle("affordable", unlocked && Boolean(building.canAfford));

  setText(node.name, unlocked ? `${building.icon} ${building.name}` : `🔒 ${building.name}`);
  setText(node.owned, building.owned);

  if (unlocked) {
    const bulk = building.batchAmount > 1 ? ` ×${building.batchAmount}` : "";
    setText(node.price, `${state.currencyIcon} ${number(building.batchPrice)}${bulk}`);
  } else {
    setText(node.price, `${building.unlockHint}（${percent(building.unlockProgress)}）`);
  }

  const showShare = unlocked && building.owned > 0;
  node.share.classList.toggle("hidden", !showShare);
  if (showShare) {
    setText(node.share, `${number(building.cpsContribution)}/s · 占 ${percent(building.cpsShare)}`);
  }

  applyStoryState(node, building.id);
}

/**
 * 把展开状态套到这一行的 DOM 上：`hidden` 管可见（全站同一套），
 * `aria-expanded` 让屏幕阅读器读得出"开着还是关着"。
 */
function applyStoryState(node, buildingId) {
  if (!node.toggle || !node.story) return;

  const open = openStories.has(buildingId);
  node.story.classList.toggle("hidden", !open);
  node.toggle.setAttribute("aria-expanded", open ? "true" : "false");
}

/**
 * 展开 / 收起某座建筑的故事框。<para>
 *
 * 只改这一行的 DOM，**不重画列表**：点一下就该立刻有反应，而不是等下一帧（最迟 250ms）。
 * 也**不发任何命令**——看故事不是购买。这个"不"由 web-smoke 第 13 节守着。
 * </para>
 */
function toggleStory(buildingId) {
  if (openStories.has(buildingId)) openStories.delete(buildingId);
  else openStories.add(buildingId);

  const node = buildingRows.get(buildingId);
  if (node) applyStoryState(node, buildingId);
}

/**
 * 建筑列表：一行 = 一个"买"的卡片 + 一个"看故事"的按钮 + 一个可展开的故事框。
 *
 * **一次点击 = 一次购买，这一点没变**：卡片本体仍然是那个 `<button>`，点它就是买。
 * 故事框是**另一个控件**（左边那个 📖），展开的是这座建筑自己的说明文本，也就是快照里的
 * `description`（内容包里 `text.json` 的 `buildings.<id>.description`，作者写的散文）。
 * 两个动作各有一个控件，互不触发：点 📖 不会买，买不会展开。
 *
 * 为什么不让"点卡片"同时担当两件事：那是这个游戏的核心循环、是肌肉记忆，而且它是这个
 * 页面上**唯一会花钱的手势**——同一个手势一会儿花钱、一会儿只是弹出一段字，误操作的
 * 代价是不对称的（点错了会买错东西，而"点错了"最坏只是多读一段字）。所以宁可多一个
 * 明确的控件，也不去动那个已经长在玩家手上的手势。
 *
 * 展开状态活在 `openStories` 里、按建筑 id 记（见那条注释）；节点按 id 复用（见
 * `buildingRows`）。两者都只为一件事：**快照每 250ms 来一帧，展开的框不能跟着抖**。
 */
function renderBuildings() {
  const host = $("#buildings");
  const rows = (state.buildings ?? []).filter((building) => building.isVisible);

  const live = [];
  for (const building of rows) {
    let node = buildingRows.get(building.id);
    if (!node) {
      node = createBuildingRow(building);
      buildingRows.set(building.id, node);
    }

    updateBuildingRow(node, building);
    live.push(node.root);
  }

  // 成员或顺序真的变了才动结构：真 DOM 里 append 一个已经是子节点的元素是"移动"，
  // 节点（连同焦点与展开状态）都还在。逐帧清空重建是另一回事——见 buildingRows 的注释。
  const sameOrder = host.children.length === live.length
    && live.every((node, index) => host.children[index] === node);
  if (!sameOrder) {
    host.textContent = "";
    host.append(...live);
  }

  // 掉出列表的建筑（换内容包、可见性变了）连同节点一起丢掉，别让 Map 越攒越多。
  const visible = new Set(rows.map((building) => building.id));
  for (const id of [...buildingRows.keys()]) {
    if (visible.has(id)) continue;
    buildingRows.delete(id);
    openStories.delete(id);
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

/**
 * 批量档位按钮。
 *
 * ⚠️ 认档位必须用 `state.modeName`（服务端给的名字），**不要**用 `state.mode`：
 * 那个字段在线上是**枚举序数**（`"mode":0`），此前这里写的是
 * `(state.mode ?? "").toLowerCase()`——数字没有 `toLowerCase`，于是**每一次 `render()`
 * 都在这里抛 `TypeError`**，而它在 `render()` 的中段：批量按钮、离线收益、表态 sheet、
 * 图鉴、成就、日志全都没画出来过，页面还"看着能玩"（见 OPEN_WORK 的 N 条）。
 * `modeName` 就是命令侧接受的那个 token（`buy10` 这种写法），所以它能原样发回去。
 * web-smoke 里有一条源码守卫盯着这个"不许写回去"。
 */
function renderBatch() {
  const modes = ["buy1", "buy10", "buy100", "buymax"];
  const labels = { buy1: "×1", buy10: "×10", buy100: "×100", buymax: "买满" };
  const current = String(state.modeName ?? "").toLowerCase();

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

/**
 * 只在真的变了才写 `textContent`。
 *
 * 建筑列表每帧都会被过一遍（4 Hz），而反复写同一个字符串会让浏览器白做一次重排；
 * 更要紧的是它会让"这一帧到底有没有变"变得看不出来。与 `renderChoicesPill` 里那条
 * "只在真的变了才写"是同一条规矩。
 */
function setText(node, text) {
  const value = text === undefined || text === null ? "" : String(text);
  if (node.textContent !== value) node.textContent = value;
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

// 表态 sheet：底部按钮 / 右上角 × / 点遮罩都只是"收起"（表态还在，只是变成药丸）；
// 药丸与 hero 里那个立场按钮把它叫回来。
$("#choices-later").addEventListener("click", collapseChoicesSheet);
$("#choices-close").addEventListener("click", collapseChoicesSheet);
$("#choices-sheet").addEventListener("click", (event) => {
  if (event.target.id === "choices-sheet") collapseChoicesSheet();
});
$("#choices-pill").addEventListener("click", openChoicesSheet);
$("#stances-open").addEventListener("click", openChoicesSheet);

// 面板切换：按钮 + URL hash 双向同步，于是可以把 #tab=codex 直接发给别人
function selectTab(name) {
  const panels = [...document.querySelectorAll("[data-panel]")];

  // 「表态」不再是页签（它是自己冒出来的那张 sheet）。老书签 #tab=choices 会指到一个
  // 已经不存在的面板，那样所有面板都会被隐藏、留下一个空白右栏——所以没有这一页就退回建筑。
  if (!panels.some((panel) => panel.dataset.panel === name)) name = "buildings";

  for (const button of document.querySelectorAll("#tabs button")) {
    button.classList.toggle("active", button.dataset.tab === name);
  }
  for (const panel of panels) {
    panel.classList.toggle("hidden", panel.dataset.panel !== name);
  }
  if (location.hash !== `#tab=${name}`) history.replaceState(null, "", `#tab=${name}`);
}

// 页面从后台回到前台：那几秒里 sheet 是看不见的，此刻才真的被玩家看到。
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

  // 表态 sheet 开着时：Esc 收起它（与离线那张同一个键，收起≠丢掉，药丸还在）。
  // 空格在这里被吃掉而**不是**退回"点猫"：那张窗口此刻盖在游戏上面，
  // 按空格却在背后偷偷点一下猫，是一步谁也没要求过的操作。要作答请点选项或按回车。
  const choicesLayer = $("#choices-sheet");
  const choicesOpen = Boolean(choicesLayer) && !choicesLayer.classList.contains("hidden");
  if (choicesOpen && event.code === "Escape") {
    event.preventDefault();
    collapseChoicesSheet();
    return;
  }
  if (choicesOpen && event.code === "Space") {
    event.preventDefault();
    return;
  }

  if (event.code === "Space") {
    // 焦点落在某个控件上时，空格是**那个控件**的激活键（浏览器会替它发一次 click）。
    // 全局这里再发一次，就变成了"按空格展开故事，顺手又点了一下猫"——两件事都不是玩家要的。
    // 上面那条 INPUT 是同一类判断，这里把它补全。
    //
    // ⚠️ 这同时修掉了一个既有的双发：焦点在 `#big-cat` 上按空格，此前会点两次猫。
    // 现在那一格归按钮自己（浏览器发的就是 click），全局这条只管"焦点不在任何控件上"。
    const tag = event.target?.tagName;
    if (tag === "BUTTON" || tag === "A" || tag === "TEXTAREA" || tag === "SELECT") return;

    event.preventDefault();
    send("click");
  }
});

// ---------------------------------------------------------------- 启动

const initialTab = new URLSearchParams(location.hash.replace(/^#/, "")).get("tab");
if (initialTab) selectTab(initialTab);

connect();
requestAnimationFrame(animate);
