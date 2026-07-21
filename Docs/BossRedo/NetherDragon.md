# NetherDragon 幽冥龙 —— Boss 重做设计文档 (V3)

> 单元: `Underworlds/Boss/NetherDragons/`（蠕虫型，月总后地府 Boss，120k HP）
> 关联单元: `AwakeningNethers/`（觉醒冥龙，另一代理负责，只读参考，不修改）
> 方法论: boss-fight-choreography SKILL / MOTION / PACING；简报 §3 流程

---

## 1. 现状诊断

以七大本能 + 失败模式清单过一遍 V2 代码，问题按严重度排序：

1. **传送门素材被浪费（差异化关键未立起来）**。`NetherPortal` 只是 fog 贴图多层旋转叠加的"烟圈"，没有"空间裂开"的门的形态；`TeleportWholeBody` 的段遍历依赖 `ai[2]` 链——而 `BasicWorm` 从未写入 `ai[2]`，循环体实际从不执行，所谓整虫传送只是**头瞬移、身体橡皮筋弹过去**，正是 SKILL 失败模式"teleports feel cheap"。
2. **入场 / 换阶段 / 死亡三大演出几乎为零**。召唤后直接刷出进入绕圈；换阶段只有 45t 无敌+抖屏；`OnKill` 即刻消失。蠕虫死亡分段爆炸链（PACING §6 现成配方）完全缺席。
3. **CircleAround 是死时间**。P1 在两次吐息之间有 200~250f 的椭圆绕圈（半径 400 还偏上 200），Boss 读作"脱战"，玩家 3 秒以上无事可做且不是刻意喘息——同时命中"boss orbiting reads as disengaged"与"boss waits for its own timer"两条失败模式。
4. **冲刺无重量**。`ChargeState` 一帧 `velocity = dir*18` 且无任何前摇/反向蜷缩/beep；预警锥线竟画在**冲刺开始之后**；18px/f 持续中速="sustained velocity"而非"brief extreme burst"；刹车 0.95 温吞。
5. **身体是死的**。60 节（SummonMax=60）纯跟随，无鞭波/弹簧次级运动；每节每帧按速度喷最多 ~15 个 dust（34+ 节 ×15 = 500+ dust/帧）性能极差还糊屏。
6. **雾系统贵而糊**。60 层环境雾 ×2 draw = 120 draw call；`GetFogDensityAt` 被每节每帧调用并遍历 60 层（≈2000 次距离计算/帧）；涡流层贡献趋近于零。
7. **多人安全缺口**。`stateTimer / exitPosition / scaleState / enrageTimer` 等核心状态全是实例字段且无 `SendExtraAI`；`AdvanceFromCircle` 用 `Main.rand` 各端各自掷骰 → 客户端状态漂移。
8. **P3 鳞珠机制方向不对**。任务定位是"**受击掉落**的逆鳞反击"，现状是定时蜕鳞+绕玩家公转的静态目标，轨道慢、无威胁、与受击无关。

结论: 有骨架（阶段门、telegraph 意识、怨念账接线）但演出与编排是 V2 半成品，**全面重做 AI 编排与视觉**，保留类名、进度标记、掉落、召唤。

---

## 2. 设计主题与幻想感

**"你不是在打一条蠕虫——你站在冥界之门前，门后的看门龙随时会掘穿现实钻出来。"**

幻想支柱：
- **门即舞台**。冥龙的一切强力行为都经由"冥界之门"：入场破门而出、P2 用门网络穿梭戳刺、换阶段被门吞回、终结技绕场开万魂门、死亡被巨门收葬。门的开裂本身就是 telegraph 语言：**裂缝发光（紫）→ 裂纹分叉+空间破碎（转红）→ 龙头轰出**，一套三拍让玩家永远"看见原因"。
- **掘墓者的重量**。蠕虫的力量感来自"钻行"：冲刺前反向蜷缩（late-snap 后撤）、一帧爆发 46px/f、复利加速、硬刹回鞭——一次输入沿 34 节身体传播成一秒的鞭波（MOTION §4 whip chain）。
- **鬼门开阖的节奏**。门开=压力波峰，门合=喘息谷。攻击循环表手工编排（PACING §2 hand-authored cycles），压制招与走位招交替，绝不连续两个重压。
- **怨念会讨债**。地府身份层保留：玩家造成的伤害记入怨念账；受击蜕出的逆鳞是"清账反制"窗口；账没清干净，终结技《万魂门》的门数更多。

配色语言（沿用 TelegraphColors，不自造红）：幽蓝紫 `NetherViolet` = 氛围/预备，鬼绿 `GhostGreen` = 致命火焰/魂束本体，纯红 `Lethal` = 即将命中的路径/落点预警。

---

## 3. 阶段结构与血量断点

