# Boss 战斗二次迭代总规划（Boss Combat Redo Master Plan · V2）

> **文档性质：** 全 Boss **第二迭代（V2 / 设计师级演出层）的唯一调度蓝图**（Master Dispatch Blueprint）
> **版本：** 1.0 · 2026-06-27
> **来源合并：** `docs/BOSS_REDO_V2/` 下五份子文档（00 工具箱 + 01 NPCs + 02 龙系 + 03 天庭其余 + 04 地府）
> **上游基准：** `docs/BOSS_REDO_PLAN.md`（V1 一审，修反模式 + 立骨架） · `docs/PROGRESSION_DESIGN_SPEC.md`（进度/数值，不被本文覆盖）
> **硬约束：** 本文是**规划与调度**，不改任何 Boss/武器代码；落地实现以本文 §5 分期、§6 全局契约为派发与验收依据。

---

## 1. 概述 Overview

### 1.1 V2 是什么 What V2 Is

V1（`BOSS_REDO_PLAN.md`）回答的是「**这场战斗能不能玩**」——它杀掉了「低血量=加速喷弹幕」反模式、拆了换皮链、接线了死状态（DarkRitual / Enraged）、给每个 Boss 补了真实 FSM 与 telegraph。

**V2 回答的是「这场战斗值不值得记住」。** 它在 V1 已修正的骨架之上，把每场战斗从「合格」拔高为**有作者签名、有视觉震撼、有镜头语言的遭遇战**，并补齐审计反复点名却始终缺席的**身份层**（尤其地府的 DoT / 冥律 / 怨念）。

| 维度 | V1（`BOSS_REDO_PLAN.md`） | V2（本程序） |
|------|--------------------------|--------------|
| 阶段 | 过阈值改规则、脚本化幕 | 幕之间有戏剧弧（起→承→转→合）、作者化高潮 |
| 攻击 | 每招有 telegraph | telegraph 升级为**全模组统一可读语言**（色/形/时） |
| 主题 | ≥1 个签名机制 | 签名机制升级为**签名 set-piece**（场地改造/脚本序列/交互谜题） |
| 表现 | dust + 屏震 + 自定义弹 | **着色器滤镜 / 溶解 / 扭曲 / 图元拖尾 / 地纹 / 镜头**，且不遮挡可读 |

### 1.2 与 V1 的关系 Relationship to V1

- **V2 不推翻 V1，只在其上「升格」。** 所有 V1 §3 的 9 条硬性原则在 V2 里**继续有效**，是 V2 验收的下限。
- **V1 的反模式不得回退。** 任何 V2 实现若重新引入「低血量=加速喷弹」即判不合格（见 §6.5）。
- **GOOD 模板（V1 §4）继续为红线。** 赢勾/旱魃/敖广/敖顺/神威/百目/玄武/黑白无常/冥狐——V2 只许加表现层，禁止改坏其战斗结构；它们反过来是 V2 着色器原语的**低风险首发验证用例**。
- **进度/数值仍以 `PROGRESSION_DESIGN_SPEC.md` 为准**，V2 与之互不覆盖。

---

## 2. 文档地图 Doc Map

> 五份子文档构成 V2 的完整设计内容；本文（`BOSS_REDO_PLAN_V2.md`）是它们的**索引、去重与调度层**。

| 子文档 | 作用域 Scope（一句话） | 行数 |
|--------|------------------------|------|
| `BOSS_REDO_V2/00_SHADER_VFX_TOOLKIT.md` | 着色器编译/加载流水线（`Effects/CompileFX.ps1`）、可复用 VFX/着色器原语目录、演出规范与性能护栏、新 `.fx` 愿望清单——**唯一权威技术规格** | 353 |
| `BOSS_REDO_V2/01_NPCS_BOSS_V2.md` | `NPCs/Boss/` 全 10 实体（含劫云三色合一）的 V2 逐 Boss 设计 + 本区共享语言 + 着色器汇总 | 482 |
| `BOSS_REDO_V2/02_CELESTIAS_DRAGONS_V2.md` | 天庭龙系 6 条（敖广/敖钦/敖闰/敖顺/天御金龙/祖龙残魂(天庭)）V2 设计 + 元素屏幕染色统一语言 | 334 |
| `BOSS_REDO_V2/03_CELESTIAS_OTHERS_V2.md` | 天庭非龙系 10 个（神威/百目/毗沙门/观察者/四圣兽/大椿/树精）V2 设计 + 四圣兽共享框架 | 357 |
| `BOSS_REDO_V2/04_UNDERWORLD_V2.md` | 地府线 7 个（怨灵/尸骸/幽冥龙/妖狐/无常/觉醒冥龙/阴天子）V2 设计 + 地府身份层（`UnderworldField`）+ 着色器复用矩阵 | 318 |

> **阅读顺序建议：** 实现任一 Boss 前，先读 `00_SHADER_VFX_TOOLKIT.md` §C（演出规范）+ §C.4（性能护栏），再读该 Boss 所属区域子文档对应小节，最后回查本文 §4 优先级与 §5 批次。

---

## 3. 着色器先行 Shader-First Strategy

