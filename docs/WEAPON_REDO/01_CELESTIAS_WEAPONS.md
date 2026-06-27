# 天庭线武器盘点与现代化重做计划（Celestias Weapons）

> **文档性质：** 天庭线（`Celestias/`）武器**纹理占位甄别 + 真武器现代化重做规格**
> **生成日期：** 2026-06-27（已按 PowerShell 实测核对修订）
> **判定基准：** 全部基于仓库实测代码（各武器 `ModItem` 的 `Texture` 重载）+ **PowerShell `Get-ChildItem -Recurse -Filter *.png`** 实测 PNG 实存
> **判定口径：** 只看**武器物品本体（ModItem 类）**的纹理，不看其发射的 `ModProjectile`
> **交叉引用：** `docs/PLACEHOLDER_CONTENT_REGISTRY.md` · `docs/BOSS_REDO_V2/00_SHADER_VFX_TOOLKIT.md`

> **纹理实测基线（PowerShell 核对）：** 全仓 PNG 总数 = **626**，其中 `Celestias/` 目录下 = **120** 张。
> 规则：**ModItem 无 `Texture` 重载时，默认纹理路径 =「命名空间转目录 + 类名」；只要对应 `.png` 与 `.cs` 同目录存在，即视为有自定义纹理。** 经实测，敖广 / 观察者 / 天龙 / 祖龙 / 天柱 共 26 件武器的本体 PNG **全部真实存在**，本次已修正为「保留」。

---

## 〇、判定规则回顾

| ModItem 的 `Texture =>` | 判定 |
|------|------|
| `"Terraria/Images/Item_" + ItemID.X`（显式复用原版物品图标） | **原版复用 ✗ → 排除**（无论 PNG 是否存在） |
| `"InnoVault/Assets/placeholder"` / 占位空白 | **占位 ✗ → 排除** |
| `"AncientChineseMythology/.../X"` 自定义路径 → 实测 PNG | 存在=**保留 ✓** / 不存在=纹理缺失→排除 |
| **无 `Texture` 重载** → 默认路径=命名空间转目录+类名 → 实测 PNG | 存在=**保留 ✓** / 不存在=纹理缺失→排除 |

---

## 一、盘点结果总表（按 Boss / 来源分组）

类型缩写：近=近战 · 远=远程 · 魔=魔法 · 召=召唤 · 鞭=鞭。
纹理来源：`原版✗`=显式复用原版物品图标 · `自定义✓`=同目录自定义 PNG 实存（PowerShell 核对）。

### 1.1 敖广 AoGuang — `Celestias/Boss/AoGuangs/Items/`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `JadeDragonChakram` 玉龙环刃 | 近(回旋) | 自定义✓ `JadeDragonChakram.png` | **保留 ✓** |
| `TsunamiPiercer` 海啸龙枪 | 近(枪) | 自定义✓ `TsunamiPiercer.png` | **保留 ✓** |
| `AbyssalDragonblade` 深渊龙刀 | 近 | 自定义✓ `AbyssalDragonblade.png` | **保留 ✓** |
| `TidecallersDecree` 潮涌龙杖 | 魔 | 自定义✓ `TidecallersDecree.png` | **保留 ✓** |
| `MaelstromBow` 漩涡龙弓 | 远 | 自定义✓ `MaelstromBow.png` | **保留 ✓** |

### 1.2 敖钦 Aokin — `Celestias/Boss/Aokins/Items/AokinWeapons.cs`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `InfernoDragonSpear` 焚天龙枪 | 近 | 原版✗ `Item_Gungnir` | 排除 |
| `FlamecoilChakram` 焰缠双环 | 近(回旋) | 原版✗ `Item_LightDisc` | 排除 |
| `CrimsonMaelstromBow` 赤潮旋涡弓 | 远 | 原版✗ `Item_Marrow` | 排除 |
| `DraconicEmber` 龙魂余烬杖 | 召 | 原版✗ `Item_PygmyStaff` | 排除 |
| `MeteorCallerStaff` 唤流星杖 | 魔 | 原版✗ `Item_AmberStaff` | 排除 |

### 1.3 敖闰 Aoyuan — `Celestias/Boss/Aoyuans/Items/AoyuanWeapons.cs`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `GlacialDragonblade` 冰龙镰刃 | 近 | 原版✗ `Item_IceSickle` | 排除 |
| `PermafrostTrident` 永冻三叉戟 | 近(矛) | 原版✗ `Item_Trident` | 排除 |
| `VortexPrimordialStain` 漩涡原染魔典 | 魔 | 原版✗ `Item_BookofSkulls` | 排除 |
| `InkscaledFlowFan` 墨鳞流风扇 | 魔 | 原版✗ `Item_MagicMirror` | 排除 |
| `BlizzardPiercer` 暴雪穿云弓 | 远 | 原版✗ `Item_IceBow` | 排除 |

