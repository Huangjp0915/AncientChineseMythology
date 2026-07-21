# Corpses（尸骸 · 枉死万骸之主）重做设计文档 — Boss 重做工程 V3

> 单元：`Underworlds/Boss/Corpseses/`（头颅 Corpses + 双手 CorpsesHand + 弹幕 + Items）
> 力度判定：**全面重做**。V2 补强只覆盖了引魂大阵/城门闭合两个 set-piece 的骨架，常态编排、手部动作波形、
> 三大演出节拍、专属视觉全部缺位。

---

## 1. 现状诊断

以 choreography skill 七大本能 + 失败模式清单为透镜：

1. **手部动作没有波形（本能 #1/#2 全违）**：`Reaching` 是 20 帧 QuadOut 匀速伸出；`Slashing` 是 SineInOut
   对称插值——挥砍读作"飘过去"。没有任何一招具备 anticipation(慢) → burst(poly 高次瞬发) → recovery(缓收)
   的三段结构。素材指向的"抬手蓄势→狠拍落地"招式（PalmSlam）根本不存在。
2. **伤害窗口与视觉不对齐（本能 #6）**：`CanHitPlayer` 对 Slashing/Reaching **全程**放行——前摇期贴脸就掉血。
   `Grabbing` 是 2 秒无预警追踪，命中后直接 `player.Center = NPC.Center` 锁位 60 帧——多人下玩家位置由本地端
   控制，该逻辑完全失效，单机下则是无反制的剥夺控制。
3. **调度失控（PACING §2 反面）**：BasicAttack 用 `PhaseTimer % 80/120/150/200/300` 六路取模轮询叠加触发，
   相互冲突时 `TriggerAttack` 静默丢弃；同时 `HandleIdleState` 里手还会 `Main.rand` 自主攻击——双头指挥，
   节奏是随机噪声而非设计的波形。且 `Main.rand.NextBool()` 直接做 AI 分支 → 多端不同步（`TriggerAttack`
   也不设 `netUpdate`）。
4. **主体失重（本能 #3）**：头颅永远 `Center += (target-Center)*k` 软悬浮，吐弹无 recoil、被拍落无震感反馈、
   拖尾常开不随速度门控（dressing 常开 = 噪声）。
5. **演出缺失**：入场只有从下方升起+一声吼；死亡是 `OnKill` 撒 60 个 dust；换阶段无清弹、无定格；无专属
   着色器、无 ScreenSystem、无 Music 字段（其他地府 Boss 均有 Underworld 曲目）。“尸山血海压迫感”为零。
6. **状态机/网络隐患**：Intro 升起穿过玩家全程带接触伤害；`CorpsesShadowOrb.OnKill` 用
   `Main.myPlayer == Projectile.owner` 判定生成敌对分裂弹（多人下服务器弹幕 owner 语义错误）；
   `BoneToss`/`ClapStrike` 伤害硬编码 40/50 不走 `GetBossDamage`。
7. **可保留的好底子**：引魂大阵（站生门破阵 vs 硬抗，双轨结算）与城门闭合（收缩牢笼）两个 set-piece 的
   **机制**是好的；`Controlled/Channeling/Stunned` 手部编排接口、骨→魂→骨 dissolve 过渡、ArenaRunic 法阵
   绘制、UnderworldField 身份层接入（魂蚀/冥律）全部保留并强化。

## 2. 设计主题与幻想感

**“枉死城·万骸之主”**——枉死城中含冤而死者的骸骨怨气凝成的巨颅与一双判官之手。给玩家的体验：

- **被一双巨手"审判"**：手是主角。抬手时天光被遮蔽般的压迫（够慢、够高），拍落是一锤定音的判决
  （瞬发、震屏、顿帧）；合掌是"夹棺"，给足穿越缝隙。
- **尸山血海的怪诞低语**：全程底部尸雾上涌、画面去饱和压暗、魂点上飘（专属全屏着色器，走名额契约）；
  唯一的红色只出现在"即将落下的判决"上（TelegraphColors.Lethal）。
- **色彩语言**：骨白（208,232,205）+ 鬼绿 GhostGreen（魂火/DoT）+ 幽蓝紫 NetherViolet（冥律/氛围）+
  唯一红 Lethal（致命落点/轴线）。

## 3. 阶段结构与血量断点

| 阶段 | 触发 | 内容 |
|---|---|---|
| Intro | 出生 | 入场演出 ~250 帧（§5），全程无伤害交互 |
| P1 役骸 | 入场后 | 节拍表 Score-1：单手招式教学循环 |
| 引魂大阵 | **HP 60%** 单次 | set-piece 保留强化：站生门破阵（奖励头颅破绽 5s）/ 硬抗（冥律+镇压波），换阶段清弹 |
| P2 判骸 | 大阵结算后 | 节拍表 Score-2：双手错拍连击 + 万骸旋冢 + 斜轴合掌 |
| P3 城闭 | **HP 30%** | 城门闭合终幕：收缩牢笼 + 三招高压轮替，清弹进场 |
| 死亡演出 | HP≤1 锁血 | ~240 帧崩解大戏（§5），演出毕真死 |

