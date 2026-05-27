# 洪荒模组 · 进度设计二次迭代 V2

> **文档性质：** `PROGRESSION_DESIGN_SPEC.md` v3.3.1 的**第二轮综合核查与裁定**  
> **版本：** Iteration V2 · 2026-05-28  
> **输入：** 正确性审计（§1）· 遗漏补全（§2）· 原版桥接（§3）· 三路 Phase 合并（§6）  
> **代码快照：** 以仓库 `.cs` 实读为准；与 spec/audit 冲突处以**本文件 §4 裁定**为准

---

## 前言 · 迭代目的与文档关系

### 迭代目的

1. **对照代码**校验 `PROGRESSION_DESIGN_SPEC.md`（目标设计）与 `PLAYABILITY_AUDIT_REPORT.md`（现状审计）中的数值、顺序、文件路径。
2. **合并**四路并行设计会话 + 二次迭代三路发现（正确性 / 遗漏 / 原版桥接），形成**单一裁定层**，避免多份文档互相矛盾。
3. 输出 **Phase 1–3 合并优先级**，供实现排期；未落地路径统一标 **Phase 2/3**，不再写入「已实现」表述。

### 与 v3.3.1 的关系

| 文档 | 角色 | 迭代 V2 后 |
|------|------|-----------|
| `PROGRESSION_DESIGN_SPEC.md` v3.3.1 | Master Design Spec | **保留**为规格主文档；§1.5 指向本文件；§4/§5 按本文件修订 |
| `PLAYABILITY_AUDIT_REPORT.md` | 现状审计 | **已同步**关键数值勘误（棍链、敖广 5%、黑熊 10%、觉醒龙 HP、符咒 9 件） |
| **本文件 ITERATION_V2** | 二次迭代权威 | 正确性 ERROR 清单、冲突裁定、遗漏 P0–P3、原版桥接表、文档 diff 清单 |

**v3.3.1 不可推翻的裁定（本迭代确认）：**

- **四大僵尸** = **月灵之后早期**（T24–27 目标），红木棺材 + 鬼面具 ML 门控。
- **非**肉前赢勾、**非** HM Plantera 邻接四灾脊柱。

**v3.3.1 需修正的裁定（本迭代 §4）：**

- **九尾妖狐** = **Plantera 后 HM**，在 ML **之前**；**不得**排在月后四僵尸之后（spec Act 4a T28 为笔误）。
- **万魂幡** 设计 **T1–52**；代码仅 **T1–28** 且顺序错乱 → Phase 2 重排。

---

## §1 正确性核查报告

> **统计：** **27 ERRORS**（必须改文档或代码）· **22 WARNINGS**（应改，不阻断发布）· **9 INCONSISTENCIES**（跨文档/流程图冲突，§4 已裁定）  
> **图例：** 🔴 ERROR · 🟡 WARNING · 🟣 INCONSISTENCY

### 1.1 ERROR 清单（27 条 · 含修正建议）

| ID | 严重度 | 主题 | 错误描述 | 代码/文件依据 | 修正建议 |
|----|--------|------|----------|---------------|----------|
| E01 | 🔴 | 敖广掉落率 | 审计写「各 20%」 | `AoGuang.cs` L171–175：`Common(..., 5)` = **5%** | 审计已改；spec 附录已写 5% ✓ |
| E02 | 🔴 | 黑熊武器率 | spec 附录 A「各 20%」 | `BlackBear.cs` L568–570：`Common(..., 10)` = **10%** | spec 附录改为 10%；或 Phase 2 提至 20% |
| E03 | 🔴 | 棍链伤害 | 审计写「8→120」跳档 | `WoodenStick` 8 · `IronStick` 28 · `GoldenStick` 48 · `GemStick` 68 · `RuyiStick` 120 | 审计已改完整链；注明真如意 32、金箍棒配方注释 |
| E04 | 🔴 | 真如意伤害 | spec §4.2 设计 **200** | `TrueRuyiStick.cs` `damage = 32`，配方注释 | Phase 2：启用配方并调至 200 |
| E05 | 🔴 | 金箍棒伤害 | spec **260** | `RuyiJinguBang.cs` `damage = 120`，配方注释 | Phase 2：启用配方并调至 260 |
| E06 | 🔴 | 觉醒冥龙 HP | 审计写 **800k** | `AwakeningNetherHead.cs` **11,200,000**；体节 800k | 审计已改；spec §6/§7 已写 1120万 ✓ |
| E07 | 🔴 | 地表祖龙 HP | 审计 **500k** vs spec **90k** 重标定 | `ArchosaurBoss.cs` 需实读 | 统一为 spec 90k 目标；审计改表 |
| E08 | 🔴 | 万魂幡：赢勾位 | 设计 T26（月后） | `SoulBannerPlayer.cs` T7「犬戎」= `Yingou` | Phase 2：移除 T7 赢勾；T24–27 四僵尸 |
| E09 | 🔴 | 万魂幡：四僵尸位 | 设计 T24–27 月后 | 代码 T15 旱魃、T17 后卿、T21 将臣（HM 内） | Phase 2 整体重排至 ML 后 |
| E10 | 🔴 | 万魂幡：九尾位 | 设计 **Plantera 后 ~T15** | 代码 T24（ML 后，在四僵尸设计位之前） | Phase 2：九尾移至 Plantera 后 tier |
| E11 | 🔴 | 万魂幡 tier 上限 | 设计 **T52** 阴天子 | 代码止于 T28 敖顺 | Phase 2：扩展表 + 天庭/地府 Boss |
| E12 | 🔴 | 万魂幡 cap 公式 | spec `cap[n]=cap[n-1]+600+(n×50)` | 代码 cap 为手工表，与 spec T24=7400 等不完全一致 | Phase 2：按公式重算或改 spec 为手工表 |
| E13 | 🔴 | 红木棺材计数 | 设计每世界 **5** 处 | `OnWorldLoad()` **重置** `coffinsGenerated=0` | 删除 L27–29 重置；仅 `OnWorldUnload` 清会话 |
| E14 | 🔴 | 红木棺材生成停 | 设计 5 处 | `PostUpdateWorld`：世界存在**任意**棺材 TP 即 `return` | 改为计数已放置数，允许多处直至 5 |
| E15 | 🔴 | 鬼面具门控 | 设计须 `downedMoonlord` | `YingouSummon.cs` 无 ML 检查 | Phase 2 增加门控 |
| E16 | 🔴 | DownedBoss 重置 | 进度持久化 | `DownedBossSystem.cs` 等 `OnWorldLoad` 在 `LoadWorldData` 后清零 | Phase 1：删 `OnWorldLoad` 覆盖（spec §3.1） |
| E17 | 🔴 | 九尾进度位 | spec Act 4a 表 T28（月后四僵尸后） | 与 §1.4、流程图 PL→KY→ML 冲突 | **裁定：** Plantera HM；spec §1.5、Act 3 表 |
| E18 | 🔴 | 生肖符咒数量 | 审计「5 件饰品」 | `Charms.cs`：**9 类**（鸡牛狗马龙猪兔蛇鼠） | 审计已改；虎羊猴缺类 |
| E19 | 🔴 | 虎羊猴配方 | spec §4.2 四饰品 | 无 `TigerCharm`/`GoatCharm`/`MonkeyCharm` | Phase 2 新增 3 类 + Buff |
| E20 | 🔴 | 奇异石掉率 | spec **1/800** + pity | `GlobalZodiacSpirits` 等仍为 **1/10000** | Phase 2 改率 + `ZodiacPityPlayer` |
| E21 | 🔴 | 亵渎伤害带 | spec Plantera 后 **320–380** | 审计 Profane **1400–1500**，无 Boss | Phase 2 新建 `ProfaneRoot`；下调伤害 |
| E22 | 🔴 | 牛头马面掉落 | spec §4.1.1 完整表 | `NiuMa_NPC.cs` **无** `ModifyNPCLoot` | Phase 1/2 补掉落 |
| E23 | 🔴 | 地表祖龙掉落 | spec §4.1.3 | `ArchosaurBoss.cs` 无 loot / Checklist 回调错 | Phase 2 补 loot + `downedSurfaceArchosaur` |
| E24 | 🔴 | 四海龙王 3/4 | spec 四海各 5 武器 5% | 敖钦/闰/顺 **空** loot | Phase 1 以敖广为模板填充 |
| E25 | 🔴 | 四圣兽/天将/观察者 | spec 完整掉落 | 代码 **空或占位** | Phase 1 P0 列表（spec §8） |
| E26 | 🔴 | 阴天子/幽冥龙/Spectre | spec §6.2 | **空** loot | Phase 1 P0 |
| E27 | 🔴 | description.txt | 宣称 2 Boss / 18 武器 | 实际 40+ / 130+ | Phase 1 重写（spec §3.7） |