### 1.4 敖顺 Aoshun — `Celestias/Boss/Aoshuns/Items/AoshunWeapons.cs`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `ThunderlordHalberd` 雷尊方天戟 | 近(戟) | 原版✗ `Item_Gungnir` | 排除 |
| `StormchainWhip` 风暴链鞭 | 鞭 | 原版✗ `Item_ThornWhip` | 排除 |
| `TempestRepeater` 暴风连弩 | 远 | 原版✗ `Item_VenusMagnum` | 排除 |
| `LightningEdictTome` 雷敕天书 | 魔 | 原版✗ `Item_BookofSkulls` | 排除 |
| `AzureRuinBlade` 苍海毁刃 | 近 | 原版✗ `Item_BreakerBlade` | 排除 |

### 1.5 神威 Vigor — `Celestias/Boss/Vigors/Items/VigorWeapons.cs`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `SinSeveringBlade` 断罪巨剑 | 近 | 原版✗ `Item_BreakerBlade` | 排除 |
| `AureateVoidrender` 辉金虚空斩裂刃 | 近 | 原版✗ `Item_Excalibur` | 排除 |
| `VerdictSealHammer` 裁决印锤 | 近(锤) | 原版✗ `Item_PaladinsHammer` | 排除 |

### 1.6 百目 Argus — `Celestias/Boss/Arguses/Items/ArgusWeapons.cs`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `SoulPiercingArc` 穿魂弧弓 | 远 | 原版✗ `Item_PulseBow` | 排除 |
| `LuminanceStellarCannon` 光华星炮 | 远 | 原版✗ `Item_VortexBeater` | 排除 |
| `LuminousIrisAnnihilator` 虹膜湮灭手铳 | 远 | 原版✗ `Item_Handgun` | 排除 |

### 1.7 毗沙门 Vaisravana — `Celestias/Boss/Vaisravanas/Items/VaisravanaItems.cs`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `TreasurePagodaStaff` 宝塔法杖 | 魔 | 原版✗ `Item_RainbowRod` | 排除 |
| `VaultshadeVoidshot` 库藏虚空狙 | 远 | 原版✗ `Item_SniperRifle` | 排除 |
| `CelestialCircletScepter` 天冠权杖 | 魔 | 原版✗ `Item_StaffofRegrowth` | 排除 |
| `TreasurePagodaCharm` 宝塔护符 | 饰品(防御) | 原版✗ `Item_PaladinsShield` | 排除（饰品） |

### 1.8 四圣兽 FourSacredBeasts — `Celestias/Boss/FourSacredBeasts/Items/FourSacredBeastWeapons.cs`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `AzureTorrentBlades` 青水流光双刃 | 近 | 原版✗ `Item_Excalibur` | 排除 |
| `WindserpentDao` 风蛇刀 | 近 | 原版✗ `Item_BreakerBlade` | 排除 |
| `ThunderclapLongbow` 雷鼓长弓 | 远 | 原版✗ `Item_PulseBow` | 排除 |
| `AurelianCataclysmSmasher` 金纹灾锤 | 近(锤) | 原版✗ `Item_PaladinsHammer` | 排除 |
| `ArgentPulseObliterator` 银脉冲枪 | 远 | 原版✗ `Item_VortexBeater` | 排除 |
| `WhiteTigerClaws` 虎爪拳套 | 近(拳) | 原版✗ `Item_FeralClaws` | 排除 |
| `StarfireAnnihilator` 珊瑚星火枪 | 远 | 原版✗ `Item_VortexBeater` | 排除 |
| `SolarisEternalVerdict` 日轮永裁 | 召 | 原版✗ `Item_OpticStaff` | 排除 |
| `PhoenixFlameStaff` 凤凰焰杖 | 魔 | 原版✗ `Item_RainbowRod` | 排除 |
| `GeocrystalShatterblade` 地晶碎刃 | 近 | 原版✗ `Item_BreakerBlade` | 排除 |
| `GeoarchonRupturer` 地能裂法 | 魔 | 原版✗ `Item_StaffofEarth` | 排除 |
| `BlackTortoiseShield` 玄龟盾 | 近(盾攻) | 原版✗ `Item_AnkhShield` | 排除 |

