# Boss 战斗重做总规划（Boss Combat Redo Master Plan）

> **文档性质：** 全 Boss 战斗/机制重做的**唯一权威规划**（Master Redo Plan）
> **版本：** 1.0 · 2026-06-27
> **来源：** 四路并行只读 AI 质量审计（NPCs 早中期+月后四僵尸 / 天庭龙系 / 天庭其余 / 地府线）的合并结论
> **审计范围：** 40+ Boss 战斗实现，覆盖 `NPCs/Boss/`、`Celestias/Boss/`、`Underworlds/Boss/`
> **配套文档：** `PROGRESSION_DESIGN_SPEC.md`（进度/掉落/数值） · `PLAYABILITY_AUDIT_REPORT.md`（现状缺口） · `PLACEHOLDER_CONTENT_REGISTRY.md`（占位物品）

---

## 1. 概述 Overview

### 1.1 文档目的

本规划聚焦一件事：**Boss 战斗设计的质量**。`PROGRESSION_DESIGN_SPEC.md` 已解决"打谁→掉什么→解锁什么"的**进度脊柱**，但它**不**评估每个 Boss 战斗本身是否好玩。本文填补这一空白，把四路审计结论合并为一份**可直接派发实现**的重做清单。

**与既有文档的分工：**

| 文档 | 回答的问题 |
|------|-----------|
| `PROGRESSION_DESIGN_SPEC.md` | 顺序、tier、掉落、召唤、门控、数值 |
| `PLAYABILITY_AUDIT_REPORT.md` | 内容缺口、空掉落、崩溃性 bug |
| `PLACEHOLDER_CONTENT_REGISTRY.md` | 武器/材料占位与理想机制 |
| **`BOSS_REDO_PLAN.md`（本文）** | **每个 Boss 的战斗手感、攻击机制、阶段结构** |

### 1.2 核心问题陈述（The Core Anti-Pattern）

> **用户核心抱怨：** 大量 Boss **没有真正的战斗/机制设计**，依赖同一个公式化反模式——
> **「Boss 血量过低 → 进入狂暴 → 只是把弹幕喷得更快」**

这个"低血量=加速喷射"的套路在**至少 21 个 Boss/阶段**中重复出现（见 §2.1）。它的本质问题：

- **阶段转换只改数字，不改规则**——玩家的应对方式从头到尾一样，只是手速要求变高。
- **没有学习曲线**——Boss 不教玩家任何新东西，狂暴只是"同一首歌放快放大声"。
- **主题与机制脱节**——元素/神话主题只是贴图和命名，从不驱动独特玩法。
- **死状态与抄袭**——多个 Boss 是同一模板的换皮，部分高潮阶段（如 DarkRitual、Enraged）**写了却从不触发**。

### 1.3 审计评级口径

| 评级 | 含义 | 处理方式 |
|------|------|----------|
| **GOOD** | 有真实战斗设计，可作参考模板 | 仅保留/微调，**禁止**改坏 |
| **MEDIOCRE** | 骨架不错但有反模式或半成品 | 局部重做：补阶段规则、换占位、修死状态 |
| **POOR** | 换皮/抄袭/反模式主导/死状态 | 结构性重写，从参考模板移植 |

**审计统计：** GOOD 9 · MEDIOCRE 17 · POOR 11（含两个同名"祖龙残魂"实体，见 §1.4）。

### 1.4 两个"祖龙残魂"实体的区分（重要）

模组中存在**两个不同的"祖龙残魂"**，本文始终分开处理：

| 中文名 | 实体 | 路径 | 定位 | 评级 |
|--------|------|------|------|------|
| 祖龙残魂（地表） | Archosaur 蠕虫 | `NPCs/Boss/Archosaur/ArchosaurBoss.cs` | HM 可选 Raid，90k HP | **POOR** |
| 祖龙残魂（天庭） | Ancestral Dragon Soul | `Celestias/Boss/AncestralDragonSouls/` | 月后天庭脊柱 T42，800万 HP | **MEDIOCRE** |

---

## 2. 全局反模式 Cross-Cutting Anti-Patterns

以下 5 类问题横跨多个区域，是本次重做的**主攻方向**。每条重做方案都必须先消除这些反模式。

### 2.1 反模式 #1：「低血量 = 加速喷弹幕」（最核心）

确认存在于以下 Boss/阶段（21 处）：