> **核心判断（来自工具箱 §0/§D）：** 约 80% 的 V2 演出可由仓库**已实现原语** + vanilla `Filters.Scene` 达成；真正需要新写的 `.fx` 集中在少数**跨区共享**的通用着色器。先做这批共享件，是整个 V2 的吞吐瓶颈与最高 ROI。

### 3.1 命名去重：四区文档 → 唯一规范着色器目录 Canonical Shader Catalog

四份区域文档各自取了不同的占位命名（龙系按元素、天庭其余用 `T-*`、地府用概念名）。下表把它们**合并为一套规范 `.fx` 目录**，供 toolkit 统一实现；实现侧回填后，各区文档的别名一律指向规范名。

| 规范着色器 Canonical `.fx` | 类型 | 四区文档别名（去重映射） | 主要消费者 Consumers | 复用计数 | 状态 |
|----------------------------|------|--------------------------|----------------------|:--------:|------|
| **`DissolveBurn`**（溶解/灼烧） | Boss 贴图单 pass（噪声 clip + 灼烧边） | 工具箱 `DissolveBurn`(B.10/D-P0)；NPCs `Dissolve`；地府 `soul-dissolve` | 召唤/瞬移/分身现形/相变/死亡——几乎全 Boss；地府 BAW(首发)/怨灵/尸骸/妖狐/觉醒龙/阴天子 | **极高（~全部）** | **新 .fx · P0** |
| **`GenericWarp`**（泛化主题扭曲） | 全屏后处理（噪声 UV 偏移 + 色散 + 径向衰减，主题可换） | 工具箱 `GenericHeatWarp`(D-P0)/`VoidCollapse`(D-P2)；NPCs `Distortion`；天庭其余 `T-ScreenDistort`；龙系 水下折射/热浪/太初扭曲；地府 `nether-fog-distortion`/`rift-warp` | 冲撞/砸地/俯冲热浪、虚空塌缩、冥雾、次元裂隙、海下折射 | **极高** | **新 .fx · P0**（重构 `XuanwuFrostDistortion` 暴露参数；fog/rift/void 为同族变体） |
| **`ElementalScreenTint`**（元素屏幕染色） | 全屏氛围 overlay（色/强度参数化） | 龙系 `ElementalScreenTint`（潮汐/热浪/风暴压暗/金芒/太初，6 龙共用）；地府 `grudge-desaturation`/`yin-yang-split`（变体） | 6 条龙的元素底色；地府怨念褪色、阴阳分屏 | **高** | **新 .fx**（由 `BloodSeaAtmosphere` 模板调色；6 龙传色即可） |
| **`PaletteLUT`**（屏幕调色） | 全屏 LUT/色相位移 | 天庭其余 `T-PaletteLUT`（罪名色/四季/涅槃灰赤）；地府 `yin-yang-split`/`grudge-desaturation` | 神威罪名色、大椿四季、朱雀涅槃灰↔赤、阴天子/无常阴阳分屏 | **高** | **新 .fx**（与 `ElementalScreenTint` 可共底；tint=加色，LUT=重映射） |
| **`GroundDecal` / `ArenaRunic`**（地纹/法阵/牢笼） | 屏幕空间 SDF + 噪声（投射物载体绘制） | 工具箱 `ArenaRunic`(D-P1)；NPCs `Arena Decal`；天庭其余 `T-GroundDecal`；地府 `prison-overlay`（牢笼变体） | 后卿疫斑/将臣雷牢/苍龙审判格/牛马领域/黑熊裂地/四兽落点风域/毗沙门坛城/观察者地纹/尸骸引魂阵/阴天子镇魂狱 | **极高** | **新 .fx**（复用 `DazhengArenaCircle.fx` 换色 + 符文频率） |
| **`BeamGrad` / `BeamFlow`**（光束梯度） | TriangleStrip 直带 + 核心/边缘渐变 + 流动 UV | 工具箱 `BeamFlow`(D-P2)；NPCs `Beam`；天庭其余 `T-BeamGrad`；龙系雷链/星辉 | 苍龙落雷柱/将臣链电/劫云终雷/牛马锁链/四兽雷柱银脉焰柱星辉/毗沙门金柱/观察者审判射线 | **极高** | **新 .fx**（**旱魃 `HanbaLaser` 已有现成参考实现，抽象即可**） |
| **`ReflectWard`**（折射护盾） | 六边形/玉环折射护罩 + 面板亮起 | 天庭其余 `T-ReflectWard` | 玄武玉璧绝防（首发）、毗沙门金护罩（换色复用） | 中 | **新 .fx** |
| **`RadialBloom`**（径向泛光） | 加性径向泛光 + 阈值 bloom | 工具箱 golden bloom；天庭其余 `T-RadialBloom`；龙系金色泛光 | 蓄力/爆发/重生/夺宝/天剑雨/各处决配方 | 高 | **新 .fx** |

#### 既有可直接复用（非新建）Existing / CPU primitives — 优先复用

