# 纹理补全计划（Texture Completion Plan）

> 生成日期：2026-05-28  
> 模组：Ancient Chinese Mythology（Primordial / 洪荒）  
> 审计基线：22 个 ModItem **缺 PNG（加载崩溃）** · 47 个 ModItem **VANILLA_REF（可加载，美术错误）**

---

## 一、优先级总览

| 优先级 | 范围 | 状态 | 策略 |
|--------|------|------|------|
| **P0** | 22 个缺 PNG 的 ModItem | ✅ **已修复**（2026-05-28 占位复制） | 复制现有 PNG 至 autoload 路径 |
| **P0b** | 9 个缺 PNG 的 ModBuff / ModProjectile（占位武器阶段） | ✅ **已修复**（2026-05-28 占位复制） | Buff → `BlankBuff`；震波/风刃 → `GlaciateWave`；回旋 → `JadeDragonChakramProjectile`；宝塔 → `VaisravanaTower` |
| **P1** | 47 个 `Terraria/Images/Item_*` 引用 | ⏳ 待美术 | 按 Boss 主题分配正式贴图 |
| **P2** | `Textures/` 目录 Git 追踪 | 📋 建议见 §四 | 纳入版本控制 + LFS 可选 |

---

## 二、P0：缺 PNG 修复策略

### 2.1 三种修复手段对比

| 手段 | 适用场景 | 优点 | 缺点 |
|------|----------|------|------|
| **A. 复制占位 PNG** | 新物品、autoload 路径明确、无 `Texture` 覆写 | tModLoader autoload 一致；无需改代码；协作友好 | 磁盘重复；占位与正式美术需二次替换 |
| **B. `Texture` 属性覆写** | 多个物品共享同一灵材精灵图（如四象精魄 → 青龙精魄） | 零重复文件；改一处即可 | 需在 `.cs` 维护路径；非 autoload 约定 |
| **C. 保留 VANILLA_REF** | Phase 1 占位武器、机制未定型 | 零美术成本、必定加载 | 玩家可见原版图标，破坏主题沉浸 |

**本模组约定：**

- **缺 PNG = 加载失败** → 必须 A 或 B，**不可**留空或仅用 C。
- **P0 已采用 A（文件复制）**，保证 autoload 与现有 colocated 资源风格一致。
- **B 仅建议用于** 语义上共享精灵图的灵材（参考 `BaihuSpirit` / `SuzakuSpirit` / `XuanwuSpirit` → `QingLongSpirit.png`）。P0 地府 7 材料仍用复制，便于后续独立换图。

### 2.2 P0 执行记录（22/22 已完成）

