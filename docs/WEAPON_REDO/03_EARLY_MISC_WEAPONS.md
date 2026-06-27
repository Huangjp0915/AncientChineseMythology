# 前期 / 杂项武器 · 盘点与现代化重做计划

> **任务区域：** 除 `Celestias/` 与 `Underworlds/` 以外的全部武器（前期 / 杂项线）
> **生成日期：** 2026-06-27
> **判定基准：** 仅看武器**物品本体（ModItem 类）**的 `Texture` 解析路径与 `.png` 实存（不看其发射的 `ModProjectile`）
> **交叉引用：** `docs/BOSS_REDO_V2/00_SHADER_VFX_TOOLKIT.md`（§A/§B/§C 着色器原语与守则）· `docs/PLACEHOLDER_CONTENT_REGISTRY.md`（占位登记）· `Effects/*.fx` · `Effects/CompileFX.ps1`

---

## 〇、判定方法与纹理结论

本仓库武器贴图存在**两种存放约定**（均已实测确认）：

1. **`Textures/` 镜像目录**：`override string Texture => "AncientChineseMythology/Textures/Items/Weapons/..."`（剑 / 棍 / 召唤杖 / 弓 / 法杖 / 符咒线用此约定）。
2. **贴图与 `.cs` 同目录**：默认路径（无 `Texture` 重载，命名空间转目录）或显式 `"AncientChineseMythology/Items/Weapons/..."`（林地 / 神木 / 傲世神木 / 亵渎 / 镇尸钉 / 万魂幡线用此约定）。

> 关键：林地系、神木系、傲世神木系、亵渎系的 `ModItem` **本体多数没有 `Texture` 重载**，走默认路径，`.png` 与 `.cs` 同目录且**全部实存**；它们文件里出现的 `Terraria/Images/Projectile_...` 等重载都属于**发射的弹幕**，按规则不影响本体判定。

**总计：保留 61 件真武器；排除 19 项**（4 件「本体复用原版纹理」占位 + 15 项非武器 / 已由他人负责的召唤·饰品·坐骑）。

---

## 一、盘点结果总表（按来源 / 进度阶段分组）

### 1.1 前期手工线 — 剑 / 棍 / 弓 / 法杖（`Textures/` 约定）

| 类名 | 文件路径 | 武器类型 | 本体纹理来源判定 | 结论 |
|------|----------|----------|------------------|------|
| `BronzeSword` | `Items/Weapons/Swords/BronzeSword.cs` | 近战·剑(8) | `Textures/Items/Weapons/Swords/BronzeSword.png` ✓ | **保留** |
| `CrimsonbronzeSword` | `Items/Weapons/Swords/CrimsonbronzeSword.cs` | 近战·剑(59) | `Textures/.../Swords/CrimsonbronzeSword.png` ✓ | **保留** |
| `XuanTieSword` | `Items/Weapons/Swords/XuanTieSword.cs` | 近战·剑(13) | `Textures/.../Swords/XuanTieSword.png` ✓ | **保留** |
| `BoneSword` | `Items/Weapons/Swords/BoneSword.cs` | 近战·剑(3) | `Textures/.../Swords/BoneSword.png` ✓ | **保留** |
| `BlackBearSword` | `Items/Weapons/Swords/BlackBearSword.cs` | 近战·重剑(47) | `Textures/.../Swords/BlackBearSword.png` ✓ | **保留** |
| `GanJiangSword` | `Items/Weapons/Swords/GanJiangSword.cs` | 近战·剑(84) | `Textures/.../Swords/GanJiangSword.png` ✓ | **保留** |
| `YuChangSword` | `Items/Weapons/Swords/YuChangSword.cs` | 近战·刺剑(35) | `Textures/.../Swords/YuChangSword.png` ✓ | **保留** |
| `WoodenStick` | `Items/Weapons/Sticks/WoodenStick.cs` | 近战·长矛(8) | `Textures/.../Sticks/WoodenStick.png` ✓ | **保留** |
| `IronStick` | `Items/Weapons/Sticks/IronStick.cs` | 近战·长矛(28) | `Textures/.../Sticks/IronStick.png` ✓ | **保留** |
| `GoldenStick` | `Items/Weapons/Sticks/GoldenStick.cs` | 近战·长矛(48) | `Textures/.../Sticks/GoldenStick.png` ✓ | **保留** |
| `GemStick` | `Items/Weapons/Sticks/GemStick.cs` | 近战·长矛(68) | `Textures/.../Sticks/GemStick.png` ✓ | **保留** |
| `RuyiStick` | `Items/Weapons/Sticks/RuyiStick.cs` | 近战·长矛(120) | `Textures/.../Sticks/RuyiStick.png` ✓ | **保留** |
| `BlackBearBow` | `Items/Weapons/Bows/BlackBearBow.cs` | 远程·弓(20) | `Textures/.../Bows/BlackBearBow.png` ✓ | **保留** |
| `BlackBearStaff` | `Items/Weapons/SummoningStaffs/BlackBearStaff.cs` | 召唤(25) | `Textures/.../Summoning Staffs/BlackBearStaff.png` ✓ | **保留** |
| `MingCrowStaff` | `Items/Weapons/SummoningStaffs/MingCrowStaff.cs` | 召唤·冥鸦(12) | `Textures/.../Summoning Staffs/MingCrowStaff.png` ✓ | **保留** |
| `Pufferfish` | `Items/Weapons/Staffs/Pufferfish.cs` | 魔法(1111) | `Textures/.../Staffs/Pufferfish.png` ✓ | **保留** |

