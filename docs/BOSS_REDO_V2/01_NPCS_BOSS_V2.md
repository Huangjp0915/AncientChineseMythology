# NPCs/Boss 二迭代战斗设计 V2（Designer-Grade Encounter Plan）

> **文档性质：** `NPCs/Boss/` 全 Boss 的**第二迭代**战斗设计规划（在 `BOSS_REDO_PLAN.md` 一迭代之上的"升格"层）
> **版本：** v2.0 · 2026-06-27
> **关系：** 一迭代（`docs/BOSS_REDO_PLAN.md`）已消除"低血量=加速喷弹幕"反模式、补齐阶段结构；本文**不重复**一迭代结论，只规定**如何把每场战斗升格为有作者意图、视觉震撼的遭遇战**。
> **配套文档：** `docs/BOSS_REDO_V2/00_SHADER_VFX_TOOLKIT.md`（着色器/VFX 原语工具箱）——本文撰写时该文档**可能尚未生成**；凡引用到具体 shader 原语处，统一标注"见 shader 工具箱"，未生成时按通用 shader 思路实现。

---

## 1. 概述 Overview

### 1.1 本迭代要解决什么

一迭代解决的是**"战斗是否公平、是否有规则变化"**；二迭代解决的是**"战斗是否令人难忘"**。两者评判维度不同：

| 维度 | 一迭代（v1） | 二迭代（v2，本文） |
|------|-------------|--------------------|
| 阶段 | 有脚本化幕，过阈值改规则 | 幕之间有**戏剧弧**（起→承→转→合），有作者化高潮 |
| 攻击 | 每招有 telegraph | telegraph 有**统一可读语言**（色彩/形状/音频），高强度下仍公平 |
| 主题 | 至少 1 个签名机制 | 签名机制升级为**签名 set-piece**（场地改造/脚本序列/交互机制） |
| 表现 | dust + 屏震 + 自定义弹 | **shader 滤镜 / 溶解 / 扭曲 / 图元拖尾 / 地面贴花 / 镜头语言** |

### 1.2 升格五准则 The Five Elevation Rules

每个 v2 设计都按以下五准则自检（与一迭代 §3 设计原则**叠加**，不替代）：

1. **每场战斗一句话身份（One-line Identity）。** 玩家事后能用一句话描述"那场打的是什么"。
2. **戏剧弧而非招式列表（Arc, not list）。** 入场建立威胁→升级制造压力→高潮 set-piece→收尾留印象。
3. **1–2 个作者化 set-piece（Authored set-piece）。** 不是又一组弹幕，而是场地变形/脚本演出/交互谜题级别的"记忆锚点"。
4. **可读性语言统一（Readable language）。** 同一种危险=同一种颜色/形状/声音；强度越高越要靠语言而非降速保证公平。
5. **表现服务于读，而非遮挡读（Presentation serves readability）。** shader/镜头永远不能盖住 telegraph；高潮处的全屏特效必须给"安全信息"让路。

### 1.3 本区 Boss 清单与处理基调

> 通过 `Glob NPCs/Boss/**` 枚举，本区共 10 个战斗实体（劫云三色合并计 1）。

| Boss | 一迭代评级 | v2 处理基调 |
|------|-----------|-------------|
| 赢勾 Yingou | GOOD | **仅打磨 + 视觉升格**，禁止改坏 FSM |
| 旱魃 Hanba | GOOD | **仅打磨 + 视觉升格**，把占位贴图/弹换成 shader |
| 后卿 Hoqing | POOR→已 P0 重写 | 在新 FSM 上叠 set-piece（疫源场地化 + shader） |
| 将臣 Jiangcen | POOR→已 P0 重写 | 在新 FSM 上叠 set-piece（雷狱场地化 + shader） |
| 祖龙残魂(地表) Archosaur | POOR | 结构升格为节奏化可选 Raid + 弱点 set-piece |
| 牛头马面 NiuMa | MEDIOCRE | 双 Boss 协同 set-piece + 镜头语言（已有 zoom 框架） |
| 九尾 Kyuubi | MEDIOCRE | 固定可读 pattern + 狐火 shader + 幻影 set-piece |
| 苍龙真身 Azure Dragon | MEDIOCRE | P3 雷暴审判 set-piece + 换原版占位弹 |
| 劫云 ×3 Tribulation | MEDIOCRE | 合并参数化 + 渡劫仪式感升格（纯演出向） |
| 黑熊精 BlackBear | POOR | 接 Attack_3 + 肉前小高潮（新手"阶段感"样板） |

---

## 2. 全区共享：可读性语言与表现公约 Shared Language & Presentation Contract

> 二迭代的统一性来自**共享视觉词汇**。所有 NPCs 区 Boss 复用以下约定，玩家跨 Boss 学习一次、终身受用。

### 2.1 telegraph 颜色语义 Telegraph Color Semantics

| 危险类型 | 颜色 | 形状语言 | 音频前摇 |
|----------|------|----------|----------|
| **落点/范围爆发**（AoE 落地） | 暖红 → 白闪 | 收束环（外圈缩进内圈即爆） | 低频 *whump*（`Item104`/`DD2`） |
| **线性命中**（冲撞/光柱/链） | 该 Boss 主题色 | 沿线尘线/细光带（先铺线后命中） | 拉弦/蓄力 *whirr*（`Item74`/`Item71`） |
| **持续/站位危险**（毒池/边界雷/领域） | 主题色低饱和 + 脉动 | 软发光底圈（呼吸式脉动） | 循环嗡鸣（低音量 loop） |
| **追踪/凝视**（预判玩家） | 紫白 | 注视线 / 锁定标记 | 单点 *ping* |

> 现状已部分落地：将臣 `JiangcenTelegraphMark`（落点环用暖色、雷用蓝色）、九尾 `DrawTelegraph` 预判线、苍龙蓄力粒子汇聚。v2 要把这些**统一到上表**，避免每 Boss 自创一套。

