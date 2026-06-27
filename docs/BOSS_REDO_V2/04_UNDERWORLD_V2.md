# 地府 Boss 战斗设计·第二迭代（Underworld Bosses · Design Iteration v2）

> **文档性质：** 地府线 Boss 的**第二迭代设计规划**（在 `BOSS_REDO_PLAN.md` 一次重做之上的"升格层"）
> **版本：** 2.0 · 2026-06-27
> **上游依据：** `docs/BOSS_REDO_PLAN.md`（§2 反模式、§3 设计原则、§4 参考模板、§6.4 地府线）
> **配套（待建）：** `docs/BOSS_REDO_V2/00_SHADER_VFX_TOOLKIT.md`（着色器/VFX 工具箱）。**本文撰写时该工具箱尚未落盘**，文中所有 shader 引用均为"概念占位"，命名见 §0.3，落地后请回填具体 primitive 名。
> **范围：** `Underworlds/Boss/` 下 7 个 Boss —— 怨灵 Spectre、枉死千骸 Corpses、幽冥龙 Nether Dragon、幽冥妖狐 Nether Kitsune、黑白无常 BAW、觉醒冥龙 Awakening Nether、阴天子 Yin Emperor（均忽略 `Items/`）。

---

## 0. 升格前提 Elevation Premise

### 0.1 v1 解决了什么，v2 要做什么

`BOSS_REDO_PLAN.md` 的一次重做是**纠错**：拆换皮、接死状态、去"低血量=加速喷弹幕"、补脚本幕。它让地府 Boss"不再坏"。

**v2 的目标是让它们"令人难忘"：** 在已修正的骨架上，叠加**作者编排的标志性时刻（authored set-pieces）**、**统一的可读语言**、**进阶着色器表现**，并补齐审计点名却始终缺席的**地府身份层**（DoT / 冥律 / 怨念 / 魂魄）。v2 不推翻 v1，只在其上"升格"。

### 0.2 审计发现的核心缺口：地府身份层完全缺席（必须补齐）

> 代码实证：7 个地府 Boss 的 `AI()` 几乎都首行写 `UnderworldPlayer.UnderworldEffect = true;`——但 `UnderworldPlayer` 仅有一个**空的** `bool` 字段与 `ResetEffects()`，**没有任何机制挂在上面**。这正是 `BOSS_REDO_PLAN.md` §2.5 "地府 DoT / 冥律 / 怨念：完全缺席"的逐字证据。

v2 的**第一优先**是把这个空壳变成真正的地府战斗身份层（`UnderworldField`），让"在地府打 Boss"本身就有规则压力，而非只是换了张贴图。三条主轴（与 `PROGRESSION_DESIGN_SPEC.md` §6.7、§7 对齐）：

| 主轴 | 含义 | 玩法压力 | 落点 |
|------|------|----------|------|
| **冥律标记 Nether Decree** | 被 Boss"判定"后叠层的 debuff（已有 `NetherDecreeMark` 占位 ModBuff + `YinJudgmentPlayer`） | 满层触发处决/定魂，迫使玩家管理"被注视/被记账"的节奏 | 阴天子主用，怨灵/尸骸/觉醒龙各取一种变体 |
| **魂蚀 DoT Soul-Erosion** | 站位/受击叠加的持续伤害与回复削弱（已有 `SoulErosion`/`SoulErosionScepter` 命名） | 把"地府"做成有 DoT 的高威胁场，鼓励走位与净化 | 全地府通用环境层，Boss 强化它 |
| **怨念账 Grudge Ledger** | Boss 记录玩家行为（输出、停留、清场）并"清算" | 资源/节奏管理，替代纯弹幕 | 怨灵签名，BAW/觉醒龙借用 |

**统一裁定：** 每个地府 Boss v2 至少**强化或消费**上述三轴之一；`UnderworldEffect` 必须从空字段升级为可被 Boss 调制（强度/层数/视觉）的真实场。

### 0.3 着色器/VFX 概念命名（占位，待工具箱回填）

下列为 v2 反复引用的 shader primitive 概念名。工具箱（`00_SHADER_VFX_TOOLKIT.md`）落地前，实现侧可先用现有 dust/纯色叠绘近似，**见着色器工具箱**后替换：

| 概念名 | 效果 | 主要用途 |
|--------|------|----------|
| `soul-dissolve` 魂魄消融 | Alpha-erosion + 边缘内发光的"实体→魂"溶解/重凝 | 出场、相位切换、传送、死亡、复活 |
| `nether-fog-distortion` 冥雾扰动 | 屏幕空间折射/流动雾，密度可调 | 地府环境层、龙系雾、妖狐迷雾 |
| `yin-yang-split` 阴阳分屏调色 | 屏幕按一条移动分界线左右分裂为两套调色（冷阴/暖阳） | 阴天子审判庭、场地阴阳分割 |
| `grudge-desaturation` 怨念褪色 | 怨念层数越高，画面越褪色/泛青黄，命中处怨念回涌染色 | 怨灵、怨念账可视化 |
| `rift-warp` 次元裂隙扭曲 | 沿裂隙线的空间拉扯/色散 | 觉醒龙次元裂隙、幽冥龙传送门 |
| `decree-vignette` 冥律压迫晕影 | 满层时屏幕边缘渗出青焰晕影 + 心跳脉动 | 冥律标记满层、处决预告 |
| `prison-overlay` 镇魂狱牢笼 | 半透明符文牢笼/锁链网叠加在场地边界 | 镇魂狱场地约束 |