### 1.2 WARNING 清单（22 条 · 摘要）

| ID | 主题 | 说明 | 建议 |
|----|------|------|------|
| W01 | 赢勾显示名 | SB 写「犬戎」非「赢勾」 | 本地化统一 |
| W02 | 将臣显示名 | SB 写「蛟尘」 | 同上 |
| W03 | 双祖龙 | 地表 90k vs Celestias 800万 | 保留双实体；掉落/标记分离（spec ✓） |
| W04 | 双青龙 | Azure 1200万 vs Qinlong 200万 | Phase 2 苍龙觉醒合并（spec §5.7） |
| W05 | TrueRuyi 配方 | 已写配方但注释 | Phase 2 启用 + 三王门控 |
| W06 | RuyiJinguBang 月碎片 | 注释配方含 Fragment* | **禁止** ML 前月碎片（§3.6 反模式 1） |
| W07 | 青龙之灵 | 材料存在无掉落 | 地表祖龙 + Qinlong 定向 |
| W08 | 天界之钥传送 | 代码注释 | Phase 3 或移除 |
| W09 | 地府 ModBiome | 仅本地化 | Phase 3 |
| W10 | en-US 空 tooltip | 大量未译 | Phase 3 |
| W11 | 玄铁甲 | spec 有、无 `Items/Armor/XuanTie/` | Phase 2 |
| W12 | 林地升级 | spec `Woodlands/Upgrades/` | **Phase 2** 新建 |
| W13 | 冥途双引符 | spec 召唤物 | **Phase 2** 新建 |
| W14 | ProfaneRoot Boss | spec 根株 65k | **Phase 2** 新建 |
| W15 | RealmGateChecker | spec §3.2 八门控 | **Phase 2** 新建 |
| W16 | ProgressionGatingGlobalItem | spec 门控入口 | **Phase 2** 新建 |
| W17 | ModRecipeGroups | spec §3.5 | **Phase 2** 新建 |
| W18 | progression.json | spec Phase 3 | **Phase 3** |
| W19 | 酆都有效 DPS | 面板 3800–24800 vs 目标 5500–6500 | Phase 3 调谐 |
| W20 | 土地公阵法纸价 | 与 spec 1石/5纸 可能不符 | 对照 `TuDiNPC.cs` |
| W21 | 孟婆冥府令牌 | spec 首枚 5 金 | 商店待实现 |
| W22 | Boss Checklist | 仅 2 条；Archosaur 回调错 | Phase 1 修 + Phase 2 全量 |

### 1.4 ERROR 逐条说明（展开 · 供实现对照）

**E01–E02 掉落率：** Terraria `Common(item, N)` 即 1/N 概率。文档「20%」若指「五件独立各 20%」则与代码「五件各 5%」完全不同；**以代码为准**写入 spec/audit，避免 QA 按错误期望刷装。

**E03–E05 棍链：** 前五段伤害与 spec 一致；断档在 **真如意**（32）与 **金箍棒**（120，且配方注释）。玩家若取消注释配方，终局棍远低于设计 200/260，破坏「西游主轴」定位。

**E06–E07 HP：** 觉醒冥龙为 worm 多头，`AwakeningNetherHead` 1120万为玩家所见总池；审计表仅写体节 80万会误导难度评估。地表祖龙需统一为 spec 90k **或** 明确「未重标定仍 500k」二选一。

**E08–E12 万魂幡：** 代码将 **赢勾** 插在 T7（肉前段），使新手过早接触 420k HP 鬼将（若召唤无 ML 门控则更严重）。四僵尸占 HM tier 15/17/21，与 **v3.3.1「月后 T24–27」** 直接冲突。cap 手工值（如 T24=7400）与公式递推存在 ±50 偏差，实现时需一次性重算避免 UI 显示「下一位 8100」与实际不符。

**E13–E14 棺材：** `SaveWorldData`/`LoadWorldData` 本可持久化 5 棺，但 `OnWorldLoad` 清零导致**每次读档从 0 重新计**；同时 `TileProcessorLoader` 在**世界已存在 1 棺**时不再生成，与「最多 5」设计矛盾。玩家体验：ML 后地下仅遇 1 棺。

**E15–E17 九尾：** spec Act 4a 将九尾列在月后四僵尸之后（T28），与 mermaid `PL→KY→ML`、修仙 G4（`downedKyuubi` 在 PL 后 HM）三重冲突。**迭代裁定 B** 后，所有新文档以 Plantera HM 为准。

**E18–E20 符咒：** 9 类已实现 ≠ 12 生肖齐全；缺虎羊猴使「定向精魄」表半悬空。奇异石 1/10000 使饰品线几乎不可玩，与 spec 1/800 + pity 差距 **12.5×**。

**E21–E27 endgame / 文案：** Profane 若无根株 Boss 则 7 件武器永无来源；11 Boss 空 loot 阻断天庭/地府双轨；`description.txt` 影响 Workshop 转化与玩家预期，属 P0 营销债。

### 1.5 WARNING 逐条说明（精选）

- **W03–W04 双祖龙/双青龙：** 非 ERROR，但需在 Checklist 与对话中区分「地表 bootstrap」与「Celestias 终局」。
- **W06 RuyiJinguBang 月碎片：** 注释配方若启用会复现 tModLoader 常见坏 Practice；必须与 §3.6 反模式 1 联动审查。
- **W19 酆都 DPS：** 面板 12800+ 与天庭 5200 并列时，玩家以为「地府碾压」，实际需攻速/MP 折算；Phase 3 数值实装时必测。

### 1.6 INCONSISTENCY 清单（9 条 → §4 裁定）

| ID | 冲突 A | 冲突 B | 裁定 |
|----|--------|--------|------|
| I01 | 流程图 PL→KY→ML | Act 4a 九尾 T28 在 ML 四僵尸后 | **九尾归 Act3 Plantera HM** |
| I02 | v3.3.1 四僵尸 T24–27 | SB 代码 T7/15/17/21 | **以 v3.3.1 为准**，Phase 2 改代码 |
| I03 | spec cap 公式 | SB 手工 cap | Phase 2 二选一写死 |
| I04 | 审计 Tier 2.5「九尾前」 | 九尾 Plantera HM | **九尾在 ML 前**；Tier 2.5 仅四僵尸 |
| I05 | 觉醒龙 800k（旧审计） | Head 11.2M | **以 Head 为准** |
| I06 | 黑熊 20%（旧附录） | 代码 10% | **以代码为准** 或改代码 |
| I07 | 敖广 20%（旧审计） | 代码 5% | **以代码 5% 为准** |
| I08 | 符咒「5 件」 | 9 类实现 | **9 类**；缺虎羊猴 |
| I09 | Kyuubi SB T24 vs G4 `downedKyuubi` | 门控暗示 Plantera+ | **G4 与 HM 九尾一致**；SB 重排 |

---

## §2 遗漏内容补全清单

> **规模估计：** ~**87** 项设计条目无完整进度闭环 · **11** Boss 无 loot · **~22** 仅本地化武器 · 八卦/阵法、NPC 商店、万魂幡 **T1–28 vs T1–52** 差距显著。

### 2.1 按系统分类（摘要）

| 类别 | 遗漏规模 | 代表项 | 优先级 |
|------|----------|--------|--------|
| Boss 掉落 | 11 Boss 空/占位 | 四圣兽、神威/百目、三龙王、阴天子、观察者、Spectre、幽冥龙、牛头 | **P0** |
| 武器来源 | ~22 本地化 only | `AzureRuinBlade`、`StarfireAnnihilator` 等 | P1–P2 |
| 配方/召唤 | ~15 spec 未建文件 | 冥途双引符、九尾狐毫、ProfaneRoot、四象碑、酆帝诏书… | **P2** |
| 盔甲 | 3–4 套 spec | 玄铁、天将、酆都阴元 | P2 |
| 修仙门控 | 8 Gates 全未接 | `RealmGateChecker`、`MythologySidebar` | P2 |
| 万魂幡 | T29–52 + 顺序 | `SoulBannerPlayer.cs` | **P2** |
| 生肖 | 虎/羊/猴饰品 | `Charms.cs` | P2 |
| 八卦/阵法 | 30+ 阵；纸经济 | 土地公定价、教程 | P3 |
| NPC 商店 | 孟婆令牌、唐僧化缘、老君调价 | §3.4 | P1 |
| 数据驱动 | progression.json | Phase 3 | P3 |

### 2.2 Boss 无掉落 / 占位（11 · P0 对齐 spec §8）

