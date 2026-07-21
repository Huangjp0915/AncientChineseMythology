# 林地系列（Woodlands）重做设计文档

> 范围：`Items/Weapons/Woodlands/` 7 件基础武器 + `Upgrades/` 7 件材料升级（Cuprite 赤铜 ×5、XuanTie 玄铁 ×2）。
> 定位：游戏前期（血肉墙前 / 升级版为肉后过渡），玩家对本模组的**第一印象**——演出朴素克制（共享原语为主），但手感与命中反馈必须扎实。

---

## 1. 现状诊断（八透镜逐件要点）

| 武器 | 诊断 |
|---|---|
| WoodlandGreatsword 林地巨剑 | 换皮数值棒：原版 Swing 匀速挥舞，无三段感、无机制深度（仅 1/4 概率中毒）、无大招时刻。系列旗舰却最平庸，**全面重做**。 |
| RootBoomerang 树根回力镖 | 标准回力镖（30 帧返航），有拖尾但无主动决策点；返回撞到玩家直接消失，"接住"这一天然爽点完全没利用。 |
| VineHunterBow 藤蔓猎弓 | GlobalProjectile 给箭加拖尾 + 25% 概率中毒——被动、不可读；无节奏、无标记语言。 |
| DeadwoodMusket 枯木火铳 | 无弹药橡子枪，弹丸有重力/碎裂音效但碎裂**无实弹**；32 帧无 autoReuse 手感钝，无装填节奏。 |
| EmeraldTwigStaff 翡翠树枝杖 | 平直魔法弹 + 拖尾，已有基本反馈但机制为零：命中即结束，无累积语言。 |
| NatureGrimoire 自然秘典 | 3 叶扇形 + 飘动（AI 有味道），但每次施法完全一样，无韵律无峰值；1/3 概率中毒不可读。 |
| MossBomb 苔藓爆弹 | 爆炸演出已达标（蘑菇云 + Burst + 震屏），但碰撞即爆无决策点；`useTime 35` 无 autoReuse。 |
| Cuprite ×5 / XuanTie ×2 | 全部正确继承基础弹幕类 + 换主题色/DoT，**架构方向对**；但基础版机制空心导致升级版同样空心；`CupriteNatureLeaf` 未设 `Main.projFrames`（继承类不共享静态设置），帧动画越界隐患，需修复。 |

共性问题：全系列使用动画零波形（匀速）；命中反馈栈已有 ACMWeaponBurst 底子（保留）；轨迹语言统一翠绿（保留并规范化）；性能卫生良好（已走 WeaponVFX 缓存）。

重做力度判定：视觉层近期已铺过一轮（拖尾/Burst 均在），**本轮主攻机制身份 + 三段手感 + 大招韵律**，视觉按新机制补强。

## 2. 系列主题与幻想感

**「草木有灵」**——中国神话里山林草木得日月精气而通灵。林地七器不是"木头做的工具"，而是**七段仍然活着的木头**：会抽根、会发芽、会开花结籽。玩家体验目标：

- 每件武器 3 秒内读出一个"活木"动词：巨剑=**抽根**、回力镖=**回巢**、猎弓=**缠藤**、火铳=**结籽**、树枝杖=**嵌晶**、秘典=**荣枯**、爆弹=**孢生**。
- 统一颜色语言：木褐 `(90,70,40)` 外层 + 嫩绿 `(170,255,130)` 内芯 + 深翠 `(40,150,60)` 边缘；命中一律 `ACMWeaponBurst.Nature`。
- 升级版 = 同一段活木被材料重新淬炼：赤铜（Cuprite）=**灼烧/燃烧链**，橙焰 `CupriteBurn`；玄铁（XuanTie）=**放血/撕裂**，暗钢+暗红 `XuanTieBleed`。机制身份完整继承，材料风味叠在其上。

## 3. 逐件机制设计

### 3.1 WoodlandGreatsword 林地巨剑（战士旗舰 · 全面重做）

- **改造为手持弹幕**（原生 held projectile `WoodlandSwing`，`noUseGraphic + noMelee`），自定义挥舞曲线三连段：
  - 段 1 横斩（26f）：anticipation 32%（ease-in-out 反拉 0.35rad）→ strike 20%（poly(10) ease-out 扫 3.6rad）→ recovery 48%（quintic 定格渐隐）。
  - 段 2 回斩（24f）：镜像反向，anticipation 28%，更快。
  - 段 3 **根须重斩**（34f）：高举过头（anticipation 40%）→ poly(14) 砸落 → 剑尖落点沿地面爆出 3 根**荆根尖刺**（`WoodlandRootSpike`，地下钻出 poly(6) 生长，1.25× 伤害段），震屏 4。
  - 连段窗口：挥完后 45 帧内再次使用进入下一段，超时回段 1；autoReuse 按住自动连段。