“把最好的留到后面”：双手错拍连拍、旋冢、Marker 骨雨群仅在 P2/P3 出现。

## 4. 招式编排表

所有招式三段波形；**伤害窗口只在爆发段**（前摇/收招零伤害）；预警时长按 `TelegraphColors.TelegraphTicks` 分级。

| 招式 | 前摇(帧) | 爆发(帧) | 收招(帧) | 预警 | 公平阀门 |
|---|---|---|---|---|---|
| **崩掌拍落 PalmSlam** | 抬手 38 + 顶点悬停 14（粒子熄灭 pre-silence） | 拍落 5（poly12 直落）+ 落地锁定 16 | 收回 30 | 落点 Lethal 光柱（着色器 uMode0，渐强 45f）| 落点在抬手时锁定不追踪；锁定期是输出窗口；伤害仅拍落+锁定前 8f |
| **白骨横扫 BoneSweep** | 弧线后摆 34 + 停 8 | 扫过 10（poly16 弧线）| 硬刹 ×0.68 + 收 26 | 扫掠线 Lethal 束（轴线着色器 uMode2, 42f）| 扫线在后摆锁定；仅 strike 段可命中 |
| **指骨连环 BoneVolley** | 后摆 22 | 3 波×(甩 6 + 后坐 10) 扇形 5 发骨镖 | 收 24 | 手臂后摆姿态 + 鬼绿聚焦粒子 | 骨镖初速 20% 起步 12f 内升满（换招防telefrag）|
| **合掌夹击 ClapPincer** | 双手飞位 26 + 反向拉开 30 + 静止 12 | 合拢 4（poly 高次）+ 合击锁定 18 | 弹开 22 + 收 26 | 合拢轴 Lethal 束贯穿双手（uMode2，46f 渐强） | 轴垂直方向永远敞开；合击点=轴中点不追踪；冲击波弹从合点向外（站中心即安全芯）|
| **魂火吐息 SoulVolley**（头） | 眼焰收束 40（converging 粒子） | 3 发魂灯球，每发间隔 12、头部 recoil | 20 | 眼焰亮度即进度条 | 球限速 9、近身 60px 停止转向、5s 自熄 |
| **万骸旋冢 SpiralTomb**（P2/P3） | 双手脱体入轨 30 | 环绕 110（速度渐增）→ 收口预警 26 → 收口 18 | 展开/回体 30 | 收口前轴线束变红 + 音调上升 | 收口轴垂直向敞开；整招限 2 循环 |
| **骨雨审判 BoneRainVolley**（P3） | Marker 落点渐强 50 | 落地窗口 16 | — | 地面红条（保留 Marker，柱体升级 uMode0） | 落点静止；预告期零伤害 |

**调度**：每阶段一张手写节拍表（authored cycle，PACING §2），表项=（帧点，动作，头颅锚位）。表间距 ≥ 招式
最大时长+呼吸拍（30~50f 连接拍：手回位、头颅呼吸下沉）——不存在"到点手还忙"的丢拍。决策零随机（帧表+
`ritualGateSeed` 类确定性随机），`Main.rand` 只用于纯视觉。

## 5. 入场 / 换阶段 / 死亡三大演出脚本

- **入场（~250f）**：尸雾自 0 涌起（miasma 0→0.55）→ 玩家侧下方魂火收束、震动渐强（rumble≤3）→ 第 90 帧
  头颅破土上冲（12f poly 上冲 + 震屏 13 + 骨尘喷发）→ 双手先后破雾 dissolve 现身（左 120f、右 138f）→
  眼焰点燃（闪 3 次后稳定）→ **60 帧完全静止凝视**（menace is stillness）→ 第 250 帧低吼开战。
  全程 `dontTakeDamage` + 零接触伤害。
- **换阶段 1（60% 引魂大阵）**：清弹 → 公告"引魂大阵" → 双手 dissolve 脱体飞坛（锁链光束+法阵展开、
  尸雾加深 0.75）→ 60s 内三段结构照旧（起阵 90f / 收缩 360f / 结算 70f）。打断成功：双手坠地硬直 300f +
  头颅破绽（受伤 ×1.6 + 眼焰熄灭 + Safe 色呼吸高光）。
- **换阶段 2（30% 城门闭合）**：清弹 → 公告"枉死城门闭合" → 一声闷门响 + 震屏 12 + 尸雾至最浓 0.9 +
  ArenaRunic 牢笼常驻 → 进入 P3 高压轮替。
- **死亡（~240f，HP≤1 锁血）**：清弹 + 停手 → 双手先后坠地崩解（dissolve + 骨尘瀑布，第 30/70 帧）→
  头颅缓缓下沉、眼焰忽明忽暗，骨裂声按递减间隔阵列加密（36→6f，音调上升）→ 第 180 帧眼焰熄灭、
  **25 帧全静默**（粒子/震动全停，miasma 骤降）→ 第 205 帧崩解爆发：白闪对比帧（miasma uFlash，一次性）+
  震屏 16 + 骨片喷泉 + 尸雾散尽 → 真死结算（掉落/downed 旗标不变）。