> **MP/性能总则：** 所有全屏 shader 仅在 `Main.netMode != Server` 且玩家"参战"时启用；分屏/折射类全屏后处理**单 Boss 限定一层**，避免叠加；着色器强度统一走 `UnderworldField` 的同一个 0–1 标量驱动，便于一处降级。

---

## 1. v2 升格原则 Elevation Principles（在 v1 §3 之上）

1. **每个 Boss 至少 1 个"作者编排时刻"（authored set-piece）。** 不是又一个攻击模式，而是有起承转合、有镜头/音乐/着色器配合、玩家会记住并复述的片段（如阴天子的阴阳审判、尸骸的献祭仪式）。
2. **身份层先于花活。** 先把 §0.2 三轴之一接到该 Boss，再谈表现升级。表现是为身份服务的。
3. **可读语言统一。** 全地府共用一套预告色/形（见 §2），玩家学一次、处处通用，降低"看不懂"挫败。
4. **着色器表现"用在刀刃"。** 全屏后处理只服务于高光时刻（出场/相位/处决/死亡），不做常驻全屏滤镜（除可控强度的环境雾）。
5. **随机性收束为"可读编排"。** 现存大量 `Main.rand.Next(状态池)`（怨灵 `ChooseNextPhase`、妖狐 P2/P3）应改为带权重/带连段记忆的编排，保留变化但去掉"轮盘赌"。
6. **GOOD Boss 只升表现、不动结构。** BAW、妖狐为参考级；v2 对它们仅做"打磨 + 着色器换皮 + 占位弹替换"，禁止改坏其协同/编排骨架。

---

## 2. 通用可读语言 Shared Readability Language

| 语义 | 颜色 | 形状/动效 | 时长（telegraph 提前量） |
|------|------|-----------|--------------------------|
| 即将冲刺/穿刺 | 青白 cyan-white | 沿轴向的拉长光线 + Boss 蓄力内缩 | 25–35 tick |
| 落点/范围杀 | 青黄 yellow-cyan | 地面/空中符文圈，由细变实 | 40–60 tick |
| 处决/满层惩戒 | 赤红 crimson | `decree-vignette` 边缘晕影 + 低频心跳音 | 60–90 tick（必须最长） |
| 不可站区（镇魂狱/阴阳错区） | 暗紫 / 冷阴蓝 | `prison-overlay` 符文网 / 分屏边线 | 持续显示 |
| 安全缝/破绽窗 | 柔白 soft-white | 短暂高光呼吸 | 与攻击同步 |

**硬规则：** 越致命的攻击，telegraph 越长、对比越强；处决类一律配 `decree-vignette` + 专属低频音，**绝不**与普通弹幕共用提示。

---

## 3. 逐 Boss v2 设计 Per-Boss v2 Design

> 标注 **[在重做中 / IN FLUX]** 的两个 Boss（觉醒冥龙、阴天子）正由其他 agent 实施 v1 P0；本节**不依据其当前代码**，仅以 `BOSS_REDO_PLAN.md` §6.4 的 intended P0 为基线向上升格。

---

### 3.1 怨灵 Spectre 「冤魂记账者 / The Grudge-Keeper」 — MEDIOCRE→ · P1

**1) Fight fantasy & identity.** 无数冤魂凝成的记账幽灵——它**记住**你怎么打它，再把你的"造业"原样还给你。

**2) Phase/act narrative（在 v1 之上的升格）.**
- v1 已要求：8 阶段真差异化、加怨念叠层、断"怨灵↔觉醒龙"换皮链。当前代码的硬伤是 `ChooseNextPhase()` 纯 `Main.rand.Next`（轮盘赌）+ `FinalGrudge@25%` 喷射。
- **v2 ELEVATION —— 把"怨念叠层"从数值升级为可见的《怨念账》机制贯穿全场：**
  - **一幕·缠（Haunting/SoulStorm/GrudgeChain 编排化）：** 三个基础式不再随机抽取，改为**带记忆的连段**（缠→风暴→锁链 的可读循环，偶发插入 PhantomRush 作 punctuation）。怨灵每受到一次"高 DPS 爆发"就在头顶**账本 UI**记一笔（怨念 +1）。
  - **二幕·债（50%）：** 怨念达阈值时，怨灵进入 **Possession**——但召唤的不是通用小怨灵，而是**"你欠下的冤魂"**：数量 = 当前怨念层。它们排成审问阵，玩家需主动清账（击杀冤魂 = 还债，降怨念）。
  - **三幕·偿（替换 FinalGrudge 喷射）：** 25% 触发 **Wailing 的升格版"清算"**——怨灵把累计怨念一次性折算成一道**定向报复波**（怨念越高、波越宽越快），但**报复方向始终来自玩家此前停留最久的象限**（账记得清楚）。这就把"狂暴喷射"改成了"你自己的输出节奏决定终幕难度"的资源博弈。

