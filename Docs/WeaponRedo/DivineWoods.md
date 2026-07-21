# 神木系列（DivineWoods）重做设计文档

> 管辖：`Items/Weapons/DivineWoods/` 七件 + 新建 `Effects/DivineWood*.fx`。
> 定位：建木/扶桑级神树幻想，介于低阶 Woodlands（朴素木系）与高阶 ArrogantDivineSylvans（金翠双色、更豪华）之间。

## 1. 现状诊断（八透镜逐件）

**系列共性**：视觉地基已半接入（全员 WeaponVFX 拖尾 + ACMWeaponBurst.DivineWood 命中反馈），但——

- 机制身份全线趋同：7 件里 6 件的核心语言都是"命中→生成减速后追踪最近敌人的小弹幕"（WhirlLeaf / VerdictPetal / VineBurstLeaf / TomeLeaf / TomePetal / VineShard / SpiralLeaf 是同一个模板的七次复制）；
- 全系列零蓄力、零右键、零资源循环——没有任何主动决策点；
- Debuff 全是原版 Poisoned/Venom，Purple 稀有度的神树神器挂初期毒，幻想感崩塌；
- 演出峰值：仅旋叶有"每 5 hit 裁决"，其余无大招时刻；旗舰无专属着色器。

逐件：

1. **DivineWoodBomb 种子弹**：平抛手雷+8 追踪碎片。三段感无；身份与火铳迫击炮重叠；"种子"没有"生长"。
2. **DivineWoodGreatblade 巨刃**：已是 held proj 挥砍（20/55/25 三段），但 Prepare 完全静止无反向拉刀，Execute 用对称 SmoothStep 无 snap；刀波每挥必发无节奏；无大招。骨架最好，适合升旗舰。
3. **DivineWoodGyratingLeaf 旋叶**：掷出→回程 1.5×，5 hit 裁决。机制最完整，但命中刷追踪叶噪声大，裁决演出弱。
4. **DivineWoodLongbow 长弓**：注释宣称"蓄力弓"，实现完全没有蓄力——文案与代码脱节；即射 1 箭+2 螺旋叶，无手感。
5. **DivineWoodMusket 火铳**：useTime=3 实为每秒 20 发机枪，与"三连发"文案不符；`Item.crit` 赋值两次（12 后被 10 覆盖）为既有 bug；无后坐、无枪口演出。
6. **DivineWoodScepter 灵杖**：设了 `channel=true` 但从未消费；"链鞭"实为慢速直线飞行头+正弦摆动 ribbon，毫无抽打感。
7. **DivineWoodTome 典籍**：每次 8~12 叶散射纯刷屏，无节奏无峰值。

**结论**：视觉可保留强化，机制需全面重做。性能上追踪扫描（遍历 maxNPCs）与粒子量可接受，但需给系列新增系统设上限。

## 2. 系列主题与幻想感

**"神木不是木头，是会生长的神。"** 建木通天、扶桑浴日：玩家的每一次攻击都是在敌人体内**播种**，而神木的愤怒在于**收割时的绽放**。

系列贯穿机制语言——**生根 → 绽放** 二段循环：

- **生根（Rooted）**：系列武器的"播种型"攻击命中时在敌人体内种下根须（上限 5 层，持续 4 秒刷新）；生根期间固定 24/s 持续伤害（DoT 走 vanilla 同步的 Buff、不随层叠加，规避 MP 同步复杂度——层数专职决定绽放规模）+ 敌脚下根须粒子提示；
- **绽放（Bloom）**：系列武器的"收割型"攻击命中带层敌人时，消耗全部层数引爆**年轮法阵绽放**——翠玉年轮从敌人脚下生长绽开（专属着色器），AoE 伤害与半径随层数增长。

七件武器各自持有不同的"种"与"收"手段，单件自洽、混用成流派。配色沿用 `ACMWeaponBurst.DivineWood(25)`（深翠 30,150,75 → 亮芯 195,255,155），点缀年轮金绿。

## 3. 逐件机制设计

### 共享系统（实现于 DivineWoodGreatblade.cs 顶部，全系列消费）