| 区域 | Boss/阶段 | 具体表现 |
|------|-----------|----------|
| NPCs | 黑熊精 半血"狂暴" | 纯装饰性加速 |
| NPCs | 牛头马面 半血 | 加速喷射 |
| NPCs | 九尾 P2 | RNG 轮盘 + 加速 |
| NPCs | 将臣 | TP 距离触发"狂暴" |
| NPCs | 苍龙真身 P3 | 加速喷射 + 原版闪电球 |
| 天庭龙 | 敖钦 P2 | +投射物 + 加速 |
| 天庭龙 | 敖闰 P2 | +4 投射物 & 提速 |
| 天庭龙 | 天御金龙 | HP 缩放密度档位 |
| 天庭龙 | 祖龙残魂(天庭) | **Enraged 是 stub，永不进入**——反模式的极端形态 |
| 天庭其余 | 毗沙门 P3 FourKingsWrath | 喷射 |
| 天庭其余 | 观察者 P3 Punishment | 喷射 |
| 天庭其余 | 青龙/白虎/朱雀 P3 | 共享 Fury hub 喷射 |
| 天庭其余 | 大椿 P2 FuryPatrol | 同 P1 加速 |
| 天庭其余 | 树精 P2 | = P1 更快 |
| 地府 | 怨灵 FinalGrudge@25% | 喷射 |
| 地府 | 觉醒冥龙 DesperateFury@25% | **用户抱怨原话的逐字复刻** |
| 地府 | 尸骸 FinalRage@20% | 喷射 |
| 地府 | 幽冥龙 | 常驻 23 火焰/90 tick，无 HP 阶段 |

**统一裁定：** 所有"低血量=加速喷弹幕"必须替换为 **§3 设计原则中的脚本化幕/目标阶段**。

### 2.2 反模式 #2：复制粘贴模板（换皮 Boss）

| 模板源 | 派生（换皮）Boss | 修复方向 |
|--------|------------------|----------|
| 旱魃 Hanba | 后卿 Hoqing、将臣 Jiangcen | 各给独立机制与状态机 |
| 敖广 AoGuang | 敖钦 Aokin | 差异化，补火系 Phase 3 |
| 毗沙门 Vaisravana | ↔ 观察者 Overseer（互为换皮） | 拆分共享模板，重建各自 P3 |
| 青龙/白虎/朱雀 | 三者共享四圣兽模板 | 各保留 1 个签名机制 |
| 怨灵 Spectre | ↔ 觉醒冥龙 Awakening Nether | 先修 Spectre 以断开抄袭链 |
| 幽冥龙 NetherDragon | ↔ 觉醒冥龙 Awakening Nether | 同上 |

### 2.3 反模式 #3：原版占位投射物

在**正式版本中禁止**直接复用原版 Boss 投射物（CultistBoss 法球等）：

| Boss | 占位投射物 | 替换为 |
|------|-----------|--------|
| 九尾 Kyuubi | 原版法球 | 狐火主题自定义弹幕 |
| 苍龙真身 Azure Dragon | 原版闪电球 | 自定义雷/风弹 |
| 朱雀 Suzaku | 原版法球 | 朱雀焰羽弹幕 |
| 冥狐 Nether Kitsune | 原版灵魂 | 幽冥狐火自定义弹 |

### 2.4 反模式 #4：死/未接线设计（写了却不触发 / 留空）

| Boss | 死状态 | 处理 |
|------|--------|------|
| 敖闰 Aoyuan | `Phase1`/`Phase2` 空 stub 文件 | 实装或删除，重建 FSM |
| 黑熊精 BlackBear | `Attack_3`（`BlackBear_Proj3`）未使用 | 接入攻击循环或移除 |
| 尸骸 Corpses | `DarkRitual` 阶段已编码但**从不触发** | 接线为真正的高潮幕 |
| 祖龙残魂(天庭) | `Enraged` 状态是 stub，永不进入 | 完成 50% 分裂后的真实激怒幕 |

### 2.5 反模式 #5：主题-机制断层

主题只停在贴图/命名层，从未驱动独特机制：

| 主题 | 现状 | 应驱动的机制 |
|------|------|--------------|
| 地府 DoT / 冥律 / 怨念 | 完全缺席 | 持续伤害、冥律标记、怨念叠层（参考 SPEC §6.7 冥律标记） |
| 天庭 / 全视 / 财宝 | 仅装饰 | 观察者"全视"应有视线/预判机制；毗沙门"财宝"应有金币联动 |
| 苍龙真身 雷/风 | 用原版球 | 自定义雷暴/风域控场 |

---

## 3. 设计原则 Redo Design Principles

每个重做**必须**遵守以下硬性规则。这是验收基准。

1. **以脚本化幕替代血量狂暴（Acts, not Enrage）。**
   阶段转换由**剧情节点 / 目标完成 / 场地变化**驱动，而非单纯 HP 阈值触发"加速"。若保留 HP 阈值，过阈值必须**改变战斗规则**，不能只改数字。

2. **每个攻击都要有预告（Telegraph）。**
   攻击前要有可读的前摇（动画、地面标线、蓄力光效、子状态）。参考赢勾的 telegraph 子状态、百目的注视线预告。

3. **阶段转换改"规则"而非"数值"。**
   新阶段应引入新机制：新的场地约束、新的攻击族、新的弱点窗口、资源/姿态切换——让玩家**改变打法**，不只是手速。

4. **元素/主题必须驱动一个独特机制。**
   火/冰/雷/水/DoT/全视/财宝各自要有**唯一玩法**，不可互换换皮。每个 Boss 至少 1 个**签名机制**。

