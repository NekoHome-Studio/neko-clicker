// tools/web-smoke.mjs —— 「表态」那张 sheet 的无头冒烟测试（不需要浏览器、不需要宿主）。
//
// 用法:   node tools/web-smoke.mjs
//
// 为什么需要它（这一段是它存在的理由，别删）:
//   `node --check` 只证明**语法**，证明不了"没有未声明的标识符"。b48f2fa 修的那个 bug
//   就是这一类: app.js 给从未声明的 `lastFrameAt` 赋值，而 ES 模块是严格模式，于是
//   **每一帧**的 render() 都抛 ReferenceError、整页停摆——而 tools/api-test.ps1 照样
//   46/46 全绿，因为它测的是 HTTP，不是页面。所以这里把 app.js 真的跑一遍。
//
// 它怎么做到:
//   1. 把 app.js 复制成 .mjs 再 import。仓库里没有 package.json，直接 import 一个 .js
//      会被 Node 当成 CommonJS —— 那是**非严格模式**，赋值给未声明的标识符只会悄悄
//      造一个全局变量，这个测试就白做了。（第一条自检就是证明这件事：给 app.js 副本
//      注入 b48f2fa 那一行，必须抛 ReferenceError。）
//   2. 最小 DOM 桩。桩里的元素是**从 index.html 里扫出来的**，不是手抄的第二份：
//      app.js 里出现一个 index.html 中不存在的选择器，就会以"选择器落空"的形式红掉。
//   3. 推一帧含待答表态的合成快照，断言不抛异常，并断言 sheet / 药丸的 class 状态、
//      以及发给宿主的命令序列（choicesShown 只由真的画出选项的那张 sheet 发出）。
//
// 覆盖边界（诚实）: 它证明的是"真的被执行过的路径不抛异常"。没被执行到的分支
// （`if (!pill) return` 这类防御）只有 node --check 的语法保证。
//
// 这个脚本刻意不进 tools/build.ps1: 构建脚本至今零 JS 依赖，加一条 node 步骤会让
// "没有 node 的机器上 -Strict 还能不能过"变成一个新问题。跑它是独立的一步。

import { readFileSync, writeFileSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const wwwroot = join(root, "games", "hosts", "Web", "wwwroot");

const html = readFileSync(join(wwwroot, "index.html"), "utf8");
const css = readFileSync(join(wwwroot, "app.css"), "utf8");
const appSource = readFileSync(join(wwwroot, "app.js"), "utf8");

// ---------------------------------------------------------------- 断言

let checks = 0;
const failures = [];

function check(name, fn) {
  checks++;
  try {
    if (fn() === false) throw new Error("断言为假");
    console.log(`  \u2714 ${name}`);
  } catch (error) {
    failures.push(`${name} — ${error.message}`);
    console.log(`  \u2718 ${name} — ${error.message}`);
  }
}

function eq(actual, expected, what) {
  if (actual !== expected) {
    throw new Error(`${what}: 期望 ${JSON.stringify(expected)}，实际 ${JSON.stringify(actual)}`);
  }
}

const section = (title) => console.log(`\n${title}`);

// ---------------------------------------------------------------- 最小 DOM 桩

class ClassList {
  constructor(element) { this.el = element; }
  add(...names) { for (const name of names) if (name) this.el._classes.add(name); }
  remove(...names) { for (const name of names) this.el._classes.delete(name); }
  contains(name) { return this.el._classes.has(name); }
  toggle(name, force) {
    const on = force === undefined ? !this.contains(name) : Boolean(force);
    if (on) this.add(name); else this.remove(name);
    return on;
  }
}

class El {
  constructor(tag, id = "", classes = [], dataset = {}) {
    this.tagName = String(tag).toUpperCase();
    this.id = id;
    this._classes = new Set(classes.filter(Boolean));
    this._listeners = new Map();
    this._attrs = new Map();
    this._text = "";
    this.children = [];
    this.style = {};
    this.dataset = { ...dataset };
    this.parent = null;
    this.disabled = false;
    this.title = "";
    this.classList = new ClassList(this);
  }

  get className() { return [...this._classes].join(" "); }
  set className(value) { this._classes = new Set(String(value).split(/\s+/).filter(Boolean)); }
  get textContent() { return this._text; }
  set textContent(value) { this._text = value === undefined || value === null ? "" : String(value); this.children = []; }
  get innerHTML() { return ""; }
  set innerHTML(_) { this._text = ""; this.children = []; }

  append(...nodes) { for (const node of nodes) { node.parent = this; this.children.push(node); } }
  addEventListener(type, handler) {
    if (!this._listeners.has(type)) this._listeners.set(type, []);
    this._listeners.get(type).push(handler);
  }
  setAttribute(name, value) { this._attrs.set(name, String(value)); }
  getAttribute(name) { return this._attrs.has(name) ? this._attrs.get(name) : null; }
  closest(selector) { return matches(this, selector) ? this : (this.parent ? this.parent.closest(selector) : null); }
}

/** app.js 用到的选择器就这几种形状：#id、[data-x="y"]、[data-x]、tag[data-x="y"]。 */
function matches(el, selector) {
  // `#id`（app.js 里用得最多的一种）：不写成正则字面量——`$/` 这个序列在写入
  // 这份文件时被吞过一次，字符串判断既躲得开它，也比正则好读。
  if (selector.startsWith("#") && !selector.includes("[")) {
    return el.id === selector.slice(1);
  }

  const m = /^([a-zA-Z]*)\[([\w-]+)(?:="([^"]*)")?\]$/.exec(selector);
  if (m) {
    const [, tag, attr, value] = m;
    if (tag && el.tagName !== tag.toUpperCase()) return false;
    const key = attr.replace(/^data-/, "").replace(/-(\w)/g, (_, c) => c.toUpperCase());
    if (value === undefined) return el.dataset[key] !== undefined;
    return el.dataset[key] === value;
  }

  throw new Error(`DOM 桩不认识这个选择器：${selector}`);
}

/**
 * 从 index.html 里扫出元素（注释先剥掉：注释里的东西在浏览器里不存在，桩里也不该存在）。
 * 于是"app.js 引用了 index.html 里没有的元素"会变成一条看得见的失败，而不是静默的 null。
 */
function makeDom() {
  const byId = new Map();
  const all = [];
  const panels = [];
  const tabs = [];

  const body = html.replace(/<!--[\s\S]*?-->/g, "");
  for (const tag of body.matchAll(/<([a-zA-Z][\w-]*)([^>]*)>/g)) {
    const [, name, attrs] = tag;
    const id = /\bid="([^"]*)"/.exec(attrs)?.[1] ?? "";
    const classes = (/\bclass="([^"]*)"/.exec(attrs)?.[1] ?? "").split(/\s+/).filter(Boolean);
    const panel = /\bdata-panel="([^"]*)"/.exec(attrs)?.[1];
    const tab = /\bdata-tab="([^"]*)"/.exec(attrs)?.[1];
    const dataset = {};
    if (panel !== undefined) dataset.panel = panel;
    if (tab !== undefined) dataset.tab = tab;

    const el = new El(name, id, classes, dataset);
    all.push(el);
    if (id) byId.set(id, el);
    if (panel !== undefined) panels.push(el);
    if (tab !== undefined) tabs.push(el);
  }

  const doc = {
    title: "",
    hidden: false,
    byId,
    all,
    panels,
    tabs,
    missedSelectors: [],
    _listeners: new Map(),
    querySelector(selector) {
      if (selector === "#tabs button") return tabs[0] ?? null;
      const found = all.find((el) => matches(el, selector));
      if (!found) this.missedSelectors.push(selector);
      return found ?? null;
    },
    querySelectorAll(selector) {
      if (selector === "#tabs button") return tabs;
      return all.filter((el) => matches(el, selector));
    },
    createElement(tag) { return new El(tag); },
    addEventListener(type, handler) {
      if (!this._listeners.has(type)) this._listeners.set(type, []);
      this._listeners.get(type).push(handler);
    },
  };

  return doc;
}

/** 触发桩元素上的监听器（真 DOM 里就是 element.dispatchEvent）。 */
function fire(target, type, event = {}) {
  const handlers = target._listeners.get(type) ?? [];
  const payload = { type, target, preventDefault() {}, ...event };
  for (const handler of handlers) handler(payload);
  return payload;
}

// ---------------------------------------------------------------- 临时副本

const scratch = mkdtempSync(join(tmpdir(), "neko-web-smoke-"));
// 中途抛出去也要把临时副本删掉（不能指望最后那行 rmSync 一定跑得到）。
process.on("exit", () => rmSync(scratch, { recursive: true, force: true }));
const copyAs = (name, source) => {
  const path = join(scratch, name);
  writeFileSync(path, source, "utf8");
  return pathToFileURL(path).href;
};