| Boss | HP（约） | 现状 | Phase |
|------|----------|------|-------|
| 牛头马面 | 10k×2 | 无 loot | 1 |
| 敖钦/敖闰/敖顺 | 420–430k | 空 | 1 |
| 神威/百目 | 200万 | 空 | 1 |
| 毗沙门天 | 135万 | 空/不全 | 1 |
| 四圣兽 | 200–250万 | 占位 | 1 |
| 天庭观察者 | 120万 | 无来源 | 1 |
| 幽冥龙 | 120k | 空 | 1 |
| Spectre | 120k | 空 | 1 |
| 阴天子 | 1200万 | 空 | 1 |
| 地表祖龙 | 待统一 | 无 loot | 2 |
| 亵渎根株 | — | **未实现** | 2 |

### 2.3 仅本地化武器（22 · 分配见 spec §5.9）

Celestias 线占位：`DraconicEmber`、`GlacialDragonblade`、`ThunderlordHalberd`、`LuminanceStellarCannon`、`VaultshadeVoidshot`、`CelestialHubAnnihilator` 等 — **须绑定 Boss 掉落或合成**，不可长期悬空。

### 2.4 八卦 / 阵法 / 修仙（under-covered）

| 项 | 现状 | 目标 |
|----|------|------|
| 八卦阵 UI | 30+ 阵独立成长 | 与 `downed` / 纸经济挂钩 |
| 阵法纸 | 土地公出售 | 1 奇异石 ↔ 5 纸（spec §3.4） |
| 修仙 16 阶 | 与 Boss 脱节 | G0–G7 门控（spec §3.2） |
| 劫云 | XOR 伤害 bug | Phase 1 修复 |

### 2.5 万魂幡：代码 T1–28 vs 设计 T1–52

| 段 | 设计（v3.3.1） | 代码（2026-05-28） |
|----|----------------|-------------------|
| 肉前～WoF | T1–8 无赢勾 | T7 **赢勾** ❌ |
| HM～Plantera | T9–14；**九尾 ~T15** | T15–16 旱魃/PL；九尾在 T24 ❌ |
| ML | T22–23 | T22–23 ✓ |
| 月后四僵尸 | **T24–27** | T15/17/21 + T24 九尾 ❌ |
| 天庭 | T28–45 | T24–28 部分天庭 ❌ |
| 地府 | T46–52 | **缺失** ❌ |

### 2.6 优先级汇总 P0–P3

| 级别 | 条数 | 内容 |
|------|------|------|
| **P0** | 6 | DownedBoss 重置；11 Boss 核心 loot；description；Checklist；冥府令牌引导；劫云 XOR |
| **P1** | 8 | 万魂幡重排启动；鬼面具 ML；四海模板；NPC 商店；真如意/金箍棒启用；英文关键 tooltip |
| **P2** | 12+ | T52 扩展；八门控；前期 Boss 召唤；生肖 3 件；ProfaneRoot；林地升级；棺材 bug；vanilla 桥接 §3.5 |
| **P3** | 6+ | progression.json；ModBiome；Fengdu DPS 调谐；指南书；全量 en-US |

### 2.7 遗漏条目明细（~87 项 · 按文件域）

> 下列为「设计 spec 已写但代码无闭环」或「有代码无获取」的合并清单，供 Phase 2 拆 ticket。

#### 2.7.1 系统 / 基础设施（12）

| # | 条目 | spec 引用 | 状态 |
|---|------|-----------|------|
| 1 | `DownedBossSystem.OnWorldLoad` 重置 | §3.1 | 🔴 Phase 1 |
| 2 | `AncientChineseMythologySystem` 同类重置 | §3.1 | 🔴 Phase 1 |
| 3 | `NetherDragonDownedSystem` 合并 | §3.1 | Phase 1 |
| 4 | `ProgressionGatingGlobalItem.cs` | §3.1 | Phase 2 新建 |
| 5 | `RealmGateChecker.cs` + 8 Gates | §3.2 | Phase 2 |
| 6 | `ModRecipeGroups.cs` 五组 | §3.5 | Phase 2 |
| 7 | `progression.json` + `ProgressionConfigSystem` | §3.5 | Phase 3 |
| 8 | `ZodiacPityPlayer.cs` | §3.4 | Phase 2 |
| 9 | `StrangeStoneGlobalNPC` 1/800 | §3.4 | Phase 2 |
| 10 | `BossChecklistIntegration` 全量 | §3.6 | P1→P2 |
| 11 | `GuideBook.cs` 进度指南 | §8 Ph3 | Phase 3 |
| 12 | 地府 `ModBiome` | audit §6 P2 | Phase 3 |

#### 2.7.2 前期～中期 Boss / 召唤（14）

| # | 条目 | 状态 |
|---|------|------|
| 13 | 牛头马面 `ModifyNPCLoot` | 空 · P0 |
| 14 | `UnderworldPairSummons.cs` 冥途双引符 | 未建 · P2 |
| 15 | 九尾狐毫召唤物 | 未建 · P2 |
| 16 | `downedNiuMa` 回调 | 待统一 · P1 |
| 17 | `downedKyuubi` 回调 | 待统一 · P1 |
| 18 | `downedSurfaceArchosaur` vs `downedArchosaur` | 区分 · P2 |
| 19 | `ProfaneRoot` Boss + 7 武器 320–380 | 未建 · P2 |
| 20 | `ProfaneCore` ML 升华 | 树精 · Ph3 |
| 21 | 地表祖龙 90k 重标定 + loot | 审计 500k 冲突 · P2 |
| 22 | 祖龙逆鳞召唤 | 未建 · P2 |
| 23 | 鬼面具 `downedMoonlord` | 未检 · P2 |
| 24 | 红木棺材多棺生成 | bug · P1 |
| 25 | 四大僵尸 `downedHanba/Hoqing/Yingou/Jiangcen` | 未注册 · P2 |
| 26 | 劫云 XOR 伤害 | bug · P0 |

#### 2.7.3 天庭 Celestias（22）

| # | 条目 | 状态 |
|---|------|------|
| 27 | 敖钦/敖闰/敖顺 各 5 武器 5% | 空 · P0 |
| 28 | `DragonKingScale.cs` 材料 | 待建/待 drop · P1 |
| 29 | 东海/南海/西海/北海珠召唤 | 未建 · P2 |
| 30 | 神威 3 武器 + 将军令 | 空 · P0 |
| 31 | 百目 3 武器 | 空 · P0 |
| 32 | 毗沙门 3 武器 + 宝塔饰 | 空 · P0 |
| 33 | 四圣兽各 3 武器 + 灵材 6–10 | 占位 · P0 |
| 34 | `FourSymbolsTablet` 四象碑 | 未建 · P2 |
| 35 | 祖龙残魂 Celestias 3 选 1 | 部分 ✓ | 
| 36 | 天御金龙 3 选 1 | 部分 ✓ |
| 37 | 观察者 8 武器 + 眼 8–12 | 无来源 · P0 |
| 38 | 青龙 15% 苍龙觉醒 hook | 未合并 · P2 |
| 39 | `downedAzureDragon` / `downedCelestialOverseer` 等 | 未注册 · P1 |
| 40 | `HeavenlyGeneralPlate` 天将甲 3 件 | 未建 · P2 |
| 41 | 天极镐 | 未建 · Ph3 |
| 42 | `DragonKingSpawnSystem` 自然刷 | 可选 · P3 |
| 43 | `QuadraseaCataclysmicEdge` 合成 | 未建 · P2 |
| 44 | 天庭观察者入侵终局触发 | 待接 · P2 |
| 45 | 20× 本地化 Celestias 武器分配 | 无来源 · P1–P2 |
| 46 | `downedHeavenInvasion` 奖励爆发 | 待平衡 · P2 |
| 47 | 四海「有效 DPS」调谐文档 | 设计 · P3 |
| 48 | 天柱武器 7 件材料桥 | 部分 ✓ |

#### 2.7.4 地府 Underworld（18）