5. **正式版禁止原版占位投射物。**
   §2.3 列表中的原版弹幕全部替换为主题自定义弹幕。

6. **接线或移除死状态。**
   §2.4 的 stub/未触发状态：要么真正接入战斗循环，要么删除。**禁止**保留"写了不用"的代码。

7. **换皮 Boss 必须差异化。**
   共享模板的派生 Boss（§2.2）至少要在**状态机结构、签名机制、场地控制**三者中差异化两项。

8. **资源/节奏机制优先于纯弹幕。**
   鼓励借鉴 GOOD 模板的资源/充能（敖顺 StormCharge）、姿态/反击（神威 Counter Stance）、身体物理（玄武 Verlet）、双 Boss 协同（黑白无常）等深度机制。

9. **保留 GOOD 模板，禁止改坏。**
   §4 的参考 Boss 仅做打磨（占位替换、数值微调），不得在重做中破坏其设计。

---

## 4. 参考模板 Reference Templates

以下 GOOD Boss 是**重做时直接抄的对象**。每个示范一种可复用的设计模式。

| Boss | 路径 | 示范的设计模式 |
|------|------|----------------|
| **赢勾 Yingou** | `NPCs/Boss/Yingous/Yingou.cs` | **phase enum + telegraph 子状态 + 各自独立的 pattern 函数**——攻击高度可读、可扩展 |
| **旱魃 Hanba** | `NPCs/Boss/Hanbas/Hanba.cs` | **8 状态循环 + 符箓破除（talisman break）+ 场地牢笼（arena cage）**——目标/可破除机制 + 空间控制 |
| **敖广 AoGuang** | `Celestias/Boss/AoGuangs/AoGuang.cs` | **3 个 HP 阶段 + 19 状态 + 水域场地控制**——大型多阶段龙王标准 |
| **敖顺 Aoshun** | `Celestias/Boss/Aoshuns/Aoshun.cs` | **FSM + StormCharge 资源机制 + 雷霆空间控制**——资源/充能驱动节奏 |
| **神威 Vigor** | `Celestias/Boss/Vigors/Vigor.cs` | **3 阶段 + 符印封印 + 反击姿态（Counter Stance）+ 连招**——姿态/反击交互 |
| **百目 Argus** | `Celestias/Boss/Arguses/` | **预判式弓手 + 注视线（gaze-line）预告**——预测性 telegraph |
| **玄武 Xuanwu** | `Celestias/Boss/FourSacredBeasts/Xuanwus/Xuanwu.cs` | **Verlet 蛇身物理 + 绝对防御反射（Absolute Defense reflect）**——身体物理 + 反射窗口 |
| **黑白无常 BAW** | `Underworlds/Boss/BAWImpermanences/` | **双 Boss 角色分工 + 协同 + 复活机制**——dual-boss synergy |
| **冥狐 Nether Kitsune** | `Underworlds/Boss/NetherKitsunes/NetherKitsune.cs` | **九尾编排 + 7 套攻击 pattern**——tail choreography（仅需打磨 P3 + 换占位弹幕） |

---

## 5. 优先级总表 Master Priority Table

> 优先级：**P0** 阻塞性（套路最重/换皮/死状态崩坏）→ **P3** 打磨。
> Tier 取自 `PROGRESSION_DESIGN_SPEC.md` §2.2（地表祖龙/劫云为可选节点，无 SB tier）。

