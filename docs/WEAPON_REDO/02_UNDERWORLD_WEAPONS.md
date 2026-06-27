# 地府线武器盘点与现代化重做计划 · Underworld Weapons

> **任务区域：** `Underworlds/` 下所有武器（Boss 掉落 `Items/` + `Underworlds/Items/Weapons/` 四档梯队）
> **判定基准：** 只看**武器物品本体（ModItem）**的纹理；严格按用户口径——**仅排除「复用原版物品纹理」者**。
> **纹理验证手段：** PowerShell `Get-ChildItem -Recurse -Filter *.png` / `Test-Path` 实测（**不使用会被结果上限截断的 glob**）。
> **代码勘察：** 通读全部 57 件 ModItem 及其弹幕、各 `*Helper.cs`、`ACMAsset` 资产引用。
> **版本：** v2.0 · 2026-06-27（**修订版：纠正 v1.0 基于截断数据的错误结论**）

---

## 0. 修订说明 & 结论速览 TL;DR

> **v1.0 勘误：** 旧版「全仓仅 39 个 PNG」「56 件武器纹理缺失 / art-blocked」**完全错误**——那是搜索工具结果被上限截断造成的假象。经 PowerShell 实测：
> - **全仓 PNG = 626 张；`Underworlds/` 下 = 118 张。**
> - 地府线武器**本体贴图几乎全部真实存在**（逐文件 `Get-ChildItem` 已确认）。

**真实结论：**
- 地府线**所有武器都已具备自定义本体纹理**，做工完整、机制成熟。
- 全仓 grep `Terraria/Images/Item_` **零命中**——地府线**没有任何**「复用原版物品纹理」的占位武器。
- 因此按用户口径（仅排除复用原版纹理者）：**地府线武器应全部保留。**

| 分类 | 数量 |
|------|------|
| 审阅武器物品（含 DamageType 的攻击物） | **57** |
| **保留 ✓**（真武器，本体纹理实测存在，非复用原版） | **57**（含 `WraithLantern`） |
| 因「复用原版物品纹理」排除 | **0** |
| 非武器排除（Boss 召唤物 / 材料 / 召唤BOSS物品） | `GhostGateKey`、`YinEmperorEdict`、`Items/Materials/*`、`SoulFragment`、`UnderworldInvasionSummon`、`Corpsefragments` 等 |
| 战斗向饰品（非武器，单列备注） | 2（`FengduImperialCrown`、`SoulBannerUnderworldRelic`） |

**统一技术现状（57 件共性，也是重做核心抓手）：**
- **VFX 全靠 `ACMAsset` 加法贴图叠绘 + 海量 `Dust`**：`SoftGlow`/`BlankStar`/`Sparkle`/`LightShot`/`GlaciateWave`/`ElectricArcSheet`/`Smoke`/`EmberShards`/`SlashBurst`。
- **没有任何一件武器使用 `Effects/` 下的 `.fx` 着色器**——这正是「现代化」最大空间：把叠贴图+撒 Dust 升级为 shader 驱动的程序化光效与 `BuildRibbonStrip` 带状拖尾。
- 路径常量（均 `命名空间.Replace(".","/")+"/"`）：`BAWHelper.Path` / `AwakeningNetherHelper.Path` / `SpectreHelper.Path` / `YinEmperorHelper.Path`。

---

## 1. 盘点结果总表（按来源分组，纹理判定列均为 PowerShell 实测）

> 类型缩写：近=近战 远=远程 魔=魔法 召=召唤。**全部「本体PNG存在 ✓ → 保留」**。

### 1A. `Items/Weapons/Umbrals/`（幽影 · 肉后初期，UmbralStone+SoulFragment@铁砧，LightRed）
| 类名 | 类型 | 本体PNG | 结果 |
|------|------|---------|------|
| AxeofXingTian | 近·斧 | ✓ | 保留 |
| BloodfiendGreatsword | 近·巨剑 | ✓ | 保留 |
| SoulDevourerSpear | 近·矛(手持) | ✓ | 保留 |
| YamasGavel | 近·回旋锤 | ✓ | 保留 |
| SoulseekerDaggers | 近·投掷匕 | ✓ (+Proj.png) | 保留 |
| BrushofJudgment | 魔 | ✓ | 保留 |
| NetherboneBow | 远·弓 | ✓ | 保留 |
| SoulLanternStaff | 召 | ✓ | 保留 |

