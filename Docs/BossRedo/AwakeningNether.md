# 觉醒冥龙（AwakeningNether）重做设计文档 — Boss 重做工程 V3

> 单元：`Underworlds/Boss/AwakeningNethers/`（阴间压轴级蠕虫龙）
> 关联单元：`Underworlds/Boss/NetherDragons/`（幽冥龙，前期形态，**另一代理负责，只读不改**）
> 质量标杆：`Celestias/Boss/FourSacredBeasts/Xuanwus/`

---

## 1. 现状诊断

以 choreography skill 七大本能 + 失败模式清单为透镜：

1. **入场演出缺失**（本能 #7 / PACING §6）。`OnSpawn` 只有一声吼 + 一圈粒子，压轴级"觉醒"冥龙直接刷在玩家旁边，没有任何"从冥渊苏醒"的仪式感。
2. **死亡演出缺失**（PACING §6 worm dies in stages）。`OnKill` 是一次性粒子喷发瞬间消失；80 节的巨龙应该分段死亡：挣扎 → 逐节爆裂 → 头部内爆终响。
3. **蠕虫招牌的"盘旋蓄势 → 直线穿刺"节奏完全不存在**（本能 #1/#2，MOTION §2/§3）。全程只有 `CircleTarget` 匀速环绕（惯性 18 平滑跟随，速度恒 ~12px/f），**整场战斗 Boss 从不朝玩家冲刺**。没有 reel-back 反向蓄势、没有瞬时 launch、没有硬刹车——一条没有速度对比的扁平蠕虫。
4. **招式无 anticipation/burst/recovery 波形**。虚空弹每 70 帧无前摇直接出膛；Finality 吐息 90 帧匀速连喷（flat）；预警只有稀疏 dust 点，缺乏"预警线扫过再喷"的清晰因果（本能 #4）。
5. **体节是死的**（本能 #3）。80 节身体只是位置跟随 + 随机冒魂雾，没有脊波传递（MOTION §4 whip chain）、没有次级运动，重量感为零；且 80 节 × 每节 3 层辉光 + 体节间逐 sprite 假光束 = 巨大 draw 开销，视觉一坨。
6. **无专属着色器**。视觉全靠 BAWDust sprite 多层叠加（DrawVoidCore 一次 5+3+2 层），廉价且噪；标杆玄武有 5 个专属 .fx。裂隙门/漩涡/吐息全是 sprite 堆。
7. **状态机字段不同步（多人隐患）**。`stateTimer/attackTimer/subPhase/act/gateEntrance/vortexCenter` 全是实例字段，Head 未重写 `SendExtraAI`，多人下客户端状态机必然漂移（技术底线违规）。
8. **公平性问题**：第三幕每帧 `Target.velocity += pull*0.3f` 硬拉玩家进致命符阵（且只拉一名玩家）；VoidBolt 追踪 360 帧不截止、越近追踪越强（惩罚近身走位）；换阶段不清弹；玩家全灭无脱战逻辑（`CheckActive()=>false` 且无 despawn 兜底）。
9. **配色噪**：紫/青/粉/红四色随机混用（DrawSoulOrbit 四色、SoulOrb 随机三色），没有统一深渊色语言。
10. **可取之处（保留并强化）**：三幕脚本结构（巡游/裂隙/吞噬）思路成立；V2 演出标量中枢（fog/bloom/runic/warp）框架良好；魂蚀 DoT 身份层已统一到 `UnderworldFieldPlayer`；SegmentLaser 的预告→实伤两段式合格。掉落表 / SoulBanner 引用（`AwakeningNetherHead`）等 public 类型不得变动。

**重做力度判定：全面重做 AI 编排与视觉**（保留三幕骨架与机制身份，重写运动/节奏/演出/绘制，新增专属着色器）。Items 子文件夹近期已接入 WeaponVFX/ACMShaders，质量达标，不在本轮范围。

---

## 2. 设计主题与幻想感

**"冥渊尽头的活体地狱"** —— 幽冥龙（NetherDragon，前期形态）掘开万墓、吞尽亡魂后的终局觉醒态。

玩家体验三个递进的幻想：
- **第一幕「冥界巡游」**：它是一条把整个屏幕当猎场的巨蠕虫——盘旋时是压迫的阴影，穿刺时是擦着耳边的音爆。学会读它的蓄势。
- **第二幕「次元裂隙」**：它的身体开始撕裂空间——裂隙门穿梭、脊波沿 44 节躯体奔涌成浪。空间本身成为武器。
- **第三幕「虚空吞噬」**：它盘成衔尾之环，环内即冥狱；中央奇点吞噬光线。玩家在龙身牢笼里找缺口求生。
- **觉醒终末（15%）**：龙体拉直成地平线，魂焰帘幕与巨吼同时落下——压轴处决签名，之后进入永久狂暴。

