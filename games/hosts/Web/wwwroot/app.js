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

/** 数字平滑：服务端 4 Hz 推，这里每帧插值，否则数字会一跳一跳。 */
let shownCookies = 0;
let targetCookies = 0;
let lastFrameAt = performance.now();

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
      shownCookies = state.cookies; // 全量帧直接对齐，不做插值
      targetCookies = state.cookies;
    } else if (frame.kind === "delta") {
      Object.assign(state, frame.changed);
      targetCookies = state.cookies;
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
    const budget = row.currency === 1 ? premium : wallet;
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
  renderBatch();
  renderChoices();
  renderCodex();
  renderAchievements();
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

/** 表态：待答选择 + 立场轴。选择不阻塞游戏（决策 R6），所以它是一张待办卡片而不是弹窗。 */
function renderChoices() {
  const pending = state.pendingChoices ?? [];
  const badge = $("#choice-badge");

  if (pending.length > 0) {
    badge.textContent = pending.length;
    badge.classList.remove("hidden");
  } else {
    badge.classList.add("hidden");
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

/** 数字平滑：每帧朝目标值靠。挂机时数字在动，才有"它自己在跑"的感觉。 */
function animate() {
  if (state) {
    const gap = targetCookies - shownCookies;
    if (Math.abs(gap) > Math.max(1, Math.abs(targetCookies) * 1e-9)) {
      shownCookies += gap * 0.25;
      $("#cookies").textContent = formatLike(state.cookiesText, shownCookies, targetCookies);
    } else if (shownCookies !== targetCookies) {
      shownCookies = targetCookies;
      $("#cookies").textContent = state.cookiesText;
    }
  }
  requestAnimationFrame(animate);
}

/**
 * 用服务端给的格式化文本作模板，把插值中的数字塞回去。
 * 服务端的 NumFormat 用的是 short scale（万亿/兆…），前端不重造一套——
 * 只在"数量级没变"时沿用它的后缀，变了就直接等下一条推送。
 */
function formatLike(reference, value, target) {
  const match = /^([\d.,]+)\s*(.*)$/.exec(reference ?? "");
  if (!match) return reference ?? "";

  const targetMagnitude = Math.floor(Math.log10(Math.max(1, Math.abs(target))));
  const valueMagnitude = Math.floor(Math.log10(Math.max(1, Math.abs(value))));
  if (targetMagnitude !== valueMagnitude) return reference; // 数量级跨了，别硬凑

  const digits = match[1].includes(".") ? 2 : 0;
  return `${value.toFixed(digits)}${match[2] ? ` ${match[2]}` : ""}`;
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

function renderUpgrades() {
  const host = $("#upgrades");
  const rows = (state.upgrades ?? []).filter((u) => u.isVisible);
  const available = rows.filter((u) => u.isAvailable);
  host.textContent = "";

  $("#upgrade-count").textContent = available.length > 0 ? `${available.length} 项可买` : "暂无可买";

  for (const upgrade of rows.slice(0, 40)) {
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
    const icon = upgrade.currency === 1 ? state.prestigeCurrencyIcon : state.currencyIcon;
    price.textContent = upgrade.isUnlocked ? `${icon} ${number(upgrade.price)}` : percent(upgrade.unlockProgress);
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

// 面板切换：按钮 + URL hash 双向同步，于是可以把 #tab=codex 直接发给别人
function selectTab(name) {
  for (const button of document.querySelectorAll("#tabs button")) {
    button.classList.toggle("active", button.dataset.tab === name);
  }
  for (const panel of document.querySelectorAll("[data-panel]")) {
    panel.classList.toggle("hidden", panel.dataset.panel !== name);
  }
  if (location.hash !== `#tab=${name}`) history.replaceState(null, "", `#tab=${name}`);
}

$("#tabs").addEventListener("click", (event) => {
  const button = event.target.closest("button[data-tab]");
  if (button) selectTab(button.dataset.tab);
});

document.addEventListener("keydown", (event) => {
  if (event.target.tagName === "INPUT") return;
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