| # | 目标路径（相对模组根目录） | 复制来源 | 占位说明 |
|---|---------------------------|----------|----------|
| 1 | `Textures/Items/Materials/DragonKingScale.png` | `Textures/Items/Materials/QingLongSpirit.png` | 龙王鳞；正式图：龙鳞 + 水色/金色 |
| 2 | `Underworlds/Items/Materials/AwakenedNetherCore.png` | `Underworlds/Boss/AwakeningNethers/VoidCore.png` | 觉醒龙心 |
| 3 | `Underworlds/Items/Materials/ImpermanenceSoul.png` | `Underworlds/Items/SoulFragment.png` | 无常之魂 |
| 4 | `Underworlds/Items/Materials/NetherDragonScale.png` | `Textures/Items/Materials/QingLongSpirit.png` | 幽冥龙鳞 |
| 5 | `Underworlds/Items/Materials/SpectreGrudgeCore.png` | `Underworlds/Boss/Spectres/SpectreCore.png` | 怨灵核 |
| 6 | `Underworlds/Items/Materials/VoidDragonSinew.png` | `Underworlds/Boss/AwakeningNethers/Items/AbyssalSpine.png` | 虚空龙筋 |
| 7 | `Underworlds/Items/Materials/YinEssence.png` | `Underworlds/Items/SoulFragment.png` | 阴元精华 |
| 8 | `Underworlds/Items/Materials/YinImperialSeal.png` | `Items/Weapons/SoulBanners/SoulBanner.png` | 阴帝印玺 |
| 9 | `Underworlds/Boss/Spectres/Items/WraithLantern.png` | `Underworlds/Boss/BAWImpermanences/Items/Ferryman.png` | 鬼火灯笼 |
| 10 | `Underworlds/Boss/YinEmperors/Items/GhostGateKey.png` | `Underworlds/Items/SoulFragment.png` | 鬼门关钥匙 |
| 11 | `Underworlds/Boss/YinEmperors/Items/SoulBannerUnderworldRelic.png` | `Items/Weapons/SoulBanners/SoulBanner.png` | 地府万魂 relic |
| 12 | `Underworlds/Boss/YinEmperors/Items/YinEmperorEdict.png` | `Underworlds/Items/SoulFragment.png` | 阴天子诏书 |
| 13 | `Underworlds/Boss/YinEmperors/Items/FengduImperialCrown.png` | `Items/Weapons/SoulBanners/SoulBanner.png` | 酆帝冠 |
| 14 | `Items/Summons/KyuubiSummonsHairpin.png` | `Items/YingouSummon.png` | 九尾狐毫 |
| 15 | `Items/Summons/UnderworldPairSummons.png` | `Items/YingouSummon.png` | 冥途双引符 |
| 16 | `Items/Weapons/Woodlands/Upgrades/CupriteDeadwoodMusket.png` | `Items/Weapons/Woodlands/DeadwoodMusket.png` | 赤铜枯木火铳 |
| 17 | `Items/Weapons/Woodlands/Upgrades/CupriteEmeraldTwigStaff.png` | `Items/Weapons/Woodlands/EmeraldTwigStaff.png` | 赤铜翠枝法杖 |
| 18 | `Items/Weapons/Woodlands/Upgrades/CupriteMossBomb.png` | `Items/Weapons/Woodlands/MossBomb.png` | 赤铜苔藓炸弹 |
| 19 | `Items/Weapons/Woodlands/Upgrades/CupriteNatureGrimoire.png` | `Items/Weapons/Woodlands/NatureGrimoire.png` | 赤铜自然秘典 |
| 20 | `Items/Weapons/Woodlands/Upgrades/CupriteWoodlandGreatsword.png` | `Items/Weapons/Woodlands/WoodlandGreatsword.png` | 赤铜林地巨剑 |
| 21 | `Items/Weapons/Woodlands/Upgrades/XuanTieHunterBow.png` | `Items/Weapons/Woodlands/VineHunterBow.png` | 玄铁猎弓 |
| 22 | `Items/Weapons/Woodlands/Upgrades/XuanTieRootBoomerang.png` | `Items/Weapons/Woodlands/RootBoomerang.png` | 玄铁根回旋镖 |

**验证：** 上述 22 路径均已存在；`dotnet build` 通过（0 错误）。

### 2.3 P0b 执行记录（9/9 已完成 — ModBuff / ModProjectile）

| # | 目标路径 | 复制来源 | 占位说明 |
|---|----------|----------|----------|
| 1 | `Celestias/Boss/Aokins/Items/DraconicEmberBuff.png` | `Textures/Buffs/BlankBuff.png` | 龙魂余烬召唤 buff |
| 2 | `Celestias/Boss/Aokins/Items/FlamecoilChakramProjectile.png` | `Celestias/Boss/AoGuangs/Items/JadeDragonChakramProjectile.png` | 焰缠双环回旋弹幕 |
| 3 | `Celestias/Boss/FourSacredBeasts/Items/AurelianShockwave.png` | `Textures/Masking/GlaciateWave.png` | 金裂地冲击波 |
| 4 | `Celestias/Boss/FourSacredBeasts/Items/AzureTorrentBladesBuff.png` | `Textures/Buffs/BlankBuff.png` | 蔚蓝剑群协战 buff |
| 5 | `Celestias/Boss/FourSacredBeasts/Items/SolarisEternalVerdictBuff.png` | `Textures/Buffs/BlankBuff.png` | 日轮审判召唤 buff |
| 6 | `Celestias/Boss/FourSacredBeasts/Items/WindserpentSlash.png` | `Textures/Masking/GlaciateWave.png` | 风蛇刀气 |
| 7 | `Celestias/Boss/Vaisravanas/Items/TreasurePagodaStack.png` | `Celestias/Boss/Vaisravanas/VaisravanaTower.png` | 层叠宝塔驻留体 |
| 8 | `Celestias/Boss/Vigors/Items/VerdictSealShockwave.png` | `Textures/Masking/GlaciateWave.png` | 裁决震波 |
| 9 | `Celestias/Boss/Vigors/Items/VerdictSealShockwavePulse.png` | `Textures/Masking/GlaciateWave.png` | 裁决震波脉冲 |