- `DivineWoodRootedNPC : GlobalNPC`（InstancePerEntity）：RootStacks/RootTimer，UpdateLifeRegen 扣 16×层（=8dps/层），DrawEffects 根须粒子；层数为**各客户端本地**记账（命中与引爆都发生在 owner 端，绽放伤害以同步弹幕输出）——多人下各玩家独立种收，无需自定义网络包。
- `DivineWoodRootedBuff : ModBuff`：仅视觉图标（复用原版 Buff_20 贴图），时长镜像 RootTimer。
- `DivineWoodRoot.AddStack(npc, n)` / `TriggerBloom(source, npc, baseDamage, owner)`：owner 端 API；绽放生成 `DivineWoodBloomBurst` 弹幕（Generic 伤害，生成时按引爆武器面板算好：`baseDamage × (0.35 + 0.18×层)`，半径 110+26×层）。同帧绽放生成 ≤3，年轮法阵 decal 同帧绘制 ≤2（超出退化为冲击环）。

### 3.1 DivineWoodGreatblade 神木巨刃（近战·旗舰）

- **左键三连段**（2 秒不挥重置）：
  - 段1 横斩 / 段2 逆斩（各 20t）：anticipation 18%（反向拉刀 -0.35rad，pow3 后吸）→ strike 32%（poly(9) ease-out snap）→ recovery（quintic 回摆过冲 6%）；每 hit 挂 1 层生根；
  - 段3 **年轮重斩**（34t）：anticipation 45%（拉刀 -0.9rad + 末段微抖）→ strike 14%（poly(20)）→ 长收招；伤害 ×1.6，命中**引爆生根**，落点开年轮法阵 + 冲击环 + 震屏 5；只有段3 放刀波（×1.5 伤害，更宽）。
  - 持刀刀身常态叠 **DivineWoodSapFlow** 翠脉流光（低强度），strike 段增亮。
- **右键大招·建木擎天**（冷却 9 秒，蓄力 46 帧）：举刀蓄力——地面年轮法阵随蓄力生长（GrowthRing uGrow 0→1）+ 汇聚粒子（密度∝√charge，72% 后寂静一拍）→ 释放：面前 120/260/400px 依次拔起三根建木巨柱（BeamGrad 竖光柱 + 顶冠绽放 + 冲击环，震屏 4/5/6），每柱 ×2.2 伤害并引爆生根；释放帧染屏 0.12 强度 12 帧（走全屏名额）。
- 决策点：连段管理（段3 时机对准生根目标）＋大招引爆窗口。

### 3.2 DivineWoodTome 神木典籍（魔法·副旗舰）

- **左键三连诵**：第 1/2 次施法 5 片叶刃（收窄 24° 扇，螺旋→追踪保留，挂 1 层生根/hit）；第 3 次自动**华盖倾泻**：7 叶收束 + 中心一朵穿透莲华弹（大体积穿透，命中**引爆生根**），音高逐诵上升。
- **右键·催花**（耗魔 40，冷却 5 秒）：以鼠标为心 420px 年轮法阵闪放，**引爆域内所有生根敌人**（各自绽放），无目标时仅轻演出。法阵用 GrowthRing 快速生长-消散。
- 决策点：左键攒层数节奏 × 右键区域收割时机。

### 3.3 DivineWoodScepter 神木灵杖（魔法·藤鞭）

- 重做为真**抽鞭**：每次使用甩出一条贝塞尔藤鞭（held proj 生命周期=use 动画）：anticipation 25%（鞭身拖后收拢）→ strike 25%（鞭梢 poly(10) 甩至鼠标点，clamp 360px）→ hold 微颤 → 收回；鞭梢命中挂 **2 层**生根，鞭身命中挂 1 层。
- **第 4 鞭自动重鞭**：伤害 ×1.45、鞭梢**引爆生根**、落点留 2 秒荆棘地（小 AoE 挂层），鞭速更狠、音爆分层（低频 whoosh + 高频脆响）。
- 决策点：数鞭节奏，把重鞭鞭梢精确点在高层数目标上（鞭梢与鞭身伤害/层数不同）。

### 3.4 DivineWoodLongbow 神木长弓（远程·蓄力弓）

- 改为真**蓄力弓**（channel held proj，消耗箭矢）：
  - 档1（<40%）：松开发快速叶矢（×0.75，挂 1 层）；
  - 档2（40~95%）：三箭平行齐射（各 ×0.9，各挂 1 层）;
  - 档3（满蓄）：**贯林矢**——巨型穿透箭 ×2.4，沿途荆棘拖尾，命中每个敌人都**引爆生根**；满蓄瞬间寂静一拍 + 弓心光收缩，释放帧径向辉光 + 震屏 3 + 玩家后坐 2px。
