# 天庭非龙系 Boss 第二迭代设计 V2（Celestias Non-Dragon Bosses — Designer-Grade Pass）

> **文档性质：** 第二迭代（V2）战斗"提升"设计——在第一遍（`docs/BOSS_REDO_PLAN.md`）消除反模式、补齐结构的基础上，把每个非龙系天庭 Boss **拔高为有作者签名、视觉震撼的遭遇战**。
> **版本：** 2.0 · 2026-06-27
> **范围：** `Celestias/Boss/` 下所有**非龙**系 Boss（神威 / 百目 / 毗沙门 / 天庭观察者 / 四圣兽：青龙·白虎·朱雀·玄武 / 大椿 / 树精）。龙系（敖广/敖钦/敖闰/敖顺/天御金龙/祖龙残魂）见对应 V2 文档，不在本文。
> **配套：** `docs/BOSS_REDO_PLAN.md`（第一遍权威基准） · `docs/BOSS_REDO_V2/00_SHADER_VFX_TOOLKIT.md`（着色器/VFX 工具箱，**若尚未建立则参见本文 §1 的工具箱占位约定**） · `PROGRESSION_DESIGN_SPEC.md`（进度/掉落/数值，不被本文覆盖）。
> **硬约束：** 本文是**规划**，不改任何 Boss 代码。所有"提升"必须在 V1 的 FSM/telegraph/脚本化幕之上叠加，**不得退回**"低血量=加速喷弹"反模式。GOOD 模板（神威/百目/玄武）只做**打磨 + 视觉升级**，禁止改坏其核心设计。

---

## 1. 设计意图与工具箱约定 Intent & Toolkit Convention

### 1.1 V1 → V2 的差距 What V1 fixed vs. what V2 adds

第一遍解决的是"**能不能玩**"：杀掉随机喷弹 hub、给每个 Boss 真实 FSM、补 telegraph、接线死状态。本文解决的是"**值不值得记住**"——把可玩的战斗升级为**有高光时刻（set-piece）、有作者编排、有视听冲击**的遭遇战。

V2 的三条提升主轴：

1. **作者化高光（Authored Set-pieces）：** 每个 Boss 至少一个**手工编排、与主题强绑定**的标志性时刻（而非又一个程序化弹幕循环）。这些时刻是玩家通关后会复述的"那一下"。
2. **可读性语言统一（Readability Language）：** 把 V1 零散的尘埃 telegraph 升级为**统一的预告语汇**——颜色编码（危险=暖金/暖红，安全=冷蓝/玉青）、形状编码（线=射线、圆=落点、扇=安全缝）、时间编码（预告时长 ∝ 伤害）。
3. **视听编排（Presentation）：** 在关键节拍引入**着色器、天空、运镜、震屏、音乐切换**，让阶段转换与高光时刻具备电影感。

### 1.2 着色器/VFX 工具箱约定（占位）Toolkit primitives (placeholder convention)

> 本文引用的着色器原语统一指向 `docs/BOSS_REDO_V2/00_SHADER_VFX_TOOLKIT.md`。**该工具箱文档目前尚未建立**——在它落地前，下列原语名按本文定义理解，并在引用处标注"**见 shader toolkit**"。本模组已具备 `.fx → .fxc`（`Effects/CompileFX.ps1` + `fxc.exe`）的着色器管线，且已有屏幕级氛围着色器先例（`Effects/BloodSeaAtmosphere.fx` + `Systems/BloodSeaAtmosphereSystem.cs`），故下列原语在工程上**可行**。

约定的工具箱原语（toolkit primitives）：

| 原语代号 | 类型 | 说明 | 复用度 |
|---|---|---|---|
| `T-ScreenDistort` | 屏幕扭曲 | 热浪/折射式屏幕扭曲（窥视/全视/降临用），半径或全屏 | 高 |
| `T-RadialBloom` | 加性泛光 | 径向金色/元素色泛光，蓄力与爆发用 | 极高 |
| `T-PaletteLUT` | 颜色分级 | 屏幕级调色（季节/阴阳/涅槃的色调位移） | 高 |
| `T-BeamGrad` | 光束 | 带核心—边缘渐变的激光/射线着色器（替代纯矩形） | 极高 |
| `T-GroundDecal` | 地纹 | 地面投影的符文/落点/安全区贴花（半透明加性） | 极高 |
| `T-ReflectWard` | 护盾 | 六边形/玉环折射护盾（反射窗口用） | 中 |
| `T-Ribbon` | 拖尾 | 顶点带（蛇身/藤蔓/凤羽的高质量飘带） | 中 |
| `T-SkyOverlay` | 天空 | 全屏天空层（星图/眼穹/季节天幕/赤焰天） | 中 |
| `T-VignettePulse` | 暗角脉冲 | 边缘暗角随节拍脉冲（审判/凝视压迫感） | 高 |

> **MP/性能总则：** 所有屏幕级着色器仅在 `Main.netMode != Server` 客户端运行；以**本地玩家是否在 Boss 战场内**为开关；阶段过渡的全屏效果（distort/LUT/sky）用淡入淡出，避免长驻；`T-GroundDecal`/`T-BeamGrad` 走投射物绘制（已是各 Boss telegraph 投射物的现成载体）。逻辑判定（视线、安全缝、命中）始终在服务器/单机，与着色器解耦。