| 名称 | 区域 | Tier | 评级 | 优先级 | 一句话问题 | 一句话重做方向 |
|------|------|------|------|--------|------------|----------------|
| 赢勾 Yingou | NPCs | T26 | GOOD | — | 参考模板 | 保留，作 NPCs 区克隆源 |
| 旱魃 Hanba | NPCs | T24 | GOOD | — | 参考模板 | 保留，作牢笼/符箓克隆源 |
| 牛头马面 NiuMa | NPCs | T9 | MEDIOCRE | P2 | 双 Boss 骨架好但半血加速喷射 | 给两体真正的协同分工 + 脚本幕 |
| 九尾 Kyuubi | NPCs | T15 | MEDIOCRE | P2 | P2 RNG 轮盘 + 原版占位弹 | 固定可读 pattern + 狐火自定义弹 |
| 苍龙真身 Azure Dragon | NPCs | T45 | MEDIOCRE | P1 | 终局蠕虫 P3 加速 + 原版闪电球 | P3 改雷暴控场幕 + 自定义弹 |
| 劫云 ×3 Tribulation | NPCs | — | MEDIOCRE | P3 | 3 份复制粘贴文件、纯生存 | 合并为 1 套参数化生存事件 |
| 后卿 Hoqing | NPCs | T25 | POOR | P0 | 2 状态旱魃换皮、复用旱魃 BGM | 独立瘟疫主题 FSM + 专属 BGM |
| 将臣 Jiangcen | NPCs | T27 | POOR | P0 | 9 数字状态但单一追踪火球 + TP 距离狂暴 | 飞僵主题多攻击族 + 脚本幕 |
| 祖龙残魂(地表) Archosaur | NPCs | 可选 | POOR | P1 | 8 字走位 + 雷球 RNG + 无敌分身噱头 + 自伤 | 重构为有节奏的可选 Raid |
| 黑熊精 BlackBear | NPCs | T3 | POOR | P3 | 未用 Attack_3、半血装饰狂暴 | 接入 Attack_3，给肉前小高潮 |
| 敖广 AoGuang | 天庭龙 | T31 | GOOD | — | 参考模板 | 保留，作龙王克隆源 |
| 敖顺 Aoshun | 天庭龙 | T34 | GOOD | — | 参考模板 | 保留，作资源机制克隆源 |
| 敖钦 Aokin | 天庭龙 | T32 | MEDIOCRE | P1 | 敖广换皮缺 Phase 3、火系身份弱 | 差异化 + 火系 Phase 3 场地 |
| 天御金龙 Celestial Dragons | 天庭龙 | T43 | MEDIOCRE | P1 | HP 缩放密度档、金蠕虫无天界身份 | 脚本化幕 + 天界签名机制 |
| 祖龙残魂(天庭) Ancestral Dragon Soul | 天庭龙 | T42 | MEDIOCRE | P1 | 50% 分裂强，但 Enraged 是永不进入的 stub | 完成激怒幕（接线 Enraged） |
| 敖闰 Aoyuan | 天庭龙 | T33 | POOR | P0 | 无真 FSM、原版蠕虫追逐、空 Phase 文件、P2 +4 弹提速 | 移植敖顺 FSM 结构性重写 |
| 神威 Vigor | 天庭其余 | T29 | GOOD | — | 参考模板 | 保留，打磨 |
| 百目 Argus | 天庭其余 | T30 | GOOD | — | 参考模板 | 保留，打磨 |
| 玄武 Xuanwu | 天庭其余 | T41 | GOOD | — | 参考模板 | 保留，打磨 |
| 青龙 Qinglong | 天庭其余 | T38 | MEDIOCRE | P1 | 共享四兽模板、P3 Fury hub | 杀 P3 hub，给 1 签名机制 |
| 白虎 Baihu | 天庭其余 | T39 | MEDIOCRE | P1 | 同上 | 同上 |
| 朱雀 Suzaku | 天庭其余 | T40 | MEDIOCRE | P1 | 同上 + 原版占位弹（Rebirth 概念好） | 围绕 10% 涅槃重生设计 + 换弹 |
| 大椿 Dazheng | 天庭其余 | T37 | MEDIOCRE | P2 | 入场好、收缩场地好，但静态弹幕 + P2 FuryPatrol | 季节/锚点解谜，去 FuryPatrol |
| 树精 Dryads | 天庭其余 | T36 | MEDIOCRE | P3 | 仅潜地出彩，其余通用、P2=P1 更快 | 围绕潜地扩展，差异化 P2 |
| 毗沙门 Vaisravana | 天庭其余 | T35 | POOR | P0 | 近乎观察者换皮、P3 FourKingsWrath 喷射、宝塔装饰 | 拆模板，宝塔/财宝驱动机制 |
| 观察者 Celestial Overseer | 天庭其余 | T44 | POOR | P0 | 毗沙门换皮、P3 Punishment 喷射、全视主题未用 | 拆模板，全视/视线预判机制 |
| 黑白无常 BAW | 地府 | T46 | GOOD | — | 参考模板 | 保留 |
| 冥狐 Nether Kitsune | 地府 | T48 | GOOD | — | 参考模板（需打磨） | 打磨 P3 + 换原版占位弹 |
| 幽冥龙 Nether Dragon | 地府 | T49 | MEDIOCRE | P2 | 常驻 23 火焰/90tick、无 HP 阶段 | 去常驻喷射，加 HP 阶段 |
| 怨灵 Spectre | 地府 | T47 | MEDIOCRE | P1 | 8 命名阶段同 4 方法、FinalGrudge 喷射、怨念主题未用 | 真差异化阶段 + 怨念机制（断换皮链） |
| 阴天子 Yin Emperor | 地府 | T52 | MEDIOCRE | P0 | 终局却无 HP 幕、随机 5 状态池、缺冥律/审判身份 | 加脚本幕 + 审判机制 + G7 身份 |
| 尸骸 Corpses | 地府 | T50 | POOR | P1 | 双 IK 手强，但 DarkRitual 写了不触发、FinalRage 喷射、缺 downedCorpses | 接线 DarkRitual、修 rage、补 downed |
| 觉醒冥龙 Awakening Nether | 地府 | T51 | POOR | P0 | 怨灵/幽冥龙换皮、DesperateFury@25% 即用户抱怨原话 | 结构性重写，独立终局身份 |

---

## 6. 分区重做方案 Per-Region Redesign Sections

> 仅列 MEDIOCRE/POOR Boss。GOOD Boss 见 §4，仅打磨。

### 6.1 NPCs 区（早期 + 月后四僵尸）