配色语言收敛为三色阶（遵守 `TelegraphColors` 契约）：
- **主题色**：AwakeningPurple（觉醒紫）/ VoidDarkPurple（虚空黑紫）——龙体、氛围、非致命预备；
- **DoT 色**：GhostGreen（鬼绿）——魂蚀领域/魂焰内芯（地府 DoT 标准色）；
- **致命色**：TelegraphColors.Lethal（纯红）——只出现在穿刺线、激光帘幕、奇点收口等真实伤害预警。
- SoulPink 仅保留给可清除的噬魂卫星（"这是可以打的东西"）；NetherCyan/DestructionRed 从 Boss 本体退役。

---

## 3. 阶段结构与血量断点

| 断点 | 状态 | 规则变化 |
|---|---|---|
| 入场 | `Awakening`（~4.2s 演出） | 无敌 + 接触伤害清零；地面震颤 → 三次骨节破土 → 头部破土冲天 → 空中咆哮定格 |
| 100%→60% | 第一幕 `ActI` | 手写招式循环：盘旋弹幕 → 穿刺×2 → 吐息走廊 → 喘息 |
| 60% | `ActTransition`（75f 无敌节拍，清弹） | 裂隙撕开演出（GenericWarp·rift 脉冲 + 咆哮） |
| 60%→30% | 第二幕 `ActII` | 裂隙门穿梭冲刺 / 脊波尾鞭 / 灵魂风暴 |
| 30% | `ActTransition`（75f 无敌节拍，清弹） | 奇点开启演出（GenericWarp·void + 咆哮） |
| 30%→0% | 第三幕 `ActIII` | 衔尾困杀（龙环收缩）/ 奇点+噬魂卫星 / 穿刺强化版 |
| 15%（一次性） | `Finality`（处决级签名） | 龙体拉直 + 巨型魂焰扫射 + 全体节激光帘幕；之后回第三幕**永久狂暴**（节奏 -18%，穿刺 +1 循环） |
| HP→1 | `DeathThroes`（~4.5s 演出） | 无敌 + 清弹 + 接触伤害清零；挣扎 → 尾到头逐节爆裂 → 头部拉升内爆 → 终爆真死 |

多人同步：状态机迁移到 `npc.ai[0..3]`（state/盘旋角/attackTimer/subPhase），扩展字段（act、finalityDone、门坐标、奇点坐标等）走 `SendExtraAI/ReceiveExtraAI`（BasicWorm 已有管线，调用 base 后追加），一切状态切换 `netUpdate = true`。玩家全灭 → 加速逃离屏幕并 `EncourageDespawn`。

---

## 4. 招式编排表

速度对比基准：盘旋 ~9-12 px/f ←→ 穿刺 46-52 px/f（对比 4 倍以上）。
所有致命预警使用 `TelegraphColors.Lethal`，时长按威胁分级（36f 冲刺 / 45f 中招 / 75f 处决）。