### 1.3 评级口径回顾

沿用 V1：本文 7 个 Boss 中——**神威 / 百目 / 玄武**为 GOOD（仅打磨 + 视觉升级，作 V2 视觉标杆）；**毗沙门 / 天庭观察者**刚完成 P0 一遍重做（V2 在其新结构上叠加）；**青龙 / 白虎 / 朱雀**仍是 V1 待办（共享四兽框架 + 各自签名 + 杀 FuryPatrol）；**大椿 / 树精**为 P2/P3 待办（机制深化 + 视觉）。

---

## 2. GOOD 模板的视觉升级 Reference Templates — Polish & Visual Uplift

> 这三者战斗设计已达标，**禁止改机制**。V2 只做**视觉拔高**，并作为其它 Boss 的"打磨样板"。

### 2.1 神威 Vigor — 断罪巨手刀将（GOOD · 打磨）

**1. 战斗幻想与身份：** 一柄会"判你有罪"的天界断罪巨刃——以姿态、连斩、反击惩罚你的贪输出，逼你像在和一位剑圣对峙。

**2. 阶段/幕叙事提升：** 现结构（试炼 → 裁决 → 天刑 + 连击递增 + 格挡反击）已优秀；V2 在**叙事可读性**上提升——为三阶段各定一个"罪名"主题色（试炼=素金、裁决=金蓝、天刑=赤金），让玩家从屏幕色调即知"罪行升级"。格挡反击窗口（`isCounterReady`）目前只有护盾脉冲绘制，V2 升级为**全屏暗角脉冲 + 时间微滞**的"反击预兆"，把"现在别打"的信号提到屏幕级。

**3. 签名 set-piece —「断罪判决（Verdict Strike）」：** 把 `Phase3_DivineExecution`（90 tick 蓄力 + 6 波刃墙）升格为本战高光：蓄力末帧全屏定格（hitstop 3~4 帧）+ 屏幕径向收束的金色符文（神威"宣判"），随后刃墙以**带核心渐变的 `T-BeamGrad` 刃**落下，每波附一次轻 hitstop。这是"判决"主题的戏剧顶点。

**4. 预告与可读性：** 沿用其符印（`RunicEnergyOrbs` 延时引爆）作为地面危险标记——V2 统一为 `T-GroundDecal` 暖金符文圈，引爆前 20 tick 由暗转亮。格挡姿态用**冷→暖**护盾颜色切换明确"可反击/已就绪"。

**5. 表现（着色器/VFX/运镜）：**
- 反击就绪：`T-VignettePulse`（暖金）+ 12Hz 护盾脉冲（已有）+ 短促弦乐 sting。
- 判决高光：蓄力末 hitstop + `T-RadialBloom` 收束 + `PunchCamera`（已有，加大到 30/15）。
- 阶段过渡：`T-PaletteLUT` 罪名色渐变（素金→金蓝→赤金），1 秒淡入。
- 连击≥3：刀身 `T-Ribbon` 金蓝飘带加亮（替代现纯色残影）。
- 音乐：保持 `Boss2`；V2 建议天刑阶段切更激烈段落（如有自定义 BGM）。

**6. 可行性与成本：** **S**。无新机制；新增 1 个 LUT（罪名色，可复用 `T-PaletteLUT`）、复用 `T-VignettePulse`/`T-RadialBloom`/`T-GroundDecal`。无新逻辑→无 MP 风险。

---

### 2.2 百目 / 天目 Argus — 预判独眼弓将（GOOD · 打磨，"全视"视觉标杆）

**1. 战斗幻想与身份：** 一只"早就看穿你下一步"的独眼——它射的不是你在哪，是你**将要**在哪；逼你做无规律走位。

**2. 阶段/幕叙事提升：** 现结构（审视 → 追猎 → 天目审判 + 预判射击 + 凝视锁定 + 瞬移）已是模组里"预测式 telegraph"的范本。V2 提升点：把 `DrawGazeLine`（紫尘瞄准线）升级为**真正的 `T-BeamGrad` 凝视预告线**，并让"天目"主题外溢到天空——三阶段开启**眼穹天幕**（`T-SkyOverlay`，满天闭合的眼睛缓缓睁开盯着玩家）。

**3. 签名 set-piece —「全视之域（All-Seeing Domain）」：** 现 `Phase3_AllSeeingDomain`（椭圆+瞳孔球阵→中心穿射）已是绝佳骨架；V2 把它做成本战签名——眼形球阵成形瞬间，天幕巨眼**同步睁开并锁定玩家**，瞳孔中心射出 `T-BeamGrad` 穿刺线，配 `T-ScreenDistort` 以巨眼为中心的轻微折射（"被注视"的物理化）。

**4. 预告与可读性：** 凝视锁定（`SniperDuel` 的 60 tick 蓄力）是模组最佳可读电报之一——V2 仅升级其视觉为渐亮的 `T-BeamGrad` 线 + 末段闪白，逻辑不动。预判箭保留现有"提前量"手感。

**5. 表现：**
- 凝视线：`T-BeamGrad`（紫）渐亮，蓄满闪白 + `Item122` sting。
- 全视之域：`T-SkyOverlay` 眼穹 + 巨眼锁定 + `T-ScreenDistort`（弱）。
- 阶段过渡：独眼睁开运镜（已有旋涡尘），加 `T-RadialBloom` 紫闪。
- 残影：已有紫蓝 `T-Ribbon` 风格，保留。