### 1B. `Items/Weapons/Revenants/`（亡魂 · 肉后中期，NetherBar+SoulFragment+UmbralStone@秘银砧，Pink）
| 类名 | 类型 | 本体PNG | 结果 |
|------|------|---------|------|
| UnderworldSoulguide | 远·弓 | ✓ | 保留 |
| JudgesSoulhook | 近·枪(手持突刺) | ✓ | 保留 |
| StaveofNetherflow | 魔 | ✓ | 保留 |
| KarmasMirrorBlade | 近·回旋刃 | ✓ | 保留 |
| YamasSeverance | 近·大刀 | ✓ | 保留 |
| CodexofFate | 魔·书 | ✓ | 保留 |
| NetherRockSoulbomb | 远·投掷雷 | ✓ | 保留 |
| NetherfireBlunderbuss | 远·散弹枪 | ✓ | 保留 |

### 1C. `Items/Weapons/RevenantEXs/`（亡魂·EX · 月族站升级版，Purple，Revenants+Corpsefragments）
| 类名 | 类型 | 本体PNG | 结果 | 升级自 |
|------|------|---------|------|--------|
| CodexofMyriadDemons | 魔·书 | ✓ | 保留 | CodexofFate |
| DamnedSoulguide | 远·弓 | ✓ | 保留 | UnderworldSoulguide |
| InfinityKarmaBlade | 近·回旋刃 | ✓ | 保留 | KarmasMirrorBlade |
| OblivionSoulhook | 近·枪 | ✓ | 保留 | JudgesSoulhook |
| SoulEatingCannon | 远·重炮 | ✓ | 保留 | NetherfireBlunderbuss |
| SoulShatteringUnderworldBomb | 远·分裂雷 | ✓ | 保留 | NetherRockSoulbomb |
| StaveofNetherEclipse | 魔 | ✓ | 保留 | StaveofNetherflow |
| YamasDeicide | 近·大刀 | ✓ | 保留 | YamasSeverance |

### 1D. `Items/Weapons/Fengdus/`（酆都 · 终极梯队，RevenantEX+大量材料@月族站，Purple）
| 类名 | 类型 | 本体PNG | 结果 | 升级自 |
|------|------|---------|------|--------|
| CelestialImperatorGreatblade | 近·巨剑(三连击) | ✓ | 保留 | YamasDeicide |
| AbyssalFrostJudgmentChakram | 近·轨道回旋轮 | ✓ | 保留 | InfinityKarmaBlade |
| VoidDamnationSoulpiercerSpear | 近·长矛(超远突刺) | ✓ | 保留 | OblivionSoulhook |
| HellwyrmAnnihilationCannon | 远·龙息炮 | ✓ | 保留 | SoulEatingCannon |
| PrimordialChaosDeicideBow | 远·弓(召羽阵) | ✓ | 保留 | DamnedSoulguide |
| ShadowOblivionSingularityBomb | 远·奇点炸弹 | ✓ | 保留 | SoulShatteringBomb |
| NetherworldArchonFateScepter | 魔·权杖(命运烙印) | ✓ | 保留 | StaveofNetherEclipse |
| NightmareRashomonMyriadCurseTome | 魔·书(罗生门) | ✓ | 保留 | CodexofMyriadDemons |

### 1E. `Boss/NetherDragons/Items/`（幽冥龙 · Red · 共享「幽冥怨念 NetherGrudge」叠层系统）
| 类名 | 类型 | 本体PNG | 结果 |
|------|------|---------|------|
| NetherStaff | 魔(怨魂 wisp×4) | ✓ | 保留 |
| Netherlayer | 近·刀(三连斩波) | ✓ | 保留 |
| Netherthrower | 远·喷射器(耗Gel) | ✓ | 保留 |
| NetherSutom | 召(龙头 minion) | ✓ (+Minon/Buff.png) | 保留 |

