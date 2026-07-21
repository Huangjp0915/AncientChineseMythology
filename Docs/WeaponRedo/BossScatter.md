# 主世界 Boss 散件武器（BossScatter）重做设计文档

> 管辖：`Items/Weapons/Bosses/` 下四件 Boss 掉落武器 —— 旱日（HanbaBook）、劫骇（HoqingFireSummon）、雷鸣锤（JiangcenHammerItem）、鬼牙（YingouKnife）。
> 四件分属"四大僵尸始祖"（旱魃 / 后卿 / 将臣 / 赢勾）的战利品，全部 Red 稀有度、月后进度位。
> 纪律：Boss 本体正被并行重做，本线**只读** Boss 文档与配色，不写入任何 Boss 文件夹文件、不引用 Boss 名下 .fx；所需着色器以武器名前缀自建。

## 1. 现状诊断（八透镜逐件）

### 旱日 HanbaBook（魔法，145 / 32f / 22 魔力）
1. 一眼身份：**弱**。点击后以玩家为中心炸开一圈无差别 nova，与光标/弹速完全无关——"数值光环"而非"大旱降临"。
2. 三段感：无。举手即放，无前摇收招。
3. 命中反馈：仅有后补的 Scorch burst，无震屏无音高分层。
4. 轨迹：冲击波贴图 + 每帧 12 个 Torch dust 乱撒，无轨迹语言。
5. 机制深度：零决策；`DmgSoumd` 衰减字典按 `npc.type` 键控（同类怪共享衰减）且首跳即 ×0.96，属于坏味道代码。
6. 演出峰值：无大招。
7. 一致性：引用 Boss 文件夹贴图 `Hanbas/Shockwave`（并行冲突风险）；焦土主题只剩配色。
8. 性能：penetrate -1 + 10f 命中冷却的 480px 大圈实为隐形高 DPS 光环，判定与视觉严重不对齐。

### 劫骇 HoqingFireSummon（召唤，136 / 6 灯一次召出）
1. 一眼身份：环绕鬼火有辨识度，但**不是真召唤物**：无 buff、不占栏、timeLeft 永续刷新。
2. **致命 MP bug**：灯体 `hostile=true, friendly=false`——绕着玩家转的"己方召唤物"会伤害所有玩家；发射的 `GhostFireProj`（Boss 类）靠 spawn 后 `friendly=true` 补丁，该标志不入同步包，其他客户端上它仍是 hostile 弹。
3. 依赖 Boss 代理名下类 `GhostFireProj`，行为随 Boss 重做漂移。
4. 命中反馈：Shadow burst 分支实际难触发（灯为 hostile 不走 friendly OnHit）。
5. 机制深度：右键召回有雏形；无大招。
6. 演出峰值：无。
7. 一致性：LimeGreen/Cyan 与后卿 V3"鬼绿/腐橙"语言接近但无腐橙致命层。
8. 性能：尚可；DissolveBurn 召回演出是好底子，保留。

### 雷鸣锤 JiangcenHammerItem（近战，680 / 22f）
1. 一眼身份：普通回旋镖，"雷鸣"只剩紫色 dust——与将臣 V3"雷青+军金"语言完全脱节。
2. 三段感：无前摇，出手即匀速直线（extraUpdates 3 = 100px/f 持续），无顿挫无硬刹。
3. 命中反馈：**灾难**——每次命中 240 个 dust（scale 高达 31.8）+ 震屏 10（预算的 5 倍），噪声淹没战场。
4. 轨迹：手抄残影不随速度门控。
5. 机制深度：零决策，无限并发投掷。
6. 演出峰值：无。
7. 一致性：紫色雷电与 Boss 文档配色（雷青 180,230,255 / 军金 / 尸暗红）不符。
8. 性能：extraUpdates 3 + localNPCHitCooldown 30 对大目标产生失控多段；每命中 240 dust 是性能红线。

### 鬼牙 YingouKnife（近战，342 / 12f）
1. 一眼身份：**有好点子**（光标处布线、双刃对冲交叉，即 Boss SaberHell 的玩家化）但被埋没：40f 无预警渐变、无音效、刃从屏外 1000px 飞入不可读。
2. 三段感：挥砍匀速；`UseStyle` 里 itemLocation 锚到躯干中心是无意义 hack。
3. 命中反馈：仅 SaberKiller（Boss 类）内置分支。
4. 轨迹：无拖尾语言。
5. 机制深度：每 12f 挥砍免费自动布线，无冷却无决策=纯噪声叠加。
6. 演出峰值：无交叉高潮演出。
7. **MP bug + 越界依赖**：`SaberKiller` 为 hostile 弹 + spawn 后 friendly 补丁（同劫骇问题）；且该类归 Boss 代理管。
8. 性能：无限布线可堆爆弹幕位。