### 1.9 观察者 CelestialOverseers — `Celestias/Boss/CelestialOverseers/Items/`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `JadeDragonCloudDao` 玉龙云刀 | 近 | 自定义✓ `JadeDragonCloudDao.png` | **保留 ✓** |
| `CelestialMechanismBow` 天机弓 | 远 | 自定义✓ `CelestialMechanismBow.png` | **保留 ✓** |
| `CelestialGearGreatsword` 天机齿轮巨剑 | 近 | 自定义✓ `CelestialGearGreatsword.png` | **保留 ✓** |
| `CelestialJudgmentChakram` 天罚审判轮 | 近(回旋) | 自定义✓ `CelestialJudgmentChakram.png` | **保留 ✓** |
| `CelestialWatcherStaff` 天眼监察杖 | 魔 | 自定义✓ `CelestialWatcherStaff.png` | **保留 ✓** |
| `AllSeeingJadeTome` 洞察玉典 | 魔 | 自定义✓ `AllSeeingJadeTome.png` | **保留 ✓** |
| `GoldenPhoenixSummonStaff` 金凤召唤杖 | 召 | 自定义✓ `GoldenPhoenixSummonStaff.png` | **保留 ✓** |
| `ClockworkPhoenixSpear` 机关凤凰枪 | 近(枪) | 自定义✓ `ClockworkPhoenixSpear.png` | **保留 ✓** |

### 1.10 天龙 CelestialDragons — `Celestias/Boss/CelestialDragons/Items/`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `ScalebreakerCleaver` 逆鳞 | 近(巨刀) | 自定义✓ `ScalebreakerCleaver.png` | **保留 ✓** |
| `CelestialEdictScepter` 敕令 | 魔 | 自定义✓ `CelestialEdictScepter.png` | **保留 ✓** |
| `SkyrendDragonbreathLongbow` 裂穹 | 远 | 自定义✓ `SkyrendDragonbreathLongbow.png` | **保留 ✓** |

### 1.11 祖龙 AncestralDragonSouls — `Celestias/Boss/AncestralDragonSouls/Items/`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `ArchosaurFerrara` 祖龙残剑 | 近 | 自定义✓ `ArchosaurFerrara.png` | **保留 ✓** |
| `ArchosaurBow` 祖龙神弓 | 远 | 自定义✓ `ArchosaurBow.png` | **保留 ✓** |
| `ArchosaurStaff` 祖龙法杖 | 魔 | 自定义✓ `ArchosaurStaff.png` | **保留 ✓** |

### 1.12 天柱 PillarofTheHeavenes — `Celestias/PillarofTheHeavenes/Items/`（合成获得，天界碎片 + 天极锭）

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `FirmamentCleaver` 苍穹巨剑 | 近 | 自定义✓ `FirmamentCleaver.png` | **保留 ✓** |
| `PillarGuardiansHalberd` 天柱守卫戟 | 近 | 自定义✓ `PillarGuardiansHalberd.png`（本体显式重载该路径，PNG 实存） | **保留 ✓** |
| `JadeArmillary` 玉衡浑仪 | 近(回旋) | 自定义✓ `JadeArmillary.png`（本体显式重载该路径，PNG 实存） | **保留 ✓** |
| `Cloudpiercer` 穿云弓 | 远 | 自定义✓ `Cloudpiercer.png` | **保留 ✓** |
| `ThunderclapHandcannon` 雷霆手炮 | 远 | 自定义✓ `ThunderclapHandcannon.png` | **保留 ✓** |
| `TomeofDivineLaw` 天律宝典 | 魔 | 自定义✓ `TomeofDivineLaw.png` | **保留 ✓** |
| `ScepterofTheOverseer` 监天权杖 | 魔 | 自定义✓ `ScepterofTheOverseer.png` | **保留 ✓** |
| `HeavenFragment` 天界碎片 | 材料 | — | 非武器，不计入 |
| `HeavenInvasionSummon` 天界入侵召唤物 | 召唤BOSS物品 | — | 非武器，不计入 |

### 1.13 大椿 Dazhengs — `Celestias/Boss/Dazhengs/Items/`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `TheNaturalAxe` 自然之斧 | 近(斧/工具) | 自定义✓ `TheNaturalAxe.png` | **保留 ✓** |
| `ArrogantDivineSylvan` 傲世神木 | 材料(无伤害) | 自定义✓ | 非武器（材料），不计入 |

### 1.14 林地 Dryades — `Celestias/Boss/Dryades/Items/`

| 类名 | 类型 | 纹理来源 | 结论 |
|------|------|----------|------|
| `Livinglog` 活木 | 材料(无伤害) | 自定义✓ | 非武器（材料），不计入 |

