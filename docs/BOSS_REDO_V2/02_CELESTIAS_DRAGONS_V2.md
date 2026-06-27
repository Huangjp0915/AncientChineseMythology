# 天庭龙系 Boss 二迭代设计 V2（Celestias Dragon Line · Second Iteration）

> **文档性质：** 第二迭代（v2）"设计师级"战斗设计规划 —— 天庭龙系（龙王脊柱 + 月后金龙/祖龙）
> **版本：** 2.0 · 2026-06-27
> **前置：** `docs/BOSS_REDO_PLAN.md`（v1 一审，修反模式 + 立骨架） · 本文在其之上"拔高"
> **配套：** `docs/BOSS_REDO_V2/00_SHADER_VFX_TOOLKIT.md`（着色器/VFX 工具箱 —— **当前尚未建立**，本文凡引用 toolkit 原语处均标注「见 shader toolkit」，落地前以本文 §0.3 已有 `.fx` 资产为可复用基线）
> **作用域：** `Celestias/Boss/` 之 AoGuangs · Aokins · Aoyuans · Aoshuns · CelestialDragons · AncestralDragonSouls（仅 AI/NPC 文件，Items/ 不在内）

---

## 0. 总览 Overview

### 0.1 本迭代的目标 What "v2" Means

v1 已完成"去反模式 + 立状态机骨架"的脏活：敖闰拿到了 P0 的 FSM 重写、祖龙残魂补了分裂 FSM、其余龙各有阶段结构。**v2 不再讨论"是否有设计"，而是讨论"是否好看、是否难忘"**：

- **每条龙必须有 1–2 个被"作者编排"过的高潮 set-piece**（不是随机弹幕池里的一项，而是一段有起承转合的演出）。
- **元素身份要驱动一个全屏级的视觉语言**（屏幕着色器 / 自定义天幕 / 镜头）——龙是模组里最适合用元素屏幕着色器的怪物族。
- **蠕虫/蛇形身体必须成为真机制**，而不仅是一条会撞人的贴图尾巴。
- 每节给出 **可读性语言（telegraph）** 与 **表现层清单（着色器/VFX/镜头/震屏/天幕/音乐）**，以及 **可行性与成本（S/M/L）**。

> v2 的验收口径：玩家打完后能说出"我记得那条龙做了 X"——X 是一个画面，不是一句"它喷得更快了"。

### 0.2 当前实现快照 Current State Snapshot（v2 起点）

| 龙 | Tier | 架构现状 | v1 评级 | v2 起点判断 |
|----|------|----------|---------|-------------|
| 敖广 AoGuang | T31 | 19 状态大 phase enum + 3 HP 阶段 + 水域封路龙卷 | **GOOD** | 抛光 + 视觉升级（海之折射）|
| 敖钦 Aokin | T32 | phase enum，但**只有 P1/P2、无 P3**；蛇身段接触伤害；俯冲 | MEDIOCRE | 火系身份补完 + P3 熔火场 |
| 敖闰 Aoyuan | T33 | **已完成 P0 FSM 重写**：永冻地痕签名 + 6 命名攻击 + 绝对零度可破弱点 | POOR→已重写 | **在新 FSM 上做演出层 v2** |
| 敖顺 Aoshun | T34 | FSM + StormCharge 蓄电 + 潜地伏击（submerge/emerge）| **GOOD** | 抛光 + 视觉升级（风暴压暗）|
| 祖龙残魂(天庭) Ancestral | T42 | 13 状态 FSM + 50% 真分裂双子 + 双龙协同；但 **Enraged 仍是空壳** | MEDIOCRE | 接线 Enraged 终曲 + 太初视觉 |
| 天御金龙 Celestial | T43 | HP 比例驱动 attackPhase 0–3（变相密度档）+ 6 模式定时轮转 | MEDIOCRE | 脚本化"天规"幕 + 金阵签名 |

> **重要修正：** v1 总规划写"祖龙残魂 Enraged 是 stub 永不进入"。现状已**会进入**（25% 触发 `didEnrage`），但 `UpdateEnraged()` 只 lerp 一下 `aggressionBuildup` 就立刻 `TransitionTo(Patrol)` —— **进了等于没进**。v2 把它当作"必须填肉的真空高潮"对待（见 §6）。

### 0.3 可复用着色器资产 Existing Shader Assets（落地基线）

模组已具备 `fxc.exe` → `.fxc` 的编译管线（`Effects/CompileFX.ps1`，`fx_2_0`）。以下既有 `.fx` 是 v2 的**直接复用/改色对象**，在 toolkit 正式建立前即可支撑大部分需求：