### 1F. `Boss/BAWImpermanences/Items/`（黑白无常 · LightPurple）
| 类名 | 类型 | 本体PNG | 结果 |
|------|------|---------|------|
| DemonicAnnihilation | 近·关刀(三连击) | ✓ | 保留 |
| NetherworldSickle | 近·链刃镰 | ✓ | 保留 |
| DemonSoulStaff | 魔·法阵(吸血) | ✓ | 保留 |
| Ferryman | 远·幽灵弓 | ✓ | 保留 |

### 1G. `Boss/AwakeningNethers/Items/`（觉醒幽冥龙 · Purple）
| 类名 | 类型 | 本体PNG | 结果 |
|------|------|---------|------|
| AbyssalSpine | 近·大刀(连击/裂空) | ✓ (+Slash.png) | 保留 |
| SoulErosionScepter | 魔·蚀魂法阵 | ✓ | 保留 |
| PhantomBreath | 远·蓄力弓 | ✓ (+Arrow.png) | 保留 |

### 1H. `Boss/Corpseses/Items/`（枉死千骸 · Red · 继承Boss招式）
| 类名 | 类型 | 本体PNG | 结果 |
|------|------|---------|------|
| CorpsesesBook | 魔(幽灵手拍掌) | ✓ (+CorpsesHand.png) | 保留 |
| CorpsesesLance | 近·IK追踪长枪 | ✓ | 保留 |
| CorpsesesStaff | 召(幽灵手掌) | ✓ | 保留 |
| CorpsesesRepeater | 远·连弩(骨箭泼洒) | ✓ | 保留 |

### 1I. `Boss/NetherKitsunes/Items/` & `Boss/Spectres/Items/`
| 类名 | 类型 | 本体PNG | 结果 |
|------|------|---------|------|
| NetherKyuubiBook | 魔(九尾抛射) | ✓ | 保留 |
| WraithLantern | 魔·双鬼火灯笼+怨灵锁链 | ✓ | 保留 |

---

## 2. 排除清单（精简）

**因「复用原版物品纹理」排除的武器：0 件。**（全仓 `Terraria/Images/Item_` 零命中。）

**非武器（不计入武器线，正常排除）：**
- Boss 召唤物：`Boss/YinEmperors/Items/GhostGateKey`、`YinEmperorEdict`、`NetherDragons/NetherDragonSummonItem`、`Items/UnderworldInvasionSummon`。
- 材料：`Items/Materials/*`（YinEssence/ImpermanenceSoul/SpectreGrudgeCore/AwakenedNetherCore/VoidDragonSinew/NetherDragonScale/YinImperialSeal）、`Items/SoulFragment`、`Boss/Corpseses/Items/Corpsefragments`。

**战斗向饰品（非武器，纹理已就绪，单列见附录 A）：** `FengduImperialCrown`、`SoulBannerUnderworldRelic`。

---

## 3. 保留武器重做计划

### 3.0 通用重做基线（适用全部 57 件，避免重复造轮子）

当前所有武器的视觉是同一套「`ACMAsset` 加法贴图叠 3~4 层 + 每帧撒十几粒 `Dust`」。统一升级路线（每件小节只点名差异，通用项不再重复）：

| 现状写法 | 现代化替换（复用现有资产） |
|---------|--------------------------|
| `SoftGlow` 多层叠光球 | **`RadialBloom.fx`** 单 pass 软辉光（省 draw call，呼吸相位接现成 `Timer/pulse`） |
| `oldPos[]` 逐帧叠贴图拖尾 | **`ACMUtils.BuildRibbonStrip` + `BeamGrad.fx`** 带状渐变拖尾（青↔黄/紫↔暗冥流动 UV） |
| `GlaciateWave`/`SlashBurst` 当剑气 | **`BeamGrad.fx`** 扇形剑气片 + **`DissolveBurn.fx`** 边缘溶解消退 |
| `Dust` 撒一圈当命中/爆炸 | 保留少量 Dust 作火星，主体改 **`RadialBloom`**（闪光）+ **`GenericWarp.fx`**（冲击扭曲） |
| 五颜六色 Dust（紫/蓝/绿/银/红混用） | **`PaletteLUT.fx`** 统一到地府线「青黄魂火 / 暗冥紫」主色板，按梯队微调色相 |
| 终结技/处决/引爆 | **`ElementalScreenTint.fx`** 短促全屏色偏 + **`RadialBloom`** 强闪（cinematic 反馈） |
| 法阵/符印/烙印 | **`ArenaRunic.fx`** 程序化符文环（替代静态 BlankStar 旋转） |
| 镜面/反射类 | **`ReflectWard.fx`** 镜面 SDF 折射 |