/** 装好全局环境并 import 一份 app.js 副本；返回这一份的观测句柄。 */
async function loadApp(moduleUrl, { hash = "" } = {}) {
  const doc = makeDom();
  const location = { search: "", hash };
  const commands = [];
  const sources = [];
  const frames = [];
  let clock = 0;

  class StubEventSource {
    constructor(url) { this.url = url; sources.push(this); }
  }

  globalThis.document = doc;
  globalThis.location = location;
  globalThis.history = {
    replaceState(_state, _title, url) {
      if (typeof url === "string" && url.startsWith("#")) location.hash = url;
    },
  };
  globalThis.EventSource = StubEventSource;
  // 合成时钟（见 step 的注释）：app.js 只在模块顶层和 onmessage 里读 performance.now()，
  // 真正决定动画步长的是喂给 animate(now) 的那个参数，所以换成一只由测试推进的钟即可。
  globalThis.performance = { now: () => clock };
  globalThis.requestAnimationFrame = (callback) => { frames.push(callback); return frames.length; };
  globalThis.fetch = async (url, options = {}) => {
    const body = options.body ? JSON.parse(options.body) : null;
    if (body) commands.push(body);
    return { ok: true, json: async () => ({ ok: true, seq: commands.length }) };
  };

  await import(moduleUrl);

  const source = sources[0];
  if (!source) throw new Error("app.js 没有连 EventSource");

  const push = async (frame) => {
    source.onmessage({ data: JSON.stringify(frame) });
    await flush();
  };

  /**
   * 手动推进合成时钟，并跑掉这一拍排队的所有 animate 回调。
   *
   * 为什么必须由测试推进时钟：`animate(now)` 的步长是 `(now - lastTickAt)`，
   * 用真时钟只能得到"约等于 0 的随机步长"，写不出可重复的断言——而"数字来回跳"
   * 那个 bug 只在具体的步长 / 帧序下才现形。有了它，每调一次 `step(ms)`，
   * animate 收到的 elapsed 就精确等于 ms / 1000。
   */
  const step = (deltaMs) => {
    clock += deltaMs;
    const callbacks = frames.splice(0, frames.length);
    for (const callback of callbacks) callback(clock);
  };

  return { doc, location, commands, push, sources, frames, source, step };
}

const flush = () => new Promise((resolve) => setImmediate(resolve));

const el = (app, id) => {
  const found = app.doc.byId.get(id);
  if (!found) throw new Error(`index.html 里没有 #${id}`);
  return found;
};
const hidden = (app, id) => el(app, id).classList.contains("hidden");
const shown = (app, id) => !hidden(app, id);
const commandsOf = (app, type) => app.commands.filter((command) => command.type === type);

// ---------------------------------------------------------------- 合成快照

function snapshot(overrides = {}) {
  return {
    title: "猫咖物语",
    currencyName: "猫饼",
    currencyIcon: "🐟",
    clickActionName: "撸猫",
    cpsText: "12.3 千",
    clickPowerText: "7",
    cookies: 1.2e6,
    cookiesPerSecond: 12345,
    prestigeChips: 3,
    prestigeCurrencyName: "魂",
    prestigeCurrencyIcon: "🍃",
    achievementCount: 1,
    achievementTotal: 12,
    playTimeSeconds: 3600,
    // 档位：**两个字段都按线上真实形状写**（§14 的"夹具不是谎话"守卫盯着它们）。
    // `mode` 是枚举序数（数字），前端**不许**读它；`modeName` 才是前端认的那个 token。
    // 这里曾经写的是 `mode: "buy10"`——那是前端以为的形状，不是线上的形状，于是整套用例
    // 绿着、而真页面上每次 `render()` 都在 `renderBatch` 处抛 TypeError，它后面所有面板
    // （批量档位按钮 / 离线收益 / 表态 / 图鉴 / 成就 / 日志）一次都没画出来过。
    // 见 OPEN_WORK 的 N 条与 §14。
    mode: 0,
    modeName: "buy10",
    prestige: { canAscend: true, chipsOnAscend: 2 },
    era: {
      icon: "🌱", name: "第一层", index: 1, total: 4, theme: "开张", progress: 0.42,
      progressText: "42%", canAdvance: false, isFinalEra: false, nextName: "第二层",
      nextIndex: 2, chipsOnAdvance: 0, blockedReason: "还差一点",
    },
    goldenCookies: [{ instanceId: "g1", x: 0.4, y: 0.6, remainingSeconds: 5 }],
    buffs: [{ isDebuff: false, icon: "☕", name: "精神", stacks: 2, remainingSeconds: 30, description: "更快" }],
    buildings: [{
      id: "b1", isVisible: true, isUnlocked: true, canAfford: true, icon: "🏠", name: "猫窝",
      owned: 3, batchAmount: 10, batchPrice: 120, cpsContribution: 5.5, cpsShare: 0.12,
      unlockHint: "", unlockProgress: 1,
      // 「这座建筑自己的升级」——**服务端算好的 id**（`GameContent.UpgradesForBuilding`）。
      // 前端不解析 `category` 里的 `"building:"` 前缀，只做集合运算（见 app.js 的
      // buildingOwnedUpgradeIds）：形状必须与真快照一致，§14 盯着它。
      upgradeIds: ["u1"],
    }],
    upgrades: [
      {
        // 建筑专属那一条：它**不该**出现在扁平的「升级」列表里（它挂在 b1 的 ⬆ 上）。
        id: "u1", isVisible: true, isPermanent: false, isAvailable: true, isUnlocked: true,
        isMaxed: false, canAfford: true, icon: "⭐", name: "更好的碗", owned: 1,
        currencyIcon: "🐟", price: 50, effectSummary: "+10%", unlockProgress: 1, maxPurchases: 1,
      },
      {
        // 普通（全局）升级那一条：它**才**该出现在扁平的「升级」列表里。
        id: "u2", isVisible: true, isPermanent: false, isAvailable: true, isUnlocked: true,
        isMaxed: false, canAfford: false, icon: "🔧", name: "全店翻新", owned: 0,
        currencyIcon: "🐟", price: 900, effectSummary: "所有建筑 ×1.5", unlockProgress: 1, maxPurchases: 1,
      },
      {
        id: "p1", isVisible: true, isPermanent: true, isAvailable: true, isUnlocked: true,
        isMaxed: false, canAfford: false, icon: "🍃", name: "永久的那条", owned: 0,
        currencyIcon: "🍃", price: 4, description: "转生后仍在", unlockHint: "", unlockProgress: 1,
        maxPurchases: 3,
      },
    ],
    codex: {
      totalUnlocked: 1, totalEntries: 2,
      storylines: [{
        icon: "📖", name: "线", unlocked: 1, total: 2,
        entries: [
          { unlocked: true, icon: "📗", title: "读到了", body: "正文" },
          { unlocked: false, icon: "🔒", title: "没读到", revealHint: "再买点", progressText: "3/10" },
        ],
      }],
    },
    achievements: [{ unlocked: true, icon: "🏆", name: "第一只", description: "买下第一间" }],
    notifications: [{ kind: 2, icon: "⚠", message: "存档失败过一次", timestamp: 3590 }],
    pendingChoices: [],
    stances: [],
    dominantStanceId: null,
    offline: null,
    ending: null,
    ...overrides,
  };
}

const CHOICE_A = {
  id: "choice-a",
  speaker: "老板",
  prompt: "要不要留下？",
  options: [
    { id: "a1", label: "留下", stanceName: "温情", stanceIcon: "💗", weight: 2, effectSummary: "+1 猫" },
    { id: "a2", label: "走", stanceName: "野心", stanceIcon: "⚡", weight: 1, effectSummary: "钱更多" },
  ],
};

const CHOICE_B = {
  id: "choice-b",
  speaker: "账本",
  prompt: "记谁的账？",
  options: [{ id: "b1", label: "记自己的", stanceName: "自省", stanceIcon: "🪞", weight: 1 }],
};

const CHOICE_C = {
  id: "choice-c",
  speaker: "门口的猫",
  prompt: "让它进来吗？",
  options: [{ id: "c1", label: "进来吧", stanceName: "温情", stanceIcon: "💗", weight: 1 }],
};

const STANCES = [
  { id: "warm", icon: "💗", name: "温情", weight: 4, share: 0.8, isDominant: true, costText: "便宜一点" },
  { id: "ambition", icon: "⚡", name: "野心", weight: 1, share: 0.2, isDominant: false },
];

// ---------------------------------------------------------------- 跑