---

## 二、排除清单（精简）—— 仅「原版纹理复用」一类

> 经修订，天庭线**唯一的占位类型是 §2.1 的 36 件武器 + 1 饰品**，它们显式 `Texture => "Terraria/Images/Item_..."` 复用原版物品图标。按用户口径，这一类**无论 PNG 是否存在都排除**，需补本体 PNG 才能转「保留」。**不再存在「纹理缺失」类武器。**

### 2.1 原版纹理复用（VANILLA_REF，36 件武器 + 1 饰品 = 37 项）

- **敖钦×5：** `InfernoDragonSpear` `FlamecoilChakram` `CrimsonMaelstromBow` `DraconicEmber` `MeteorCallerStaff`
- **敖闰×5：** `GlacialDragonblade` `PermafrostTrident` `VortexPrimordialStain` `InkscaledFlowFan` `BlizzardPiercer`
- **敖顺×5：** `ThunderlordHalberd` `StormchainWhip` `TempestRepeater` `LightningEdictTome` `AzureRuinBlade`
- **神威×3：** `SinSeveringBlade` `AureateVoidrender` `VerdictSealHammer`
- **百目×3：** `SoulPiercingArc` `LuminanceStellarCannon` `LuminousIrisAnnihilator`
- **毗沙门×3+1：** `TreasurePagodaStaff` `VaultshadeVoidshot` `CelestialCircletScepter`（+ 饰品 `TreasurePagodaCharm`）
- **四圣兽×12：** `AzureTorrentBlades` `WindserpentDao` `ThunderclapLongbow` `AurelianCataclysmSmasher` `ArgentPulseObliterator` `WhiteTigerClaws` `StarfireAnnihilator` `SolarisEternalVerdict` `PhoenixFlameStaff` `GeocrystalShatterblade` `GeoarchonRupturer` `BlackTortoiseShield`

> 这 37 项**机制已实装且弹幕 VFX 丰富**（程序化拖尾 / orb / 标记叠层引爆 / 三段式持握挥砍等），仅**物品本体沿用原版图标**。补上自定义 PNG 即可转「保留」，详见 §四。

### 校正后统计

| 分类 | 数量 |
|------|------|
| 盘点武器总数（含伤害的 ModItem） | **63** |
| **保留（自定义 PNG 实存）** | **27** = `TheNaturalAxe` + 敖广5 + 观察者8 + 天龙3 + 祖龙3 + 天柱7 |
| 排除 · 原版复用（需补 PNG） | 36 武器 + 1 饰品 |
| 非武器（材料/召唤物，未计入） | `ArrogantDivineSylvan` `Livinglog` `HeavenFragment` `HeavenInvasionSummon` 等 |

---

## 三、保留武器重做计划（27 件）

> 机制概述均读真实代码总结；着色器全部引用 `Effects/` 既有资产（`BeamGrad` `RadialBloom` `DissolveBurn` `GenericWarp` `PaletteLUT` `ReflectWard` `ArenaRunic` `ElementalScreenTint`）+ `ACMUtils.BuildRibbonStrip` 顶点拖尾 + `ACMAsset.*` 现成贴图，遵守 `00_SHADER_VFX_TOOLKIT.md` §C 守则（同屏全屏后处理 ≤1、Effect 仅缓存一次、服务端零绘制、红色仅作致命预警）。

### 3.0 `TheNaturalAxe` 自然之斧（大椿，近/斧·工具，伤 200）

- **当前机制：** `axe=55`（≈275% 斧力）+ `tileBoost=4` 高速挥砍；右键 `AltFunctionUse` 切「种树模式」，在光标 6 格半径向裸露草地批量种树苗（≤5 株，含多人同步 + 草尘）。无拖尾/无命中反馈/无挥砍弹幕。
- **现代化方向：** 强化「斧伐木 · 斧植林」的自然循环身份——左键命中累积「年轮」层，右键消耗年轮在光标催生**临时灵木桩**（`ModProjectile` 哨戒）向附近敌甩藤蔓鞭击（DoT+减速）数秒后枯萎，把种树从纯探索升级为战场布置；挥砍改三段式持握（参考 `AzureRuinBladeSwing`），中段甩**翠金「木叶斩浪」**。
- **着色器/VFX：** 挥砍拖尾 `BuildRibbonStrip` 双层（外暗翠+内亮金）贴 `SwordTrail*`；斩浪 `ACMAsset.GlaciateWave`+`SlashBurst`；灵木桩范围用 `ArenaRunic.fx`（改翠金 + 藤蔓纹）；「繁茂」状态轻量 `ElementalScreenTint.fx` 翠染（≤0.15）。配色用 `HeavenlyEffect.heavenlyColors` 金白/翠玉。