### 2.2 表现原语映射 Presentation Primitive Map（引用 shader 工具箱）

| 原语（见 shader 工具箱） | 用途 | 本区使用者（示例） |
|--------------------------|------|--------------------|
| **入场滤镜 Intro Filter**（短暂去饱和/暗角/径向模糊） | 入场 1.5s 聚焦 Boss | 全部 Boss 入场 |
| **溶解 Dissolve**（噪声阈值消融贴图） | 召唤/瞬移/死亡/分身现形 | 后卿仆从、九尾幻影、将臣镜像锤魂、Boss 死亡 |
| **扭曲 Distortion**（屏幕空间热浪/冲击波环） | 冲撞/落地/爆发冲击 | 牛头冲撞、黑熊砸地、苍龙俯冲、将臣落地 |
| **图元拖尾 Primitive Trail**（顶点带 + 渐变 shader） | 高速运动体的彩带拖尾 | 九尾本体/尾、苍龙龙身、赢勾大刀、将臣雷锤 |
| **地面贴花 Arena Decal**（投影到场地的法阵/裂纹/池） | 场地改造 set-piece | 将臣雷狱裂纹、后卿疫源、苍龙落雷区、黑熊裂地 |
| **光柱/激光 Beam**（带流动 UV 与端点灼烧） | 审判/凝视/吐息 | 苍龙雷暴审判、旱魃已用（升格复用） |
| **天空着色 Sky Tint/Shader** | 氛围与距离反馈 | 各 Boss 已有 CustomSky，升格为 shader 天幕 |

### 2.3 镜头与屏震公约 Camera & Shake Contract

- 已有基础设施：`ScreenShakePlayer.ShakeScreen(scale, time)`（多数 Boss 用）、`NiuMaPlayer` 的 `SetScreenPos/SetZoom/SetScreenShake`（牛马专用，**可作为全区镜头框架的样板**）。
- v2 公约：
  - **入场**：轻推拉近（zoom ~1.1–1.4）+ 短滤镜，≤2s。
  - **过阶段**：一次重屏震（scale 12–20）+ 短 i 帧（已普遍实现）+ 可选 0.3s 慢镜。
  - **高潮 set-piece**：镜头让位给"安全信息"——禁止在需要精确走位的瞬间做强烈抖动；把抖动放在**爆发的命中帧**而非**预告帧**。
  - **死亡**：拉近 + 溶解 + 降速收尾（见各 Boss 收尾）。

---

## 3. 参考基线：GOOD 模板的"好"在哪 Reference Baselines

> 升格其他 Boss 时直接对标这两个本区 GOOD。

- **赢勾 Yingou（`NPCs/Boss/Yingous/Yingou.cs`）**：phase enum + telegraph 子状态 + 双手 `CommandBothHands` 编排 + 侵略性动作系统（近/中/远分层）。**好在"动作密度可控且可读"**。v2 对其他 Boss 的"攻击编排"直接抄它的分层节奏。
- **旱魃 Hanba（`NPCs/Boss/Hanbas/Hanba.cs`）**：8 状态循环 + 符箓可破除（`Talisman` 决定阶段推进）+ 鬼域牢笼（`CarftRestriction` 限制场地）+ 多段蝗虫过境脚本幕 + 自定义流动 UV 激光（`HanbaLaser`）。**好在"目标/可破除 + 空间控制 + 已有真 shader 级激光"**。v2 的"场地改造 set-piece"全部对标它的牢笼与激光。

---

## 4. 逐 Boss V2 设计 Per-Boss V2 Design

> 每节固定六小节：① Fantasy & Identity ② Act Narrative（升格点） ③ Signature Set-piece ④ Telegraph & Readability ⑤ Presentation ⑥ Feasibility & Cost。
> "升格点"只写**相对一迭代新增的内容**，不复述一迭代已有结构。

---

### 4.1 后卿 Hoqing —— 瘟疫亡神 Plague God（POOR→已重写，v2 叠加）

> 代码现状：`Hoqing.cs` 已是三幕脚本 FSM（幽火列阵 / 疫疠扩散 / 万鬼夜行）+ 专属地府 BGM + 衰朽叠层 + 尸坑/脓潭/疫风/祭坛蓄力。v2 在此之上做**身份强化**。

**① Fantasy & Identity**
"我把整个战场变成一片会蔓延的尸疫沼泽，你越站越没有干净的立足之地。"——一场**关于场地被逐步污染、玩家不断被逼迁**的战斗。

**② Act Narrative（升格点）**
- 一迭代三幕是"换攻击族"；v2 让三幕共享**一条上升曲线：场地洁净度**。
  - 幕一（幽火列阵）：场地零污染，尸坑只是点状预告——建立"地面会变危险"的认知。
  - 幕二（疫疠扩散）：脓雨潭（`SputumPool`）落点**残留为持久疫斑贴花**（不只是池子计时消失），疫斑随幕推进累积，可站区域缓慢缩小——制造压力。
  - 幕三（万鬼夜行）：四祭坛蓄力时，**祭坛之间连成"疫气经络"**，未被净化的疫斑此时全部激活为高潮压迫；玩家必须用幕二留下的"净化窗口"清出一条活路。
- **新增可破除机制（呼应旱魃符箓）**：疫源（`CorpsePit`/疫斑）可被玩家攻击"净化"，净化时回吐少量安全区——给玩家**主动管理场地**的目标，而非纯躲。

**③ Signature Set-piece**
- **"万鬼夜行"经络高潮（幕三）**：四祭坛 = 四个 `Arena Decal` 法阵；蓄力满时，祭坛间生成**沿经络流动的尸火洪流光带**（图元拖尾 + 溶解），洪流之间留可读缝隙。这是全场记忆点：场地从"零散危险"升级为"成型的鬼域阵"。
- **次 set-piece**：尸链复生（`HoqingCorpseChain`）命中地面疫斑时**优先在疫斑处复活仆从**，把"场地污染"与"召唤"逻辑绑定，强化主题闭环。