console.log("NekoClicker Web 冒烟测试（无头，无浏览器）");
console.log(`  app.js: ${appSource.split("\n").length} 行 / index.html: ${html.split("\n").length} 行`);

// 0. 判别力自检：把 b48f2fa 那个 bug **原样复现**一遍——删掉 `lastFrameAt` 的声明
//    （它当年就是"只赋值、没声明"），然后推一帧。必须抛 ReferenceError。
//    这条不过，后面所有"没抛异常"的结论都不算数：非严格模式会把未声明的赋值悄悄吞掉，
//    整页停摆而测试全绿——那正是 b48f2fa 与 api-test.ps1 46/46 同时成立的原因。
section("0. 测试自身的判别力（复现 b48f2fa）");
{
  const brokenSource = appSource.replace(/^let lastFrameAt = 0;.*$/m, "// 这一行被删掉：复现 b48f2fa 的「只赋值、没声明」");
  let error = null;

  if (brokenSource === appSource) {
    error = new Error("故障注入失败：app.js 里找不到 lastFrameAt 的声明");
  } else {
    const broken = await loadApp(copyAs("app-broken.mjs", brokenSource));
    try {
      await broken.push({ kind: "full", seq: 1, snapshot: snapshot() });
    } catch (caught) {
      error = caught;
    }
  }

  check("删掉 `lastFrameAt` 的声明后，第一帧必须抛 ReferenceError", () => {
    if (!error) throw new Error("没有抛错——这个测试抓不到 b48f2fa 那一类 bug");
    eq(error.name, "ReferenceError", "错误类型");
    if (!String(error.message).includes("lastFrameAt")) {
      throw new Error(`错误信息里没有 lastFrameAt：${error.message}`);
    }
  });
}

// 1. 加载真实的 app.js（未连接状态也不能抛）。
section("1. 加载 + 首帧之前的 render（state 还是 null）");
const app = await loadApp(copyAs("app-live.mjs", appSource));

check("import 没有抛异常", () => true);
check("建了 EventSource 连接", () => { eq(app.sources.length, 1, "连接数"); });
check("state 未到时 render() 直接返回（onopen 不抛）", () => {
  app.source.onopen();
  return true;
});
check("连接建立后 onerror 也不抛", () => {
  app.source.onerror();
  app.source.onopen();
  return true;
});

// 2. 满帧：什么都画一遍，且这一帧里没有待答表态。
section("2. 首帧全量快照：所有面板都画一遍");
let frameError = null;
try {
  await app.push({ kind: "full", seq: 1, snapshot: snapshot({ stances: STANCES, dominantStanceId: "warm" }) });
} catch (caught) {
  frameError = caught;
}
check("render() 全程没有抛异常（未声明的标识符会在这里现形）", () => {
  if (frameError) throw frameError;
});
check("没有待答表态时不冒 sheet（规则 1 的反面）", () => hidden(app, "choices-sheet"));
check("没有待答表态时也没有药丸", () => hidden(app, "choices-pill"));
check("立场按钮显示主导立场", () => {
  eq(hidden(app, "stances-open"), false, "立场按钮可见");
  eq(el(app, "stances-open").textContent, "💗 温情", "按钮文字");
});
check("没有待答表态时不发 choicesShown", () => eq(commandsOf(app, "choicesShown").length, 0, "choicesShown 次数"));
check("连接状态画在页面上（首帧之后「已连接」）", () => {
  eq(el(app, "status-text").textContent, "已连接", "status-text");
  eq(el(app, "status-dot").className, "dot ok", "status-dot");
});
check("建筑 / 升级 / 图鉴 / 日志 / 金猫都被画出来了", () => {
  eq(el(app, "buildings").children.length, 1, "建筑数");
  // 扁平「升级」列表**只**放不属于任何建筑的升级：夹具里是 u2。
  // 把 u1（b1 的升级）也放回这一栏，这条会红——那正是"一条线只有一个家"。
  const flat = el(app, "upgrades").children;
  eq(flat.length, 1, "普通升级数（建筑专属与永久线都不在这一栏）");
  eq(flat[0].children.find((child) => child.className === "name").textContent, "🔧 全店翻新", "留在这一栏的是哪一条");
  eq(el(app, "permanent").children.length, 1, "永久线数");
  eq(el(app, "codex").children.length, 1, "剧情线数");
  eq(el(app, "log").children.length, 1, "日志条数");
  eq(el(app, "golden").children.length, 1, "金猫数");
  eq(hidden(app, "golden"), false, "金猫浮层可见");
});
check("被移走的建筑升级**说出去**了（静默少内容是这个仓库最反对的失败形态）", () => {
  const hint = el(app, "upgrade-hint").textContent;
  if (!hint.includes("1 条")) throw new Error(`指路文案里没有说出移走了几条：<${hint}>`);
  if (!hint.includes("⬆")) throw new Error(`指路文案没有说去哪找：<${hint}>`);
});
check("选择器全部落在 index.html 真实存在的元素上（没有任何落空）", () => {
  if (app.doc.missedSelectors.length > 0) throw new Error(`落空：${app.missedSelectors.join(", ")}`);
});

// 3. 待答表态来了：sheet 自己冒出来，并且报告"已展示"。
section("3. 待答表态到达：sheet 自己出现 + 报告 choicesShown");
await app.push({ kind: "delta", seq: 2, changed: { pendingChoices: [CHOICE_A] } });
check("sheet 自己出现（规则 1：不必先点页签）", () => shown(app, "choices-sheet"));
check("sheet 出现时药丸不显示", () => hidden(app, "choices-pill"));
check("选项真的被画出来了（2 个选项 = 2 个按钮）", () => {
  const cards = el(app, "choices").children;
  eq(cards.length, 1, "表态卡片数");
  eq(cards[0].children.find((child) => child.className === "choice-options").children.length, 2, "选项数");
});
check("发了一条 choicesShown（sheet 真的显示选项就算「展示过」）", () => eq(commandsOf(app, "choicesShown").length, 1, "次数"));
check("底部按钮写着「收起，稍后再答」", () => eq(el(app, "choices-later").textContent, "收起，稍后再答", "按钮文字"));

await app.push({ kind: "delta", seq: 3, changed: {} });
check("同一批表态不会重复报告（幂等）", () => eq(commandsOf(app, "choicesShown").length, 1, "次数"));

// 4. 收起：变成药丸，而且不丢。
section("4. 收起 → 药丸（可收起，但不会丢）");
fire(el(app, "choices-later"), "click");
await flush();
check("收起后 sheet 隐藏", () => hidden(app, "choices-sheet"));
check("收起后药丸出现，并写着还有几项", () => {
  eq(hidden(app, "choices-pill"), false, "药丸可见");
  eq(el(app, "choices-pill").textContent, "🗣 还有 1 项表态", "药丸文字");
});
check("药丸出现那一下带强调类（urgent）", () => el(app, "choices-pill").classList.contains("urgent"));
check("收起之后不再重发 choicesShown（药丸只写条数，不算展示）", () => eq(commandsOf(app, "choicesShown").length, 1, "次数"));

await app.push({ kind: "delta", seq: 4, changed: {} });
check("被收起的那一批不会自己弹回来（收起就是收起）", () => hidden(app, "choices-sheet"));
check("药丸一直在（这就是「不会丢」）", () => shown(app, "choices-pill"));

check("点药丸能把 sheet 叫回来", () => {
  fire(el(app, "choices-pill"), "click");
  return shown(app, "choices-sheet");
});
check("叫回来时不会重复报告已展示过的 id", () => eq(commandsOf(app, "choicesShown").length, 1, "次数"));

// 5. 作答：答完最后一条，sheet 与药丸一起消失。
section("5. 作答：答完最后一条 → sheet 与药丸一起消失");
{
  const optionButton = el(app, "choices").children[0]
    .children.find((child) => child.className === "choice-options").children[0];
  fire(optionButton, "click");
  await flush();
  const answers = commandsOf(app, "answer");
  check("点选项发出 answer 命令（带表态 id 与选项 id）", () => {
    eq(answers.length, 1, "answer 次数");
    eq(answers[0].id, "choice-a", "表态 id");
    eq(answers[0].optionId, "a1", "选项 id");
  });
}
await app.push({ kind: "delta", seq: 5, changed: { pendingChoices: [] } });
check("答完之后 sheet 自己收起", () => hidden(app, "choices-sheet"));
check("答完之后药丸也消失（待答为 0）", () => hidden(app, "choices-pill"));

// 6. 新的一批表态：即使刚收起过，也要自己冒出来。
section("6. 新的一批表态（没被收起过）→ 再次自己出现");
await app.push({ kind: "delta", seq: 6, changed: { pendingChoices: [CHOICE_B] } });
check("新表态自己冒出来", () => shown(app, "choices-sheet"));
check("报告了新表态势必新增一条 choicesShown", () => eq(commandsOf(app, "choicesShown").length, 2, "次数"));