> 着色器加载/编译遵循 `docs/BOSS_REDO_V2/00_SHADER_VFX_TOOLKIT.md` §A（`ModContent.Request<Effect>` 静态缓存 + `Effects/CompileFX.ps1`）。所有新 shader 沿用 `uTime/uIntensity/uCenter` 命名约定。

---

### 3.1 幽影 Umbrals（入门梯队 · 最简陋，重做收益最大）

> 该梯队最朴素：3 件几乎只有原版挥砍 + Dust，是「从占位到成品」收益最高的一批。

- **AxeofXingTian 刑天之斧**（近·斧）｜机制：纯挥砍，血量<50% 时 `ModifyWeaponDamage` 最高+30%、附 Ichor。｜重做：把「刑天不屈」做成可见的**狂暴层**——低血时斧刃缠绕血色能量。｜着色器：`PaletteLUT` 血色刀光 + 低血触发 `ElementalScreenTint` 暗红描边 + 命中 `RadialBloom`。
- **BloodfiendGreatsword 血魔巨剑**（近·巨剑）｜机制：5% 吸血、暴击双倍吸血，仅 `DustID.Blood`。｜重做：吸血时血粒回流玩家做成可见**血环**；连斩叠攻速。｜着色器：`BeamGrad` 血色弧光剑气 + 吸血 `BuildRibbonStrip` 血丝牵引线。
- **SoulDevourerSpear 噬魂枪**（近·手持突刺）｜机制：三段 Prepare/Thrust/Retract 手持枪、线段碰撞、25% 概率吸血。｜重做：突刺残留**噬魂裂隙**短暂续伤（轻量版穿心矛）。｜着色器：枪身 `BeamGrad` 冷蓝拖影 + 枪尖 `RadialBloom` + 命中 `GenericWarp` 小扭曲。
- **YamasGavel 阎罗锤**（近·回旋锤）｜机制：投掷飞回状态机、黄金烈焰、命中 OnFire+几率混乱。｜重做：飞回路径加**审判印**短驻；蓄力重投。｜着色器：`BuildRibbonStrip` 金焰拖尾 + 命中 `ArenaRunic` 审判符环。
- **SoulseekerDaggers 索魂匕**（近·投掷匕）｜机制：挥+投双形态，<15% 血非Boss 1/5 即死。｜重做：即死触发**索魂链**在敌群间跳跃。｜着色器：`BeamGrad` 幽蓝匕光 + 即死 `DissolveBurn` 灵魂溶解。
- **BrushofJudgment 判官笔**（魔）｜机制：3 散射 `LostSoulFriendly`（原版弹幕）。｜重做：改为自绘**朱批符印**追踪弹，命中写「勾」字判罪叠 debuff。｜着色器：`ArenaRunic` 笔锋符文 + 弹体 `RadialBloom`（弹幕占位待补，见附录 B）。
- **NetherboneBow 冥骨弓**（远·弓）｜机制：木箭转 `HellfireArrow`、1/3 双发。｜重做：自绘**骨焰箭**带冥火拖尾，蓄力满射穿骨钉。｜着色器：`BeamGrad` 箭矢拖尾 + `PaletteLUT` 冥火配色。
- **SoulLanternStaff 魂灯杖**（召）｜机制：**仅生成原版 `LostSoulFriendly`，并非真 minion**（机制残缺，是真正需要补全的一件）。｜重做：实现真正的**幽灯 minion**（环绕灯笼+点名攻击，参考 WraithLantern/CorpsesesStaff）。｜着色器：灯体 `RadialBloom` + `SpectreHelper` 青黄魂火。

### 3.2 亡魂 Revenants（中期梯队 · 已有较完整自绘）