**验证：** 全模组 ModBuff / ModProjectile 审计 0 缺图；`dotnet build` 通过（0 错误）。

---

## 三、P1：47 个 VANILLA_REF 物品 — 主题美术分配

### 3.1 主题色板与参考

| 主题 | Boss / 来源 | 主色 | 形态关键词 | 可参考现有资源 |
|------|-------------|------|------------|----------------|
| 🔥 **火 / 龙** | 敖钦（Aokin）、敖广线 | 朱红、金、焰纹 | 枪、环、弓、法杖、流星 | `Celestias/Boss/AoGuangs/`、`JadeDragonChakram` |
| ⚡ **雷 / 风暴** | 敖顺（Aoshun） | 紫电、青白、裂空 | 戟、链鞭、连弩、雷书、巨剑 | 渡劫云、`ThunderOrb` |
| ❄️ **冰 / 水** | 敖闰（Aoyuan） | 冰蓝、墨黑、霜纹 | 冰刃、三叉戟、水书、扇、冰弓 | 原版 `IceSickle` 色系升级版 |
| 🐉 **圣兽 / 四象** | 青龙/白虎/朱雀/玄武 | 青/白/赤/玄 | 各象专属武器形态 | `FourSacredBeasts/` Boss 贴图 |
| 👁️ **天眼 / 百目** | 百目（Argus） | 金色、虹彩、瞳纹 | 弓、星炮、手铳 | `OverseersEye` 材料 |
| 🛡️ **天将 / 神威** | 神威（Vigor）、毗沙门（Vaisravana） | 金甲、天门光 | 巨剑、锤、法杖、盾 | 天庭 Pillar 系列 |
| 📜 **杂项占位** | 非 Boss 链 | — | 见下表 | 对应进度材料 |

### 3.2 完整分配表（Item → 推荐源 PNG → 正式美术说明）

#### 四海龙王占位武器（15）