---

### 3.1 敖广 AoGuang · 东海水龙（5 件，月后过渡，伤 260–380）

**共享基调：** 东海潮汐/水龙，色板 `AoGuangHelper` 的 `OceanTeal / DragonBlue / WaterGlow / PureWhite`；当前用 `DustID.Water/BlueTorch/Wet` 堆水花，龙卷复用原版 `SandnadoHostile` 纹理。多件带「海洋之力 / 潮力」蓄能槽，满槽放大招。

- **`JadeDragonChakram` 玉龙环刃（近·回旋 320）** — 投掷旋转飞回（速度衰减/超时自动返回），命中 5 次释放 6 向 `ChakramWaterBurst` 水波，返回程 ×1.5 伤、附减速。**重做：** 飞行时真正「吸水成漩涡」——用 `GenericWarp.fx` 在环周做局部水面折射；拖尾改 `BuildRibbonStrip` 螺旋双环。
- **`TsunamiPiercer` 海啸龙枪（近·枪 350）** — 突刺 `TsunamiPiercerThrust`，每 4 刺放海啸冲击波 `TsunamiWaveThrust`，命中积「海洋之力」满力触发**龙翔突刺**（玩家短暂无敌冲刺 + `DragonSoaringThrust`）。**重做：** 龙翔突刺是天然 set-piece——冲刺瞬间 `ElementalScreenTint.fx` 冰蓝定格 + `RadialBloom.fx` 起手辉光 + `BuildRibbonStrip` 画水龙形身段；海啸波叠 `GenericWarp.fx` 热浪式水折射。
- **`AbyssalDragonblade` 深渊龙刀（近 380）** — 每 3 刀 `DragonTidalSlash` 水龙斩波，命中积「潮力」满潮召 `MiniWaterTornado`。**重做：** 把潮力可视化为刀身水位上涨光效（`SoftGlow` 叠色）；斩波用 `GlaciateWave` ribbon + `RadialBloom` 命中环。
- **`TidecallersDecree` 潮涌龙杖（魔 260）** — 追踪 `TidalDragonSpirit`（蛇形水龙），每 5 次施法召中型 `SummonedWaterTornado`（吸怪）。**重做：** 水龙灵改 `BuildRibbonStrip` 真蛇身（替代 dust 堆叠）；龙卷叠 `GenericWarp.fx` 折射。
- **`MaelstromBow` 漩涡龙弓（远 280）** — 箭→`DragonWaterArrow`（轻追踪），蓄力满发 `TornadoArrow`（吸怪巨龙卷箭）。**重做：** 蓄力环用 `RadialBloom.fx`；龙卷箭命中 `GenericWarp.fx` 漩涡塌陷。

> **组级个性化：** 敖广是四海之首，建议让其 5 件与后续敖钦/敖闰/敖顺三王共享一套「龙鳞」叠层协同（命中叠层、满层全武器增伤），并统一用 `PaletteLUT.fx` 锁定「东海青」色板，避免各 dust 颜色发散。

---

### 3.2 观察者 CelestialOverseers · 机关飞升·金青（8 件，月后终局，伤 1400–3380）

**共享基调：** 天庭机关 / 齿轮 / 天眼 / 金凤，色板金（`GoldCoin/GoldFlame`）+ 翠绿（`GreenTorch`，玉龙系）+ 机械火花（`Torch`）。多数为「每 N 次攻击触发大招」。

- **`JadeDragonCloudDao` 玉龙云刀（近 3380）** — 剑气 `JadeDragonSlash`，每 3 次龙形大斩 `JadeDragonWave`；暴击「玉龙之息」爆发、1/4 几率天机印记减速。
- **`CelestialMechanismBow` 天机弓（远 2280）** — 转神圣光箭 `CelestialLightArrow`（追踪）主+左右辅箭，每 5 次召**光柱箭雨** `CelestialRainArrow` 从天而降。
- **`CelestialGearGreatsword` 天机齿轮巨剑（近 2420）** — 抛旋转齿轮 `CelestialGearProjectile`（多段穿透），每 4 次**齿轮风暴** `CelestialGearStorm` 六向；暴击撕裂（着火）+ 齿轮爆发。
- **`CelestialJudgmentChakram` 天罚审判轮（近·回旋 2320）** — Flying→Tracking→Returning 三态追踪回旋，命中释放审判光环，最多 2 把。
- **`CelestialWatcherStaff` 天眼监察杖（魔 1400）** — 召 3 道神圣光柱 `CelestialWatcherPillar` 从天而降（类 Boss 审判），低血减蓝耗。
- **`AllSeeingJadeTome` 洞察玉典（魔 2350）** — 鼠标处召天眼 `AllSeeingEyeProjectile` 注视并发追踪光束。
- **`GoldenPhoenixSummonStaff` 金凤召唤杖（召 2300）** — 召金凤 `GoldenPhoenixMinion`（Idle/Targeting/Attacking/Diving 状态机）自动攻击 + 火焰 + 俯冲。
- **`ClockworkPhoenixSpear` 机关凤凰枪（近·枪 3360）** — Prepare/Thrust/Retract 手持突刺，突刺释放凤凰火焰。

