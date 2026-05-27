# 占位内容注册表（Placeholder Content Registry）

> **生成日期：** 2026-05-28  
> **模组：** Ancient Chinese Mythology（洪荒 / Primordial）  
> **用途：** Phase 1/2 敷衍实现物品的**定位、理想机制、实装优先级**唯一索引  
> **交叉引用：** `docs/PROGRESSION_DESIGN_SPEC.md` · `docs/TEXTURE_COMPLETION_PLAN.md` · `docs/PLAYABILITY_AUDIT_REPORT.md`

---

## 一、摘要统计

| 类别 | STUB | PARTIAL | COMPLETE | 合计 |
|------|------|---------|----------|------|
| **四海龙王武器** | 15 | 0 | 0 | 15 |
| **天将武器/饰品** | 9 | 0 | 0 | 9 |
| **四圣兽武器** | 12 | 0 | 0 | 12 |
| **观察者** | 0 | 0 | 0* | 0* |
| **地府 Phase 2** | 8 材料 + 0 武器† | 1† | — | 9 |
| **前期占位** | 2 | 8 | 1‡ | 11 |
| **材料/召唤物** | 10 | 3 | 1 | 14 |
| **合计（本表登记）** | **56** | **12** | **2** | **70** |

\* 观察者 **8 件现有武器**（天眼杖等）已实装，不在本占位表；仅 `OverseersEye` 材料登记。  
† `WraithLantern` 为 PARTIAL（原版 `LostSoul` 弹幕，无主题机制）。  
‡ `XuanTie` 三件套套装奖励已实装（`XuanTieSetBonusPlayer`）。

### 按 SB Tier 的 STUB 武器分布

| SB Tier | Act | Boss 来源 | STUB 武器数 |
|---------|-----|-----------|-------------|
| T9 (1350) | Act 2 | 牛头马面 | 1（冥链刃；勾魂索为 PARTIAL） |
| T32 (13750) | Act 4a | 敖钦 | 5 |
| T33 (14600) | Act 4a | 敖闰 | 5 |
| T34 (15450) | Act 4a | 敖顺 | 5 |
| T29 (11200) | Act 4a | 神威 | 3 |
| T30 (12050) | Act 4a | 百目 | 3 |
| T35 (16300) | Act 4a | 毗沙门 | 3 + 1 饰品 |
| T38–41 | Act 4a | 四圣兽 | 12（各 3） |

**P1 VANILLA_REF 对照：** 本表 37 件天庭线占位武器 + 10 件杂项/材料/前期 = **47 项**，与 `TEXTURE_COMPLETION_PLAN.md` §3.2 一致。

---

## 二、四海龙王（§PROGRESSION_DESIGN_SPEC §5.2）

**共享材料：** `DragonKingScale` · 100% · 8–12（专家 +25%）  
**掉落率：** 各龙王专属武器 **5% × 5 件**（专家/大师倍率见 Boss `ModifyNPCLoot`）  
**伤害带：** 敖钦 340–390 · 敖闰 355–400 · 敖顺 370–420

### 2.1 敖钦（火 / 南海）— SB T32

| 类名 | 伤害 | 定位 | 作用 | 理想效果 | 状态 | 优先级 |
|------|------|------|------|----------|------|--------|
| `InfernoDragonSpear` | 350 近战 | Boss 掉落 | 火系近战主力 | 焰纹龙枪突刺 + 焚风 trail + 点燃叠层 | STUB | P1 |
| `FlamecoilChakram` | 355 近战 | Boss 掉落 | 中距回旋输出 | 双环交缠飞出、回程二次命中、火 coil 残焰 | STUB | P1 |
| `CrimsonMaelstromBow` | 360 远程 | Boss 掉落 | 风暴弓物理 | 赤旋箭袋、命中生成小型火龙卷 | STUB | P1 |
| `DraconicEmber` | 340 召唤 | Boss 掉落 | 火系召唤入门 | 余烬幼龙跟随、周期吐熔岩蛋 AoE | STUB | P1 |
| `MeteorCallerStaff` | 365 魔法 | Boss 掉落 | 火系法师 | 唤流星雨（非单次 Meteor1），龙纹法阵前摇 | STUB | P1 |

**文件：** `Celestias/Boss/Aokins/Items/AokinWeapons.cs`

### 2.2 敖闰（冰 / 西海）— SB T33