| 阶段 | 血量 | 名称 | 核心机制 |
|---|---|---|---|
| P0 | — | 《开门》入场 | ~3.5s 破门演出，无敌不攻击 |
| P1 | >60% | 《巡墓》 | 蛇形游弋压近 + 吐息锥 + 掘墓冲刺；身段留幽火残痕（保留） |
| 换阶段 | 60% | 《裂土》 | 被门吞没 → 1s 静默 → 三符阵两假一真 → 真出口轰出 |
| P2 | 60–30% | 《裂土》 | 门梭三连（teleport-loop 戳刺）+ 魂束扫射 + 强化吐息 |
| 换阶段 | 30% | 《噬墓》 | 仰天怒吼蜕鳞演出，逆鳞被动开启 |
| P3 | ≤30% | 《噬墓》 | 受击蜕逆鳞（清账反制）+ 双向扫射 + 二连冲刺 |
| 终结技 | ≤15% 一次 | 《万魂门》 | 绕场 2~4 门（怨念账定数）依次扫射交叉火网，龙从最后一门轰出 |
| 死亡 | 0 | 《葬门》 | 尾→头逐节爆炸链（beep 加速）→ 巨门收葬 → 砰然合拢 |

阶段读法保持 `Phase` 属性按血量比例推导（无需额外同步）；`SummonMax` 60 → **34**（整条龙 1.5 屏内可读，速度感提升，弹幕/性能双赢）。

---

## 4. 招式编排表

循环表（hand-authored，索引存 `ai[3]`，确定性推进，无 `Main.rand` 分支 → 多人天然同步）：

- **P1**: `Weave(110) → BreathCone → Weave(80) → GraveDash → Weave(55) → BreathCone → GraveDash` 循环
- **P2**: `PortalShuttle → Weave(70) → BeamSweep → GraveDash → PortalShuttle → Weave(55) → BreathCone(双段)` 循环
- **P3**: `PortalShuttle(4 戳) → BeamSweep(双向) → Weave(45) → GraveDash(2 连) → BreathCone(双段)` 循环 + 逆鳞被动 + 万魂门(一次性插入)

| 招式 | 前摇 | 爆发 | 收招 | 预警方式 | 公平阀门 |
|---|---|---|---|---|---|
| Weave 蛇形游弋 | — | 45~110f | — | 非攻击（连接拍/喘息） | 速度 9~13，保持在玩家 260~420px 环内，距离栓绳 |
| BreathCone 吐息锥 | 55f 锥形 shader 预警（紫→红收口）+ 反向蜷缩 130px | 释放 1f（P2/P3 双段各 1f，间隔 30f） | 40f | 锥形 SDF 预警 + beep + 蓄力泛光 | 预警期无伤；锥内扇形弹速分层 8.5~12 留缝隙 |
| GraveDash 掘墓冲刺 | 40f 对齐减速（×0.9/f）+ 36f 红色冲刺线 + pow(t,8) 后撤 220px + beep | 1f set 46px/f，×1.015/f 复利 12f | 15f 硬刹 ×0.86 + 尾鞭波 + 刹车点 6 发环形慢火 | DrawBeam 红色路径线 + beep 固定 36f 前置 | 接触伤害仅 \|v\|>24；冲刺越过玩家 250px 强制刹车；P3 二连时第二段重新预警 |
| PortalShuttle 门梭三连 | 每戳: 出口门裂缝 12f + 门→预测点红色戳刺线 30f | 龙头 52px/f 直线 12f 戳出 | 3 戳后 60f 门全关喘息（可打窗口） | 门的裂缝生长本身 + 红色戳刺线 | 玩家距出口门 <200px 时该戳延迟 12f（最小距离阀）；teleport-loop 无"飞回"死时间 |
| BeamSweep 魂束扫射 | 75f: 扫射扇形两界红线 + 锥形 shader 扇面（后 25f 转红） | 90f 扫射，0.0155 rad/f 恒速 | 25f | 扫射全轨迹扇形预警（遵守扫射预警线规范） | 恒定角速度可预跑；扇形 80°留安全侧；头部后坐微退 |
| ReverseScale 逆鳞（被动） | 蜕出弧线飞行 40f 内无伤 | 入轨绕玩家 260px | — | 蜕鳞音 + 红色菱形发光体 | 击毁 → 掉 1 颗心 + 怨念 −10%；480f 超时 → 碎裂 + 龙暴怒 240f（移速 ×1.35、火幕 +2 发）+ 一道 45f 预警暴怒吐息 |
| MyriadGates 万魂门（≤15% 一次） | 龙被门吞 45f 静默 + 各门符阵/裂缝依次预警 75f | 各门错相 35f 依次扫射 40°（共 ~210f） | 龙从最后一门轰出（30f 红线预警）| ArenaRunic 符阵 + 门裂缝 + beep 依次 | 门环半径 620px 环内可跑位；束恒速有安全缝；全程清了旧弹幕 |