**④ Telegraph & Readability**
- 统一到 §2.1：疫斑=尸绿低饱和呼吸圈（持续危险）；祭坛扇形/360 释放沿用现有红橙(扇)/绿(360)辉光区分（已实现，保留）。
- 高潮经络洪流必须**先以暗色经络线预告 ~40t**，再点亮为伤害带。

**⑤ Presentation**
- **天幕**：`HoqingSky` 升格为 shader 天幕——尸绿瘴雾 + 距离越近越浑浊（对标 `HanbaSky` 的距离-色彩 lerp）。
- **疫斑贴花**：`Arena Decal` 投影绿斑，边缘用噪声 `Dissolve` 让蔓延/净化有"生长/枯萎"动画。
- **仆从现形/复活**：`Dissolve` 由尸火噪声聚成形。
- **入场**：`Intro Filter` 去饱和 + 尸绿暗角，配现有 `Roar` + 屏震。
- **死亡**：本体 `Dissolve` 成尸火飘散 + 全场疫斑同时枯萎褪色（"瘟疫随主人消散"）。
- 音乐：保留专属地府曲；高潮幕可叠一层低频心跳/钟声 SFX 提示"万鬼夜行"开启。

**⑥ Feasibility & Cost**
- 规模 **M**。新 shader：疫斑贴花溶解（可与将臣/苍龙共用一个通用 `Arena Decal + Dissolve` shader）。
- 复用：现有 FSM、`HoqingSky`、衰朽 buff、祭坛蓄力逻辑全部保留。
- 性能/MP：疫斑用**单张全屏贴花 RT 累加**而非大量 dust/projectile；净化状态走 `SyncVar`/extraAI 同步避免不一致。

---

### 4.2 将臣 Jiangcen —— 雷狱僵将 Thunder-Prison General（POOR→已重写，v2 叠加）

> 代码现状：`Jiangcen.cs` 已有阶段枚举 + 六柄功能化环绕重锤（蓄力变红→径向猛砸）+ 5 招轮转 + 50% 进入「雷狱」场地阶段（边界雷霆 + 锚点链式闪电）+ 全套 `JiangcenTelegraphMark` 预告。v2 强化**雷狱场地的存在感**与**重锤的戏剧性**。

**① Fantasy & Identity**
"半血时他把整片天地封进一座雷牢，逃出边界就被劈，六柄环锤是随时会砸下的达摩克利斯之剑。"——一场**场地封闭 + 头顶威胁**的压迫战。

**② Act Narrative（升格点）**
- 一迭代雷狱是"边界雷 + 链电"；v2 把雷狱升格为**真正可见的封闭场地**：
  - 进入雷狱（`RunTransition`）时，边界从"裂纹尘"升级为**环形雷墙贴花**（`Arena Decal`），玩家第一眼就看清"牢笼有多大"。
  - 六柄环锤在雷狱中**改为协同蓄力**：不再逐柄触发，而是"两两对位"同时砸，形成可读的对穿走廊——把装饰锤变成场地分割工具。
- 幕收尾（雷狱后段）加一个**"将令总攻"**短脚本：所有存活机制（边界雷 + 链电 + 镜像锤魂）同时收束一次，作为该 Boss 的情绪顶点。

**③ Signature Set-piece**
- **雷牢降临（Thunder-Prison Descends）**：过半血演出时，镜头短暂拉远看清雷墙合拢，天幕转为雷暴，`Distortion` 冲击波从 Boss 扩散一圈——一秒钟内确立"规则变了"。
- **镜像锤魂（已实现 `JiangcenHammerGhost`）升格为 set-piece**：镜像玩家走位 → 突袭。v2 给镜像体 `Dissolve` 现形 + 与本体异色（紫红）以区分"这是你的影子"，强化"和自己走位对抗"的独特体验。

**④ Telegraph & Readability**
- 已有 `JiangcenTelegraphMark` 四样式（落点/尸坟/锚点/边界），统一到 §2.1 配色即可（雷=蓝、砸=暖红、边界=低饱和脉动）。
- 雷牢边界必须**常驻可见**（持续危险语言），而非仅在触发时闪现——让"别贴墙"成为肌肉记忆。
- 重锤蓄力已有"变红+闪烁"（`JiangcenHammer.PreDraw`），保留；径向猛砸前补一条**径向预告线**让对穿走廊可读。

**⑤ Presentation**
- **天幕**：`JiangcenSky` 已有距离-色彩 + 脉动红，升格为 shader 天幕叠雷闪频闪。
- **雷墙/链电**：`Beam` + `Arena Decal`；链式闪电（`JiangcenChainArc`）用流动 UV 的 `LightningBranch`（已用该贴图）升格为发光 beam。
- **环锤拖尾**：`Primitive Trail` 替代当前残影 draw，猛砸时拖尾变红。
- **入场**：`Intro Filter` + 现有 `Shadowflame` 暗红聚拢。
- **死亡**：雷牢崩解（雷墙贴花碎裂 `Dissolve`）+ 六锤坠地。

**⑥ Feasibility & Cost**
- 规模 **M**。新 shader：雷墙环贴花（与通用 `Arena Decal` 共用）、链电 beam 流动 UV（可与旱魃激光 shader 同族）。
- 复用：整套 FSM、重锤、预告、镜像锤魂、`JiangcenSky` 保留。
- 性能/MP：雷牢边界 dust 当前每帧生成，建议改为 shader 贴花一次绘制省 dust；镜像/锚点已走 extraAI 同步。

---

### 4.3 祖龙残魂（地表）Archosaur —— 残破雷龙 Broken Thunder-Wyrm（POOR · P1）