**力度判定：四件全部全面重做**（机制身份、弹幕自持化、演出、MP 安全）。所有 public/internal 既有类名保留（`HanbaBookProj` / `HoqingFireSummonProj` / `JiangcenHammerProj` / `SaberHellFriendly`），掉落、稀有度、职业、进度位不变。

## 2. 系列主题与幻想感

**"降尸者的战利品"——你击败了一位尸祖，就把他最招牌的一招握在手里。**

每件武器 = 对应 Boss 签名招式的玩家化直译，配色与 Boss V3 文档同源：

| 武器 | Boss 招式原型 | 玩家化翻译 | 配色语言 |
|---|---|---|---|
| 旱日 | 旱魃"烈日灼柱 + 焚天坠日 + 焦痕延燃" | 点名式日炷轰击；每第 4 次施法引坠小焦日掀双向火波 | 焦橙 Flame + 烈金 + 灰烬深红（Scorch=31） |
| 劫骇 | 后卿"幽火仪仗 + 鬼门开" | 仪仗幽灯环绕吐魂焰；右键开小鬼门倾泻万鬼 | 鬼绿（持续）+ 腐橙（爆发）+ 幽紫（GhostGreen=12） |
| 雷鸣锤 | 将臣"点将砸 + 雷狱落雷 + 落地山崩" | 听令回旋锤，顶点/命中引落天雷；第 5 掷点将召虚影锤连轰 | 雷青 Lightning + 军金 Gold（Gold=22 / Shadow=33） |
| 鬼牙 | 赢勾"SaberHell 刀阵 + 居合斩线" | 三段连斩接刀气；右键布冷光斩线双刃对冲，第 3 线展开米字刀阵 | 冷青白刃芯 + 幽紫外缘，红只给爆发帧（Soul=28 / Fatal=24） |

## 3. 逐件机制设计

### 3.1 旱日（魔法书）
- **左键·灼日之炷**：在光标处（限 1100px）降下竖直日炷。波形：预警 20f（细橙聚焦线 + 地面光点收拢 dust，末 6f 掺红）→ 爆发 10f（柱宽 poly-snap 3→42 半宽，白热芯 + 焦橙边，判定开，命中上 OnFire3）→ 收束 22f（宽度 ×0.88/f 指数衰减）。若柱脚下方 ≤15 tile 有实地，地表燃起**焦土阴燃带**（阴燃 30f 后延燃，tick 30% × 4 次左右——旱魃"焦痕 30f 后延燃"语言的直译）。
- **大招·坠日**（每第 4 次施法）：光标上方 560px 凝聚小焦日 40f（汇聚流线密度 ∝√t、72% 处硬切静默、末 6f 预坍缩收缩+闪烁——照抄 Boss 蓄力语法）→ 锁 X 直线坠落（4→34px/f，t² 加速）→ 触地爆炸：230px 圆判定 260%、冲击环 + 径向辉光 + 震屏 6、左右双向**燎原火波**（各 55%，8.5px/f 贴地行进 70f，可跳越）。
- 决策点：日炷是即时点名（打空中/精确），坠日要预判走位（延迟高伤 + 地面波只对地面目标增益）；焦土带奖励"把怪拉过火线"的路径规划。

### 3.2 劫骇（召唤杖）
- **左键**：召唤 1 只**仪仗幽灯**（标准 minion：buff + 1 栏 + 可再召），灯群绕玩家等分圆环列队（半径 96 呼吸 + 灵异漂移），有目标时逐灯错拍（52f±）执行"后拉 10f 前摇 → 吐魂焰弹 + 后坐 8px"。魂焰弹：16px/f、弱追踪（10f 后 lerp 0.035）、命中挂毒液（瘟疫主题）+ GhostGreen burst。
- **右键·鬼门开**（大招，需 ≥1 灯且场上无门）：所有灯飞赴光标处，列成竖椭圆**小鬼门**（开门 36f → 倾泻 ~110f：按灯数 ×3 发魂焰从门内扇状喷出 → 合拢 40f 灯归位）。开门 3 帧走全屏名额打一次 rift 微扭曲定调；倾泻期灯停普攻（大招是节奏高点而非 DPS 白赚）。
- 决策点：灯数=召唤栏投资；鬼门是"集火窗口"——把灯从贴身护卫态切换成阵地炮台态，位置选择即决策。
- 修复：全链路 friendly/Summon/owner 端生成；不再引用 Boss 类。