| 原语 | 实现 | 别名 | 备注 |
|------|------|------|------|
| **图元拖尾 Primitive Trail / `T-Ribbon`** | `ACMUtils.BuildRibbonStrip`（CPU 顶点，**已实现**，无 `.fx`） | 龙身/蛇身/刀光/锤/尾焰/狐火尾 | **首选拖尾方案**；替换大量「逐帧叠贴图」残影 |
| **程序化天幕 Sky Shader / `T-SkyOverlay`** | `AncestralDragonSky.fx`（**已存在**，复制改色板） | 各区域大 Boss 专属天幕 | 雷暴天幕族（苍龙/将臣/Archosaur/劫云）可参数化共用 |
| **入场/相变滤镜 Intro Filter / `T-VignettePulse`** | vanilla `Filters.Scene("FilterMiniTower")`（**已可用**） | 去饱和/暗角/反击预兆/审判压迫 | 需程序化图案再升级为 `GenericWarp`/`ElementalScreenTint` |
| **冲击波环 Shockwave / 充能光环 Charge Glow** | `Xuanwu.DrawShockwaveRing` / 多层 `SoftGlow`（**已实现**） | 落地/爆发命中环、蓄力辉 | 要真实折射再叠 `GenericWarp` |
| **粒子版天幕 Custom Sky (Particle)** | `HeavenlyEffect.cs`（**已实现**） | 低端/演出降级 fallback | — |

### 3.2 推荐授权顺序 Recommended Authoring Order（按 ROI）

1. **`DissolveBurn`**（P0）——复用率最高，几乎所有 Boss 的召唤/分身/死亡都要；单 pass，复用 `GenerateTileableFBM` 噪声，成本小。
2. **`GenericWarp`**（P0）——重构 `XuanwuFrostDistortion` 暴露 `uTint/uChroma/uWarpScale`，一份覆盖 火/冰/雷/虚空/雾/裂隙 全主题。
3. **`ElementalScreenTint`**（+ `PaletteLUT` 同底）——`BloodSeaAtmosphere` 调色，6 龙 + 天庭罪名色 + 地府阴阳/褪色共用。
4. **`GroundDecal`/`ArenaRunic`**——复用 `DazhengArenaCircle.fx`，覆盖全部场地法阵/落点/牢笼。
5. **`BeamGrad`/`BeamFlow`**——从 `HanbaLaser` 抽原语。
6. **`RadialBloom`**——蓄力/爆发/处决通用。
7. **`ReflectWard`**——较复杂（折射 + 面板），玄武首发后供毗沙门换色。

### 3.3 验证方法 Validation Approach（先在低风险 GOOD Boss 上原型）

> 每个新 `.fx` 都遵循「**低风险 GOOD Boss 首发验证 → 终局 Boss 收口**」，先把着色器在零逻辑风险处跑通，再上高价值/高复杂 Boss。

| 规范着色器 | 首发验证 Boss（低风险） | 后续收口/复用 Boss |
|------------|------------------------|--------------------|
| `DissolveBurn` | **黑白无常 BAW**（复活/出场，纯视觉、零 AI 改动） | 怨灵/尸骸/妖狐/觉醒龙/阴天子 + 全 Boss 死亡 |
| `GenericWarp` | **敖闰 Aoyuan**（霜冻折射，`XuanwuFrostDistortion` 已存在，逻辑零改） | 苍龙/牛马/黑熊冲击；幽冥龙(fog)/觉醒龙(rift)；敖钦(heat)/敖广(refraction) |
| `ElementalScreenTint`/`PaletteLUT` | **敖闰/敖广**（GOOD 龙，纯接线） | 6 龙 + 神威/大椿/朱雀 + 地府阴阳/褪色 |
| `GroundDecal`/`ArenaRunic` | **尸骸 Corpses**（引魂阵，`prison-overlay` 首发） | 全区场地机制 + 阴天子镇魂狱收口 |
| `BeamGrad` | **旱魃 Hanba**（其 `HanbaLaser` 即现成参考，抽原语） | 全区光束/激光/雷柱 |
| `ReflectWard` | **玄武 Xuanwu**（玉璧绝防，反射逻辑已存在） | 毗沙门金护罩 |
| `yin-yang-split`(LUT 变体) | **黑白无常 BAW**（协同分屏首发） | 阴天子审判庭收口 |
| `rift-warp`(Warp 变体) | **幽冥龙 Nether Dragon**（传送门首发） | 觉醒冥龙裂隙/奇点收口 |

### 3.4 关键前置修复 Key Prerequisite Fixes（着色器之外、阻塞身份层）

> 这两项不是「演出」，而是 V2 身份层与占位清除的**硬前置**，须在对应区域 Boss 开工前落地。

1. **接线 `UnderworldPlayer.UnderworldEffect` 为真实场（`UnderworldField`）。**
   现状：7 个地府 Boss 的 `AI()` 几乎都首行写 `UnderworldPlayer.UnderworldEffect = true;`，但 `UnderworldPlayer` 仅有一个**空 `bool` 字段 + `ResetEffects()`，没有任何机制挂在上面**——即 V1 §2.5「地府 DoT/冥律/怨念完全缺席」的逐字证据。
   **必须**把它升级为可被 Boss 调制（强度/层数/视觉，统一 0–1 标量驱动）的真实战斗身份层 `UnderworldField`，承载三轴：**冥律标记 Nether Decree / 魂蚀 DoT Soul-Erosion / 怨念账 Grudge Ledger**（详见 `04_UNDERWORLD_V2.md` §0.2）。这是全地府 V2 的**第一优先**，先于任何地府演出。

