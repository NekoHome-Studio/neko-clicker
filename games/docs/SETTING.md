# 《九命猫娘：增量宇宙》设定集

> **状态：骨架。** 本文是**目录与约定**，各节内容**尚未填写**（原稿每一节都是 `...`）。
> 它与 [ROADMAP.md](ROADMAP.md)（1107 行的**大纲**）并列：**ROADMAP 讲"怎么做、什么时候做"，
> 本文讲"这个世界是什么"**。两者一起读；任一处改动请顺手看另一处是否需要跟着改。
>
> 相关文档：[NINE_LIVES_DESIGN.md](NINE_LIVES_DESIGN.md)（十包设计矩阵，含「需要 Choice」列）、
> [PACK_01_CAT_CAFE.md](PACK_01_CAT_CAFE.md)（#1 咖啡馆）、
> [STAGE_5_RESKINS.md](STAGE_5_RESKINS.md)（换皮批产）、
> [../engine/docs/CONTENT_AUTHORING.md](../engine/docs/CONTENT_AUTHORING.md)（怎么写内容包）。
>
> **填写约定**：每节末尾的「落地位置」指的是这一节的内容**在代码里对应什么**——
> 设定集与代码不许各说各话（这个项目的规矩是"文档里的话必须是真的"）。
> 填内容时请把「落地位置」补全，或标注"尚无对应实现"。

---

## 一句话核心

（待填）

---

## 一、起源

（待填）

## 二、九命 = 九纪元 = 九服务器

（待填。约定：**用表格文本**，见文末「层间总表」的格式）

## 三、猫娘分类

（待填）

## 四、资源体系

（待填）

- 落地位置：`engine/content/<Pack>/` 里的资源定义；第二资源与转生货币见 `Buildings.cs` / `Upgrades.cs` 的货币语义。

## 五、建筑与设施树

（待填。这是**"建筑的设定集"**所在节——每座建筑的叙事定位、它在世界里的意义。）

- 落地位置：`engine/content/<Pack>/Buildings.cs`（`BuildingDefinition[]`，顺序即 UI 展示顺序）。
- **文本已外置**：每座建筑的 `name` / `description` / `icon` 现在住在
  `engine/content/<Pack>/text.json` 的根节 `buildings` 里
  （`"<建筑 id>": { "name": …, "description": …, "icon": … }`），C# 侧用
  `Prose.Text("buildings", "<id>", "name")` 取值。**11 个包、104 座**全部迁完，
  迁移前后运行时 dump 逐字节相同（name+description 13,567 字节、SHA-256 `BB40F11C…`；
  图标另有一份 7,895 字节的 dump，见 §10）。
  形状、方法与守卫见 `../engine/docs/TEXT_AS_DATA_PLAN.md` §9 与 §10。
- **纪元文案也已外置**：`EraDefinition` 的 `Name` / `Theme` / `Icon` / `EntryText` / `ExitText` /
  `CompletionHint` 住在同一个 `text.json` 的根节 `eras`（**9 个包、49 层**；示例包「猫咖物语」
  与「猫娘咖啡馆」没有纪元）。**这一节的"每座建筑的叙事定位"应当照 `buildings` 与 `eras`
  两个节来写，不要照 C#。**
- **仍留在代码里的**：全部逻辑/数值字段（`BasePrice` / `BaseCps` / `PriceGrowth` /
  `Unlock` / `Category` / `Tags`）。⚠️ 曾经有一处"`Icon` 留在代码里、而剧情线的 icon 已外置"
  的不一致，**2026-10-03 已按"搬出去"结案**（`OPEN_WORK.md` 的 L 条）——现在图标也在文件里。

## 六、阵营

（待填）

- 落地位置：多包共用的 `Stances`（立场轴）机制；哪些包有立场轴见
  [NINE_LIVES_DESIGN.md](NINE_LIVES_DESIGN.md) 的设计矩阵。

## 七、猫娘社会结构

（待填）

## 八、代表猫娘

（待填。约定：每个内容包至少一位，写明她与「六、阵营」的关系）

## 九、事件系统

（待填）

- 落地位置：`GameSnapshot.Notifications`（Web 有「日志」页签）；金猫浮层与增益见 `Buffs.cs`。

## 十、结局条件表

（待填。用表格：**包 → 结局 id → 触发条件 → 是否走 `EndingSystem`**）

- 落地位置：`engine/content/<Pack>/Endings.cs` + `engine/core/Simulation/EndingSystem.cs`。
- **已知的一处例外**：#1 咖啡馆的结局**存在但不走 `EndingSystem`**——它是隐藏成就 + 弹窗叙事
  （`PACK_01_CAT_CAFE.md` §11、`Cafe/Achievements.cs` 的 `happiness_50000`、
  `Cafe/text.json` 的 `door_20`）。填这张表时**不要把它抹平**。

## 十一、隐藏真相层级

（待填。约定：一层一揭，写明"读者在第几命之后才该知道"）

## 十二、玩法与叙事结合

（待填。这一节最该与 ROADMAP 的决策记录（R1–R11）对齐）

## 十三、黑话与术语

（待填。术语表。**落地位置**：凡进入界面的词，都必须同时出现在
`engine/content/<Pack>/text.json` 里，否则界面与设定会漂移。）

## 十四、每层具体剧情与建筑

每一命固定四段：**核心主题 / 剧情三幕 / 建筑链 / 关键事件 / 转生文本**。

### 第 1 命：纸箱纪元

（待填：核心主题 / 剧情三幕 / 建筑链 / 关键事件 / 转生文本）

### 第 2 ~ 第 9 命

（待填。包与层数的对应关系见 [NINE_LIVES_DESIGN.md](NINE_LIVES_DESIGN.md)；
已实测的"到结局游戏小时数"见 [../engine/docs/TUNING_ANALYSIS.md](../engine/docs/TUNING_ANALYSIS.md)）

### 第 10 隐藏层：公司纪元

（待填）

---

## 层间总表

（待填。约定：**表格式用文本**——层号 / 纪元名 / 内容包 id / 层数 / 该层核心资源 / 该层揭开的真相 / 结局 id）

## 终极主线

（待填。约定：写明"玩家最终在追什么"，以及它与「十一、隐藏真相层级」如何收束）