fire(el(app, "choices-close"), "click");
await flush();
check("右上角 × 也只是收起（药丸还在）", () => {
  eq(hidden(app, "choices-sheet"), true, "sheet 隐藏");
  eq(shown(app, "choices-pill"), true, "药丸可见");
});
check("Esc 收起（键盘路径）", () => {
  fire(el(app, "choices-pill"), "click");
  const documentHandlers = app.doc._listeners.get("keydown") ?? [];
  for (const handler of documentHandlers) {
    handler({ code: "Escape", target: { tagName: "BODY" }, preventDefault() {} });
  }
  return hidden(app, "choices-sheet");
});
await app.push({ kind: "delta", seq: 7, changed: { pendingChoices: [] } });
check("待答清零后药丸消失", () => hidden(app, "choices-pill"));

// 7. 与离线收益那张的次序：离线先讲完。
section("7. 两张 sheet 不打架：离线收益先讲完");
await app.push({
  kind: "full",
  seq: 8,
  snapshot: snapshot({
    pendingChoices: [CHOICE_C],
    offline: { cookiesText: "1.2 万", wasCapped: true, durationText: "12m 30s", elapsedSeconds: 900 },
  }),
});
check("离线收益那张显示着", () => shown(app, "offline"));
check("表态那张让位（不同时出现两张 sheet）", () => hidden(app, "choices-sheet"));
check("让位时也不放药丸（它在遮罩底下，点了也点不到）", () => hidden(app, "choices-pill"));
check("被挡住的表态**不算**已展示（没有发出 choicesShown）", () => eq(commandsOf(app, "choicesShown").length, 2, "次数（仍是 2）"));

fire(el(app, "offline-ok"), "click");
await flush();
check("收下离线收益之后，表态那张立刻自己回来", () => shown(app, "choices-sheet"));
check("回来之后才报告 choicesShown", () => eq(commandsOf(app, "choicesShown").length, 3, "次数"));

// 8. 答完之后还能回去看立场与结局（药丸已经没有了，入口是 hero 里那个按钮）。
section("8. 立场 / 结局的常驻入口");
await app.push({
  kind: "delta",
  seq: 9,
  changed: {
    pendingChoices: [],
    stances: STANCES,
    dominantStanceId: "warm",
    ending: { id: "end_open", icon: "🌅", name: "开着的门", text: "故事停在这里。" },
  },
});
check("待答清零后 sheet 收起、药丸消失", () => {
  eq(hidden(app, "choices-sheet"), true, "sheet");
  eq(hidden(app, "choices-pill"), true, "pill");
});
check("立场按钮仍在那儿", () => shown(app, "stances-open"));

fire(el(app, "stances-open"), "click");
await flush();
check("点它能重新打开那张 sheet", () => shown(app, "choices-sheet"));
check("重新打开时不发 choicesShown（没有待答，没有东西要展示）", () => eq(commandsOf(app, "choicesShown").length, 3, "次数"));
check("立场轴画出来了（2 条，主导那条带 dominant 类）", () => {
  const rows = el(app, "stances").children.filter((child) => child.className.startsWith("stance"));
  eq(rows.length, 2, "立场条数");
  eq(rows[0].classList.contains("dominant"), true, "主导条带 dominant");
});
check("结局画出来了", () => {
  const ending = el(app, "stances").children.find((child) => child.className === "ending");
  if (!ending) throw new Error("没有 .ending 节点");
  eq(ending.children[0].textContent, "🌅 结局：开着的门", "结局标题");
});
check("底部按钮这时写「关闭」（没有待答可「稍后再答」）", () => eq(el(app, "choices-later").textContent, "关闭", "按钮文字"));
await app.push({ kind: "delta", seq: 10, changed: {} });
check("玩家自己点开看立场时，不会被下一帧自动关掉", () => shown(app, "choices-sheet"));
fire(el(app, "choices-sheet"), "click", { target: el(app, "choices-sheet") });
await flush();
check("点遮罩收起", () => hidden(app, "choices-sheet"));

// 9. 只有结局、没有立场轴的内容包（11 个里有 6 个）也必须能看见结局。
section("9. 只有结局、没有立场轴的内容包");
await app.push({
  kind: "full",
  seq: 11,
  snapshot: snapshot({
    ending: { id: "end_cyber", icon: "🛰", name: "上传完成", text: "你留在了网里。" },
  }),
});
check("立场按钮出现（有结局就算有东西可看）", () => shown(app, "stances-open"));
fire(el(app, "stances-open"), "click");
await flush();
check("结局文本被画出来了（旧代码把它关在 `if (stances.length > 0)` 里，这 6 个包一次都没显示过）", () => {
  const ending = el(app, "stances").children.find((child) => child.className === "ending");
  if (!ending) throw new Error("没有 .ending 节点");
  eq(ending.children[1].textContent, "你留在了网里。", "结局正文");
});

// 10. 每一帧都要过的那些路径（animate / 键盘 / 老书签）。
section("10. animate、键盘、老书签");
check("animate() 跑一帧不抛异常", () => {
  const callbacks = [...app.frames];
  app.frames.length = 0;
  for (const callback of callbacks) callback(performance.now());
  return el(app, "cookies").textContent.length > 0;
});

fire(el(app, "choices-close"), "click");
await flush();
check("空格仍然点猫（sheet 关着时）", () => {
  const before = commandsOf(app, "click").length;
  for (const handler of app.doc._listeners.get("keydown") ?? []) {
    handler({ code: "Space", target: { tagName: "BODY" }, preventDefault() {} });
  }
  return commandsOf(app, "click").length === before + 1;
});
check("sheet 开着时空格不会偷偷点猫", () => {
  fire(el(app, "stances-open"), "click");
  if (hidden(app, "choices-sheet")) throw new Error("sheet 没打开");
  const before = commandsOf(app, "click").length;
  for (const handler of app.doc._listeners.get("keydown") ?? []) {
    handler({ code: "Space", target: { tagName: "BODY" }, preventDefault() {} });
  }
  return commandsOf(app, "click").length === before;
});
check("切页签（建筑/升级/图鉴…）照旧", () => {
  const codexTab = app.doc.tabs.find((tab) => tab.dataset.tab === "codex");
  codexTab.parent = el(app, "tabs");
  fire(el(app, "tabs"), "click", { target: codexTab });
  const visible = app.doc.panels.filter((panel) => !panel.classList.contains("hidden"));
  eq(visible.length, 1, "可见面板数");
  return visible[0].dataset.panel === "codex";
});

{
  // 老书签 #tab=choices：那一页已经不存在了，不能留下一个所有面板都隐藏的空白右栏。
  const boot = await loadApp(copyAs("app-boot.mjs", appSource), { hash: "#tab=choices" });
  await boot.push({ kind: "full", seq: 1, snapshot: snapshot() });
  check("老书签 #tab=choices 退回「建筑」，不留空白右栏", () => {
    const visible = boot.doc.panels.filter((panel) => !panel.classList.contains("hidden"));
    eq(visible.length, 1, "可见面板数");
    eq(visible[0].dataset.panel, "buildings", "可见的那一页");
    eq(boot.location.hash, "#tab=buildings", "hash 被改正");
  });
}

// 11. index.html / app.css 的形状守卫（这几条守的是需求本身，不是 JS）。
section("11. 标记与样式的形状守卫");
check("index.html 里已经没有「表态」页签与面板（不留半个死按钮）", () => {
  if (/data-tab="choices"/.test(html)) throw new Error('还有 data-tab="choices"');
  if (/data-panel="choices"/.test(html)) throw new Error('还有 data-panel="choices"');
  if (/choice-badge|tab-pop|\.tabs button\.urgent/.test(html + css + appSource)) throw new Error("还有旧页签的残留（角标 / 动画类）");
});
check("表态那两张用的是同一套 sheet 形态（没有第二套弹窗）", () => {
  const sheet = /<div id="choices-sheet" class="sheet-layer hidden">[\s\S]*?<div class="sheet"[\s\S]*?class="sheet-ok"/.exec(html);
  if (!sheet) throw new Error("choices-sheet 的标记没有沿用 .sheet-layer / .sheet / .sheet-ok");
});
check(".sheet 仍留着 max-height 与 overflow-y（关掉它的历史修复就退回「关不掉」）", () => {
  const block = /\.sheet \{([\s\S]*?)\}/.exec(css)?.[1] ?? "";
  if (!block.includes("max-height: calc(100vh - 2rem)")) throw new Error("没有 max-height");
  if (!block.includes("overflow-y: auto")) throw new Error("没有 overflow-y");
});
check(".sheet-layer 仍留着 overflow-y", () => {
  const block = /\.sheet-layer \{([\s\S]*?)\}/.exec(css)?.[1] ?? "";
  if (!block.includes("overflow-y: auto")) throw new Error("没有 overflow-y");
});
check("@keyframes sheet-in 还在（复用而不是新造动画）", () => /@keyframes sheet-in/.test(css));