### 3.3 雷鸣锤（近战投掷）
- **左键·掷锤**（同屏至多 2 柄）：
  1. **前摇 12f**：锤在头顶举起后仰（pow(4) 后拉曲线），末 4f 微抖 + 金辉渐亮——重量从这里来；
  2. **爆发**：瞬时 set 23px/f（extraUpdates 1 → 46 有效），直线飞行，速度门控拖尾/残影全开；
  3. **顶点"听令"顿 10f**：×0.72/f 硬刹悬停自旋减速 + 顶点**天罚落雷**一道（40%，竖直雷柱 14f 预警细线 → 6f 落雷）；
  4. **回收**：加速弧线追手，接触消失。
- **命中**：≤14 dust（雷青+军金）+ 震屏 2 + Gold burst；每次命中在目标头顶补一道落雷（每掷至多 3 道，防群怪爆量）。
- **大招·点将令**（每第 5 掷）：金锤（拖尾转军金），首个命中目标上空召 3 柄虚影锤每 10f 依次轰落（各 60%，各带小冲击环 + 震屏 3）——"点将砸"的玩家化。
- 决策点：顶点顿拍让"投掷距离"变成瞄准参数（顶点落雷打谁）；点将锤希望怼进人堆。

### 3.4 鬼牙（近战刀）
- **左键·三段连斩**：斩1/斩2 普通（12f），斩3 略重（15f）并挥出一道**鬼牙刀气**（90%，短程 ~380px 弧形刃波）；挥砍弧内冷青/幽紫火花（节流）。本体命中：小 Soul burst + 震屏 1。
- **右键·冥刃斩线**（34f 使用期，该次不带近战判定）：光标处布一条垂直于视线的**冷光斩线**（±480px）：预警 24f（专属着色器画"细冷青线 → 白 → 末 6f 掺红"+ 两拍定音）→ 两端各出一柄**冥刃**相向 84px/f 对冲 → 交叉帧：十字冷光爆闪 + Fatal burst + 震屏 3 + 交叉点 240px 内一次 150% **处决斩**。
- **大招·米字刀阵**（每第 3 条斩线）：以光标为心同时布 3 条线（0°/60°/120°，各延迟 0/8/16f 错拍对冲）——Boss BladeMatrix 的玩家化小抄，收束节拍可逐线阅读。
- 决策点：左键贴脸循环 vs 右键远程布线（有 34f 出手成本）；处决斩要求把线布在怪堆中心而非随手甩。

## 4. 系列内梯度

四件为平行进度位（月后四尸），梯度按"演出复杂度=Boss 战利品叙事浓度"排布：

- **朴素层**：雷鸣锤、劫骇——全部复用共享原语（BeamGrad 雷柱 / LightningBranch 电弧贴图 / ribbon / 冲击环 / DissolveBurn），无专属 shader。
- **豪华层**：旱日（专属日轮着色器 `HanbaBookSunFlare`，坠日大招全屏辉光）、鬼牙（专属斩线着色器 `YingouKnifeArc`，一张 quad 内完成预警→爆发→残光全波形）。
- 论证：日轮（白热核+熔面+日冕光舌）与"完整生命周期的冷光斩线"都无法用现有贴图/原语拼出；雷柱与鬼火用 BeamGrad + 既有贴图已可达标，不为堆而堆。

## 5. 视觉技术方案

| 组件 | 方案 |
|---|---|
| 旱日·日炷/雷鸣锤·落雷 | `ACMShaders.DrawBeam`（BeamGrad）细预警线→粗柱两态 |
| 旱日·日轮 | **新建 `Effects/HanbaBookSunFlare.fx`（ps_3_0）**：极坐标熔面噪声 + pow 白热核 + 正弦×噪声日冕光舌 + uCollapse 预坍缩；Immediate+Additive 喂 SoftGlow quad |
| 鬼牙·斩线 | **新建 `Effects/YingouKnifeArc.fx`（ps_3_0）**：uProgress 单参驱动三段波形（细预警线/poly-snap 爆宽/噪声撕裂残光），uWarn 控终段红量；BuildRibbonStrip 两点直带图元 |
| 鬼牙·交叉闪 | ACMAsset.SlashBurst 双张 ±45° 加性拉伸 + ACMWeaponBurst.Fatal |
| 劫骇·灯/门 | GhostFire 贴图 4 帧 + WeaponVFX.ApplyDissolveBurn 显形/崩解；门=DrawRibbonTrail 椭圆弧双层 + DrawBeam 中缝 + 开门帧 GenericWarp(rift) 走全屏名额 ≤0.3 强度 3 帧 |
| 拖尾 | 全部 WeaponVFX.DrawRibbonTrail/DrawProjectileTrail 双层（受 Trail 配置降级），速度门控 |
| 命中反馈栈 | ACMWeaponBurst（Scorch/GhostGreen/Gold/Shadow/Soul/Fatal）+ AddScreenShake 预算内 + SoundID 双层（低频体+高频质感，Pitch ±0.1~0.35） |