**6. 可行性与成本：** **S–M**。眼穹天幕（`T-SkyOverlay`）是唯一新美术资产；其余复用。无新逻辑。MP：天幕仅客户端绘制。

---

### 2.3 玄武 Xuanwu — Verlet 蛇龟·绝对防御（GOOD · 打磨，"玉反射"视觉标杆）

**1. 战斗幻想与身份：** 一座会**反弹你伤害**的北方水龟——蛇身缠绕、龟甲绝对防御，教你"该停手时停手"。

**2. 阶段/幕叙事提升：** 现结构（行走/跳砸 → 蛇击/冰风暴/双重突袭 → 绝对防御/潮涌/北辰审判/阴阳平衡）+ Verlet 蛇身物理 + 绝对防御反射，已是模组身体物理与反射窗口的标杆。V2 提升：把"绝对防御"（`Phase3_AbsoluteDefense`，k=0.5/c=12 钉死 + 受击反射冰锥）升级为**视觉化的"玉反射护罩"**——这是请求点名的 jade reflect ward。

**3. 签名 set-piece —「玉璧绝防（Jade Aegis）」：** 绝对防御期间，龟甲外覆**六边形玉色折射护罩（`T-ReflectWard`）**；玩家近战命中时护罩对应面板**亮起裂纹并迸射反射冰锥**（已有反射逻辑，仅加视觉反馈）。配合 `T-ScreenDistort` 在护罩表面的折射，让"打它=自伤"在视觉上不言自明。北辰审判（`NorthStarJudgment`）保留为另一高光，天空可加北斗星图 `T-SkyOverlay`。

**4. 预告与可读性：** 绝防进入前的钉死 + 护罩成形即是"停火"信号；V2 用护罩**冷蓝→玉青**的颜色确立"无敌中"。蛇身 `T-Ribbon` 升级让缠绕攻击的判定带更可读。

**5. 表现：**
- 绝对防御：`T-ReflectWard`（玉青六边形）+ 命中面板裂纹 + `T-ScreenDistort`（护罩局部）。
- 北辰审判：北斗 `T-SkyOverlay` + `T-BeamGrad` 星辉落柱。
- 阴阳平衡：`T-PaletteLUT` 黑白对比脉冲（已有阴阳主题）。
- 蛇身：现 Verlet ribbon 双层顶点绘制升级为 `T-Ribbon` 玉色渐变。

**6. 可行性与成本：** **M**。`T-ReflectWard` 为唯一较复杂新着色器（折射 + 面板亮起）。逻辑全部已存在。MP：护罩绘制客户端，反射判定服务器（已是）。

---

## 3. P0 重做之上的提升 Built on the P0 Reworks

> 毗沙门与天庭观察者刚完成 P0 一遍（已具备完整 FSM、签名机制、脚本化 P3）。V2 **在其新结构上叠加**作者化高光与视听，不重构机制。

### 3.1 毗沙门天王 Vaisravana — 财宝·宝塔·守护（P0 已重做 · V2 提升）

**1. 战斗幻想与身份：** 北方多闻天王手托宝塔、四塔环绕——他用财宝庇护自己、用守护反震你，逼你"去偷他的赐福"而非硬刚。

**2. 阶段/幕叙事提升：** 现结构（宝塔威光 → 天王降临 → 库藏封印 A/B/C 轮替 + 宝塔充能/赐福窃取 + 守护反击）已扎实。V2 强化"**财宝**"身份的体感：
- 让"赐福窃取"（玩家入塔赐福区偷充能）成为**视觉奖励事件**——窃取瞬间金币迸射 + `T-RadialBloom` 金闪 + 屏幕一角金光（强化"我抢到了"的爽感）。
- 三阶段战场升格为"**库藏开启**"：背景浮现金色经幢与悬浮宝物（装饰层），战场被金光笼罩。

**3. 签名 set-piece —「天王托塔·终极宝塔（Pagoda Apex）」：** 把 `Phase3_UltimateTower`（70 tick 蓄力 + 地纹 + 终极激光）做成本战顶点——蓄力期四塔金光汇入本体宝塔（`T-Ribbon` 金链把四塔连向中心），地面 `T-GroundDecal` 金色坛城符文逐格点亮，蓄满时宝塔顶射出**贯穿全屏的金色 `T-BeamGrad` 巨柱**，配 `T-RadialBloom` 金爆 + 强 hitstop + `T-VignettePulse`。这是"财神镇压"的戏剧高潮，且与现 70 tick 可读蓄力完全兼容。

**4. 预告与可读性：**
- 赐福区：四塔周围 `T-GroundDecal` 金色光圈（玩家知道"进这里能偷"），有充能时圈亮、被偷空时熄灭。
- 守护姿态：本体覆**金色护罩**（同玄武 `T-ReflectWard` 复用，色调改金），明确"此时输出会被反震"。
- 终极宝塔：70 tick 地纹坛城 + 蓄力金链，颜色暖金=危险。