| # | 条目 | 状态 |
|---|------|------|
| 49 | 阴天子 酆帝印 + 精华 + 33% 三选一 | 空 · P0 |
| 50 | 幽冥龙 鳞 8–12 + 4 武器 25% | 空 · P0 |
| 51 | Spectre 怨核 + 灯笼 | 空 · P0 |
| 52 | `ImpermanenceSoul` 无常之魂 | 未建 · P2 |
| 53 | 无常 EX 升级链 720 伤 | 未建 · P2 |
| 54 | `AwakenedNetherCore` / `VoidDragonSinew` | 待补 · P2 |
| 55 | 酆帝诏书召唤 | 未建 · P2 |
| 56 | `FengduYin` 酆都阴元甲 3 件 | 未建 · P2 |
| 57 | 酆都黑帝甲升级 | 未建 · P3 |
| 58 | `NetherPickaxe` / `NetherAxe` | 未建 · P2 |
| 59 | 桥梁武器 4 件（怨缚秘典等） | 未建 · P2 |
| 60 | `DakkiBook` 合成链 | 部分 · P2 |
| 61 | 枉骸→EX 4 映射 | 未建 · P2 |
| 62 | 孟婆冥府令牌商店 | 未建 · P1 |
| 63 | ML 首次对话赠 10 残魂 | 未建 · P1 |
| 64 | 地府入侵 325 残魂/次平衡 | 待测 · P3 |
| 65 | `downedYinEmperor` 等 7+ 标记 | 未注册 · P1 |
| 66 | Fengdu 有效 DPS 5500–6500 实装 | 面板过高 · P3 |

#### 2.7.5 装备 / 武器 / 材料（21）

| # | 条目 | 状态 |
|---|------|------|
| 67 | 玄铁甲 3 件 + 套装 bonus | 未建 · P2 |
| 68 | 林地→赤铜/玄铁 6 升级 | 未建 · P2 |
| 69 | 虎/羊/猴 `Charms` | 未建 · P2 |
| 70 | `QingLongSpirit` 掉落 | 无 · P2 |
| 71 | 奇异石 1/800 | 仍 1/10000 · P2 |
| 72 | 生肖定向精魄表落地 | 部分 · P2 |
| 73 | TrueRuyi 伤害 200 + 配方启用 | 32/注释 · P2 |
| 74 | RuyiJinguBang 260 + 无月碎片 | 120/注释 · P1–P2 |
| 75 | Profane 7 件来源 | 无 · P2 |
| 76 | 观察者 8 件来源 | 无 · P0 |
| 77 | 天界之钥传送 | 注释 · P3 |
| 78 | HM 钓竿 / 钻锯 | 无 · P3 |
| 79 | Cuprite + HellstoneBar 共炼 | 未建 · P1 |
| 80 | `Bone` mod 材 vs `ItemID.Bone` | 冲突 · P1 |
| 81 | DivineWood + Chlorophyte | 未建 · P1 |
| 82 | Revenant + Ectoplasm | 未建 · P1 |
| 83 | Umbral + Bone/RottenChunk | 未建 · P1 |
| 84 | 冥火护体丹 / 还魂丹 | 未建 · P2 |
| 85 | 太上老君地火 15 金 | 待改 · P2 |
| 86 | 土地公 1 石 5 纸 | 待改 · P2 |
| 87 | `description.txt` v3.3 | 过时 · P0 |

### 2.8 仅本地化武器寄存表（22 · spec §5.9 扩展）

| 武器 | 建议归属 Boss | 目标伤害带 |
|------|--------------|-----------|
| `DraconicEmber` | 敖钦 | 340 |
| `InfernoDragonSpear` | 敖钦 | 350 |
| `FlamecoilChakram` | 敖钦 | 355 |
| `CrimsonMaelstromBow` | 敖钦 | 360 |
| `MeteorCallerStaff` | 敖钦 | 365 |
| `GlacialDragonblade` | 敖闰 | 355 |
| `PermafrostTrident` | 敖闰 | 360 |
| `VortexPrimordialStain` | 敖闰 | 365 |
| `InkscaledFlowFan` | 敖闰 | 370 |
| `BlizzardPiercer` | 敖闰 | 375 |
| `ThunderlordHalberd` | 敖顺 | 370 |
| `StormchainWhip` | 敖顺 | 375 |
| `TempestRepeater` | 敖顺 | 380 |
| `LightningEdictTome` | 敖顺 | 385 |
| `AzureRuinBlade` | 敖顺 | 420 |
| `AzureTorrentBlades` | 青龙 | 1480 |
| `StarfireAnnihilator` | 朱雀 | 1520 |
| `LuminanceStellarCannon` | 百目 | 1200 |
| `VaultshadeVoidshot` | 毗沙门 | 1280 |
| `CelestialHubAnnihilator` | 观察者 | 2500 |
| `AureateVoidrender` | 神威 | 1120 |
| `VerdictSealHammer` | 神威 | 1250 |

### 2.9 万魂幡目标 Tier 全表（设计 T1–52 · 对照代码）

| Tier | Cap | 解锁 Boss | 代码现状 | 备注 |
|------|-----|-----------|----------|------|
| 1 | 50 | 史莱姆王 | T1 ✓ | |
| 2 | 120 | 克眼 | T2 ✓ | |
| 3 | 200 | 黑熊精 | T3 ✓ | |
| 4 | 300 | 世吞/克脑 | T4 ✓ | |
| 5 | 420 | 蜂后 | T5 ✓ | |
| 6 | 560 | 骷髅王 | T6 ✓ | |
| 7 | — | ~~赢勾~~ | **T7 赢勾** ❌ | 删除 |
| 8 | 880 | 鹿角怪 | T8 ✓ | |
| 9 | 1100 | 血肉墙 | T9 ✓ | |
| 10 | 1350 | 牛头马面 | T10 ✓ | |
| 11 | 1600 | 史莱姆皇后 | T11 ✓ | |
| 12 | 1900 | 毁灭者 | T12 ✓ | |
| 13 | 2200 | 双子 | T13 ✓ | |
| 14 | 2550 | 机械骷髅 | T14 ✓ | |
| 15 | **~2900** | **九尾妖狐** | **T24** ❌ | 移入 |
| 16 | 3300 | 世纪之花 | T16 PL ✓ | |
| 17–23 | … | 三王后→月灵 | T17–23 错位 | 重排 |
| 24 | 7400 | 旱魃 | T15 ❌ | 移至 24 |
| 25 | 8150 | 后卿 | T17 ❌ | 移至 25 |
| 26 | 8900 | 赢勾 | T7 ❌ | 移至 26 |
| 27 | 9650 | 将臣 | T21 ❌ | 移至 27 |
| 28 | 10350 | 神威 | T25 九尾位 ❌ | 顺延 |
| 29–35 | … | 百目→毗沙门 | 部分 | |
| 36–45 | … | 树精→苍龙 | **缺失** | |
| 46–52 | … | 地府→阴天子 | **缺失** | |

---

## §3 原版关联与材料桥接设计

> **本节保留**任务 3/3 完整产出；与 §1 不重复，仅补充 **门控与 v3.3.1 对齐说明**。

### 3.0 设计目标与现状摘要

**目标：** 肉前 → 月灵前，模组进度与原版里程碑（EoC → Skeletron → WoF → 三王 → Plantera → 石巨人/事件）**同频共振**；材料经济避免「纯 mod 矿 silo」，Boss 掉落承担**桥接奖励**职能。

**与 v3.3.1 对齐：**

- **九尾狐毫**、亵渎种子、地表祖龙逆鳞：门控 **Plantera**，属 Tier D（§3.1），**不属于** ML 后四僵尸段。
- **四僵尸**桥接掉落（§3.2 ML 表）为 **补充**，不替代 HM 九尾主轴。
- **TrueRuyi / RuyiJinguBang**：启用配方时 **不得** 在 ML 前消耗月碎片（§3.6 反模式 1）。

**现状（代码实读）：**

| 区域 | 原版融合度 | 主要问题 |
|------|-----------|----------|
| 林地 `Woodlands/` | ★★★★☆ | `ItemID.Wood/Vine/Gel/Stinger/JungleSpores/FallenStar` 已用；缺 WoF 后升级链（spec §4.2 **Phase 2**） |
| 青铜 `Bronze/` | ★★★☆☆ | 锭配方已接 `Copper/Tin/Iron/Lead`；武器/工具仍为纯 `BronzeIngot` |
| 棍链 `Sticks/` | ★★★★☆ | 铁/金/宝石/狱石已桥接；**`TrueRuyiStick` / `RuyiJinguBang` 配方被注释** |
| 赤铜 `Cuprite/` | ★★☆☆☆ | 仅 mod 矿 + `Hellforge`；甲仅 + 青铜，缺狱炎/神圣桥 |
| 玄铁 `XuanTie/` | ★☆☆☆☆ | 纯 mod 矿/锭/剑；spec 玄铁甲 **Phase 2** |
| 神木 `DivineWoods/` | ★☆☆☆☆ | 仅 `Livinglog` @ `MythrilAnvil`，零原版材料 |
| 冥府早/中期 | ★★☆☆☆ | `Umbral`/`Revenant` 纯 mod；`CoffinNail` 是良好反例 |
| 入侵召唤 | ★★★★★ | 已用月锭+碎片/灵气 |
| 炼丹 `RecipeSystem` | ★★★★☆ | 四丹已接原版药草 + 落星 |

