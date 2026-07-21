# 天柱系列武器重做设计文档（PillarOfTheHeavens）

> 管辖件：`Celestias/PillarofTheHeavenes/Items/` 下 Cloudpiercer / FirmamentCleaver / JadeArmillary / PillarGuardiansHalberd / ScepterofTheOverseer / ThunderclapHandcannon / TomeofDivineLaw 七件。
> 掉落/兑换来源（天柱入侵 + HeavenFragment/EmpyriteBar 配方）与进度位（Red 稀有度、月亮领主前后段）**不变**。

## 1. 现状诊断（八条透镜逐件）

| 件 | 一眼身份 | 三段感 | 命中反馈 | 轨迹 | 机制深度 | 演出峰值 | 一致性 | 性能 |
|---|---|---|---|---|---|---|---|---|
| 流云落日弓 Cloudpiercer | ✗ 三箭扇+轻追踪=通用换皮弓 | ✗ 匀速连发 | △ Burst 0.8 单层 | ✓ ribbon 已接 | ✗ 无决策点 | ✗ 无 | ✓ 金青 | △ 每箭每帧全 NPC 扫描 |
| 昊天巨阙 FirmamentCleaver | ✗ 原生挥砍+剑气=夜刃换皮 | ✗ 原生 Swing 匀速 | ✓ Burst 1.1+shake2 | ✓ | ✗ | ✗ | ✓ | ✓ |
| 璇玑玉轮 JadeArmillary | △ 回旋镖+3 命中散珠，但"浑天仪"只是贴图自转 | ✗ | ✓ | ✓ | △ 被动计数 | ✗ | ✓ | △ 碎片每帧全扫描 |
| 镇天神戟 PillarGuardiansHalberd | △ 突刺+冲击波有雏形，无"方阵"感 | △ Quad 伸缩但无前摇 | ✓ | △ | ✗ 无连段 | ✗ | ✓ | ✓（PreDraw 翻转旋转存疑） |
| 监察者权杖 ScepterofTheOverseer | ✗ 双追踪爆炸球=通用法杖，"监天"未落地 | ✗ | ✓ Burst 1.2+shake | ✓ | ✗ | ✗ | ✓ | △ 全扫描 |
| 轰雷神铳 ThunderclapHandcannon | △ 链电有雏形，"雷霆后坐"未做 | ✗ 无后坐 | ✓ | ✓ DrawBeam 链电 | ✗ | ✗ | ✓ | ✓ |
| 天律法典 TomeofDivineLaw | △ 每 4 次符文阵有节奏，"律令锁敌"未做 | ✗ | ✓ | ✓ | △ 被动计数 | △ 符文阵朴素 | ✓ | △ 全扫描 |

结论：七件都已接入 WeaponVFX/ACMWeaponBurst 的基础反馈（近期低保补丁），但**机制身份全部缺位**、无一有三段感与大招时刻 → 全系列做机制重做 + 演出梯度重建，保留已经达标的拖尾/Burst 底座。

## 2. 系列主题与幻想感

神话原型：**不周山天柱 / 擎天玉柱**。天柱既是撑天之柱，也是天庭纲纪的具象——柱倾则天倾，故守卫天柱者行使"代天判罚"之权。玩家拿到这套武器时应感到：**我在替天行道，每一次关键打击都由苍穹亲自盖章**。

- 配色语言：金白祥瑞（`ACMWeaponBurst.HeavenlyPillar` 主题：暖金 255,215,120 / 瑞白 255,250,220）+ 天青云气（140,215,235）+ 雷霆青白（190,235,255，对齐 `TelegraphColors.Lightning`）。
- **系列贯穿语言——"天罚落雷"**：每件武器的高光时刻都以同一原语收尾：目标点短暂金环汇聚（12f 因果预告）→ 一道贯天光柱自天顶轰落（BeamGrad 光柱 + LightningBranch 电弧 + 落点冲击环 + 震屏 3~4）。七件武器以不同机制"申请"这道雷，玩家形成条件反射：看到金环收缩 = 判决已下。
- 形状语言：竖直线（天→地）、正圆环（浑仪/法阵）、直线突刺（方阵）。

## 3. 逐件机制设计

通用：所有"大招时刻"触发原语 `HeavenJudgmentBolt.Strike(...)`（共享类，置于 ThunderclapHandcannon.cs，同命名空间七件共用）。