暴怒（逆鳞超时惩罚）只提升移动/压迫密度，不缩短任何前摇——前摇时长是不可侵犯的可读性底线。

---

## 5. 入场 / 换阶段 / 死亡三大演出脚本

### 5.1 入场《开门》（~210f，无敌、不攻击）
1. 0f: 头瞬移至玩家上方 -560/侧 300 的空间点并隐身，身体 stack 于同点；该点开始**裂缝**（Gate shader uCrack 0→1），低频 rumble 渐起（shake 1→2.5）。
2. 0–70f: 裂缝红光渐强、裂纹分叉生长——纯 telegraph，玩家有充足时间注意到"那里要出事"。
3. 70–100f: beep ×3 加速，裂纹密度峰值，空间破碎白闪（RadialBloom 短脉冲）。
4. 100f: 门轰然全开（uOpen 20f 内 0→1），riftWarp 拉起。
5. 105f: 龙头以 40px/f 破门俯冲而出，身体从门心鱼贯涌出（stack + ChangePos 自然拉出），shake 10、Roar、雾涟漪 3.0。
6. 105–150f: 冲出后弧线拉起减速。
7. 150–210f: 悬停亮相，**60f 凝视玩家的静止**（Menace is mostly stillness），身体涌完，门在身后合拢。
8. 210f: 解除无敌，进 P1 循环表。

### 5.2 换阶段 P1→P2《裂土》（~200f）
清全场己方弹幕 → 龙正后方裂开大门把龙**倒吸**进去（velocity 强制向门 + 吸入粒子）→ 整虫收入门内（无敌、屏外 stack）→ 门合拢，**60f 全场只剩雾**（浓度 +0.15，静默恐怖）→ 玩家周围三处 ArenaRunic 符阵依次亮起（两假一真，真符阵最后 35f 转红）→ 真出口开门，龙 34px/f 轰出恢复战斗。假符阵熄灭。

### 5.3 换阶段 P2→P3《噬墓》（~130f）
清弹 → 龙减速仰起、全身 ribbon 泛红脉冲、怒吼（Zombie104），蜕鳞演出：从身体中段弹出 2 枚逆鳞入轨（首次演出化蜕鳞，此后转为受击被动）→ shake 9、雾色偏紫红 → 战斗继续。短促，不打断节奏。

### 5.4 死亡《葬门》（~280f，CheckDead 拦截）
1. 0–40f: 清弹、关门；龙硬刹抽搐（×0.9/f + 抖动），长吼 pitch −0.6。
2. 40–200f: **从尾到头每 10f 引爆一节**（粒子爆 + shake 3 + 每节音效 pitch 随进度上行 = beep 加速变体），已爆节隐灭；头部缓慢挣扎爬升。
3. 200f: 头下方 240px 裂开一道 2× 巨门。
4. 200–260f: 头被门加速吸入，riftWarp 0.9、全屏收束。
5. 260f: 头没入瞬间巨门**砰然合拢**（20f），冲击涟漪 3.5、**shake 16（全场唯一最大震）**、RadialBloom 满脉冲、冥雾散尽。
6. 275f: 真死于门位置——掉落从门口喷出，`NetherDragonDownedSystem.OnNetherDragonKilled()` 照常触发矿脉显形播报。

---

## 6. 视觉技术方案

新建专属着色器（全部 `NetherDragon` 前缀、ps_3_0、`ModContent.Request<Effect>` 静态缓存，不注册 ACMShaders）：

| 着色器 | 类型 | 用途 |
|---|---|---|
| `NetherDragonGate.fx` | 屏幕空间 SDF decal | 冥界之门本体：竖缝椭圆门。uCrack=裂纹前兆（噪声分叉裂缝+红光），uOpen=开裂度（细缝→椭圆虚空），门内暗渊旋涡+星点、边缘鬼绿焰、外辉。`NetherPortal.PreDraw` 经 `DrawScreenSpaceDecal` 绘制 |
| `NetherDragonRibbon.fx` | TriangleStrip 条带 | 龙躯"冥焰披风"：头亮尾暗、鬼绿→幽紫渐变、噪声焰舌沿身流动、暴怒转红。head 每帧收集全段 spine 画一条 ribbon 于 NPC 层下；亦用于火焰弹小拖尾 |
| `NetherDragonCone.fx` | 屏幕空间 SDF decal | 锥形/扇形预警：uDir/uSpread/uLength/uProgress，紫→红收口 + 边界亮线 + 内部微填充。吐息锥与扫射扇共用，由 ScreenSystem 在 PostDrawTiles（弹幕层之下）绘制 |