> 代码现状：`ArchosaurBoss.cs` 是 `BasicWorm` 蠕虫，8 字 Lissajous 走位 + 雷球 RNG 散射 + **无敌分身噱头**（分身存活则本体 `dontTakeDamage`）+ **Phase1 自残**（`SelfDamage`）。占位伤害 999、原版无主题弹 `ThunderOrb`（自定义但通用）。这是**最需要结构升格**的一个。

**① Fantasy & Identity**
"一条已经残破、靠分裂残魂续命的远古雷龙——你得先打碎它的'替身残魂'，才能在短暂窗口里真正伤到本体。"——一场**HM 可选 Raid，强调'破替身→抓窗口'的爽快节奏**。

**② Act Narrative（升格点）**
- **删除无敌噱头与自残**（一迭代已定）；v2 给出替代弧：
  - 常态：本体高速盘绕（保留蠕虫机动的爽感），周期性甩出**主题化雷弹幕走廊**（非纯 RNG，改成可读的盘绕轨迹同步弹）。
  - 半血：分裂出**1 条"残魂分身"蠕虫**——但它**不是无敌护盾，而是弱点钥匙**：打掉残魂会让本体进入数秒**破绽窗口**（`dontTakeDamage=false` + 受伤加成 + 减速），错过窗口残魂重新凝聚。把"无敌分身"反模式**反转为节奏机制**。
- 作为可选 Raid，不追求终局复杂度，追求"盘龙 + 破替身 + 爆发输出"的循环爽快。

**③ Signature Set-piece**
- **残魂分裂（Soul Split）**：半血时本体 `Dissolve` 一分为二，分身以异色（残破灰蓝）现形并开始与本体**对称盘绕**，形成双龙交织的视觉奇观——这是该 Raid 的记忆点。
- **破绽窗口（Vulnerability Window）**：打碎残魂时，本体**坠速、龙身发光、天幕骤亮**，给玩家"现在全力输出"的明确信号。

**④ Telegraph & Readability**
- 当前雷球 ±35° 纯随机 → 改为**沿盘绕切线的可读扇形**（保留密度，去掉不可预测）。
- 残魂"是弱点还是威胁"必须一眼可辨：残魂用**统一的紫白凝视/锁定色**，本体破绽窗口用**金白高亮**。

**⑤ Presentation**
- **龙身拖尾**：`Primitive Trail` 沿蠕虫节段渲染雷光带（替代逐节贴图叠加的朴素 draw）。
- **天幕**：补一个简易 shader 雷暴天幕（Archosaur 当前无 CustomSky）；破绽窗口时天幕骤亮。
- **分裂/重凝**：`Dissolve`。
- **入场/死亡**：复用已有自定义 `ArchosaurSummon`/`ArchosaurDeath` 音效 + 入场滤镜；死亡时双龙同时溶解。
- 镜头：破绽窗口给一次轻微拉近强调"输出时机"。

**⑥ Feasibility & Cost**
- 规模 **L**（结构性重写：去蠕虫纯追逐、加破绽窗口状态机、改弹幕、加分身钥匙逻辑、加天幕）。
- 新 shader：龙身图元拖尾、简易雷暴天幕（可复用苍龙/将臣的雷暴天幕族）。
- 性能/MP：蠕虫 + 分身蠕虫的双 worm 同步需谨慎（`realLife`/分身索引已有基础，注意分身被击破的同步）。

---

### 4.4 牛头马面 NiuMa —— 鬼差搭档 Hell-Wardens Duo（MEDIOCRE · P2）

> 代码现状：`NiuMa_NPC.cs` 双 Boss（牛头近战冲撞/锁链牵引/凝视眼束；马面齐射/减速领域/爆裂），已有**互相复活**（一方死、另一方>30% 血则满状态复活）、**双半血协同阶段**（牛头 `Ai_3`、马面 `Ai3_Synergy`）、**镜头框架**（`NiuMaPlayer` zoom/screenpos/shake）、双半血时玩家受伤 +30%。底子相当好，主要缺**协同的"作者化演出"**与**统一可读性**。

**① Fantasy & Identity**
"两个鬼差一个抡链锁你、一个拉魂减速你——当两个都见血，他们会合体演一出'勾魂锁命'的双人连携。"——一场**双 Boss 分工 + 协同连携**的战斗。

**② Act Narrative（升格点）**
- 一迭代已去半血加速、有协同阶段；v2 把"协同"从"各自更快"升格为**真正的配合连携**：
  - 牛头锁链（`ChainProj`）**牵引玩家**的同时，马面在牵引落点**预布减速领域/齐射**——"锁住你→拉过去→正好踩进马面的网"。这要求两体协同阶段**互相读取对方位置/状态**编排，而非各打各的。
  - 复活演出（`ai[3]==-2`）升格为情绪节点：被复活方从同伴身上 `Dissolve` 重生，配镜头拉近——把"互相复活"这个已有亮点放大成 set-piece。

**③ Signature Set-piece**
- **勾魂锁命连携（Chain-and-Reap Combo）**：双半血协同时的招牌——牛头链刃锁定 + 马面灵魂牵引同向叠加，场地中央形成"被拉向死亡漩涡"的合力，玩家需逆向走位挣脱。利用已有 `SetZoom(2.2f)` 拉近强化压迫。
- **同伴复生（Partner Revival）**：已有逻辑，v2 加 `Dissolve` 现形 + 镜头语言，使其成为可被识别的"翻盘时刻"。

**④ Telegraph & Readability**
- 当前两体用 dust 颜色区分（牛=暗红、马=紫），v2 固化为**身份色**：牛头一切预告暖红、马面一切预告紫——跨招式一致。
- 锁链/牵引必须有清晰"被拉方向"的可读箭头/光带；减速领域用持续危险脉动圈（已有 dust 圈，升格为贴花）。