| 物品类名 | 当前 VANILLA | Boss | 主题 | 推荐正式贴图路径 | 美术说明 |
|----------|-------------|------|------|------------------|----------|
| `InfernoDragonSpear` | Gungnir | 敖钦 | 🔥龙 | `Celestias/Boss/Aokins/Items/InfernoDragonSpear.png` | 焰纹龙枪，枪尖焚风 |
| `FlamecoilChakram` | LightDisc | 敖钦 | 🔥龙 | `…/FlamecoilChakram.png` | 双环交缠火 coil |
| `CrimsonMaelstromBow` | Marrow | 敖钦 | 🔥龙 | `…/CrimsonMaelstromBow.png` | 赤色风暴弓，箭袋如旋涡 |
| `DraconicEmber` | PygmyStaff | 敖钦 | 🔥龙 | `…/DraconicEmber.png` | 龙魂召唤杖，顶端余烬 |
| `MeteorCallerStaff` | AmberStaff | 敖钦 | 🔥龙 | `…/MeteorCallerStaff.png` | 流星唤魔杖 |
| `ThunderlordHalberd` | Gungnir | 敖顺 | ⚡雷 | `Celestias/Boss/Aoshuns/Items/ThunderlordHalberd.png` | 雷纹方天戟 |
| `StormchainWhip` | DD2SquireBetsySword | 敖顺 | ⚡雷 | `…/StormchainWhip.png` | 闪电链鞭（非剑形） |
| `TempestRepeater` | VenusMagnum | 敖顺 | ⚡雷 | `…/TempestRepeater.png` | 风暴连弩 |
| `LightningEdictTome` | BookofSkulls | 敖顺 | ⚡雷 | `…/LightningEdictTome.png` | 雷敕天书 |
| `StormcleaverGreatsword` | BreakerBlade | 敖顺 | ⚡雷 | `…/StormcleaverGreatsword.png` | 裂风巨剑 |
| `GlacialDragonblade` | IceSickle | 敖闰 | ❄️冰 | `Celestias/Boss/Aoyuans/Items/GlacialDragonblade.png` | 冰龙镰刃 |
| `PermafrostTrident` | Trident | 敖闰 | ❄️冰 | `…/PermafrostTrident.png` | 永冻三叉戟 |
| `VortexPrimordialStain` | BookofSkulls | 敖闰 | ❄️水 | `…/VortexPrimordialStain.png` | 漩涡魔书，水墨感 |
| `InkscaledFlowFan` | MagicMirror | 敖闰 | ❄️水 | `…/InkscaledFlowFan.png` | 墨鳞流风扇（非镜） |
| `BlizzardPiercer` | IceBow | 敖闰 | ❄️冰 | `…/BlizzardPiercer.png` | 暴雪穿云弓 |

#### 四圣兽占位武器（12）

| 物品类名 | 当前 VANILLA | 圣兽 | 主题 | 推荐路径 | 美术说明 |
|----------|-------------|------|------|----------|----------|
| `AzureTorrentBlades` | Excalibur | 青龙 | 🐉青 | `Celestias/Boss/FourSacredBeasts/Items/AzureTorrentBlades.png` | 双短刃，青水流光 |
| `WindserpentDao` | BreakerBlade | 青龙 | 🐉青 | `…/WindserpentDao.png` | 风蛇长刀 |
| `ThunderclapLongbow` | PulseBow | 青龙 | ⚡ | `…/ThunderclapLongbow.png` | 雷鼓长弓 |
| `AurelianCataclysmSmasher` | PaladinsHammer | 白虎 | 🐉白 | `…/AurelianCataclysmSmasher.png` | 金纹灾 hammer |
| `ArgentPulseObliterator` | VortexBeater | 白虎 | 🐉白 | `…/ArgentPulseObliterator.png` | 银脉冲枪 |
| `WhiteTigerClaws` | FeralClaws | 白虎 | 🐉白 | `…/WhiteTigerClaws.png` | 虎爪拳套 |
| `StarfireAnnihilator` | VortexBeater | 朱雀 | 🐉赤 | `…/StarfireAnnihilator.png` | 星火灭杀枪 |
| `SolarisEternalVerdict` | OpticStaff | 朱雀 | 🐉赤 | `…/SolarisEternalVerdict.png` | 日轮审判杖 |
| `PhoenixFlameStaff` | RainbowRod | 朱雀 | 🔥 | `…/PhoenixFlameStaff.png` | 凤凰焰杖 |
| `GeocrystalShatterblade` | BreakerBlade | 玄武 | 🐉玄 | `…/GeocrystalShatterblade.png` | 地晶碎刃 |
| `GeoarchonRupturer` | StaffofEarth | 玄武 | 🐉玄 | `…/GeoarchonRupturer.png` | 地尊裂岩杖 |
| `BlackTortoiseShield` | AnkhShield | 玄武 | 🐉玄 | `…/BlackTortoiseShield.png` | 玄龟盾，龟甲纹 |

#### 天庭 / 天将占位（10）