## 6. 视觉技术方案

**新建专属着色器（全部 ps_3_0，Corpses 前缀，Boss 代码内 `ModContent.Request<Effect>` 静态缓存，不进 ACMShaders）**：

1. `CorpsesMiasma.fx` —— 全屏尸雾后处理（读 screenTarget + s1 共享噪声）：底部 FBM 尸雾上涌 + 画面
   去饱和压暗 + 暗角 + 上飘魂点 + `uFlash` 白化对比帧（死亡爆发一次性）。**唯一全屏件**，由
   CorpsesScreenSystem 经 `RequestFullscreenSlot()` 名额契约绘制，尊重 `MythologyConfig`。
2. `CorpsesBoneRing.fx` —— 预警/冲击多模式 decal（屏幕空间，非 screenTarget，不占名额）：
   uMode0=落点光柱（PalmSlam/Marker），uMode1=扩张骨裂冲击环（拍落/合掌 impact），uMode2=轴线束
   （合掌/横扫/旋冢收口预警）。
3. `CorpsesSoulFlame.fx` —— 程序化鬼火（SoftGlow 载体，Additive）：内核骨白外焰鬼绿、顶部拉丝、闪烁。
   用于头颅眼焰（状态广播：蓄力变亮、破绽熄灭）、魂灯球本体、手部脱体掌心焰。

**复用共享件**：ArenaRunic（大阵/牢笼）、DrawBeam（锁链/生门/预警轴辅助）、DrawRadialBloomAt（impact 泛光）、
DissolveBurn（脱体/崩解）、ACMAsset.SoftGlow/Smoke、共享 NoiseTexture、ACMUtils 缓动/震屏。

**尸雾接入点（实现调整）**：不另建 ScreenSystem——`CorpsesMiasma` 是读 screenTarget 的后处理，须在活动
批内执行，故由 `Corpses.PostDraw`（MustAlwaysDraw 保证离屏也调用）直接 `RequestFullscreenSlot()` 申请
名额绘制；miasma/deathFlash 为本地视觉标量，随阶段 lerp 推进，Boss 消亡即随实体消失。

**权重/反馈**：吐息 recoil 4px；手 impact 时头颅"受震下沉回弹"弹簧（headBobVel）；拖尾与 dissolve 残影
仅在爆发段出现（速度门控）；震屏预算：impact 8~10、入场 13、死亡 16（统一 max-not-additive 预算）。

## 7. 性能与多人预算

- 着色器 `static Asset<Effect>` 缓存一次；全屏仅 Miasma 且走名额契约；BoneRing 每帧≤3 次 decal 绘制，
  `intensity<0.01` 直接 return；粒子每招≤60、常态≤3/帧。
- AI 决策全部由同步计时器 + 节拍表驱动（零 `Main.rand` 分支）；手部状态切换一律 `netUpdate=true` +
  `SendExtraAI` 全量同步；弹幕/召唤仅 `Main.netMode != MultiplayerClient`；抓取锁位机制**删除**，改为
  合掌/拍落命中叠魂蚀+冥律（身份层记账，多人安全）。
- `Main.LocalPlayer`/纯视觉仅存在于 Draw/ScreenSystem 路径；服务器零绘制。
- 掉落表、`downedCorpses`、BossLoot 保持不变；补 `Music`（复用现有 Underworld 曲目，不新增文件）。

## 8. 实施清单

1. `Effects/CorpsesMiasma.fx` / `CorpsesBoneRing.fx` / `CorpsesSoulFlame.fx` 新建 + 按名编译至退出码 0。
2. `CorpsesHand.cs` 重构：执行器化（节拍表指令驱动），新招式状态机（PalmSlam/Sweep/Volley/ClapPincer），
   严格伤害窗口，删除自主攻击与抓取锁位，Controlled/Channeling/Stunned 保留，全状态超时兜底，
   Materializing/Dying 演出态新增，超距骨臂化魂链（DrawBeam）。
3. `Corpses.cs` 重写：Intro/Score1/Ritual/Score2/CityGate/Death 六段结构，节拍表调度，头颅锚位+受震弹簧+
   recoil+眼焰绘制，三大演出，清弹阀门，PostDraw 尸雾名额，Music。
4. 弹幕重做：`CorpsesShadowOrb`（魂灯球）、`CorpsesClapWave`（冥掌冲击波）、`CorpsesBoneShower`（骨镖）、
   `CorpsesBoneRainMarker`（光柱升级+波次错拍）。类名/键全保留。
5. ReadLints 清零 → 最后小步编辑两个 hjson（新增 Awaken/Collapse 公告键，zh/en 同步，编辑后立即重读验证）。