- **生机（资源循环）**：命中 +1、暴击 +2，上限 10；满层时剑刃持续翠光（HoldItem 粒子提示）。满层后的下一次段 3 升级为大招——
- **大招「万木生」**：落点为中心 8 根荆根环形依次爆出（半径 60→240，逐根延迟 3f），中心播放专属着色器 `WoodlandVerdantPulse` 年轮脉冲（世界空间 quad，非全屏后处理），震屏 7 + `Nature` Burst 2×，生机清零。
- 手感细节：命中 hitstop（挥砍进度冻结 3 帧）；strike 段才有满亮刃尖 ribbon 拖尾（速度门控）；挥砍音 pitch 随段数上升；1/4 中毒保留（移入挥砍弹幕）。

### 3.2 RootBoomerang 树根回力镖（接镖连击）

- 返回碰到玩家 = **接镖**：+1「研磨」层（≤3，20s 保持），每层 +10% 伤害、+8% 出手速度；落地未接住/超时清层。
- 接镖反馈：接住瞬间柔光 + 音效 pitch 随层数上升 + 震屏 1。
- 单轮命中 ≥2 次时返程内芯拖尾增亮且返程伤害 ×1.2（奖励穿串）。
- 返航速度 14→16，归手更利落；penetrate 3 保持、单镖限制保持。

### 3.3 VineHunterBow 藤蔓猎弓（藤矢标记—藤鞭收割）

- 每第 4 支箭自动化为**藤矢**（粗藤 ribbon + 叶旋视觉），命中在目标身上**种下藤蔓标记**（6s，目标脚边藤叶环绕，仅对射手可见）。
- 后续任意本弓箭命中带标记目标：从目标脚下抽出**藤鞭**（`VineWhipLash`，50% 伤害、60px 小竖排判定）并刷新中毒。
- 计数可读：HoldItem 时弓身翠光随计数渐盛，第 4 发出弦音 pitch +0.2。
- 25% 概率中毒改为：藤矢必中毒、普通箭 1/4（身份保持且更可读）。

### 3.4 DeadwoodMusket 枯木火铳（三发弹仓—结籽霰爆）

- **弹仓 3 发**：第 1、2 发单橡子（28f）；第 3 发**霰爆**（36f，前摇多 8 帧枪身下压）：一次喷出 4 颗小橡子（各 40% 伤害，8° 扇形）+ 大枪口焰 + 后坐（玩家反向 2.5px/f）+ 震屏 2，弹仓重置。
- 弹仓可读：枪口烟量随已射发数增加；霰爆出膛音 = Item11 低沉 + Item36 分层。
- 橡子弹丸补强：命中/落地碎成 2 片**木壳弹片**（`DeadwoodShard`，30% 伤害、短寿命弧线）——"碎裂"从音效变成实弹；霰爆小橡子不再二次碎裂（性能与数值双控）。
- autoReuse 打开（原 false 是手感缺陷）。

### 3.5 EmeraldTwigStaff 翡翠树枝杖（翡翠共鸣）

- 枝弹命中往目标身上**嵌入翡翠碎片**（≤3，目标头顶绿星环绕，仅施法者可见）。
- 嵌满 3 枚后再次命中 → **共鸣爆裂**：3 枚碎片同时炸（75% 伤害、90px AoE `EmeraldResonanceBlast`），冲击环 + `Nature` Burst 1.4×，印记清空。
- 弹速/穿透保持；发射时杖尖小聚光。

### 3.6 NatureGrimoire 自然秘典（法师旗舰 · 页读韵律）

- 施法翻页 1→5，页数可读（书周绿萤 0~4 粒）。
- 第 5 页 = 大招**「荣枯页」**：不发 3 叶，改为在鼠标处绽放**生命之环**——8 片叶刃（100% 伤害）螺旋展开成环（半径 0→150）→ 悬停 → 收拢回心（二次命中窗口），环心播放 `WoodlandVerdantPulse`（嫩绿金参数）并留下 2.5s **繁茂领域**（站内玩家每秒回 1 HP 的微再生，前期克制值）。
- 普通 3 叶保持飘动机制，新增微追踪（转向 0.03/f）提高前期命中率。
- 1/3 中毒保持。

### 3.7 MossBomb 苔藓爆弹（孢子二段）