> **组级现代化 + VFX：**
> - **统一金青色板：** 用 `PaletteLUT.fx` 把全组 dust/弹幕统一为「机关金 + 天青」，确立观察者线视觉身份。
> - **「天机蓄能」可视化：** 把分散的「每 N 次」触发改为头顶/武器旁的 `RadialBloom.fx` 充能环，满环放大招，给玩家清晰的节奏反馈。
> - **致命预警（红色）：** 光柱箭雨 / 天眼审判 / 落点用 `ArenaRunic.fx` 画地面预警法阵，命中前 0.3–0.6s 渐强（遵守 §C.1 红=致命）。
> - **凤凰系（金凤/机关枪）：** 涅槃重生 / 俯冲灼烧用 `DissolveBurn.fx`（噪声裁切 + 灼烧边）+ `BuildRibbonStrip` 火羽拖尾。
> - **个性化协同：** 天眼系（监察杖 + 玉典）命中叠「洞察」标记，使全套对该目标增伤，呼应「观察者」主题。

---

### 3.3 天龙 CelestialDragons · 金龙巡卫（3 件，伤 3860–4680，蓄能/怒气）

**共享基调：** 金龙逆鳞 / 天庭权柄 / 龙息，纯金色 `GoldFlame/GoldCoin`；普遍带「龙威怒气 / 蓄力」资源。

- **`ScalebreakerCleaver` 逆鳞（近·巨刀 4680）** — 龙气斩 `DragonAuraSlash`，每 3 刀**巨龙咆哮波** `DragonRoarWave`，命中积「龙威」满怒触发**逆鳞之怒**（8 向 `ReverseScaleWrath`）。
- **`CelestialEdictScepter` 敕令（魔 4120·蓄力）** — 敕令符咒 `CelestialEdictSeal`（追踪引天雷），满蓄召**龙威法阵** `DragonAuthorityCircle`（天庭审判）。
- **`SkyrendDragonbreathLongbow` 裂穹（远 3860·蓄力）** — 龙息箭 `DragonbreathArrow`（命中爆炸 + 龙息云），满蓄发**裂天龙箭** `SkyrendDragonArrow`。

> **组级现代化 + VFX：**
> - **统一「龙威」资源：** 把逆鳞怒气 / 敕令蓄力 / 裂穹蓄力做成共享「龙威」叠层（全套协同：龙威满时三件大招强化）。
> - **金龙真身演出：** 满龙威 / 大招时召半透明金龙虚影协同，用 `BuildRibbonStrip` 画金龙形身段 + `RadialBloom.fx` 金芒。
> - **set-piece 着色器：** `DragonAuthorityCircle` 用 `ArenaRunic.fx`（龙纹法阵）；逆鳞之怒 / 裂天龙箭用 `GenericWarp.fx`（裂空热浪扭曲）+ `ElementalScreenTint.fx` 金闪定格；巨龙咆哮波叠 `BeamGrad.fx` 流动。

---

### 3.4 祖龙 AncestralDragonSouls · 残魂迷幻·白青（3 件，伤 4200–5200，全线最高）

**共享基调：** 远古龙魂残影，色板白青迷幻 `Cloud/WhiteTorch/Clentaminator_Cyan`。**本线已有专属天幕 `Effects/AncestralDragonSky.fx`（fbm 云海 + 龙鳞光轮 + Voronoi 星辰），是全模组最成熟的程序化天幕——武器演出应与之统一。**