**5. 表现：**
- 赐福窃取：`CoinPickup` 音（已有）+ `T-RadialBloom` 金闪 + 屏角金光。
- 守护反击：金色 `T-ReflectWard`（复用玄武护罩）+ 反震金镖（已有 `TreasureTowerOrb`）。
- 终极宝塔：`T-BeamGrad` 金柱 + `T-RadialBloom` + hitstop + `T-VignettePulse`。
- 阶段过渡：`T-PaletteLUT` 渐入金调；三阶段加金色经幢装饰 + 轻 `T-SkyOverlay` 天宫金幕。
- 音乐：保持 `LunarBoss`。

**6. 可行性与成本：** **S–M**。机制已全在；新增主要是视觉（金护罩=复用、金柱=`T-BeamGrad`、坛城地纹=`T-GroundDecal`）。MP：注意终极金柱与守护护罩仅客户端绘制，判定走现有投射物/`ModifyIncomingHit`。

---

### 3.2 天庭观察者 Celestial Overseer — 监视·全视·审判终局（P0 已重做 · V2 提升）

**1. 战斗幻想与身份：** 天庭派来的"监控之眼"——它持续**监视**你（被盯久了就被审判）、用**窥视相位**预演攻击、最后召**天庭陪审团**裁决你。

**2. 阶段/幕叙事提升：** 现结构（观测 → 攻击族 → 窥视相位 → 监视满槽审判 → 陪审团事件 → 三阶段全知循环）+ 监视槽 + 视线判定 + 裁决叠层，已是模组最有"信息战"身份的 Boss。V2 提升其"**被监控**"的压迫感与"**终局审判**"的仪式感：
- 监视槽（已有 12 点弧形 HUD）升级为**屏幕边缘暗角随槽升高而收紧**（`T-VignettePulse`），让"我正在被盯死"成为体感而非读数。
- 窥视相位的"假预告"用**冷灰半透 `T-GroundDecal`**（区别真攻击的暖色），并加 `T-ScreenDistort`（被"扫描"的折射），强化"它在预演"的诡异感。

**3. 签名 set-piece —「天庭陪审 / 审判标记（Tribunal & Verdict）」：** 两个互补高光：
- **陪审团事件（`JuryTrial`）：** 召唤瞬间天空降下**审判庭天幕**（`T-SkyOverlay` 庄严天宫 + 列柱），本体居中无敌、陪审 NPC 环绕，全屏 `T-VignettePulse` 收紧——把"清陪审 or 吃永久裁决叠层"做成有仪式感的限时审判。
- **监视满槽·审判射线（`MarkedForJudgment`）：** 满槽锁定瞬间全屏闪白 + `T-ScreenDistort` 以本体为中心收束 + 巨大 `T-BeamGrad` 审判光柱沿锁定方向贯穿，配 `Zombie104` 低吼（已有）。这是"全视终于看穿你"的处决感顶点。

**4. 预告与可读性：**
- 监视：天眼有视线时偏暖（危险）、无视线偏冷（已有绘制）；V2 加 `T-VignettePulse` 槽位反馈。
- 十字激光/凝视扫描/光柱阵：均已有 `OverseerGroundTelegraph`（线/光柱列/安全扇区三式）——V2 把它换成 `T-GroundDecal` 着色器版（更清晰的边缘与填充）。
- 审判射线：55 tick 锁定地纹（固定方向，不追踪）+ 渐强震屏（已有），加闪白。

**5. 表现：**
- 监视压迫：`T-VignettePulse`（冷蓝→暖红随槽）。
- 窥视相位：假预告 `T-GroundDecal`（冷灰）+ `T-ScreenDistort`（弱全屏）。
- 陪审团：`T-SkyOverlay` 审判庭 + 全屏暗角收紧 + `Item119` 召唤音（已有）。
- 审判射线：闪白 + `T-ScreenDistort` 收束 + `T-BeamGrad` 巨柱 + hitstop。
- 三阶段全知循环：`T-PaletteLUT` 冷调 + 天眼轨道加亮。
- 音乐：保持 `LunarBoss`；建议陪审团事件压低 BGM 突出仪式音效。

**6. 可行性与成本：** **M**。机制（监视/窥视/陪审/裁决/全知循环）全在。新增：审判庭 `T-SkyOverlay`（新美术）、`T-ScreenDistort`（请求点名的 scrying distortion，可全模组复用）、地纹着色器化。MP：所有屏幕效果客户端；监视/视线/陪审判定服务器（已是）。

---

## 4. 四圣兽统一升级框架 Four Sacred Beasts — Shared Elevated Framework

> 青龙/白虎/朱雀仍是 V1 待办（共享 `Patrol → GetRandomPhaseN → P3 FuryPatrol/FuryProwl/NirvanaFlight` 模板，纯随机喷弹，无 telegraph 子状态）。玄武已 GOOD（见 §2.3）。**V2 在 V1"杀 hub + 各给 1 签名"基础上，把四兽统一进一个可共享的"五行守护"框架，同时保留每兽签名。**

### 4.1 为何共享框架 Why a shared framework

四兽是**同一组遭遇（四方守护神兽）**，共享框架能：①一次性给三兽补齐 telegraph 子状态 + 确定性轮替（替代随机 hub）；②统一"五行/方位"视觉语言（青龙=东·木风雷·青、白虎=西·金·白银、朱雀=南·火·赤、玄武=北·水·玉黑）；③共享一套地纹/光束/天幕着色器（仅换色），把美术成本摊薄；④为可能的"四兽连战/合击"留接口。**关键：共享的是"骨架与表现层"，不是攻击内容——每兽各保留 1~2 个签名机制。**