| 既有着色器 | 原用途 | 龙系可复用方向 |
|------------|--------|----------------|
| `XuanwuFrostDistortion.fx` | 玄武霜冻屏幕扭曲 | **敖闰**全屏霜冻折射/绝对零度白屏结晶 |
| `XuanwuIceField.fx` / `XuanwuIcePillar.fx` | 冰原/冰柱 | 敖闰永冻地痕、冰晶棋局柱体 |
| `XuanwuCaustics.fx` | 水体焦散 | **敖广**水下折射屏幕、漩涡焦散 |
| `XuanwuTrailRibbon.fx` | 缎带拖尾 | 通用龙体能量拖尾（金龙/祖龙/敖顺雷链）|
| `AncestralDragonSky.fx` | **祖龙专属天幕（已存在）** | 祖龙 v2 直接增强，无需新建 |
| `DazhengArenaCircle.fx` | 场地圆环 | 金龙"天规"安全/危险格、祖龙灵链场 |
| `BloodSeaAtmosphere.fx` | 大气染色 overlay | 通用元素屏幕染色模板（热浪/风暴/金芒）|

> **新建着色器**统一记入 toolkit 待办；本文每节"Feasibility"会标注"复用既有 / 需新建"。

### 0.4 跨龙统一语言 Cross-Dragon Conventions（避免重复造轮子）

为了让 6 条龙"成体系"而非散件，v2 约定三条共享语言（具体参数各龙微调）：

1. **元素屏幕染色（Elemental Screen Tint）**：进入战斗 → 屏幕缓入一层元素 overlay（敖广水蓝折射 / 敖钦热浪 / 敖闰霜白 / 敖顺风暴压暗 / 金龙金芒 / 祖龙太初灰白），血量越低越浓。**复用 `BloodSeaAtmosphere` 模板 + 各龙调色**。
2. **蛇身即机制（Body-as-Mechanic）**：每条龙的高潮里身体段至少承担一种功能——封路墙、可破弱点、灵链锚、缠绕牢笼。禁止"身体只是会撞人的装饰"。
3. **三拍演出节律（3-Beat Cinematic）**：出场 / 阶段转换 / 死亡各为一段 ≤3 秒、带 `dontTakeDamage` i-frame 的镜头节拍（震屏 + 天幕脉冲 + 音效），玩家在此期间**只看不打**。敖广/祖龙已有雏形，统一所有龙到这条节律。

---

## 1. 敖广 AoGuang — 东海 / 海（GOOD · 抛光 + 视觉升级）

> 路径：`Celestias/Boss/AoGuangs/`（`AoGuang.AI.cs` + `Phase1/2/3` + `Drawing` + `Projectiles*`）
> v2 定位：**龙王脊柱的"标尺"**。结构不动，只把它的"海"做成全屏沉浸——它将成为后续龙的视觉上限参考。

### 1. Fight Fantasy & Identity
东海龙王坐镇一片被它召来的真实海域：你不是在打一条龙，而是在它的海里求生。

### 2. Phase/Act Narrative（Elevation over v1）
v1 已有 Intro→封路龙卷→P1(6状态)→P2(7状态)→P3(7状态) 的完整大 enum。v2 的**拔高点不在加招，而在"水位叙事"**：

- 把三阶段重新包装成**潮汐三幕**：P1「涨潮」（水蓝 overlay 浅、海面在屏幕下缘）→ P2「没顶」（overlay 加深、屏幕下半进入水下折射，水平移动有轻微阻尼手感暗示）→ P3「深渊」（全屏水下、光线柱、`AbyssalVortex` 成为真正的场地中心）。
- 阶段转换的封路龙卷（已存在）在 P3 收束为**一个绕场旋转的巨型漩涡边界**，把"封路"升级成"被吸向中心"的持续位移压力。

### 3. Signature Set-Piece(s)
- **「龙吟·没顶」(P2 进入演出)**：龙盘绕成一圈环住玩家 → 张口龙吟（三拍镜头）→ 海面"漫上来"，屏幕自下而上被水下折射覆盖至屏幕中线。此后 P2 的所有水弹在折射层下显得"漂浮变形"，强化压迫。**身体机制**：盘绕的龙体本身就是 P2 的圆形场地边界。
- **「深渊漩涡」(P3 set-piece, 复用既有 `Phase3_AbyssalVortex`)**：升级为全屏向心漩涡——中心是死亡区，龙绕外圈高速游弋抛珊瑚刺，玩家要**逆着吸力**在中圈环带走位；龙每绕场一周收紧一次安全环带（呼应敖顺风暴之眼，但这里是"被吸"而非"被压缩"）。

### 4. Telegraph & Readability
- 水下折射强度=深度=阶段，给玩家一个"我现在多危险"的恒定可读底色。
- 漩涡边界用 `DazhengArenaCircle` 式圆环描边（颜色 = 当前安全环带），吸力方向用向心粒子流明示。
- 封路龙卷保留其现有粒子柱作为硬边界（已可读）。