- **UnderworldSoulguide 引魂弓**（远）｜机制：箭转 `SoulguideArrow` 弱追踪、1/3 双发、命中灵魂升腾+Frostburn；SoftGlow/BlankStar 自绘。｜重做：命中标记「引魂」，击杀升腾灵魂回弹补伤。｜着色器：`BeamGrad` 蓝魂箭迹 + `RadialBloom` 箭头核。
- **JudgesSoulhook 判官勾魂枪**（近·手持枪）｜机制：三段突刺、命中吸血+Slow、LightShot/SoftGlow 枪尖光。｜重做：勾中拉拽轻位移；连击叠「业」。｜着色器：枪身 `BeamGrad` 绿冥光 + 勾中 `GenericWarp`。
- **StaveofNetherflow 黄泉幽冥杖**（魔）｜机制：能量弹弱追踪、命中漩涡、Smoke 帧动画+双层 SoftGlow。｜重做：命中留**幽冥漩涡**驻留区续伤（EX 已是区域版，本体做小号）。｜着色器：`RadialBloom` 双层球 + 漩涡 `GenericWarp`。
- **KarmasMirrorBlade 孽镜回旋刃**（近·回旋）｜机制：飞出/返回状态机、暴击 1/3 镜像复伤、Sparkle/BlankStar 镜光。｜重做：强化「镜像」——命中留**镜面残像**二次反射。｜着色器：**`ReflectWard.fx`** 镜面折射（专为此设计）+ `BuildRibbonStrip` 银紫拖尾。
- **YamasSeverance 断业刀**（近·大刀）｜机制：挥砍放剑气(0.7×)、暴击冲击波、`GlaciateWave` 当剑气。｜重做：连斩积「业」，第三击宽幅断业斩。｜着色器：`BeamGrad` 扇形剑气 + `DissolveBurn` 消退。
- **CodexofFate 生死冥罗录**（魔·书）｜机制：双符文弱追踪、暴击 200 范围链式电弧、`ElectricArcSheet`+SoftGlow+BlankStar。｜重做：符文命中写「判词」叠层，满层引电狱。｜着色器：`ArenaRunic` 符文环 + 链电用 `BeamGrad` 折线束。
- **NetherRockSoulbomb 冥岩爆魂雷**（远·投掷）｜机制：抛物线引信弹、反弹一次、范围爆炸+Soul/Shadowflame Dust。｜重做：爆炸留**冥火灼地**。｜着色器：引信 `RadialBloom` 渐亮 + 爆炸 `GenericWarp` 冲击波 + `ElementalScreenTint`。
- **NetherfireBlunderbuss 幽火铳**（远·散弹）｜机制：3-5 散射幽火弹、枪口烟+火、命中冥烟爆裂。｜重做：近距霰弹高伤、枪口热浪。｜着色器：弹体 `BeamGrad` 短拖尾 + 枪口 `RadialBloom` 闪光。

### 3.3 亡魂·EX RevenantEXs（升级梯队 · 量级放大 + 链式/AOE/即死）

> 与 Revenants 同机制骨架，参数翻倍并加「暴击/击杀 → 链锁、AOE、回血」。重做沿用 §3.2 各自方向，**额外**强化升级感：

- **CodexofMyriadDemons**（魔）｜5 符文、暴击连锁 5 体电击。｜重做+着色器：`ArenaRunic` 万魔法阵 + 多目标 `BeamGrad` 电网。
- **DamnedSoulguide**（远·弓）｜5(+3) 箭强追踪、暴击 350 连锁。｜`BeamGrad` 蓝魂多线 + 连锁 `BuildRibbonStrip`。
- **InfinityKarmaBlade**（近·回旋）｜三刃齐发、暴击半伤镜像。｜**`ReflectWard`** 三镜阵 + 银紫 ribbon。
- **OblivionSoulhook**（近·枪）｜大突刺、强吸血、击杀 500 寂灭 AOE。｜枪身 `BeamGrad` + 击杀 `GenericWarp`+`ElementalScreenTint`。
- **SoulEatingCannon**（远·重炮）｜10-15 噬魂弹+后坐力、击杀回血。｜枪口 `RadialBloom` 大闪 + `PaletteLUT` 噬魂紫。
- **SoulShatteringUnderworldBomb**（远·雷）｜主雷爆炸**分裂 3 子雷**二段爆。｜两级爆炸 `GenericWarp`+`ElementalScreenTint` 层层冲击。
- **StaveofNetherEclipse**（魔）｜3 冥球、命中 300 范围寂灭区减速。｜`RadialBloom` 球 + 区域 `ArenaRunic` 冥罗网环。
- **YamasDeicide**（近·大刀）｜3 屠神斩、**<15% 非Boss 直接斩杀**、暴击 400 AOE。｜`BeamGrad` 屠神斩 + 斩杀 `DissolveBurn`+`ElementalScreenTint`。