| 类名 | 伤害 | 定位 | 作用 | 理想效果 | 状态 | 优先级 |
|------|------|------|------|----------|------|--------|
| `GlacialDragonblade` | 355 近战 | Boss 掉落 | 冰系快刀 | 冰龙镰刃弧斩 + 霜冻减速 | STUB | P1 |
| `PermafrostTrident` | 360 近战 | Boss 掉落 | 矛系控场 | 三叉戟投掷回收、永冻戳刺、水冰双属性 | STUB | P1 |
| `VortexPrimordialStain` | 365 魔法 | Boss 掉落 | 水系法师 | 三枚水墨漩涡 orb 蛇形追踪 | STUB | P1 |
| `InkscaledFlowFan` | 370 魔法 | Boss 掉落 | 扇形召唤/魔 | 墨鳞游鱼扇形涌出、潮涌 DoT | STUB | P1 |
| `BlizzardPiercer` | 375 远程 | Boss 掉落 | 冰弓穿透 | 暴雪穿云箭、 frost 穿透计数 | STUB | P1 |

**文件：** `Celestias/Boss/Aoyuans/Items/AoyuanWeapons.cs`

### 2.3 敖顺（雷 / 北海）— SB T34

| 类名 | 伤害 | 定位 | 作用 | 理想效果 | 状态 | 优先级 |
|------|------|------|------|----------|------|--------|
| `ThunderlordHalberd` | 370 近战 | Boss 掉落 | 雷系重戳 | 方天戟雷击戳刺、链式 arc 弹跳 | STUB | P1 |
| `StormchainWhip` | 375 鞭/召近 | Boss 掉落 | 雷链控场 | 闪电链鞭、链接多目标感电 | STUB | P1 |
| `TempestRepeater` | 380 远程 | Boss 掉落 | 风暴连弩 | 三连弩箭 + 雷暴标记引爆 | STUB | P1 |
| `LightningEdictTome` | 385 魔法 | Boss 掉落 | 雷系法师 | 雷敕天书、落雷符箓阵列 | STUB | P1 |
| `AzureRuinBlade` | 420 近战 | Boss 掉落 | 雷水终局刀 | 苍海毁刃、挥砍释放潮汐雷浪（§5.2 表） | STUB | P1 |

**文件：** `Celestias/Boss/Aoshuns/Items/AoshunWeapons.cs`  
**注：** 美术计划中的 `StormcleaverGreatsword` 已由 `AzureRuinBlade` 取代（代码与 §5.2 一致）。

---

## 三、天将线（§5.3）

**共享材料：** `GeneralOrder`（将军令）100%；神威/百目 1–3，毗沙门 1–3  
**召唤门控：** 神威 ← 入侵 ≥15 波 · 百目 ← `downedVigor` · 毗沙门 ← 神威令 + 百目弦 + 3 将军令

### 3.1 神威 Vigor — SB T29

| 类名 | 伤害 | 定位 | 作用 | 理想效果 | 状态 | 优先级 |
|------|------|------|------|----------|------|--------|
| `SinSeveringBlade` | 1180 近战 | Boss 三选一 | 断罪近战 apex | 挥砍断罪标记、对「罪孽」Debuff 目标暴击 | STUB | P1 |
| `AureateVoidrender` | 1120 近战 | Boss 三选一 | 高速清图 | 金紫虚空斩浪、穿透多体 | STUB | P1 |
| `VerdictSealHammer` | 1250 近战 | Boss 三选一 | 重锤爆发 | 裁决印锤、命中全场震波 + 封印缓速 | STUB | P1 |

**文件：** `Celestias/Boss/Vigors/Items/VigorWeapons.cs`

### 3.2 百目 Argus — SB T30

| 类名 | 伤害 | 定位 | 作用 | 理想效果 | 状态 | 优先级 |
|------|------|------|------|----------|------|--------|
| `SoulPiercingArc` | 1150 远程 | Boss 三选一 | 穿魂弓 | 瞳纹追踪箭、弱点标记叠层 | STUB | P1 |
| `LuminanceStellarCannon` | 1200 远程 | Boss 三选一 | 星炮 | 聚星一炮、命中恒星爆发 | STUB | P1 |
| `LuminousIrisAnnihilator` | 1180 远程 | Boss 三选一 | 手铳爆发 | 虹膜充能、金色光弹连射 | STUB | P1 |

**文件：** `Celestias/Boss/Arguses/Items/ArgusWeapons.cs`

### 3.3 毗沙门 Vaisravana — SB T35