### 5. Presentation（Shader/VFX/Camera/Shake/Sky/Music）
- **屏幕着色器**：`XuanwuCaustics`（水下焦散，改蓝）做"没顶"后的全屏底层；`BloodSeaAtmosphere` 模板改水蓝做潮汐 overlay。**水下折射** = 沿屏幕 Y 做正弦 UV 偏移（见 shader toolkit「refraction」原语；可由 `XuanwuFrostDistortion` 改算法复用）。
- **自定义天幕**：现有 AoGuang 天空保留，P3 压暗 + 加海面高光带。
- **镜头/震屏**：沿用现有 `ScreenShakePlayer`；龙吟没顶用 15–20 幅短震 + 一次潮涌缓推。
- **音乐**：保留；P3 可切到更低沉混响段（若资源允许）。

### 6. Feasibility & Cost
- **S–M**。结构零改动，主要是**绘制层 + 屏幕着色器**工作。
- 新建：水下折射 `.fx`（或由 `XuanwuFrostDistortion` 改）；其余复用。
- **MP/性能**：屏幕着色器是纯客户端，多人安全；折射全屏 pass 注意只在玩家进入战斗范围时启用，远离即淡出（沿用现有 Sky intensity 淡入淡出模式）。
- **复用**：本节的"元素屏幕染色 + 折射"是 §0.4 共享语言的旗舰范例，其它龙照搬调色。

---

## 2. 敖钦 Aokin — 南海 / 火（MEDIOCRE · 补完火系身份 + P3）

> 路径：`Celestias/Boss/Aokins/`（`Aokin.AI.cs` + `Phase1` + `Phase2` + `AokinHelper` + `AokinProjectiles` + `AokinSky`）
> v2 定位：从"敖广换皮 + 缺 P3"升级为**有独立火焰节律的熔火龙王**。

### 1. Fight Fantasy & Identity
一条把战场点成熔炉的烈焰巨蛇——它不喷弹幕，它在"加热"整个房间，直到地板本身成为威胁。

### 2. Phase/Act Narrative（Elevation over v1）
v1 现状：P1（火弹/龙息/甩尾/陨石）+ P2（狂怒冲刺/旋涡/地狱龙息/陨石风暴/俯冲/突袭火球），**无 P3**，俯冲带冷却已是亮点。v2 拔高：

- **引入"过热 Heat"资源（对位敖顺 StormCharge）**：敖钦的攻击与陨石会在场地累积"余烬温度"。温度是一条全屏可读的热浪 overlay 强度条。这把 v1 互不相关的火招**串成一条升温曲线**，而不是随机轮转。
- **新增 P3「熔心」（≤33% HP）**：场地进入熔火幕。这是 v1 缺失的高潮，必须是"改规则"的：玩家可站的安全地面随温度收缩。

### 3. Signature Set-Piece(s)
- **「焚风走廊」(P2 招式升级，蛇身机制)**：敖钦把蛇身**横拉成一道贯穿屏幕的火墙**（利用现有 `UpdateSegments` 段位），墙上留一道随机缺口缓慢移动——玩家钻缝（结构上类似敖闰暴雪帷幕，但这里是"龙体本身"成墙，主题差异化）。钻缝失败 = 点燃叠层（呼应火系武器点燃主题）。
- **「熔心·下沉天花板」(P3 set-piece)**：龙升空盘成顶环 → 龙息把屏幕顶部烧成下压的"岩浆天花板"（一条从上向下推进的伤害带 + 热浪 overlay 飙红）→ 同时地面随机喷岩浆柱（带地面预警圈）。玩家被夹在"下压的热"与"上顶的柱"之间找节奏窗口。**温度满档**时天花板下压更快——把 v1 的"低血加速"反模式改写成"你自己把房间烧热了"的因果。

### 4. Telegraph & Readability
- 热浪 overlay = 温度条，画面越扭曲越红代表越危险（恒定底色可读）。
- 岩浆柱：地面焦黑预警圈 → 龟裂发光 → 喷发（三段，参考 §0.4 三拍）。
- 焚风走廊缺口：龙体断口处粒子稀疏 + 一道指示光，明示可钻位置。

### 5. Presentation
- **屏幕着色器**：**热浪扭曲（heat-haze）** —— 屏幕上半随高度做 UV 抖动 + 暖色染（见 shader toolkit「heat haze」；可由 `XuanwuFrostDistortion` 反相调暖色改出）。`BloodSeaAtmosphere` 模板改橙红做温度 overlay。
- **VFX**：现有 `AokinHelper.CreateFlameVortex/DragonFireBurst` 复用；P3 加岩浆地面贴花（emissive）。
- **天幕**：现有 `AokinSky` P3 转血橙 + 落灰粒子。
- **镜头/震屏**：俯冲/天花板下压用持续低频震；陨石落地点震。
- **音乐**：P3 切高张力段（若有）。