### 4.2 共享骨架 `SacredBeastBase`（设计层，非强制同一基类）

统一约定（各�- 兽可在自己文件内实现，或抽公共 helper）：

1. **方位/五行身份常量：** `ElementColor`、`CardinalDir`、`SkyOverlayId`、主弹/副弹类型——驱动所有视觉与地纹着色。
2. **确定性轮替替代随机 hub：** 用"巡游枢纽 + 固定可读 rotation 数组"（参考毗沙门 `p1Index/p2Index` 轮替、观察者 `attackIndex` 轮换）替换 `GetRandomPhaseN`。**杀掉 `FuryPatrol/FuryProwl/Phase3_NirvanaFlight` 纯加速巡逻**，三阶段改为各兽**签名脚本幕循环**。
3. **统一 telegraph 子状态：** 每个攻击拆 `预告(SubState 0) → 释放(SubState 1)`，预告用本兽元素色 `T-GroundDecal`/`T-BeamGrad`，预告时长 ∝ 伤害。
4. **统一阶段过渡演出：** 入场降临 + 两次阶段过渡均走"元素色 `T-RadialBloom` + 元素 `T-SkyOverlay` 淡入 + `PunchCamera`"。
5. **统一签名节拍位（Signature Beat）：** 三阶段循环中固定插入该兽专属签名 set-piece（见各兽）。

### 4.3 青龙 Qinglong — 东方·木风雷·苍龙降世（MEDIOCRE · P1）

**1. 身份：** 蛇形飞龙，以风域位移 + 雷链连锁掌控空间——它不堵你，它**改变你能站的地方**。

**2. 阶段提升：** P1/P2 现有风刃+雷击轮换可读化（加 telegraph 子状态）；**杀 `Phase3_FuryPatrol`**，三阶段改为"苍龙降世"签名幕循环（风域 + 天罚雷网，脚本化而非加速巡逻）。

**3. 签名 set-piece —「风域天罚（Stormfield Judgment）」：** 划定 2~3 个**风域区**（`T-GroundDecal` 青色旋风圈）持续推/拉玩家走位（位移力场，非伤害），同时在风域**外**的安全区落天罚雷网（`QinglongThunderBolt` 多列，先 `T-GroundDecal` 落点预告再落雷）。机制 = "风把你往雷区推，你要逆风站位"。呼应 `WindserpentDao`/`ThunderclapLongbow` 武器主题。

**4. 预告：** 雷击落点用青色 `T-GroundDecal` 圆 + 30 tick 预告（替代现纯尘）；风域用旋转 `T-GroundDecal` 圈明确边界；风刃保留扇形但加发射前摇闪。

**5. 表现：** 雷=`T-BeamGrad`（青白）落柱 + `Thunder` 音（已有）；风域=`T-Ribbon` 旋风 + 轻 `T-ScreenDistort`；三阶段 `T-SkyOverlay` 雷云天幕 + `T-PaletteLUT` 青调；阶段过渡 `T-RadialBloom` 青爆 + 震屏（已有）。

**6. 可行性：** **M**。需新增风域位移逻辑（力场，服务器算）+ 落雷点预告。着色器全为元素换色复用。MP：力场对每客户端玩家本地施力或服务器同步速度，注意一致性。

### 4.4 白虎 Baihu — 西方·金·神虎降世（MEDIOCRE · P1）

**1. 身份：** 高速近战巨虎，以裂地冲击 + 银脉连斩压迫——它**冲进你脸**，逼你读冲刺方向。

**2. 阶段提升：** P1/P2 现有扑击/爪击/金属弹可读化（冲刺前加方向预告线）；**杀 `Phase3_FuryProwl`**，三阶段改"神虎降世"签名幕循环。现有 `Phase2_FuryCombo`（三段连招）是好骨架，应保留并扩成签名。

**3. 签名 set-piece —「裂地灭世爪（Riftclaw Cataclysm）」：** 升格 `Phase3_ExtinctionClaw`——白虎跃至高空，地面预告**多道平行裂地线**（`T-GroundDecal` 银白裂纹，呈"虎爪抓痕"形），落地瞬间沿抓痕迸发 `T-BeamGrad` 银脉冲击波 + 强 hitstop + 碎石。机制 = "站在爪痕之间的缝"。呼应 `AurelianCataclysmSmasher`。

**4. 预告：** 所有冲刺/扑击前加银白 `T-BeamGrad` 方向预告线（白虎是高速近战，方向可读是命门）；裂地用爪痕状 `T-GroundDecal`；音波弹保留。

**5. 表现：** 冲刺残影 `T-Ribbon`（银白）；裂地 `T-GroundDecal` 爪痕 + 落地 hitstop + `PunchCamera`（已有强震）；三阶段 `T-PaletteLUT` 冷银调 + `T-SkyOverlay` 肃杀白幕；虎啸用 `T-RadialBloom` 音波环。

**6. 可行性：** **S–M**。机制基本现成（冲刺+连招+裂地已有雏形），主要加方向预告 + 爪痕地纹 + hitstop。着色器全复用换色。