| 类名 | 伤害 | 定位 | 作用 | 理想效果 | 状态 | 优先级 |
|------|------|------|------|----------|------|--------|
| `TreasurePagodaStaff` | 1320 魔法 | Boss 三选一 | 宝塔法师 | 层叠宝塔弹幕、拾取金币增伤（财神主题） | STUB | P1 |
| `VaultshadeVoidshot` | 1280 远程 | Boss 三选一 | 库藏狙 | 虚空弹生长、命中虚空坍缩吸怪 | STUB | P1 |
| `CelestialCircletScepter` | 1300 魔法 | Boss 三选一 | 天冠权杖 | 五枚耀能环螺旋收束冲刺 | STUB | P1 |
| `TreasurePagodaCharm` | — 饰品 | 25% 掉落 | 防御向 | 宝塔护体：减伤 + 受击反震宝塔虚影 | STUB | P2 |

**文件：** `Celestias/Boss/Vaisravanas/Items/VaisravanaItems.cs`

---

## 四、四圣兽（§5.4）

**灵材：** 各圣兽 100% · 6–10（`QingLongSpirit` 等）  
**武器：** 各 Boss **三选一 100%** · 伤害 1450–1600  
**合成桥：** 8 灵 + 15 天极锭 + 12 碎片 → 1650–1750 升华（仅 `AzureTorrentBlades` 已有配方示例）

| 圣兽 | 类名 | 伤害 | 定位 | 理想效果 | 状态 | P |
|------|------|------|------|----------|------|---|
| 青龙 | `AzureTorrentBlades` | 1480 | 双刀速攻 | 青水流光双短刃、鞘中飞出旋转 | STUB | P1 |
| 青龙 | `WindserpentDao` | 1520 | 长刀 | 风蛇刀气、横扫龙卷 | STUB | P1 |
| 青龙 | `ThunderclapLongbow` | 1550 | 雷弓 | 雷鼓蓄力、穿透 thunder 箭 | STUB | P1 |
| 白虎 | `AurelianCataclysmSmasher` | 1580 | 重锤 | 金纹灾 hammer、裂地冲击波 | STUB | P1 |
| 白虎 | `ArgentPulseObliterator` | 1450 | 速射枪 | 银脉冲、每 8 发三重 burst | STUB | P1 |
| 白虎 | `WhiteTigerClaws` | 1500 | 拳套 | 虎爪连击、撕裂流血 | STUB | P1 |
| 朱雀 | `StarfireAnnihilator` | 1520 | 星火枪 | 珊瑚星火弹、穿透爆炸 | STUB | P1 |
| 朱雀 | `SolarisEternalVerdict` | 1600 | 召唤 | 日轮眼悬浮、阳光射线穿透 | STUB | P1 |
| 朱雀 | `PhoenixFlameStaff` | 1480 | 火焰法 | 凤凰焰杖、涅槃 rebirth 弹幕 | STUB | P1 |
| 玄武 | `GeocrystalShatterblade` | 1450 | 地晶剑 | 命中 lava 晶 burst | STUB | P1 |
| 玄武 | `GeoarchonRupturer` | 1500 | 地系法 | 七柱地能裂穴 | STUB | P1 |
| 玄武 | `BlackTortoiseShield` | 1550 | 盾攻 | 玄龟盾格挡反伤、龟甲纹减伤 | STUB | P1 |

**文件：** `Celestias/Boss/FourSacredBeasts/Items/FourSacredBeastWeapons.cs`

---

## 五、观察者

| 类名 | 类型 | 定位 | 作用 | 理想效果 | 状态 | P |
|------|------|------|------|----------|------|---|
| `OverseersEye` | 材料 | 观察者 100% · 8–12 | 入侵终局材料 | 合成 8 选 1 观察者武器备份配方 | STUB 材料 | P2 |

**说明：** 观察者 **战斗武器**（天眼杖、全视玉简等）已在 `Celestias/Boss/CelestialOverseers/` 实装，不在占位表。

---

## 六、地府 Phase 2（§6）