- 蓄力演出：收束粒子（比例吸入）+ 弓身弯曲幅度随 charge、拉满后弓臂微抖。
- 决策点：三档换挡——快速压制 vs 满蓄收割。

### 3.5 DivineWoodMusket 神木火铳（远程·点射枪）

- **真三连发**：useTime 8 / useAnimation 24 / reuseDelay 14 —— 每次按压三发点射后强制喘息；每发后坐（枪口上跳 + 玩家微推 0.55px）、枪口闪光、荆棘针挂 1 层生根、三发音高阶梯上升。
- **第 9 发循环强化**：每第 9 发（第三轮点射末发）替换为**荆棘迫击炮**：抛物线种子落地爆出一排 5 根根须尖刺（自内向外依次窜出、地裂 telegraph，最外侧双刺引爆生根），震屏 4。
- 决策点：三连发节奏管理 + 迫击炮落点预判（收割窗口）。修复 crit 双赋值 bug。

### 3.6 DivineWoodBomb 神木种子弹（远程·投掷）

- 种子雷三形态（体现"种子会生长"）：
  - **落地→扎根**：钻入地面成根须雷区，3 波根须尖刺从地下依次窜出（波间 20 帧、每波前 8 帧地裂 telegraph），前两波挂 1 层生根，**第 3 波引爆生根**；
  - **命中敌人→寄生**：种子挂附敌身 45 帧，呼吸膨胀后**开花**：直接挂 3 层再立即引爆（单体处决向）；
  - **空中超时**：原地绽放爆炸（保留现有多层爆发，挂 2 层）。
- 决策点：打地板铺场 vs 直击寄生单体。

### 3.7 DivineWoodGyratingLeaf 神木旋叶（近战·回旋镖）

- 保留掷出/回程骨架，新增**驻旋**：掷出后按住左键，旋叶到最远点驻场化作**年轮锯**（跟随鼠标缓移，持续 ≤90 帧，连续锯击每 hit 挂 1 层，脚下小年轮法阵随转）；松开→回程 ×1.5 并**引爆沿途生根**。
- 5 hit 裁决保留，演出升级：花瓣环 + 年轮法阵闪现 + 音高上抬。
- 决策点：驻旋位置控制（把锯钉在敌群里攒层）→ 松手收割一条线。

## 4. 系列内梯度

- 朴素层（共享原语即可）：Bomb、Musket、Longbow、GyratingLeaf——拖尾/柔光/冲击环 + 小型 GrowthRing 消费；
- 进阶层：Scepter（贝塞尔鞭全程 ribbon 演出）、Tome（右键区域法阵）；
- **旗舰 Greatblade**：2 个专属着色器（SapFlow 常态流光 + GrowthRing 大招全尺寸年轮法阵）+ 三巨柱大招 + 唯一染屏时刻。
- 对下（Woodlands）：他们无系列机制语言、无专属 shader；对上（ArrogantDivineSylvans）：金翠双色更豪华，本系列刻意只用翠绿单色系留出头部空间。

## 5. 视觉技术方案

**复用**：WeaponVFX.DrawRibbonTrail / DrawProjectileTrail / DrawShockwaveRing / DrawGlowBurst / DrawRadialBloom / ApplyPaletteTint（仅大招 12 帧）；ACMShaders.DrawBeam（巨柱）；ACMWeaponBurst.DivineWood 命中栈；ACMAsset 遮罩；ACMShaders.NoiseTexture / WorldDecalParams。

**新建（系列前缀，均 ps_3_0）**：

1. `DivineWoodGrowthRing.fx` —— 年轮法阵（屏幕空间 decal，喂共享噪声）：同心年轮环 + 角向枝纹 + 生长参数 uGrow（法阵从心向外"长"出来）+ 生长沿亮边。消费点：绽放爆炸、段3 落点、大招地阵、旋叶驻锯、典籍右键。同帧绘制 ≤2 预算。
2. `DivineWoodSapFlow.fx` —— 翠脉流光（贴图空间，s0=武器贴图 s1=噪声）：沿贴图流动的树液光脉 + 边缘翠光 rim。消费点：旗舰刀身常态/蓄力增亮（唯一日常 shader 展示位）。