#### 后卿 Hoqing — `NPCs/Boss/Hoqings/Hoqing.cs`（POOR · P0）
- **反模式：** 仅 2 状态的旱魃换皮；**复用旱魃 BGM**；几乎无独立身份。
- **重做：**
  - 移植赢勾的 phase enum + telegraph 子状态结构（§4），但内容全新。
  - **签名机制（瘟疫/疠气）：** 场地内生成"疫源"地块，玩家久留叠加 DoT；后卿可"播种"，玩家需主动清理（呼应旱魃的可破除符箓，但主题不同）。
  - 阶段幕：P1 散播疫源 → P2 召唤瘟疫仆从守源 → P3 全场疠气潮汐（有安全缝，非纯加速）。
  - **专属 BGM**，停止复用旱魃曲。

#### 将臣 Jiangcen — `NPCs/Boss/Jiangcens/Jiangcen.cs`（POOR · P0）
- **反模式：** 9 个数字状态但全部归结为**单一追踪火球族**；用 **TP-距离触发"狂暴"**。
- **重做：**
  - 飞僵主题：高机动俯冲 + 僵尸尖刺；火球只作其中一族，新增近身横扫、地刺、扑击。
  - 去掉"TP 远离即狂暴"，改为**脚本幕**：P2 引入封印锚点（玩家击破解控），P3 改变场地（碎裂地形 / 落石）。
  - 每个攻击补 telegraph 前摇。

#### 祖龙残魂(地表) Archosaur — `NPCs/Boss/Archosaur/ArchosaurBoss.cs` + `CloneBoss.cs`（POOR · P1）
- **反模式：** 固定 8 字走位 + 雷球 RNG + **无敌分身噱头** + 自伤行为。
- **重做：**
  - 移除无敌分身或改为**可破弱点**（打掉分身才解锁本体破绽窗口）。
  - 8 字走位改为有节奏的冲刺/盘绕，配 telegraph。
  - 作为 HM 可选 Raid，强调"青龙之灵 bootstrap"的爽快度，不追求终局复杂度。

#### 牛头马面 NiuMa — `NPCs/Boss/NiutouMamian/NiuMa_NPC.cs`（MEDIOCRE · P2）
- **反模式：** 双 Boss 骨架不错，但**半血=加速喷射**。
- **重做：** 借鉴黑白无常（§4）的**双 Boss 分工 + 协同**——牛头近战压制、马面远程勾索；两体存活时触发协同连携（链刃—勾索夹击），一体死亡后另一体进入**愤怒接管**（改攻击族而非加速）。

#### 九尾 Kyuubi — `NPCs/Boss/KyuubiKitsunes/KyuubiKitsune.cs`（MEDIOCRE · P2）
- **反模式：** 尾巴系统丰富但 **P2 是 RNG 轮盘**；**原版占位弹幕**。
- **重做：** P2 改为**固定可读 pattern 序列**（参考冥狐 §4 的 9 尾编排）；原版法球替换为狐火主题自定义弹（§2.3）。

#### 苍龙真身 Azure Dragon — `NPCs/Boss/AzureDragons/AzureDragonHead.cs` / `AzureDragonAI.cs`（MEDIOCRE · P1）
- **反模式：** 终局 G7 蠕虫但 **P3=加速喷射**；**原版闪电球**。
- **重做：** P3 改为**雷暴控场幕**——划定雷击区（telegraph 后落雷）、风域推拉玩家走位；原版闪电球换自定义雷/风弹。配合 SPEC §5.7 青龙→苍龙觉醒剧情，强化"真身降临"的终局压迫感。

#### 劫云 ×3 Tribulation — `NPCs/Boss/TribulationCloud/TribulationCloud{Black,Red,Purple}.cs`（MEDIOCRE · P3）
- **反模式：** 3 份复制粘贴文件，纯生存事件。
- **重做：** 合并为**单套参数化生存逻辑**（颜色/强度/雷击次数作参数），消除三份重复代码。配合 SPEC §3.2 修复伤害公式（XOR→加法）。

#### 黑熊精 BlackBear — `NPCs/Boss/BlackBear/BlackBear.cs`（POOR · P3）
- **反模式：** `Attack_3`（`BlackBear_Proj3`）**编码却未使用**；半血**装饰性狂暴**。
- **重做：** 接入 Attack_3 形成 3 攻击循环；半血改为一个**肉前小高潮幕**（如召唤幼熊 / 滚石冲撞），给新手一个"阶段感"样板。

### 6.2 天庭龙系 Celestias Dragon Line

#### 敖闰 Aoyuan — `Celestias/Boss/Aoyuans/`（POOR · P0，结构性重写）
- **反模式：** **无真正 FSM**；原版蠕虫追逐 + 计时攻击窗；`Aoyuan.Phase1.cs`/`Phase2.cs` **空 stub**；P2 仅 +4 投射物 & 提速。
- **重做：**
  - **移植敖顺（§4）的 FSM + 资源机制**作为骨架。
  - 西海冰系签名：**永冻立场**——被冻地块减速玩家，敖闰可冻结场地分块，玩家需走位破冰。
  - 删除空 Phase 文件，按真实状态机重建（或实装其内容）。
  - P2 改规则（冰封场地阶段），非加投射物。