### 6. Feasibility & Cost
- **M–L**。需**新写 P3 状态 + Heat 资源 + 熔心 set-piece**（P3 是从零搭）。焚风走廊复用现有段位系统，成本中等。
- 新建：heat-haze `.fx`（或改 `XuanwuFrostDistortion`）；岩浆柱/天花板弹幕（可复用 `AokinFireball`/`AokinMeteor` 改）。
- **MP/性能**：热浪全屏 pass 客户端；温度值需 `SendExtraAI` 同步（一个 float）。注意点燃叠层走 BuffID 以省网络。
- **复用**：Heat 资源结构直接抄 Aoshun 的 `StormCharge` 字段 + 同步模式。

---

## 3. 敖闰 Aoyuan — 西海 / 冰（已完成 P0 FSM 重写 · 在其上做 v2 演出）

> 路径：`Celestias/Boss/Aoyuans/`（`Aoyuan.cs` + `Aoyuan.AI.cs` + `Phase1` + `Phase2` + `AoyuanAttacks` + `AoyuanFrost` + `AoyuanSky` + `Drawing`）
> v2 定位：**不动 FSM 骨架**（Intro/Patrol/PreAttack/Attacking/Cooldown/PhaseTransition + 6 命名攻击 + 永冻地痕 + 绝对零度可破弱点），把它**包装成模组里最"冷"的一场战斗**。

### 1. Fight Fantasy & Identity
西海龙王把战场冻成一片正在结冰的死海——温度不是数字，是你脚下逐渐失去的摩擦力与逐渐变白的视野。

### 2. Phase/Act Narrative（Elevation over v1）
v1 已有：50% 浮空破境（`RunPhaseTransition` + `ApplySlipperyField` 打滑 30s + `AoyuanFrostPlayer.slipperyTimer`）、绝对零度大招（吸气蓄力 → 弱点可破 → 全屏冻结/削弱）。v2 拔高的是**"结冰"的全程可视化与因果**：

- 把"永冻地痕"从一个减速 debuff 升级成**全屏霜冻进度**：玩家踩过/停留越久，屏幕四周的结霜越往中心生长（vignette 式霜冻），3 层=即将冻结的视觉警告。让 v1 已有的冻结机制"看得见、躲得开"。
- 50% 浮空破境后，`slipperyField` 打滑 + 屏幕底色转更深的极夜蓝（`AoyuanSky` 已有 lifePercent 调色，强化之）。

### 3. Signature Set-Piece(s)
- **「绝对零度·破弱点」(已有机制 → 升级为可读 set-piece)**：v1 已实现"3 秒吸气 + 身体段暴露冰晶弱点 + 击破阈值打断"。v2 把它演出化：吸气时**全场粒子向龙汇聚 + 屏幕边缘急速结霜向中心收口**（霜冻 vignette 逼近满屏=即将全屏冻结的倒计时）；身体段弱点用发光冰晶高亮 + 一条"打这里"的指示。打破 → 屏幕霜裂玻璃碎裂特效 + 削弱；没打破 → 满屏白化结晶冻结惩罚。**蛇身=可破弱点阵列**，这是它的身体机制核心。
- **「冰晶棋局」(已有 → 视觉升级)**：v1 的 3×3 真/虚冰柱预告（`AoyuanPillarTelegraph`）极适合做成**棋盘地面着色器**——真柱格地面冰裂发蓝光、虚招格仅霜面，让"读棋盘"成为一个清爽的视觉解谜而非靠记弹幕。

### 4. Telegraph & Readability
- **霜冻 vignette = 你离被冻多近**（恒定可读，全程在场）。
- 冰晶棋局：真柱格地面预亮（已有 telegraph 弹幕，v2 加地面着色器强化）。
- 绝对零度：吸气向心粒子 + 弱点高亮 + 收口的霜环 = 三重计时提示。

### 5. Presentation
- **屏幕着色器**：`XuanwuFrostDistortion`（**直接复用**，玄武已用同一套霜冻扭曲）做全屏霜冻折射；霜冻 vignette 用 `XuanwuIceField` 改径向 alpha。绝对零度释放=一帧白屏结晶闪 + 霜裂。
- **VFX**：现有 `AoyuanHelper.CreateFrostVortex/CreateIceBurst` 复用；地痕用 `XuanwuIcePillar` 风格冰面贴花。
- **天幕**：`AoyuanSky` 已是深海蓝→极夜黑，v2 仅加二阶段极光带强度。
- **镜头/震屏**：破境 + 绝对零度释放用强震；冻结惩罚命中=镜头短暂霜白冻帧（freeze-frame 0.2s）。
- **音乐**：保留 Boss2；绝对零度蓄力时低频环境音渐强（已有 Item122 蓄力音可叠）。