### 3.4 酆都 Fengdus（终极梯队 · 机制已最丰富，重做重在「演出级」VFX）

> 本梯队机制已经很出色（每件都有独立 gimmick），缺的只是把 Dust/叠贴图升级成 set-piece 级 shader 演出。

- **CelestialImperatorGreatblade 黑帝刀**（近·巨剑）｜**三段连击**：前两段扇形刀气、第三段 `ImperatorVoidEruption` 虚空漩涡拉拽+引爆；<25% 斩杀、暴击 600 全屏审判。｜着色器：刀气 `BeamGrad`、漩涡 **`GenericWarp`**（吸拽扭曲）、第三击 `ElementalScreenTint` 暗红 + `RadialBloom` 核。
- **AbyssalFrostJudgmentChakram 九幽判官轮**（近·轨道轮）｜投出后进入**环绕玩家轨道**，每 40 帧自动冲刺最近敌人，每 5 次命中触发 500 范围「绝对零度审判」冻结。｜着色器：`BuildRibbonStrip` 冰蓝轨道残影 + 冲刺 `BeamGrad` + 审判 `ElementalScreenTint` 冰屏 + `RadialBloom` 霜爆。
- **VoidDamnationSoulpiercerSpear 穿心矛**（近·超远突刺）｜MaxThrust 180、突刺路径留**虚空裂隙**续伤 2s、无视防御、击杀回血。｜着色器：裂隙用 **`GenericWarp`** 沿线扭曲带 + 矛身 `BeamGrad` + 击杀 `DissolveBurn`。
- **HellwyrmAnnihilationCannon 冥龙吐纳炮**（远·龙息）｜单发**膨胀**龙息波(scale→4)、命中挂 `DragonAnnihilationMark` 2s 延迟引爆。｜着色器：龙息主体 **`BeamGrad`**（粗→宽渐变束）替代 Smoke 帧动画 + 引爆 `RadialBloom`+`ElementalScreenTint`。
- **PrimordialChaosDeicideBow 坠神弓**（远·弓）｜无箭凝混元冰矢、主箭命中撕裂**混元之门**降羽箭幕。｜着色器：主箭 `BeamGrad` + 门 `ArenaRunic`/`GenericWarp` + 羽箭幕 ribbon。
- **ShadowOblivionSingularityBomb 奇点炸弹**（远·投掷）｜两阶段：60 帧**引力场吸怪** → 600 范围内爆，击杀生连锁奇点。｜着色器：引力场 **`GenericWarp`**（黑洞透镜，最契合）+ 内爆 `RadialBloom`+`ElementalScreenTint`。
- **NetherworldArchonFateScepter 司命杖**（魔·权杖）｜5 追踪灵球、命中施「命运烙印」`FateMarkProj`，**记录期间所受伤害**，3s 后以 1.5× 回响引爆+AOE。｜着色器：烙印 **`ArenaRunic`** 命运符环（替代 BlankStar）+ 引爆 `RadialBloom` + 灵球 `RadialBloom`。
- **NightmareRashomonMyriadCurseTome 罗生门**（魔·书）｜光标召**罗生门**驻留 4s：500 吸怪 + 200 续伤 + 定期噩梦触手；最多 3 门。｜着色器：门体 **`GenericWarp`**（虚空吸入扭曲）+ `ArenaRunic` 门框符文 + 触手 `BeamGrad`。

### 3.5 幽冥龙 NetherDragons（Boss 套 · 已有「幽冥怨念」联动系统）