### 4.5 朱雀 Suzaku — 南方·火·涅槃重生（MEDIOCRE · P1，含两处必修）

**1. 身份：** 浴火凤凰，以涅槃重生改写战斗节奏——它**死过一次会更强**，重生时刻是全战核心。

**2. 必修（来自 V1）：**
- **换占位弹：** 当前 `FireProjectile` 用 `ProjectileID.InfernoFriendlyBlast`（**原版占位**，违反 §2.3）→ 必须换朱雀焰羽自定义弹。
- **保留并强化 `CheckDead` 涅槃重生**（已实装：首次 10%/3 阶段触发恢复 20% + `Phase3_Rebirth`）——这是 V1 认可的好概念，V2 围绕它做签名。
- **杀 `Phase3_NirvanaFlight` 纯巡逻压制**，重生后改为"涅槃形态"的新攻击族（规则变化，非加速）。

**3. 签名 set-piece —「涅槃重生（Nirvana Rebirth）」：** 把现 `Phase3_Rebirth`（120 tick）做成全战戏剧顶点——朱雀坠落成灰（`T-PaletteLUT` 全屏褪色至灰烬），短暂"死亡"沉默（BGM 压低），随后从灰中爆燃复生：全屏 `T-PaletteLUT` 由灰转赤 + `T-RadialBloom` 赤金爆 + `T-SkyOverlay` 赤焰天幕 + 双层同心焰环（已有弹幕）。复生后进入**涅槃形态**（攻击族改变：焰羽 + 太阳坠落，更快但有清晰新 telegraph）。这是"输出阶段→处决失败→它复活更强"的情绪过山车。

**4. 预告：** 太阳/火柱落点 `T-GroundDecal`（赤）；俯冲 `T-BeamGrad` 方向线；重生有明确"灰烬沉默 → 爆燃"的两段可读节拍。

**5. 表现：** 焰羽弹 `T-Ribbon` 火尾；重生 `T-PaletteLUT`（灰↔赤）+ `T-RadialBloom` + `T-SkyOverlay` 赤焰天 + hitstop + 大震屏（已有）；常态火光 `Lighting` 已有。音乐：重生前压低、复生时回归（强烈对比）。

**6. 可行性：** **M**。重生机制已在；主要工作：①换自定义焰羽弹（必修）；②重生 set-piece 的 LUT/sky/泛光编排；③涅槃形态新攻击族。MP：`CheckDead` 重生逻辑已服务器侧，注意同步 `didRebirth`/血量。

### 4.6 玄武 Xuanwu — 见 §2.3（GOOD，作四兽视觉标杆）

玄武已达标，作为四兽框架的**视觉与反射机制标杆**：其 Verlet 蛇身 `T-Ribbon`、玉反射护罩 `T-ReflectWard`、北辰 `T-SkyOverlay` 是另三兽元素换色复用的来源。四兽统一的"元素色 + 方位天幕 + 反射/护罩语汇"以玄武为基准。

### 4.7 四兽共享成本小结

| 共享件 | 内容 | 一次做、四处用 |
|---|---|---|
| `T-GroundDecal`（换色） | 落点/裂纹/风域/坛城地纹 | ✔ 四兽 + 毗沙门/观察者 |
| `T-BeamGrad`（换色） | 雷柱/银脉/焰柱/星辉 | ✔ 全部 Boss |
| `T-SkyOverlay`（换色） | 雷云/白幕/赤焰/北斗 | ✔ 四兽 |
| `T-PaletteLUT`（换色） | 元素调色/重生灰赤 | ✔ 四兽 + 神威 |
| telegraph 子状态骨架 | 预告→释放 | ✔ 青/白/朱（玄武已有） |
| 确定性轮替 helper | 替代随机 hub | ✔ 青/白/朱 |

---

## 5. 大椿与树精 Dazheng & Dryads（自然/季节/潜地）

### 5.1 大椿 Dazheng — 上古树神·四季轮转（MEDIOCRE · P2）

**1. 战斗幻想与身份：** 一棵破土而起、撑天的上古神木——固定不动，却用**四季轮转**重塑整片战场，让你在它的季节里求生。

**2. 阶段/幕叙事提升：** 现有出色的"破土升起"入场（4 段时间轴）+ 收缩竞技场屏障（`DazhengArenaBarrier` P1→P2 半径收缩）值得保留；但战斗本体是**静态弹幕地狱 + 随机 hub + `Phase2_FuryPatrol`**。V2 按 V1 方向"季节/锚点解谜"重构，并**视觉上做成模组里最具色彩冲击的一战**：
- **四季锚点机制（签名，见下）替代随机 hub。**
- **杀 `Phase2_FuryPatrol`**，二阶段改为"季节加速轮转 + 锚点更难"。
- 现有藤蔓迷宫/藤蔓墙/落叶/黄金幻象作为各季节的"弹幕方言"复用（春=藤蔓生长、夏=落叶/金幻象繁盛、秋=金叶凋零、冬=枯枝尖刺），每季只用对应子集，避免一锅乱炖。