### 3.1 流云落日弓 Cloudpiercer —— 穿云（贯穿云隙的节奏狙击）
- 左键：2 支穿云箭（3° 微扇，比旧 3 箭×6° 更聚焦），保留轻追踪与 ribbon。
- **每第 5 发 = "贯云神矢"**：前 4 发每发在弓口叠一圈青环（视觉计数）；第 5 发射出巨型光矢——12f 弓口凝聚（提示音渐高）随射出，神矢 2.2× 伤害、无限穿透、extraUpdates 3、巨幅 ribbon + 光轴；**命中的第一个敌人头顶落天罚雷（0.8×）**。
- 决策点：数发管理，把第 5 发留给高价值目标/一串直线敌人。

### 3.2 昊天巨阙 FirmamentCleaver —— 断穹（旗舰 1，手持弹幕三连段）
- 改造为原生手持挥舞弹幕 `FirmamentSwing`（noUseGraphic），三连段（2.5s 未续段则重置）：
  - **连段 1/2（各 26f）**：正/反手横斩。波形＝前摇 42%（Quad 拉回 -0.35 rad 反向蓄势）→ 爆发 14%（poly(16) ease-out 扫过 3.6 rad）→ 收招 44%（Quintic 回摆）。爆发帧发射剑气（0.85×）。刀尖记录轨迹画双层 ribbon，只在爆发窗口不透明（速度门控）。
  - **连段 3（44f）＝断穹斩**：前摇 18f 高举过顶（末 4f pow(8) late-snap 后拉 + 抖动 + 蓄力粒子上聚，72% 处粒子静默）→ 3f poly(20) 劈落（1.55×）→ 落点劈出**竖直天裂 `FirmamentSkyRift`**（专属着色器 PillarSkyRift，720px 高裂隙，伤害窗 8f ×0.98/跳最多 2 跳）→ 震屏 6 + 径向泛光 + 0.12 强度金青染屏 8f（全屏名额契约）→ 收招 23f。
- 决策点：连段节奏管理；断穹斩前摇 18f 是主动承担的硬直。

### 3.3 璇玑玉轮 JadeArmillary —— 玉衡仪（旗舰 2，浑天仪环轨道）
- 左键（180 伤害）：投掷玉轮（≤2 枚在场），保留去/回程与 3 命中散星珠；本体绘制改为程序化三环（贴图 + BlankStar 环点 + ribbon），自转速度随飞行速度门控。
- **右键 = "张衡浑天阵"（10s 冷却，6s 持续）**：以玩家为中心展开浑天仪领域（专属着色器 PillarArmillaryRing：赤道/黄道/子午三椭圆环 + 刻度珠，随时间转动倾角）。6 枚星官玉珠沿环轨道运行（0.5× / 珠，单体 30f 判定间隔）；每 60f 对环内最近敌人落天罚雷（0.7×）。展开瞬间全屏径向泛光 + 震屏 4。
- 决策点：浑天阵是贴身清怪/防御窗口，与投掷远程互补；冷却管理。

### 3.4 镇天神戟 PillarGuardiansHalberd —— 守卫戟（方阵三连突）
- 基伤 210。三连突（1.6s 未续则重置）：
  - **突 1/2（各 12f）**：短突（×0.85，150px），波形＝4f 收杆反拉 → 2f poly(20) 全伸 → 6f 收回；伴随戟尖白芒。
  - **突 3（20f）＝贯阵**：8f 拉杆前摇（戟后收 40px + 玩家重心下沉粒子）→ 3f 爆发全伸 200px（×1.7）+ 玩家向前惯性 5px/f×4f + 贯阵冲击波（×0.8）→ **命中时落点落天罚雷（0.6×）** → 9f 收招。
- 决策点：三突节奏与第三突的对线定位；第三突前摇是破绽。

### 3.5 监察者权杖 ScepterofTheOverseer —— 监天（监天印标记→天罚）
- 左键（190 伤害）：单发监天光球（追踪增强、必中感），命中爆炸并烙**监天印**（GlobalNPC 本地层数，5s 衰减）：敌人头顶浮现旋转金环 + 竖直细天光丝（层数越多环越亮）。
- **叠满 3 层印 → 印记引爆**：清层并从天顶落天罚雷（1.5×），伴随"审判确认"音（Item29 高音 + Item122）。
- 决策点：集火同一目标叠印 vs 分散压制。

