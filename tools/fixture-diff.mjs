// tools/fixture-diff.mjs —— 重抓夹具之后的"逐项对一遍"工具（OPEN_WORK §0.42/§0.44 那条工作流）。
//
// 背景：FixtureFreshnessTests 守着"仓库夹具 == 引擎此刻的线上形状"。它一红，
// 出路是重抓：`pwsh -File tools/capture-fixture.ps1 -Output .tmp/candidate.json`，
// 然后**在提升之前**把候选与仓库里那份逐项对一遍——§0.42 那次对比抓出了三类差异
// （状态漂移 / 内容演进 / 守卫修复现形），靠的是一段用完即弃的 node -e 脚本。
// 这个文件就是那段脚本的正式版：比对口径与 web-smoke §14 相同，别每次重写。
//
// 用法：
//   node tools/fixture-diff.mjs .tmp/candidate.json              # 对仓库夹具（缺省基准）
//   node tools/fixture-diff.mjs .tmp/candidate.json other.json   # 显式给基准
//
// 它打印什么：
//   · 路径集差异（数组只取 [0]，与 §14 同口径）——新字段 / 消失的字段，点名到路径；
//   · 内容数组条数差异——shareholder_paws 那一类（内容变了、夹具没跟上）；
//   · 全数组按 id 逐行的**值级**差异（buildings / upgrades / achievements / codex 全部行，
//     不止 [0]）——形状守卫看不见的那一半，提升前由人来读。
//
// 退出码：0（比对工具不做裁判——差异是不是预期，读的人说了算；形状守卫的裁判
// 是 FixtureFreshnessTests，对的是引擎而不是旧夹具）。
//
// 读数指南（§0.42 的经验）：状态字段（点击数、计时器、通知、unlockProgress、progressText
// 的分子）不同是**预期**；成就描述、era 主题、effectSummary、数组条数不同说明
// 旧夹具落在内容或守卫改动之后——正是要重抓的原因。
import { readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const [candidatePath, basePath = join(root, "tools", "fixtures", "web-snapshot.json")] = process.argv.slice(2);
if (!candidatePath) {
  console.error("用法：node tools/fixture-diff.mjs <candidate.json> [base.json]");
  process.exit(2);
}

const load = (p) => JSON.parse(readFileSync(p, "utf8"));
const candidate = load(candidatePath);
const base = load(basePath);
console.log(`候选：${candidatePath}\n基准：${basePath}\n`);

// ---- 1. 路径集（数组按 [0]；空数组记为盲区，与 §14 同口径）
function collectPaths(value, path, into) {
  if (value === null || value === undefined) { into.add(`${path || "<顶层>"} = null`); return; }
  if (Array.isArray(value)) {
    if (value.length > 0) collectPaths(value[0], `${path}[]`, into);
    else into.add(`${path}[] = <空数组盲区>`);
    return;
  }
  if (typeof value === "object") {
    for (const key of Object.keys(value)) collectPaths(value[key], path ? `${path}.${key}` : key, into);
    return;
  }
  into.add(path);
}
const basePaths = new Set(), candidatePaths = new Set();
collectPaths(base, "", basePaths);
collectPaths(candidate, "", candidatePaths);
const onlyInBase = [...basePaths].filter((p) => !candidatePaths.has(p));
const onlyInCandidate = [...candidatePaths].filter((p) => !basePaths.has(p));

// ---- 2. 值级深比：数组按 id 配对（没有 id 的按索引），逐字段比
function deepDiff(a, b, path, into) {
  if (a === null || a === undefined || b === null || b === undefined) {
    if (JSON.stringify(a) !== JSON.stringify(b)) into.push([path, a, b]);
    return;
  }
  if (Array.isArray(a) || Array.isArray(b)) {
    if (!Array.isArray(a) || !Array.isArray(b)) { into.push([path, a, b]); return; }
    const byId = a.length > 0 && a[0] && typeof a[0] === "object" && "id" in a[0];
    if (byId) {
      const mapB = new Map(b.map((x) => [x.id, x]));
      const mapA = new Map(a.map((x) => [x.id, x]));
      for (const row of a) {
        const twin = mapB.get(row.id);
        if (!twin) into.push([`${path}[${row.id}]`, "<基准里有>", "<候选里没有>"]);
        else deepDiff(row, twin, `${path}[${row.id}]`, into);
      }
      for (const row of b) if (!mapA.has(row.id)) into.push([`${path}[${row.id}]`, "<基准里没有>", "<候选里有>"]);
    } else {
      const n = Math.max(a.length, b.length);
      for (let i = 0; i < n; i++) deepDiff(a[i], b[i], `${path}[${i}]`, into);
    }
    return;
  }
  if (typeof a === "object" && typeof b === "object") {
    for (const key of new Set([...Object.keys(a), ...Object.keys(b)])) {
      deepDiff(a[key], b[key], path ? `${path}.${key}` : key, into);
    }
    return;
  }
  if (JSON.stringify(a) !== JSON.stringify(b)) into.push([path, a, b]);
}
const valueDiffs = [];
deepDiff(base, candidate, "", valueDiffs);

// ---- 3. 汇总
const shapeDiffs = onlyInBase.length + onlyInCandidate.length;
console.log("== 路径集（形状层）");
console.log(`  基准 ${basePaths.size} 条 / 候选 ${candidatePaths.size} 条；只在基准 ${onlyInBase.length} 处，只在候选 ${onlyInCandidate.length} 处`);
for (const p of onlyInBase) console.log(`  · ${p}：基准有、候选没有（候选侧字段被删了？）`);
for (const p of onlyInCandidate) console.log(`  · ${p}：候选有、基准没有（新字段没进旧夹具——正是要重抓的原因）`);

console.log("\n== 数组条数（内容层）");
function arrayLens(value, path, into) {
  if (value === null || value === undefined || typeof value !== "object") return;
  if (Array.isArray(value)) {
    into.set(path, value.length);
    if (value.length > 0) arrayLens(value[0], `${path}[]`, into);
    return;
  }
  for (const key of Object.keys(value)) arrayLens(value[key], path ? `${path}.${key}` : key, into);
}
const baseLens = new Map(), candidateLens = new Map();
arrayLens(base, "", baseLens);
arrayLens(candidate, "", candidateLens);
let lengthDiffs = 0;
for (const key of new Set([...baseLens.keys(), ...candidateLens.keys()])) {
  const a = baseLens.get(key), b = candidateLens.get(key);
  if (a !== b && !(a === undefined && b === undefined)) {
    // 数组内部（[] 后缀）的条数在 [0] 展开时天然一致，跳过空对空
    if (a === undefined || b === undefined) continue;
    console.log(`  · ${key}: ${a} -> ${b}`);
    lengthDiffs++;
  }
}
if (lengthDiffs === 0) console.log("  （无差异）");

console.log("\n== 值级差异（全部行，按 id 配对）");
console.log(`  共 ${valueDiffs.length} 处`);
for (const [path, a, b] of valueDiffs) {
  const show = (v) => JSON.stringify(v) ?? String(v);
  console.log(`  · ${path}: ${show(a)} -> ${show(b)}`);
}

console.log("\n== 读法");
console.log(`  形状差异 ${shapeDiffs} 处；条数差异 ${lengthDiffs} 处；值级差异 ${valueDiffs.length} 处。`);
console.log("  状态字段（点击数 / 计时器 / 通知 / progress 分子）不同 = 预期漂移；");
console.log("  描述文本、era 主题、effectSummary、条数不同 = 旧夹具落在内容或守卫改动之后。");
console.log("  提升前逐项过目；提升后跑 node tools/web-smoke.mjs（§14/§23 比形状）。");