### 1.2 林地线（WoF 前～后） — `Items/Weapons/Woodlands/`（同目录约定）

| 类名 | 文件路径 | 武器类型 | 本体纹理来源判定 | 结论 |
|------|----------|----------|------------------|------|
| `WoodlandGreatsword` | `.../Woodlands/WoodlandGreatsword.cs` | 近战·大剑(16) | 默认路径 → `Items/Weapons/Woodlands/WoodlandGreatsword.png` ✓ | **保留** |
| `DeadwoodMusket` | `.../Woodlands/DeadwoodMusket.cs` | 远程·铳(14) | 默认 → `.../Woodlands/DeadwoodMusket.png` ✓ | **保留** |
| `EmeraldTwigStaff` | `.../Woodlands/EmeraldTwigStaff.cs` | 魔法·法杖(16) | 默认 → `.../Woodlands/EmeraldTwigStaff.png` ✓ | **保留** |
| `NatureGrimoire` | `.../Woodlands/NatureGrimoire.cs` | 魔法·书(13) | 默认 → `.../Woodlands/NatureGrimoire.png` ✓ | **保留** |
| `RootBoomerang` | `.../Woodlands/RootBoomerang.cs` | 近战·回旋镖(13) | 默认 → `.../Woodlands/RootBoomerang.png` ✓ | **保留** |
| `MossBomb` | `.../Woodlands/MossBomb.cs` | 远程·投掷(20) | 默认 → `.../Woodlands/MossBomb.png` ✓ | **保留** |
| `VineHunterBow` | `.../Woodlands/VineHunterBow.cs` | 远程·弓(11) | 默认 → `.../Woodlands/VineHunterBow.png` ✓ | **保留** |

### 1.3 林地升级线（赤铜 / 玄铁过渡） — `Items/Weapons/Woodlands/Upgrades/`

| 类名 | 文件路径 | 武器类型 | 本体纹理来源判定 | 结论 |
|------|----------|----------|------------------|------|
| `CupriteWoodlandGreatsword` | `.../Upgrades/CupriteWoodlandGreatsword.cs` | 近战·大剑(42) | 默认 → 同目录 `.png` ✓ | **保留** |
| `CupriteDeadwoodMusket` | `.../Upgrades/CupriteDeadwoodMusket.cs` | 远程·铳 | 默认 → 同目录 `.png` ✓ | **保留** |
| `CupriteEmeraldTwigStaff` | `.../Upgrades/CupriteEmeraldTwigStaff.cs` | 魔法·法杖 | 默认 → 同目录 `.png` ✓ | **保留** |
| `CupriteNatureGrimoire` | `.../Upgrades/CupriteNatureGrimoire.cs` | 魔法·书 | 默认 → 同目录 `.png` ✓ | **保留** |
| `CupriteMossBomb` | `.../Upgrades/CupriteMossBomb.cs` | 远程·投掷 | 默认 → 同目录 `.png` ✓ | **保留** |
| `XuanTieHunterBow` | `.../Upgrades/XuanTieHunterBow.cs` | 远程·弓(38) | 默认 → 同目录 `.png` ✓ | **保留** |
| `XuanTieRootBoomerang` | `.../Upgrades/XuanTieRootBoomerang.cs` | 近战·回旋镖 | 默认 → 同目录 `.png` ✓ | **保留** |

### 1.4 神木线（困难模式 · 树妖/活木材料） — `Items/Weapons/DivineWoods/`

| 类名 | 文件路径 | 武器类型 | 本体纹理来源判定 | 结论 |
|------|----------|----------|------------------|------|
| `DivineWoodGreatblade` | `.../DivineWoods/DivineWoodGreatblade.cs` | 近战·持握大刀(190) | 默认 → 同目录 `.png` ✓ | **保留** |
| `DivineWoodGyratingLeaf` | `.../DivineWoods/DivineWoodGyratingLeaf.cs` | 近战·回旋镖(175) | 默认 → 同目录 `.png` ✓ | **保留** |
| `DivineWoodScepter` | `.../DivineWoods/DivineWoodScepter.cs` | 魔法·藤鞭法杖(155) | 默认 → 同目录 `.png` ✓ | **保留** |
| `DivineWoodMusket` | `.../DivineWoods/DivineWoodMusket.cs` | 远程·三连铳(140) | 默认 → 同目录 `.png` ✓ | **保留** |
| `DivineWoodLongbow` | `.../DivineWoods/DivineWoodLongbow.cs` | 远程·弓(155) | 默认 → 同目录 `.png` ✓ | **保留** |
| `DivineWoodTome` | `.../DivineWoods/DivineWoodTome.cs` | 魔法·书(165) | 默认 → 同目录 `.png` ✓ | **保留** |
| `DivineWoodBomb` | `.../DivineWoods/DivineWoodBomb.cs` | 远程·投掷(200) | 默认 → 同目录 `.png` ✓ | **保留** |

### 1.5 傲世神木线（天庭 · 大椿掉落升华，神木 → 终极形态） — `Items/Weapons/ArrogantDivineSylvans/`