### 3.6 轰雷神铳 ThunderclapHandcannon —— 霹雳手铳（雷霆后坐 + 六发转轮）
- 基伤 150 / 22f。**每发强后坐**：玩家沿反方向获得 4.5px/f 冲量（可向下射击做后坐小跳——后坐即位移工具）、枪口暴闪 + 后坐震屏 2、Item38 低频 + Item122 高频分层。
- **六发转轮**：1~5 发普通雷弹（保留链电 0.5×）；第 5 发后枪口缠绕电弧粒子提示上膛；**第 6 发 = 轰天雷**（2.2×，弹体大、拖雷光柱），命中点**横排连落 3 道天罚雷（0.65×/道，90px 间距）**，后坐 ×2.5（明显腾空）。
- 决策点：管理第 6 发时机 + 用后坐位移躲弹。

### 3.7 天律法典 TomeofDivineLaw —— 天条（律令锁敌→审判）
- 左键（160 伤害）：掷"天条律令符"（直线快弹，轻追踪），命中烙**天条锁**（GlobalNPC 本地 4s）：敌人身上升起竖排金色律文光丝（BeamGrad 细线），受天罚雷伤害 +25%。
- **每 4 次施法 = 天律审判页**：鼠标处展开天书法阵（复用共享 ArenaRunic 着色器 uMode=0 + 金律文配色，1.0× 阵伤 2 跳）；展开瞬间**对阵内所有被锁敌人各落一道天罚雷（1.2×，锁敌加成后 1.5×）**。
- 决策点：先用律令符锁多个目标，再把审判页盖在人堆上收割。

## 4. 系列内梯度

- 朴素件（共享原语）：穿云弓 / 守卫戟 / 监天权杖 / 霹雳手铳 / 天律法典——拖尾、冲击环、GlowBurst、Burst、BeamGrad、ArenaRunic 复用 + 共享天罚雷。
- 旗舰件（专属 ps_3_0 + 全屏时刻）：**昊天巨阙**（PillarSkyRift 天裂 + 染屏 8f）、**璇玑玉轮**（PillarArmillaryRing 浑天领域 + 展开泛光）。
- 天罚雷是全系列统一的"高光收尾"，件与件之间靠触发机制（数发/连段/叠印/转轮/锁敌/轨道）区分身份。

## 5. 视觉技术方案

- 复用：`WeaponVFX.DrawProjectileTrail/DrawRibbonTrail/DrawShockwaveRing/DrawGlowBurst/DrawRadialBloom/AddScreenShake/ApplyPaletteTint`、`ACMShaders.DrawBeam(BeamGrad)/ArenaRunic/NoiseTexture`、`ACMAsset.LightningBranch/ElectricArcSheet/BlankStar/SlashBurst/GlaciateWave/SoftGlow`、`ACMWeaponBurst.HeavenlyPillar`。
- 新建专属着色器（均 ps_3_0，按名编译）：
  1. `Effects/PillarSkyRift.fx`——断穹天裂：顶点直带（BuildRibbonStrip 契约，uv.x 沿长），噪声撕裂的中心白热裂缝 + 金边 + 青色外晕，天光自上而下流动，uProgress 控开裂/弥合。
  2. `Effects/PillarArmillaryRing.fx`——浑天仪领域：屏幕空间 decal（WorldDecalParams + DrawScreenSpaceDecalStandalone，加性），三椭圆环（倾角随 uTime 转动出 3D 感）+ 环上刻度珠 + 中心淡金。
- 天罚雷不建专属 shader：BeamGrad 光柱 + LightningBranch 两层抖动叠加 + 冲击环即可（性能/一致性优先）。
- 全屏名额：断穹斩染屏（ApplyPaletteTint 内部管理）、浑天阵展开与天裂泛光（DrawRadialBloom 内部管理，占用失败自动退化 GlowBurst）。decal 类（ArenaRunic/浑天环）非 screenTarget 后处理，不占名额，强度克制（加性 ≤0.5）。

## 6. 平衡与定位（获取途径 / 稀有度 / 职业线均不变）

理论单体 DPS 对比（60fps；"旧实际"按典型命中数折算）：