**3. 签名 set-piece —「四季轮转（Cycle of Seasons）」：** 战场四角各一**季节锚点**（春绿/夏金/秋橙/冬蓝，`T-GroundDecal` 季节符），大椿按当前"主导季节"驱动对应弹幕方言；玩家可**激活/破坏锚点**改变主导季节，从而切换 Boss 弹幕规则与战场调色——`T-PaletteLUT` 让整屏随季节变色（春青→夏金→秋橙→冬白蓝），`T-SkyOverlay` 同步换季节天幕。这是请求点名的 seasonal palette shift，且把"静态弹幕地狱"转成"用季节解谜控制战斗"的主动玩法。

**4. 预告与可读性：** 藤蔓墙缝隙保留（已留 gap）；落点/根须 `T-GroundDecal`；锚点状态用颜色明示（亮=激活、灰=可破）；季节切换有 1 秒过渡演出（明确"规则要变了"）。

**5. 表现：** `T-PaletteLUT` 四季调色（核心卖点）+ `T-SkyOverlay` 季节天幕；藤蔓/根须升级 `T-Ribbon`；黄金幻象 `T-RadialBloom` 金闪；入场破土保留并加 `T-GroundDecal` 地裂；阶段过渡屏障收缩配 `T-VignettePulse`。音乐：保持 `Boss2`，季节切换可叠环境音色。

**6. 可行性：** **M–L**。季节锚点是新机制（锚点实体 + 主导季节状态 + 弹幕方言切换 + 玩家交互），是本文最大单项逻辑工作。着色器（LUT/sky）为核心新美术。MP：锚点状态需服务器同步，季节切换为权威事件。

### 5.2 树精 Dryads — 潜地伏击树妖（MEDIOCRE · P3，大椿弱化版）

**1. 战斗幻想与身份：** 大椿的"幼体"——会**潜入地下、从你脚边冒出**的伏击树妖；潜地是它唯一的、也是最该被放大的特色。

**2. 阶段/幕叙事提升：** 现有潜地机制（下沉→地下移动→玩家附近冒出 + 冒出前地面绿尘预警）是唯一亮点，其余通用、**P2 ≈ P1 更快**。V2 按 V1 方向"围绕潜地扩展 + 差异化 P2"，作为**低成本打磨**：
- **冒出升格为伏击 set-piece**（见下）。
- P2 引入**活木墙生长**改变场地（新机制），而非单纯提速。

**3. 签名 set-piece —「潜地伏击（Ambush Surface）」：** 冒出前的地面预警（已有绿尘 + 微震）升级为清晰的 `T-GroundDecal` 绿色"根须裂纹"圈（明确"它要从这里出来，快走开"），冒出瞬间根须放射爆发（已有 `DryadsVine` 环）+ `T-RadialBloom` 绿闪 + hitstop + `PunchCamera`（已有）。把"猜它从哪冒"做成可读、有冲击的伏击对决。

**4. 预告与可读性：** 冒出点 `T-GroundDecal` 圈（替代纯尘）；根须爆发方向沿冒出点放射；刺球/落叶保留。

**5. 表现：** 潜地/冒出 `T-GroundDecal` + `T-RadialBloom` 绿闪 + hitstop；活木墙 `T-Ribbon`；常态落叶 `Lighting`/dust 已有。无需 sky/LUT（保持低成本）。

**6. 可行性：** **S–M**。潜地已实装，主要加冒出点地纹 + 活木墙 P2 新机制。着色器复用大椿件换色。MP：潜地/冒出已服务器侧（`burrowTargetPos`、`anchorPosition` 同步）。

---

## 6. 统一可读性与表现语汇 Unified Readability & Presentation Language

> 跨全部七 Boss 强制统一，确保"提升"不变成"视觉噪音"。

### 6.1 颜色编码 Color coding
- **危险**：暖金 / 暖红（落点、危险射线、敌意地纹）。
- **安全**：冷蓝 / 玉青（安全缝、安全扇区、反射/守护护罩、赐福区）。
- **元素**：青龙青 / 白虎银白 / 朱雀赤 / 玄武玉黑 / 大椿随季节 / 观察者金（视线时偏暖）/ 毗沙门金。

### 6.2 形状编码 Shape coding
- **线**（`T-BeamGrad`/`T-GroundDecal` 线）= 射线/冲刺方向。
- **圆**（`T-GroundDecal` 圆）= 落点/落雷/冒出点。
- **扇**（安全扇区贴花）= 安全缝。
- **环/裂纹** = 收缩弹幕 / 裂地。

### 6.3 时间编码 Time coding
- 预告时长 ∝ 伤害：小压制弹 ≤20 tick；中等攻击 ~35–55 tick；处决级大招（终极宝塔/审判射线/灭世爪/涅槃）60–90 tick + 渐强震屏 + 蓄力泛光。

### 6.4 高光时刻通用配方 Set-piece recipe
`长蓄力(渐强 T-RadialBloom + 渐强震屏) → 末帧 hitstop(3–4f) → 释放(T-BeamGrad + T-VignettePulse + 大 PunchCamera) → 短余波`。所有处决级签名共用此配方，保证"重击感"一致。

---

## 7. 每 Boss 优先级 / 工作量总表 Per-Boss Priority & Effort

> 成本：**S**=纯视觉/换色复用 · **M**=少量新逻辑 + 新着色器 · **L**=显著新机制。
> 优先级延续 V1（P0=刚重做需叠加、P1=核心待办、P2/P3=深化/打磨）。GOOD 三兽为视觉打磨（标 —）。