- **弹跳引信**：撞地不再即爆——弹跳一次（保留 55% 速度）+ 22 帧短引信（脉冲闪烁加速），引信到时/二次碰撞才爆；**直接砸中敌人立即爆**（奖励直击瞄准）。
- 爆炸（蘑菇云 AoE + 毒，保留现有演出）后弹出 3 颗**孢子芽**（`MossSporeBud`，弧线散落，落地生成 25% 伤害小孢子云 0.7s）——爆点变成短暂毒区。
- autoReuse 打开，useTime 35 保持。

### 3.8 Cuprite 赤铜升级 ×5（灼烧 · 燃烧链）

全部改为**继承对应基础类**（机制身份自动继承），统一叠加：

- **燃烧链**：本武器命中**已点燃**目标时迸出 2 颗火星（`CupriteEmberSpark`，35% 伤害弧线小弹，命中点燃；火星本身不再连锁）。
- 逐件风味：巨剑荆根变**焦铜火根**（点燃），万木生 →「燎原」（CupriteBurn 主题年轮 + 火环）；火铳霰爆 = **燃爆**（燃橡子 + 火口焰）；树枝杖共鸣爆裂 → **熔爆**（点燃 AoE）;秘典荣枯页 → 「燎原页」焰叶环 + 余烬领域（改为对领域内敌人持续点燃，不回血）；爆弹孢子芽 = **火孢**（点燃小云）。
- 配方 / 数值（40/36/45/35/42 伤害等）不变。

### 3.9 XuanTie 玄铁升级 ×2（放血 · 撕裂）

- **XuanTieHunterBow**：藤矢机制继承 → 第 4 发为**血钩矢**；标记收割从藤鞭变**血刺爆**（`XuanTieBloodSpike`，55% 伤害 + 流血 +2 层）；对流血 ≥5 层目标血刺伤害 ×1.5（与玄铁套装 bleedStacks 体系协同）。
- **XuanTieRootBoomerang**：接镖研磨继承；命中 +1 流血层（保持）；单轮命中 ≥3 → **血怒返航**：返速 +50%、返程伤害 ×1.25 + 溅血演出。
- 配方 / 数值（38/48）不变。

## 4. 系列内梯度

```
朴素 ──────────────────────────────────────▶ 豪华
猎弓 / 回力镖 / 爆弹        火铳 / 树枝杖        秘典（荣枯页+年轮）  巨剑（三连段+万木生+年轮）
仅共享原语                  共享原语+节奏峰值      专属shader复用        专属shader首发+手持弹幕
```

升级版继承对应基础件的梯度位置，材料风味只换色/换 DoT/换大招皮，不加新 shader。

## 5. 视觉技术方案

- **专属着色器（系列仅 1 个）**：`Effects/WoodlandVerdantPulse.fx`（ps_3_0）——极坐标"年轮脉冲"：3 道同心年轮环随 uProgress 外推 + 12 条根须放射线（s1 噪声扰动）+ 中心柔光，边缘 smoothstep 淡出。画在世界空间 ~560px quad（SpriteBatch Immediate + Additive），**不是全屏后处理、不占全屏名额**。消费方：巨剑「万木生」（翠绿）、秘典「荣枯页」（嫩绿金）、对应 Cuprite 大招（橙焰参数）。由纯视觉弹幕 `WoodlandVerdantPulseVFX` 承载，主题经 `ai[0]` 同步。
- 其余全部复用共享原语：`WeaponVFX.DrawRibbonTrail/DrawProjectileTrail`（拖尾）、`DrawShockwaveRing`（爆点）、`DrawGlowBurst`（廉价柔光）、`ACMWeaponBurst`（命中栈，主题 Nature(3)/CupriteBurn(1)/XuanTieBleed(2)）、`ACMAsset.SlashBurst`（荆根/藤鞭形体）、`ACMAsset.SoftGlow/Sparkle`。
- 荆根/藤鞭/血刺均为程序化绘制（SlashBurst 双层染色 + dust），**不新增任何贴图**；无贴图弹幕用 `InnoVault/Assets/placeholder`。
- 印记/标记视觉走 GlobalNPC PostDraw 画 ≤3 颗 BlankStar 小星，无每帧分配。

## 6. 平衡与定位

获取途径 / 配方 / 稀有度 / 职业定位全部不变。标称 DPS 论证（全中口径）：

