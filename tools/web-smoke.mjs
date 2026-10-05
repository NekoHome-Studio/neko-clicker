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
// 这个脚本**从 2026-10 起进了 tools/build.ps1**（也进了 CI）：一条 202 条断言（2026-10-04 起；
// §26 的「等高」之前是 199 条、§27「批量档位下的总价」之前是 193 条、
// §25「缩放适配」与 §26「两栏对齐」之前是 162 条、
// 第 24 节「存档的导出/导入窗口」之前是
// 135 条）、其中 210 行
// 专为"复发过五次"的 bug 类而写的套件，不进自动闸门就等于没有守卫。
// 需要 node —— 没有 node 时 build.ps1 **故意红**（并给出 -SkipWebSmoke 这条人工出路），
// 而不是静默跳过：静默跳过正是它当初缺闸门时的那种失效形态。
// （旧注释写着"刻意不进来，免得『没有 node 的机器上还能不能过』变成新问题"。那个问题是真的，
//  现在的答案是"让它红、并且只留一个人工开关"，而不是"不接"。）

import { readFileSync, writeFileSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const wwwroot = join(root, "games", "hosts", "Web", "wwwroot");

const html = readFileSync(join(wwwroot, "index.html"), "utf8");
const css = readFileSync(join(wwwroot, "app.css"), "utf8");
// 去掉注释之后再用来解析**声明**。这不是洁癖：这一版新加的那几条 CSS 常量断言第一次跑
// 就踩了坑——`main` 的注释里写着「footer 早就有 max-width: 60rem」，于是"读 main 的
// max-width"读到了注释里的那个数（60rem），把真声明改成 9999rem 那条用例照样全绿。
// 注释在浏览器里不参与层叠，在这里也不该参与断言。
const cssRules = css.replace(/\/\*[\s\S]*?\*\//g, "");
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
    this._value = "";
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

  /**
   * 表单控件的当前值。<b>存档窗口那两张文本框靠它</b>：app.js 读 `.value`（不是 textContent），
   * 而且只读那一份也要能设（宿主回来的文本写进去）。
   */
  get value() { return this._value; }
  set value(value) { this._value = value === undefined || value === null ? "" : String(value); }

  append(...nodes) { for (const node of nodes) { node.parent = this; this.children.push(node); } }

  /** 从父节点摘掉（下载用的 <a> 会挂进 body 再摘掉）。 */
  remove() {
    if (this.parent) {
      const at = this.parent.children.indexOf(this);
      if (at >= 0) this.parent.children.splice(at, 1);
      this.parent = null;
    }
  }

  /**
   * 焦点。<b>计数而不是布尔</b>：`focus()` 只记"被调过几次"，这样"打开时给导出框、
   * 关闭时还给入口按钮"可以两条都断言到。
   */
  focus() { this.focused = (this.focused ?? 0) + 1; }

  /** 选中（剪贴板写不进去时的退路：把文本选中让人按 Ctrl+C）。 */
  select() { this.selected = true; }

  /**
   * 脚本点一下。**只记数、不派发监听器**：真 DOM 里 `click()` 会走一遍 click 监听器，
   * 而这里派发的话，测试自己用 fire() 触发时就会与它互相干扰。今天的用法只有一处
   * （下载用的那个 <a>，它没有监听器），所以这条简化不会让任何断言变成假绿。
   */
  click() { this.clicked = (this.clicked ?? 0) + 1; }

  addEventListener(type, handler) {
    if (!this._listeners.has(type)) this._listeners.set(type, []);
    this._listeners.get(type).push(handler);
  }
  setAttribute(name, value) { this._attrs.set(name, String(value)); }
  getAttribute(name) { return this._attrs.has(name) ? this._attrs.get(name) : null; }
  removeAttribute(name) { this._attrs.delete(name); }

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
    // index.html 上的**每一个**属性都收进 `_attrs`（不只 id / class / data-*）：
    // 静态标记里写着的 aria 状态（例如建筑页签的 aria-current="true"）在真浏览器里
    // 首帧就生效，桩里也必须读得到——否则"首帧的 aria-current 与 class=active 对齐"
    // 这条只能靠 app.js 再写一遍才成立，而那是在替浏览器做事。
    for (const attr of attrs.matchAll(/([\w:-]+)="([^"]*)"/g)) el.setAttribute(attr[1], attr[2]);
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
    /** `document.body`：下载用的 `<a>` 要先挂进文档再点（有些浏览器否则不触发下载）。 */
    body: new El("body"),
    /** 脚本创建过的节点（存档窗口的下载链接要在这里面找）。 */
    created: [],
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
    createElement(tag) {
      const node = new El(tag);
      this.created.push(node);
      return node;
    },
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

/**
 * 装好全局环境并 import 一份 app.js 副本；返回这一份的观测句柄。
 *
 * `reply`：**按命令给不同的回答**。默认那份桩对每条命令都回 `{ok:true}`，
 * 而"导入坏文本要显示引擎那句话"这件事必须能让宿主回一次 `{ok:false, message}` ——
 * 否则那半条路只能靠人眼看浏览器（本机起不了浏览器，见 OPEN_WORK §0.6）。
 *
 * `clipboard`：要不要给一个可用的 `navigator.clipboard`。**两种都要测**：
 * 一种走"复制成功"，另一种走"写不进去"的退路（那时不许假装成功）。
 */
async function loadApp(moduleUrl, { hash = "", reply = null, clipboard = true } = {}) {
  const doc = makeDom();
  const location = { search: "", hash };
  const commands = [];
  const sources = [];
  const frames = [];
  const blobs = [];
  const clipboardWrites = [];
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
    const answer = reply ? reply(body, commands.length) : null;
    return { ok: true, json: async () => answer ?? { ok: true, seq: commands.length } };
  };

  // `navigator` 在 Node 21+ 是**只读**的全局 getter：直接赋值在严格模式（ES 模块）下会抛，
  // 必须用 defineProperty。剪贴板那两条路都要能造出来（见上面的 `clipboard`）。
  Object.defineProperty(globalThis, "navigator", {
    value: clipboard
      ? { clipboard: { writeText: async (text) => { clipboardWrites.push(text); } } }
      : {},
    configurable: true,
    writable: true,
  });

  // 下载那一路：把 Blob 留下来，测试可以 `await blob.text()` 与文本框逐字节比对。
  globalThis.URL.createObjectURL = (blob) => { blobs.push(blob); return `blob:stub/${blobs.length}`; };
  globalThis.URL.revokeObjectURL = () => {};

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

  return { doc, location, commands, blobs, clipboardWrites, push, sources, frames, source, step };
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
      // 层内**阶段**（1.9.0）。形状必须与真快照一致——`tools/fixtures/web-snapshot.json`
      // 现在是从**公司包**抓的（它是第一个声明阶段的包），§14 盯着这六个字段。
      // `stageIndex` 从 1 起且含开场那一个，所以走到最后一个阶段时 `stageNextName` 是空串。
      stageIndex: 2, stageCount: 4, stageName: "猫窝扩建", stageNextName: "第二间房",
      stageProgress: 0.42, stageProgressText: "4.5 万 / 600 万（1%）",
    },
    goldenCookies: [{ instanceId: "g1", x: 0.4, y: 0.6, remainingSeconds: 5 }],
    buffs: [{ isDebuff: false, icon: "☕", name: "精神", stacks: 2, remainingSeconds: 30, description: "更快" }],
    buildings: [{
      id: "b1", isVisible: true, isUnlocked: true, canAfford: true, icon: "🏠", name: "猫窝",
      owned: 3, batchAmount: 10, batchPrice: 120, cpsEach: 4.5, cpsContribution: 5.5, cpsShare: 0.12,
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
check("纪元面板里那行「第 k/n 阶段」画出来了（层内进度不再是空白）", () => {
  eq(hidden(app, "era-stage"), false, "阶段那一行的可见性");
  eq(el(app, "era-stage").textContent, "第 2 / 4 阶段 · 下一阶段：第二间房（42%）", "阶段那一行的文字");
  // 原始门槛（还差多少）塞不进一行，放在 title 里——它是玩家真正想看的那个数。
  eq(el(app, "era-stage").title, "4.5 万 / 600 万（1%）", "阶段那一行的 title");
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
  if (!/\.building > \.card \{ grid-column: 3;/.test(css)) throw new Error(".building > .card 没有钉在第 3 列");
  const story = /\.story \{([\s\S]*?)\}/.exec(css)?.[1] ?? "";
  if (!story.includes("grid-column: 1 / -1")) throw new Error(".story 没有跨整行");
  const track = /\.track \{([\s\S]*?)\}/.exec(css)?.[1] ?? "";
  if (!track.includes("grid-column: 1 / -1")) throw new Error(".track 没有跨整行");
});
check("展开按钮的「开着」状态有独立样式（只靠 aria-expanded 这一件事驱动）", () => {
  if (!/\.story-toggle\[aria-expanded="true"\]/.test(css)) throw new Error("没有 aria-expanded=true 的样式");
  if (!/\.upgrade-toggle\[aria-expanded="true"\]/.test(css)) throw new Error("⬆ 没有 aria-expanded=true 的样式");
});
check("展开按钮在**任何宽度**下都 ≥44px（基规则里就有，不是「窄屏才有」——原先只有 2rem = 32px）", () => {
  const story = /\.story-toggle \{([\s\S]*?)\}/.exec(css)?.[1] ?? "";
  if (!story.includes("min-width: 44px")) throw new Error(".story-toggle 的基规则里没有 44px");
  const upgrade = /\.upgrade-toggle \{([\s\S]*?)\}/.exec(css)?.[1] ?? "";
  if (!upgrade.includes("min-width: 44px")) throw new Error(".upgrade-toggle 的基规则里没有 44px");
});

// 面板里那两段说明（`#upgrade-hint` / `#permanent-hint`）与列表里的空态段此前只有
// 浏览器默认的 `margin: 1em 0`：.85rem 字号下上下各 13.6px，比这块面板里其它所有间距
// （.4~.75rem = 6.4~12px）都大一档。形状守卫：这两处必须自己写出 margin。
check("面板说明段 / 列表空态段有显式 margin（不留 UA 默认的 1em = 13.6px）", () => {
  if (!/\.panel > p\.muted \{ margin:/.test(css)) throw new Error("没有 .panel > p.muted 的 margin");
  if (!/\.list > p\.muted \{ margin:/.test(css)) throw new Error("没有 .list > p.muted 的 margin");
});

// 折不断的长 token（长英文名 / 连写的 id）不许把卡片顶出面板。两条缺一不可：
// `overflow-wrap: anywhere` 把 min-content 压到一个字符，`min-width: 0` 去掉网格项
// 默认的 `min-width: auto`（= min-content，会把这个卡片所在的 1fr 轨道撑开）。
check("卡片正文折得断：overflow-wrap: anywhere + min-width: 0", () => {
  const block = /\.card \.name, \.card \.price, \.card \.share, \.card \.effect \{([\s\S]*?)\}/.exec(css)?.[1];
  if (block === undefined) throw new Error("没有那一条合并的卡片正文规则");
  if (!block.includes("overflow-wrap: anywhere")) throw new Error("没有 overflow-wrap: anywhere");
  if (!block.includes("min-width: 0")) throw new Error("没有 min-width: 0");
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
// 真快照 = tools/fixtures/web-snapshot.json，就是从起着的宿主上抓的**原始响应**。
// 抓它的脚本**在仓库里**（原先只有某次会话的 .tmp/capture-fixture.ps1，已 gitignore）：
//   pwsh -File tools/capture-fixture.ps1 -Package company -Port 5398
// 它自己起宿主、--save-root 与 --latency-log 都指向临时目录（**仓库真实 saves/ 一个字节不碰**）、
// 抓之前先 POST 几十次 click 让通知日志非空、按响应体**原始字节**落盘，并打印字节数 /
// sha256 / mode / era / 通知条数。落盘前它还会拒绝几种"看着像快照但是坏的"结果
// （buildings 空、notifications 空、`mode` 是字符串——最后这条正是下面那条线上故障的形态）。
// 它现在是**按公司包**抓的（`?package=company`，60,859 字节）——公司是第一个声明阶段
// （`era.stageIndex` / `stageCount` / `stageName` / `stageNextName` / `stageProgress` /
// `stageProgressText`）的包，所以这份夹具同时是那六个字段"真的在线上"的判据。
// 它以前是按末世包抓的：那次切换是为了让新字段有**真实取值**可比，而不是一个空数组。
// 它是**形状**的参照，不是数值的参照：重生成时数值（金币、时长、通知时间戳、阶段号）会变，
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

// 16. 层内阶段那一行的边界情形。
//
// 这一节守的是三条**静默**失效：
//   ① 服务端说"阶段走完了"（stageNextName 空）时，前端如果照旧拼"下一阶段：（100%）"，
//      就是把一个不存在的阶段画给玩家看——而它不会报错。
//   ② 没有声明阶段的包（8/9 个）必须整行隐藏，而不是显示"第 0 / 0 阶段"。
//   ③ **老引擎 + 新前端**：宿主的 wwwroot 是源码目录，所以页面可能比引擎先更新。
//      那时快照里根本没有 stageCount 这几个字段，前端必须当作"没有阶段"处理。
//      这条不是假想：改这一版的同一天，就有一个 play-test 宿主正在别处跑着旧引擎。
section("16. 层内阶段：走到最后一段、没有阶段、老引擎三种情形");
{
  const stageApp = await loadApp(copyAs("app-stage.mjs", appSource));

  const withStage = (eraFields) => {
    const snap = snapshot();
    snap.era = { ...snap.era, ...eraFields };
    return snap;
  };

  await stageApp.push({ kind: "full", seq: 1, snapshot: withStage({ stageIndex: 4, stageCount: 4, stageNextName: "" }) });
  check("走到最后一个阶段时说「这一层的阶段走完了」，不再编一个下一阶段", () => {
    eq(el(stageApp, "era-stage").textContent, "第 4 / 4 阶段 · 这一层的阶段走完了", "最后一阶段的文字");
  });

  await stageApp.push({ kind: "delta", seq: 2, changed: { era: withStage({ stageIndex: 1, stageCount: 0, stageName: "", stageNextName: "" }).era } });
  check("stageCount 为 0 时整行隐藏（没有声明阶段的包不许多出一行）", () => {
    eq(hidden(stageApp, "era-stage"), true, "阶段那一行的可见性");
    eq(el(stageApp, "era-stage").textContent, "", "隐藏时文字要清空");
  });

  const oldEngineEra = {
    icon: "🌱", name: "第一层", index: 1, total: 4, theme: "开张", progress: 0.42,
    progressText: "42%", canAdvance: false, isFinalEra: false, nextName: "第二层",
    nextIndex: 2, chipsOnAdvance: 0, blockedReason: "还差一点",
  };
  await stageApp.push({ kind: "delta", seq: 3, changed: { era: oldEngineEra } });
  check("老引擎的快照里没有阶段字段：当作没有阶段，不抛异常也不显示半行", () => {
    eq(hidden(stageApp, "era-stage"), true, "阶段那一行的可见性");
    eq(hidden(stageApp, "era"), false, "纪元面板本身照常显示");
  });

  // 阶段字段是"有值但为 0"的那种脏数据（服务端理论上不会给，但前端不该因此把它画出来）
  await stageApp.push({ kind: "delta", seq: 4, changed: { era: withStage({ stageIndex: 0, stageCount: 0 }) } });
  check("stageIndex 为 0（脏数据）时同样隐藏，不画「第 0 / 0 阶段」", () => {
    eq(hidden(stageApp, "era-stage"), true, "阶段那一行的可见性");
  });
}

// 17. 触屏下限与 400px 水平预算（全部从 app.css 里的常量算出来，不看渲染）。
//
// 为什么要有这一节：本机结构性起不了浏览器（OPEN_WORK §0.6），于是「够不够得着」
// 「会不会挤出去」这类问题只有两条活路——请真人看一眼，或者把写死在 CSS 里的常量
// 拿出来算一遍。这一节走的是第二条：每个数字都明文写在 app.css 里，算出来的预算与
// 阈值都进断言，红的时候差额直接印在消息里。
section("17. 触屏下限与 400px 水平预算（CSS 常量算出来的）");
{
  const REM = 16;
  /** 从某条规则里读一个尺寸（px 或 rem），读不到返回 null。 */
  const sizeOf = (block, prop) => {
    const m = new RegExp(`${prop}:\\s*([\\d.]+)(px|rem)`).exec(block);
    return m ? Number(m[1]) * (m[2] === "rem" ? REM : 1) : null;
  };
  const blockOf = (selector) =>
    new RegExp(selector.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + " \\{([\\s\\S]*?)\\}").exec(cssRules)?.[1] ?? "";

  const TARGET = 44; // 这个仓库自己那条线（.choices-pill / .sheet-ok 都是 44px）

  check("建筑行整行 ≥44px（两个 44px 宽的按钮 align-self: stretch，行高由卡片决定）", () => {
    const height = sizeOf(blockOf(".building > .card"), "min-height");
    if (height === null) throw new Error("`.building > .card` 没有 min-height——卡片会比它旁边的 📖 / ⬆ 矮");
    if (height < TARGET) throw new Error(`卡片行只有 ${height}px，低于 ${TARGET}px`);
  });

  check("sheet 右上角 × 是 44×44（那张 sheet 唯一常驻的关闭控件）", () => {
    const block = blockOf(".sheet-close");
    const width = sizeOf(block, "min-width");
    const height = sizeOf(block, "min-height");
    if (width === null || height === null) throw new Error("没有 min-width / min-height");
    if (width < TARGET || height < TARGET) throw new Error(`${width}×${height}，低于 ${TARGET}×${TARGET}`);
  });

  // 400px 视口下的横向预算。三个常量都来自 app.css：
  //   · body 左右内边距 = clamp(1rem, 4vw, 3rem)，400px 时 4vw = 16px（正好取到 1rem 那一端）；
  //   · main 在 ≤34rem 那节里的左右内边距 = .6rem；
  //   · .building 的列间距 = .35rem，两处。
  const bodyPad = Math.max(16, Math.min(0.04 * 400, 48));
  const mainPad = 0.6 * REM;
  const columnGap = 0.35 * REM;
  const available = 400 - 2 * bodyPad - 2 * mainPad;
  const toggles = 2 * TARGET + 2 * columnGap;

  check("400px 下建筑行放得下：两个 44px 按钮之后，卡片还剩 ≥100px（附实际数字）", () => {
    const left = available - toggles;
    // 卡片自己的 min-content 上界：最长的一段是价格行里**折不断**的那一段。
    // 价格行现在是 `×N 总价 {图标} {数}`（§27 第二版：限定语在前、没有括号），
    // 按空格切开之后最长的一段是那个数本身——量级缩写最多 6 个字符（`3.40百万` / `2.92十亿`），
    // 而 `priceText` 给 1 以下的小价格留了小数，最多 7 个字符（`0.12345`，见 §27）；
    // `×100` 是 4 个、`总价` 是 2 个，都比它短。按 1em/字符这个对任何字体都成立的上界算，
    // 在最大的那一档卡片字号（.card .name = .92rem）下是 103.0px，
    // 加上左右内边距 2×.7rem 与 2px 边框。
    // （第一版那一行是 `{图标} {数} 总价（×N）`，同样落在 7 上，但来源是 `总价（×10）`；
    //  次序调过来之后括号没了，7 只剩「1 以下的价格」这一种来源——**数没变，理由变了**。）
    const bound = 7 * 0.92 * REM + 2 * 0.7 * REM + 2;
    if (left < bound) throw new Error(`卡片只剩 ${left.toFixed(1)}px，而它最少要 ${bound.toFixed(1)}px`);
    if (left < 100) throw new Error(`只剩 ${left.toFixed(1)}px（可用 ${available.toFixed(1)} − 按钮 ${toggles.toFixed(1)}），余量不足 100px`);
  });

  check("400px 下 sheet 里的立场轴：12rem 固定列之后，进度条还有 ≥96px", () => {
    // .sheet { width: min(26rem, 100%) } 外面还套着 .sheet-layer 的 1rem 内边距。
    const outer = Math.min(26 * REM, 400 - 2 * REM);
    const inner = outer - 2 * 1.3 * REM;
    const fixed = 9 * REM + 2.5 * REM + 2 * 0.5 * REM; // .stance 的 9rem + 2.5rem + 两处 .5rem 间隙
    const bar = inner - fixed;
    if (bar < 96) throw new Error(`进度条只剩 ${bar.toFixed(1)}px（sheet 内宽 ${inner.toFixed(1)}px − 固定列 ${fixed}px）`);
  });
}

// 18. 超宽屏：正文行宽上限。
//
// 2K 屏上原先没有上限：main 的第二列有多少给多少，卡片正文一行能到约 1990px
// （字号最小的 .effect 是 .75rem ≈ 135 个汉字）。这条把「上限存在」与「按它算出来的
// 行宽不超过 80 个汉字」一起钉住——删掉 app.css 里那条 max-width，它会红。
section("18. 超宽屏：正文行宽上限");
{
  const REM = 16;
  const mainBlock = /main \{([\s\S]*?)\}/.exec(cssRules)?.[1] ?? "";
  const capMatch = /max-width:\s*([\d.]+)rem/.exec(mainBlock);
  const cap = capMatch ? Number(capMatch[1]) * REM : null;

  check("main 声明了宽度上限（footer 早就有 max-width: 60rem，右栏此前没有）", () => {
    if (cap === null) throw new Error("main 没有 max-width：2560px 屏上卡片正文一行约 1990px");
  });

  check("按这个上限算：最小一档卡片字号（.effect 的 .75rem）每行不超过 80 个汉字", () => {
    if (cap === null) throw new Error("没有 max-width，无从计算");
    const hero = 26 * REM;        // 第一列的上限 minmax(20rem, 26rem)
    const gap = 1.5 * REM;
    const panelPad = 2 * 1.1 * REM;
    const cardPad = 2 * 0.7 * REM;
    const text = cap - hero - gap - panelPad - cardPad - 2;
    const glyphs = text / (0.75 * REM);
    if (glyphs > 80) throw new Error(`一行 ${glyphs.toFixed(1)} 个汉字（正文栏 ${text.toFixed(0)}px），超过 80`);
  });
}

// 19. 建筑卡片说得出「已有几个」。
//
// `buildings[].owned` 此前**一次都没显示过**：app.js 每帧把它写进一个 `<span class="owned">`，
// 而 CSS 从第一个前端提交（d238825）起就是 `.card .owned { display: none }`——数字一直在被
// 计算、被写进 DOM，然后被藏掉。终端宿主有这一列（TerminalUi.BuildingRow 的 Owned），
// 这边没有。现在它并进本来就只在 owned > 0 时出现的那一行。
section("19. 建筑卡片说得出「已有几个」");
{
  const own = await loadApp(copyAs("app-owned.mjs", appSource));
  const bed = (count) => ({
    id: "b1", isVisible: true, isUnlocked: true, canAfford: true, icon: "🏠", name: "猫窝",
    owned: count, batchAmount: 10, batchPrice: 120, cpsEach: 4.5, cpsContribution: 5.5, cpsShare: 0.12,
    unlockHint: "", unlockProgress: 1, description: "猫在里面睡。", upgradeIds: [],
  });
  const partsOf = (cls) =>
    el(own, "buildings").children[0].children
      .find((child) => child.classList.contains("card")).children
      .find((child) => child.classList.contains(cls));

  await own.push({ kind: "full", seq: 1, snapshot: snapshot({ buildings: [bed(3)] }) });

  check("owned > 0：卡片上写出「已有 3」，与价格行上的批次记号 `×10` 用两个词分开", () => {
    const share = partsOf("share");
    if (share.classList.contains("hidden")) throw new Error("share 那一行被藏起来了");
    if (!share.textContent.includes("已有 3")) throw new Error(`那一行上没有「已有 3」：<${share.textContent}>`);
    const price = partsOf("price").textContent;
    if (!price.includes("×10")) throw new Error(`价格行上的批次记号不见了：<${price}>`);
    if (share.textContent.includes("×3")) throw new Error(`「已有」写成了批次的记号：<${share.textContent}>`);
  });

  await own.push({ kind: "full", seq: 2, snapshot: snapshot({ buildings: [bed(0)] }) });

  check("owned = 0：不写「已有 0」（那一行本来就不显示，0 不是信息）", () => {
    const share = partsOf("share");
    if (!share.classList.contains("hidden")) throw new Error("owned 为 0 时那一行还显示着");
    if (share.textContent.includes("已有")) throw new Error(`写了「已有 0」：<${share.textContent}>`);
  });
}

// 20. 状态要能被读屏读到。
//
// 页面此前没有一处 aria 状态：批量档位「哪一档生效」只写进 `.active` 这个类名，
// 页签「在哪一页」也只写进 `.active`，而提示条（钱不够 / 存档失败 / 连不上宿主）
// 根本没有实时区域——读屏用户既看不到颜色，也读不到类名，那些消息等于不存在。
section("20. 状态要能被读屏读到（提示条 / 批量档位 / 页签）");
{
  check("提示条是一个实时区域（role=status：隐含 aria-live=polite + atomic）", () => {
    if (!/<div id="toast"[^>]*role="status"/.test(html)) throw new Error("#toast 上没有 role=status");
  });
  // 这一条此前写的是"页面上**唯一**的实时区域"。**加存档窗口之后那句话不再为真**
  // （导入的失败原因必须留在屏幕上，所以 `#import-result` 也是一条 role=status）——
  // 于是这里改成"哪一条"的守卫，"**恰好几条**"由 §24 末条盯着（数量比存在更值得钉）。

  check("批量档位有一个组名（role=group + aria-label）——四个符号自己说不清是干什么的", () => {
    if (!/<div id="batch"[^>]*role="group"/.test(html)) throw new Error("#batch 上没有 role=group");
    if (!/<div id="batch"[^>]*aria-label="[^"]+"/.test(html)) throw new Error("#batch 上没有 aria-label");
  });

  const aria = await loadApp(copyAs("app-aria.mjs", appSource));
  await aria.push({ kind: "full", seq: 1, snapshot: snapshot() });

  check("批量档位里恰好一个 aria-pressed=true，且就是 modeName 那一个（夹具是 buy10）", () => {
    const buttons = el(aria, "batch").children;
    const pressed = buttons.filter((button) => button.getAttribute("aria-pressed") === "true");
    eq(pressed.length, 1, "按下的个数");
    eq(pressed[0].textContent, "×10", "按下的是哪一个");
    const others = buttons.filter((button) => button.getAttribute("aria-pressed") === "false");
    eq(others.length, 3, "其余三个显式写着 false");
  });

  check("首帧页签：恰好一个 aria-current=true，且与 index.html 里的 class=active 对齐", () => {
    const current = aria.doc.tabs.filter((tab) => tab.getAttribute("aria-current") === "true");
    eq(current.length, 1, "带 aria-current 的页签数");
    eq(current[0].dataset.tab, "buildings", "标的是哪一页");
  });

  check("切页签之后 aria-current 跟着走（旧的那个要被摘掉，不能留下两个）", () => {
    const upgrades = aria.doc.tabs.find((tab) => tab.dataset.tab === "upgrades");
    upgrades.parent = el(aria, "tabs");
    fire(el(aria, "tabs"), "click", { target: upgrades });
    const current = aria.doc.tabs.filter((tab) => tab.getAttribute("aria-current") === "true");
    eq(current.length, 1, "带 aria-current 的页签数");
    eq(current[0].dataset.tab, "upgrades", "现在标的是哪一页");
    return true;
  });

  check("Tab 顺序：离线那张 sheet 的按钮排在页面正文之前（模态先够得着）", () => {
    const order = aria.doc.all.map((node) => node.id);
    if (!(order.indexOf("offline-ok") < order.indexOf("big-cat"))) {
      throw new Error("离线 sheet 的「收下」排到了大猫后面：Tab 要先走一遍背景才够得着那张窗口");
    }
  });

  // 这一行必须**两样都齐**：有说明（才有 📖 与故事框）又有 upgradeIds（才有 ⬆ 与轨）。
  // 夹具里那一座没有 description，所以这里先推一帧齐全的——否则测到的是"少了两个控件"的顺序。
  // 用一个新的 id（节点的形状在**第一次见到那座建筑时**就定下来了：说明文本是内容里的静态
  // 字段，同一个 id 中途长出说明文本不是线上会发生的事）。
  await aria.push({
    kind: "full", seq: 2,
    snapshot: snapshot({
      buildings: [{
        id: "b9", isVisible: true, isUnlocked: true, canAfford: true, icon: "🏠", name: "猫窝",
        owned: 3, batchAmount: 1, batchPrice: 120, cpsContribution: 5.5, cpsShare: 0.12,
        unlockHint: "", unlockProgress: 1, description: "猫在里面睡。", upgradeIds: ["u1"],
      }],
    }),
  });

  check("建筑行内的 Tab 顺序：📖 → ⬆ → 买卡片，两个展开框排在卡片之后（视觉顺序 = DOM 顺序）", () => {
    const row = el(aria, "buildings").children[0];
    const order = row.children.map((child) =>
      child.classList.contains("story-toggle") ? "story-toggle"
        : child.classList.contains("upgrade-toggle") ? "upgrade-toggle"
          : child.classList.contains("card") ? "card"
            : child.classList.contains("story") ? "story" : "track");
    eq(order.join(" > "), "story-toggle > upgrade-toggle > card > story > track", "行内的 DOM 顺序");
  });
}

// 21. 阶段那一行 vs「再买 N 个解锁「X」」——两个概念不许读成同一件事。
//
// 里程碑那几个字段（`buildings[].nextMilestoneAt` / `NextMilestoneName`）**在快照里**，
// 但今天只有终端宿主读它，措辞是「再买 N 个解锁「X」」（`Demo.Cli/TerminalUi.cs:457-460`）；
// 页面上一个字符都没画过（`CONTENT_AUTHORING` §794 与 `BUILDING_UPGRADES_PLAN` §86 都写着
// "语义一字未动"，但**没有一处守卫**）。这一节把"今天屏幕上只有阶段那一行"钉成事实，
// 并禁止阶段那一行借用里程碑的措辞。将来真要在界面上画里程碑，这条会红——那时要做的
// 是把它画成**另一个元素**，而不是把两句话并进同一行。
section("21. 阶段那一行 vs 里程碑那一句（不许读成同一件事）");
{
  const words = await loadApp(copyAs("app-words.mjs", appSource));
  const bed = {
    id: "b1", isVisible: true, isUnlocked: true, canAfford: true, icon: "🏠", name: "猫窝",
    owned: 3, batchAmount: 1, batchPrice: 120, cpsContribution: 5.5, cpsShare: 0.12,
    unlockHint: "", unlockProgress: 1, description: "猫在里面睡。", upgradeIds: [],
    // 线上形状：这两个字段一直随 buildings[] 推过来（见 tools/fixtures/web-snapshot.json）。
    nextMilestoneAt: 10, nextMilestoneName: "正式配齐的猫窝",
  };
  await words.push({ kind: "full", seq: 1, snapshot: snapshot({ buildings: [bed] }) });

  // 桩里的 textContent 是"自己的文字"（不聚合子节点），所以遍历一遍就是全页的可见文字。
  const allText = [];
  const collect = (node) => {
    if (node.textContent) allText.push(node.textContent);
    for (const child of node.children) collect(child);
  };
  for (const node of words.doc.all) collect(node);

  check("阶段那一行说的是「第 k / n 阶段 · …」，而且没有借里程碑的措辞", () => {
    const stage = el(words, "era-stage").textContent;
    if (!/^第 \d+ \/ \d+ 阶段 · /.test(stage)) throw new Error(`阶段那一行不是那个形状：<${stage}>`);
    if (stage.includes("解锁")) throw new Error(`阶段那一行借了里程碑的措辞：<${stage}>`);
  });

  check("页面上没有任何一处画出「再买 N 个解锁「X」」那一句（它今天只活在终端宿主里）", () => {
    const hit = allText.find((text) => text.includes("解锁「"));
    if (hit) throw new Error(`有人在页面上画了里程碑那一句：<${hit}>`);
  });

  check("里程碑的名字即使在快照里，也不会跑到阶段那一行上去", () => {
    const stage = el(words, "era-stage").textContent;
    if (stage.includes("正式配齐的猫窝")) throw new Error(`阶段那一行里出现了里程碑的名字：<${stage}>`);
  });
}

// 22. 建筑卡片上的「单个产速」（`buildings[].cpsEach`）。
//
// 这是 `WEB_BUILDING_RATE_FEEDBACK.md` 的第一层：那个数**早就随着快照推过来了**，
// 终端详情面板也一直在画（`TerminalUi.cs` 的 `单个 4.5/s`），只有 Web 前端一次都没引用过
// （`grep cpsEach games/hosts/Web/wwwroot/` 当年是零命中，卡片上也没有 tooltip 藏着它）。
// 修法：并进建筑卡片本来就有的那一行（`已有 N · …`），措辞照终端（单个 / 合计 / 占）。
//
// 这一节钉住三件事：那一行真的同时写出单个与合计、**小数不会被抹掉**、以及
// `owned == 0` 时宁可不说不画出「单个 0/s」。
section("22. 建筑卡片上的「单个产速」");
{
  const perUnit = await loadApp(copyAs("app-rate.mjs", appSource));
  // 反馈件 §1 那一局 🔌 发电机的真值：持有 20、单个 4.5/s、合计 90/s、占 41.28%。
  const generator = {
    id: "b1", isVisible: true, isUnlocked: true, canAfford: true, icon: "🔌", name: "发电机",
    owned: 20, batchAmount: 1, batchPrice: 1636.65, cpsEach: 4.5,
    cpsContribution: 90, cpsShare: 0.4128,
    unlockHint: "", unlockProgress: 1, description: "烧油。", upgradeIds: [],
  };
  const shareOf = () =>
    el(perUnit, "buildings").children[0].children
      .find((child) => child.classList.contains("card")).children
      .find((child) => child.classList.contains("share"));

  await perUnit.push({ kind: "full", seq: 1, snapshot: snapshot({ buildings: [generator] }) });

  check("卡片那一行同时写出「单个」与「合计」，次序与终端详情面板一致", () => {
    eq(shareOf().textContent, "已有 20 · 单个 4.5/s · 合计 90/s · 占 41%", "那一行");
  });

  // 🧱 废墟那一档：单个 0.225/s。`number()`（价格 / 合计用的那个格式化器）在 1000 以下取整，
  // 会把 0.225 画成 `0/s`——不是舍入，是一句"这建筑不产钱"。所以 `rate()` 单独存在。
  await perUnit.push({
    kind: "full", seq: 2,
    snapshot: snapshot({ buildings: [{ ...generator, icon: "🧱", name: "废墟", owned: 9, cpsEach: 0.225, cpsContribution: 2.025, cpsShare: 0.0093 }] }),
  });

  check("1 以下的产速不被抹成 0：0.225 → 「单个 0.225/s」（用 number() 会画成 0/s）", () => {
    const text = shareOf().textContent;
    if (!text.includes("单个 0.225/s")) throw new Error(`小数被抹掉了：<${text}>`);
  });

  await perUnit.push({
    kind: "full", seq: 3,
    snapshot: snapshot({ buildings: [{ ...generator, owned: 0, cpsEach: 0, cpsContribution: 0, cpsShare: 0 }] }),
  });

  check("owned = 0：那一行整行不出现，页面上也没有一处写出「单个 0/s」那句假话", () => {
    const share = shareOf();
    if (!share.classList.contains("hidden")) throw new Error("owned 为 0 时那一行还显示着");
    if (share.textContent.includes("单个")) throw new Error(`写出了「单个 0/s」这种假话：<${share.textContent}>`);
  });
}

// 23. 快照里的每个字段都要有人决定过：画了，或者写明为什么不画。
//
// 这是**同一类缺陷的第五次**——引擎算好、快照推过来，而 Web 前端一次都没画过：
//   · `category` / `tier` / `nextMilestoneAt`（§15 与 §21 记着它们只活在终端宿主里）
//   · `buildings[].owned`（§19，2026-10-03 修：数字一直在算、一直在写、然后被 CSS 藏掉）
//   · `buildings[].cpsEach`（§22，2026-10-03 修）
// 五次都是**人注意到**或**代理审计**发现的——仓库里没有任何东西会在"多了一个没人画的字段"
// 时变红。这一节就是那个东西：以后**没决定过的新字段会让这一节红，并点名到完整路径**。
//
// 判据分两层，缺一不可：
//   · **正向**：夹具（`tools/fixtures/web-snapshot.json`，真宿主抓下来的线上形状）里逐个字段，
//     要么 app.js 的代码里读得到（`.字段名`），要么在下面的 `NOT_DRAWN` 表里有一句理由。
//   · **反向**：表里每一条**必须还是真的**——字段还在夹具里、且确实没被引用。
//     过期的借口也是红：否则这张表会慢慢烂成"一些字段的历史注记"，而不是一份决定记录。
//
// **粒度选的是"下钻到记录内部"**（`buildings[].cpsEach` 这一级），不是只数顶层字段。
// 理由就是这五次事故本身：五个字段里**没有一个是顶层字段**——顶层只有 `buildings` /
// `upgrades` 这些容器，而它们当然都被引用了，只守顶层一条都逮不到。
//
// **引用是怎么认的（这条守卫的边界，不假装它更强）**：把 app.js 的注释剥掉之后找 `.字段名`。
// 它证明的是"有人碰过这个名字"，**不是"这个字段被画出来了"；名字撞车（将来多一个 `icon` /
// `progress`）会漏**。它的判别力来自一个事实：这五次事故的字段在 app.js 里一个字符都没有。
// **注释不算引用**：注释里必须能提 `nextMilestoneAt` 这类名字（那正是"为什么不画"要解释的东西），
// 所以先剥注释再扫。今天这个文件里既没有声明解构、也没有 `obj["x"]`（第四条用例守着这两件事），
// 所以 `.字段名` 是完备的读法；参数解构（`function f({ a })`）是这条规则看不见的第三种写法，
// 今天一处都没有（`grep '(\s*{'` 只命中一个对象字面量）。**前端 = app.js**：index.html 与
// app.css 都画不出快照里的字段。
section("23. 快照里的每个字段都要有人决定过（画了，或写明不画）");
{
  const wire = JSON.parse(readFileSync(join(root, "tools", "fixtures", "web-snapshot.json"), "utf8"));

  const frontCode = appSource
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .split("\n")
    .map((line) => line.replace(/\/\/.*$/, ""))
    .join("\n");

  /**
   * 「明确不画」表：**完整路径 → 一句理由**。
   *
   * 键用完整路径（`buildings[].category`）而不是裸字段名：同一个名字在两种记录里可以是
   * 两件事（`hiddenUntilUnlocked` 在 `buildings[]` 与 `upgrades[]` 上各有各的处置），
   * 只写名字等于把两个决定合成一个。
   *
   * 理由里凡是真的"还没决定"，就写"未决定"——不编一个听起来像设计的说法。
   */
  const NOT_DRAWN = {
    // ---- 顶层：整块没有落点的数字
    "clickPower": "前端画的是服务端格式化好的 clickPowerText，裸数没有画布",
    "cookiesEarnedThisRun": "页面上没有统计面板——这些数一个落点都没有（未决定要不要做）",
    "cookiesEarnedAllTime": "同上：没有统计面板（未决定）",
    "handMadeCookies": "同上：没有统计面板（未决定）",
    "totalClicks": "同上：没有统计面板（未决定）",
    "ascensions": "同上：没有统计面板（未决定）",
    "purchasedUpgrades": "同上：没有统计面板（未决定）",
    "goldenCookiesClicked": "终端状态行画「已抓 N 只」；Web 没有那个位置（未决定）",
    "goldenCookieCountdown": "Web 只画在场的那只金猫（goldenCookies[]）；「下一只还有多久」没画（未决定）",
    "totalBuildings": "终端画「建筑 N」；Web 的建筑面板本身就是它，没有重复写（未决定）",
    "prestigeLevel": "前端画的是 canAscend / chipsOnAscend；等级本身没有落点（未决定）",
    "mode": "枚举序数：前端只认 modeName 这个 token，序数由 §14 单独守着不许读（不许前端解释序数）",
    // ---- buildings[]：五次事故里有三次在这一行
    "buildings[].category": "服务端把分组算成了 upgradeIds；前端不解析 category 的 building: 前缀（既有规矩）",
    "buildings[].hiddenUntilUnlocked": "隐藏规则由服务端算成 isVisible / isUnlocked（Views.cs 的一行属性），前端只读结果",
    "buildings[].unitPrice": "前端画的是真正会扣的 batchPrice，批量档位下还写明「总价」（§27）；单价那一份没画（未决定）",
    "buildings[].nextMilestoneAt": "里程碑那一句今天只活在终端宿主里，§21 明确守着「页面不许画它」",
    "buildings[].nextMilestoneName": "同上：§21 守着它不许跑到阶段那一行上（未决定要不要单独画）",
    "buildings[].sellRefundRate": "页面上没有「卖出」这个动作（终端有卖模式，Web 没有）",
    // ---- upgrades[]
    "upgrades[].hiddenUntilUnlocked": "同 buildings[]：服务端算成 isVisible / isUnlocked",
    "upgrades[].category": "前端用服务端算好的 upgradeIds 挂建筑升级，不解析 category",
    "upgrades[].tier": "终端用它排序分区；Web 的升级面板不分区（未决定）",
    // ---- 其他记录
    "achievements[].category": "成就列表面板不分组（未决定）",
    "prestige.currentLevel": "前端只有 canAscend / chipsOnAscend 与 era.progressText 有落点（未决定）",
    "prestige.nextLevel": "同上（未决定）",
    "prestige.cookiesForNextLevel": "转生门槛的原始数：前端画的是 era.progressText 那句现成的话（未决定）",
    "era.entryText": "本层进入叙事：Web 没有画它的位置（终端在横幅里画）（未决定）",
    "era.stageName": "阶段那一行画的是序号 + 门槛文本（§21）；阶段名本身没画（未决定）",
    "era.modifierSummary": "本层常驻规则摘要：Web 一处都没画（未决定）",
    "era.all[].completed": "纪元总览（整张表）前端一张都没画（未决定）",
    "era.all[].current": "同上：总览没画，所以「当前是哪一层」也没画（未决定）",
    "codex.storylines[].entries[].storylineId": "条目就嵌在自己的 storyline 里，这个回指字段前端用不上",
    "codex.storylines[].entries[].storylineName": "同上：外层 storyline 对象上已经有 name",
    "codex.storylines[].entries[].order": "数组顺序就是服务端给的顺序，前端不再排序",
    "codex.storylines[].entries[].channel": "投放通道（Log / Popup / Codex / EraText）：前端画的是图鉴那一栏（未决定）",
  };

  /** 空数组：夹具里一格都没有，所以它们**内部**有什么字段这条守卫看不见（见第三条用例）。 */
  const EMPTY_ARRAY_BLIND_SPOTS = ["buffs[]", "goldenCookies[]", "pendingLore[]", "pendingChoices[]"];

  const fields = [];
  const emptyArrays = [];
  (function walk(value, path) {
    if (value === null || value === undefined) return;
    if (Array.isArray(value)) {
      // 数组只看第 0 个元素：服务端按同一个 record 序列化每一行，字段集合是同一套（同 §14）。
      if (value.length > 0) walk(value[0], `${path}[]`);
      else emptyArrays.push(`${path}[]`);
      return;
    }
    if (typeof value === "object") {
      for (const key of Object.keys(value)) walk(value[key], path ? `${path}.${key}` : key);
      return;
    }
    fields.push(path);
  })(wire, "");

  const nameOf = (path) => path.split(".").pop().replace(/\[\]$/, "");
  const escape = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const referenced = (path) => new RegExp(`\\.${escape(nameOf(path))}\\b`).test(frontCode);

  const undecided = fields.filter((path) => !referenced(path) && !(path in NOT_DRAWN));

  check(`夹具里 ${fields.length} 个字段：要么 app.js 在读它，要么在「明确不画」表里`, () => {
    if (undecided.length > 0) {
      throw new Error(`${undecided.length} 个字段没有决定过：\n        · ${undecided.join("\n        · ")}\n      `
        + `要么在 app.js 里画它，要么写进这一节的 NOT_DRAWN 表并给一句理由（「还没决定」也是理由）。`);
    }
  });

  check("「明确不画」表里每一条都还是真的：字段还在夹具里，而且前端确实没引用它", () => {
    const stale = Object.keys(NOT_DRAWN).filter((path) => !fields.includes(path) || referenced(path));
    if (stale.length > 0) {
      const why = stale.map((path) => {
        const gone = !fields.includes(path);
        return `${path}（${gone ? "这个字段已经不在夹具里了" : "前端已经在读它了"}）`;
      });
      throw new Error(`过期的借口 ${stale.length} 条：\n        · ${why.join("\n        · ")}\n      `
        + `字段没了就删掉这一行；开始画了就把它从表里移走（决定记录要跟着事实走）。`);
    }
  });

  check("夹具里的空数组还是那 4 个（它们是这条守卫的盲区：里面有什么字段看不出来）", () => {
    const now = [...emptyArrays].sort().join(", ");
    const pinned = [...EMPTY_ARRAY_BLIND_SPOTS].sort().join(", ");
    if (now !== pinned) {
      throw new Error(`空数组变了：钉住的是 <${pinned}>，夹具里是 <${now}>。`
        + `新填上的那个数组里的字段这条守卫看不见——请逐个决定（画 / 写进 NOT_DRAWN），再把这里更新成新的事实。`);
    }
  });

  check("`.字段名` 这条读法规则是完备的：app.js 里没有声明解构、也没有方括号取值", () => {
    const other = [/(?:^|[^.\w])(?:const|let|var)\s*\{/, /\w\[\s*["']/]
      .map((pattern) => pattern.exec(frontCode))
      .filter(Boolean);
    if (other.length > 0) {
      throw new Error(`发现了第三种读法 <${other[0][0].trim()}>——上面那条规则会看不见它：`
        + `要么补进规则，要么把那个字段写进 NOT_DRAWN。`);
    }
  });
}

// 24. 存档的导出 / 导入窗口（W13）。
//
// 引擎侧那 17 条用例（SaveTransferTests）与宿主侧那 8 条（SaveTransferHostTests）都绿着
// 也说明不了"玩家有一个入口"——缺口 D 的形状恰恰就是"引擎全绿、界面上一个入口都没有"
// （`ExportShareCode()` 公开了好几个版本、零调用者）。这一节守的是**界面这一层**：
//
//   · 入口真的换了：点「存档」开窗口，**不再**直接发 save 命令（那是旧行为，而且它
//     与导出/导入毫无关系）；手动存档那条命令挪进窗口里、功能没删；
//   · 导出文本真的被画进只读文本框、能复制、能下载——**下载给的是同一份字节**；
//   · 剪贴板写不进去时**不许假装成功**（把文本选中 + 明说失败），这是"复制"最容易撒的谎；
//   · 导入的失败原因**原样**留在窗口里（不翻译、不只弹 2.6 秒的提示条），
//     而三种坏输入（人话 / 别的包 / 校验和对不上）各自显示引擎写的那一句；
//   · 关闭的三条路（Esc / × / 点遮罩）与"开着时空格不许偷偷点猫"。
section("24. 存档的导出 / 导入窗口（入口 / 复制 / 下载 / 导入的失败与成功）");
{
  const ENVELOPE = [
    "{",
    '  "Format": "neko-save",',
    '  "FormatVersion": 1,',
    '  "PackId": "neko",',
    '  "SaveVersion": 1,',
    '  "FrameworkVersion": "1.10.2",',
    '  "Checksum": "sha256:deadbeef",',
    '  "Save": "{\\"Version\\":1,\\"Cookies\\":1234.5}"',
    "}",
  ].join("\n");

  const EXPORT_MESSAGE = "已生成导出文本（内容包「neko」、218 个字符）——整份复制走，就能在别处导入；也可以下载成 .json 文件。";

  // 三种坏输入各自对应的**引擎原话**（形如 SaveManager.Import 的 Message：说清哪一类、怎么办）。
  const IMPORT_FAILURES = {
    人话: "这不是本游戏导出的存档文本：它连合法 JSON 都不是（'这' is an invalid start of a value）。请整份复制「导出」出来的内容，不要只复制其中一段。",
    别的包: "这份存档是内容包「cafe」的，当前会话是「neko」——两个包的建筑 / 升级 id 不一样，灌进来只会得到一份这个包答不上的存档，所以拒绝导入。磁盘上原来的存档与备份都没有被动过。",
    被改过: "校验和对不上（信封里写的是 sha256:deadbeef…，这份内容的实际值是 sha256:1234abcd…）——文本被改动或被截断过。请整份重新复制一次。",
  };
  const IMPORT_OK = "已导入：内容包「neko」｜存档格式 1｜框架 1.10.2｜当前会话与 neko.json 都已换成这一份｜已核对包标识（neko）｜存档里的 id 这个包全都认识。";

  /** 一个按命令回答的宿主桩：export 给文本，import 按内容给上面那几句。 */
  const reply = (body) => {
    if (!body) return null;
    if (body.type === "export") return { ok: true, message: EXPORT_MESSAGE, text: ENVELOPE, seq: 1 };
    if (body.type === "import") {
      const failure = IMPORT_FAILURES[body.text];
      return failure
        ? { ok: false, message: failure, seq: 2 }
        : { ok: true, message: IMPORT_OK, seq: 3 };
    }
    return { ok: true, message: "已存档。", seq: 4 };
  };

  const save = await loadApp(copyAs("app-save.mjs", appSource), { reply });
  await save.push({ kind: "full", seq: 1, snapshot: snapshot() });

  check("首帧：存档窗口是关着的（它不该在玩家点之前冒出来）", () => hidden(save, "save-sheet"));

  // ---- 入口 ----
  fire(el(save, "save"), "click");
  await flush();

  check("点「存档」开的是窗口，**不再**直接发 save 命令（那是旧行为）", () => {
    eq(shown(save, "save-sheet"), true, "窗口应当打开");
    eq(commandsOf(save, "save").length, 0, "打开窗口不该顺手存一次盘");
  });
  check("打开窗口就向宿主取一份导出文本（导出是一次读，不是一次写）", () => {
    eq(commandsOf(save, "export").length, 1, "export 次数");
  });
  check("宿主的导出文本原样落进文本框", () => {
    eq(el(save, "export-text").value, ENVELOPE, "文本框内容");
    eq(el(save, "export-note").textContent, EXPORT_MESSAGE, "那一行状态用的是宿主自己的话");
  });
  check("焦点交给导出框（这张窗口里有七个控件，留在背后按钮上 Tab 会走到遮罩底下）", () => {
    eq(el(save, "export-text").focused, 1, "导出框被 focus 的次数");
  });

  // ---- 复制 ----
  fire(el(save, "export-copy"), "click");
  await flush();
  check("「复制」把**同一份文本**写进剪贴板，并明说复制了几个字符", () => {
    eq(save.clipboardWrites.length, 1, "写剪贴板的次数");
    eq(save.clipboardWrites[0], ENVELOPE, "写进去的内容");
    if (!el(save, "export-note").textContent.includes("已复制")) {
      throw new Error(`复制成功之后那一行要说"已复制"：<${el(save, "export-note").textContent}>`);
    }
  });

  // ---- 下载 ----
  // 先触发、再把 Blob 的内容读出来：check() 是同步的，所以异步那步不能藏在它里面
  // （藏进去的话失败会变成一个没人接的 Promise，断言悄悄变绿）。
  fire(el(save, "export-download"), "click");
  await flush();
  {
    const blob = save.blobs[0];
    const bytes = blob ? await blob.text() : null;
    const anchor = save.doc.created.filter((node) => node.tagName === "A").pop();

    check("「下载 .json」给的是同一份字节（校验和算在文本里，所以两条路等价）", () => {
      eq(save.blobs.length, 1, "创建的 Blob 数");
      eq(bytes, ENVELOPE, "下载内容");
    });
    check("下载链接带着 download 文件名、真的被点过、点完从文档里摘掉", () => {
      if (!anchor) throw new Error("没有创建 <a> 元素");
      if (!String(anchor.download ?? "").endsWith(".json")) {
        throw new Error(`download 文件名不是 .json：<${anchor.download}>`);
      }
      eq(anchor.clicked, 1, "点了几次");
      eq(anchor.parent, null, "点完要摘掉（否则文档里会留一串看不见的链接）");
    });
  }

  // ---- 导入：三种坏输入，各显示引擎那句话 ----
  for (const [what, message] of Object.entries(IMPORT_FAILURES)) {
    el(save, "import-text").value = what;
    fire(el(save, "import-go"), "click");
    await flush();

    check(`导入「${what}」：宿主那句话原样留在窗口里`, () => {
      eq(el(save, "import-result").textContent, message, "窗口里的字");
      eq(shown(save, "save-sheet"), true, "失败不该把窗口关掉（那段话是玩家唯一能读到的解释）");
    });
    check(`导入「${what}」：不走提示条（那句话 2.6 秒就会被吞掉，必须留在屏幕上）`, () => {
      eq(el(save, "toast").textContent, "", "提示条应当一个字都没写");
    });
  }

  check("三种坏输入都只发了 import（前端不许自己判一遍、更不许顺手做别的）", () => {
    eq(commandsOf(save, "import").length, Object.keys(IMPORT_FAILURES).length, "import 次数");
    eq(commandsOf(save, "save").length, 0, "整段下来没有一次 save");
  });

  // ---- 导入：成功 ----
  const exportsBefore = commandsOf(save, "export").length;
  el(save, "import-text").value = "好文本";
  fire(el(save, "import-go"), "click");
  await flush();
  check("导入成功：同样是宿主那句话，而且窗口留着让人读完（导入了哪个包、有没有不认识的 id）", () => {
    eq(el(save, "import-result").textContent, IMPORT_OK, "窗口里的字");
    eq(shown(save, "save-sheet"), true, "成功之后窗口不关");
  });
  check("导入成功之后重新导出一份（文本框里那一份已经是导入前的旧状态，留着会被下载走）", () => {
    eq(commandsOf(save, "export").length, exportsBefore + 1, "export 次数");
    eq(el(save, "export-text").value, ENVELOPE, "文本框仍然是宿主给的那一份");
  });

  // ---- 手动存档那条命令没被删掉，只是换了入口 ----
  fire(el(save, "save-now"), "click");
  await flush();
  check("窗口里的「立刻存一次盘」才发 save（功能没删、只是挪了地方）", () => {
    eq(commandsOf(save, "save").length, 1, "save 次数");
    eq(el(save, "save-note").textContent, "已存档。", "结果写在它自己那一行");
  });

  // ---- 键盘：空格不许偷偷点猫；Esc 关窗口 ----
  {
    const before = commandsOf(save, "click").length;
    for (const handler of save.doc._listeners.get("keydown") ?? []) {
      handler({ code: "Space", target: { tagName: "BODY" }, preventDefault() {} });
    }
    check("窗口开着时按空格**不点猫**（窗口盖在游戏上面，背后点一下谁也没要求过）", () => {
      eq(commandsOf(save, "click").length, before, "click 次数");
    });
  }

  {
    for (const handler of save.doc._listeners.get("keydown") ?? []) {
      handler({ code: "Escape", target: { tagName: "BODY" }, preventDefault() {} });
    }
    check("Esc 关窗口，并把焦点还给入口按钮（丢在隐藏元素里读屏会迷路）", () => {
      eq(hidden(save, "save-sheet"), true, "窗口应当关闭");
      eq(el(save, "save").focused, 1, "「存档」按钮被 focus 的次数");
    });
  }

  check("再开一次、点右上角 × 也能关", () => {
    fire(el(save, "save"), "click");
    fire(el(save, "save-close"), "click");
    return hidden(save, "save-sheet");
  });
  check("点遮罩空白处也关（点在卡片里不关——与另外两张 sheet 同一条规矩）", () => {
    fire(el(save, "save"), "click");
    fire(el(save, "save-sheet"), "click", { target: el(save, "export-text") });
    if (hidden(save, "save-sheet")) throw new Error("点在卡片里不该关掉窗口");
    fire(el(save, "save-sheet"), "click", { target: el(save, "save-sheet") });
    return hidden(save, "save-sheet");
  });

  // ---- 剪贴板拿不到时的退路：**不许假装成功** ----
  const noClipboard = await loadApp(copyAs("app-save-noclip.mjs", appSource), { reply, clipboard: false });
  await noClipboard.push({ kind: "full", seq: 1, snapshot: snapshot() });
  fire(el(noClipboard, "save"), "click");
  await flush();
  fire(el(noClipboard, "export-copy"), "click");
  await flush();

  check("没有剪贴板 API 时：明说复制失败、把文本选中、指路 Ctrl+C 与「下载 .json」", () => {
    eq(noClipboard.clipboardWrites.length, 0, "一次都没有写进去");
    const note = el(noClipboard, "export-note").textContent;
    if (note.includes("已复制")) throw new Error(`复制没成功却写着"已复制"：<${note}>`);
    if (!note.includes("Ctrl+C")) throw new Error(`没有给出退路（Ctrl+C）：<${note}>`);
    if (!note.includes("下载")) throw new Error(`没有指向「下载 .json」：<${note}>`);
    eq(el(noClipboard, "export-text").selected, true, "文本要被选中，玩家才能直接按 Ctrl+C");
  });

  // ---- 标记与样式的形状守卫 ----
  check("存档窗口沿用同一套 sheet 形态（.sheet-layer / .sheet / .sheet-close / .sheet-ok）", () => {
    const sheet = /<div id="save-sheet" class="sheet-layer hidden">[\s\S]*?class="sheet sheet-wide"[\s\S]*?class="sheet-close"[\s\S]*?class="sheet-ok"/.exec(html);
    if (!sheet) throw new Error("save-sheet 的标记没有沿用 .sheet-layer / .sheet / .sheet-close / .sheet-ok");
  });
  check("导出文本框是**只读**的（它是要被整份复制走的东西，不是在这里编辑的东西）", () => {
    if (!/<textarea id="export-text"[^>]*\breadonly\b/.test(html)) throw new Error("#export-text 上没有 readonly");
  });
  check("导出与导入都用多行文本框（一份真实存档 1.5~6 KB，单行 input 没法用）", () => {
    for (const id of ["export-text", "import-text"]) {
      if (!new RegExp(`<textarea id="${id}"`).test(html)) throw new Error(`#${id} 不是 <textarea>`);
    }
  });
  check("提示条与存档窗口的结果行是页面上**恰好两个**实时区域（一个不多、一个不少）", () => {
    // 先剥注释：注释里必须能提 `role="status"` 这类名字（那正是"为什么有两条"要解释的东西），
    // 而注释在浏览器里不产生元素——这一条第一次跑就是这么红的（数出了三条，多出来那条是注释）。
    const body = html.replace(/<!--[\s\S]*?-->/g, "");
    const live = [...body.matchAll(/<[^>]*\brole="status"[^>]*>/g)].map((m) => m[0]);
    const ids = live.map((tag) => /\bid="([^"]+)"/.exec(tag)?.[1] ?? "(无 id)").sort();
    eq(ids.join(", "), "import-result, toast", "实时区域");
  });
}

// 25. 缩放适配（浏览器 / 系统缩放、窄视口、以及"图形"本身）。
//
// 为什么这一节只有这样几条：这个桩件**没有布局引擎**（见文件头部），量不出溢出、折行
// 与真实尺寸。所以它守的是"把缩放写进规则里"这件事本身——视口单位、相对尺寸、断点、
// 以及不许出现的 px 字号；"看起来对不对 / 到底会不会溢出"只有人眼能判，登记在
// OPEN_WORK 的 **H3** 里（这一节不假装能替它）。
// 两条纪律与 §17 一致：数字只从 app.css / app.js / index.html 的**文本**里读（读不到就红），
// 算出来的预算把每个中间量都印进消息里 —— 红的时候差额直接看得到，不用再猜。
section("25. 缩放适配：视口单位、相对尺寸、断点（结构性可查的部分）");
{
  const REM = 16;
  const blockOf = (selector) =>
    new RegExp(selector.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + " \\{([\\s\\S]*?)\\}").exec(cssRules)?.[1] ?? "";
  /** 取一整节媒体查询（从 `@media <query> {` 到与它配对的 `}`）；找不到返回空串。 */
  const mediaOf = (query) => {
    const marker = `@media ${query} {`;
    const start = cssRules.indexOf(marker);
    if (start < 0) return "";
    let depth = 0;
    for (let i = start + marker.length - 1; i < cssRules.length; i++) {
      if (cssRules[i] === "{") depth++;
      else if (cssRules[i] === "}" && --depth === 0) return cssRules.slice(start, i + 1);
    }
    return "";
  };

  // ---- A. 缩放本身不许被挡住；文字一律相对量 ----

  check("viewport meta 不挡缩放（没有 maximum-scale / user-scalable=no），并且有 width=device-width", () => {
    const meta = /<meta name="viewport" content="([^"]*)"/.exec(html)?.[1] ?? "";
    if (!meta) throw new Error("index.html 里找不到 viewport meta");
    if (/maximum-scale|user-scalable\s*=\s*no|minimum-scale/i.test(meta)) {
      throw new Error(`viewport 里出现了挡缩放的指令：<${meta}>`);
    }
    if (!/width=device-width/.test(meta)) throw new Error(`没有 width=device-width：<${meta}>`);
  });

  check("app.css 里一处 px 字号都没有（文字全部跟着浏览器缩放 / 用户字号设置走）", () => {
    const declarations = [...cssRules.matchAll(/font-size\s*:\s*([^;{}]+)/g)].map((m) => m[1].trim());
    const px = declarations.filter((value) => /[0-9.]+px/.test(value));
    if (px.length > 0) throw new Error(`这些 font-size 用了 px（px 不跟用户的默认字号走）：${px.join(" / ")}`);
    // 空集也满足上面那条，所以再钉一条"这条守卫确实在扫东西"。
    if (declarations.length < 30) throw new Error(`只数到 ${declarations.length} 处 font-size（期望 ≥30）——这条守卫变得没有内容了`);
  });

  check("px 只出现在有先例的那批属性上（边框 / 圆角 / 轮廓 / 阴影 / 触屏下限 / 背景光的兜底 / 动画位移）", () => {
    // 这些 px 是**故意的**：1px 的边与 2px 的轮廓是"发丝线"，跟着 rem 放大会变粗；
    // 44px 的触屏下限是**物理尺寸**（CSS px ≈ 1/96 英寸），它不该跟字号一起长大；
    // 12px 的圆角与 8/30px 的阴影是形状与装饰；background 里的 1200px/600px 是 min() 的
    // 兜底上界（这里只允许它出现在 min() 里，见下面单独一条）；transform 是 float 动画。
    const allowed = new Set([
      "--radius", "background", "border", "border-top", "border-bottom", "border-bottom-width",
      "border-left", "border-radius", "outline", "outline-offset", "box-shadow",
      "min-width", "min-height", "transform",
    ]);
    const found = new Set();
    for (const m of cssRules.matchAll(/([a-z-]+)\s*:\s*([^;{}]*)/g)) {
      if (/[0-9.]+px/.test(m[2])) found.add(m[1]);
    }
    const unexpected = [...found].filter((prop) => !allowed.has(prop));
    if (unexpected.length > 0) throw new Error(`这些属性上没有先例地用了 px：${unexpected.join("、")}`);
  });

  check("min-width / min-height 上的 px 只有触屏下限那几个数（2 / 40 / 44）", () => {
    const values = [...cssRules.matchAll(/min-(?:width|height)\s*:\s*([0-9.]+)px/g)].map((m) => Number(m[1]));
    const odd = [...new Set(values)].filter((v) => ![2, 40, 44].includes(v));
    if (odd.length > 0) throw new Error(`不在允许的触屏下限里：${odd.join("、")}px`);
    if (values.length < 8) throw new Error(`只数到 ${values.length} 处 min-* 的 px（期望 ≥8）——守卫变得没有内容了`);
  });

  // ---- B. 视口单位：vh 是**大**视口，dvh 才是"现在看得见的那块" ----

  check(".sheet 的高度上限用视口单位，且 dvh 写在 vh 之后（旧浏览器退回 vh，而不是没有上限）", () => {
    const block = blockOf(".sheet");
    const vh = block.search(/max-height:\s*calc\(100vh - 2rem\)/);
    const dvh = block.search(/max-height:\s*calc\(100dvh - 2rem\)/);
    if (vh < 0) throw new Error(".sheet 没有 vh 那条回退");
    if (dvh < 0) throw new Error(".sheet 没有 dvh：手机上 100vh = 地址栏藏起来时的高度，sheet 会比看得见的区域高一截");
    if (dvh < vh) throw new Error("dvh 写在了 vh 前面：不认 dvh 的浏览器会拿到后面那条，回退就白写了");
  });

  check("body 的 min-height 同样两条都写、顺序同上", () => {
    const block = blockOf("body");
    const vh = block.search(/min-height:\s*100vh/);
    const dvh = block.search(/min-height:\s*100dvh/);
    if (vh < 0 || dvh < 0) throw new Error(`vh ${vh < 0 ? "缺" : "在"} / dvh ${dvh < 0 ? "缺" : "在"}`);
    if (dvh < vh) throw new Error("dvh 写在了 vh 前面");
  });

  check(".list 的高度上限跟着视口收（min(26rem, 60dvh)）", () => {
    const block = blockOf(".list");
    if (!/max-height:\s*min\(26rem,\s*60vh\)/.test(block)) throw new Error(".list 没有 vh 那条回退");
    if (!/max-height:\s*min\(26rem,\s*60dvh\)/.test(block)) {
      throw new Error(".list 没有 dvh：26rem = 416px，在 384px 高的视口里比整个窗口还高");
    }
  });

  check("背景光不再是尺寸写死的图形（1200×600 在窄屏上有一大半在屏幕外）", () => {
    const block = blockOf("body");
    if (!/radial-gradient\(min\(1200px,\s*120vw\)\s+min\(600px,\s*90vh\)/.test(block)) {
      throw new Error(`body 的背景椭圆不是相对的：<${/background:[^;]+/.exec(block)?.[0] ?? "(没有 background)"}>`);
    }
  });

  // ---- C. 断点：声称支持的宽度 / 高度都要有 ----

  check("两个宽度断点都在（62rem 单栏、34rem 手机）", () => {
    for (const query of ["(max-width: 62rem)", "(max-width: 34rem)"]) {
      if (!cssRules.includes(`@media ${query}`)) throw new Error(`没有 ${query} 这一节`);
    }
  });

  check("多一条高度断点（又宽又矮：2560×1080 在 200% 缩放下是 1280×540）", () => {
    const short = mediaOf("(max-height: 34rem)");
    if (!short) throw new Error("没有高度断点：hero 会比可视高度还高，吸顶时下半截永远够不着");
    if (!/\.hero \{[^}]*position:\s*static/.test(short)) throw new Error("高度断点里没有把 hero 的吸顶关掉");
  });

  check("单栏（≤62rem）时 hero 不吸顶 —— \"单栏\"的分界是 62rem，不是 34rem", () => {
    // 注意：这里不能用 blockOf(".hero") —— 62rem 那节本身就在基规则**之前**，
    // 它里面那条 `.hero { position: static }` 才是 cssRules 里的第一处匹配。
    if (!/\.hero \{[^}]*position:\s*sticky/.test(cssRules)) {
      throw new Error("没有任何一处 .hero 声明 sticky——这条守卫在证明一条不存在的规则");
    }
    const single = mediaOf("(max-width: 62rem)");
    if (!single) throw new Error("找不到 62rem 那一节");
    if (!/\.hero \{[^}]*position:\s*static/.test(single)) {
      throw new Error("62rem 那节没有关掉吸顶：544~992px（平板 / 分屏 / 200% 缩放的笔记本）单栏时列表会被压在 hero 下面");
    }
    const narrow = mediaOf("(max-width: 34rem)");
    if (/\.hero \{[^}]*position:\s*static/.test(narrow)) {
      throw new Error("34rem 那节又写了一遍同一条：同一条理由留在两处，只会让人以为另一处没有");
    }
  });

  check("sheet 的标题行吸顶，且负上外边距与 .sheet 的上内边距等量（视觉位置一个像素不变）", () => {
    const head = blockOf(".sheet-head");
    if (!/position:\s*sticky/.test(head) || !/top:\s*0/.test(head)) {
      throw new Error(".sheet-head 不吸顶：.sheet 自己滚动，内容一滚唯一的 × 就滚出去了");
    }
    if (!/background:\s*#2a2135/.test(head)) throw new Error("吸顶的标题行没有底色：滚动的内容会从它上面穿过去");
    const sheetPad = Number(/padding:\s*([\d.]+)rem/.exec(blockOf(".sheet"))?.[1]);
    const pull = Number(/margin-top:\s*-([\d.]+)rem/.exec(head)?.[1]);
    const pad = Number(/padding-top:\s*([\d.]+)rem/.exec(head)?.[1]);
    if (!Number.isFinite(sheetPad) || sheetPad !== pull || pull !== pad) {
      throw new Error(`对不上：.sheet 上内边距 ${sheetPad}rem / 标题行 -${pull}rem ＋ ${pad}rem（两者必须相等，否则标题会跳一下）`);
    }
  });

  // ---- D. "图形"本身：相对尺寸、位置由 CSS 钳制、没有栅格化 ----

  check("大猫的宽有上限，且那个上限就是两栏布局里 hero 列的上限（26rem）", () => {
    const cat = blockOf(".big-cat");
    if (!/width:\s*min\(100%,\s*26rem\)/.test(cat)) {
      throw new Error(`大猫仍然是"窗口多宽它就多宽"：<${/width:[^;]+/.exec(cat)?.[0] ?? "(没有 width)"}>`);
    }
    const heroColumn = /grid-template-columns:\s*minmax\([^)]*\)\s+1fr/.exec(blockOf("main"))?.[0] ?? "";
    if (!heroColumn.includes("26rem")) throw new Error(`两栏那一列的上限不再是 26rem：<${heroColumn}>`);
  });

  check("金猫：宽 / 高 / 字号都从 --golden-cat-size 来，字号是它的 .56 倍", () => {
    if (!/--golden-cat-size:\s*clamp\([^)]*\)/.test(blockOf(":root"))) {
      throw new Error(":root 里没有 --golden-cat-size，或者它不是相对的（clamp）");
    }
    const cat = blockOf(".golden-cat");
    for (const prop of ["width", "height"]) {
      if (!new RegExp(`${prop}:\\s*var\\(--golden-cat-size\\)`).test(cat)) {
        throw new Error(`${prop} 不是从 --golden-cat-size 来的（尺寸与位置必须是同一个数）`);
      }
    }
    if (!/font-size:\s*calc\(var\(--golden-cat-size\)\s*\*\s*\.56\)/.test(cat)) {
      throw new Error("字号不是尺寸的 .56 倍：改动前是 1.9rem / 3.4rem = .559，别让它另立一个数");
    }
  });

  check("金猫的位置由 CSS 钳制（min(92%, calc(100% - var(--golden-cat-size)))，不是光秃秃的百分比）", () => {
    if (!/button\.style\.left = place\(cat\.x, 92\)/.test(appSource) || !/button\.style\.top = place\(cat\.y, 88\)/.test(appSource)) {
      throw new Error("那两行位置不再走 place() 了");
    }
    const clamp = /min\([^\n]*calc\(100% - var\(--golden-cat-size\)\)[^\n]*/.exec(appSource)?.[0] ?? "";
    if (!clamp) {
      throw new Error("找不到 calc(100% - var(--golden-cat-size))：光秃秃的 92% 在 320px 下算到 294+54 = 349px，右侧 29px 连同点击目标一起出界");
    }
  });

  check("提示条与药丸的宽度跟着视口收（fixed + 居中：溢出时两头都出界）", () => {
    if (!/max-width:\s*min\(32rem,\s*calc\(100vw - 2rem\)\)/.test(blockOf(".toast"))) {
      throw new Error(".toast 没有跟着视口走的宽度上限");
    }
    if (!/overflow-wrap:\s*anywhere/.test(blockOf(".toast"))) throw new Error(".toast 里折不断的 token 会直接顶出去");
    if (!/max-width:\s*calc\(100vw - 2rem\)/.test(blockOf(".choices-pill"))) {
      throw new Error(".choices-pill 没有跟着视口走的宽度上限");
    }
  });

  check("面板头允许折行（300~360px 的窗口里标题与控件同一行放不下）", () => {
    if (!/flex-wrap:\s*wrap/.test(blockOf(".panel-head"))) throw new Error(".panel-head 不许折行");
  });

  check("页面里没有栅格化图形，也没有人需要 devicePixelRatio", () => {
    const rasters = [
      ["<img>", html, /<img\b/i],
      ["<canvas>", html, /<canvas\b/i],
      ["background-image: url(...)", cssRules, /background-image:\s*url\(/],
    ];
    for (const [what, text, pattern] of rasters) {
      if (pattern.test(text)) {
        throw new Error(`出现了 ${what}：栅格图形要自己按 devicePixelRatio 处理尺寸（150%~400% 缩放会模糊）——处理它，或者把这一条与那段处理一起更新`);
      }
    }
    if (/devicePixelRatio/.test(appSource)) {
      throw new Error("app.js 用了 devicePixelRatio：今天页面里没有栅格图形，不需要手工换算");
    }
  });

  check("两处 emoji 都是相对字号（大猫的 🐱 是 clamp，金猫的 🐱 是那个变量）", () => {
    const icon = blockOf(".big-cat span:first-child");
    if (!/font-size:\s*clamp\([^)]*\)/.test(icon)) throw new Error(`大猫的图标字号不是 clamp：<${icon.trim()}>`);
    if (!/font-size:\s*calc\(var\(--golden-cat-size\)/.test(blockOf(".golden-cat"))) {
      throw new Error("金猫的 🐱 不是从 --golden-cat-size 算出来的");
    }
  });

  // ---- F. 这次真踩到的一条：漏一个 `*/` 会静默吞掉后面的规则 ----
  // （2026-10-04 就在这一轮里发生过：`.panels` 那条注释漏了 `*/`，于是 `.panel` 与
  //   `.panel-head` 两条规则整段进了注释——浏览器里面板当场变形，而**原始字节里
  //   花括号是配平的**，所以"括号平衡"这种检查只有在剥掉注释之后才看得见。）

  check("app.css 的注释都是闭合的（/* 与 */ 一样多）", () => {
    const opens = (css.match(/\/\*/g) ?? []).length;
    const closes = (css.match(/\*\//g) ?? []).length;
    if (opens !== closes) throw new Error(`/* 出现 ${opens} 次、*/ 出现 ${closes} 次——有一个注释没闭合，它后面的规则会被吞掉`);
  });

  check("剥掉注释之后 app.css 的花括号仍然配平", () => {
    const opens = (cssRules.match(/\{/g) ?? []).length;
    const closes = (cssRules.match(/\}/g) ?? []).length;
    if (opens !== closes) throw new Error(`剥掉注释后 ${opens} 个 { / ${closes} 个 }——有选择器或规则被注释吞了`);
  });

  // ---- E. 预算：把 CSS 里那些数拿来算一遍（每个中间量都印在消息里） ----

  check("320px（本仓库支持的最窄一档）下建筑行仍放得下：两个 44px 按钮之后卡片 ≥100px", () => {
    const bodyPad = Math.max(16, Math.min(0.04 * 320, 48)); // clamp(1rem, 4vw, 3rem)，320px 时取到 1rem
    const mainPad = 0.6 * REM;    // ≤34rem 那节的 main 内边距
    const columnGap = 0.35 * REM; // .building 的列间距（两处）
    const available = 320 - 2 * bodyPad - 2 * mainPad;
    const left = available - (2 * 44 + 2 * columnGap);
    if (left < 100) throw new Error(`卡片只剩 ${left.toFixed(1)}px（可用 ${available.toFixed(1)} − 按钮 ${(2 * 44 + 2 * columnGap).toFixed(1)}）`);
  });

  check("320px 下立场轴的进度条 ≥96px（窄屏那条把 9rem 的固定首列变成相对量）", () => {
    const stance = /\.stance \{([^}]*)\}/.exec(mediaOf("(max-width: 34rem)"))?.[1] ?? "";
    if (!/minmax\(4\.5rem,\s*38%\)/.test(stance)) {
      throw new Error(`窄屏那节没有把立场轴的首列改成相对量：<${stance.trim() || "(没有 .stance 规则)"}>`);
    }
    const outer = Math.min(26 * REM, 320 - 2 * REM); // .sheet 宽 + .sheet-layer 的 1rem 内边距
    const inner = outer - 2 * 1.3 * REM;             // .sheet 的左右内边距
    const first = Math.max(4.5 * REM, Math.min(0.38 * inner, 9 * REM));
    const bar = inner - first - 2.5 * REM - 2 * 0.5 * REM;
    if (bar < 96) throw new Error(`进度条只剩 ${bar.toFixed(1)}px（sheet 内宽 ${inner.toFixed(1)} − 首列 ${first.toFixed(1)} − 权重列 ${(2.5 * REM).toFixed(1)} − 间隙 ${REM}）`);
  });

  check("320px 下批量档位这一行确实放不下（算出差额），所以 .batch 必须折行", () => {
    const glyph = 0.78 * REM;
    const chars = 2 + 3 + 4 + 2;                     // "×1" / "×10" / "×100" / "买满"（1em/字符的上界）
    const batch = chars * glyph + 4 * 0.5 * REM * 2 + 4 * 2 + 3 * 0.3 * REM;
    const panelInner = 320 - 2 * REM - 2 * 0.6 * REM - 2 * 1.1 * REM; // body / main / panel 的内边距
    const room = panelInner - 2 * 0.95 * REM - 1 * REM;               // 减去「建筑」两个字与 .panel-head 的 1rem 间隙
    if (batch <= room) {
      throw new Error(`算出来放得下（${batch.toFixed(1)} ≤ ${room.toFixed(1)}px）——这条预算的前提变了，折行那两条要重新判断`);
    }
    if (!/flex-wrap:\s*wrap/.test(blockOf(".batch"))) {
      throw new Error(`差 ${(batch - room).toFixed(1)}px 却不让折行（档位要 ${batch.toFixed(1)}px，只有 ${room.toFixed(1)}px）`);
    }
  });

  check("单栏窗口里的\"巨猫\"有上限：900px 宽的窗口原本会得到一只 596px 高的大猫", () => {
    const bodyPad = Math.max(16, Math.min(0.04 * 900, 48)); // 4vw = 36px
    const full = 900 - 2 * bodyPad;
    const capped = Math.min(full, 26 * REM);
    if (!/width:\s*min\(100%,\s*26rem\)/.test(blockOf(".big-cat"))) {
      throw new Error(`大猫会拿到 ${full}px 宽 = ${(full * 0.72).toFixed(1)}px 高，比多数窗口的可视高度还高`);
    }
    if (capped * 0.72 >= full * 0.72) throw new Error("上限没起作用");
  });
}

// 26. 两栏的对齐：左栏那张大框（.hero）与右栏那张卡片（.panel）的**上沿与下沿**。
//
// 这一节守的是**结构**：页签行与面板各自是 main 的网格项、两张卡片从同一行开始、
// 两块卡片的**高度**由它们自己 opt in 拉伸（下沿因此也落在同一条线上）、
// 单栏时那套落位必须被重置（否则 `grid-column: 2` 会造出隐式的第 2 列），
// 而且单栏里"拉伸"必须无从发生（一列一项 ⇒ 行高就是内容高，不会造出大空盒）。
// ⚠️ 它证明不了"看上去对齐了 / 看上去一样高了" —— 那个桩件没有布局引擎，
// 最后一眼只能是人看的（OPEN_WORK H3）。
section("26. 两栏对齐：左栏大框与右栏卡片的上沿 ＋ 下沿");
{
  const blockOf = (selector) =>
    new RegExp(selector.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + " \\{([\\s\\S]*?)\\}").exec(cssRules)?.[1] ?? "";
  /** 取一整节媒体查询（同 §25；这里再写一遍是因为 §17 / §25 的辅助函数都是块级的）。 */
  const mediaOf = (query) => {
    const marker = `@media ${query} {`;
    const start = cssRules.indexOf(marker);
    if (start < 0) return "";
    let depth = 0;
    for (let i = start + marker.length - 1; i < cssRules.length; i++) {
      if (cssRules[i] === "{") depth++;
      else if (cssRules[i] === "}" && --depth === 0) return cssRules.slice(start, i + 1);
    }
    return "";
  };
  const REM = 16;

  check(".panels 这层壳被去掉了（display: contents），页签行与面板因此各自是 main 的网格项", () => {
    if (!/\.panels \{ display: contents; \}/.test(cssRules)) {
      throw new Error(`.panels 还不是 display: contents：<${blockOf(".panels").trim() || "(没有 .panels 规则)"}>`);
    }
    if (/\.panels \{ display: flex/.test(cssRules)) throw new Error("旧的 flex 版本还在（同一件事有两个出处）");
  });

  check("页签钉在第 1 行 / 两张卡片都从第 2 行开始（上沿因此严格落在同一条线上）", () => {
    if (!/\.tabs \{ grid-area: 1 \/ 2; \}/.test(cssRules)) throw new Error("页签没有被钉在「第 1 行第 2 列」");
    if (!/\.hero \{ grid-area: 2 \/ 1; \}/.test(cssRules)) throw new Error("左栏大框没有被钉在「第 2 行第 1 列」——它就又和页签齐平了");
    if (!/\.panel \{ grid-column: 2; \}/.test(cssRules)) {
      throw new Error("面板只该钉列（行交给自动排布：第 1 行被页签占了，第一张可见面板才会落在第 2 行）");
    }
  });

  // 下沿。上沿对齐（上一条）之后人报的是「左右两边还是没对齐」＝两块卡片**不等高**。
  // 第 2 行里高的那个撑着行高、矮的那个自己缩着（`main` 的 `align-items: start`），
  // 所以差多少完全由内容决定：两栏实测 hero ≈ 493px，而面板 = 1rem ＋ 36.3px 表头
  // ＋ 列表上限 min(26rem, 60dvh) ＋ 1.1rem —— 1080p 上差 7px（几乎看不出），
  // 600px 高的窗口里 60dvh = 360px，面板矮 60px 上下，一眼就是两块不等高。
  // 修法是让这两块**自己** opt in（不是把 main 改成 stretch：那还得再给 .tabs
  // 补一条 align-self: start 把它排除出去）。
  check("等高：两块卡片各自声明 align-self: stretch（容器仍是 start，`.tabs` 不参与）", () => {
    // 全表扫一遍"谁对 .hero / .panel 声明过 align-self"：既要**有** stretch，
    // 也要**没有**任何一条把它撤回去（同特异度时后写的那条赢）。
    const declarations = [...cssRules.matchAll(/([^{}]+)\{([^}]*)\}/g)]
      .filter(([, selector, body]) => /(^|,)\s*\.(hero|panel)\s*(,|$)/.test(selector.trim()) && /align-self\s*:/.test(body))
      .map(([, selector, body]) => `${selector.trim()} → ${/align-self\s*:\s*[^;]+/.exec(body)[0].trim()}`);
    if (declarations.length === 0) {
      throw new Error("没有任何一条给 .hero / .panel 的 align-self：两块里矮的那个不会长到行高，下沿对不上");
    }
    const bad = declarations.filter((d) => !/align-self\s*:\s*stretch/.test(d));
    if (bad.length > 0) {
      throw new Error(`有规则把等高撤销了（后写的赢）：${bad.join(" ；")}`);
    }
    if (!/align-items:\s*start/.test(blockOf("main"))) {
      throw new Error("main 的默认对齐不再是 start：等于把「哪一块该长」从这两块挪到容器上，还得再给 .tabs 补一条 align-self 把它排除出去");
    }
    if (/align-self/.test(blockOf(".tabs"))) {
      throw new Error(`.tabs 也开始管自己的对齐了（页签被拉伸只会让按钮变高，没人要）：<${blockOf(".tabs").trim()}>`);
    }
  });

  // 等高只许改**外框**：多出来的高度必须落在卡片内部的**底部**，内容一个像素都不许被抻长
  // （否则大猫会被拉长、按钮会变高、渐变会被摊开）。两条前提，缺一不可：
  //   · `.hero` 是 flex 纵列，`justify-content` 没写过（默认 flex-start）；
  //   · 里面**没有**任何子项声明过 `flex-grow` / `flex: <正数>`（全表唯一一条是存档窗口的
  //     `.sheet-row .ghost`，不在 hero 里）。
  check("拉伸多出来的空间落在底部：hero 是 flex 纵列、且没有任何子项会长（没有 flex-grow）", () => {
    const hero = blockOf(".hero");
    if (!/display:\s*flex/.test(hero) || !/flex-direction:\s*column/.test(hero)) {
      throw new Error(`.hero 不再是 flex 纵列，空白落哪儿要重新判断：<${hero.trim()}>`);
    }
    if (/justify-content/.test(hero)) {
      throw new Error(`.hero 新写了 justify-content：内容会被推开（我们要的是"内容留在顶部、空在底部"）:<${hero.trim()}>`);
    }
    const growers = [...cssRules.matchAll(/([^{}]+)\{([^}]*)\}/g)]
      .filter(([, , body]) => /(?:^|;)\s*flex\s*:\s*[1-9]/.test(body) || /flex-grow\s*:\s*(?!0\b)[\d.]+/.test(body))
      .map(([, selector]) => selector.trim());
    const inHero = growers.filter((s) => /\.(hero|big-cat|buffs|era|counter|rates|meta)\b/.test(s));
    if (inHero.length > 0) {
      throw new Error(`hero 里有子项会把空白吃掉（它会长高，而不是让空留在底部）：${inHero.join(" ；")}`);
    }
    if (growers.length === 0) throw new Error("全表一条 flex-grow 都没有了——这条守卫的判据（拿它当反例清单）已经失效");
    if (!/aspect-ratio:\s*1\s*\/\s*\.72/.test(blockOf(".big-cat"))) {
      throw new Error("大猫不再由 aspect-ratio ＋ 宽度定高：容器变高时它会被抻长");
    }
  });

  // 单栏（≤62rem）：上面那条 stretch **故意不重置**，因为它在单栏里不可能生效 ——
  // 一列一项、每行只有一个网格项，行高就是那一项自己的内容高。这两条前提（单列 ＋
  // 三项都回到 auto 落位）必须同时成立，否则窄屏上会凭空多出一个大空盒。
  check("单栏里拉伸无从发生：`1fr` 一列 ＋ 三项都回到 auto 落位（每行只有一项）", () => {
    const single = mediaOf("(max-width: 62rem)");
    if (!single) throw new Error("找不到 62rem 那一节");
    if (!/main \{ grid-template-columns: 1fr; \}/.test(single)) {
      throw new Error("单栏那节没有把 main 变成一列——两块卡片可能被塞进同一行，拉伸就会造出一个大空盒");
    }
    if (!/\.tabs, \.hero, \.panel \{ grid-area: auto; \}/.test(single)) {
      throw new Error("单栏那节没有把三项的落位重置成 auto（显式行号可能让两项同一行）");
    }
  });

  check("单栏（≤62rem）时那套落位全部重置为 auto（不重置会造出隐式的第 2 列，整页缩成一半宽）", () => {
    const single = mediaOf("(max-width: 62rem)");
    if (!single) throw new Error("找不到 62rem 那一节");
    if (!/\.tabs, \.hero, \.panel \{ grid-area: auto; \}/.test(single)) {
      throw new Error("62rem 那节没有重置 .tabs / .hero / .panel 的落位");
    }
  });

  check("顺序：`.hero` 的 `position: static` 写在基规则 `position: sticky` **之后**（媒体查询不加特异度，只看源顺序）", () => {
    const base = /\.hero \{[^}]*position:\s*sticky/.exec(cssRules);
    if (!base) throw new Error("找不到基规则里的 sticky");
    const statics = [...cssRules.matchAll(/\.hero \{[^}]*position:\s*static/g)];
    if (statics.length < 2) throw new Error(`只找到 ${statics.length} 处 position: static（期望两处：单栏 ＋ 又宽又矮）`);
    const early = statics.filter((m) => m.index < base.index);
    if (early.length > 0) {
      const lines = early.map((m) => cssRules.slice(0, m.index).split("\n").length).join(" / ");
      throw new Error(`${early.length} 处写在基规则之前（第 ${lines} 行）：会被后面那条 sticky 盖掉，等于没写`);
    }
  });

  check("顺序：单栏那套落位重置写在两栏落位**之后**（否则 `grid-column: 2` 赢，造出隐式的第 2 列）", () => {
    const place = /\.tabs \{ grid-area: 1 \/ 2; \}/.exec(cssRules);
    const reset = /\.tabs, \.hero, \.panel \{ grid-area: auto; \}/.exec(cssRules);
    if (!place || !reset) throw new Error("两栏落位或单栏重置不见了");
    if (reset.index < place.index) {
      const at = (i) => cssRules.slice(0, i).split("\n").length;
      throw new Error(`重置在第 ${at(reset.index)} 行、落位在第 ${at(place.index)} 行——同特异度时后者赢，单栏会变成两列`);
    }
  });

  check("量一下这次改动消掉的那条错位：页签行高 ＋ 行间距（数字全部从 CSS 常量算出来）", () => {
    const tab = blockOf(".tabs button");
    const fontSize = Number(/font-size:\s*([\d.]+)rem/.exec(tab)?.[1]);
    const padding = Number(/padding:\s*([\d.]+)rem/.exec(tab)?.[1]);
    const lineHeight = Number(/line-height:\s*([\d.]+)/.exec(blockOf("body"))?.[1]);
    const rowGap = Number(/row-gap:\s*([\d.]+)rem/.exec(blockOf("main"))?.[1]);
    for (const [what, value] of [["页签字号", fontSize], ["页签内边距", padding], ["body 行高", lineHeight], ["行间距", rowGap]]) {
      if (!Number.isFinite(value)) throw new Error(`读不出${what}——这条预算的前提变了`);
    }
    const tabHeight = fontSize * REM * lineHeight + 2 * padding * REM + 2; // ＋上下各 1px 边框
    const offset = tabHeight + rowGap * REM;
    if (offset < 40) throw new Error(`算出来的错位只有 ${offset.toFixed(1)}px——这条守卫的前提变了`);
    if (!/\.hero \{ grid-area: 2 \/ 1; \}/.test(cssRules)) {
      throw new Error(`左栏大框的上沿比右栏那张卡片高 ${offset.toFixed(1)}px（页签 ${tabHeight.toFixed(1)} ＋ 行间距 ${(rowGap * REM).toFixed(1)}）`);
    }
  });
}

// 27. 批量档位下的「总价」（×10 / ×100 / 买满）。
//
// 人 2026-10-04 报的：「x10 和 x100 在切换之后要显示总价」。
//
// 动手前先查清了这一件事，它决定了这一节怎么写：**卡片上那个数一直是整批的总价，不是单价。**
// `buildings[].batchPrice` 由服务端按价格曲线算好（`GameViewFactory` 里的 `Pricing.BulkPrice`
// 就是等比数列求和），而前端从 d238825（第一个前端提交）起画的一直是它；
// **单价** `buildings[].unitPrice` 一次都没画过（§23 的 NOT_DRAWN 里写着这一条）。
// 所以真问题不是"画错了数"，而是"**没说清这个数是总价**"——线上那一行此前是
// `🐟 7.58千 ×10`，读起来正好像"单价 7.58千，×10 个"（而真值恰好相反：单价 373、整批 7.58千）。
// 第一版修法是把「总价」两个字加在数字**后面**、`×N` 收进括号（`🐟 7.58千 总价（×10）`）。
// 第二版（2026-10-04，人看过后拍的）把限定语整个挪到数字**前面**：`×10 总价 🐟 7.58千`。
// 两版都写明了「总价」，差别在**读的顺序**：第一版眼睛先落在数上、再补一句括号里的注解，
// 扫一眼的速度下与改之前的 `🐟 7.58千 ×10` 差得不够远；第二版数字前面已经站好 `×N 总价`，
// `数字 + ×N` 那个"单价 ×N 个"的读法在句法上就不成立了（括号也一并去掉：
// `×N` 在这里是**限定语**，不是注解）。图标仍然贴着数字，与 ×1 那一行一致。
//
// 这一节钉四件事：批量档位写出「总价」二字**且在数字前面**；**切档位时画出来的那个数真的跟着换**
// （同一座建筑、同一行 DOM）；×1 一个字不加（一个的总价就是单价，多写是噪音）；小总价不被
// `number()` 抹成 0。两版的**准确字符串**都由 `eq()` 钉死，所以"数字在限定语前"这种回退
// 一定红（见 `OPEN_WORK` §0.21.5 的第四次注入）。
section("27. 批量档位下的「总价」（×10 / ×100 / 买满）");
{
  const batch = await loadApp(copyAs("app-batch.mjs", appSource));
  const priceOf = () => el(batch, "buildings").children[0].children
    .find((child) => child.classList.contains("card")).children
    .find((child) => child.classList.contains("price")).textContent;

  // 末世包那一局的真值（从真宿主的 `/api/snapshot` 上读下来的）：🧱 废墟持有 23、
  // 单价 373.37、×10 整批 7579、×100 整批 117,431,205。
  const ruins = {
    id: "b1", isVisible: true, isUnlocked: true, canAfford: true, icon: "🧱", name: "废墟",
    owned: 23, unitPrice: 373.37, batchAmount: 1, batchPrice: 373.37,
    cpsEach: 0.225, cpsContribution: 5.2, cpsShare: 0.01,
    unlockHint: "", unlockProgress: 1, description: "捡来的。", upgradeIds: [],
  };
  const frame = (seq, building) => ({ kind: "full", seq, snapshot: snapshot({ buildings: [building] }) });

  await batch.push(frame(1, ruins));

  check("×1：价格行与改动前逐字相同（一个的总价就是单价，不加「总价」两个字）", () => {
    eq(priceOf(), "🐟 373", "×1 那一行");
  });

  await batch.push(frame(2, { ...ruins, batchAmount: 10, batchPrice: 7579 }));

  check("×10：写出「总价」而且在数字**前面**，那个数就是整批的总价（7.58千），不是单价 373", () => {
    eq(priceOf(), "×10 总价 🐟 7.58千", "×10 那一行");
    // 下面三句不是 `eq()` 的重复：它们**不依赖期望串**。有人把这一行的期望串改成旧次序
    // （"改测试而不是改代码"）时 `eq()` 会放行，这几句仍然会红。
    if (priceOf().indexOf("总价") > priceOf().indexOf("7.58千")) {
      throw new Error(`限定语落在数字后面了（那是第一版、也是"数字 + ×N"那个误读的形态）：<${priceOf()}>`);
    }
    if (priceOf().includes("（")) throw new Error(`括号回来了——\`×N\` 在这里是限定语、不是注解：<${priceOf()}>`);
    if (priceOf().includes("373")) throw new Error(`画的是单价而不是整批总价：<${priceOf()}>`);
  });

  await batch.push(frame(3, { ...ruins, batchAmount: 100, batchPrice: 117431205 }));

  check("×100：切档位之后，同一行画出来的数跟着换成整批的总价（117百万）", () => {
    eq(priceOf(), "×100 总价 🐟 117百万", "×100 那一行");
  });

  await batch.push(frame(4, { ...ruins, batchAmount: 37, batchPrice: 3.4e6 }));

  check("买满：N 是服务端按钱包算出来的可变数量，所以 `×N` 记号还得留着（页面上别处没有它）", () => {
    eq(priceOf(), "×37 总价 🐟 3.40百万", "买满那一行");
  });

  await batch.push(frame(5, { ...ruins, batchAmount: 10, batchPrice: 0.4 }));

  check("小总价不被抹成 0：0.4 画成「0.4」（`number()` 会把它画成「×10 总价 🐟 0」那句假话）", () => {
    eq(priceOf(), "×10 总价 🐟 0.4", "小总价那一行");
  });

  await batch.push(frame(6, {
    ...ruins, isUnlocked: false, batchAmount: 10, batchPrice: 7579,
    unlockHint: "累计赚到 300", unlockProgress: 0.4,
  }));

  check("未解锁那一行照旧画解锁条件（不许在锁着的行上摆一个总价）", () => {
    eq(priceOf(), "累计赚到 300（40%）", "未解锁那一行");
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