| 招式 | 前摇 | 爆发 | 收招 | 预警方式 | 公平阀门 |
|---|---|---|---|---|---|
| **A1 盘旋虚空弹**（第一幕） | 每轮 30f 口部聚能（粒子向心收敛，72% 处截止——静默的吸气） | 1f 三连扇形出膛 + 头部后坐 6px/f | 40f 盘旋 | 聚能粒子密度即进度条 | 弹速 wind-up：前 30f 速度 40%→100%；追踪 210f 截止；最小发射距离 260px |
| **A2 冥渊穿刺**（全程招牌，第一幕×2 / 第三幕×3 狂暴） | reel-back 36f：`offset=dirAway*pow(t/36,8)*300px` 后撤（8 次幂——前 30f 几乎不动，最后 6f 猛然吸气）；同时红色穿刺线 BeamGrad 从龙口扫向玩家 | 瞬时 `velocity=dir*46`（狂暴 52），转向钳制 0.012rad/f，9-14f | 穿过玩家 280px 后 ×0.80/f 硬刹 18f，接盘旋回环 | 36f 固定蜂鸣（Item29 变调）+ 红色冲刺线渐亮 | 冲刺线预警必达 36f；转向钳死避免"回头咬"；刹车期接触伤害减半；循环间 30f 盘旋喘息 |
| **A3 魂焰走廊**（第一幕/终末强化） | 45f：口部聚能 + 走廊预警线（鬼绿→红渐变的水平扫线，跟随玩家当前高度锁定） | 55f：吐息焦点沿走廊匀速扫过（7f/发魂焰舌），沿途铺魂雾 | 25f 抬升脱离 | 预警线先行扫过整条走廊 | 走廊锁定后不再跟踪玩家（可预判离开）；魂雾 DoT 非致命（鬼绿） |
| **B1 裂隙门穿梭**（第二幕） | 60f：成对裂隙门开启动画（VoidRift 吸积盘从 0 旋开）+ 出口门红色符阵收口 | 穿门冲刺 42px/f ×3 次：入门瞬移出门，冲刺方向**朝玩家预测点**（`Target.Center+velocity*14`） | 25f 减速 | 出口门先亮红 45f 才有第一次穿出 | 门口伤害区仅开门完成后生效；穿出前 8f 出口门爆闪；冲刺线预警同 A2 |
| **B2 脊波尾鞭**（第二幕，体节机制主角） | 30f：头部甩头（角度反打），尾端蓄势发光 | 尾部注入脊波冲量（SpringForce=±14，×0.88/f 衰减，0.35/f 向邻节传播）；波峰经过的体节每 6 节喷 1 枚慢速魂火余烬（wisp，24px/f→漂浮） | 波传完自然结束（~70f） | 波峰本身发光可见，wisp 出膛慢 | wisp 速度低、寿命 150f、密度受限（≤8 枚/波）；纯走位可解 |
| **B3 灵魂风暴**（第二幕收尾） | 50f 环形聚能（密度 ∝ sqrt(t)，72% 截止） | 1f 环形 16 连发（螺旋/直线交替） | 45f 盘旋 | 聚能环 + 固定蜂鸣 | 出膛速度 wind-up 40%→100% over 30f；风暴前必有 B1 完整循环（节奏留白） |
| **C1 衔尾困杀**（第三幕招牌） | 40f：龙加速绕玩家成环（r=560），环上体节亮起 | 环半径 560→350 收缩 240f；环上体节每 20f 向环心喷 1 轮 wisp（内密外疏，赶玩家出环）；**头尾之间天然缺口即安全出口**（尾端鬼绿标记高亮） | 收缩到底后头部向环心穿刺 1 次（36f 红线预警），随后散开 45f | 尾端缺口鬼绿高亮 + 收缩节奏可读 | 缺口宽度 ≥ 龙身 8 节弧长；wisp 慢速可穿；穿刺仅 1 次且预警足额 |
| **C2 奇点+噬魂卫星**（第三幕） | 55f：奇点旋开（VoidRift 大 decal + 符阵收口预备色） | 奇点活跃 300f：温和引力（仅 220~900px 环带内 0.14/f，**留反制窗口**）+ 5-6 枚可清除卫星环绕收缩后齐射 | 60f 喘息（奇点闭合） | 符阵收口 + 卫星闪烁 45f 后才齐射 | 引力≤玩家加速度的一半；卫星 3 次友方命中可清除；卫星齐射方向锁定后不再跟踪 |
| **F 觉醒终末**（15% 一次性处决签名） | 75f：龙体拉直 + 尖啸蜂鸣加速（延迟数组 40→6f，8 步升调）+ bloom ramp（t³） | 全体节激光帘幕（40f 预告细线 + 40f 实伤）+ 巨型魂焰扇 90f 横扫 | 30f 收束 → 回第三幕狂暴循环 | 处决级 75f 预告 + 尖啸升调 + 全屏泛光 | 起手 22f 无敌防倒地秒杀；激光帘幕保持 40f 细线预告；帘幕间距 ≥ 玩家 3 身位 |

招式选择：每幕为**手写循环表**（PACING §2 hand-authored cycles），压迫招与喘息招显式交替，禁止随机连读同一招。

---

## 5. 入场 / 换阶段 / 死亡三大演出脚本

### 5.1 入场「冥渊苏醒」（Awakening，~250f）