**工作台权威映射：**

| 原版工作台 | 模组层级 | 代表物品 |
|-----------|---------|----------|
| `WorkBenches` | Act 0 | 林地、`Charms`、丹炉 |
| `Furnaces` | 肉前～HM | 青铜锭、玄铁锭、幽冥锭 |
| `Anvils` | 肉前～HM | 棍链、`Cuprite` 甲、`TrueRuyi`（待启用） |
| `HeavyWorkBench` | EoC 后 | `GemStick` |
| `Hellforge` | WoF 后 | `Cuprite` 锭、`RuyiStick` |
| `MythrilAnvil` | 三王后 | `Revenant`、`DivineWoods`、`CoffinNail` |
| `AdamantiteForge` | 三王后 | `NetherOre`→锭 |
| `LunarCraftingStation` | ML 后 | 入侵令、EX/Fengdu |

### 3.1 配方原版材料桥接表（Tier A–E）

#### Tier A — 肉前 · Act 0

| 模组物品/配方 | 当前原版材料 | 建议新增原版材料 | 原版里程碑 | 神话/主题依据 |
|--------------|-------------|-----------------|---------------|--------------|
| `WoodenStick`（唐僧赠） | — | — | 开局 | 引路「先从木棍开始」 |
| `IronStick` | `RecipeGroupID.IronBar`×81 | + `ItemID.Gel`×15（减铁×9） | 地下铁矿 | 妖气炼棍需粘质定形 |
| `GoldenStick` | `ItemID.GoldBar`×81 | + `ItemID.Lens`×3 | **EoC** 后 | 克眼「洞察」→ 金棍开锋 |
| `GemStick` | 六宝石各×10 | + `ItemID.FallenStar`×5 | EoC 后流星雨 | 星宿入棍 |
| `BronzeSword` | —（纯 `BronzeIngot`×18） | + `WormTooth`×5 或 `Vertebrae`×5 | EoC / 世吞 | 青铜杀伐需孽牙淬毒 |
| `BronzePickaxe/Axe` | — | + `ItemID.Wood`×10 | 肉前 | 柄以凡木 |
| `WoodlandGreatsword` 等 7 件 | `Wood/Vine/JungleSpores/Gel/Stinger` | + `StoneBlock`×20（巨剑/盾） | 肉前 | 林野武器以石磨刃 |
| `NatureGrimoire` | `ItemID.Book`×1 | — | 肉前 | 已接原版书 |
| `BoneSword` | mod `Bone`×20 | → **`ItemID.Bone`×15** + `RottenChunk`×5 | Skeletron 前地牢 | 统一骨材 |
| `XuanTieSword` | — | + `ItemID.IronBar`×8 | 肉前（可选） | 神铁凡形需人间铁骨 |
| `ScrapElixir` | `FallenStar`×5 | — | 流星雨 | 已桥接 |
| 四丹（血魄/凝神/玄罡/破军） | 四原版药草×9 | + `BottledWater`×1/丹 | 药草解锁 | 太上炼丹沿用原版药引 |
| `ElixirFurnaceItem` | — | + `Obsidian`×20（Phase 2） | WoF 后 | 丹炉隔热 |

#### Tier B — EoC～WoF

| 模组物品 | 建议新增 | 门控 |
|----------|----------|------|
| 黑熊间接 | `HoneyComb` 配方饰 | 蜂后 |
| `Cuprite` 锭 | + `HellstoneBar`×2/4 矿 | WoF |
| `Cuprite` 甲 | + `Obsidian` / `HellstoneBar` | WoF |
| `RuyiStick` | + `SoulofLight/Night`×10 | WoF |
| 林地→赤铜升级（**Phase 2**） | 林地巨剑 + 赤铜 + 妖气 | WoF |
| **冥途双引符**（**Phase 2**） | 骨 + 光暗魂 + 妖气 @ 恶魔祭坛 | WoF |

#### Tier C — HM 前期（WoF～Plantera）

| 模组物品 | 建议新增 | 门控 |
|----------|----------|------|
| `TrueRuyiStick`（启用） | 三王魂×25 + `HallowedBar`×20 | ≥1 三王 |
| 玄铁甲（**Phase 2**） | 玄铁 + 青龙之灵 + `HallowedBar` | 三王 |
| `Revenant` | + `SoulofNight` + `Ectoplasm` | HM |
| `Umbral` | + `Bone` + `RottenChunk` | WoF |
| **九尾狐毫**（**Phase 2**） | 奇异石 + 妖气 + 丛林孢子 + 惧魂 | **Plantera** |

#### Tier D — Plantera～ML 前

| 模组物品 | 建议新增 | 门控 |
|----------|----------|------|
| `DivineWoods` | + `ChlorophyteBar` + `BeetleHusk` | Plantera |
| `RuyiJinguBang`（启用） | 叶绿 + 龟壳；**无月碎片** | Plantera+ |
| 祖龙逆鳞（**Phase 2**） | 青龙之灵 + 神圣锭 + 力魂 | 三王+PL |
| Profane 种子（**Phase 2**） | `Ichor` + 惧魂 + 奇异石 | Plantera |
| 生肖高阶饰 | `BiomeKey` 定向 | 对应 Boss |

#### Tier E — ML 前末期（可选加速）

霜月、海盗、蘑菇线材料 — **可选**，非硬性（金箍棒不含 `LunarBar`）。

### 3.2 模组 Boss 原版掉落桥接表

> **原则：** 每场 mod Boss 除 mod 材料外，至少 1 条**可立即用于原版或桥接配方**的掉落；率用 `ItemDropRule.Common(item, N)` = 1/N。

#### 肉前～WoF

| 模组 Boss | 当前原版掉落 | 建议新增原版掉落 | 掉落率 | 里程碑 | 依据 |
|-----------|-------------|-----------------|--------|--------|------|
| **黑熊精** `BlackBear` | `GoldCoin` 5–10（100%） | `ItemID.Lens`×1–2 | 100% | EoC 后 | 黑风洞府「明目」 |
| | | `ItemID.HoneyComb`×1 | 33% | 蜂后前可选 | 熊精与蜂巢灵韵 |
| | | `ItemID.AncientCopperHelmet` 或 `GladiatorHelmet` | 20% | 肉前 | 妖洞藏古人类兵器 |
| mod 武器 | 勇士金剑/弓/冠 各 **10%** | — | 已代码 | 非 20% |

#### HM · Plantera 前

| 模组 Boss | 建议新增原版掉落 | 掉落率 | 里程碑 |
|-----------|-----------------|--------|--------|
| **牛头马面**（待补 loot） | `GoldCoin` 8–15 | 100% | WoF 后 |
| | `ItemID.Ectoplasm`×0–2 | 50% | HM |
| | `SoulofLight`×2–4 **或** `SoulofNight`×2–4 | 100% | WoF 后 |
| | `ItemID.Rope`×30–50 | 66% | 勾魂索材料提示 |
| **劫云** 成功 | `FallenStar`×1–3 | 100% | 修仙 |

#### HM · Plantera 后～ML 前

| 模组 Boss | 建议新增 | 掉落率 | 里程碑 |
|-----------|----------|--------|--------|
| **九尾妖狐** | `GoldCoin` 15–25 | 100% | Plantera |
| | `JungleSpores`×5–10 | 100% | 狐居丛林 |
| | `BeetleHusk`×1–2 | 50% | 桥接叶绿 |
| | `Yelets` 或 `MagicQuiver` | 10% | 狐射 |
| **地表祖龙** | `GoldCoin` 20–30 | 100% | 三王+PL |
| | `SoulofMight`×3–5 | 100% | 龙魂 |
| | `HallowedBar`×5–8 | 100% | 神圣 |
| **亵渎根株**（Phase 2） | `Ichor`×3–5 | 100% | Plantera |
| | `SoulofLight`×10 或 15% `RodofDiscord` | 100%/15% | 空间扭曲 |

#### ML 后早期（四僵尸 · 桥接 only）

| 模组 Boss | 建议新增 | 掉落率 | 说明 |
|-----------|----------|--------|------|
| 旱魃/后卿/将臣 | `YaoQiFragment` 10–20 | 100% | spec |
| 赢勾 | `Ectoplasm`×5–8 | 100% | 桥接 `CoffinNail` |
| 四僵尸共用 | `StrangePlant`×1 | 25% | 炼丹引 |

#### ML 后 · 天庭/地府（endgame 桥接 · Phase 2）