2. **替换剩余的原版占位投射物 Replace remaining vanilla placeholder projectiles。**
   V1 §2.3 已点名禁用原版 Boss 弹；V2 须确认全部清除，**重点核查**：
   - **苍龙真身 Azure Dragon**：满屏 `CultistBossLightningOrb*`（吐息/雷球/矩阵/审判/俯冲）→ 自定义**雷弹/风弹**（青蓝 + 电弧 + 拖尾）。
   - **朱雀 Suzaku**：`FireProjectile` 用 `ProjectileID.InfernoFriendlyBlast`（原版占位）→ 朱雀**焰羽自定义弹**。
   - 连带核查（同属 §2.3）：**九尾 Kyuubi** `CultistBossFireBall*` → 狐火弹；**幽冥妖狐 Nether Kitsune** `CultistBossLightningOrb` → 幽冥狐火弹。

---

## 4. 全 Boss V2 优先级总表 Master Priority Table

> 一表覆盖四区全部 33 个战斗实体（劫云三色合一计 1）。
> **是否已 P0 重做：** ✅=V1 已完成 P0 结构重写（V2 只叠加演出）；🔧=正由其它 agent 实施 P0（IN FLUX，V2 在 intended P0 之上规划）；—=未做 P0 / 非 P0 Boss。
> **新着色器需求** 已映射到 §3.1 规范名。工作量 S/M/L 为 V2 **增量**（不含 V1 已完成部分）。