| 物品类名 | 当前 VANILLA | Boss | 主题 | 推荐路径 | 美术说明 |
|----------|-------------|------|------|----------|----------|
| `SoulPiercingArc` | PulseBow | 百目 Argus | 👁️ | `Celestias/Boss/Arguses/Items/SoulPiercingArc.png` | 穿魂弧弓，瞳纹弓身 |
| `LuminanceStellarCannon` | VortexBeater | 百目 | 👁️ | `…/LuminanceStellarCannon.png` | 光华星炮 |
| `LuminousIrisAnnihilator` | Handgun | 百目 | 👁️ | `…/LuminousIrisAnnihilator.png` | 虹膜手铳 |
| `SinSeveringBlade` | BreakerBlade | 神威 Vigor | 🛡️ | `Celestias/Boss/Vigors/Items/SinSeveringBlade.png` | 断罪巨剑 |
| `AureateVoidrender` | Excalibur | 神威 | 🛡️ | `…/AureateVoidrender.png` | 金装虚空刃 |
| `VerdictSealHammer` | PaladinsHammer | 神威 | 🛡️ | `…/VerdictSealHammer.png` | 裁决印锤 |
| `TreasurePagodaStaff` | RainbowRod | 毗沙门 | 🛡️ | `Celestias/Boss/Vaisravanas/Items/TreasurePagodaStaff.png` | 宝塔法杖 |
| `VaultshadeVoidshot` | SniperRifle | 毗沙门 | 🛡️ | `…/VaultshadeVoidshot.png` | 库藏虚空狙 |
| `CelestialCircletScepter` | StaffofRegrowth | 毗沙门 | 🛡️ | `…/CelestialCircletScepter.png` | 天冠权杖 |
| `TreasurePagodaCharm` | PaladinsShield | 毗沙门 | 🛡️ | `…/TreasurePagodaCharm.png` | 宝塔护符 |

#### 其他 VANILLA_REF（10）

| 物品类名 | 当前 VANILLA | 类别 | 推荐路径 | 美术说明 |
|----------|-------------|------|----------|----------|
| `FourSymbolsTablet` | LunarTabletFragment | 四象材料 | `Textures/Items/Materials/FourSymbolsTablet.png` | 四象石板，四色分区 |
| `GeneralOrder` | AdamantiteHeadgear | 天将材料 | `Textures/Items/Materials/GeneralOrder.png` | 将令卷轴/令牌 |
| `OverseersEye` | EyeoftheGolem | 观察者材料 | `Textures/Items/Materials/OverseersEye.png` | 天眼宝珠 |
| `MaMianSeal` | AncientGoldHelmet | 牛头 seal | `Textures/Items/Materials/MaMianSeal.png` | 马面印（与牛头区分色） |
| `NiuTouSeal` | AncientGoldHelmet | 牛头 seal | `Textures/Items/Materials/NiuTouSeal.png` | 牛头印 |
| `NetherChainBlade` | ChainKnife | 牛头马面武器 | `Items/Weapons/NiuMa/NetherChainBlade.png` | 冥链刃 |
| `SoulHookWhip` | ThornWhip | 牛头马面武器 | `Items/Weapons/NiuMa/SoulHookWhip.png` | 勾魂鞭 |
| `RuyiJinguBang` | SilverBroadsword | 西游线 | `Textures/Items/Weapons/Sticks/RuyiJinguBang.png` | 金箍棒（已有 stick 系列可延伸） |
| `TrueRuyiStick` | Item_676 | 西游线 | `Textures/Items/Weapons/Sticks/TrueRuyiStick.png` | 真·如意棍 |
| `ZhenfaBook` | Item_149 | 阵法 | `Textures/Items/ZhenfaBook.png` | 阵法书（与 `ZhenfaPaper` 统一风格） |

### 3.3 P1 实施步骤

1. 按上表在 **colocated 路径**（与 `.cs` 同目录）或 **`Textures/`** 下新建 PNG。
2. 删除对应类中的 `public override string Texture => "Terraria/Images/Item_…"` 行，改由 autoload 或显式指向新路径。
3. 四象精魄类可继续共用 `QingLongSpirit.png` + `Texture` 覆写，直到独立精灵图完成。
4. Boss 武器优先于材料；同一 Boss 5 件武器保持统一色板。