## 6. 平衡与定位（获取途径/稀有度/职业不变）

- **旱日**：旧实测为"480px 半径每 10f 一跳的隐形光环"（大目标理论 >800 DPS 且无操作）。新：单体循环 = 日炷 145 + 焦土 ~0.3×4 + 坠日均摊（260%+55%×2/4 发）≈ 名义 330~360 DPS，但改为需瞄准的点名判定——有效 DPS 与旧持平（±10%），操作上限提高。伤害/魔耗/攻速数值全部不动。
- **劫骇**：旧 6 灯固定、不占召唤栏（可与其它 minion 全额叠加=隐形超模）且平均 85f/发（全命中 ≈576 DPS）。新：每灯 1 栏、140 伤害（+3%）、52f/发 → 每灯 ≈162 DPS；6 栏投入 ≈970，但挤占其它 minion 栏位后**组合总量与旧持平略降**；鬼门为瞬发窗口非增益（喷完普攻停摆）。
- **雷鸣锤**：680 不动。旧 extraUpdates 3 + 30 冷却（=7.5f 实际）对大目标失控多段；新 extraUpdates 1 + 24 冷却（=12f 实际）+ 一去一回一顿的固定节奏，单体理论 ≈1300~1700 DPS + 落雷 40%（帽 3 道/掷）——名义在 ±15% 带内，尾部极端多段被砍属修复而非 nerf。
- **鬼牙**：342/12f 本体 1710 DPS 不动。旧"每挥自动布线"理论 +3420 DPS 但 40f 延迟固定线大多打空且 MP 下是 hostile 弹（实际不可用）。新：斩3 刀气 +90%/连段、右键线 2×100%+150% 处决/34f+冷却节奏 → 全命中理论 ≈2900 DPS，实战与旧"能用部分"持平，可控性大增。

## 7. 性能与多人预算

- **生成纪律**：所有弹幕生成走 `Shoot`（owner 天然）或 `Projectile.owner == Main.myPlayer` / `IsOwnedByLocalPlayer` 判定；全链 friendly，杜绝 hostile 补丁。minion 走 buff + `originalDamage` 标准模式。
- **状态同步**：状态一律 `ai[]` / 确定性计时推进；武器连段/计数用 ModItem 私有字段（仅 owner 端 Shoot 消费，基准 TidecallersDecree 同模式）。
- **dust 上限**：单命中 ≤14；持续弹幕每帧 ≤2（节流 %2~3）；爆点一次 ≤20。
- **震屏预算**：普通命中 ≤2 / 落雷·交叉 3 / 虚影锤 3 / 坠日·点将终锤 6~8，全走 WeaponVFX（取 max 不累加）。
- **全屏后处理**：仅坠日爆炸径向辉光（burst 内部自管名额）与鬼门开门 3 帧 rift（RequestFullscreenSlot，强度 0.3）；名额被占自动退化。
- **弹幕上限**：锤同屏 ≤2、斩线控制器天然受 34f 使用期限流、落雷每掷 ≤3+1、灯受召唤栏管制。
- **着色器**：静态缓存（WeaponVFX.GetEffect），绝不每帧 Request；拖尾受 `MythologyConfig.Trail` 降级。

## 8. 实施清单

1. `Effects/HanbaBookSunFlare.fx`（新，ps_3_0）+ `Effects/YingouKnifeArc.fx`（新，ps_3_0），按名编译过。
2. `Items/Weapons/Bosses/HanbaBook.cs`：重写 `HanbaBookProj`（日炷，类名保留）；新增 `HanbaBookScorchBrand` / `HanbaBookFallingSun` / `HanbaBookFireWave`。
3. `Items/Weapons/Bosses/HoqingFireSummon.cs`：`HoqingFireSummonProj` 改标准 minion（类名保留，基类改 ModProjectile）；新增 `HoqingFireSummonBuff` / `HoqingFireSummonBolt` / `HoqingFireSummonGate`；删除对 `GhostFireProj` 的引用。
4. `Items/Weapons/Bosses/JiangcenHammerItem.cs`：重写 `JiangcenHammerProj` 四段状态机；新增 `JiangcenHammerSkyBolt` / `JiangcenHammerEchoStrike`；dust/震屏回预算。
5. `Items/Weapons/Bosses/YingouKnife.cs`：重写 `SaberHellFriendly` 为斩线控制器（类名保留）；新增 `YingouKnifePhantomBlade` / `YingouKnifeBladeWave`；删除对 `SaberKiller` 的引用。
6. ReadLints 四文件清零；两个 hjson 补条目（四件 Tooltip、新弹幕 DisplayName、minion buff），zh/en 同步，最后一步执行并复读验证。