| 模组 Boss | 建议原版桥接 | 目的 |
|-----------|-------------|------|
| 敖广–敖顺 | `LunarBar`×0（**禁止**早期）/ `GoldCoin` 加强 | 仅金币与魂，月锭仅合成台 |
| 神威/百目 | `SoulofMight`×5–10 | 机械魂收尾 |
| 阴天子 | `LunarBar`×8–12 @ 月球配方 | 终局消耗 |
| 觉醒冥龙 | `SpectreBar`×6–10 可选 | 与 Dakki 链一致 |

### 3.3 「原版桥接」迷你进度链（6 条 · 详述）

#### 链 1 · 明目炼棍（EoC）

```
击杀克苏鲁之眼 → 掉落 Lens
→ 合成 GoldenStick（+Lens×3，减金锭）
→ 唐僧对话更新：「金灿灿的已备，可嵌宝石于重Workbench」
→ 侧路：黑熊精 33% HoneyComb → 前期饰
```

#### 链 2 · 狱火赤铜（WoF）

```
击败血肉墙 → HM 开始
→ 唐僧提示：「你不下地狱谁下地狱」→ Hellforge 炼 RuyiStick
→ Cuprite 锭配方 +HellstoneBar×2/4 赤铜矿（共炼解锁）
→ 合成 Cuprite 甲（+Obsidian/HellstoneBar）
→ 冥途双引符 @ 恶魔祭坛 → 牛头马面
```

#### 链 3 · 三王真如意（Mechanical）

```
击杀任一机械 Boss → SoulofFright/Might/Sight
→ 启用 TrueRuyiStick 配方（魂×25 + HallowedBar×20 + RuyiStick）
→ 唐僧：「须断邪教召唤之怪」→ 指向 Plantera/教徒
→ 合成 XuanTie 甲（三王门控）
```

#### 链 4 · 丛林狐焰（Plantera）

```
击败世纪之花 → 丛林孢子/HM 地牢开放
→ 九尾狐毫 @ MythrilAnvil（+SoulofFright×5）
→ 九尾掉落 BeetleHusk → 提示叶绿/龟壳线
→ 并行：Profane 种子（+Ichor）或 地表祖龙（+HallowedBar）
→ 【注意】此链在 ML 之前，与四僵尸无关
```

#### 链 5 · 金箍棒前置（Pre-ML，无月碎片）

```
TrueRuyiStick + ChlorophyteBar×100 + TurtleShell×10
→ RuyiJinguBang @ MythrilAnvil（禁用 Fragment* / LunarBar）
→ 唐僧误导向钓鱼（现有台词彩蛋）→ 实际需石巨人/海龟
→ Frost Moon / 海盗 / Biome Key 为可选加速
```

#### 链 6 · 冥府入门（WoF～ML）

```
WoF → Umbral 武器（+Bone/RottenChunk）@ Anvils
→ 牛头马面 → Ectoplasm/SoulofNight 桥接
→ Plantera 后 Revenant（+Ectoplasm）@ MythrilAnvil
→ ML 后 MengPo 售首枚冥府令牌（5 金 + 10 残魂赠礼）
→ ML 后可选：红木棺材四僵尸（与链 4 九尾分离）
```

### 3.4 NPC 商店集成

| NPC | 关键调整 | 门控 |
|-----|----------|------|
| 唐僧 | 化缘店：水瓶、虫牙、落星包 | 肉前/EoC |
| 土地公 | 1 奇异石 ↔ 5 阵法纸 | 肉前 |
| 太上老君 | 地火 15 金；药草互换 | 肉前/HM |
| 孟婆 | **冥府令牌** 5 金；ML 赠 10 残魂 | **ML** |
| 大圣 | 妖气换云朵瓶等 | 黑熊后 |

### 3.5 实施清单（原版桥接 · 合并 Phase）

| 优先级 | 动作 |
|--------|------|
| P0 | 启用 TrueRuyi / RuyiJinguBang（无月碎片）；BlackBear/NiuMa/Kyuubi/Archosaur loot |
| P1 | Cuprite 共炼 `HellstoneBar`；Bone 统一；DivineWood + Chlorophyte |
| P2 | 林地升级、冥途双引符、NPC 商店、唐僧对话链 |

### 3.6 反模式清单（严禁 10 条）

1. ML 前月碎片 · 2. ML 前 `LunarBar` · 3. PL 前叶绿/龟壳 · 4. 连续 3 级纯 mod 矿 · 5. 双 Bone 体系 · 6. 未杀三王可合真如意 · 7. 甲零原版锭 · 8. Boss 零原版掉落 · 9. 阵法纸仅金币无价 · 10. ML 前冥府令牌  

### 3.7 验收指标

肉前～PL 主武器线 ≥50% 配方含 `ItemID.*`；黑熊/牛头/九尾/祖龙 ≥3 原版掉落；真如意/金箍棒无 ML 前月碎片；土地公换纸 + 孟婆令牌上线。

---

## §4 二次迭代综合裁定

> 以下冲突以**本文件为二次迭代权威**；`PROGRESSION_DESIGN_SPEC.md` 已增 §1.5 指向此处。

### 4.1 九尾妖狐位置（I01 / I09 / E17）

| 方案 | 描述 | 裁定 |
|------|------|------|
| A | Act 4a · ML 后 · 四僵尸之后（spec 旧表 T28） | ❌ 废除 |
| B | Act 3 · **Plantera 后 HM** · ML 之前（流程图 PL→KY→ML） | ✅ **采用** |

**实施含义：**

- 召唤：**九尾狐毫**需 `downedPlantBoss`（已实现召唤物则补门控）。
- 万魂幡：目标 tier **~15–16**（Plantera 邻接），**不是** T28。
- 修仙 **G4** `downedKyuubi` 与 B 一致。
- **四大僵尸**仍在 **ML 后 T24–27**，与九尾**分离**。

### 4.2 万魂幡 T1–52（I03 / E11–E12）

| 项目 | 裁定 |
|------|------|
| 终局 tier | **T52** 阴天子（采纳 v3.3.1） |
| 四僵尸 | **T24–27**（旱魃→后卿→赢勾→将臣） |
| 九尾 | **~T15–16**（HM），在 ML 前 |
| 天庭/地府 | T28–45 天庭；T46–52 地府（spec §3.3 表） |
| cap | Phase 2：**或**全表按公式重算 **或** spec 改为「手工 cap 表」二选一 |
| 代码 | 当前 T1–28 **作废顺序**；Phase 2 重写 `SoulBannerPlayer.cs` |

### 4.3 红木棺材（E13–E14）

| 项目 | 设计 | 代码真相 | Phase 1 修复 |
|------|------|----------|--------------|
| 数量上限 | 5/世界 | 常仅 **1**（任意 TP 存在即停） | 改检测为 `coffinsGenerated` 计数 |
| 持久化 | 存档保留 | `OnWorldLoad` **清零** | **删除** L27–29 |
| ML 门控 | 必须 ML | ✓ `downedMoonlord` | 保持 |
| 鬼面具 | 仅 ML 召赢勾 | 无检查 | Phase 2 补 |

### 4.4 生肖符咒（E18–E19 / I08）

| 项目 | 裁定 |
|------|------|
| 已实现 | **9** 类：鸡牛狗马龙猪兔蛇鼠（武器 2 + 饰品 7） |
| 有配方 | 9 中 **鼠** 及既有 8 件均有 `AddRecipes` |
| 缺失 | **虎、羊、猴** — Phase 2 补 `Charms` + `Buff` + 定向精魄 |
| 掉率 | 全局奇异石目标 **1/800** + pity（Phase 2） |

### 4.5 关键数值裁定（E01–E07）

| 项 | 权威值 | 备注 |
|----|--------|------|
| 敖广武器 | **5%×5** | `Common(..., 5)` |
| 黑熊武器 | **10%×3** | 可 Phase 2 提至 20% 若设计需要 |
| 棍链 | **8→28→48→68→120** | 真如意/金箍棒另议 |
| 觉醒冥龙头 | **11,200,000** | 审计已同步 |
| 四僵尸 | ML 后；推荐顺序不变 | v3.3.1 |

### 4.6 未实现路径统一标记

凡 spec 出现下列字样且无对应 `.cs`：**Phase 2**（除非 spec §8 已列 Phase 1）  
`新建`、`待建`、`待实现`、`Upgrades/`、`ProfaneRoot/`、`RealmGateChecker`、`UnderworldPairSummons`、`ProgressionGatingGlobalItem`、`ModRecipeGroups`、`HeavenlyGeneralPlate`、`FengduYin/`、`GuideBook`、`progression.json`。

---

## §5 文档修正清单

> 对 Master Spec 与 Audit 的**具体 edits**；✅ = 本次已应用 · ☐ = 建议后续应用

### 5.1 `PROGRESSION_DESIGN_SPEC.md`