### 6. Feasibility & Cost
- **S–M**。**逻辑零改动**（FSM 已完备），纯表现层叠加，是性价比最高的一条。
- 新建：几乎不需要——`XuanwuFrostDistortion` / `XuanwuIceField` / `XuanwuIcePillar` **全部已存在**，只需调色 + 接线到敖闰的 Sky/Draw。棋盘地面着色器为唯一可选新增。
- **MP/性能**：全客户端着色器；vignette 进度可由本地 `AoyuanFrostPlayer` 字段驱动（已存在），无新增网络。
- **复用**：敖闰是"冰系视觉"的标杆，其霜冻 vignette 逻辑可被玄武/劫云等其它冰系内容复用。

---

## 4. 敖顺 Aoshun — 北海 / 雷（GOOD · 抛光 + 视觉升级）

> 路径：`Celestias/Boss/Aoshuns/`（`Aoshun.AI.cs` + `AoshunAttacks` + `AoshunArms` + `AoshunTail` + `AoshunBody` + `AoshunHelper` + `AoshunSky`）
> v2 定位：**资源机制的标尺**。StormCharge 蓄电 + 潜地伏击 + 风暴之眼已是好设计，v2 只把"风暴"做成全屏天气。

### 1. Fight Fantasy & Identity
北海龙王把战场拖进它的雷暴中心——它钻地蓄电，浮空放电，整片天空随它的电量明灭。

### 2. Phase/Act Narrative（Elevation over v1）
v1 已有：StormCharge（钻地蓄电）、8 攻击（雷链/深渊伏击/龙鳞风暴/龙卷缠绕/天雷印 + 二阶段龙王怒啸/风暴之眼/雷霆连环冲）、潜地 submerge/emerge 伏击。v2 拔高：

- **把 StormCharge 升级为全屏天气表**：电量越高 → 屏幕越暗、闪电频率越高、边缘电弧越密。满电时屏幕进入"雷暴临界"压暗，玩家一眼知道"它要放大招了"。这让现有的"满电优先放强招"逻辑（`IsFullyCharged` 分支）**有了视觉前兆**。
- **风暴之眼（已有）成为 P2 的招牌缩圈幕**，v2 给它加全屏风暴压暗 + 安全眼内的"晴空"对比。

### 3. Signature Set-Piece(s)
- **「深渊伏击」(已有 submerge/emerge → 演出升级)**：v1 已实现潜地→地面预警标记→高速上冲爆出冲击波。v2 把"潜地"做成**全屏变暗 + 只剩地面预警电弧亮起**的紧张静默拍——龙消失，屏幕只剩一处闪烁的地裂电光，然后炸出。把伏击从"一次攻击"升级成"一个会让你屏息的瞬间"。
- **「雷暴临界」(满电 set-piece)**：满 StormCharge 时触发——龙盘空一周把蛇身**电链相连成一张笼罩场地的电网**（蛇身=电链锚点机制），网格随机段位轮流通电，玩家在网格安全格间走位（呼应玄武反射窗口，但这里是龙体自身连成的雷网）。

### 4. Telegraph & Readability
- 屏幕压暗程度 = StormCharge 电量 = 危险等级（恒定可读）。
- 深渊伏击：地面电弧预警标记（已有 `SpawnThunderSealMarker`）+ 全屏静默暗场强化。
- 雷网：通电前段位预亮黄白 → 通电（两拍）。

### 5. Presentation
- **屏幕着色器**：**风暴压暗（storm darkening）** —— 全屏降亮 + 暗角 + 随机闪电白闪（见 shader toolkit「storm」；`BloodSeaAtmosphere` 模板改深靛蓝即可）。雷链/电网用 `XuanwuTrailRibbon` 改电弧色。
- **VFX**：现有 `AoshunHelper.CreateThunderVortex/ThunderBurst/LightningTrail` 复用充分。
- **天幕**：`AoshunSky` 加电量驱动的乌云密度 + 远景雷闪。
- **镜头/震屏**：伏击爆出强震；满电临界进入时一次全屏白闪 + 短震。
- **音乐**：满电时叠加低频雷鸣环境层。

### 6. Feasibility & Cost
- **S–M**。逻辑基本不动，主要是 storm overlay + 雷网 set-piece（雷网复用现有段位 + 节点弹幕）。
- 新建：storm darkening `.fx`（`BloodSeaAtmosphere` 改色，低成本）。
- **MP/性能**：StormCharge 已在网络同步（`internalAI`/字段），overlay 纯客户端读电量。雷网注意通电判定走弹幕而非逐玩家循环。
- **复用**：storm overlay 与敖钦 heat overlay、敖广 tide overlay 共用 §0.4 同一套"元素屏幕染色"模板。

---