## 6. 平衡与定位

获取途径（12×Livinglog@秘银砧）、稀有度 Purple、职业线全部不变。原版 Poisoned/Venom 替换为系列生根机制：固定 24/s DoT（略低于原 Poisoned+Venom 合计 ~42dps），差额由绽放 AoE（0.35+0.18×层 倍率）补齐并反超为主动决策收益。

DPS 论证（单目标持续输出估算，60fps）：

| 武器 | 原 DPS 估算 | 新 DPS 估算（含生根/绽放摊销） | 变化 |
|---|---|---|---|
| Greatblade | 190/22t + 刀波228/22t ≈ 1140 | 三段(170+170+272)/74t≈497 + 段3刀波255/74t≈207 + 绽放摊销≈310 + 大招摊销≈120 ≈ 1134 | ≈0% |
| Tome | 165×10×命中率0.55/24t ≈ 415(实战弹幕浪费大) | (5+5+7叶×0.6命中+莲华165)/72t ≈ 388 + 绽放右键摊销 ≈ 60 → 448 | +8% |
| Scepter | 155/26t≈358 + 追踪叶≈90 → 448 | 鞭(155×3+225)/104t≈400 + 绽放摊销 ≈75 → 475 | +6% |
| Longbow | (155+77×2)/18t≈1030(三弹全中理论值,实战≈600) | 档2三箭418/加权 ≈ 560 + 满蓄爆发窗口 | ≈-7%(实战) |
| Musket | 140×20/s=2800(每秒20发,明显超模) | 3发×140/(24+14)t≈663 + 迫击炮摊销≈180 → 843 | -70%(修正超模,回归三连发文案定位) |
| Bomb | (200+8×66)/26t≈~1300(理论) | 扎根3波(120×2+200)/铺场 ≈ 900 + 寄生单体爆发 | -15%(理论峰值下修,可靠性上升) |
| GyratingLeaf | 175×2程+旋叶刷屏 ≈ 700 | 驻锯连击 175×0.35/8t≈460 + 回程263 + 绽放 ≈ 720 | +3% |

Musket 为修 bug 性质的下修（原 useTime=3 是实现失误，且与文案不符）；Bomb 理论峰值下修但命中可靠性大升。其余 ±10% 内。

## 7. 性能与多人预算

- 生根层数各客户端本地记账，绽放伤害走同步弹幕；所有 NewProjectile 都在 owner 端（OnHitNPC/Shoot 天然 owner；主动生成判 `Projectile.owner == Main.myPlayer`）。
- 绽放生成同帧 ≤3（静态帧计数）；GrowthRing decal 同帧 ≤2；根须提示粒子每 NPC 每 12 帧 ≤2 dust。
- 着色器全部静态缓存（WeaponVFX.GetEffect）；拖尾受 MythologyConfig.Trail 降级；染屏仅大招释放 12 帧、强度 0.12、走全屏名额契约。
- 震屏预算：小命中 ≤2 / 迫击炮·根须波 3-4 / 段3·满蓄 3-5 / 大招三柱 4/5/6。

## 8. 实施清单

1. 新建 `Effects/DivineWoodGrowthRing.fx`、`Effects/DivineWoodSapFlow.fx`，按名编译过；
2. `DivineWoodGreatblade.cs`：生根共享系统（GlobalNPC/Buff/API/BloomBurst）+ 三连段 + 右键大招（PillarRite/Pillar 弹幕）+ SapFlow 刀身；
3. `DivineWoodTome.cs`：三连诵 + 莲华 + 右键催花；
4. `DivineWoodScepter.cs`：贝塞尔藤鞭 + 第 4 鞭重击；
5. `DivineWoodLongbow.cs`：蓄力弓 held proj 三档 + 贯林矢；
6. `DivineWoodMusket.cs`：真三连发 + 荆棘迫击炮循环 + 修 crit bug；
7. `DivineWoodBomb.cs`：扎根/寄生/空爆三形态；
8. `DivineWoodGyratingLeaf.cs`：驻旋年轮锯 + 回程引爆；
9. ReadLints 全部改动文件清零；
10. 最后同步两个 hjson 键区（新弹幕/ Buff 名尽量代码内 GetOrRegister，减少 hjson 面积）。