**3) Signature set-piece —— 《怨念清算 Grudge Reckoning》.** 25% 时镜头微拉远，画面随怨念层数 `grudge-desaturation` 逐渐褪色至近乎黑白，账本 UI 翻页，怨灵周身浮现你这一战所有"业"的残影；随后一道沿"你最爱待的角落"袭来的偿还波。清账成功（开战即压低怨念）→ 波弱且窄；积怨过重 → 满屏青黄。**这是"你怎么打就怎么被还"的镜像高潮。**

**4) Telegraph & readability.** 账本层数常驻头顶（数字 + 青黄填充条）；清算前 60 tick 全屏 `grudge-desaturation` 加深 + 低吟渐起作为唯一且明确的"终幕将至"信号；报复波来向用 §2 的青黄符线在该象限边缘预画。

**5) Presentation（shader/VFX/camera/shake/sky/music）.**
- `grudge-desaturation`（核心）：随怨念层数实时驱动全屏饱和度，命中怨灵处怨念"回涌"局部染青黄。
- `soul-dissolve`：召唤冤魂时由账本残影**重凝**为实体；清账击杀时溶散。
- 出场沿用现有 `SpectreVortex`，但接 `soul-dissolve` 让"凝聚成形"可信。
- 相机：清算时缓推 + 轻微呼吸缩放（≤1.15×）；shake 仅在偿还波发出瞬间（12,40 现有值即可）。
- 音乐：复用 `Underworld` 曲，清算幕叠一层人声哀吟 stinger（音频资产，非 shader）。

**6) Feasibility & cost.** **M。** 结构骨架已在（8 phase enum、SpectreHelper VFX 齐全），主要工作是把随机选择改为账本驱动 + 新增账本 ModPlayer/UI + 接 `grudge-desaturation`。**新 shader：是（grudge-desaturation 为全地府可复用资产，优先做）。** 复用：召唤逻辑、能量波、VFX helper 全部沿用。MP：账本需按玩家结算（怨念账记"对 Boss 的总输出"可全队共享，简化同步）。

---

### 3.2 枉死千骸 Corpses 「献祭的双手 / The Sacrificial Hands」 — POOR→ · P1

**1) Fight fantasy & identity.** 一颗头颅与两只独立 IK 巨手——它不是打你，是要把你**摆上祭坛**。双手系统是全模组最强的肢体编排，v2 要让它"演一场仪式"。

**2) Phase/act narrative（升格）.**
- v1 已要求：**接线从不触发的 `DarkRitual`**、修 `FinalRage@20%` 喷射、补 `downedCorpses`。代码确证 `DarkRitual` 的双手"画圆"只算了坐标、**没有任何 `hand.TriggerAttack`**，是彻底的死状态。
- **v2 ELEVATION —— 把 DarkRitual 做成全战核心的《献祭仪式》两段循环，而非一次性高潮：**
  - **常态·镇压：** 沿用现有强力的双手编排（拍掌 Clap、传送拍掌 TeleportClap、抓取 Grabbing、骨雨 BoneToss），这是 GOOD 级素材，保留并补 telegraph。
  - **70% / 40% —— 仪式开启（接线 DarkRitual）：** 双手离体，飞到场地**两个祭坛锚点**，开始"画引魂阵"。地面浮现一个不断收缩的**献祭法阵**（冥律 DoT 区）。玩家有两条解：①在限时内**打断一只手**（手有独立血/硬直窗）中止仪式；②躲进法阵的"安全符位"硬抗仪式完成。
  - **仪式完成的后果（替换 FinalRage 喷射）：** 若未打断，仪式"成功"——给玩家叠一层**冥律标记**并触发一次**全场镇压波**（一次性、可躲、非持续喷射）；若打断，双手重伤回体、Boss 进入短暂破绽窗（奖励正反馈）。**这把"狂暴"改成了有攻防选择的仪式博弈。**

**3) Signature set-piece —— 《引魂大阵 Soul-Summoning Ritual》.** 双手脱离躯干、各据一坛，锁链/符文从两手连成一张覆盖场地的 `prison-overlay` 符网，法阵随仪式进度收缩并加深 DoT。镜头略降至能同框两手与法阵，给玩家"看清两个目标 + 一个收缩死区"的决策画面。**记忆点：选择打断哪只手 vs 硬抗。**