| 章节 | 修改内容 | 状态 |
|------|----------|------|
| §1.4 末 | 棺材 bug + `OnWorldLoad` 说明 | ✅ |
| **§1.5 新增** | 指向 ITERATION_V2；九尾/黑熊/Phase2 标记 | ✅ |
| §2.1 流程图 | 已 PL→KY→ML；保持 | ✅ |
| §2.2 Act 4a | 九尾行移注 Act3 / T15 目标 | ✅ |
| §3.3 万魂幡表 | T28 九尾行改 **T15–16 目标** | ✅ |
| 附录 A 黑熊 | 20% → **10%** | ✅ |
| §4.2 林地升级 | 标 **Phase 2** | ☐ 文内加标记 |
| §4.2 ProfaneRoot | 标 **Phase 2** | ☐ |
| §3.2 G0–G7 | `RealmGateChecker` 标 **Phase 2** | ☐ |
| §8 Phase 1 | 合并 §6 顶 5 项 | ☐ 见 §6 |

### 5.2 `PLAYABILITY_AUDIT_REPORT.md`

| 章节 | 修改内容 | 状态 |
|------|----------|------|
| 头部 | 链接 ITERATION_V2 | ✅ |
| §3.1 棍链 | 8→28→48→68→120 | ✅ |
| §3.1 敖广 | 20% → **5%** | ✅ |
| §3.1 符咒 | 9 件 / 缺虎羊猴 | ✅ |
| §3.6 黑熊 | 武器 **10%** | ✅ |
| §3.6 觉醒龙 | **1120万** | ✅ |
| §5.2 Tier 2.5 | 移除「九尾前」误导 | ✅ |
| §3.6 地表祖龙 HP | 500k → 对齐 spec 90k 目标 | ☐ |
| §6 P0 | 与 §6 合并清单交叉引用 | ☐ |

### 5.3 spec 内「Phase 2」标记建议（☐ 待批量插入）

在下列章节首行增加引用块 `> **实现状态：Phase 2** — 见 ITERATION_V2 §2.7`：

- §4.2 林地升级 `Woodlands/Upgrades/`
- §4.2 Profane Phase A `ProfaneRoot/`
- §3.2 `RealmGateChecker` 全文
- §3.1 `ProgressionGatingGlobalItem`
- §3.5 `progression.json`
- §5.8 天将重甲
- §6.7–6.8 酆都甲/幽冥工具（若无 `.cs`）
- §5.2 四海珍珠召唤物（若未建 Items）

### 5.4 交叉引用矩阵（读哪份文档）

| 问题 | 首选文档 |
|------|----------|
| 目标伤害/配方/门控 | `PROGRESSION_DESIGN_SPEC.md` |
| 代码有没有实现 | `PLAYABILITY_AUDIT_REPORT.md` |
| 数值是否与代码一致 | **本文件 §1** |
| 冲突谁说了算 | **本文件 §4** |
| 原版材料怎么接 | **本文件 §3** |
| 先做哪件事 | **本文件 §6 Top 5** |

---

## §6 更新后的 Phase 1 / 2 / 3 实施优先级

> 合并 **spec §8** · **audit §6** · **本迭代 §1–§3**。

### Phase 1 — 阻断性（1–2 周）

| # | 任务 | 来源 |
|---|------|------|
| 1 | 删除 `DownedBossSystem` 等 `OnWorldLoad` 重置 | E16 · audit P0 |
| 2 | 填充 **11 Boss** 核心 loot（四海模板、四圣兽、天将、观察者、地府三 Boss、牛头） | E22–E26 · §2.2 |
| 3 | 修复红木棺材 `OnWorldLoad` + 生成逻辑（允许多棺至 5） | E13–E14 |
| 4 | Boss Checklist + `description.txt` | E27 · W22 |
| 5 | 劫云 XOR → 加法伤害 | audit P0 |
| 6 | 孟婆首枚冥府令牌 + ML 赠残魂 | §3.4 · W21 |
| 7 | 统一 `downedBlackBear`；新增 §3.1 关键 downed 回调 | spec §3.1 |
| 8 | 启用 TrueRuyi / RuyiJinguBang（**无 ML 碎片**）+ Cuprite/HellstoneBar | §3.5 P0 · E05–E06 |

### Phase 2 — 进度闭环（2–4 周）

| # | 任务 | 来源 |
|---|------|------|
| 9 | 万魂幡 **T1–52 重排**（九尾 ~T15；四僵尸 T24–27；天庭/地府顺延） | §4.2 · E08–E11 |
| 10 | 鬼面具 ML；移除 T7 赢勾 | E15 · E08 |
| 11 | 八修仙门控 + 引劫 UI（`RealmGateChecker`） | W15–W16 |
| 12 | 前期 Boss 召唤/loot：牛头、九尾、地表祖龙、亵渎根株 | E21–E23 |
| 13 | 虎/羊/猴符咒 + 奇异石 1/800 + pity | E18–E20 |
| 14 | 林地升级、冥途双引符、玄铁甲 | W12–W13 · §3.5 |
| 15 | 青龙→苍龙合并；RecipeGroups | spec §5.7 · W17 |
| 16 | NPC 商店 §3.4；vanilla 桥接 P1 表 | §3.5 |
| 17 | 修正 TrueRuyi 32→200、金箍棒 120→260（若启用配方） | E04–E05 |

### Phase 3 — 打磨（持续）

| # | 任务 |
|---|------|
| 21 | `progression.json` + 加载器 |
| 22 | 游戏内进度指南书 |
| 23 | 地府 ModBiome |
| 24 | 天极镐 / 天柱传送 |
| 25 | en-US 全量 |
| 26 | Fengdu 有效 DPS 调谐 |
| 27 | Profane ML 升华（树精 `ProfaneCore`） |

### Top 5 优先动作（合并 Phase · 立即）

1. **修复 `DownedBossSystem.OnWorldLoad`** — 否则一切进度门控无效。  
2. **P0 Boss loot 批量填充**（至少四海模板 + 四圣兽 + 阴天子 + 观察者 + 牛头）。  
3. **红木棺材双 bug**（世界加载重置 + 单棺停生）。  
4. **万魂幡顺序裁定落地**（九尾回 HM / 四僵尸 T24–27 / 扩展至 T52 路线图）。  
5. **启用棍链终段配方且无月碎片**（TrueRuyi + RuyiJinguBang + Cuprite 狱炎桥）。

---

## 附录 A — Soul Banner 代码对照（2026-05-28）

```
T7  赢勾(Yingou)     ← 应删除（月后 T26）
T15 旱魃             ← 应移至 T24（ML 后）
T17 后卿             ← 应移至 T25
T21 将臣             ← 应移至 T27
T24 九尾             ← 应移至 ~T15（Plantera HM）
T25–28 天庭早期      ← 顺延至 T28+（设计表）
```

## 附录 B — 棍链伤害代码对照

| 物品 | 代码 damage | spec 目标 |
|------|-------------|-----------|
| WoodenStick | 8 | 8 |
| IronStick | 28 | 28 |
| GoldenStick | 48 | 48 |
| GemStick | 68 | 68 |
| RuyiStick | 120 | 120 |
| TrueRuyiStick | **32** | **200** |
| RuyiJinguBang | **120** | **260** |

## 附录 D — 八卦阵法与 BaGua / Zhenfa 覆盖（审计缺口）

| 维度 | 现状 | 迭代建议 | 优先级 |
|------|------|----------|--------|
| 阵法数量 | 30+ 独立 UI | 与 `downed` / 纸经济挂钩 | P3 |
| 阵法纸来源 | 土地公金币购 | **1 奇异石 ↔ 5 纸** + 金币贵购双轨 | P2 |
| 八卦阵盘 | 老君售 `BaGuaZhenpan` | 定价与入门教程 | P2 |
| 与修仙关系 | 平行于 Boss 脊柱 | G4+ 可选「阵法槽」门控 | P3 |
| 与万魂幡关系 | 无联动 | T35+ 可选「阵魂」加成 | P3 |
| 教程 | 缺 | 唐僧/土地公对话链 3 步 | P2 |

**设计原则：** 阵法为 **横向成长**，不得替代 Boss 掉落主循环；纸消耗应对标「每小时 2–3 场 Boss」的材料节奏。

## 附录 E — 万魂幡 cap 公式演算（spec §3.3）

公式：`cap[n] = cap[n-1] + 600 + (n × 50)`，四舍五入到 50。

| n | 递推计算 | 公式值 | spec 表值 | Δ |
|---|----------|--------|-----------|---|
| 1 | 50（基底） | 50 | 50 | 0 |
| 8 | 1100 | 1100 | 1100 | 0 |
| 14 | 2550+600+700=3850→3850 | 3850 | 3300 | **-550** |
| 24 | … | 7400 | 7400 | 0 |
| 28 | … | 10350 目标 | 代码 10500 | +150 |
| 52 | … | 37500 | 37500 | 0 |