| 类名 | 文件路径 | 武器类型 | 本体纹理来源判定 | 结论 |
|------|----------|----------|------------------|------|
| `ArrogantDivineSylvanGreatblade` | `.../ArrogantDivineSylvans/ArrogantDivineSylvanGreatblade.cs` | 近战·三连斩(1700) | 默认 → 同目录 `.png` ✓ | **保留** |
| `ArrogantDivineSylvanChakram` | `.../ArrogantDivineSylvanChakram.cs` | 近战·风暴回旋镖(1500) | 默认 → 同目录 `.png` ✓ | **保留** |
| `ArrogantDivineSylvanStaff` | `.../ArrogantDivineSylvanStaff.cs` | 魔法·万藤杖(1400) | 默认 → 同目录 `.png` ✓ | **保留** |
| `ArrogantDivineSylvanMusket` | `.../ArrogantDivineSylvanMusket.cs` | 远程·五连弩(300) | 默认 → 同目录 `.png` ✓ | **保留** |
| `ArrogantDivineSylvanBow` | `.../ArrogantDivineSylvanBow.cs` | 远程·穿林弓(1400) | 默认 → 同目录 `.png` ✓ | **保留** |
| `ArrogantDivineSylvanTome` | `.../ArrogantDivineSylvanTome.cs` | 魔法·山海典(1500) | 默认 → 同目录 `.png` ✓ | **保留** |
| `ArrogantDivineSylvanBomb` | `.../ArrogantDivineSylvanBomb.cs` | 远程·世界种(1800) | 默认 → 同目录 `.png` ✓ | **保留** |

### 1.6 亵渎线（血肉 / 猩红主题） — `Items/Weapons/Profanes/`

| 类名 | 文件路径 | 武器类型 | 本体纹理来源判定 | 结论 |
|------|----------|----------|------------------|------|
| `ProfaneDismemberer` | `.../Profanes/ProfaneDismemberer.cs` | 近战·持握大剑(1400) | 默认 → 同目录 `.png` ✓ | **保留** |
| `GluttonousFleshrang` | `.../Profanes/GluttonousFleshrang.cs` | 近战·吸血回旋镖(1350) | 默认 → 同目录 `.png` ✓ | **保留** |
| `VisceraSpitter` | `.../Profanes/VisceraSpitter.cs` | 远程·血铳(1100) | 默认 → 同目录 `.png` ✓ | **保留** |
| `TwitchingTendonBow` | `.../Profanes/TwitchingTendonBow.cs` | 远程·脊椎弓(1200) | 默认 → 同目录 `.png` ✓ | **保留** |
| `AberrantEyeStaff` | `.../Profanes/AberrantEyeStaff.cs` | 魔法·持续法杖(1250) | 默认 → 同目录 `.png` ✓ | **保留** |
| `GazingFleshGrimoire` | `.../Profanes/GazingFleshGrimoire.cs` | 魔法·散射书(1300) | 默认 → 同目录 `.png` ✓ | **保留** |
| `BurstingTumorBomb` | `.../Profanes/BurstingTumorBomb.cs` | 远程·投掷(1500) | 默认 → 同目录 `.png` ✓ | **保留** |

### 1.7 Boss 专属 / 散件武器

| 类名 | 文件路径 | 武器类型 | 本体纹理来源判定 | 结论 |
|------|----------|----------|------------------|------|
| `YingouKnife` | `Items/Weapons/Bosses/YingouKnife.cs` | 近战·刀(342) | 无重载 → 默认 `Items/Weapons/Bosses/YingouKnife.png` ✓（弹幕用 InnoVault placeholder，不计本体） | **保留** |
| `HanbaBook` | `Items/Weapons/Bosses/HanbaBook.cs` | 魔法·旱魃书(145) | 无重载 → 默认 `.../Bosses/HanbaBook.png` ✓ | **保留** |
| `HoqingFireSummon` | `Items/Weapons/Bosses/HoqingFireSummon.cs` | 召唤·后羿鬼火(136) | 无重载 → 默认 `.../Bosses/HoqingFireSummon.png` ✓ | **保留** |
| `JiangcenHammerItem` | `Items/Weapons/Bosses/JiangcenHammerItem.cs` | 近战·持握巨锤(680) | `NPCs/Boss/Jiangcens/JiangcenHammer.png` ✓ | **保留** |
| `CoffinNail` | `Items/Weapons/CoffinNail.cs` | 近战·投掷镇尸钉(420) | `Items/Weapons/CoffinNail.png` ✓ | **保留** |
| `SoulBanner` | `Items/Weapons/SoulBanners/SoulBanner.cs` | 召唤·成长万魂幡(52) | `Items/Weapons/SoulBanners/SoulBanner.png` ✓ | **保留** |
| `KyuubiBook` | `NPCs/Boss/KyuubiKitsunes/Items/KyuubiBook.cs` | 魔法·九尾天书(185) | 无重载 → 默认 `NPCs/Boss/KyuubiKitsunes/Items/KyuubiBook.png` ✓ | **保留** |
| `DakkiBook` | `NPCs/Boss/KyuubiKitsunes/Items/DakkiBook.cs` | 魔法·妲己之书(6380) | 无重载 → 默认 `.../Items/DakkiBook.png` ✓ | **保留** |
| `DragonCharm` | `Items/Weapons/Charms.cs`（@253） | 远程·激光符咒(300) | `Textures/Items/Weapons/Charms/DragonCharm.png` ✓ | **保留** |
| `PigCharm` | `Items/Weapons/Charms.cs`（@424） | 魔法·持续激光(168) | `Textures/Items/Weapons/Charms/PigCharm.png` ✓ | **保留** |

> **保留合计：16 + 7 + 7 + 7 + 7 + 7 + 10 = 61 件。**

---

## 二、排除清单（精简）

### 2.1 物品本体复用原版 / 占位纹理（4 件，需补本体 sprite 后才进重做线）