**4) Telegraph & readability.** 仪式开启有 90 tick 长前摇（双手脱体动画 + 法阵由虚到实）；可被打断的手以柔白破绽高光标识；法阵收缩边线持续可见（暗紫 `prison-overlay`）；DoT 区严格沿法阵可视边界，绝不"看不见就掉血"。

**5) Presentation.**
- `prison-overlay`（核心）：献祭法阵 + 两手锁链符网。
- `soul-dissolve`：双手脱体/回体时的"骨→魂→骨"过渡；现有出场 Shadowflame dust 升级为 soul-dissolve。
- `decree-vignette`：仪式完成那一瞬的冥律标记惩戒，配赤红边缘。
- 现有拖尾/红色 FinalRage 染色保留作"重伤回体"状态色。
- shake：仅在拍掌落点、镇压波、打断成功三处；相机略降不旋转。
- 音乐：仪式段叠诵经式 drone（音频）。

**6) Feasibility & cost.** **M（偏大）。** 双手 IK、所有 HandState、骨雨/暗影球已就绪；主工作量在 ①给 `CorpsesHand` 加"脱体/可被打断/独立硬直"状态，②法阵实体（收缩 + DoT + 安全位），③补 `downedCorpses` 标记。**新 shader：是（prison-overlay 复用于阴天子镇魂狱，优先）。** 复用：现有手部攻击全留。MP：手的可打断状态、法阵进度需 `SendExtraAI` 同步。

---

### 3.3 幽冥龙 Nether Dragon 「掘墓的冥龙 / The Grave-Delving Wyrm」 — MEDIOCRE→ · P2

**1) Fight fantasy & identity.** 在冥雾与传送门间穿行的巨虫龙，以幽冥火与次元跳跃掘开生死边界——主题应是"开矿/掘墓"（G6 门控、死后生成幽冥矿）。

**2) Phase/act narrative（升格）.**
- v1 已要求：去掉**常驻 23 火焰/90tick** 喷射、加 HP 阶段。代码确证 `flameTimer` 恒定每 90 tick 喷 23 发，与 AI 状态机**完全解耦**——这是"主题机制断层 + 背景喷射"的典型。
- **v2 ELEVATION —— 用"掘墓"主题把无意义喷火改成场地演化的三段：**
  - **P1·巡墓（>66%）：** 现有 CircleAround/Hover/Charge/LaserSweep/PortalTeleport 骨架很好，**保留**。删除常驻喷火，改为**只在特定状态末尾**的有预告火幕（如冲刺收尾喷一次锥形火）。
  - **P2·裂土（66–33%）：** 龙的传送门不再只换位——**出口处砸开一道"幽冥矿脉裂缝"**，在场地留下可破坏的悬浮矿体（既是掩体也是 DoT 源）。玩家可用裂缝走位/卡视线，规则变了而非数字变了。
  - **P3·噬墓（<33%）：** 龙沿场地"穿土潜行"——身体分段在矿脉间钻进钻出（用现有蠕虫分段 + 传送骨架扩展），激光横扫升级为沿矿脉的连锁爆裂。**无加速喷射**，难度来自空间被矿脉切割。

**3) Signature set-piece —— 《穿墓追猎 Burrow Hunt》.** P3 龙整体钻入一道巨型 `rift-warp` 裂隙、消失数秒，场地多处矿脉同时震动预告出口，随后龙从其中一处轰然贯穿全屏。结合现有的整虫传送（`TeleportWholeBody`）已具备技术基础。**记忆点：读震动找出口。**

**4) Telegraph & readability.** 传送门开启沿用现有蓄力（吸入 dust + 音效）已不错，v2 加 `rift-warp` 让"门"真的扭曲空间；穿墓出口在出现前 35 tick 用青白轴线 + 矿脉震动双重预告；火幕改为有锥形预画的一次性攻击。

**5) Presentation.**
- `rift-warp`（核心）：传送门与穿墓裂隙的空间扭曲/色散，替换现有纯 dust 门。
- `nether-fog-distortion`：把已有的 `NetherDragonFogSystem`（涟漪/雾密度，PreDraw 已读取 fogDensity）升级为屏幕空间折射雾，龙在浓雾中半隐。
- 现有蓝焰拖尾、涟漪 CreateRipple 全保留。
- shake：穿墓贯穿/急刹现有值即可。
- 音乐：复用 `Underworld`。

**6) Feasibility & cost.** **M。** 传送/分段/雾系统/激光已就绪，工作量在矿脉场地实体 + 三段 HP 门 + 删常驻火。**新 shader：建议（rift-warp 与觉醒龙共享，fog-distortion 升级现有系统）；可先用现有 dust/涟漪降级上线。** MP：矿脉实体需作 NPC/Projectile 同步。

---

### 3.4 幽冥妖狐 Nether Kitsune 「迷雾九尾 / The Mist Nine-Tails」 — GOOD（需打磨）· P3