| 名称 | 区域 | v1 评级 | 已 P0 | v2 招牌 set-piece（一句话） | 新着色器需求（规范名） | 优先级 | 工作量 |
|------|------|---------|:-----:|------------------------------|------------------------|:------:|:------:|
| 赢勾 Yingou | NPCs | GOOD | — | 刀地狱刀光残痕（纯视觉升格） | `DissolveBurn`·`RadialBloom`(复用) | — | S |
| 旱魃 Hanba | NPCs | GOOD | — | 占位贴图替换 + 激光抽 `BeamGrad` 原语 | `BeamGrad`(**反哺工具箱**) | — | S |
| 后卿 Hoqing | NPCs | POOR | ✅ | 万鬼夜行经络高潮（疫斑逼迁场地） | `GroundDecal`·`DissolveBurn` | P0 叠加 | M |
| 将臣 Jiangcen | NPCs | POOR | ✅ | 雷牢降临 + 镜像锤魂 | `GroundDecal`·`BeamGrad`·`DissolveBurn` | P0 叠加 | M |
| 祖龙残魂(地表) Archosaur | NPCs | POOR | — | 残魂分裂 + 破绽窗口（破替身雷龙 Raid） | `T-Ribbon`·雷暴 Sky·`DissolveBurn` | P1 | L |
| 牛头马面 NiuMa | NPCs | MEDIOCRE | — | 勾魂锁命连携 + 同伴复生 | `GroundDecal`·`BeamGrad`·地府 Sky | P2 | M |
| 九尾 Kyuubi | NPCs | MEDIOCRE | — | 狐影九重 + 九方向同刺 | `DissolveBurn`·`T-Ribbon`·狐火 glow（**换占位弹**） | P2 | M |
| 苍龙真身 Azure Dragon | NPCs | MEDIOCRE | — | 网格化雷霆审判庭 + 风域 | 雷暴 Sky·`GroundDecal`·`BeamGrad`·`GenericWarp`（**换占位弹**） | P1 | L |
| 劫云 ×3 Tribulation | NPCs | MEDIOCRE | — | 三波渐强 + 终雷（渡劫仪式） | 劫云 Sky(参数化)·`BeamGrad`·`GenericWarp` | P3 | S–M |
| 黑熊精 BlackBear | NPCs | POOR | — | 狂怒裂地 + 滚石冲撞（新手阶段感样板） | `GroundDecal`·`GenericWarp`·狂怒描边 | P3 | S–M |
| 敖广 AoGuang | 龙系 | GOOD | — | 龙吟·没顶 + 深渊漩涡（潮汐三幕水下折射） | `ElementalScreenTint`·`GenericWarp`(refraction) | P2 | S |
| 敖钦 Aokin | 龙系 | MEDIOCRE | — | 熔心·下沉天花板 + 焚风走廊（补 P3 + Heat 资源） | `ElementalScreenTint`·`GenericWarp`(heat)·`GroundDecal` | P1 | M–L |
| 敖闰 Aoyuan | 龙系 | POOR | ✅ | 绝对零度·破弱点 + 冰晶棋局 | （几乎无，`XuanwuFrostDistortion` 已存）·棋盘 `GroundDecal` | P2 | S |
| 敖顺 Aoshun | 龙系 | GOOD | — | 深渊伏击 + 雷暴临界雷网（StormCharge 全屏天气） | `ElementalScreenTint`(storm)·`T-Ribbon` | P3 | S |
| 天御金龙 Celestial | 龙系 | MEDIOCRE | — | 天规棋盘 + 敕令天剑雨（替换密度档） | `RadialBloom`(golden)·`GroundDecal`·`BeamGrad` | P0/P1 | L |
| 祖龙残魂(天庭) Ancestral | 龙系 | MEDIOCRE | — | 双魂回拢 Enraged 终曲（填真空高潮） | `ElementalScreenTint`(太初)·`GenericWarp`·`GroundDecal` | P0/P1 | M |
| 神威 Vigor | 天庭其余 | GOOD | — | 断罪判决 Verdict Strike | `PaletteLUT`(罪名色)·`RadialBloom`·`T-VignettePulse` | — | S |
| 百目 Argus | 天庭其余 | GOOD | — | 全视之域 All-Seeing Domain | 眼穹 Sky(新美术)·`BeamGrad`·`GenericWarp` | — | S–M |
| 玄武 Xuanwu | 天庭其余 | GOOD | — | 玉璧绝防 Jade Aegis | `ReflectWard`(**首发**)·北斗 Sky | — | M |
| 毗沙门 Vaisravana | 天庭其余 | POOR | ✅ | 终极宝塔 Pagoda Apex | `BeamGrad`·`GroundDecal`(坛城)·金 `ReflectWard`(复用) | P0 叠加 | S–M |
| 天庭观察者 Overseer | 天庭其余 | POOR | ✅ | 天庭陪审 + 审判射线 | `GenericWarp`(scrying)·审判庭 Sky·`GroundDecal` | P0 叠加 | M |
| 青龙 Qinglong | 天庭其余 | MEDIOCRE | — | 风域天罚 Stormfield Judgment | `GroundDecal`·`BeamGrad`·`T-Ribbon`（四兽换色复用） | P1 | M |
| 白虎 Baihu | 天庭其余 | MEDIOCRE | — | 裂地灭世爪 Riftclaw Cataclysm | `GroundDecal`(爪痕)·`BeamGrad`（四兽换色复用） | P1 | S–M |
| 朱雀 Suzaku | 天庭其余 | MEDIOCRE | — | 涅槃重生 Nirvana Rebirth | `PaletteLUT`(灰↔赤)·赤焰 Sky·`RadialBloom`（**换占位弹**） | P1 | M |
| 大椿 Dazheng | 天庭其余 | MEDIOCRE | — | 四季轮转 Cycle of Seasons | `PaletteLUT`(四季)·季节 Sky·`GroundDecal` | P2 | M–L |
| 树精 Dryads | 天庭其余 | MEDIOCRE | — | 潜地伏击 Ambush Surface | `GroundDecal`·`RadialBloom`（复用大椿件） | P3 | S–M |
| 怨灵 Spectre | 地府 | MEDIOCRE | — | 怨念清算 Grudge Reckoning | `grudge-desaturation`(`PaletteLUT` 变体)·`DissolveBurn` | P1 | M |
| 枉死千骸 Corpses | 地府 | POOR | — | 引魂大阵 Soul-Summoning Ritual | `prison-overlay`(`GroundDecal` 变体,**首发**)·`DissolveBurn`·`decree-vignette` | P1 | M+ |
| 幽冥龙 Nether Dragon | 地府 | MEDIOCRE | — | 穿墓追猎 Burrow Hunt | `rift-warp`(`GenericWarp` 变体,**首发**)·`nether-fog-distortion` | P2 | M |
| 幽冥妖狐 Nether Kitsune | 地府 | GOOD | — | 虚实九影 Phantom Veil | `DissolveBurn`·`nether-fog-distortion`(复用)（**换占位弹**） | P3 | S–M |
| 黑白无常 BAW | 地府 | GOOD | — | 阴阳勾魂 Yin-Yang Reaping | `DissolveBurn`·`yin-yang-split`(**两者首发验证**) | P3 | S |
| 觉醒冥龙 Awakening Nether | 地府 | POOR | 🔧 | 虚空吞噬 Void Devour（压缩空间终局） | `rift-warp`·`nether-fog-distortion`·`decree-vignette` | P0 | L |
| 阴天子 Yin Emperor | 地府 | MEDIOCRE | 🔧 | 酆都审判·阴阳定罪 Fengdu Judgment | `yin-yang-split`·`prison-overlay`·`decree-vignette`（三者共享收口） | P0 | L |

---

## 5. 实施分期 Implementation Phasing

> 设计目标：后续按「**一 Boss 一线程**」并行派发。批次划分遵守两条铁律——
> ① **着色器/身份层前置先行**（§3）；② **共享件只做一次再复用**（四圣兽框架、龙系元素染色、地府冥律场）。
> 批内 Boss 除标注的依赖外**相互独立**，可并行。

### 批 V2-0 —— 地基 Foundation（强串行，阻塞其余全部批次）

> 这是整个 V2 的吞吐瓶颈，必须先完成或至少把共享件落地到「可被消费」状态。