| 类名 | 来源 | 定位 | 作用 | 理想效果 | 状态 | P |
|------|------|------|------|----------|------|---|
| `SpectreGrudgeCore` | Spectre 100% · 4–7 | 合成材料 | 妲己/枉骸链 | 怨灵核 → EX 武器升阶 | STUB | P2 |
| `WraithLantern` | Spectre ~14% | Boss 掉落武器 | 早期地府魔武 | 双鬼火灯笼、怨灵 tether DoT | PARTIAL | P2 |
| `ImpermanenceSoul` | 黑白无常各 100% · 2–4 | 合成材料 | EX 升级 | 无常之魂 → 冥府链刃 720 等 | STUB | P2 |
| `NetherDragonScale` | 幽冥龙 100% · 8–12 | 合成材料 | 幽冥矿链 | 同龙王鳞地位 | STUB | P2 |
| `AwakenedNetherCore` | 觉醒冥龙 100% | 召唤材料 | 阴天子门控 | 酆帝诏书主材 | STUB | P2 |
| `VoidDragonSinew` | 觉醒冥龙 100% · 3–5 | 合成材料 | Fengdu 强化 | +8% 伤强化材 | STUB | P2 |
| `YinEssence` | 阴天子 100% · 18–24 | 终局材料 | 准圣门控 | 阴元精华 → 酆都甲 | STUB | P2 |
| `YinImperialSeal` | 阴天子 100% | 终局饰品/材料 | 修仙 G7 | 酆帝印玺 | STUB | P2 |
| `FengduImperialCrown` | 阴天子 33% | 饰品占位 | 终局防御 | 酆帝冠 | STUB | P3 |
| `GhostGateKey` | 阴天子 33% | 功能物品 | 区域门控 | 鬼门关钥匙 | STUB | P3 |
| `SoulBannerUnderworldRelic` | 阴天子 33% | 万魂幡 relic | 终局收集 | 地府万魂 relic | STUB | P3 |
| `YinEmperorEdict` | 合成召唤 | 召唤物 | 阴天子 | 8 龙筋 + 12 尸块 + 1 龙心 | STUB | P2 |

**交叉引用：** §6.2 掉落表 · §6.3 无常/觉醒链 · `Underworlds/Items/Materials/`

---

## 七、前期占位

### 7.1 牛头马面 — SB T9（§4.2 Act 2）

| 类名 | 伤害 | 定位 | 作用 | 理想效果 | 状态 | P |
|------|------|------|------|----------|------|---|
| `NetherChainBlade` | 58 近战 | Boss 掉落 | HM 前期近战 | 冥链刃飞出回收、勾连双目标 | STUB | P2 |
| `SoulHookWhip` | 52 鞭 | Boss 掉落 | HM 前期鞭 | 勾魂索拉怪 + 灵魂 DoT | PARTIAL | P2 |
| `NiuTouSeal` | 材料 | 100% 掉落 | 召唤/合成 | 牛头印 · 冥途双引 | STUB | P2 |
| `MaMianSeal` | 材料 | 100% 掉落 | 召唤/合成 | 马面印 | STUB | P2 |

**文件：** `Items/Weapons/NiuMa/` · `Items/Materials/NiuTouSeal.cs` · `MaMianSeal.cs`

### 7.2 林地 → 赤铜/玄铁升级 — WoF 后（§4.2）

| 类名 | 伤害 | 定位 | 作用 | 理想效果 | 状态 | P |
|------|------|------|------|----------|------|---|
| `CupriteWoodlandGreatsword` | 42 | 配方升级 | 近战过渡 | 林地毒刃 + 赤铜灼烧（已有 1/3 中毒） | PARTIAL | P3 |
| `CupriteDeadwoodMusket` | ~40 | 配方升级 | 远程过渡 | 枯木火铳 + 赤铜燃弹 | PARTIAL | P3 |
| `CupriteEmeraldTwigStaff` | ~38 | 配方升级 | 魔法过渡 | 翠枝法杖 + 自然弹幕 | PARTIAL | P3 |
| `CupriteMossBomb` | — | 配方升级 | 投掷 | 苔藓炸弹分裂 | PARTIAL | P3 |
| `CupriteNatureGrimoire` | ~45 | 配方升级 | 召唤 | 自然秘典 + 精灵 minion | PARTIAL | P3 |
| `XuanTieHunterBow` | 38 | 配方升级 | 三王前弓 | 玄铁猎弓 + 流血 synergy | PARTIAL | P3 |
| `XuanTieRootBoomerang` | ~36 | 配方升级 | 回旋 | 根回旋镖 + 束缚 | PARTIAL | P3 |

**文件：** `Items/Weapons/Woodlands/Upgrades/`

### 7.3 玄铁盔甲 — 三王后（§4.3）