| 类名 | 文件 | 本体 `Texture` | 说明 |
|------|------|----------------|------|
| `RuyiJinguBang` | `Items/Weapons/Sticks/RuyiJinguBang.cs` | `Terraria/Images/Item_` + `SilverBroadsword` | 本体复用原版银阔剑纹理 → 占位 |
| `TrueRuyiStick` | `Items/Weapons/Sticks/TrueRuyiStick.cs` | `Terraria/Images/Item_676` | 本体复用原版物品纹理 → 占位 |
| `SoulHookWhip` | `Items/Weapons/NiuMa/SoulHookWhip.cs` | `Terraria/Images/Item_` + `ThornWhip` | 机制已实装（勾魂索鞭），但本体复用原版荆棘鞭纹理 → 占位 |
| `NetherChainBlade` | `Items/Weapons/NiuMa/NetherChainBlade.cs` | `Terraria/Images/Item_` + `ChainKnife` | 机制已实装（冥链刃），但本体复用原版链刀纹理 → 占位 |

> `SoulHookWhip` / `NetherChainBlade` 在 `PLACEHOLDER_CONTENT_REGISTRY.md §7.1` 登记为牛头马面 COMPLETE/PARTIAL；**逻辑完整、缺本体美术**，补 `.png` 后即可转入重做线。

### 2.2 非武器 / 由他人负责（15 项，不属本任务武器范畴）

| 类名 | 文件 | 原因 |
|------|------|------|
| `BaGuaZhenpan` | `Items/Weapons/SummoningStaffs/BaGuaZhenpan.cs` | 无 `DamageType`/`Item.damage`；为 Buff + 阵图 UI 工具 |
| `ChengYingReins` | `Items/Weapons/SummoningStaffs/ChengYing.cs` | `Item.mountType` → 坐骑，非武器 |
| `ChickenCharm` 等 7 生肖符 | `Items/Weapons/Charms.cs` | 鸡/牛/狗/马/兔/蛇/鼠符均为 `Item.accessory` 饰品，无伤害（仅龙符·猪符是武器） |
| `CloudMountItem` | `Items/Summons/CloudMountItem.cs` | `Item.mountType` → 坐骑 |
| `ShenxianGuanglunItem` | `Items/Summons/ShenxianGuanglunItem.cs` | 召唤光环宠物（Pet），无伤害 |
| `JiaSha` | `Items/Summons/JiaSha.cs` | `consumable` 召唤物，非武器 |
| `KyuubiSummonsHairpin` | `Items/Summons/KyuubiSummonsHairpin.cs` | `consumable` 召唤 Boss 物品 |
| `UnderworldPairSummons` | `Items/Summons/UnderworldPairSummons.cs` | `consumable` 召唤牛头马面 Boss 物品 |
| `YingouSummon` | `Items/YingouSummon.cs` | 召唤赢勾 Boss 物品（`UseItem` 仅 `TrySpawnBoss`），非武器 |

> 7 个生肖饰品逐一为：`ChickenCharm`（无限飞行）、`CowCharm`、`DogCharm`、`HorseCharm`、`RabbitCharm`、`SnakeCharm`、`RatCharm`。它们走 `Textures/Items/Weapons/Charms/*` 贴图但属饰品线，不在武器重做范围。

---

## 三、保留武器重做计划

> **统一基调：** 复用 `00_SHADER_VFX_TOOLKIT.md` §B 已实现原语 —— `BuildRibbonStrip`（B.1 拖尾）、`SoftGlow/LightShot/SlashBurst/Sparkle`（`ACMAsset`）、冲击环（B.8）、屏幕扭曲（B.3 `GenericWarp.fx`）、溶解（B.10 `DissolveBurn.fx`）、法阵地贴（B.5 `ArenaRunic.fx`）。预警严守 §C.1 颜色语言（致命=纯红 `#FF2838`），震动走 §C.2 预算，性能守 §C.4。
> **优先把已存在但未用的 `Effects/` 资产接上** —— 目前 `BeamGrad / RadialBloom / DissolveBurn / GenericWarp / PaletteLUT / ReflectWard / ArenaRunic` 已有 `.fx`+`.fxc`，前期武器尚未消费，是「不造轮子」的最高 ROI。

---

### 3.1 前期手工剑线（7 件）

通用现状：均为标准 `Swing/Rapier` 近战，伤害与 buff 各异；多数靠原版 `Dust` + 一个简单弹幕（`BlankProjectile`/`*SwordProj`）表现，几乎无着色器。