// 建筑那一行的形状：两个独立控件（📖 故事 / ⬆ 升级轨）、卡片在右、两个框都跨整行。
// 这几条守的是"展开"这件事的**布局**——把它改回"点卡片出字"、把框塞进卡片里、
// 或者让三个元素回到自动排布（少一个按钮就把卡片挤进窄轨道），这几条会直接红。
check("建筑行是「两个控件 + 卡片」三列，两个展开框都跨整行（形状守卫）", () => {
  const building = /\.building \{([\s\S]*?)\}/.exec(css)?.[1] ?? "";
  if (!building.includes("grid-template-columns: auto auto 1fr")) throw new Error(".building 不是三列网格");
  // 显式列号：少一个按钮时**空的 auto 轨道宽度是 0**，卡片不能因此掉进窄轨道里。
  if (!/\.story-toggle \{[\s\S]*?grid-column: 1;/.test(css)) throw new Error(".story-toggle 没有钉在第 1 列");
  if (!/\.upgrade-toggle \{[\s\S]*?grid-column: 2;/.test(css)) throw new Error(".upgrade-toggle 没有钉在第 2 列");
  if (!/\.building > \.card \{ grid-column: 3; \}/.test(css)) throw new Error(".building > .card 没有钉在第 3 列");
  const story = /\.story \{([\s\S]*?)\}/.exec(css)?.[1] ?? "";
  if (!story.includes("grid-column: 1 / -1")) throw new Error(".story 没有跨整行");
  const track = /\.track \{([\s\S]*?)\}/.exec(css)?.[1] ?? "";
  if (!track.includes("grid-column: 1 / -1")) throw new Error(".track 没有跨整行");
});
check("展开按钮的「开着」状态有独立样式（只靠 aria-expanded 这一件事驱动）", () => {
  if (!/\.story-toggle\[aria-expanded="true"\]/.test(css)) throw new Error("没有 aria-expanded=true 的样式");
  if (!/\.upgrade-toggle\[aria-expanded="true"\]/.test(css)) throw new Error("⬆ 没有 aria-expanded=true 的样式");
});
check("展开按钮在窄屏上撑到 44px（📖 / ⬆ 只有一字符宽，按不到就等于没有）", () => {
  const narrow = /@media \(max-width: 34rem\) \{([\s\S]*?)\n\}/.exec(css)?.[1] ?? "";
  if (!/\.story-toggle \{ min-width: 44px; \}/.test(narrow)) throw new Error("窄屏那节里没有 .story-toggle 的 44px");
  if (!/\.upgrade-toggle \{ min-width: 44px; \}/.test(narrow)) throw new Error("窄屏那节里没有 .upgrade-toggle 的 44px");
});

// 12. 计数器动画：单调、不振荡。这一条守的是 human 报的"数字来回跳"。
//
// 病灶有两处，都在 animate / onmessage 里：显示值**本来就该领先服务端**（那正是外推
// 存在的意义），可旧代码拿"显示值 > 服务端"当"服务端倒退了"的证据，于是每来一帧就把
// 已经画上去的数字往下按一次；另一处是把超出上限的部分只拉回一半（拉回就是倒退）。
// 下面用可复现的合成时钟（loadApp 的 step）按真实的 4 Hz 推帧节奏把这两条路径走一遍。
section("12. 计数器动画：只前进，只在服务端倒退时倒退");
{
  const anim = await loadApp(copyAs("app-anim.mjs", appSource));

  const TICK_MS = 50;            // 一拍 50ms
  const FRAME_TICKS = 5;         // 5 拍 = 250ms，与 GameHost.PushIntervalSeconds 一致
  const RATE = 60;               // 每秒 60：一拍正好 +3，格式化后是纯整数，序列一眼可读
  const TOLERANCE = RATE * 0.5;  // animate 里的上限：半秒的产量 = 两帧的量
  const START = 100;
  const PRICE = 40;              // 买入之后服务端的钱包落到这里
  const LAG_MS = [20, 220];      // SSE 投递延迟在 20ms / 220ms 之间摆——抖动就是这个形状

  const read = () => Number(el(anim, "cookies").textContent.replace(/,/g, ""));
  const samples = [];            // 屏幕上真的画出来的值 + 那一刻最新的服务端值
  let server = START;
  const tap = () => samples.push({ shown: read(), server });
  const ticks = (count) => { for (let i = 0; i < count; i++) { anim.step(TICK_MS); tap(); } };
  const values = () => samples.map((sample) => sample.shown);
  const trace = (list) => list.join(" → ");
  const firstDrop = (list) => {
    for (let i = 1; i < list.length; i++) if (list[i] < list[i - 1]) return i;
    return -1;
  };

  await anim.push({ kind: "full", seq: 1, snapshot: snapshot({ cookies: server, cookiesPerSecond: RATE }) });
  anim.step(0); // 步长 0：只让 animate 把首帧画进 DOM，不推进时钟
  tap();
  const ascentFrom = samples.length;

  // 8 帧 = 2 秒，服务端每帧都在**前进**：它报的是采样于 (到达时刻 - 延迟) 的真值，
  // 只是投递晚了；本地在这段时间里已经按 cps 外推过去了。现实中每 250ms 就是这样。
  for (let frame = 1; frame <= 8; frame++) {
    ticks(FRAME_TICKS);
    const lag = LAG_MS[frame % LAG_MS.length] / 1000;
    const arrival = (frame * FRAME_TICKS * TICK_MS) / 1000;
    server = START + RATE * (arrival - lag);
    await anim.push({ kind: "delta", seq: 1 + frame, changed: { cookies: server } });
  }
  ticks(FRAME_TICKS);
  const ascent = samples.slice(ascentFrom);

  check("服务端一直在前进时，数字一拍一拍往上爬，一次都不往回走", () => {
    const list = ascent.map((sample) => sample.shown);
    const i = firstDrop(list);
    if (i !== -1) throw new Error(`第 ${i} 拍倒退了：${list[i - 1]} → ${list[i]}（整段：${trace(list)}）`);
    const flat = list.findIndex((value, index) => index > 0 && value === list[index - 1]);
    if (flat !== -1) throw new Error(`第 ${flat} 拍卡住不动了（外推应当每一拍都往上）：${trace(list)}`);
  });
  check("领先服务端的量始终不超过 tolerance（半秒产量 = 两帧）", () => {
    let worst = 0;
    for (const sample of ascent) worst = Math.max(worst, sample.shown - sample.server);
    if (worst > TOLERANCE + 1e-9) throw new Error(`最多领先 ${worst}，超过上限 ${TOLERANCE}`);
    if (worst <= 0) throw new Error("一次都没有领先过服务端——外推没在工作");
  });

  // 买入：服务端自己倒退了。这是唯一被允许的下降，而且必须是**一次干净**的下降。
  const beforeDrop = samples[samples.length - 1].shown;
  server = PRICE;
  await anim.push({ kind: "delta", seq: 10, changed: { cookies: server } });
  const dropFrom = samples.length;
  ticks(3);
  const afterDrop = samples.slice(dropFrom);

  check("服务端自己倒退时只允许一次下降，而且一步落到新的真值上", () => {
    const list = values();
    const drops = [];
    for (let i = 1; i < list.length; i++) if (list[i] < list[i - 1]) drops.push(i);
    if (drops.length !== 1) throw new Error(`整段里下降了 ${drops.length} 次（只允许买入那一次）：${trace(list)}`);
    const landed = list[drops[0]];
    const ceiling = PRICE + RATE * (TICK_MS / 1000);
    if (landed < PRICE || landed > ceiling) {
      throw new Error(`落点 ${landed} 不在 [${PRICE}, ${ceiling}]：不是一步落到真值，而是在慢慢滑（整段：${trace(list)}）`);
    }
    if (beforeDrop <= landed) throw new Error(`没有真的下降：${beforeDrop} → ${landed}`);
  });
  check("倒退之后立刻恢复只增不减，落点以下再也没有出现过", () => {
    const list = afterDrop.map((sample) => sample.shown);
    const i = firstDrop(list);
    if (i !== -1) throw new Error(`买入之后又来回：${trace(list)}`);
    if (list.some((value) => value < PRICE)) throw new Error(`掉到落点以下：${trace(list)}`);
  });

  // 断流：SSE 掉了（EventSource 正在重连），服务端的值不再更新，但 rAF 还在跑。
  // 显示值必须**停下来等**，而不是一直外推、再被拉回来。
  const stallFrom = samples.length;
  ticks(30);
  const stall = samples.slice(stallFrom).map((sample) => sample.shown);

  check("断流时数字收敛到一个定值，而不是绕着真值来回摆", () => {
    const i = firstDrop(stall);
    if (i !== -1) throw new Error(`断流期间倒退了：${trace(stall)}`);
    const tail = stall.slice(-10);
    if (tail.some((value) => value !== tail[0])) throw new Error(`最后 10 拍还在变：${trace(tail)}`);
    const lead = tail[0] - PRICE;
    if (lead > TOLERANCE + 1e-9) throw new Error(`停在真值上方 ${lead}，超过上限 ${TOLERANCE}`);
  });
}

// 13. 建筑：卡片"买"、📖"看故事"。守的是需求本身——"把建筑写成可以点开的文本框，
//     把剧情内容藏在里面"。三个判别点各有一条：
//       · 点 📖 **不发 buy**（否则"看故事"会顺手花掉钱）；
//       · 点卡片**不展开**故事框（否则每一次购买都会弹出一段字）；
//       · 展开状态**不被下一帧快照合上**（服务端 4 Hz 推帧，列表每帧都过一遍）。
//     说明文本全部来自快照（内容包里 text.json 的 buildings.<id>.description）：
//     这里刻意用一份**两段**的合成文案，证明文本框放得下不止一段，而不是只有一行。
section("13. 建筑：卡片买、📖 看故事（两个手势互不触发）");
{
  const story = await loadApp(copyAs("app-story.mjs", appSource));

  const catBed = {
    id: "b1", isVisible: true, isUnlocked: true, canAfford: true, icon: "🏠", name: "猫窝",
    owned: 3, batchAmount: 10, batchPrice: 120, cpsContribution: 5.5, cpsShare: 0.12,
    unlockHint: "", unlockProgress: 1,
    description: "猫在里面睡 16 小时。\n\n剩下 8 小时思考要不要出来。",
  };
  const feeder = {
    id: "b2", isVisible: true, isUnlocked: false, canAfford: false, icon: "🍽️", name: "自动喂食器",
    owned: 0, batchAmount: 1, batchPrice: 900, cpsContribution: 0, cpsShare: 0,
    unlockHint: "累计赚到 300", unlockProgress: 0.4,
    description: "定时投喂，它记得这个机器。",
  };
  const full = (buildings) => ({ kind: "full", seq: 1, snapshot: snapshot({ buildings }) });

  await story.push(full([catBed, feeder]));

  const row = (index) => el(story, "buildings").children[index];
  const find = (index, cls) => row(index).children.find((child) => child.classList.contains(cls));
  const toggleOf = (index) => find(index, "story-toggle");
  const cardOf = (index) => find(index, "card");
  const storyOf = (index) => find(index, "story");
  const openAt = (index) => {
    const box = storyOf(index);
    return Boolean(box) && !box.classList.contains("hidden");
  };
  const pressKey = (target, code) => {
    for (const handler of story.doc._listeners.get("keydown") ?? []) {
      handler({ code, target, preventDefault() {} });
    }
  };

  check("每一行都是「📖 + 卡片 + 故事框」三件，且默认全部收起", () => {
    eq(el(story, "buildings").children.length, 2, "建筑行数");
    for (const index of [0, 1]) {
      if (!toggleOf(index)) throw new Error(`第 ${index + 1} 行没有故事按钮`);
      if (!cardOf(index)) throw new Error(`第 ${index + 1} 行没有卡片`);
      if (!storyOf(index)) throw new Error(`第 ${index + 1} 行没有故事框`);
      if (openAt(index)) throw new Error(`第 ${index + 1} 行的故事框默认是开着的`);
    }
  });

  check("卡片仍然只做一件事：点它发 buy，而且不展开任何故事框", () => {
    fire(cardOf(0), "click");
    const buys = commandsOf(story, "buy");
    eq(buys.length, 1, "buy 次数");
    eq(buys[0].id, "b1", "买的是哪一座");
    if (openAt(0)) throw new Error("买的时候顺手展开了故事框");
  });

  check("点 📖 不发 buy，只展开那一行（看故事不是购买）", () => {
    fire(toggleOf(0), "click");
    eq(commandsOf(story, "buy").length, 1, "buy 次数（仍是 1，没有多出来一发）");
    if (!openAt(0)) throw new Error("点了 📖 但故事框没展开");
    if (openAt(1)) throw new Error("展开第 1 行时第 2 行也跟着开了");
  });

  check("故事框里就是快照给的那段话，逐字一致；空行分成两段", () => {
    const parts = storyOf(0).children.map((child) => child.textContent);
    eq(parts.length, 2, "段数（空行分段）");
    eq(parts[0], "猫在里面睡 16 小时。", "第一段");
    eq(parts[1], "剩下 8 小时思考要不要出来。", "第二段");
  });

  check("锁着的那一行也读得到自己的说明（说明文本不按解锁状态藏）", () => {
    fire(toggleOf(1), "click");
    if (!openAt(1)) throw new Error("锁着的行打不开故事框");
    eq(storyOf(1).children.length, 1, "段数");
    eq(storyOf(1).children[0].textContent, "定时投喂，它记得这个机器。", "正文");
    eq(commandsOf(story, "buy").length, 1, "读说明没有顺手下单");
    fire(toggleOf(1), "click"); // 收回去，后面的断言才好读
  });

  // 下一帧（同样的两行、只有价格变了）来了：开着的必须还开着，关着的必须还关着。
  const beforeRow = row(0);
  const beforeToggle = toggleOf(0);
  await story.push(full([{ ...catBed, batchPrice: 240 }, feeder]));
  check("重画之后：开着的还开着，关着的还关着", () => {
    if (!openAt(0)) throw new Error("下一帧把展开着的那个故事框合上了");
    if (openAt(1)) throw new Error("下一帧把本来关着的那一行打开了");
  });
  check("重画没有把这一行换成新节点（焦点与展开按钮的身份都还留着）", () => {
    if (row(0) !== beforeRow) throw new Error("整行被换成了新节点");
    if (toggleOf(0) !== beforeToggle) throw new Error("展开按钮被换成了新按钮（键盘焦点会掉回 body）");
  });

  check("再点一下能收起（同一个按钮关得掉自己打开的东西），且不发 buy", () => {
    fire(toggleOf(0), "click");
    if (openAt(0)) throw new Error("点了第二下没收起");
    eq(commandsOf(story, "buy").length, 1, "buy 次数");
    fire(toggleOf(0), "click"); // 再打开，给后面几条用
  });

  check("展开按钮带 aria-expanded / aria-controls，故事框是带名字的 region", () => {
    const toggle = toggleOf(0);
    const box = storyOf(0);
    eq(toggle.tagName, "BUTTON", "展开按钮的标签（Tab 停得到、回车/空格自带激活）");
    eq(toggle.type, "button", "type（不该是提交按钮）");
    eq(toggle.getAttribute("aria-controls"), box.id, "aria-controls 指向哪一个框");
    eq(toggle.getAttribute("aria-expanded"), "true", "展开时的 aria-expanded");
    eq(box.getAttribute("role"), "region", "故事框的 role");
    if (!box.getAttribute("aria-label")) throw new Error("故事框没有可读的名字");
    fire(toggle, "click");
    eq(toggle.getAttribute("aria-expanded"), "false", "收起时的 aria-expanded");
    fire(toggle, "click");
  });

  check("键盘：焦点在 📖 上按空格不会顺手点一下猫（空格归那个按钮自己）", () => {
    const before = commandsOf(story, "click").length;
    pressKey({ tagName: "BUTTON" }, "Space");
    return commandsOf(story, "click").length === before;
  });
  check("键盘：焦点不在任何控件上时空格照旧点猫", () => {
    const before = commandsOf(story, "click").length;
    pressKey({ tagName: "BODY" }, "Space");
    return commandsOf(story, "click").length === before + 1;
  });

  // 说明文本是快照给的：没有它就不该造一个点开空空如也的 📖
  // （引擎侧那条不变量保证每座建筑的说明非空，所以这是形状守卫，不是活路径）。
  await story.push(full([{ ...feeder, id: "b3", name: "没写说明的建筑", description: "" }]));
  check("快照里没有说明的那一座不给 📖（不做点开空空如也的按钮）", () => {
    eq(el(story, "buildings").children.length, 1, "建筑行数（换了一帧之后只剩这一座）");
    if (toggleOf(0)) throw new Error("没有说明却给了故事按钮");
    if (storyOf(0)) throw new Error("没有说明却建了故事框");
    eq(row(0).children.length, 1, "这一行只剩卡片");
  });
}

// 14. 夹具不是谎话：形状对着**真宿主抓下来的快照**比。
//
// 这一节存在的唯一理由，是上面那条线上故障：夹具里写 `mode: "buy10"`，线上却是 `"mode":0`，
// 于是 `render()` 每帧在 `renderBatch` 处抛 TypeError、它后面所有面板一次都没画出来过——
// **而 82 条用例全绿**。判据只有一条：夹具里前端会用到的每个路径，形状必须与真快照一致。
//
// 真快照 = tools/fixtures/web-snapshot.json，就是从起着的宿主上抓的**原始响应**：
//   curl.exe -s "http://127.0.0.1:5299/api/snapshot?package=apocalypse"
// 它是**形状**的参照，不是数值的参照：重生成时数值（金币、时长、通知时间戳）会变，
// 那是预期的；这里比的是类型，所以数值漂移不会让它红。
section("14. 夹具形状 vs 真宿主快照");
{
  const golden = JSON.parse(readFileSync(join(root, "tools", "fixtures", "web-snapshot.json"), "utf8"));

  /** JSON 形状名：null / array / object / string / number / boolean。 */
  const shapeOf = (value) => {
    if (value === null) return "null";
    if (Array.isArray(value)) return "array";
    if (value === undefined) return "missing";
    return typeof value;
  };

  /**
   * 收集形状差异。规则三条：
   *   · 任一边是 null 就放过——null 表示"这个内容包没有这一项"（stances / offline / ending…），
   *     而夹具恰恰是用来演练"有"的那条路的；
   *   · 夹具里有、真快照里没有 = 夹具**编**了一个线上不存在的字段（`mode` 就是这么来的）；
   *   · 形状不同 = 同一类谎，点名到具体路径。
   */
  function shapeMismatches(fixture, wire, path, into) {
    const left = shapeOf(fixture);
    const right = shapeOf(wire);
    if (left === "null" || right === "null") return;

    if (left === "missing" || right === "missing") {
      into.push(`${path || "<顶层>"}：夹具是 ${left}，真快照是 ${right}`);
      return;
    }

    if (left !== right) {
      into.push(`${path}：夹具是 ${left}，线上是 ${right}（${JSON.stringify(fixture)} vs ${JSON.stringify(wire)}）`);
      return;
    }

    if (left === "object") {
      for (const key of Object.keys(fixture)) {
        shapeMismatches(fixture[key], wire[key], path ? `${path}.${key}` : key, into);
      }
      return;
    }

    if (left === "array" && fixture.length > 0 && wire.length > 0) {
      // 数组只比第 0 个元素：服务端按同一个 record 序列化每一行，字段集合是同一套。
      shapeMismatches(fixture[0], wire[0], `${path}[0]`, into);
    }
  }

  check("夹具里前端会用到的每个路径，形状都与真宿主快照一致", () => {
    const mismatches = [];
    shapeMismatches(snapshot(), golden, "", mismatches);
    if (mismatches.length > 0) {
      throw new Error(`夹具与真快照对不上 ${mismatches.length} 处：\n      · ${mismatches.join("\n      · ")}`);
    }
  });
  // 这个方向只能发现"夹具里**写了的**路径错了"；反过来的"夹具漏了某个前端要用的字段"
  // 由 C# 的 WebSnapshotProtocolTests.FrontendContract_FieldNamesAndTheModeToken
  // 与下面那条"真快照推过 render()"一起守（漏字段会让那条渲染用例红）。

  // app.js 里还有没有人在读那个枚举序数。按行去掉注释再扫——文档里**必须**能提
  // `state.mode`（那正是要解释的坑），但代码里不许再出现。
  check("app.js 的代码里不再读 state.mode（序数只配躺在快照里）", () => {
    const code = appSource
      .replace(/\/\*[\s\S]*?\*\//g, "")
      .split("\n")
      .map((line) => line.replace(/\/\/.*$/, ""))
      .filter((line) => /\bstate\.mode\b/.test(line));
    if (code.length > 0) throw new Error(`还在读序数：${code.map((line) => line.trim()).join(" | ")}`);
  });

  // 同一份真快照推过整个 render()。判据不只是"没抛"——**晚段**才该出现的效果必须真的出现：
  // `renderBatch` 在 render() 的中段，它后面的东西一条都不该少。这就是当初那条故障的判别力。
  const real = await loadApp(copyAs("app-real.mjs", appSource));
  let renderError = null;
  try {
    await real.push({ kind: "full", seq: 1, snapshot: golden });
  } catch (error) {
    renderError = error;
  }

  check("真宿主快照推过 render()：全程不抛异常", () => {
    if (renderError) throw renderError;
  });
  check("中段之后的批量档位按钮真的画出来了（4 个，且高亮的是 modeName 那一个）", () => {
    const buttons = el(real, "batch").children;
    eq(buttons.length, 4, "档位按钮数");
    const active = buttons.filter((button) => button.classList.contains("active"));
    eq(active.length, 1, "高亮的按钮数");
    const labels = { buy1: "×1", buy10: "×10", buy100: "×100", buymax: "买满" };
    eq(active[0].textContent, labels[golden.modeName], `高亮的是 <${golden.modeName}> 对应的那个`);
  });
  check("图鉴 / 成就 / 日志 / 建筑这些同样在它后面的面板也都画了", () => {
    if (golden.codex && el(real, "codex").children.length === 0) throw new Error("图鉴一条都没画");
    eq(el(real, "achievement-list").children.length, golden.achievements.length, "成就行数");
    if (el(real, "log").children.length === 0) throw new Error("日志一条都没画");
    eq(el(real, "buildings").children.length, golden.buildings.filter((b) => b.isVisible).length, "可见建筑行数");
  });
  check("离线收益那张的可见性与真快照一致（它也在 renderBatch 之后）", () => {
    eq(el(real, "offline").classList.contains("hidden"), golden.offline === null, "offline 那张 sheet");
  });
}

// 15. 建筑自己的升级（`buildings[].upgradeIds`）挂在建筑上，与 📖 并列的第二个手势。
//
// 这一节守的是一个**静默失效**：引擎从 1.4.0 起就把「这条升级属于哪座建筑」写在
// `UpgradeDefinition.Category` 上（`"building:<id>"`），十一个包 312 条都在用，
// 而快照里 `category` / `tier` / `nextMilestoneAt` **一处都没被前端读过**——
// 于是"建筑专属升级"在玩家眼里根本不存在，且没有任何东西会因此变红。
// 现在的形状是服务端把 id 列表算好（`upgradeIds`），前端只做集合运算，不解析约定。
section("15. 建筑自己的升级：⬆ 展开、点一下买那一条（不是买建筑）");
{
  const track = await loadApp(copyAs("app-track.mjs", appSource));

  const catBed = {
    id: "b1", isVisible: true, isUnlocked: true, canAfford: true, icon: "🏠", name: "猫窝",
    owned: 3, batchAmount: 10, batchPrice: 120, cpsContribution: 5.5, cpsShare: 0.12,
    unlockHint: "", unlockProgress: 1,
    description: "猫在里面睡 16 小时。",
    upgradeIds: ["t1", "t2"],
  };
  const feeder = {
    id: "b2", isVisible: true, isUnlocked: true, canAfford: false, icon: "🍽️", name: "自动喂食器",
    owned: 0, batchAmount: 1, batchPrice: 900, cpsContribution: 0, cpsShare: 0,
    unlockHint: "", unlockProgress: 1,
    description: "定时投喂。",
    upgradeIds: [], // 这座建筑没有任何升级 → **不给 ⬆**
  };
  const upgrades = [
    {
      id: "t1", isVisible: true, isPermanent: false, isAvailable: true, isUnlocked: true,
      isMaxed: false, canAfford: true, icon: "⭐", name: "更好的碗", owned: 1,
      currencyIcon: "🐟", price: 50, effectSummary: "+100% 猫窝", unlockProgress: 1, maxPurchases: 1,
    },
    {
      id: "t2", isVisible: true, isPermanent: false, isAvailable: true, isUnlocked: false,
      isMaxed: false, canAfford: false, icon: "🌟", name: "连成片的猫窝", owned: 0,
      currencyIcon: "🐟", price: 500, effectSummary: "猫窝 ×2", unlockHint: "有 10 个猫窝", unlockProgress: 0.3,
      maxPurchases: 1,
    },
    {
      id: "g1", isVisible: true, isPermanent: false, isAvailable: true, isUnlocked: true,
      isMaxed: false, canAfford: true, icon: "🔧", name: "全店翻新", owned: 0,
      currencyIcon: "🐟", price: 900, effectSummary: "所有建筑 ×1.5", unlockProgress: 1, maxPurchases: 1,
    },
  ];
  const full = (buildings, rows = upgrades) =>
    ({ kind: "full", seq: 1, snapshot: snapshot({ buildings, upgrades: rows }) });

  await track.push(full([catBed, feeder]));

  const row = (index) => el(track, "buildings").children[index];
  const find = (index, cls) => row(index).children.find((child) => child.classList.contains(cls));
  const toggleOf = (index) => find(index, "upgrade-toggle");
  const trackOf = (index) => find(index, "track");
  const cardOf = (index) => find(index, "card");
  const openAt = (index) => {
    const box = trackOf(index);
    return Boolean(box) && !box.classList.contains("hidden");
  };
  /** 轨道里的升级行（跳过表头那一行）。 */
  const trackRowsAt = (index) => trackOf(index).children.filter((child) => child.classList.contains("track-row"));

  check("有升级的建筑才有 ⬆；没有升级的那座**不给**这个按钮", () => {
    if (!toggleOf(0)) throw new Error("b1 有升级却没有 ⬆");
    if (toggleOf(1)) throw new Error("b2 一条升级都没有，却给了 ⬆（点开是空的）");
    if (!trackOf(0)) throw new Error("b1 没有升级轨容器");
    if (trackOf(1)) throw new Error("b2 没有升级，却建了轨容器");
    if (openAt(0)) throw new Error("升级轨默认是开着的");
  });

  check("⬆ 是真 button，带 aria-expanded / aria-controls", () => {
    const toggle = toggleOf(0);
    eq(toggle.tagName, "BUTTON", "标签");
    eq(toggle.type, "button", "type（不该是提交按钮）");
    eq(toggle.getAttribute("aria-controls"), trackOf(0).id, "aria-controls 指向哪一个框");
    eq(toggle.getAttribute("aria-expanded"), "false", "收起时的 aria-expanded");
  });

  check("徽标 = 这座建筑现在买得起的条数（t1 买得起、t2 锁着 → 1）", () => {
    const badge = toggleOf(0).children.find((child) => child.classList.contains("badge"));
    if (!badge) throw new Error("⬆ 上没有徽标");
    eq(badge.textContent, "1", "徽标数字");
    eq(badge.classList.contains("hidden"), false, "有买得起的就应当显示徽标");
  });

  check("点 ⬆ 只展开这一行，**不发 buy**", () => {
    fire(toggleOf(0), "click");
    eq(commandsOf(track, "buy").length, 0, "buy 次数（看升级不是买建筑）");
    if (!openAt(0)) throw new Error("点了 ⬆ 但升级轨没展开");
    if (openAt(1)) throw new Error("展开第 1 行时第 2 行也跟着开了");
    eq(toggleOf(0).getAttribute("aria-expanded"), "true", "展开时的 aria-expanded");
  });

  check("轨里就是这座建筑那几条（按服务端给的顺序），锁着的那条也在", () => {
    const rows = trackRowsAt(0);
    eq(rows.length, 2, "轨内行数（含锁着的那一条）");
    const names = rows.map((node) => node.children.find((child) => child.className === "name").textContent);
    eq(names[0], "⭐ 更好的碗 ✔", "第 1 条");
    eq(names[1], "🔒 连成片的猫窝", "第 2 条（锁着也要看得见：这一行回答的是「还有什么」）");
  });

  check("点轨里的一条升级发的是 upgrade，而且发的是**那一条**的 id", () => {
    fire(trackRowsAt(0)[0], "click");
    const sent = commandsOf(track, "upgrade");
    eq(sent.length, 1, "upgrade 次数");
    eq(sent[0].id, "t1", "发出去的 id");
    eq(commandsOf(track, "buy").length, 0, "全程没有买建筑");
  });

  check("锁着的升级行点不动（真 DOM 里 disabled 的按钮本来就点不到）", () => {
    const locked = trackRowsAt(0)[1];
    eq(locked.disabled, true, "锁着的那一行 disabled");
    if (!locked.classList.contains("locked")) throw new Error("锁着的那一行没有 locked 类");
    fire(locked, "click");
    eq(commandsOf(track, "upgrade").length, 1, "upgrade 次数（没有多出来一发）");
  });

  // 下一帧：只有价格变了。开着的必须还开着，节点必须还是同一个（焦点会掉的话就在这里）。
  const beforeRow = row(0);
  const beforeToggle = toggleOf(0);
  const beforeTrackRow = trackRowsAt(0)[0];
  await track.push(full([{ ...catBed, batchPrice: 240 }, feeder]));

  check("重画之后：开着的还开着，关着的还关着", () => {
    if (!openAt(0)) throw new Error("下一帧把展开着的升级轨合上了");
    if (openAt(1)) throw new Error("下一帧把本来关着的那一行打开了");
  });
  check("重画没有把这一行 / ⬆ / 轨内行换成新节点", () => {
    if (row(0) !== beforeRow) throw new Error("整行被换成了新节点");
    if (toggleOf(0) !== beforeToggle) throw new Error("⬆ 被换成了新按钮（键盘焦点会掉回 body）");
    if (trackRowsAt(0)[0] !== beforeTrackRow) throw new Error("轨内的升级行被换成了新节点");
  });

  check("再点一下能收起；收起之后轨里还是那两条（不丢内容）", () => {
    fire(toggleOf(0), "click");
    if (openAt(0)) throw new Error("点了第二下没收起");
    eq(toggleOf(0).getAttribute("aria-expanded"), "false", "收起时的 aria-expanded");
    eq(trackRowsAt(0).length, 2, "收起之后轨内的行数");
  });

  // 徽标与"买得起"是同一份口径：把钱包抬高，t2 也买得起 → 徽标变 2。
  // 这里刻意**不改** upgradeIds，只改行本身——徽标必须跟着行走。
  await track.push({
    kind: "full", seq: 2,
    snapshot: snapshot({
      buildings: [catBed, feeder],
      upgrades: upgrades.map((row_) => (row_.id === "t2" ? { ...row_, isUnlocked: true, canAfford: true } : row_)),
    }),
  });
  check("徽标跟着「买得起」走（t2 也能买了 → 2）", () => {
    const badge = toggleOf(0).children.find((child) => child.classList.contains("badge"));
    eq(badge.textContent, "2", "徽标数字");
    eq(toggleOf(0).classList.contains("affordable"), true, "⬆ 上的第二重信号");
  });

  // 换一帧：b1 的升级**换成了别的两条**（成员变了）→ 轨必须重建，
  // 而且重建之后展开状态还在（它是按建筑 id 记的，不是按行记的）。
  const swapped = upgrades.filter((row_) => row_.id === "g1").concat([{
    id: "t9", isVisible: true, isPermanent: false, isAvailable: true, isUnlocked: true,
    isMaxed: false, canAfford: false, icon: "🧺", name: "换过的升级", owned: 0,
    currencyIcon: "🐟", price: 10, effectSummary: "x", unlockProgress: 1, maxPurchases: 1,
  }]);
  await track.push(full([{ ...catBed, upgradeIds: ["t9"] }, feeder], swapped));
  check("轨的成员真的变了才重建（新的一条在里面，旧的不在）", () => {
    fire(toggleOf(0), "click"); // 重新展开
    const rows = trackRowsAt(0);
    eq(rows.length, 1, "轨内行数");
    eq(
      rows[0].children.find((child) => child.className === "name").textContent,
      "🧺 换过的升级",
      "换过之后的那一条");
  });

  // upgradeIds 指向一条 `upgrades[]` 里根本没有的 id：不许抛，也不许造一个空行。
  await track.push(full([{ ...catBed, upgradeIds: ["t9", "missing"] }, feeder], swapped));
  check("upgradeIds 里有对不上的 id 时：跳过它，不抛异常、不造空行", () => {
    eq(trackRowsAt(0).length, 1, "轨内行数（missing 被跳过）");
  });

  check("键盘：焦点在 ⬆ 上按空格不会顺手点一下猫", () => {
    const before = commandsOf(track, "click").length;
    for (const handler of track.doc._listeners.get("keydown") ?? []) {
      handler({ code: "Space", target: { tagName: "BUTTON" }, preventDefault() {} });
    }
    return commandsOf(track, "click").length === before;
  });
}

rmSync(scratch, { recursive: true, force: true });

// ---------------------------------------------------------------- 结果

console.log(`\n${checks - failures.length} / ${checks} 条通过`);
if (failures.length > 0) {
  console.log("\n失败：");
  for (const failure of failures) console.log(`  · ${failure}`);
  process.exit(1);
}
console.log("OK");
process.exit(0);