| 类名 | 防御 | 定位 | 作用 | 理想效果 | 状态 | P |
|------|------|------|------|----------|------|---|
| `XuanTieHelmet` | 11 | 套装 | 中期唯一护甲扩展 | 玄铁流血叠层 | COMPLETE | — |
| `XuanTieBreastplate` | 14 | 套装 | 同上 | 3 层 8% 武器伤 AoE | COMPLETE | — |
| `XuanTieLeggings` | 12 | 套装 | 同上 | +10% 移速 | COMPLETE | — |

**文件：** `Items/Armor/XuanTie/` · `Players/XuanTieSetBonusPlayer.cs`

### 7.4 四大僵尸 / 召唤

| 类名 | 定位 | 作用 | 状态 | P |
|------|------|------|------|---|
| `CoffinNailFragment` | 赢勾 1/3 材料 | 镇尸钉配方 | STUB 材料 | P2 |
| `KyuubiSummonsHairpin` | 九尾召唤 | 狐毫 · Plantera 后 | COMPLETE 召唤 | — |
| `UnderworldPairSummons` | 牛头马面召唤 | 冥途双引符 | COMPLETE 召唤 | — |

---

## 八、材料（跨线）

| 类名 | 来源 | 定位 | 理想用途 | 状态 | P |
|------|------|------|----------|------|---|
| `DragonKingScale` | 四海龙王 100% | 龙王共享材料 | 海珠召唤 + 四象碑 + 合成 | PARTIAL† | P1 |
| `GeneralOrder` | 天将 100% | 天将共享材料 | 毗沙门召唤链 | STUB | P2 |
| `FourSymbolsTablet` | 合成 | 四圣兽召唤 | 四海 + 入侵后天庭使用 | STUB | P1 |
| `QingLongSpirit` | 青龙/地表祖龙 | 灵材 | 玄铁甲/圣兽武器 | PARTIAL | P1 |
| `BaihuSpirit` | 白虎 | 灵材 | 圣兽合成 | PARTIAL | P1 |
| `SuzakuSpirit` | 朱雀 | 灵材 | 圣兽合成 | PARTIAL | P1 |
| `XuanwuSpirit` | 玄武 | 灵材 | 圣兽合成 | PARTIAL | P1 |

† `DragonKingScale` 已有 autoload 占位 PNG，无合成逻辑扩展。

---

## 九、实装优先级说明

| 级别 | 含义 | 典型项 |
|------|------|--------|
| **P0** | 阻塞可玩/崩溃 | （无 — P0 纹理已修复） |
| **P1** | 月后天庭脊柱核心 | 37 件 VANILLA_REF 武器 + 四象碑 + 龙王鳞链 |
| **P2** | 地府线 / 天将材料 / 牛头马面 | 无常之魂、怨核、冥链刃、将军令 |
| **P3** | 过渡升级 / 终局饰品占位 | 赤铜林地 7 件、酆帝冠等 |

---

## 十、代码标记约定

STUB 武器文件顶部应含：

```csharp
// PLACEHOLDER: see docs/PLACEHOLDER_CONTENT_REGISTRY.md
```

**已标记文件：**

- `Celestias/Boss/Aokins/Items/AokinWeapons.cs`
- `Celestias/Boss/Aoyuans/Items/AoyuanWeapons.cs`
- `Celestias/Boss/Aoshuns/Items/AoshunWeapons.cs`
- `Celestias/Boss/Vigors/Items/VigorWeapons.cs`
- `Celestias/Boss/Arguses/Items/ArgusWeapons.cs`
- `Celestias/Boss/Vaisravanas/Items/VaisravanaItems.cs`
- `Celestias/Boss/FourSacredBeasts/Items/FourSacredBeastWeapons.cs`
- `Items/Weapons/NiuMa/NetherChainBlade.cs`

---

## 十一、相关文档索引

| 章节 | 文档位置 |
|------|----------|
| 四海龙王掉落/伤害 | `PROGRESSION_DESIGN_SPEC.md` §5.2 |
| 天将线 | §5.3 |
| 四圣兽 | §5.4 |
| 观察者 | §5.6 |
| 地府掉落 | §6.2–§6.3 |
| 玄铁/林地 | §4.2–§4.3 |
| VANILLA_REF 美术 | `TEXTURE_COMPLETION_PLAN.md` §3.2 |
| 可玩性缺口 | `PLAYABILITY_AUDIT_REPORT.md` P1–P3 |

---

*本表随 Phase 2 实装进度更新；武器机制实装后请将对应行状态改为 COMPLETE 并移出 STUB 统计。*