#### 敖钦 Aokin — `Celestias/Boss/Aokins/`（MEDIOCRE · P1）
- **反模式：** 敖广换皮**缺 Phase 3**；火系身份弱；P2 仅 +投射物 + 加速。
- **重做：** 与敖广差异化（§7 规则）；**新增火系 Phase 3 场地**——熔岩/焚风区域控制（点燃叠层呼应 SPEC §5.2 火系武器主题）。

#### 天御金龙 Celestial Dragons — `Celestias/Boss/CelestialDragons/CelestialDragons.cs`（MEDIOCRE · P1）
- **反模式：** HP 缩放密度档位（=变相加速）；金蠕虫**无天界身份**。
- **重做：** 用脚本化幕替换密度档；天界签名机制（如金色秩序符阵：周期性在场地布"天规"安全/危险格）。

#### 祖龙残魂(天庭) Ancestral Dragon Soul — `Celestias/Boss/AncestralDragonSouls/`（MEDIOCRE · P1）
- **反模式：** 50% **分裂机制很强**，但 `Enraged` 状态是 **stub，永不进入**——反模式极端形态（写了不用）。
- **重做：** **完成 Enraged 幕的接线**——分裂后两段残魂进入真实激怒行为（协同夹击 / 重组冲锋），而非占位。务必与地表 Archosaur 区分（§1.4）。

### 6.3 天庭其余 Celestias Non-Dragon

#### 毗沙门 Vaisravana — `Celestias/Boss/Vaisravanas/Vaisravana.cs` + `VaisravanaPhases.cs`（POOR · P0）
- **反模式：** 与观察者**互为换皮**；P3 `FourKingsWrath` 喷射；宝塔仅装饰。
- **重做：**
  - **拆开与观察者的共享模板**（先确定哪边保留 base）。
  - **财宝/宝塔签名机制：** 宝塔实体化为场地结构——玩家可借塔为掩体/跳台；拾取金币增伤（呼应 `TreasurePagodaStaff` 主题）。
  - 重建 P3：四天王轮替召唤（各带不同攻击族），非统一喷射。

#### 观察者 Celestial Overseer — `Celestias/Boss/CelestialOverseers/CelestialOverseer.cs`（POOR · P0）
- **反模式：** 毗沙门换皮；P3 `Punishment` 喷射；**全视主题完全未用**。
- **重做：**
  - 拆模板（与毗沙门分家）。
  - **全视/视线签名机制（参考百目 §4）：** 注视线预判玩家走位、"凝视"锁定后惩戒；玩家需打断视线 / 进入盲区。
  - 作为天庭入侵终局，P3 应是**全场审视幕**（扫描光束 + 安全格），非喷射。

#### 青龙 / 白虎 / 朱雀 — `Celestias/Boss/FourSacredBeasts/{Qinlongs,Baihus,Suzakus}/`（MEDIOCRE · P1）
- **反模式：** 三者共享 `Patrol→GetRandomPhaseN→P3 Fury hub` 模板；P3 是共享喷射 hub。
- **重做（共同）：** **杀掉 P3 Fury hub**，三者各保留 **1 个签名机制**：
  - 青龙：风/雷流——风域位移 + 雷链（呼应 `WindserpentDao`/`ThunderclapLongbow`）。
  - 白虎：金属冲撞——裂地冲击波 + 银脉冲连射节奏（呼应 `AurelianCataclysmSmasher`）。
  - 朱雀：**围绕 10% 涅槃重生（Rebirth）设计**（审计认为这是好概念）——重生后改变攻击族，焰羽弹幕替换原版占位弹（§2.3）。

#### 大椿 Dazheng — `Celestias/Boss/Dazhengs/Dazheng.cs`（MEDIOCRE · P2）
- **反模式：** 入场演出好、收缩场地好，但**静态弹幕地狱**；P2 `FuryPatrol`。
- **重做：** 围绕"季节/锚点"做**解谜机制**——场地四角季节锚点，玩家需按序激活/破坏改变 Boss 状态；去掉 FuryPatrol，P2 改为场地机制升级。

#### 树精 Dryads — `Celestias/Boss/Dryades/`（MEDIOCRE · P3）
- **反模式：** **潜地（burrow）是唯一亮点**，其余通用；P2=P1 更快。
- **重做：** 围绕潜地扩展（潜地伏击 + 根须地刺 telegraph）；P2 引入新机制（活木墙生长改变场地），非加速。

### 6.4 地府线 Underworld

#### 觉醒冥龙 Awakening Nether — `Underworlds/Boss/AwakeningNethers/AwakeningNetherHead.cs`（POOR · P0，结构性重写）
- **反模式：** 怨灵 + 幽冥龙的换皮；**`DesperateFury@25%` 是用户抱怨原话的逐字复刻**。
- **重做：**
  - 结构性重写，断开与怨灵/幽冥龙的抄袭链（需配合 §6.4 怨灵先修）。
  - 作为 T51 终局前哨，应有**独立终局身份**：多 HP 脚本幕、独特弹幕、场地控制。
  - 删除 `DesperateFury` 加速幕，改为有学习曲线的真实终局阶段。