**⑤ Presentation**
- **拖尾**：两体已有 `Draw_Tail`（红/紫尾焰），升格为 `Primitive Trail`。
- **领域/牵引**：马面减速领域（`Ai_Const_0`）升格为 `Arena Decal` 脉动圈；锁链升格为带流动 UV 的 beam。
- **镜头**：复用 `NiuMaPlayer` 框架（这是全区最完整的镜头系统）——协同/复活/爆裂各给一个 zoom 节拍。
- **入场**：已有钻石 dust 升空演出，叠 `Intro Filter`。
- **天幕**：NiuMa 当前无 CustomSky，建议补一层地府阴森 shader 天幕（可与后卿/地府线共用）。

**⑥ Feasibility & Cost**
- 规模 **M**（协同编排重构 + 视觉升格；镜头系统已现成，省力）。
- 新 shader：领域贴花、锁链 beam（共用件）、地府天幕（共用件）。
- 性能/MP：双 Boss + 互读状态，注意协同阶段的状态同步（现以 `ai[3]` 驱动，扩展需谨慎 netAlways 已开）。

---

### 4.5 九尾 Kyuubi —— 妖狐九尾 Nine-Tailed Fox（MEDIOCRE · P2）

> 代码现状：`KyuubiKitsune.cs` 尾巴系统丰富（9 尾、多 pattern、远距刺击带预判线 `DrawTelegraph`）、有 Intro/相变/幻影/瞬移/冲刺。**两大病灶**：① 二阶段大量 `Main.rand.Next` 轮盘选招（`RunPhase2Chase` 随机转移、`ExecutePhase2TailAttack` 随机）；② **原版占位弹**（`CultistBossFireBall`/`Clone`/`Arc`）。

**① Fantasy & Identity**
"九条妖尾各自为戈，它瞬移、留影、九方向同刺——你要读的不是它本体，而是九条尾巴的合奏。"——一场**尾巴编排 + 狐火幻影**的战斗（对标地府冥狐的尾编排）。

**② Act Narrative（升格点）**
- 一迭代要求"固定可读 pattern"；v2 给出编排弧：
  - 一阶段：尾巴 pattern 从"随机切换"改为**固定循环序列**（顺序刺→齐刺→九方向→波浪），让玩家学会"这套之后是哪套"。
  - 二阶段：把随机 4 选 1（冲刺/瞬移/幻影/九刺）改为**有逻辑的连段**——例如"瞬移→幻影分身→九方向同刺"组成一套招牌连招，而非掷骰子。
- 相变演出已有（尾巴收束 + `ForceRoar`），v2 加狐火变色（金→赤）标记"妖力解放"。

**③ Signature Set-piece**
- **狐影九重（Nine Phantoms）**：升格现有幻影机制（`RunPhase2Illusion`）——本体与幻影同时摆出九方向同刺，玩家须分辨真身（真身尾巴有 `Primitive Trail` 实焰，幻影为半透溶解）。把"幻影"从视觉装饰升格为**辨真伪的交互谜题**。
- **九方向同刺（Nine-Direction Stab）**：已是亮点，v2 给每条尾尖加狐火 beam 拖影，命中线更醒目，作为该 Boss 的标志攻击。

**④ Telegraph & Readability**
- 已有远距刺击预判线 `DrawTelegraph`，统一到 §2.1（线性=主题色细光带）。
- 幻影 vs 真身的可读性是核心：真身=实色+实焰拖尾；幻影=青/半透+溶解边缘（现有 `DrawIllusions` 已 lerp 到 Cyan，方向正确，升格为 shader 溶解）。

**⑤ Presentation**
- **换占位弹（硬性）**：`CultistBossFireBall*` → 自定义**狐火弹**（金赤渐变 + soft glow + 轻拖尾）。这是一迭代 §2.3 的硬要求。
- **本体/尾拖尾**：`Primitive Trail`（现为 `DrawTrail` 朴素叠贴图）。
- **幻影**：`Dissolve` 现形/消散。
- **天幕**：当前用 `MusicID.Boss4` + 无专属天幕；建议补妖异狐火天幕（暖金夜色）+ 专属/合适 BGM（现为原版 Boss4，属占位）。
- **入场/相变**：`Intro Filter` + 相变金赤换色。

**⑥ Feasibility & Cost**
- 规模 **M**（去随机化选招 + 换 4 类占位弹 + 幻影 shader + 拖尾 + 天幕/BGM）。
- 新 shader：狐火弹 glow、幻影溶解、图元拖尾（共用件）。
- 性能/MP：9 尾 + 幻影 + 弹幕，注意尾巴是**纯客户端逻辑对象**（`KyuubiTail` 非 NPC），换弹要确保服务器生成、视觉客户端化。

---

### 4.6 苍龙真身 Azure Dragon —— 雷霆苍龙 Azure Thunder-Dragon（MEDIOCRE · P1）

> 代码现状：`AzureDragonAI.cs` 三阶段 partial（出海/震怒/天威），结构清晰、招式多。**两大病灶**：① 满屏使用**原版占位弹** `CultistBossLightningOrb*`（吐息/雷球/矩阵/审判/俯冲全是）；② P3「雷霆审判」是**随机落雷 + 追踪弹倾泻**，接近一迭代点名的"加速喷射"味道（虽有阶段但 P3 缺真正的场地规则）。

**① Fantasy & Identity**
"G7 终局真身——它不再只是吐弹，而是把整片天空变成它的审判庭，雷区一格格点亮，你在落雷的缝隙里舞蹈。"——一场**雷暴控场 + 走位审判**的终局压迫战。

**② Act Narrative（升格点）**
- P1/P2 基本保留（盘旋/吐息/雷球/冲刺/风暴/矩阵/旋风/连冲已够丰富）；**核心升格在 P3**：
  - 把「雷霆审判」从"随机落雷洪流"改为**网格化落雷审判**：场地划为可读网格（`Arena Decal`），雷按**可预告的序列**逐格点亮落下（横扫/棋盘/收束等图案），玩家走安全格——这是"规则"而非"密度"。
  - 「风域」机制兑现一迭代承诺：P3 引入**风场推拉**（周期性把玩家朝危险格推），让走位与雷区联动。