- **`ArchosaurFerrara` 祖龙残剑（近 4800）** — 龙魂波动 `ArchosaurSoulWave`，连击 8 满放**祖龙吐息**（5 向 `ArchosaurBreathWave`），命中 `Frostburn2` 龙魂侵蚀。
- **`ArchosaurBow` 祖龙神弓（远 4200）** — 龙魂箭 `ArchosaurSoulArrow`（追踪三连散射），每 6 次召**龙魂箭雨** `ArchosaurRainArrow`。
- **`ArchosaurStaff` 祖龙法杖（魔 5200·全线伤害之最）** — 召龙魂法阵 `ArchosaurSoulCircle`（持续伤害区域，最多 4 个），每 10 次召**祖龙虚影** `ArchosaurPhantom` 毁灭打击。

> **组级现代化 + VFX：**
> - **与天幕同源演出：** 大招（祖龙虚影 / 吐息 / 箭雨）触发时，局部 VFX 复用 `AncestralDragonSky` 同款色板推进（玄青→紫芒→赤金），形成「祖龙真身降临」统一观感。
> - **龙魂残影：** 用 `DissolveBurn.fx`（残魂溶解/重凝）+ `BuildRibbonStrip` 半透明龙魂 ribbon 替代纯 dust 堆叠。
> - **虚空残魂：** `GenericWarp.fx` 做残魂扭曲；箭雨/虚影落点用 `ArenaRunic.fx` 白青预警 + `RadialBloom.fx`。
> - **个性化协同：** 命中叠「龙魂」印，祖龙虚影对印记目标附加打击（呼应「残魂共鸣」）。

---

### 3.5 天柱 PillarofTheHeavenes · 天柱守卫·金青（7 件，合成获得，伤 155–245，月后入门过渡套）

**共享基调：** 撑天柱 / 苍穹，色板金（`GoldFlame/GoldCoin`）+ 青（`IceTorch`）。**全部为合成获得**（`HeavenFragment`×10–15 + `EmpyriteBar`×15 @ 月球制作台），是月后入门过渡套，**机制最朴素**（多为单弹幕 / 每 N 次小招），现代化空间最大。

- **`FirmamentCleaver` 苍穹巨剑（近 245）** — 挥砍 `FirmamentSlash` 天柱剑气。
- **`PillarGuardiansHalberd` 天柱守卫戟（近 225）** — 突刺 `HalberdThrust` + 天柱冲击波（弹幕复用本体 PNG 绘制，纹理实存）。
- **`JadeArmillary` 玉衡浑仪（近·回旋 195）** — 浑天仪旋转飞行 `JadeArmillaryProjectile`，命中释放星辰碎片，最多 2。
- **`Cloudpiercer` 穿云弓（远 185）** — 扇形 3 支 `CloudpiercerArrow`（追踪穿云箭）。
- **`ThunderclapHandcannon` 雷霆手炮（远 175）** — `ThunderclapBlast` 雷霆弹，命中引发连锁闪电 + 后坐力屏震。
- **`TomeofDivineLaw` 天律宝典（魔 155）** — 符文弹 `DivineRune`（追踪），每 4 次召符文阵 `RuneCircle`。
- **`ScepterofTheOverseer` 监天权杖（魔 165）** — 双追踪天光球 `OverseerOrb`，命中爆炸。

> **组级现代化 + VFX：**
> - 因属入门过渡套，重点在**手感升级而非堆 set-piece**：用 `PaletteLUT.fx` 统一金青色板；穿云箭 / 天光球用 `BeamGrad.fx` 流动光束；雷霆手炮连锁闪电用 `ACMAsset.LightningBranch` + `BeamGrad.fx`；符文阵 / 玉衡浑仪用 `ArenaRunic.fx` 星图法阵；苍穹剑气 / 天柱冲击用 `GlaciateWave` ribbon（`BuildRibbonStrip`）。
> - **轻量协同：** 命中叠「天柱·镇」标记，使整套对标记目标小幅增伤，把 7 件零散过渡武器绑成一个有辨识度的「天柱套」。

---

## 四、待补 PNG 即转保留的占位武器（36 件原版复用 + 1 饰品）

> 以下武器**机制已实装、弹幕/标记/VFX 代码相当完整**（多数在占位注册表标为 COMPLETE），与「保留」的唯一差距是**物品本体显式复用了原版图标**（`Texture => "Terraria/Images/Item_..."`）。**补一张自定义本体 PNG 并删除该重载即可转保留。** 按现代化潜力优先级列出标杆：

### 4.1 `AzureRuinBlade` 苍海毁刃（敖顺 · 雷水终局刀，复用 `Item_BreakerBlade`）
- 已是完整三段式持握挥砍（`AzureRuinBladeSwing` Prepare/Execute/Unwind），中段甩「蔚蓝潮汐雷浪」`AzureRuinTidal`，每第三斩三道巨浪 + 连锁电弧 + 屏震。代码是天庭线近战标杆。
- **补图 + VFX：** 雷浪叠 `GenericWarp.fx` 水面折射、`BeamGrad.fx` 连锁电弧渐变、`RadialBloom.fx` 命中辉光。