**1) Fight fantasy & identity.** 幽蓝迷雾中的青丘鬼狐，九条幽冥尾各自为战、虚实难辨——模组中最华丽的肢体编排之一，v2 仅升表现、不动骨架。

**2) Phase/act narrative（升格）.**
- v1 已要求：**打磨 P3 + 替换原版占位弹**。代码确证：`FireSoulProjectile`、SpiritRealm 幻影、VoidStrike 全部发射 `ProjectileID.CultistBossLightningOrb`（原版法球）——违反 §2.3。P3 `Possession` 用 `AttackTimer%15` 随机抽尾，略偏轮盘。
- **v2 ELEVATION（克制，保 GOOD）：**
  - **换弹：** 所有 `CultistBossLightningOrb` → 自定义**幽冥狐火弹**（青蓝魂火，可接 `soul-dissolve` 拖尾）。这是硬性必改项。
  - **P3 Possession 收束随机：** 把 `Next(4)` 随机抽尾改为**幻影/本体真假博弈的可读编排**——SpiritRealm 的幻影系统（已有 phantom 数组与淡入淡出）延伸到 P3，让"哪个是真身"成为 P3 的核心读法，而非手速抽尾。
  - **迷雾消费身份：** 现有 `NetherKitsuneFogSystem` 已驱动 `fogIntensity`；v2 让浓雾**真正影响玩法**（雾中本体降低被锁定可读性，玩家需靠尾巴指向/狐火来源反推位置），把"装饰雾"接进 §0.2 的魂蚀场。

**3) Signature set-piece —— 《虚实九影 Phantom Veil》.** P3 高潮：本体与 4 幻影在迷雾中同步施放虚空九刺，仅真身的刺有实体伤害符线（柔白），幻影为虚（青蓝半透）。`soul-dissolve` 让真假切换时短暂"全部溶为魂雾再重凝"，迫使玩家每轮重新辨真。**记忆点：在九刺压迫下读真身。**

**4) Telegraph & readability.** 真身九刺 = 柔白实线 + 较长前摇；幻影九刺 = 青蓝虚线、无 `decree`/无实体反馈，玩家可学会用"反馈缺失"判伪；雾浓度越高，真身轮廓越靠尾巴根部高光辨识（给一个稳定可读锚）。

**5) Presentation.**
- `soul-dissolve`（核心）：真身/幻影切换、出场浮现、传送审判。
- `nether-fog-distortion`：升级 `NetherKitsuneFogSystem` 为屏幕空间雾，强度走 fogIntensity。
- `grudge-desaturation`（轻度）：P3 附身狂暴时画面微褪色呼应地府身份。
- 现有幽蓝发光、尾巴 telegraph、幻影绘制全保留。
- shake/相机：保持现值，不新增。
- 音乐：复用 `Underworld`。

**6) Feasibility & cost.** **S–M。** GOOD 骨架完整，主工作是换弹（确定性）+ P3 真假编排（中）+ 雾升级（接 shader）。**新 shader：复用（soul-dissolve / fog-distortion，均为其它 Boss 已需的共享资产，妖狐零新增）。** MP：真假身份需同步"哪个是真身"标志。**禁止改坏其九尾编排与七套 pattern。**

---

### 3.5 黑白无常 BAW 「双生勾魂使 / The Twin Reapers」 — GOOD · P3（打磨/视觉升级参考）

**1) Fight fantasy & identity.** 一黑（锁链近战）一白（幽术远程）的勾魂双使，分工 + 协同 + 互相复活——模组双 Boss 协同的黄金范本（v1 §4 参考模板）。v2 仅作**视觉升级 + 协同放大**，作为其它双体/协同设计的参照。

**2) Phase/act narrative（升格）.**
- v1：保留，不重做。代码确证其骨架优秀：黑无常 ChainDash/Grab/Sweep/Pull、白无常远程、`CheckDead` 互救复活、`AI_SynergyAttack` 双半血触发灵魂锁链、`BAWPlayer` 自带 zoom/shake 镜头系统。
- **v2 ELEVATION（纯升表现，不动 AI 结构）：**
  - **协同时刻视觉放大：** 现有 `AI_SynergyAttack` 已发"连接黑白的灵魂锁链"——v2 给这根锁链接 `soul-dissolve` + 沿链的能量流光，并在协同触发时短暂 `yin-yang-split` **黑白分屏**呼应"无常"二字主题（左阴右阳、对应黑白二使位置）。
  - **复活演出升格：** `CheckDead` 复活（已有完整演出 + drawAlpha 渐显）接 `soul-dissolve`：死去一使"溶为魂"被伙伴重新"凝形"，比现有 alpha lerp 更具仪式感。

**3) Signature set-piece —— 《黑白无常·勾魂连环 Yin-Yang Reaping》.** 双半血协同：屏幕沿两使连线 `yin-yang-split` 一分为二（黑侧锁链、白侧幽术），灵魂锁链在中线流光，玩家被夹在阴阳之间走位。**作为"双体协同高光"的可复用范式样板（供牛头马面等参考）。**