> 4 件共享 `NetherGrudgeGlobalNPC.AddGrudge` 叠层 debuff——已是地府线最体系化的一套。重做应**围绕怨念层数做可视化**（层数越高视觉越狂暴），统一青蓝冥焰主色。

- **NetherStaff 幽冥之杖**（魔·怨魂×4）｜机制：维持上限 4 的穿梭怨魂、命中叠怨念+迸火星。｜着色器：怨魂 `RadialBloom` 核 + `BuildRibbonStrip` 穿梭残影 + 满怨念 `PaletteLUT` 提亮。
- **Netherlayer 幽冥雍刀**（近·刀）｜机制：掷新月斩、每第三击宽幅裂魂斩、直击+斩波叠怨念。｜着色器：`BeamGrad` 新月/裂魂斩 + 第三击 `RadialBloom` 爆闪。
- **Netherthrower 幽冥喷射器**（远·喷火，耗 Gel）｜机制：useTime 4 高频龙息、自追猎、密集灼烧叠怨念引爆湮灭。｜着色器：龙息流 **`BeamGrad`** 连续束 + 引爆 `GenericWarp`（弹幕占位待补，见附录 B）。
- **NetherSutom 幽冥召唤杖**（召·龙头 minion）｜机制：召幽冥龙头跟随喷火、Buff 维持。｜着色器：龙头喷火 `BeamGrad` + `RadialBloom` 口焰；龙头自绘可加 `DissolveBurn` 残影。

### 3.6 各 Boss 掉落武器（继承 Boss 招式 · 主题鲜明）

- **黑白无常 BAW**
  - **DemonicAnnihilation 斩魂关刀**（近·三连击）｜机制：连击 3 段不同斩波、命中几率束缚。｜着色器：`BeamGrad` 暗影斩波 + 束缚 `ArenaRunic` 锁链符环。
  - **NetherworldSickle 黄泉镰**（近·链刃）｜机制：锁链镰可 channel 收放状态机。｜着色器：链身 `BuildRibbonStrip`/`BeamGrad` 能量链 + 镰刃 `RadialBloom`。
  - **DemonSoulStaff 勾魂法杖**（魔·法阵）｜机制：鼠标处召旋转幽灵法阵持续吸血续伤。｜着色器：法阵 **`ArenaRunic`** 旋转符阵（替代 BAWDust 占位）+ `RadialBloom` 核。
  - **Ferryman 摆渡人**（远·幽灵弓）｜机制：耗箭射 `FerrymanArrow`、命中放追踪幽魂。｜着色器：箭迹 `BeamGrad` + 幽魂 `RadialBloom`+`SpectreHelper` 魂火。
- **觉醒幽冥龙 AwakeningNethers**（Purple，伤害量级最高）
  - **AbyssalSpine 冥渊龙脊**（近·大刀）｜机制：连击计数、挥砍放 `AbyssalSpineSlash`(有专属png)、几率撕裂空间。｜着色器：斩波 `BeamGrad` + 撕裂 `GenericWarp`。
  - **SoulErosionScepter 蚀魂权杖**（魔·法阵）｜机制：鼠标召蚀魂法阵限量、持续侵蚀+吸血。｜着色器：`ArenaRunic` 蚀魂阵 + `RadialBloom`（弹体复用 VoidCore，见附录 B）。
  - **PhantomBreath 幻影龙息**（远·蓄力弓）｜机制：`HoldItem` 蓄力(MaxCharge 60)、箭转幻影龙息分裂追踪、蓄满放毁灭龙息。｜着色器：蓄力环 `RadialBloom` 渐强 + 龙息 `BeamGrad` + 满蓄 `ElementalScreenTint`。