### 4.2 `LuminousIrisAnnihilator` 虹膜湮灭手铳（百目 · 蓄力连射，复用 `Item_Handgun`）
- 完整蓄力（环聚虹膜 → 1/4/8 连发 → 满蓄召 `ArgusAllSeeingEye` 全视之眼自动连射），命中绽放金紫湮灭爆炸。
- **补图 + VFX：** 蓄力环 `RadialBloom.fx`；爆炸 `DissolveBurn.fx` + `PaletteLUT.fx` 金紫渐变；全视之眼 `ReflectWard.fx` 瞳膜折射。

### 4.3 `VerdictSealHammer` 裁决印锤（神威 · 重锤爆发，复用 `Item_PaladinsHammer`）
- 命中放全场裁决震波 + 八向脉冲 + 头顶蓄势符印轰落（`VerdictSealSigil`）+ 封印缓速。
- **补图 + VFX：** 震波 `RadialBloom.fx`；落锤 `ElementalScreenTint.fx` 金闪定格；符印 `ArenaRunic.fx`。

### 4.4 四圣兽 12 件 + 四海龙王（敖钦/敖闰/敖顺）15 件（复用各类原版图标）
- 整两大组（火/冰/雷龙王 + 青龙/白虎/朱雀/玄武）机制均 COMPLETE，弹幕丰富（叠层引爆 / orb 蛇形 / 召唤等）。**纯粹缺 27 张本体图标**，是天庭线**美术工程量最集中、补完后立刻成形**的一批。
- **建议：** 按元素主题成套补图，VFX 复用本文 §五工具箱，与 §3 各组手法保持一致。

### 4.5 毗沙门 3 件 + 宝塔护符（复用原版图标）
- 财神主题机制完整（宝塔层叠聚财 / 虚空坍缩劫财 / 天冠加冕爆发 / 受击反震宝塔虚影）。补「宝塔 / 库藏 / 天冠」金色图标即可转保留。

---

## 五、可复用着色器 / VFX 资产速查（来自 `Effects/` 实测）

| 资产 | 用途 | 适配武器场景 |
|------|------|--------------|
| `BeamGrad.fx` | 光束/激光渐变流动 | 弓箭/光球/连锁闪电/咆哮波 |
| `RadialBloom.fx` | 径向辉光/充能爆发 | 蓄力环、命中冲击、大招核心 |
| `DissolveBurn.fx` | 噪声裁切 + 灼烧边溶解 | 召唤显形、湮灭、涅槃、龙魂残影 |
| `GenericWarp.fx` | 主题可换的全屏/局部扭曲 | 水面/火浪/虚空/裂空折射 |
| `PaletteLUT.fx` | 调色板 LUT 统一配色 | 各线色板统一（东海青/机关金青/金/白青） |
| `ReflectWard.fx` | 反射/护罩折射 | 护体、瞳膜、结界 |
| `ArenaRunic.fx` | 通用法阵/封印地贴 | 法阵、标记桩、落点预警、龙纹审判 |
| `ElementalScreenTint.fx` | 元素屏幕染色 | 相变/大招定调（强度 ≤0.15，同屏 ≤1） |
| `ACMUtils.BuildRibbonStrip` | TriangleStrip 拖尾（纯 CPU） | **首选**挥砍残影/弹道/水龙·金龙·龙魂身段 |
| `ACMAsset.*`（`GlaciateWave`/`SoftGlow`/`LightShot`/`BlankStar`/`Sparkle`/`SlashBurst`/`LightningBranch`） | 现成弹幕贴图 | 拖尾/光晕/星芒/斩浪/电弧 |
| `AncestralDragonSky.fx` | 全屏程序化天幕（祖龙） | 祖龙线武器演出同源色板 |

> 性能/多人守则见 `00_SHADER_VFX_TOOLKIT.md` §C.4 / §C.5：Effect 只 `Request` 一次并 `static` 缓存；同屏全屏后处理 ≤1；服务端零绘制；`SpriteBatch` End/Begin 必须成对恢复默认状态；红色仅留给致命预警。

---

*本表基于 2026-06-27 仓库快照，所有类名、文件路径、`Texture` 重载与 PNG 实存均经代码与 PowerShell `Get-ChildItem` 实测核对（全仓 626 PNG / `Celestias/` 120 PNG）。*