| Boss | V1 评级/优先级 | V2 提升核心 | 签名 set-piece | 新着色器需求 | 成本 | MP/性能注意 |
|---|---|---|---|---|---|---|
| 神威 Vigor | GOOD · — | 罪名色调 + 反击屏幕预兆 + 判决 hitstop | 断罪判决 Verdict Strike | `T-PaletteLUT`(复用) | **S** | 全客户端绘制，无新逻辑 |
| 百目 Argus | GOOD · — | 凝视线着色器化 + 眼穹天幕 | 全视之域 All-Seeing Domain | `T-SkyOverlay`(眼穹,新) | **S–M** | 天幕仅客户端 |
| 玄武 Xuanwu | GOOD · — | 玉反射护罩 + 北斗天幕 | 玉璧绝防 Jade Aegis | `T-ReflectWard`(新) | **M** | 反射判定已服务器 |
| 毗沙门 Vaisravana | P0(已做)·叠加 | 财宝体感 + 库藏开启场 | 终极宝塔 Pagoda Apex | `T-BeamGrad`/金护罩(复用) | **S–M** | 金柱/护罩仅客户端 |
| 天庭观察者 Overseer | P0(已做)·叠加 | 监视压迫 + 审判仪式 | 天庭陪审 + 审判射线 | `T-ScreenDistort`(新)+审判庭天幕 | **M** | 屏幕效果客户端；判定已服务器 |
| 青龙 Qinglong | MEDIOCRE · P1 | 杀 FuryPatrol + 风域天罚 | 风域天罚 Stormfield Judgment | 元素换色复用 | **M** | 风域力场需同步一致性 |
| 白虎 Baihu | MEDIOCRE · P1 | 杀 FuryProwl + 方向预告 | 裂地灭世爪 Riftclaw Cataclysm | 元素换色复用 | **S–M** | 客户端绘制为主 |
| 朱雀 Suzaku | MEDIOCRE · P1 | 换占位弹(必修) + 围绕重生 | 涅槃重生 Nirvana Rebirth | `T-PaletteLUT`/`T-SkyOverlay`(换色) | **M** | `CheckDead` 重生已服务器，同步血量 |
| 大椿 Dazheng | MEDIOCRE · P2 | 四季锚点解谜 + 杀 FuryPatrol | 四季轮转 Cycle of Seasons | `T-PaletteLUT`/`T-SkyOverlay`(四季) | **M–L** | 锚点/主导季节需服务器权威 |
| 树精 Dryads | MEDIOCRE · P3 | 潜地伏击放大 + P2 活木墙 | 潜地伏击 Ambush Surface | 复用大椿件换色 | **S–M** | 潜地已服务器侧 |

### 7.1 共享前置（一次做、全局收益）Shared prerequisites
1. **`00_SHADER_VFX_TOOLKIT.md` + 工具箱原语落地**（`T-GroundDecal`/`T-BeamGrad`/`T-RadialBloom`/`T-PaletteLUT`/`T-ScreenDistort`/`T-ReflectWard`/`T-SkyOverlay`/`T-VignettePulse`/`T-Ribbon`）——本文所有 Boss 的最大公约数，**应先于单 Boss 实装**。
2. **四兽共享骨架**（telegraph 子状态 + 确定性轮替 + 元素色/方位天幕）——一次做覆盖青/白/朱。
3. **统一可读性语汇**（§6 颜色/形状/时间编码）——验收基准。

### 7.2 建议批次 Suggested batching
- **批 V2-A（地基）：** 工具箱原语 + 四兽共享骨架 + 统一语汇。
- **批 V2-B（P1 主力）：** 青龙/白虎/朱雀（含朱雀换占位弹必修）——依赖 V2-A。
- **批 V2-C（P0 叠加）：** 毗沙门 / 观察者视听高光——独立，可早做（机制已在）。
- **批 V2-D（打磨标杆）：** 神威 / 百目 / 玄武视觉升级——独立、低风险，可作工具箱验证用例。
- **批 V2-E（深化）：** 大椿四季锚点（最大逻辑项）/ 树精潜地伏击。

---

## 8. 验收基准 Acceptance Criteria

每个 Boss V2 交付前逐项自检：
1. **不回退反模式：** 未引入任何"低血量=加速喷弹"；`FuryPatrol/FuryProwl/NirvanaFlight` 纯加速巡逻已被签名脚本幕替代（青/白/朱/大椿）。
2. **签名 set-piece 存在且主题贴合：** 该 Boss 至少 1 个手工编排、与主题强绑定的高光时刻，遵循 §6.4 配方。
3. **可读性达标：** 所有攻击有 telegraph，且遵守 §6 颜色/形状/时间编码；处决级大招预告 ≥60 tick。
4. **GOOD 未改坏：** 神威/百目/玄武机制零改动，仅视觉叠加。
5. **MP/性能安全：** 屏幕级着色器仅客户端、随战场开关、过渡淡入淡出；逻辑判定全部服务器/单机；无长驻全屏 distort/LUT。
6. **占位清除：** 朱雀 `InfernoFriendlyBlast` 等原版占位弹已换自定义主题弹。

---

*Primordial / 洪荒 · 天庭非龙系 Boss 第二迭代（V2）· 实装前先读 §1 工具箱约定与 §6 统一语汇，并确认 §7.1 共享前置已就绪。*