复用共享件：`GenericWarp`（fog/rift 全屏后处理，走唯一名额契约，保持在 head.PostDraw）、`ArenaRunic`（符阵落点预警）、`RadialBloom`（吐息/破门泛光）、`BeamGrad/DrawBeam`（冲刺线/戳刺线/扫射界线/魂束本体）、`ACMShaders.NoiseTexture`。

实施细节落定（与代码一致）：
- 门贴花由 `NetherDragonScreenSystem.PostDrawTiles` 统一绘制（**实体之下**，龙从门中钻出不被门内暗渊遮挡）；`NetherPortal.PreDraw`（弹幕层）只画应压在实体之上的红色戳刺线与破开白闪。
- 门的生成参数全部打包进 `Projectile.ai[]`（`ai1 = holdTime*1000+crackTime`，`ai2` 负值=假门），各端确定性推进生命周期，多人零额外同步；红色戳刺线由 `crackTime ≥ 24f` 确定性推导（长裂纹=攻击门）。
- 假门在裂缝转红**之前**枯萎——保证"红=致命"语义诚实（红过的门必然有龙）。
- 着色器关闭 (`MythologyConfig`) 时：门沿椭圆缘 dust 描点、锥形预警两界线 dust 描点——核心预警信息不缺席。

其他绘制升级：
- 身体各节绘制加 spring 偏移（whip 链视觉），受击闪白、暴怒泛红。
- `NetherFlameTrail` 残痕改用 SoftGlow+Fog 双层 + 底部地焰舌，紫（预备）→绿（致命）语义保持。
- `NetherScaleOrb` 逆鳞改为红芯菱形双层 glow + 超时进度环（可读倒计时）。
- 雾系统瘦身：环境雾 60→28 层、删除涡流类、`GetFogDensityAt` 去循环化（只按中心距离），身体逐节 dust 喷发全删（由 ribbon 接管体积感）。

---

## 7. 性能与多人预算

- **粒子**: 删身体每帧 500+ dust；各招式粒子峰值 <120/帧；粒子数量随 `MythologyConfig.TrailQuality` 降档。
- **Draw**: 雾 120→56 draw；ribbon 每帧 1 次 strip（~200 顶点）；门 decal 每门 1 draw（同屏 ≤4）；全屏后处理仍只有 head.PostDraw 一处（名额契约）；Cone/Runic/Bloom 均为不读 screenTarget 的廉价 overlay。
- **分配**: ribbon spine 数组静态复用；无每帧 new 纹理/Effect；无热路径 LINQ。
- **多人**: 状态机四元组入 `npc.ai[0..3]`；`breathDir/exitPosition/anchor/循环表索引/暴怒/逆鳞计数` 走 `SendExtraAI`；所有状态切换 `netUpdate = true`；弹幕/NPC 生成仅服务器（`netMode != MultiplayerClient`）；纯视觉标量（warp/bloom/runic/cone）留本地。循环表推进确定性化，去掉全部 AI 分支 `Main.rand`。
- **进度安全**: `NetherDragonDownedSystem`、掉落表、召唤条件、`SoulBannerPlayer` 引用的类型全部不动。

---

## 8. 实施清单

1. `Effects/NetherDragonGate.fx` / `NetherDragonRibbon.fx` / `NetherDragonCone.fx` — 新建 + CompileFX 按名编译过零。
2. `NetherPortal.cs` — 重做为四段生命周期（裂纹→破门→稳定→合拢），Gate shader 绘制，公开 `OpenAmount/StartClosing/裂纹时长` 参数（ai0=朝向, ai1=状态）。
3. `NetherDragon.cs` — SummonMax 34；whip 弹簧链（`SpringImpulse` 传播）；ribbon spine 收集；删逐节 dust；P1 留痕保持；受击/暴怒着色。
4. `NetherDragonHead.cs` — 全重写：Intro/Weave/BreathCone/GraveDash/PortalShuttle/BeamSweep/PhaseRift/MyriadGates/DeathThroes 状态机 + 循环表 + 三大演出 + 逆鳞受击蜕落控制器 + `SendExtraAI` + `CheckDead` 拦截。
5. `NetherLaserBeam.cs` — 重做为扫射魂束（预警 75f + 恒速扫射 90f，ai0=起始角/ai1=扫向）。
6. `NetherScaleOrb.cs` — 重做为逆鳞（受击蜕落、480f 倒计时环、击毁奖励/超时暴怒回调）。
7. `NetherFlameProjectile.cs` / `NetherFlameTrail.cs` — 视觉升级（ribbon 拖尾/地焰），语义不变。
8. `NetherDragonFogSystem.cs` — 瘦身（28 层、删涡流、密度函数 O(1)）。
9. `NetherDragonScreenSystem.cs` — 扩展 Publish（cone 预警参数组），Cone shader 绘制点。
10. ReadLints 清零全部改动 .cs；最后 StrReplace 微调 hjson（怨念龙鳞 → 幽冥逆鳞，zh/en 同步）并重读验证。