| 武器 | 原标称 DPS | 新标称 DPS | 变化 | 论证 |
|---|---|---|---|---|
| 巨剑 | 16/30f = 32.0 | (16+16+20)/(26+24+34)f ≈ 37.1 | +16% | 需维持连段（断连回段1）+近身风险；万木生需 10 次命中充能，峰值不入常态 DPS |
| 回力镖 | 13/22f 标称 | 满研磨 +30%，需连续 3 次接镖维持 | 峰值+30% | 执行技巧付费；未接镖时与原版持平 |
| 猎弓 | 11+箭 | 藤鞭 = 每 ≥5 支箭附加 50% | 期望 +10~12% | 需要 4 发铺垫 + 标记 6s 窗口内命中 |
| 火铳 | 14/32f = 26.3 | (14+14+4×5.6)/(28+28+36)f ≈ 33.1 | +26%(全中) | 原基线同期明显偏弱（同期武器 30+）；霰弹 8° 散布中距实战 ~3/4 命中 → 实效 ≈ +15%；碎片 30% 为贴脸奖励 |
| 树枝杖 | 16/28f = 34.3 | 每 4 中附加 75% 爆裂 ≈ 40.7 | +19%(单体) | 需对同一目标连续 4 次命中；主要价值在 90px AoE |
| 秘典 | 13×3/30f | 4 轮常规 + 第 5 轮 8 叶环 | 全中 +33% / 实战 +10~15% | 环形叶刃对单体仅 2~4 叶命中；繁茂领域回 1HP/s×2.5s 为微量 |
| 爆弹 | 20/35f = 34.3 | 主爆不变 + 孢子芽 3×25% 站雾生效 | 区域 +15% 内 | 弹跳引信实际延迟了均爆速度，直击玩法与原版等价 |
| 升级 7 件 | 数值全部保持 | 火星 2×35% 需"已点燃"前置；血刺 55% 每第 4 发 | 期望 +8~12% | 肉后过渡位可容纳；配方不变 |

## 7. 性能与多人预算

- 弹幕上限：万木生单次 8 荆根 + 1 VFX；荣枯页 8 叶 + 1 领域 + 1 VFX；霰爆 4 小橡子 + ≤2 碎片；全部寿命 ≤ 150f。
- 拖尾全部经 `WeaponVFX`（受 `MythologyConfig.Trail` 降级）；VerdantPulse 为单 quad Immediate 绘制，参数逐帧 set、Effect 静态缓存（`WeaponVFX.GetEffect`）。
- 震屏预算：普通命中 ≤2 / 霰爆·重斩 2~4 / 万木生 7（一次性）。
- 多人：连段/弹仓/页数/藤矢计数存 ModItem 实例字段（仅 owner 端 Shoot/OnHit 消费）；段数与主题经 `Projectile.ai[]` 同步；印记/标记存 GlobalNPC 实例字段（owner 端驱动伤害，仅 owner 可见视觉——伤害判定本就发生在 owner 端）；一切追加弹幕生成判 `owner == Main.myPlayer`；繁茂领域治疗只对 `Main.LocalPlayer` 自身判定。
- 修复既有隐患：`CupriteNatureLeaf` 补 `Main.projFrames`（继承类静态设置不共享）。

## 8. 实施清单

1. `Effects/WoodlandVerdantPulse.fx` 新建 + 按名编译（ps_3_0）。
2. `WoodlandGreatsword.cs`：重写为手持弹幕三连段 + 生机 + 万木生；新增 `WoodlandSwing`、`WoodlandRootSpike`、`WoodlandVerdantPulseVFX`（供秘典复用）。
3. `DeadwoodMusket.cs`：弹仓/霰爆；新增 `DeadwoodPellet`、`DeadwoodShard`。
4. `EmeraldTwigStaff.cs`：共鸣印记；新增 `EmeraldMarkGlobalNPC`、`EmeraldResonanceBlast`。
5. `NatureGrimoire.cs`：页读韵律 + 荣枯页；新增 `GrimoireBloomLeaf`、`VerdantFieldProj`。
6. `MossBomb.cs`：弹跳引信 + 孢子芽；新增 `MossSporeBud`；`MossExplosion` 参数化半径/伤害缩放。
7. `RootBoomerang.cs`：接镖研磨。
8. `VineHunterBow.cs`：藤矢标记/藤鞭；新增 `VineWhipLash`、`VineMarkGlobalNPC`。
9. Upgrades ×7：改为继承基础类（覆写 SetDefaults/AddRecipes/主题虚成员）；新增 `CupriteEmberSpark`（写在 CupriteWoodlandGreatsword.cs）、`XuanTieBloodSpike`（写在 XuanTieHunterBow.cs）。
10. ReadLints 清零 → 最后小步更新两个 hjson 的本系列 Tooltip（zh/en 同步，回读验证）。