**4) Telegraph & readability.** 沿用现有蓄力音/dust/zoom（`ScreenPlayer.SetZoom`）；协同锁链中线即"高危分界"，分屏边线本身就是 telegraph，玩家天然读懂"别站中线"。

**5) Presentation.**
- `yin-yang-split`（核心，作该 shader 的首发用例）：协同时黑白分屏。
- `soul-dissolve`：复活/重凝、出场。
- 现有 `BAWPlayer` 的 zoom + shake 镜头系统直接驱动这些时刻。
- 音乐：复用 `Underworld`。

**6) Feasibility & cost.** **S。** 纯视觉叠加，无 AI 改动，风险最低。**新 shader：是但属共享（yin-yang-split 与阴天子共用，BAW 作首个落地用例验证 shader）。** **严禁触碰其协同/复活骨架。**

---

### 3.6 觉醒冥龙 Awakening Nether 「次元终焉之龙 / The Dimensional End-Wyrm」 — POOR→ · P0 **[在重做中 / IN FLUX]**

> 本节**不依据当前代码**（正由其它 agent 实施 P0 结构性重写）。基线取 `BOSS_REDO_PLAN.md` §6.4：**结构性重写、断怨灵/幽冥龙换皮链、删 `DesperateFury@25%` 加速、给独立终局身份（多 HP 脚本幕 / 独特弹幕 / 场地控制）。** intended P0 描述为三段脚本幕：**冥界巡游 / 次元裂隙 / 虚空吞噬**。

**1) Fight fantasy & identity.** T51 终局前哨——一条撕裂次元、吞噬虚空的觉醒龙；它的威胁不在"快"，在于**把战场本身的空间吃掉**。

**2) Phase/act narrative（在 intended P0 三幕之上升格）.**
- **P0 基线三幕：** 冥界巡游（巡场建立压迫）→ 次元裂隙（开裂隙改变场地）→ 虚空吞噬（终幕）。
- **v2 ELEVATION —— 让三幕递进地"压缩可用空间"，把空间作为资源：**
  - **冥界巡游（升格）：** 巡游时沿途留下短时 `rift-warp` 残痕（魂蚀 DoT 微区），教玩家"龙过之处即危险"。
  - **次元裂隙（升格为场地切割）：** 不只是开裂隙放弹——裂隙**永久分割场地**为数块，玩家可用裂隙的"另一侧出口"做战术传送（双刃：龙也会用）。这把"加投射物"彻底换成空间博弈。
  - **虚空吞噬（替换 DesperateFury 的真终幕）：** 龙张口在场地中心生成一个**持续吞噬的虚空奇点**，缓慢拉拽玩家与所有弹幕；安全区是不断变化的"裂隙背风面"。玩家必须读裂隙布局抗拉走位，而非比手速。**这是"有学习曲线的终局"而非加速喷射。**

**3) Signature set-piece —— 《虚空吞噬 Void Devour》.** 终幕奇点：全屏 `rift-warp` 向中心汇聚色散，音乐抽离为低频耳鸣，所有实体被缓拉向心；龙绕奇点游弋伺机贯穿。玩家在"被吞 vs 走位 vs 输出"间权衡。**记忆点：在塌缩的战场边缘求生。**

**4) Telegraph & readability.** 裂隙生成有长前摇（裂纹由细到裂）+ 暗紫边线；奇点的拉力梯度用向心粒子流可视化（越靠近流越急）；龙贯穿奇点前用青白长轴线预告。所有"空间杀"必须可见可读，严禁隐形拉死。

**5) Presentation.**
- `rift-warp`（核心，与幽冥龙共享）：裂隙、奇点、贯穿。
- `nether-fog-distortion`：终幕的虚空雾。
- `decree-vignette`：被奇点拉拽临界时的赤红边缘警示。
- 相机：终幕缓拉远以露出整个塌缩场地；shake 在贯穿/裂隙生成时。
- 音乐：终幕做"抽真空"式静默→轰鸣对比（音频设计）。

**6) Feasibility & cost.** **L。** 依赖 P0 重写先落地（**强串行依赖**），且空间切割/奇点拉力是新场地系统。**新 shader：是（rift-warp，与幽冥龙复用以摊销成本）。** MP：奇点拉力、裂隙分区需稳健同步与服务器侧权威，性能上奇点向心计算需限频。**建议在幽冥龙 rift-warp 验证后再开工。**

---

### 3.7 阴天子 Yin Emperor 「酆都审判者 / The Fengdu Judge」 — MEDIOCRE→ · P0 **[在重做中 / IN FLUX]**