- 配合 SPEC §5.7 青龙→苍龙觉醒剧情：P3 入场做一次"真身降临"演出强化终局感。

**③ Signature Set-piece**
- **雷霆审判庭（Thunder Tribunal）**：P3 招牌——全场网格雷阵 + 风域，雷格按图案波次点亮，玩家在缝隙走位。把当前"喷射味"P3 彻底替换为**编舞式落雷**。
- **真身降临（True Form Descent）**：P3 转场（`RunPhaseTransition3` 已有强演出）升格为镜头拉远 + 天幕雷暴蔽日 + `Distortion` 冲击波。

**④ Telegraph & Readability**
- 落雷=暖红→白闪收束格 + 低频蓄力音（§2.1 AoE 语言）；风域=方向性蓝箭头光带。
- 网格安全/危险格必须高对比，雷下前 ~45t 预告。

**⑤ Presentation**
- **换占位弹（硬性）**：所有 `CultistBossLightningOrb*` → 自定义**雷弹/风弹**（青蓝 + 电弧 + 拖尾）。一迭代 §2.3 硬要求。
- **龙身拖尾**：`Primitive Trail` 青雷带。
- **天幕**：`AzureDragon` 已有 Draw 类，升格 shader 雷暴天幕（频闪 + 蔽日）；落雷与天幕闪同步。
- **审判网格**：`Arena Decal` + `Beam`（落雷柱）。
- 镜头：P3 降临拉远，审判波次的"命中帧"给屏震（预告帧不抖）。

**⑥ Feasibility & Cost**
- 规模 **L**（P3 重做为网格审判 + 风域 + 全量换弹 + 天幕/拖尾 shader；P1/P2 仅换弹与拖尾）。
- 新 shader：雷暴天幕、审判网格贴花、落雷 beam、雷弹 glow（多为共用件）。
- 性能/MP：网格落雷弹数量需限幅；落雷序列须服务器决定 + 同步（避免各客户端图案不一致）。

---

### 4.7 劫云 ×3 Tribulation —— 天劫 Heavenly Tribulation（MEDIOCRE · P3）

> 代码现状：`TribulationCloud{Black,Red,Purple}.cs` 三份近乎复制粘贴；纯生存事件（悬浮跟随 + 定时落雷 N 次，撑过=突破，玩家死=境界跌）。`Black` 用 `MusicID.Boss3`、原版无主题落雷弹。这是**事件而非战斗**，v2 走"仪式感升格 + 代码合并"，不强行做成弹幕 Boss。

**① Fantasy & Identity**
"不是一场战斗，而是一段你独自承受天威、咬牙撑过去的渡劫仪式。"——一场**生存仪式 + 渐强压迫**的演出向遭遇。

**② Act Narrative（升格点）**
- 一迭代要求合并三份为参数化逻辑（颜色/强度/雷击次数作参数）；v2 在此基础加**仪式弧**：
  - 落雷不是匀速 18 次，而是**三波渐强**（试探→紧逼→终雷），波间留喘息，终雷一记最重并伴最强演出——把"数雷"变成"有节奏的考验"。
  - 成功/失败结算（已有 `SuccessTribulation`/`FailTribulation`）升格为有重量的视听节点（成功：金光冲天；失败：境界跌的灰暗收尾）。

**③ Signature Set-piece**
- **终雷（Final Bolt）**：最后一记雷给最强的预告 + 全屏 `Distortion` + 镜头拉近 + 天幕刹白——渡劫的高潮就在"撑过这一下"。
- 三色（黑/赤/紫）通过**参数化天幕色 + 雷色**区分境界，而非三套代码。

**④ Telegraph & Readability**
- 每记落雷严格遵守 §2.1 线性/落点语言 + 蓄力音；渐强波次用雷色亮度递增表达"越来越狠"。

**⑤ Presentation**
- **天幕**：统一参数化 shader 劫云天幕（乌云压顶 + 渐暗 + 雷闪），颜色按三色传参。
- **落雷**：换原版弹为自定义雷柱 beam（与苍龙/将臣雷族共用）。
- **结算**：成功金光（shader 径向光） / 失败去饱和暗角。

**⑥ Feasibility & Cost**
- 规模 **S–M**（主要是三合一参数化 + 演出包装，玩法本身轻）。
- 新 shader：劫云天幕（参数化）、雷柱 beam（共用件）。
- 注意：配合 SPEC §3.2 修复伤害公式（一迭代已点名 XOR→加法）；`DoLightningStrike` 的伤害公式三份要统一进参数化基类。

---

### 4.8 黑熊精 BlackBear —— 早期试炼熊 Tutorial Bruiser（POOR · P3）

> 代码现状：`BlackBear.cs` 是地面物理近战 Boss（追/跳/Attack_1 近战击退 + Attack_2 投射 + 接触伤害），早期低血量（8888）。**病灶**：① `Attack_3`（`BlackBear_Proj3`）**写了但只在半血时一次性发射头饰弹**，并非攻击循环的一环；② 半血只是切贴图（`_1` 版本）+ 发一次头饰，属"装饰性狂暴"；③ 大量手写跳跃/平台逻辑较脆。作为**最早期 Boss**，v2 目标是当好"阶段感教学样板"。

**① Fantasy & Identity**
"新手遇到的第一头会'换姿态'的猛兽——半血时它真的变凶了，而不是换张图。"——一场**教玩家'Boss 会进入新阶段'的入门战**。