## 5. 天御金龙 Celestial Dragons — 月后 / 天界金（MEDIOCRE · 脚本化"天规"幕 + 金阵签名）

> 路径：`Celestias/Boss/CelestialDragons/`（`CelestialDragons.cs` 基类含全部 HeadAI + `CelestialDragonsHead/Body/Tail` + `Projectiles`）
> v2 定位：从"HP 比例密度档（变相加速）"升级为**有"天界秩序"主题的脚本化幕战**。

### 1. Fight Fantasy & Identity
天庭巡卫的金龙不与你缠斗，它"裁决"你——在场地上画下天规法阵，把战场切成允许与禁止的格子。

### 2. Phase/Act Narrative（Elevation over v1）
v1 现状（`HeadAI`）：`attackPhase` 由 `lifeRatio` 算出 0–3，6 个攻击模式（巡空/俯冲/剑气/大圆/龙威法阵/全屏）按 600−phase*100 帧定时轮转，phase 越高密度越大、间隔越短——**这正是 §2.1 的"低血加速密度档"反模式**。v2 拔高：

- **用脚本化"天规三诏"替换密度档**：把 attackPhase 从"密度乘数"改成"**法阵规则升级**"。每跨一档，场地上常驻的"天规法阵"换一套规则，而不是单纯把所有招喷更快。
- 现有 `DragonAuthorityAttack`（龙威法阵 + 叉状天雷）已是最有"天界"味的招——把它**从轮转池里抽出来，提升为贯穿全程的场地机制**（法阵常驻，攻击围绕它展开）。

### 3. Signature Set-Piece(s)
- **「天规棋盘」(签名机制, 贯穿全程)**：场地常驻一组金色法阵格（`DazhengArenaCircle` 风格圆环/网格）。每一档天规切换"哪些格安全/危险"的规则：
  - 一诏（>75%）：单法阵，站阵内安全。
  - 二诏（75–50%）：双阵交替明灭（呼应现有 P≥1 的左右追加法阵 `DragonCircleWarning`）。
  - 三诏（<50%）：四角法阵 + 中心禁区（现有 P≥2 的四向法阵已是雏形），玩家须按"亮格"跳位。
  把现有的 `DragonCircleWarning`/`ForkedLightningWarning` 从"随机零散天雷"重组为"踩错格 → 法阵落雷"的因果机制。
- **「敕令·天剑雨」(终幕 set-piece, 复用 `FullScreenAttack`)**：现有全屏攻击（环形闪电 + 天降金剑 `FallingSword`）升级为**一段编排过的天界审判**：龙盘成顶环 → 全屏金芒 bloom 渐亮（蓄力可读）→ 金剑按法阵格次序逐列落下（不是随机洒，而是"扫格"），玩家顺着安全格的推进节奏走。蛇身在外圈高速绕场=移动的边界压力。

### 4. Telegraph & Readability
- 天规法阵格用 `DazhengArenaCircle` 着色器明示安全/危险（金=安全、暗红=危险），切诏时圆环颜色翻转。
- 叉状天雷保留现有高空预警 `ForkedLightningWarning`（已可读）。
- 天剑雨：金芒 bloom 渐亮做总蓄力条 + 逐列落剑的列预警线。

### 5. Presentation
- **屏幕着色器**：**金色泛光（golden bloom）** —— 高亮区域外溢金芒 + 全屏暖金 tint（见 shader toolkit「bloom」；`BloodSeaAtmosphere` 改金 + 加阈值 bloom pass）。法阵用 `DazhengArenaCircle`（**已存在**）。
- **VFX**：现有金色 `GoldFlame`/`Sparkle`/`GoldenEnergy`/`GoldenSwordAura` 复用充分。
- **天幕**：可加金龙专属天幕（云海 + 金色裂隙），或复用通用天庭天幕。
- **镜头/震屏**：切诏一次缓推镜头 + 短震；天剑落列逐次轻震。
- **音乐**：天界主题；终幕审判切高潮段。

### 6. Feasibility & Cost
- **L**。这是龙系里**逻辑改动最大**的一条——要把 `HeadAI` 的 `attackPhase` 密度档重构为"天规规则机"，并把法阵从临时弹幕升级为常驻场地系统。
- 新建：天规法阵场地系统（可基于 `DazhengArenaCircle` + `DragonCircleWarning` 扩展）；golden bloom `.fx`。
- **MP/性能**：当前 AI 全走 `NPC.ai/localAI` 同步（已 `netAlways`）。常驻法阵建议用单一管理弹幕持有"当前诏书规则"，避免逐格弹幕刷屏；天剑雨用按列时序生成控制弹幕峰值。
- **复用**：法阵系统与祖龙灵链场、敖广漩涡边界共享 `DazhengArenaCircle` 描边语言。

---