| 件 | 旧（实际） | 新（周期折算） | 变化 | 论证 |
|---|---|---|---|---|
| 穿云弓 | 3 箭理论 2081 / 实际双中 ≈1388 | 4×2×185 + 神矢 407+雷 148 → ≈1527 | +10% | 双箭更聚焦找回浪费伤害；峰值移入第 5 发 |
| 昊天巨阙 | 挥+剑气双中 ≈1050 | (245+208)×2+380+裂 2 跳 480 → ≈1104（1 跳 916） | -13%~+5% | 断穹斩前摇 18f 换峰值，期望持平 |
| 璇玑玉轮 | ≈1000（驻留多跳） | 左键 180（923）+ 阵均摊 ≈190 → ≈1113 | +11% | 左键 -8% 抵消右键新增；阵有 10s 冷却 |
| 镇天神戟 | 突+波双中 1227 | (178×2+357+波 168+雷 126)/44f → ≈1373 | +12% | 第三突前摇硬直 + 全额需贴脸 |
| 监察者权杖 | 双球全中 990 / 常单中 ≈740 | (3×190+印雷 285)/60f → ≈855 | +16%* | 旧双叉对单体天然浪费一球；新单球全额命中，属找回浪费而非纯增幅 |
| 轰雷神铳 | ≈444（系列谷底） | (5×150+330+雷 98)/132f → ≈535 | +20%* | 曲线修正：旧值远低于系列均值；后坐自我位移是内建风险 |
| 天律法典 | ≈903 | (4×160+阵 320+锁雷 240)/72f → ≈1000 | +11% | 审判雷需要先锁敌（两步操作） |

\* 超 ±15% 的两件均为"旧版明显低于系列曲线/存在结构性浪费"的修正，非纯数值膨胀。

## 7. 性能与多人预算

- 天罚雷生成频率被各武器机制天然限流（最快 = 权杖 1 道/s）；每道绘制 = 1 次 DrawBeam + 2 张 LightningBranch + 1 环 + 1 GlowBurst，无 RenderTarget。
- decal 满屏 quad 同屏 ≤2（浑天阵 1 + 审判页 1），均为大招短窗口；天裂为顶点带非满屏。
- 粒子单事件 ≤25，弹幕 AI 内 dust 均带 NextBool 节流；全 NPC 扫描仅保留在低频路径（命中/每 60f），追踪弹幕的每帧扫描改为 8f 间隔重锁。
- 多人安全：所有主动生成（神矢、天罚雷、天裂、浑天珠、审判雷）判 `Projectile.owner == Main.myPlayer` / Shoot 天然 owner；监天印/天条锁为 GlobalNPC **本地字段**（owner 端闭环：owner 命中累计 → owner 端引爆生成弹幕同步），其他端仅少视觉不影响逻辑；震屏/泛光/染屏只在绘制路径。
- 连段/数发状态存 ModItem 实例字段（Shoot 仅 owner 端调用，与基准件 TidecallersDecree 同法）。

## 8. 实施清单

1. `Effects/PillarSkyRift.fx` + `Effects/PillarArmillaryRing.fx`（ps_3_0，按名编译）。
2. `ThunderclapHandcannon.cs`：+`PillarPalette` 共享配色、+`HeavenJudgmentBolt`（系列天罚雷）、手铳后坐/转轮重做。
3. `Cloudpiercer.cs`：双箭 + 第 5 发贯云神矢（`PiercingSunArrow`）。
4. `ScepterofTheOverseer.cs`：单球 + `OverseerMarkGlobalNPC` 监天印 + 引爆天罚。
5. `TomeofDivineLaw.cs`：律令符锁敌（`DivineLawGlobalNPC`）+ 审判页（ArenaRunic 复用）。
6. `PillarGuardiansHalberd.cs`：三连突波形 + 贯阵 + 天罚雷。
7. `FirmamentCleaver.cs`：手持弹幕 `FirmamentSwing` 三连段 + `FirmamentSkyRift` 天裂（旗舰 1）。
8. `JadeArmillary.cs`：本体程序化三环 + 右键 `ArmillarySphereField` 浑天阵（旗舰 2）。
9. ReadLints 清零 → 最后同步两个 hjson（物品 Tooltip 简中/英文，新弹幕 DisplayName 走代码注册为主）。