| 帧 | 节拍 |
|---|---|
| 0-60 | 低频 rumble 渐强（`t²*5` 震屏）+ 冥雾骤起（fog 0→0.30）+ 玩家脚下地面出现"冥渊之眼"（VoidRift decal 从 0 旋开，符阵预备色） |
| 60-150 | 三次**骨节破土**假动作（每 30f 一次）：破土点依次逼近玩家，各一次 dust 岩屑喷发 + 震屏 6 + 闷响（越来越近、越来越响——蜂鸣加速原理） |
| 150-152 | **静默 2f**（粒子全停——爆发前的收缩） |
| 152-200 | 头部从眼位破土冲天：`velocity=(0,-38)`，拖尾全开 + 岩屑瀑布 + 震屏 12 + Roar；身体节从洞中鱼贯而出 |
| 200-250 | 空中减速悬停，头对准玩家**定格 30f**（菜单式威压——静止即威严），全屏 bloom 脉冲 + GenericWarp·void 一次收缩脉冲 + 第二声低吼；结束转入第一幕 |

全程 `dontTakeDamage=true`、接触伤害 0；Boss 生成点在目标玩家下方 ~700px（地下），破土前不可见（不绘制或 alpha 0——用 fadeIn 标量）。

### 5.2 换阶段（ActTransition，75f，两次）

清全部敌对弹幕 → 龙向玩家上方汇聚减速 → 无敌 + 咆哮 + 对应主题脉冲：
- →第二幕：GenericWarp·rift 脉冲 0.6 + 裂隙撕裂声（Item122）+ bloom 1.0；
- →第三幕：GenericWarp·void 脉冲 0.6 + 深渊嗡鸣（Item117 降调）+ fog 阶跃加深。
结束后第一招固定为该幕的"教学招"（B1 / C2），且首轮弹速 wind-up。

### 5.3 死亡「冥渊崩解」（DeathThroes，~270f）

| 帧 | 节拍 |
|---|---|
| 0 | HP 锁 1、无敌、清弹、接触伤害 0；所有长音效停止 |
| 0-90 | 挣扎：连续 4 次脊波冲量乱序注入，龙身如鞭乱摆，速度渐降 ×0.97/f；魂焰从鳞缝喷出（伤害叙事：dust 密度 ∝ progress） |
| 60-200 | **从尾到头逐节爆裂**：每 3f 引爆一节（dust 爆发 + 魂火余烬上飘 + 每第 8 节震屏 7 与低爆音）；被爆体节隐藏（alpha→0 + 停止绘制） |
| 200-235 | 头部最后拉升 15px/f → 悬停、粒子**向内收缩**（吸积内爆前奏，密度递减至静默） |
| 235-250 | 内爆：头部缩至 40% + 白紫闪 → **终爆**：bloom 1.0 + VoidRift 反向旋散 + 震屏 16（一次性预算）+ NPCDeath14 降调 |
| 250 | `NPC.life=0` 真死，掉落照常结算 |

---

## 6. 视觉技术方案

### 6.1 新建专属着色器（2 个，均 ps_3_0，程序化噪声自包含，不注册 ACMShaders，静态缓存于 AwakeningNetherHelper）

1. **`AwakeningNetherSoulflame.fx`** —— 魂焰（鬼绿芯→觉醒紫缘的程序化火舌）
   - FBM 火焰场 + 沿向拉伸 + 边缘撕裂；`uDir` 控制火舌流向，`uCoreColor/uEdgeColor` 双色阶。
   - 用途：魂焰舌吐息弹本体（替代 5 点 sprite 阵）、虚空魂雾领域地效（替代 10 blob 旋转）、口部聚能辉光、死亡魂焰。
   - 载体：自开合批 Additive 绘制方形占位（`ACMAsset` SoftGlow / MagicPixel），不读 screenTarget，**不占全屏名额**。

2. **`AwakeningNetherVoidRift.fx`** —— 虚空裂隙 / 奇点（吸积盘）
   - 对数螺旋吸积臂 + 事件视界暗核 + 边缘色散撕裂 + `uProgress` 旋开/闭合动画；`uLethal` 把吸积辉光推向致命红（出口门/活跃奇点）。
   - 用途：裂隙门本体（替代 DrawDimensionRift sprite 堆）、第三幕奇点、入场"冥渊之眼"、死亡内爆反旋。
   - 载体同上，不占全屏名额。

**批量绘制契约（实现落点）**：两个专属着色器不在各弹幕 PreDraw 中逐实例开合批，而是经
`AwakeningNetherScreenSystem.RequestSoulflame / RequestVoidRift` 在 AI(tick) 阶段入队，
由 ScreenSystem 在 `PostDrawTiles` 各用**一次 Begin/End（Immediate 逐实例改参）画完全部实例**——
每帧批次开销为常数 2 次，与火舌/魂雾/裂隙数量解耦；每类 decal 每帧上限 24 张，超出静默丢弃
（各弹幕自带紧凑亮核 sprite 兜底，危险 hitbox 始终可读）。