> 本节**不依据当前代码**（正由其它 agent 实施 P0）。基线取 `BOSS_REDO_PLAN.md` §6.4 + §8 intended P0：**酆都三幕、冥律标记、SoulSeal 逃脱、阴/阳审判、G7 处决终结**；现有 `NetherDecreeMark`/`YinJudgmentPlayer`/`ArenaEdge`/`YinEmperorSky`/`GhostGateLock` 为该方向的占位骨架。

**1) Fight fantasy & identity.** T52 全地府终局——端坐酆都审判庭的冥帝；他不"打"你，他**判**你。整场是一场你被审、被记账、最终被处决或翻案的庭审。

**2) Phase/act narrative（在 intended 酆都三幕之上升格）.**
- **基线三幕（酆都三幕）：** 立庭（冥律标记起手）→ 阴阳审（阴/阳判决）→ 处决（G7 终结）。
- **v2 ELEVATION —— 把整场做成一条"罪→判→刑→赦"的庭审线，冥律标记是贯穿货币：**
  - **一幕·立庭：** 阴天子给玩家持续累积**冥律标记**（受击/站错区叠层），头顶可见层数。这是全场的"罪状条"。
  - **二幕·阴阳审（核心场地机制）：** 审判庭沿一条移动分界 `yin-yang-split` 分为**阴侧/阳侧**两套规则：阳侧惩罚静止（要动），阴侧惩罚移动（要静）；分界线左右扫动，强迫玩家随分界切换"动/静"姿态。判错（站错侧）→ 冥律 +1。
  - **三幕·处决与 SoulSeal 翻案（替换"无 HP 幕 + 随机 5 状态池"）：** 冥律满层触发**处决（G7 镇魂）**——玩家被 `SoulSeal` 定魂锁定、进入限时**镇魂狱**（`prison-overlay` 牢笼），必须在牢笼内完成"翻案"小目标（破符/打断）才能逃脱、清空冥律；失败则受处决级重击。这把"终局却无阶段"彻底改成"全程被审、定期受刑、可凭操作翻案"的终局仪式。

**3) Signature set-piece —— 《酆都审判·阴阳定罪 Fengdu Judgment》.** 二幕的阴阳分屏 + 三幕的镇魂狱处决。处决时全屏冻结一拍、`decree-vignette` 赤红压顶、阴天子抬手宣判、玩家落入 `prison-overlay` 符牢——这是全地府的终极高光，必须最具仪式感与镜头语言。**记忆点：在阴阳之间求生、在镇魂狱里翻案。**

**4) Telegraph & readability.** 冥律层数常驻（罪状条）；阴阳分界线是持续可见的移动 telegraph，分界扫来前用 §2 冷阴蓝预画；处决前给最长 90 tick 的 `decree-vignette` + 心跳音，**保证玩家有时间预判被判**；镇魂狱翻案目标高亮柔白。

**5) Presentation.**
- `yin-yang-split`（核心，与 BAW 共享）：审判庭阴阳分屏调色，左阴冷 / 右阳暖。
- `prison-overlay`（与尸骸共享）：镇魂狱符牢。
- `decree-vignette`：冥律满层/处决宣判。
- `soul-dissolve`：SoulSeal 定魂时玩家/Boss 的魂态化。
- `YinEmperorSky` 现有天幕系统 + 相机：宣判时定格/缓拉；shake 仅处决落定一击。
- 音乐：庭审主题（专属，音频），处决一拍静默 + 钟磬 stinger。

**6) Feasibility & cost.** **L（全地府最高）。** 依赖 P0 落地（**强串行依赖**），阴阳场地、镇魂狱翻案、冥律满层处决均为新系统；但占位骨架（NetherDecreeMark/YinJudgmentPlayer/ArenaEdge/Sky/GhostGateLock）已铺路。**新 shader：是（yin-yang-split + prison-overlay + decree-vignette，三者均与其它 Boss 共享）。** MP：阴阳侧判定、冥律层、镇魂狱状态需严格同步且服务器权威，全屏分屏 shader 单实例限定。**建议在 BAW（yin-yang-split）、尸骸（prison-overlay）验证各 shader 后收口实现。**

---

## 4. 着色器共享与复用矩阵 Shader Reuse Matrix

> 关键策略：**每个新 shader 都有 ≥2 个 Boss 共用**，先在低风险 Boss 验证、再上终局，摊销成本。

| Shader 概念 | 首发验证 Boss（低风险） | 复用 Boss | 备注 |
|-------------|------------------------|-----------|------|
| `grudge-desaturation` 怨念褪色 | 怨灵 Spectre | （全地府身份层可选叠加） | 与 §0.2 怨念账同生 |
| `soul-dissolve` 魂魄消融 | 黑白无常 BAW（复活/出场） | 怨灵、尸骸、妖狐、觉醒龙、阴天子 | 复用率最高，**最优先做** |
| `nether-fog-distortion` 冥雾扰动 | 幽冥龙（升级现有 FogSystem） | 妖狐、觉醒龙 | 升级既有雾系统而非从零 |
| `yin-yang-split` 阴阳分屏 | 黑白无常 BAW（协同） | 阴天子 | BAW 验证、阴天子收口 |
| `prison-overlay` 镇魂狱牢笼 | 尸骸 Corpses（引魂阵） | 阴天子（镇魂狱） | 尸骸验证、阴天子收口 |
| `rift-warp` 次元裂隙 | 幽冥龙（传送门） | 觉醒冥龙（裂隙/奇点） | 幽冥龙验证、觉醒龙收口 |
| `decree-vignette` 冥律晕影 | 尸骸（仪式完成） | 阴天子、觉醒龙 | 处决/满层专用 |