- **`BronzeSword`（8 / 中毒 + 1% 秒杀）** — 现状：挥砍施毒，`ModifyHitNPC` 1/100 斩杀。重做：把 1% 斩杀做成**演出反馈**——命中触发瞬间用 `SoftGlow` 加法大闪 + 一道 `SwordTrail55` 青铜色斩痕（B.1），斩杀时叠 `RadialBloom.fx` 一次性绿芒脉冲，强化「青铜断金」手感。VFX：B.1 拖尾 + B.4 轻量闪。
- **`CrimsonbronzeSword`（59 / 左键挥砍点燃，右键发射 `CrimsonbronzeSwordProj1`）** — 现状：右键已发弹但无特效。重做：右键弹幕走 `BeamGrad.fx` 渐变火刃（猩红→橙），命中爆 `DissolveBurn` 灼烧边小爆；左键挥砍加火色 ribbon 拖尾。VFX：B.9 光束渐变 + B.10 灼烧。
- **`GanJiangSword`（84 / 干将——左右键双弹 + `attackType` 二段 + `Counter` 三拍连击）** — 现状：机制最丰富（左键交替斩、右键蓄连击 `GanJiangSwordProj_2`），但弹幕表现单薄。重做：做**干将莫邪双剑**对偶——左剑暖（赤）右剑冷（青），连击第 3 拍合击时双色 ribbon 交缠（B.1 双层）并触发一次 `GenericWarp.fx` 径向折射定格（B.3，强度尖脉冲）。VFX：B.1 双色拖尾 + B.3 合击折射 + B.8 命中冲击环。
- **`YuChangSword`（35 / 鱼肠——左键刺击每 4 连击射 `YuChangSwordBean`，右键 300% 穿透技能 20s CD，1% 钓获）** — 现状：右键已是「定身刺杀」原型。重做：右键蓄力时屏幕轻微 `GenericWarp` 收束，释放瞬间一道 `BeamGrad` 突刺光 + `PaletteLUT.fx` 短暂冷调染屏（刺客「时停」感）；CD 就绪用刀身 `SoftGlow` 呼吸提示。VFX：B.3 收束 + B.9 突刺 + B.6/PaletteLUT 染屏。
- **`BlackBearSword`（47 / 超慢重剑 useTime 150，发 `BlackBearSwordProj1`）** — 现状：蓄力重斩定位，但缺「重」反馈。重做：落刃接地用 B.8 双层冲击环 + §C.2「落地/普通爆炸」级震动（4–6px），刀身拖一条粗 `SwordTrail553` 残影。VFX：B.1 重拖尾 + B.8 冲击环 + 震动。
- **`XuanTieSword`（13 / 玄铁——命中叠 `XuanTieBleed`）** — 现状：纯流血过渡剑。重做：与玄铁套装流血叠层联动，命中按叠层数渐强刀身暗红 `SoftGlow`，满层一次 `RadialBloom` 暗血脉冲。VFX：B.4 轻量闪（按叠层）。
- **`BoneSword`（3 / 极速 useTime 6）** — 现状：白板速刷剑。重做：极速挥砍残留骨白 `SwordTrail551` 余像点状拖尾（密度跟 use 频率），保持极低成本。VFX：B.1 ⚡ 拖尾。

### 3.2 长矛（如意棒）线（5 件）

通用现状：`WoodenStick(8)→IronStick(28)→GoldenStick(48)→GemStick(68)→RuyiStick(120)` 为一条**升级长矛线**，统一机制：左键 `*SpearProjectile` 突刺 + `attackType` 二段 / 右键 `*_2` 变招；`RuyiStick` 额外有第三段 `*SpearProjectile_3`（`RuyiStickSpearProjectile_2.isA` 触发）。已知 `RuyiStickSpearProjectile` 用到 `BuildRibbonStrip`（工具箱 §B.1 范例之一），是全模组拖尾标杆。

- **统一重做方向**：把 5 级长矛做成**同一拖尾语汇、按层级提质**——`WoodenStick` 仅细 ribbon；逐级加宽并叠加色（铁=钢蓝、金=金辉、宝石=多彩 `PaletteLUT` 流光、如意=纯红 `#FF2838` 致命预警拖尾，呼应工具箱已点名「如意棒拖尾」）。
- **`RuyiStick` 顶配**：三段连击的第三段（`_3`）做成「定海神针」放大突刺——`GenericWarp.fx` 沿矛轴向折射 + B.8 命中环 + §C.2 相变级震动；蓄第三段时矛尖 `RadialBloom` 充能预警。
- VFX：B.1（全线统一双层 ribbon）+ B.3（如意顶段折射）+ `PaletteLUT.fx`（宝石矛流光）。`TrailQuality` 配置降级到点状 dust。

### 3.3 召唤 / 法杖散件（4 件）

- **`BlackBearStaff`（25 召唤 / 单只黑熊 `BlackBearStaffProj1`）** — 现状：基础召唤，限 1。重做：召唤 / 攻击瞬间用 `DissolveBurn.fx` 让黑熊「材质化显形」（B.10，喂 minion 贴图而非 screenTarget），攻击落点 B.8 小冲击环。VFX：B.10 显形 + B.8。
- **`MingCrowStaff`（12 召唤 / 冥鸦 `MingCrowMinion`，鼠标点位生成）** — 现状：已有 `MingCrowMinion_Attack/_Fly` 双帧贴图。重做：冥鸦俯冲攻击拉一条幽蓝（§C.1 地府色）`BuildRibbonStrip` 残影，生成点 `Smoke`+`SoftGlow` 鬼焰团。VFX：B.1 幽蓝拖尾 + B.7 粒子。
- **`Pufferfish`（1111 魔法 / `PufferfishProj1`，玩笑高伤）** — 现状：高伤趣味法杖，已有 3 帧弹幕贴图。重做：保留趣味但加「河豚膨胀」节奏——蓄爆时弹体 scale 脉冲 + `RadialBloom` 一次性闪，命中 B.8 水纹环。VFX：B.8 + B.4。
- **`HoqingFireSummon`（136 召唤 / 后羿鬼火，左键一次生成 6 只 `HoqingFireSummonProj`，右键全收）** — 现状：用 `NPCs/Boss/Hoqings/GhostFire` 贴图，6 火环绕。重做：鬼火轨迹用幽蓝-赤(主题双色) `BuildRibbonStrip` 拖尾，右键回收用 `DissolveBurn` 反向溶解收束。VFX：B.1 + B.10。