**② Act Narrative（升格点）**
- 一迭代要求接入 `Attack_3` 形成 3 攻击循环 + 半血给"肉前小高潮"；v2 把它做成**最干净的阶段感样板**：
  - 前半：追击 + 近战击退（Attack_1）+ 扑击投射（Attack_2），节奏慢、好读，教玩家"贴近危险、拉开安全"。
  - 半血小高潮：一次**短演出**（咆哮 + 屏震 + 裂地）后进入"狂怒姿态"——新增 `Attack_3` 接入循环（如**滚石冲撞**或**召唤幼熊**），并让攻击循环**节奏加快但每招仍有清晰前摇**。重点是让新手第一次体会"阶段转换=新东西"。

**③ Signature Set-piece**
- **狂怒裂地（Enrage Stomp）**：半血演出——黑熊砸地，`Distortion` 冲击波环 + `Arena Decal` 裂纹 + 屏震，简单但强烈，作为新手的第一个"阶段高潮"印象。
- 新增 `Attack_3` 的"滚石冲撞"作为狂怒后的招牌新招（地面横向碾压，留跳跃窗口）。

**④ Telegraph & Readability**
- 作为入门 Boss，telegraph 要**最夸张最慢**：近战前的明显抬手、冲撞前的明显蓄力下蹲（现有 dust 蓄势可升格）。
- 半血"变凶"用**统一狂怒色**（暖橙发光描边，对标已有 `_1` 贴图思路）让新手一眼看出"它进阶了"。

**⑤ Presentation**
- **入场/半血**：`Intro Filter` + 半血一次 `Distortion` 裂地冲击。
- **裂地贴花**：`Arena Decal`（与共用件）。
- 已有 `PunchCameraModifier` 屏震，保留；狂怒态加发光描边（轻量 shader 或纯叠色）。
- 天幕：早期 Boss 可不做专属天幕，保持轻量；最多日间染一层尘黄。

**⑥ Feasibility & Cost**
- 规模 **S–M**（接 `Attack_3`、加半血演出与一招、telegraph 夸张化；不需重写物理）。
- 新 shader：裂地贴花（共用件）、可选狂怒描边。
- 性能/MP：早期 Boss，控制弹量；现有手写跳跃逻辑建议顺手做防卡墙加固（非必须）。

---

### 4.9 赢勾 Yingou —— 参考 GOOD（仅打磨 + 视觉升格）

> 禁止改坏其 FSM / 双手编排 / 侵略性动作系统。

**① Fantasy & Identity（既有）**："双刀环绕的刀阵杀手"——大刀地狱 + 螺旋压迫 + 连斩冲刺。保持。

**② / ③ 升格点（仅锦上添花）**
- 不动玩法结构；把 `SaberHell` 多图案高潮升格为**视觉招牌**：大刀（`SaberHell` 弹）加 `Primitive Trail` 刀光、收束/旋转图案配 `Arena Decal` 刀痕残留，让"刀地狱"名副其实。
- `BladeScatter` 蓄力已有 `SoftGlow` 多层发光（`PreDraw`），升格为 shader 充能光环。

**④ Telegraph**：已优秀（telegraph 子状态 + 充能粒子）。仅统一配色到 §2.1。

**⑤ Presentation**
- 大刀 `Primitive Trail`；`YingouSky` 升格 shader 天幕；入场已有扭曲漂移（`introAppear`）叠 `Intro Filter`。
- 死亡：刀阵溶解收束。

**⑥ Feasibility & Cost**：规模 **S**（纯视觉，零玩法风险）。新 shader：刀光拖尾、充能环（共用件）。

---

### 4.10 旱魃 Hanba —— 参考 GOOD（仅打磨 + 视觉升格）

> 禁止改坏其 8 状态循环 / 符箓破除 / 鬼域牢笼 / 蝗虫脚本幕。

**① Fantasy & Identity（既有）**："旱灾武神"——符箓护体、鬼域三层牢笼、蝗虫过境、黄金太阳柱激光。保持。

**② / ③ 升格点（仅锦上添花）**
- `HanbaLaser`/`HanbaBigLaser` 已是**本区最接近 shader 级**的流动 UV 激光（焦紫→血红→烈焰黄渐变）——v2 把它**抽为可复用 beam 原语**供苍龙/将臣/劫云的雷柱复用（反向贡献工具箱）。
- 占位修整：`HanbaFireBall`/`LocustSet` 用 `InnoVault/Assets/placeholder` 贴图，升格为正式贴图 + glow；牢笼（`CarftRestriction`）的蝗虫墙可叠 `Arena Decal` 边界提示让"牢笼范围"更可读。

**④ Telegraph**：已优秀（眼睛蓄力 dust、`Shockwave` 预告）。统一配色到 §2.1（旱魃=焦橙/血红身份色）。

**⑤ Presentation**
- `HanbaSky` 已是距离-色彩 shader 思路天幕，保留；占位弹换正式贴图 + glow；牢笼边界贴花。
- 死亡：已有 gore，叠太阳柱溶解收束。

**⑥ Feasibility & Cost**：规模 **S**（占位贴图替换 + 激光抽原语 + 牢笼边界贴花）。**反向收益**：其激光是工具箱 beam 原语的现成参考实现。

---

## 5. 新增/共享着色器需求汇总 Shaders to Author

> 本区 set-piece 反复依赖少数几个**可复用** shader 原语。建议在 `00_SHADER_VFX_TOOLKIT.md` 统一实现，避免每 Boss 各写一份。