| 项 | 内容 | 备注 |
|----|------|------|
| 0.1 规范着色器目录 | 按 §3.2 顺序授权 `DissolveBurn`→`GenericWarp`→`ElementalScreenTint`/`PaletteLUT`→`GroundDecal`→`BeamGrad`→`RadialBloom`→`ReflectWard` | 边做边按 §3.3 在 GOOD Boss 上验证 |
| 0.2 地府身份层 `UnderworldField` | 接线 `UnderworldEffect`（冥律/魂蚀/怨念三轴，统一 0–1 标量） | **全地府前置**（§3.4#1） |
| 0.3 四圣兽共享骨架 `SacredBeastBase` | telegraph 子状态 + 确定性轮替（替代随机 hub）+ 方位/五行常量 + 元素天幕 | 覆盖青/白/朱（玄武已 GOOD） |
| 0.4 龙系元素染色基座 | `ElementalScreenTint` 参数化（6 龙传色/强度） | 6 龙视觉升级前置 |
| 0.5 统一可读性语汇 + 公共工具 | telegraph 色/形/时编码常量；`ACMUtils.AddScreenShake`（取 max）、`GenerateTileableNoise` 抽公共 | §6 验收基准 |
| 0.6 占位弹清除 | 苍龙 / 朱雀 / 九尾 / 妖狐 换自定义主题弹（§3.4#2） | 可与各 Boss 并行，但须在其 V2 交付前完成 |

**验证 Boss（与 0.1 并行跑通着色器）：** 敖闰（frost 复用）、黑白无常（`DissolveBurn`+`yin-yang-split` 首发）、旱魃（`BeamGrad` 抽原语）、玄武（`ReflectWard` 首发）。

### 批 V2-A —— GOOD/已重写 视觉验证（低风险，依赖 V2-0，可并行）

> 纯视觉叠加、零/极少逻辑风险，**同时充当工具箱原语的回归用例**。

赢勾 · 旱魃 · 敖广 · 敖顺 · 敖闰 · 神威 · 百目 · 玄武 · 黑白无常 BAW · 幽冥妖狐（换弹）
**红线：** 这些是 GOOD/参考模板，只许加表现层，禁止改坏 FSM/协同/反射骨架。

### 批 V2-B —— P0 叠加（FSM 已就绪，性价比最高，可并行）

> V1 已完成结构重写，V2 只叠 set-piece + 着色器，风险最低。

后卿 Hoqing · 将臣 Jiangcen · 毗沙门 Vaisravana · 天庭观察者 Overseer
**依赖：** 仅依赖 V2-0 着色器（`GroundDecal`/`BeamGrad`/`DissolveBurn`/`GenericWarp`）。毗沙门↔观察者模板已在 V1 分家，无需再裁定。

### 批 V2-C —— P1 主力（逻辑改动较大，需最多测试）

> 玩法层动得最多的一批。**四兽（青/白/朱）依赖 V2-0.3 `SacredBeastBase`，建议同子组协调后并行。**

苍龙真身（P3 重做网格审判 + 换弹） · 祖龙残魂(地表)（破绽窗口结构升格） · 敖钦（补 P3 熔心 + Heat 资源） · 天御金龙（天规法阵机替换密度档） · 祖龙残魂(天庭)（填 Enraged 双魂回拢终曲） · 青龙 · 白虎 · 朱雀（换弹必修） · 怨灵（`grudge-desaturation` 首发 + 怨念账） · 尸骸（`prison-overlay` 首发 + 接线 DarkRitual）

**批内依赖：**
- 青/白/朱 共享 `SacredBeastBase`（V2-0.3）——先抽公共骨架再各自加签名。
- 天御金龙 / 祖龙(天庭) 虽列 V1 P1，但因含**未消除的核心问题**（密度档 / 真空 Enraged），优先级实质等同 P0，建议本批最先开工。
- 怨灵的 `grudge-desaturation`、尸骸的 `prison-overlay` 是地府终局（觉醒龙/阴天子）的**前置验证用例**，应早于 V2-E。

### 批 V2-D —— P2/P3 深化与打磨（可并行）

牛头马面（协同连携，镜头框架现成） · 九尾（去随机化 + 换弹） · 大椿（四季锚点，最大单项逻辑） · 幽冥龙（`rift-warp` 首发 + 掘墓三段） · 黑熊精 · 劫云（三合一参数化） · 树精

### 批 V2-E —— 终局收口（强依赖 P0 in-flux + 已验证着色器）

> 两条终局龙/帝**串行依赖**其它 agent 的 P0 落地，且复用前批已验证的着色器，最后开工。

- **觉醒冥龙 Awakening Nether**：依赖自身 P0 重写落地 + **幽冥龙已验证 `rift-warp`**（V2-D）。
- **阴天子 Yin Emperor**：依赖自身 P0 落地 + **BAW 已验证 `yin-yang-split`**（V2-0/A）+ **尸骸已验证 `prison-overlay`**（V2-C）+ `decree-vignette`。

### 全局依赖链速查 Critical Dependency Chains