#### 阴天子 Yin Emperor — `Underworlds/Boss/YinEmperors/YinEmperor.cs`（MEDIOCRE · P0）
- **反模式：** T52 终局**却无 HP 幕**；随机 5 状态池；**缺冥律/审判身份**；缺 G7 身份。
- **重做：**
  - **加脚本化 HP 幕**（终局 Boss 必须有清晰的阶段递进）。
  - **审判/冥律签名机制（§2.5）：** "冥律标记"——玩家被判定叠层后触发处决性攻击；引入审判庭场地（参考 SPEC §6.7 冥律标记）。
  - 强化 G7 / 准圣门控的终局仪式感（呼应 SPEC §3.2 G7）。

#### 尸骸 Corpses — `Underworlds/Boss/Corpseses/Corpses.cs`（POOR · P1）
- **反模式：** 双 IK 手系统**很强**，但 `DarkRitual` 阶段**写了从不触发**；`FinalRage@20%` 喷射；**缺 `downedCorpses`**。
- **重做：**
  - **接线 DarkRitual** 为真正的高潮幕（双手献祭仪式：玩家需打断仪式或承受全场惩戒）。
  - 修 FinalRage：改为规则变化而非喷射。
  - 补 `downedCorpses` 标记（配合 SPEC §3.1）。

#### 怨灵 Spectre — `Underworlds/Boss/Spectres/Spectre.cs`（MEDIOCRE · P1）
- **反模式：** 8 个命名阶段**实为同 4 个方法**；`FinalGrudge@25%` 喷射；**怨念主题未用**。
- **重做：**
  - 让 8 阶段**真正差异化**（不同方法/攻击族）。
  - **怨念签名机制：** 怨念叠层——Boss 受击积累怨念，达阈值释放定向报复（玩家需管理输出节奏）。
  - **优先修此 Boss 以断开"怨灵↔觉醒冥龙"换皮链**（§2.2），让 §6.4 觉醒冥龙重写有干净基线。

#### 幽冥龙 Nether Dragon — `Underworlds/Boss/NetherDragons/NetherDragonHead.cs`（MEDIOCRE · P2）
- **反模式：** **常驻 23 火焰/90 tick** 喷射；**无 HP 阶段**；G6 门控却无深度。
- **重做：** 去掉常驻火焰喷射；**加入 HP 阶段**（开矿主题：阶段切换改变场地矿脉/落石）；作为 G6 门控应有匹配的难度曲线。

---

## 7. 实施分期 Implementation Phasing

> 按优先级分批，每批内 Boss **相互独立**，适合后续并行派发子智能体（每个 Boss 一个 worktree/任务）。
> **先决：** 每批开工前，先确认 §4 参考模板未被改动（作为克隆基线）。

### Phase P0 — 反模式最重 / 换皮 / 死状态崩坏（最高优先）

| 批次 | Boss | 路径 | 重做要点 |
|------|------|------|----------|
| P0-A | 后卿 Hoqing | `NPCs/Boss/Hoqings/` | 瘟疫 FSM + 专属 BGM（克隆赢勾结构） |
| P0-A | 将臣 Jiangcen | `NPCs/Boss/Jiangcens/` | 飞僵多攻击族 + 脚本幕（去 TP 狂暴） |
| P0-B | 敖闰 Aoyuan | `Celestias/Boss/Aoyuans/` | 移植敖顺 FSM，结构性重写，清空 stub |
| P0-C | 毗沙门 Vaisravana | `Celestias/Boss/Vaisravanas/` | 拆模板 + 财宝机制 + 重建 P3 |
| P0-C | 观察者 Overseer | `Celestias/Boss/CelestialOverseers/` | 拆模板 + 全视机制 + 重建 P3 |
| P0-D | 觉醒冥龙 Awakening Nether | `Underworlds/Boss/AwakeningNethers/` | 结构性重写（依赖 P1 怨灵先修基线） |
| P0-D | 阴天子 Yin Emperor | `Underworlds/Boss/YinEmperors/` | 脚本幕 + 审判/冥律 + G7 身份 |

> **批内依赖：** P0-C 两个 Boss 需先裁定共享模板归属；P0-D 觉醒冥龙建议在 P1 怨灵修复后再开工（断换皮链）。

### Phase P1 — 核心脊柱反模式