## 6. 祖龙残魂(天庭) Ancestral Dragon Soul — 月后脊柱 / 太初（MEDIOCRE · 接线 Enraged 终曲 + 太初视觉）

> 路径：`Celestias/Boss/AncestralDragonSouls/`（`AncestralDragonSoulHead.cs`（13 状态 FSM） + `Body/Tail` + `Projectiles` + `AncestralDragonSky`）
> v2 定位：**双龙协同已是亮点，唯一的真空是 25% 的 Enraged 终曲**——v2 必须把这块填成全战斗的高潮。

### 1. Fight Fantasy & Identity
祖龙残魂是一缕太初之魂的回响——半血时它"一分为二"成双子并肩作战，残血时双魂回拢成一场孤注一掷的终焉。

### 2. Phase/Act Narrative（Elevation over v1）
v1 现状（`AncestralDragonSoulHead`）：完整 13 状态 FSM（鳞弹/符文/阴阳双珠/龙息激光/螺旋俯冲/掠袭冲锋 + 50% `SplitTransition` 真分裂双子 + 双龙 TwinLink/TwinCrossfire/TwinPressure）。**问题点**：`UpdateEnraged()` 仅 `aggressionBuildup` lerp 后立刻 `TransitionTo(Patrol)` —— 25% 的"狂暴"是真空。v2 拔高：

- **填满 Enraged 为"双魂回拢"终曲**：当任一龙残血触发（建议改为：双龙合计血量 <25%），双子龙**朝场地中心回拢、灵链收紧、合体为一条更亮的"太初真身"**做最后一波编排攻击。把"低血狂暴"反模式直接改写成"分→合"的叙事闭环（与 50% 的"合→分"对称）。
- 现有 `SoulTetherChain`（双龙灵链）在 Enraged 升级为**致命收束链**：链不再只是连接，而是回拢过程中横扫全场的处决线。

### 3. Signature Set-Piece(s)
- **「半血·双魂裂分」(已有 `SplitTransition` → 演出升级)**：v1 已实现分裂（血量减半生成双子 + 镜头 punch + 粒子）。v2 加**太初灰白屏幕闪 + `AncestralDragonSky`（已存在的专属天幕）脉冲一拍**，让分裂成为一个画面记忆点。
- **「残血·双魂回拢」(Enraged 终曲 set-piece, 新填)**：双子向中心对冲 → 灵链收束成 X 形处决线扫场（telegraph：链先变亮拉直）→ 合体闪光 → "太初真身"释放一次螺旋俯冲 + 符文封锁的组合终招（复用现有 `SpiralDive` + `SigilEruption` 编排成固定序列，而非随机）。蛇身（双龙体 + 灵链）是这一幕的全部机制载体。

### 4. Telegraph & Readability
- 灵链：连接态=细而柔（无伤或弱），处决态=变粗拉直发亮（强伤）——粗细即危险，恒定可读。
- 回拢：双龙对冲路径用拖尾预示；合体闪光前一拍镜头缓推（看而不打）。
- 符文封锁沿用现有 `AncestralSoulSigil` 延时能量柱预警。

### 5. Presentation
- **屏幕着色器**：**太初染色 + 灵能扭曲** —— 灰白/青白全屏 tint + 合体瞬间径向扭曲（见 shader toolkit；可由 `XuanwuFrostDistortion` 改青白复用）。
- **天幕**：`AncestralDragonSky.fx`（**已存在**）—— v2 直接增强其脉冲，分裂/回拢各触发一次天幕亮拍。
- **VFX**：现有 `WhiteTorch`/`Clentaminator_Cyan`/`Cloud` 粒子 + `DrawMysticalGlow`/`DrawEtherealTrail`（已存在的发光绘制）复用充分。
- **镜头/震屏**：现有 `PunchCameraModifier` 已用于 Intro/Split/Enrage —— v2 给回拢合体加一次更强的 punch + 短冻帧。
- **音乐**：回拢终曲切最高潮段（若有），或叠环境层。

### 6. Feasibility & Cost
- **M**。骨架与双龙系统已完备，**核心工作是填 `UpdateEnraged` 终曲 + 调整触发条件（双龙合计血量）**，外加表现层。风险点在于双龙合体/血量同步的网络正确性。
- 新建：合体逻辑（可复用 `SpawnTwinDragon` 的逆过程思路）；处决链行为升级（改 `SoulTetherChain`）；太初染色 `.fx`（改 `XuanwuFrostDistortion`）。
- **MP/性能**：分裂/合体涉及 NPC 生成与血量重分配，已有 `SendExtraAI`/`SyncNPC` 基础——合体务必只在 server 决策并广播，客户端只演出。`partnerIndex`/`IsTwin` 同步已存在，复用之。
- **复用**：处决链 + 灵链场与金龙天规法阵共享 `DazhengArenaCircle` 场地描边语言；太初染色 `.fx` 与敖闰霜冻同源。