- **枉死千骸 Corpseses**（Red，继承 Boss 招式，主色暗绿骨白）
  - **CorpsesesBook 千骸之书**（魔·拍掌）｜机制：鼠标处左右成对召幽灵手拍掌（继承 Boss `CorpsesClapWave`）。｜着色器：手掌 `DissolveBurn` 虚实切换 + 拍掌冲击 `GenericWarp`。
  - **CorpsesesLance 千骸之枪**（近·IK 追踪）｜机制：IK 手臂自动追踪敌人突刺。｜着色器：枪迹 `BeamGrad` + 关节 `RadialBloom`。
  - **CorpsesesStaff 千骸法杖**（召·手掌 minion）｜机制：召幽灵手掌 minion 跟随攻击+Buff。｜着色器：手掌 `DissolveBurn` 残影 + `RadialBloom`。
  - **CorpsesesRepeater 千骸连弩**（远·骨箭泼洒）｜机制：木箭转 `CorpsesesBoneArrow` 3 扇散+重力（继承 Boss 骨头泼洒）。｜着色器：骨箭 `BeamGrad` 短拖尾 + 落地 `DissolveBurn`（弹幕占位待补，见附录 B）。
- **幽冥青丘狐 NetherKitsunes**
  - **NetherKyuubiBook 幽冥狐典**（魔·九尾）｜机制：`NetherBookTailController` 管理 9 条尾巴抛射魂魄弹幕（尾身 `NetherMissesBody`，魂弹复用 `SpectreWrath`）。｜着色器：尾巴 `BuildRibbonStrip` 飘带（最契合九尾）+ 魂弹 `RadialBloom`+`PaletteLUT` 青狐火（弹幕占位待补，见附录 B）。
- **怨灵 Spectres**
  - **WraithLantern 鬼火灯笼**（魔·双灯+锁链）｜机制：双 `WraithLanternGhost`（slot0青/slot1黄椭圆轨道、咬定 NPC 锚点、双 DoT）+ `WraithLanternTether` 线段碰撞锁链；已用 `SpectreHelper.DrawSpectreCore/DrawSoulChain` 自绘。｜重做：「张力锁链」双形态（同目标收束叠伤 / 异目标拉成 AoE 长弧）+ 怨气充能六灯爆发。｜着色器：锁链 **`BeamGrad`**、灯核 `RadialBloom`、咬定 `GenericWarp`、爆发 `DissolveBurn`+`ElementalScreenTint`（详见 v1.0 已写的同名小节思路）。

---

## 附录 A：战斗向饰品（非武器，单列备注）

- **FengduImperialCrown 酆帝冠**（阴天子 33% 三选一，`accessory`）：+12 防御、+8% 通用伤害，置位 `YinJudgmentPlayer.fengduSetActive`（G7 处决资格最小桩）。VFX：头顶 `ArenaRunic` 冥符光环；处决激活 `ElementalScreenTint` 阴红反馈。
- **SoulBannerUnderworldRelic 万魂幡·阴**（阴天子 33% 三选一，`accessory`）：+12% 召唤伤害、+1 召唤上限。VFX：召唤物挂 `SpectreHelper` 青黄魂火。

---

## 附录 B：弹幕占位待补清单（区别于「本体已就绪」）

以下武器**本体纹理 OK**，但其**发射的弹幕**仍复用原版/占位纹理，需替换为自绘纹理或改纯着色器自绘（不影响保留判定，仅打磨待办）：

| 武器 | 占位弹幕 | 当前占位纹理 |
|------|---------|-------------|
| NetherStaff | NetherOrbProjectile | `Projectile_ShadowOrb` |
| Netherlayer | NetherSlashProjectile | `Projectile_None` |
| Netherthrower | NetherBreathProjectile | `Projectile_None` |
| NetherSutom | (龙头火弹) | `Projectile_None` |
| CorpsesesRepeater | CorpsesesBoneArrow | `Projectile_BoneArrow` |
| CorpsesesBook | CorpsesesGhostHand | `Projectile_ShadowFlame` |
| NetherKyuubiBook | TailController / 魂弹 | `Projectile_None` / `Projectile_SpectreWrath` |

> 其余梯队（Umbrals/Revenants/RevenantEXs/Fengdus）的弹幕多 `override Texture` 指向各自本体 png 并用 `ACMAsset` 加法绘制，非原版占位；少数仍发原版弹幕（`BrushofJudgment`/`SoulLanternStaff` 的 `LostSoulFriendly`、`NetherboneBow` 的 `HellfireArrow`）已在 §3.1 标注，按重做方向改为自绘弹幕即可。