### 3.4 林地基础线（7 件）

通用现状：完整的「战法牧射 + 投掷」前期森林套，代码注释已规划 `SoftGlow/LightShot` 叠加渲染，但实际多停留在原版 `Dust`（`Grass`/`Smoke`）。主题统一为自然·翠绿。

- **`WoodlandGreatsword`（16 / 慢挥大剑，1/4 中毒，挥砍撒草尘）** — 重做：挥砍叠一条草绿 `SwordTrail55` 拖尾（B.1），命中绽放 `Sparkle` 叶屑。
- **`DeadwoodMusket`（14 / 橡子弹 `DeadwoodAcornProj`，枪口烟雾）** — 重做：枪口 `SoftGlow` 闪 + 橡子拖 `LightShot` 暖芯；橡子碎裂用小 B.8 环。
- **`EmeraldTwigStaff`（16 / `EmeraldTwigBolt`，注释已写 LightShot+SoftGlow 叠加）** — 重做：直接落实注释——`LightShot` 翠芯 + `SoftGlow` 外晕双层弹幕拖尾。
- **`NatureGrimoire`（13 / 扇形 3 叶 `NatureGrimoireLeaf`）** — 重做：叶片飘落 `Sparkle` 残点，命中 1/N 中毒时叶面 `SoftGlow` 绿闪。
- **`RootBoomerang`（13 / 树根回旋镖，穿 2 限 1）** — 重做：旋转飞行拉 `BuildRibbonStrip` 木纹弧尾，回程提速时拖尾变亮。
- **`MossBomb`（20 / 上抛苔藓弹，绿蘑菇云）** — 重做：爆炸用 `SlashBurst`+`SoftGlow`+`Sparkle` 多层（与神木弹同语汇）+ B.8 绿环。
- **`VineHunterBow`（11 / 箭+1/4 中毒，`GlobalProjectile` 实现）** — 重做：发射 `Grass` dust → 升级为藤蔓 `BuildRibbonStrip` 短尾随箭。
- VFX 共用：B.1 + `ACMAsset`（SoftGlow/LightShot/Sparkle/SlashBurst）+ B.8。

### 3.5 林地升级线（7 件） — **本组重做收益最高**

通用现状（实测 `CupriteWoodlandGreatsword`）：升级版**几乎只是数值 + buff 时长上调**（赤铜=42 伤 / 1-3 中毒，玄铁系同理），**完全没有独立 VFX**，与基础版视觉无差异。`PLACEHOLDER_CONTENT_REGISTRY.md §7.2` 标为 PARTIAL/P3。

- **统一现代化方向**：让升级有「看得见的质变」——
  - **赤铜系**（`CupriteWoodlandGreatsword/DeadwoodMusket/EmeraldTwigStaff/NatureGrimoire/MossBomb`）：在林地基础 VFX 上叠**赤铜灼烧**主题——`DissolveBurn.fx` 灼烧边 + 橙红 `PaletteLUT` 染色，中毒改为「毒+灼」双 DoT 的视觉混合（绿尘 + 橙焰）。
  - **玄铁系**（`XuanTieHunterBow/XuanTieRootBoomerang`）：叠**玄铁流血**主题——暗红 `SoftGlow` + 命中 `XuanTieBleed` 联动（与玄铁套装叠层呼应），回旋镖/箭拉暗钢色 ribbon。
- VFX：B.10 灼烧（赤铜）+ `PaletteLUT.fx` 主题染色 + B.1 主题色拖尾。**这是把已存在却闲置的 `DissolveBurn.fx`/`PaletteLUT.fx` 接上的最佳切入点。**

### 3.6 神木线（7 件）

通用现状：**已是较现代的持握 / 多段弹幕实现**——大刀走 Held Projectile（参照 `AzureRuinBlade`），含弧光拖尾、刀波、链鞭分节、三连铳第 9 发种子迫击、长弓螺旋、典籍 8–12 叶追踪、炸弹 `SlashBurst+SoftGlow+Sparkle` 绽放。已大量用 `ACMAsset`。重做主要是**统一拖尾语汇 + 补 shader 级演出**。

- **`DivineWoodGreatblade`（190 / 持握挥砍 40% 进度发自然刀波 + 荆棘缠绕）** — 重做：弧光拖尾统一为「外暗内亮」双层 ribbon（工具箱 §B.1 二次迭代建议）；刀波改用 `BeamGrad.fx` 渐变绿刃，命中沿途用 `DissolveBurn` 生长荆棘的灼烧式显形。
- **`DivineWoodGyratingLeaf`（175 / 回旋镖回程×1.5，每 5 命中花瓣裁决）** — 重做：花瓣裁决环用 B.8 + `Sparkle`；回程加速段 ribbon 提亮提速。
- **`DivineWoodScepter`（155 / 藤蔓链鞭分节，末端叶爆）** — 重做：分节链身用 `BuildRibbonStrip` 连续藤带替代逐贴片；末端叶爆 `SlashBurst`。
- **`DivineWoodMusket`（140 / 三连，第 9 发种子迫击落地荆棘领域）** — 重做：荆棘领域地贴用 `ArenaRunic.fx`（B.5 复用，换绿藤纹）做范围 DoT 可视化。
- **`DivineWoodLongbow`（155 / 主箭 + 双螺旋叶刃）** — 重做：螺旋叶刃 DNA 轨迹拉细 ribbon，蓄力满弓 `RadialBloom` 提示。
- **`DivineWoodTome`（165 / 8–12 叶扇形螺旋追踪）** — 重做：叶群追踪期统一翠绿 `SoftGlow` 描边，命中次生花瓣 `Sparkle`。
- **`DivineWoodBomb`（200 / 种子手雷，绽放 8 追踪藤蔓碎片）** — 重做：绽放叠 B.8 绿环 + `DissolveBurn` 藤蔓生长。
- VFX 共用：B.1 双层 ribbon（统一）+ `BeamGrad/ArenaRunic/DissolveBurn` 接线 + B.8。