```
V2-0 着色器/身份层/四兽骨架 ──> 其余全部批次
旱魃 BeamGrad 抽原语 ──> 苍龙/将臣/劫云/四兽/毗沙门/观察者 光束
BAW(yin-yang-split) ─┐
尸骸(prison-overlay) ─┼─> 阴天子(收口)
怨灵(grudge-desat) ──┘
幽冥龙(rift-warp) ──> 觉醒冥龙(收口)
SacredBeastBase ──> 青龙/白虎/朱雀
ElementalScreenTint ──> 敖广/敖钦/敖闰/敖顺/天御金龙/祖龙(天庭)
UnderworldField(冥律/魂蚀/怨念) ──> 全地府 7 Boss
```

---

## 6. 全局观感契约 Global Presentation Contract

> 折叠工具箱 §C 的全部规范为**每个 V2 实现的强制验收标准（binding acceptance criteria）**。四区 Boss 由不同实现者并行开发，仍必须整体观感统一、不互相打架、不炸帧。

### 6.1 预警色彩语言 Telegraph Color Language（强制）

**红色 = 即将造成伤害的致命预警；其余为主题色，不与红冲突。预警必有「形状 + 颜色 + 渐强时间」三要素，红色只留给真正的伤害源。**

| 含义 | 颜色 | 用法 |
|------|------|------|
| 致命攻击预警（落点/激光路径/冲刺线） | 纯红 `#FF2838`（`new Color(250,40,56)`） | 命中前 0.3–0.6s 必须可读 |
| 安全/治疗/神圣（天庭） | 金白 `#FFFAD0` / 翠玉 `#DCFFE6` | 天庭线氛围与正反馈、安全缝/护盾/赐福区 |
| 地府 / 阴 | 幽蓝紫 / 鬼绿；处决=赤红 + `decree-vignette` | 地府线氛围；DoT/冥律标记 |
| 冰/水（玄武四海） | 冰蓝 `#8CC7F2` / 深冰 `#264073` | 冰系预警用冰白高光做边 |
| 雷 | 青白电弧 | 高频闪 |
| 元素方位（四兽） | 青龙青 / 白虎银白 / 朱雀赤 / 玄武玉黑 | 形状编码：线=射线/冲刺、圆=落点、扇=安全缝、环/裂纹=收缩/裂地 |
| 阶段升级 | 主题色 → 提亮/转暖 | 见各 Boss 阶段色板 |

**时间编码：** 预告时长 ∝ 伤害——小压制弹 ≤20 tick；中等攻击 ~35–55 tick；处决级大招 60–90 tick + 渐强震屏 + 蓄力泛光。

### 6.2 屏幕震动预算 Screen-Shake Budget（强制）

统一封装 `ACMUtils.AddScreenShake(amount)`，**同帧多源取 max 而非累加**，衰减用 `*=0.9`。

| 事件 | 强度（像素峰值） | 时长 |
|------|------------------|------|
| 小命中/格挡 | ≤ 2 | ≤ 6 帧 |
| 落地/普通爆炸 | 4–6 | 8–12 帧 |
| 相变/大招释放 | 8–12 | 12–20 帧 |
| 入场/死亡定格 | ≤ 16（一次性） | ≤ 30 帧 |

提供 `MythologyConfig.ScreenShakeScale`（0–1）供玩家缩放，0 时完全关闭。

### 6.3 镜头/演出节拍 Cinematic Beats（建议统一）

| 节拍 | 时长 | 视觉栈 |
|------|------|--------|
| 入场 Intro | 1.5–2.5s | 天幕淡入 + `Filters.Scene` 染色 + `DissolveBurn` 显形 + 一次性大闪 + 入场震动；轻推拉近 zoom 1.1–1.4 |
| 相变 Phase | 0.6–1.2s | 全屏闪 + 天幕切色 + 冲击环 + 中度震动；短暂无敌避免被秒过场 |
| 死亡 Death | 1.5–3s | 慢动作(可选) + 多段 `DissolveBurn` 崩解 + 渐隐天幕 + 最终大冲击波 |

**高潮 set-piece 通用配方：** `长蓄力(渐强 RadialBloom + 渐强震屏) → 末帧 hitstop(3–4f) → 释放(BeamGrad + VignettePulse + 大 PunchCamera) → 短余波`。所有处决级签名共用，保证「重击感」一致。天幕 `intensity` 用 `Lerp` 淡入(~0.012/帧)淡出(~0.02/帧)；相变 `phase` 用 `Lerp(...,0.02)` 平滑。

### 6.4 性能护栏 Performance Guardrails（强制）

1. **Effect/纹理只 Request 一次**：缓存到 `static Asset<Effect>` / `static Texture2D`（`??=` 惰性缓存）；**禁止**每帧 `ImmediateLoad`。
2. **全屏后处理同屏 ≤ 1 个生效**：`GenericWarp` 这类喂 `Main.screenTarget` 的 shader 很贵且打断主 SpriteBatch；多 Boss 同屏按优先级只跑一个，`uIntensity < 0.01` 立即 `return`。
3. **shader 内尽早 early-out**：用 `normDist`/距离阈值跳过大片像素。
4. **复用 RenderTarget / 噪声纹理**：static 生成一次（`IsDisposed` 校验），`Unload()` 里 `Dispose` + 置 null；**不要**每帧 `new`。
5. **SpriteBatch 配对**：自定义 `Begin`→画→`End` 后**必须**恢复项目默认 `Begin(Deferred, AlphaBlend, PointClamp, ..., Main.GameViewMatrix.TransformationMatrix)`。
6. **顶点拖尾设上限**：历史点数固定（如 12），`subdivisions` 默认 3。
7. **服务端零绘制**：所有绘制 `if (Main.dedServ) return;`；伤害/推力逻辑 server 权威。