**结论：** 中段 tier（HM）手工表与公式 **不完全一致**（I03）。Phase 2 实现时二选一：**全公式生成** 或 **导出手工表为 JSON** 写入 `progression.json`。

## 附录 F — 修仙门控 G0–G7 与 Boss 映射

| 门控 | 境界跨越 | 所需 downed | 关联迷你链 |
|------|----------|-------------|-----------|
| G0 | →炼精化气 | 无 | 链 1 前 |
| G1 | →人仙 | `downedBlackBear` | 黑熊后 |
| G2 | →地仙 | `downedWallOfFlesh` | 链 2 |
| G3 | →天仙 | `downedPlantBoss` | 链 4 前段 |
| G4 | →金仙 | `downedKyuubi` | **链 4 九尾** |
| G5 | →太乙 | `downedMoonLord` + 任一四僵尸 + `downedDazheng` | ML + 链 6 后 |
| G6 | →大罗 | `downedNetherDragon` OR `downedCelestialDragon` | 双轨 |
| G7 | →准圣 | `downedYinEmperor` AND `downedAzureDragon` | 终局合流 |

**Phase 2：** `MythologySidebar` 移除「一键升境」；引劫前 `RealmGateChecker.CanAdvance`。

## 附录 G — Phase 1–3 完整任务表（合并 spec §8 + audit §6）

### Phase 1 详表

| # | 任务 | 文件 |
|---|------|------|
| 1 | 删 `OnWorldLoad` 重置 | `DownedBossSystem.cs` 等 |
| 2 | `downedBlackBear` 统一 | `BlackBear.cs`、`YangJianNPC.cs` |
| 3 | §3.1 downed 回调 | 各 Boss `OnKill` |
| 4 | Checklist Archosaur | `BossChecklistIntegration.cs` |
| 5 | 11 Boss loot P0 | `Celestias/`、`Underworlds/` |
| 6 | 孟婆令牌 | `MengPoNPC.cs` |
| 7 | 劫云 XOR | `TribulationCloud*.cs` |
| 8 | `description.txt` | 根目录 |
| 9 | 棺材 bug | `RedwoodCoffinGenSystem.cs` |
| 10 | TrueRuyi/RuyiJinguBang 启用（无月碎片） | `Sticks/*.cs` |

### Phase 2 详表

| # | 任务 | 文件 |
|---|------|------|
| 9 | SB T1–52 重排 | `SoulBannerPlayer.cs` |
| 10 | 鬼面具 ML | `YingouSummon.cs` |
| 11 | RealmGate + UI | `RealmGateChecker.cs`、`MythologySidebar.cs` |
| 12 | 牛头/九尾/祖龙/ProfaneRoot | 各 Boss 目录 |
| 13 | 四海+圣兽+天将+观察者 | `Celestias/Boss/` |
| 14 | 地府 EX→阴天子 | `Underworlds/` |
| 15 | 苍龙合并 | `Qinlong.cs`、`AzureDragonHead.cs` |
| 16 | 棍/林地/玄铁/亵渎 | `Items/Weapons/`、`Armor/` |
| 17 | 奇异石+pity | `GlobalZodiacSpirits.cs` |
| 18 | RecipeGroups | `ModRecipeGroups.cs` |
| 19 | 四套盔甲 | `Items/Armor/` |
| 20 | Checklist 全量 | `BossChecklistIntegration.cs` |

### Phase 3 详表

| # | 任务 |
|---|------|
| 21 | progression.json |
| 22 | GuideBook |
| 23 | 地府 ModBiome |
| 24 | 天极镐 |
| 25 | en-US |
| 26 | Fengdu DPS |
| 27 | Profane ML 升华 |

## 附录 H — 材料经济桥接（spec §3.4 与原版）

| 材料 | 原版关联 | mod 来源 | 主要消耗 | 桥接建议 |
|------|----------|----------|----------|----------|
| 妖气碎片 | 无 | 地表 1/10；Boss | 棍链 200 总量 | 保持；Boss 月后补充 |
| 青铜/赤铜/玄铁 | 锭配方的 `Copper/Tin/Iron/Lead` | 世界生成 | 早期武器甲 | Cuprite + `HellstoneBar` |
| 奇异石 | — | 1/10000（应 1/800） | 生肖 9+3 饰 | pity 500/1200 |
| 天界碎片 | — | 天柱 1/2；入侵 | 天柱武器 10–15/件 | ML 后 |
| 残魂 | — | 地府 1/2；入侵 ~325/次 | Revenant 6–8/件 | ML 孟婆引导 |
| 青龙之灵 | — | 无掉落（待祖龙/Qinlong） | 玄铁甲、逆鳞、圣兽 | P2 定向 |
| 龙王鳞 | — | 敖广 100%（余龙空） | 龙珠、四象碑 | P0 四海 |
| 尸块 | — | 尸骸 23–30 | 枉骸/EX/Fengdu | ✓ |
| 无常之魂 | — | 未建 | EX、酆都甲 | P2 |

**棍链总需求（调整后）：** 约 200 妖气 ≈ 80 地表怪 + 2–3 Boss；与 Tier A–D 原版材料叠加后，玩家应在 **Plantera 前** 完成金箍棒前置，**不依赖** ML 碎片。

## 附录 I — 入侵事件与原版节奏

| 事件 | 触发材料（原版部分） | 产出 | 与 Boss 关系 |
|------|---------------------|------|-------------|
| 天庭入侵 | 月锭 + 天界碎片（合成） | 碎片爆发 20–30 首次 | 神威/百目 入侵掉落 5% |
| 地府入侵 | 月锭 + 残魂 + 骨 | ~325 残魂/20 波 | ML 后开门；非 HM |
| 鼠符咒事件 | 圣主雕像 | 独立 | 与脊柱平行 |

**节奏目标：** 每条 endgame 线 3–4 次入侵 + 2–3 场关键 Boss 可换 1 件同 tier 武器；原版 **南瓜月/霜月** 仅作可选加速（§3.1 Tier E）。

## 附录 J — 文件路径速查（迭代涉及）

| 系统 | 路径 |
|------|------|
| Downed 标记 | `Systems/DownedBossSystem.cs` |
| 万魂幡 | `Items/Weapons/SoulBanners/SoulBannerPlayer.cs` |
| 棺材生成 | `RedwoodCoffins/RedwoodCoffinGenSystem.cs` |
| 鬼面具 | `Items/YingouSummon.cs` |
| 棍链 | `Items/Weapons/Sticks/*.cs` |
| 符咒 | `Items/Weapons/Charms.cs` |
| 敖广掉落 | `Celestias/Boss/AoGuangs/AoGuang.cs` |
| 黑熊掉落 | `NPCs/Boss/BlackBear/BlackBear.cs` |
| 觉醒冥龙 | `Underworlds/Boss/AwakeningNethers/AwakeningNetherHead.cs` |
| 九尾 | `NPCs/Boss/KyuubiKitsunes/KyuubiKitsune.cs` |
| 孟婆 | `NPCs/TownNPCs/MengPoNPC.cs` |
| 土地公 | `NPCs/TownNPCs/TuDiNPC.cs` |

## 附录 K — 术语与缩写

| 术语 | 含义 |
|------|------|
| Primordial / 洪荒 | 模组展示名 |
| SB / 万魂幡 | Soul Banner，击败 Boss 提升吸魂上限 |
| ML / 月后 | Moon Lord 后，`downedMoonlord` |
| HM / 困难 | Wall of Flesh 后 |
| PL / 世纪之花 | Plantera |
| 四僵尸 | 旱魃、后卿、赢勾、将臣；**月后** T24–27 |
| 天柱 | Post-ML 天界柱体与 `EmpyriteOre` |
| 入侵 | 天庭/地府 20 波事件 |
| 定向精魄 | Boss 必掉对应生肖 `Zodiac*` |
| silo | 纯 mod 材料、无原版副材的配方链 |
| Phase 1/2/3 | 阻断 / 闭环 / 打磨 实施波次 |

## 附录 C — 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| V2.0 | 2026-05-28 | 任务 1/3 正确性 + 2/3 遗漏 + 3/3 原版桥接合并；§4 裁定；§5–§6 文档与 Phase |
| V2.0.1 | 2026-05-28 | 同步修正 SPEC §1.5、AUDIT 数值；附录 A–K |

---

*Primordial / 洪荒 · Progression Design Iteration V2 · 实施前请先完成 Phase 1 Top 5。*