| Shader 原语 | 优先级 | 本区主要使用者 | 备注 |
|-------------|--------|----------------|------|
| **Arena Decal + Dissolve**（场地法阵/裂纹/毒池，噪声溶解生长） | **高** | 后卿疫斑、将臣雷牢、苍龙审判格、牛马领域、黑熊裂地 | 全区最高复用，先做 |
| **Beam（流动 UV + 端点灼烧）** | **高** | 苍龙落雷柱、将臣链电、劫云终雷、牛马锁链 | **旱魃 `HanbaLaser` 已有现成参考实现**，抽象即可 |
| **Primitive Trail（顶点带渐变）** | **高** | 九尾本体/尾、苍龙/Archosaur 龙身、赢勾刀光、将臣锤、牛马尾焰 | 替换大量"逐帧叠贴图"残影 |
| **Dissolve（噪声阈值消融）** | 中 | 召唤/瞬移/分身现形/死亡（几乎所有 Boss 收尾） | 与 Arena Decal 共用噪声 |
| **Sky Shader（距离-色彩 + 频闪）** | 中 | 各 Boss CustomSky 升格（**`HanbaSky`/`JiangcenSky` 已有距离-色彩 lerp 思路**） | 雷暴天幕族（苍龙/将臣/Archosaur/劫云）可参数化共用 |
| **Intro Filter / 全屏后处理（去饱和/暗角/径向模糊）** | 中 | 全部 Boss 入场 + 失败/死亡收尾 | 一个可参数化 filter 覆盖多场景 |
| **Distortion（屏幕空间冲击波环/热浪）** | 中 | 牛头冲撞、黑熊砸地、苍龙俯冲、将臣/劫云冲击 | 命中帧使用，注意不遮挡 telegraph |
| **充能光环 Charge Glow** | 低 | 赢勾 `BladeScatter`、各蓄力招 | 现有多层 `SoftGlow` 叠绘可升级 |

---

## 6. 优先级 / 工作量总表 Priority & Effort Table

> 优先级沿用一迭代 §5（P0=已重写需叠加、P1=核心反模式、P2=补深度、P3=打磨、—=GOOD仅视觉）。
> 工作量 S/M/L 为 v2 **增量**估计（不含一迭代已完成部分）。

| Boss | 一迭代评级 | v2 优先级 | v2 工作量 | 一句话身份 | 招牌 set-piece | 需新 shader |
|------|-----------|-----------|-----------|------------|----------------|-------------|
| 苍龙真身 Azure Dragon | MEDIOCRE | **P1** | **L** | 雷暴审判庭终局压迫 | 网格化雷霆审判 + 风域 | 雷暴天幕·审判网格·落雷beam·雷弹glow |
| 祖龙残魂(地表) Archosaur | POOR | **P1** | **L** | 破替身→抓窗口的雷龙Raid | 残魂分裂 + 破绽窗口 | 龙身trail·雷暴天幕 |
| 后卿 Hoqing | POOR(已重写) | **P0叠加** | **M** | 蔓延尸疫逼迁场地 | 万鬼夜行经络高潮 | 疫斑Decal·Dissolve |
| 将臣 Jiangcen | POOR(已重写) | **P0叠加** | **M** | 雷牢封场+头顶环锤 | 雷牢降临 + 镜像锤魂 | 雷牢Decal·链电beam |
| 牛头马面 NiuMa | MEDIOCRE | **P2** | **M** | 双鬼差勾魂锁命连携 | 勾魂锁命连携 + 同伴复生 | 领域Decal·锁链beam·地府天幕 |
| 九尾 Kyuubi | MEDIOCRE | **P2** | **M** | 九尾合奏+辨真伪狐影 | 狐影九重 + 九方向同刺 | 狐火弹glow·幻影Dissolve·trail |
| 黑熊精 BlackBear | POOR | **P3** | **S–M** | 新手第一头会变阶段的猛兽 | 狂怒裂地 + 滚石冲撞 | 裂地Decal·狂怒描边 |
| 劫云 ×3 Tribulation | MEDIOCRE | **P3** | **S–M** | 独自硬撑的渡劫仪式 | 三波渐强 + 终雷 | 劫云天幕(参数化)·雷柱beam |
| 赢勾 Yingou | GOOD | **—** | **S** | 双刀环绕刀阵杀手 | （视觉升格）刀光trail | 刀光trail·充能环 |
| 旱魃 Hanba | GOOD | **—** | **S** | 旱灾武神鬼域牢笼 | （视觉升格）激光抽原语 | 占位贴图替换(其激光反哺工具箱) |

### 建议实施顺序 Suggested Order

1. **先做共享 shader 原语**（Arena Decal+Dissolve、Beam、Primitive Trail）——它们被 8/10 个 Boss 引用，是吞吐瓶颈；Beam 可直接从 `HanbaLaser` 抽取。
2. **P0 叠加（后卿/将臣）**：FSM 已就绪，只叠 set-piece + shader，性价比最高、风险最低，可作 shader 原语的首批落地验证。
3. **P1（苍龙/Archosaur）**：玩法层面动得最多（苍龙 P3 重做、Archosaur 结构升格），需要最多测试。
4. **P2（牛马/九尾）**：牛马镜头系统现成、九尾需去随机化 + 换 4 类占位弹。
5. **P3 + GOOD 视觉（黑熊/劫云/赢勾/旱魃）**：打磨收尾。

---

## 7. 验收自检 V2 Acceptance Checklist

每个 v2 交付前逐项确认：

- [ ] 满足一迭代 §3 全部 9 条硬性原则（未回退）。
- [ ] 满足本文 §1.2 升格五准则（身份/弧/set-piece/可读语言/表现服务可读）。
- [ ] telegraph 配色/形状/音频已对齐 §2.1 共享语言。
- [ ] 高潮全屏特效**不遮挡**任何需要躲避的危险信息（在最高难度实测）。
- [ ] 原版占位弹已全部替换为主题自定义弹（九尾/苍龙/劫云重点核查）。
- [ ] shader 走共享原语（§5），未重复造轮子。
- [ ] 多人模式：set-piece 的随机/序列由服务器决定并同步；场地贴花一致。
- [ ] GOOD 模板（赢勾/旱魃）FSM 未被改坏，仅视觉升格。

---

*Primordial / 洪荒 · NPCs/Boss 二迭代设计 V2 · 实现前请并读 `BOSS_REDO_PLAN.md` §3–4 与 `00_SHADER_VFX_TOOLKIT.md`（若已生成）。*