---

## 5. 每 Boss 优先级 / 工作量总表 Priority & Effort Table

| Boss | 上游评级·优先级 | v2 升格核心 | 标志性 set-piece | 推荐 shader（核心） | 工作量 | 需新 shader | MP/性能注意 |
|------|----------------|-------------|------------------|---------------------|--------|-------------|-------------|
| 怨灵 Spectre | MEDIOCRE · P1 | 怨念账贯穿全场 + 镜像清算终幕 | 《怨念清算》 | grudge-desaturation, soul-dissolve | **M** | 是（首做） | 怨念按对 Boss 总输出结算，简化同步 |
| 尸骸 Corpses | POOR · P1 | 接线 DarkRitual 为可攻防的献祭仪式 | 《引魂大阵》 | prison-overlay, soul-dissolve | **M+** | 是 | 手可打断状态 + 法阵进度需同步 |
| 幽冥龙 Nether Dragon | MEDIOCRE · P2 | 去常驻喷火 + 掘墓三段场地演化 | 《穿墓追猎》 | rift-warp, fog-distortion | **M** | 建议（可降级上线） | 矿脉实体同步 |
| 幽冥妖狐 Nether Kitsune | GOOD · P3 | 换占位弹 + P3 真假博弈 + 雾消费身份 | 《虚实九影》 | soul-dissolve, fog-distortion（复用） | **S–M** | 复用（零新增） | 真身标志同步；**勿改骨架** |
| 黑白无常 BAW | GOOD · P3 | 纯视觉升级 + 协同分屏放大 | 《阴阳勾魂》 | yin-yang-split, soul-dissolve | **S** | 共享（首发验证） | **勿动 AI**；分屏单实例 |
| 觉醒冥龙 Awakening Nether | POOR · P0 **[IN FLUX]** | P0 三幕之上"压缩空间"终局 | 《虚空吞噬》 | rift-warp, fog-distortion | **L** | 是（与龙复用） | 依赖 P0；奇点拉力限频 + 服务器权威 |
| 阴天子 Yin Emperor | MEDIOCRE · P0 **[IN FLUX]** | 罪→判→刑→赦 庭审线 + 冥律处决 | 《酆都审判·阴阳定罪》 | yin-yang-split, prison-overlay, decree-vignette | **L（最高）** | 是（三者共享） | 依赖 P0；阴阳/冥律/镇魂狱严格同步；分屏单实例 |

### 5.1 推荐实施次序（含 shader 验证链与依赖）

1. **先做共享 shader 验证（低风险）：** BAW（soul-dissolve + yin-yang-split）→ 尸骸（prison-overlay）→ 幽冥龙（rift-warp + fog-distortion）→ 怨灵（grudge-desaturation）。
2. **身份层基建并行：** `UnderworldField`（魂蚀 DoT + 冥律标记 + 怨念账）作为公共 ModPlayer/System 先落地，供全地府消费。
3. **终局收口（依赖 P0 + 上述 shader）：** 觉醒冥龙（用已验证 rift-warp）→ 阴天子（用已验证 yin-yang-split / prison-overlay / decree-vignette）。
4. **打磨补完：** 妖狐换弹 + P3 真假（可早做，确定性高）。

---

## 6. 交叉引用 Cross-References

| 主题 | 文档 · 章节 |
|------|-------------|
| 一次重做（反模式 / 设计原则 / 参考模板 / 地府线） | `BOSS_REDO_PLAN.md` §2、§3、§4、§6.4 |
| 着色器/VFX primitive 定义（**待建，回填命名**） | `BOSS_REDO_V2/00_SHADER_VFX_TOOLKIT.md` |
| 冥律标记 / 地府 DoT / 处决 / 吸血（数值依据） | `PROGRESSION_DESIGN_SPEC.md` §6.7、§7 |
| Boss 顺序 / Tier / 门控 / downed 标记 | `PROGRESSION_DESIGN_SPEC.md` §2.2、§3.1、§6.9 |
| 占位武器主题（换弹参考） | `PLACEHOLDER_CONTENT_REGISTRY.md` |

---

*Primordial / 洪荒 · Underworld Bosses Design v2 · 升格前请先确认 §0.2 身份层基建与 §4 shader 复用链；GOOD 级（BAW / 妖狐）仅升表现，禁止改坏骨架。*