### 3.7 傲世神木线（7 件） — 终极形态，演出应「拉满」

通用现状：神木线的**升华终极版**（神木武器 + 大椿材料 `ArrogantDivineSylvan` 合成），伤害 300–1800，机制更暴力（三连斩地裂、风暴回旋镖内爆坍缩、万藤杖藤蔓新星 16 爆 + 分支触手、五连弩连锁 + 万棘狂涌、穿林弓世界树之矢、山海典叶暴漩涡、世界种分裂 5 子种 + 16 藤蛇）。金翠双色主题。已有相当完成度。

- **统一现代化方向**：作为前期/杂项线**视觉天花板**，全线接 shader 级 set-piece——
  - **金翠双色 ribbon**（B.1 双层，外金内翠）统一所有挥砍 / 弹幕拖尾。
  - **`ArrogantDivineSylvanGreatblade` 下劈地裂** → `ArenaRunic.fx` 根须法阵地贴（B.5）+ B.8 双环 + §C.2 相变级震动（8–12px）。
  - **`ArrogantDivineSylvanChakram` 内爆坍缩** → `GenericWarp.fx` 径向吸入折射（B.3 / 工具箱 D 表 `VoidCollapse` 思路的绿色变体），回收接住时 B.8 + 震动（已有注释要求屏震）。
  - **`ArrogantDivineSylvanStaff` 藤蔓新星 16 爆** → 环形 `SlashBurst` + B.8；分支触手用 ribbon。
  - **`ArrogantDivineSylvanBomb` 世界种绽放** → `RadialBloom.fx` 大型一次性绿金爆 + `DissolveBurn` 荆棘领域。
  - **Musket / Bow / Tome** → 主弹 `BeamGrad` 金翠渐变芯，触发技（万棘狂涌 / 世界树之矢 / 叶暴漩涡）各配一次 `PaletteLUT` 短暂染屏定调。
- VFX 共用：B.1（金翠双层）+ B.3 + B.5 + B.8 + `RadialBloom/BeamGrad/DissolveBurn/PaletteLUT` 全套接线。受 §C.4#2 限制：同屏全屏后处理 ≤ 1，按强度仲裁。

### 3.8 亵渎线（7 件） — 血肉 / 猩红主题

通用现状：结构对位神木线（大剑 / 回旋镖 / 铳 / 弓 / 持续法杖 / 散射书 / 炸弹），伤害 1100–1500，已有血肉拖尾、`LightShot` 暗红弹、`SoftGlow` 血雾、爆裂多层 VFX。主题暗红血肉。

- **`ProfaneDismemberer`（1400 / 持握三连斩，下劈血肉震荡）** — 重做：弧光 ribbon 改暗红双层，下劈震荡用 B.8 血环 + `GenericWarp` 血色热浪折射。
- **`GluttonousFleshrang`（1350 / 吸血回旋镖回程×1.8，每 4 命中 8 血肉触手）** — 重做：吸血命中飘血珠 `Sparkle`（暗红），触手用 ribbon；回程提速拖尾转猩红致命色。
- **`VisceraSpitter`（1100 / 五连血弹，第 5 轮巨型脏器弹爆）** — 重做：枪口血雾 `Smoke`+`SoftGlow`，脏器弹爆裂 `SlashBurst` + B.8 血环。
- **`TwitchingTendonBow`（1200 / 脊椎箭，每 3 箭巨眼弹 6 血刺 + 两侧筋腱飞刃）** — 重做：脊椎箭拖暗红 ribbon + 沿途血滴 dust；巨眼弹爆用 `RadialBloom` 暗红脉冲。
- **`AberrantEyeStaff`（1250 / channel 持续触手，每 6 次畸变眼球）** — 重做：channel 期玩家与触手间用 `BeamFlow`/`BeamGrad` 血色连线（B.9）；眼球弹 `GenericWarp` 视觉扭曲（畸变主题）。
- **`GazingFleshGrimoire`（1300 / 扇形 10 血弹，每 4 次巨眼追踪）** — 重做：散射弹统一 `LightShot` 暗红芯，巨眼弹拖 `SoftGlow` 凝视光。
- **`BurstingTumorBomb`（1500 / 投掷弹跳一次再爆，8 追踪血肉碎片）** — 重做：爆炸 `SlashBurst`+`SoftGlow`+B.8 大血环 + `DissolveBurn` 血肉消融边。
- VFX 共用：B.1（暗红双层）+ B.3 血热浪 + B.9 血色连线 + B.8。预警遵 §C.1：致命落点用纯红，区分血肉主题色（暗红/猩红）。

### 3.9 Boss 专属 / 散件武器（6 件）