### 6.2 复用共享件

- `GenericWarp`（rift/void 模式）：全屏扭曲，只在 Head.PostDraw 单点申请 `RequestFullscreenSlot()`（维持现约定）；
- `ElementalScreenTint` / `ArenaRunic` / `RadialBloom`：经 `AwakeningNetherScreenSystem` 演出标量中枢（保留框架，新增 shockRing 冲击环标量供入场/死亡用）；
- `BeamGrad`：穿刺预警线、体节激光、脊柱能量流；
- `DissolveBurn`：噬魂卫星溶凝形成（现有，保留）。

### 6.3 体节绘制与重量感

- SummonMax 80 → 44（视觉密度与性能双赢）；
- 每节新增 `SpringOffset/SpringForce` 脊波链（MOTION §4：尾部一次冲量 → 波沿身体传播一秒）；绘制位置 = 逻辑位置 + 垂直于体轴的 SpringOffset（纯视觉，不影响 hitbox 公平性）；
- 体节绘制减为：单层辉光 + 主体 + 血量呼吸脉动；删除逐节的 per-sprite 能量连接线（换 Head 侧一条 BeamGrad 脊柱流，仅在脊波激活时亮起）；
- 头部拖尾保留 oldPos 但减为单层。

## 7. 性能与多人预算

- 全屏后处理：仅 Head.PostDraw 一处申请名额（GenericWarp），ScreenSystem 全部走非 screenTarget overlay——维持"同屏 ≤1"契约；
- Soulflame/VoidRift 为局部 decal 绘制，每帧各 ≤3 次实例（吐息弹合并绘制按需降档：距屏 >1.2 屏宽直接跳过）；
- Dust 预算：入场/死亡峰值 ≤ 120/帧，常态 ≤ 40/帧（现状 CreateAuraParticles+80 节各自冒粒子远超此值，将统一收口：体节粒子仅在脊波经过/高速时发射——速度门控，MOTION §7）；
- 弹幕上限：魂雾同屏 ≤ 10（新增计数护栏）、wisp ≤ 16、卫星 ≤ 6；
- 多人：AI 只读 `npc.ai[]/localAI[]/SendExtraAI` 同步字段；弹幕/召唤全部 `Main.netMode != MultiplayerClient` 判定；`Main.LocalPlayer`/演出标量只在绘制路径；引力场对**所有**范围内玩家生效（每端确定性执行）。

## 8. 实施清单

1. `Effects/AwakeningNetherSoulflame.fx`、`Effects/AwakeningNetherVoidRift.fx` 新建 + CompileFX 按名编译；
2. `AwakeningNetherHelper.cs`：新增着色器静态缓存 + `DrawSoulflame/DrawVoidRift` 绘制助手；保留全部既有 public 方法（武器仍在引用）；
3. `AwakeningNether.cs`（基类）：脊波链字段与传播、绘制简化、粒子速度门控、SummonMax 44；
4. `AwakeningNetherHead.cs`：状态机全面重写（Awakening/ActI/ActII/ActIII/Finality/DeathThroes/Transition + 手写循环表 + 多人同步 + 距离栓绳 + despawn 兜底）；
5. `AwakeningNetherBreath.cs`：魂焰舌重做（Soulflame 条带）+ 裂隙门 shader 化；
6. `AwakeningNetherVoidBolt.cs`：公平阀门（追踪截止/wind-up/最小距离）+ 配色收敛；SoulOrb 统一配色；
7. `AwakeningNetherMechanics.cs`：魂雾 shader 化 + 新增魂火余烬（SoulWisp）+ 卫星微调 + 激光帘幕保留；
8. `AwakeningNetherScreenSystem.cs`：新增 Soulflame/VoidRift decal 批量队列（Request* 入队 → PostDrawTiles 一批画完，常数批次开销）；
9. C# 验证（ReadLints 在本环境未生效，改用隔离 Roslyn 语义编译整仓源码 + 真实 tML 引用，输出到 %TEMP%，不触碰共享构建产物）：本文件夹错误清零；
10. 最后一步：两个 hjson 增补 SoulWisp 显示名（StrReplace 小步 + 立即回读验证，zh-Hans 与 en-US 同步）。