| Boss | 路径 | 重做要点 |
|------|------|----------|
| 苍龙真身 Azure Dragon | `NPCs/Boss/AzureDragons/` | P3 雷暴控场 + 换原版弹 |
| 祖龙残魂(地表) Archosaur | `NPCs/Boss/Archosaur/` | 去无敌分身 + 节奏化走位 |
| 敖钦 Aokin | `Celestias/Boss/Aokins/` | 差异化 + 火系 Phase 3 |
| 天御金龙 Celestial Dragons | `Celestias/Boss/CelestialDragons/` | 脚本幕 + 天界机制 |
| 祖龙残魂(天庭) Ancestral Dragon Soul | `Celestias/Boss/AncestralDragonSouls/` | 接线 Enraged 激怒幕 |
| 青龙 Qinglong | `Celestias/Boss/FourSacredBeasts/Qinlongs/` | 杀 P3 hub + 风雷签名 |
| 白虎 Baihu | `Celestias/Boss/FourSacredBeasts/Baihus/` | 杀 P3 hub + 金属冲撞签名 |
| 朱雀 Suzaku | `Celestias/Boss/FourSacredBeasts/Suzakus/` | 涅槃重生机制 + 换弹 |
| 怨灵 Spectre | `Underworlds/Boss/Spectres/` | 阶段差异化 + 怨念机制（断换皮链） |
| 尸骸 Corpses | `Underworlds/Boss/Corpseses/` | 接线 DarkRitual + 修 rage + downed |

> **批内依赖：** 青龙/白虎/朱雀共享模板，建议同批协调（先抽公共 base，再各自加签名机制）；怨灵优先于 P0-D 觉醒冥龙。

### Phase P2 — 骨架可用、补阶段深度

| Boss | 路径 | 重做要点 |
|------|------|----------|
| 牛头马面 NiuMa | `NPCs/Boss/NiutouMamian/` | 双 Boss 协同分工（借黑白无常） |
| 九尾 Kyuubi | `NPCs/Boss/KyuubiKitsunes/` | 固定 pattern + 狐火换弹 |
| 大椿 Dazheng | `Celestias/Boss/Dazhengs/` | 季节锚点解谜，去 FuryPatrol |
| 幽冥龙 Nether Dragon | `Underworlds/Boss/NetherDragons/` | 去常驻火焰 + 加 HP 阶段 |

### Phase P3 — 打磨

| Boss | 路径 | 重做要点 |
|------|------|----------|
| 黑熊精 BlackBear | `NPCs/Boss/BlackBear/` | 接入 Attack_3 + 肉前小高潮 |
| 劫云 ×3 Tribulation | `NPCs/Boss/TribulationCloud/` | 合并为 1 套参数化逻辑 |
| 树精 Dryads | `Celestias/Boss/Dryades/` | 扩展潜地 + 差异化 P2 |
| 冥狐 Nether Kitsune | `Underworlds/Boss/NetherKitsunes/` | 打磨 P3 + 换原版占位弹 |

### 并行派发建议

- **每个 Boss = 一个独立子任务**（独立 worktree），除标注的批内依赖外可并行。
- **关键串行链：** 怨灵（P1）→ 觉醒冥龙（P0-D）；毗沙门 ↔ 观察者 模板裁定（P0-C 内部协调）；青龙/白虎/朱雀 公共 base 抽取（P1 协调）。
- 每个子任务交付前对照 **§3 设计原则 9 条**逐项自检，并确保未触碰 §4 参考模板。

---

## 8. 文档索引 Cross-References

| 主题 | 文档 · 章节 |
|------|-------------|
| Boss 顺序 / Tier / 掉落 / 召唤 / 门控 | `PROGRESSION_DESIGN_SPEC.md` §2.2、§5、§6 |
| Downed 标记修复（含 `downedCorpses` 等缺失标记） | `PROGRESSION_DESIGN_SPEC.md` §3.1 |
| 修仙门控 G6/G7（幽冥龙 / 阴天子 / 苍龙） | `PROGRESSION_DESIGN_SPEC.md` §3.2 |
| 冥律标记 / 地府 DoT 主题（阴天子签名机制依据） | `PROGRESSION_DESIGN_SPEC.md` §6.7 |
| 青龙→苍龙觉醒剧情合并 | `PROGRESSION_DESIGN_SPEC.md` §5.7 |
| 内容缺口 / 空掉落 / P0–P3 | `PLAYABILITY_AUDIT_REPORT.md` |
| 占位武器 / 材料理想机制（换弹时参考主题） | `PLACEHOLDER_CONTENT_REGISTRY.md` |
| 美术 / 纹理占位 | `TEXTURE_COMPLETION_PLAN.md` |

---

## 文档维护

- 本文是 **Boss 战斗质量**的权威基准；进度/数值仍以 `PROGRESSION_DESIGN_SPEC.md` 为准，二者**互不覆盖**。
- 每完成一个 Boss 重做，在本文 §5 总表对应行评级升级（POOR/MEDIOCRE→GOOD），并在 §7 批次勾除。
- **禁止**在重做中破坏 §4 参考模板；如需改动参考 Boss，须先在本文记录理由。
- **版本历史：** v1.0 · 2026-06-27 · 四路并行只读审计合并初版。

---

*Primordial / 洪荒 · Boss Combat Redo Master Plan · 重做前请先阅读 §3 设计原则与 §4 参考模板。*