### 6.5 多人 & 低端安全 Multiplayer / Low-End Safety（强制）

- **逻辑与表现分离**：竞技场伤害、相变判定、出生/死亡、随机序列（落雷图案/安全格/真假身份）在 **server 决策并同步**；shader/dust/震动纯本地。
- **降级开关（`MythologyConfig`）：** `EnableFullscreenShaders`（关后退化为 dust/粒子 fallback）、`ScreenShakeScale`（0–1）、`TrailQuality`（High/Med/Off）。
- 全屏分屏/折射类后处理**单 Boss 限定一层**；着色器强度统一走身份层（如 `UnderworldField`）单一 0–1 标量，便于一处降级。
- `Main.gameMenu` / 截图模式下跳过 overlay 绘制。

### 6.6 V2 验收自检 Acceptance Checklist（每 Boss 交付前逐项确认）

- [ ] 满足 V1 §3 全部 9 条硬性原则（未回退反模式；`FuryPatrol/FuryProwl/NirvanaFlight/DesperateFury` 等纯加速幕已被签名脚本幕替代）。
- [ ] ≥1 个手工编排、与主题强绑定的签名 set-piece，遵循 §6.3 配方。
- [ ] telegraph 色/形/时已对齐 §6.1 共享语言；处决级大招预告 ≥60 tick。
- [ ] 高潮全屏特效**不遮挡**任何需躲避的危险信息（最高难度实测）。
- [ ] 原版占位弹已全部替换为主题自定义弹（苍龙/朱雀/九尾/妖狐重点核查）。
- [ ] 着色器走 §3.1 规范原语，未重复造轮子。
- [ ] 满足 §6.4 性能护栏 + §6.5 MP/低端安全（逻辑服务器权威、全屏后处理单实例、降级开关生效）。
- [ ] GOOD 模板（赢勾/旱魃/敖广/敖顺/神威/百目/玄武/BAW/妖狐）FSM/协同/反射骨架未被改坏。
- [ ] （地府 Boss）已强化或消费 `UnderworldField` 三轴之一（冥律/魂蚀/怨念）。

---

## 7. 文档索引 Cross-References

| 主题 | 文档 · 章节 |
|------|-------------|
| V2 着色器/VFX 技术规格、原语目录、性能护栏、`.fx` 愿望清单 | `docs/BOSS_REDO_V2/00_SHADER_VFX_TOOLKIT.md` |
| V2 逐 Boss 设计 · NPCs 区 | `docs/BOSS_REDO_V2/01_NPCS_BOSS_V2.md` |
| V2 逐 Boss 设计 · 天庭龙系 + 元素屏幕染色 | `docs/BOSS_REDO_V2/02_CELESTIAS_DRAGONS_V2.md` |
| V2 逐 Boss 设计 · 天庭非龙系 + 四圣兽框架 | `docs/BOSS_REDO_V2/03_CELESTIAS_OTHERS_V2.md` |
| V2 逐 Boss 设计 · 地府线 + 身份层 + 着色器复用矩阵 | `docs/BOSS_REDO_V2/04_UNDERWORLD_V2.md` |
| V1 一审（反模式 / 设计原则 / 参考模板 / 优先级 / 分期） | `docs/BOSS_REDO_PLAN.md` §2–§7 |
| 顺序 / Tier / 掉落 / 召唤 / 门控 / 冥律标记数值依据 | `docs/PROGRESSION_DESIGN_SPEC.md` §2.2、§3、§5、§6.7、§7 |
| 占位武器/材料主题（换弹/换贴图时参考） | `docs/PLACEHOLDER_CONTENT_REGISTRY.md` |
| 着色器编译脚本（`fxc.exe` → `.fxc`，退出码 0/1 判编译结果） | `Effects/CompileFX.ps1` |

---

## 文档维护 Maintenance

- 本文是 **V2 实现阶段的调度蓝图**；逐 Boss 设计细节以 `docs/BOSS_REDO_V2/` 五份子文档为权威，进度/数值以 `PROGRESSION_DESIGN_SPEC.md` 为准，三者互不覆盖。
- 着色器规范名（§3.1）落地后，请回各子文档把占位别名回填为规范 `.fx` 名。
- 每完成一个 Boss V2 交付，在 §4 总表对应行标注完成，并在 §5 对应批次勾除。
- **版本历史：** v1.0 · 2026-06-27 · 合并五份 V2 子文档 + V1 基准的初版调度蓝图。

---

*Primordial / 洪荒 · Boss Combat Redo Master Plan V2 · 实现演出前请先读 §3 着色器先行、§6 全局观感契约（尤其 §6.4 性能护栏），并确认 §5 批 V2-0 地基已就绪。*