- **`YingouKnife`（342 / 赢勾——朝鼠标射 `SaberHellFriendly`，40 帧后两端召 `SaberKiller` 巨型斩击）** — 现状：弹幕用 InnoVault placeholder + 手绘巨型矩形斩光（Azure→Red 渐变）。重做：把 placeholder 斩光替换为 `BeamGrad.fx` 渐变长刃 + `SlashBurst` 收口；命中 B.8 + §C.2 中度震动。本体 sprite 已有，仅弹幕表现升级。VFX：B.9 + B.8。
- **`HanbaBook`（145 / 旱魃书，`HanbaBookProj` 用 Shockwave 贴图）** — 重做：旱魃「焦土」主题——发射 `GenericWarp.fx` 热浪扭曲（B.3）+ 橙红 `PaletteLUT` 局部染色，命中 B.8 焦土环。VFX：B.3 + B.8。
- **`JiangcenHammerItem`（680 / 持握巨锤 `JiangcenHammerProj`）** — 重做：巨锤落点是天然 set-piece——B.8 双层冲击环 + `ArenaRunic` 砸地裂纹地贴 + §C.2 相变级震动 + 锤头 `SoftGlow` 蓄力。VFX：B.5 + B.8 + 震动。
- **`CoffinNail`（420 / 投掷镇尸钉，已有华丽红轨迹 + 自定义 tooltip）** — 现状：注释强调「血色轨迹 + 冥府之力」。重做：轨迹改 `BuildRibbonStrip` 血红 ribbon（落实注释），命中爆 `DissolveBurn` 冥府裂痕 + 对僵尸类增伤的特异闪光。VFX：B.1 + B.10。
- **`SoulBanner`（52 召唤 / 成长系万魂幡：吸魂上限随击杀 Boss 提升，动态改伤害/击退/回复，左键 `SoulBannerHeldProj` 右键 `SoulBannerMinion`）** — 现状：机制最独特（`SoulBannerPlayer` 成长 + UI + 动态 tooltip），但视觉未体现「灵魂越多越盛」。重做：把成长比例驱动到视觉——幡面 `SoftGlow` 紫芒随 `GrowthRatio` 增亮、吸魂时灵魂沿 `BuildRibbonStrip` 幽紫轨迹汇入幡，满级触发 `RadialBloom` 万魂脉冲。VFX：B.1 幽紫吸魂 + B.4/RadialBloom 成长反馈。
- 共用：B.8 冲击环 + `ArenaRunic`（锤/钉地裂）+ `BeamGrad`（赢勾斩）。

### 3.10 九尾线魔法书（2 件）

- **`KyuubiBook`（185 / 九尾天书——`KyuubiBookTailController` 召 9 条狐尾从背后刺敌）** — 现状：石巨人后强度，9 尾控制器机制完整。重做：狐尾用「外暗内亮」双层 ribbon（金/橙狐火色）+ 尾尖 `SoftGlow`；同时刺击瞬间 B.8 小环聚焦。VFX：B.1 + B.4。
- **`DakkiBook`（6380 / 妲己之书——九尾天书 + 幽冥狐典合体，魅惑之环锁定 → 收缩刺击 → 火焰魂魄毁灭波）** — 现状：上位终极书，三段式演出框架已在。重做：魅惑之环用 `ArenaRunic.fx` 锁定法阵地贴（B.5，狐魅紫红纹）+ 收缩期 `GenericWarp` 向心折射 + 毁灭波 `RadialBloom` 大爆 + `PaletteLUT` 染屏定格（§C.3 大招节拍）。作为本任务最高伤武器，演出对标傲世神木终极。VFX：B.5 + B.3 + RadialBloom + PaletteLUT + B.8。

### 3.11 生肖武器符（2 件）

- **`DragonCharm`（300 远程 / 龙符——`HoldUp` 发 `DragonCharmLaser`，每次自扣 30 HP，已有 `DragonCharmExplosion/Laser` 贴图）** — 现状：献祭血量换激光的高风险输出。重做：自扣血做成「龙血献祭」视觉——施放时玩家身侧 `SoftGlow` 血光，激光走 `BeamGrad.fx` 金龙渐变 + 命中 `RadialBloom` 龙纹爆。VFX：B.9 + RadialBloom + B.4。
- **`PigCharm`（168 魔法 / 猪符——`channel` 长按持续 `PigCharmLaser`，已有 firing/continuous/ending 三段激光贴图）** — 现状：持续光束已分三段帧。重做：持续光束接 `BeamGrad.fx` 流动渐变 + 端点 `SoftGlow` 收口（B.9 标准光束语汇）+ 持续命中处 `GenericWarp` 轻微热浪。VFX：B.9 + B.3。

---

## 四、优先级提示（实现顺序建议）

| 级别 | 范围 | 理由 |
|------|------|------|
| **P1** | §3.5 林地升级线（7 件） | 当前**纯数值升级、零独立 VFX**，质变收益最大；正好接闲置的 `DissolveBurn.fx`/`PaletteLUT.fx` |
| **P1** | 排除清单 §2.1 的 `SoulHookWhip`/`NetherChainBlade` 补本体 sprite | 逻辑已完整，仅缺美术即可转正 |
| **P2** | §3.7 傲世神木线 + §3.10 `DakkiBook` | 终极武器，演出天花板，集中接全套 shader set-piece |
| **P2** | §3.1–3.2 剑 / 长矛线统一双层 ribbon 语汇 | 量大、复用 §B.1，奠定全前期手感基线 |
| **P3** | §3.4/3.6/3.8 落实已有注释里的 `SoftGlow/LightShot/SlashBurst` 叠加 + 接 `ArenaRunic/BeamGrad` | 提质但非阻塞 |

---

*Primordial / 洪荒 · Weapon Redo · 03 前期/杂项 · 基于仓库实测代码与纹理实存，未臆造路径。*