---

## 7. 表现层成本一览 Shader/VFX Workload Summary

> 汇总 v2 涉及的屏幕着色器需求，区分"复用既有"与"需新建"，供 toolkit（`00_SHADER_VFX_TOOLKIT.md`）建立时统一排期。

| 元素屏幕语言 | 龙 | 复用既有 | 需新建 | 备注 |
|--------------|----|----------|--------|------|
| 水下折射 / 焦散 | 敖广 | `XuanwuCaustics` | 折射 UV 偏移（或改 `XuanwuFrostDistortion`）| 旗舰范例 |
| 热浪扭曲 heat-haze | 敖钦 | `BloodSeaAtmosphere`(改橙) | heat `.fx`（可改 `XuanwuFrostDistortion`）| + Heat 资源 |
| 霜冻折射 + vignette | 敖闰 | `XuanwuFrostDistortion`/`XuanwuIceField`/`XuanwuIcePillar` | （几乎无）| **零逻辑改动，最高性价比** |
| 风暴压暗 storm | 敖顺 | `BloodSeaAtmosphere`(改靛)/`XuanwuTrailRibbon` | storm `.fx`（改色）| |
| 金色泛光 golden bloom | 金龙 | `DazhengArenaCircle` | bloom `.fx` | + 天规法阵系统（逻辑大头）|
| 太初染色 + 灵能扭曲 | 祖龙 | `AncestralDragonSky`(已存)/`DazhengArenaCircle` | 太初 tint（改 `XuanwuFrostDistortion`）| + Enraged 终曲（逻辑大头）|

> **统一原则**：6 条"元素屏幕染色"全部基于 `BloodSeaAtmosphere` 同一模板调色 → toolkit 应抽出一个参数化 `ElementalScreenTint` 通用 `.fx`，6 龙传不同 color/强度即可，避免 6 份重复着色器。

---

## 8. 每龙 优先级 / 工作量 总表 Per-Boss Priority & Effort

> 优先级口径：**P0** 有真空高潮/反模式核心未除 → **P3** 纯抛光。工作量 S/M/L 含逻辑 + 表现层。
> 排序按进度 Tier。

| 龙 | Tier | v1 评级 | v2 核心交付（headline）| 逻辑工作量 | 表现工作量 | v2 优先级 | 关键风险 |
|----|------|---------|------------------------|------------|------------|-----------|----------|
| 天御金龙 Celestial | T43 | MEDIOCRE | 天规法阵机替换密度档 + 敕令天剑雨 | **L** | M | **P0** | 重构 HeadAI 密度档；常驻法阵网络/弹幕峰值 |
| 祖龙残魂 Ancestral | T42 | MEDIOCRE | 填 Enraged「双魂回拢」终曲（真空高潮）| **M** | M | **P0** | 合体/血量同步正确性 |
| 敖钦 Aokin | T32 | MEDIOCRE | 补 P3「熔心」+ Heat 资源 + 焚风走廊 | **M–L** | M | **P1** | P3 从零搭；Heat 同步 |
| 敖闰 Aoyuan | T33 | 已重写 | 霜冻 vignette + 绝对零度演出化（逻辑零改）| **S** | M | **P2** | 几乎无（着色器已存在）|
| 敖广 AoGuang | T31 | GOOD | 潮汐三幕水下折射 + 深渊漩涡升级 | **S** | M | **P2** | 折射 `.fx` 新建；勿改坏 GOOD 结构 |
| 敖顺 Aoshun | T34 | GOOD | StormCharge 全屏天气 + 雷暴临界雷网 | **S** | S–M | **P3** | 勿改坏 GOOD 结构 |

### 实施建议 Sequencing
1. **先做 toolkit 的 `ElementalScreenTint` 通用着色器**（§7），6 龙共用——这是所有视觉升级的前置。
2. **P0 两条（金龙 / 祖龙）优先**：它们仍含未消除的核心问题（密度档 / 真空 Enraged），是 v2 的"必须"。
3. **P1 敖钦**：补完 P3 是它从 MEDIOCRE 升 GOOD 的关键。
4. **P2/P3 四条（敖闰 / 敖广 / 敖顺）以表现层为主**，逻辑基本不动——其中**敖闰性价比最高**（着色器全已存在，纯接线）。
5. 全程红线：**敖广 / 敖顺为 GOOD 参考模板，v2 只许加表现层，禁止改坏其战斗结构**（沿用 v1 §3 原则 9）。

---

*Primordial / 洪荒 · Celestias Dragon Line v2 · 本文只做"拔高"，不复述 v1；落地前先建 `00_SHADER_VFX_TOOLKIT.md` 的 `ElementalScreenTint` 通用着色器。*