---

## 四、P2：Git 追踪建议

### 4.1 现状

- 模组约 **595 个 PNG**，多数在 `Textures/`、`Underworlds/`、`Items/`、`Celestias/` 等目录。
- 根目录 `.gitignore` **未排除** `Textures/`，但大量 PNG 仍为 untracked（协作时易丢失）。
- P0 新增的 22 个占位 PNG 同样需要纳入版本控制。

### 4.2 建议

| 项 | 建议 |
|----|------|
| **追踪范围** | 追踪全部 `*.png` / `*.gif`（物品、NPC、弹幕、Buff、UI） |
| **忽略项** | 保留 `/bin`、`/obj`；**不要**忽略 `Textures/` |
| **大文件** | 单文件 >1MB 或仓库 PNG 总量持续增长时，启用 **Git LFS**（`*.png filter=lfs`） |
| **占位标记** | 占位复制文件在提交信息中标注 `[texture-placeholder]`，便于 P1 批量替换 |
| **CI 可选** | 增加脚本：扫描 `ModItem` 子类，断言对应 PNG 或 `Texture` 覆写存在 |

### 4.3 推荐 `.gitattributes` 片段（可选）

```
*.png filter=lfs diff=lfs merge=lfs -text
*.gif filter=lfs diff=lfs merge=lfs -text
```

---

## 五、文件夹命名约定（本模组）

tModLoader 纹理路径规则：`AncientChineseMythology/{相对模组根的路径，无扩展名}`

### 5.1 两种并存模式

| 模式 | 典型目录 | 代码特征 | 示例 |
|------|----------|----------|------|
| **集中式 `Textures/`** | `Textures/Items/`、`Textures/NPCs/`、`Textures/Projectiles/` | 显式 `Texture => "AncientChineseMythology/Textures/…"` | `DragonKingScale`、`BlazingFlowerSeeds`、渡劫云 |
| **Colocated 同级资源** | `Items/`、`Underworlds/`、`Celestias/Boss/*/` | 无 `Texture` 覆写，PNG 与 `.cs` 同名同目录 | `SoulFragment.png`、`WoodlandGreatsword.png`、`Ferryman.png` |

### 5.2 命名规则

1. **文件名 = 类名**（PascalCase），扩展名 `.png`。
2. **子目录 = 命名空间/内容域**：如 `Items/Weapons/Woodlands/Upgrades/` 对应升级武器。
3. **`Textures/Items/Materials/`**：跨 Boss 共享的材料、灵魄、碎片。
4. **`Underworlds/`**：地府线 Boss、材料、武器自成 subtree，与 `Celestias/` 对称。
5. **Boss 掉落武器**：优先 `{BossFolder}/Items/{ItemName}.png`（与 `BAWImpermanences/Items/Ferryman.png` 一致）。
6. **精魄共享**：`QingLongSpirit.png` 为四象精魄母版；白虎/朱雀/玄武可用 `Texture` 覆写直至独立图完成。

### 5.3 新物品 checklist

- [ ] 确定使用 `Textures/` 还是 colocated
- [ ] 创建 `{Path}/{ClassName}.png`（**P0 缺图会崩溃**）
- [ ] 若共享精灵图，添加 `Texture` 覆写并在此文档登记
- [ ] 避免新增 VANILLA_REF，除非 Phase 1 占位且已在 P1 表登记

---

## 六、后续工作摘要

| 任务 | 负责 | 备注 |
|------|------|------|
| P0 占位复制 | ✅ 完成 | 22/22 路径已存在 |
| P1 龙王/四象/天将武器正式美术 | 美术 | 47 项，按 §3.2 表 |
| P1 材料/杂项贴图 | 美术 | 10 项，见 §3.2 末表 |
| Git 追踪 `Textures/` + 占位 PNG | 工程 | 见 §4 |
| 可选：地府材料改 `Texture` 覆写减重复 | 程序 | 仅当确认不需独立换图 |

---

*文档版本：1.0 · 与 P0 修复同步*
