# 敖广（东海龙王）重做设计文档 — Boss 重做工程 V3

> 单元：`Celestias/Boss/AoGuangs/`　主题：海潮 / 海啸 / 定海威仪
> 与三弟区分：敖钦=炎热、敖顺=风暴、敖闰=冰霜；敖广 = **纯水的质量与秩序（潮汐节律）**。
>
> 收敛备注：本单元执行期间存在一个重复派发的并行进程反复交叉写入（03:13~04:17，涉及
> AI/相位文件/设计文档/着色器产物）。该进程已于 04:21 终止；本文档对应 04:30 后收敛的**最终
> 一致实现**（以本代理版本为基底，吸收对方"潮涌立柱单向波浪推进"一处更优设计）。对方遗留的
> 三个未被任何代码引用的着色器（AoGuangSurfaceCaustics / AoGuangWaterBody / AoGuangTsunami
> 的 .fx/.fxc）保留在 Effects 下，惰性无害，去留由协调者定夺。

## 1. 现状诊断

以 choreography skill 七大本能与失败模式清单为透镜：

1. **失重（本能 #1/#2/#3 全缺）**：所有移动是 `velocity = Lerp(velocity, toTarget*k, 0.1)` 式悬停漂移；冲刺是 25f 红点线后直接 `velocity = dir * 28`，无反向吸身、无发射后硬刹、无射击后座。龙王读作"会飘的贴纸"。
2. **无蛇形语言**：龙形 Boss 却没有盘旋-穿刺节奏；本体绘制只是同贴图三层叠加 + oldPos 残影，没有龙躯。
3. **攻击选择纯随机（PACING §2 反例）**：`Main.rand.Next(7)` 可连续重复同招；且各端各自随机（多人下客户端预测错招）。招与招之间零连接拍，一阶段每招后固定 180f 巡游空转（"Boss 等自己的时间表"）。
4. **三大演出全缺（PACING §6）**：入场 = lerp 上升 + 吼；换阶段 = 原地转圈粒子 + `dontTakeDamage`；死亡 = OnKill 一圈 dust 直接消失。无一达标。
5. **公平性问题**：TornadoRush 是全程追踪的 homing 冲刺（不可读）；换阶段不清弹；GiantWhirlpool/AbyssalVortex 直接大力拉扯 `player.velocity`（1.5/f 近乎控制玩家）；WaterSpike 落点不做地面探测（浮空悬柱）；接触伤害常开。
6. **性能/多人**：BarrierWaterTornado 每帧 162 段×3 层 = 486 次 draw；AbyssalVortex 118 层同心 overdraw；`random/seed` 死代码；编排字段（clawPositions、vortexRadius 等）未同步。
7. **可保留资产**：V2 的 GenericWarp/Tint/Bloom"海之沉浸"标量框架结构良好（升级为专属着色器）；TelegraphColors 已接入；掉落表/downed 标记/BossChecklist 完整。

**结论：全面重做 AI 编排与视觉**（保留全部 public 类型名、掉落、进度接入），屏幕演出升级为专属着色器。

## 2. 设计主题与幻想感

**「东海潮主 · 定海龙王」**。四海龙王之长，掌定海之权。他的海不狂怒（狂怒是敖顺的风暴），而是以**不可抗拒的节律**行进：涨潮 → 憋潮（静默）→ 溃堤。玩家的功课是"读潮"：每个大招前海面必先退潮/静止（负空间预警），然后巨浪层涌。

本体动作骨架：**蛇形巡曳（慢而威仪）→ 定身蓄势（静止即威压）→ 雷霆穿刺（快而直）**。速度感全部来自对比：全场只有穿刺的 9~12 帧是真正快的。

## 3. 阶段结构与血量断点

| 断点 | 阶段 | 内容 |
|---|---|---|
| 入场 | Intro (~190f) | 背景冲镜(fake-Z 6→0) + 60f 静止威压 + 举戟→戟落斩拍 + 封路龙卷 |
| 100%~65% | P1 东海巡游 | 潮弓三连 / 穿刺巡游×2 / 潮涌立柱 |
| 65% | 相变一「没顶」(~320f 可玩) | 受创反冲 → 冲天离场 → 水位吞屏 + 两波穿越浪墙 → 破水回场 |
| 65%~30% | P2 怒潮压境 | 浪墙层涌(签名) / 水龙卷投掷 / 龙息水柱 / 穿刺巡游×3 |
| 30% | 相变二「定海·潮止」(~180f) | 清弹 → 潮止 70f(全屏水效退干+无声举戟) → 戟落 impact frame(一场唯一) → 赤目 |
| 30%~0% | P3 沧海倾覆 | 深渊漩涡(签名) / 终潮天倾(压箱底) / 狂龙连刺 / 浪墙·龙息(强化版) |
| 0% | 死亡「潮退归海」(~350f) | CheckDead 接管：失控抽搐 → 螺旋上升+加速鼓点 → 顶点定格 20f → 水爆(shake 19) → 泡沫沉降 → 真死掉落 |

攻击选择：**每阶段洗牌袋**（Fisher-Yates，反连击，服务器专选 + netUpdate 同步，客户端从 ai[0] 跟随）。招与招之间必经 26~56f「巡曳」连接拍（蛇形缓游 + 到位提前退出）。

## 4. 招式编排表（60fps 帧数）

| 招式 | 前摇 | 爆发 | 收招 | 预警方式 | 公平阀门 |
|---|---|---|---|---|---|
| 潮弓三连 (P1) | 蛇游入位 45f，龙首汇聚流光 | 3 波扇形潮矢(5/7 枚)，波间 22f，每波后座 9px/f | 30f | 汇聚粒子(形状) | 潮矢初速 8 自加速至 19；扇形 50°/62° 均布留缝 |
| 穿刺巡游 (P1/P2) | 盘旋 40f(到位早退) + 锁线 24f(转向衰减 0.4→0.08) + 反吸 12f(t³×15px/f) | 穿刺 11f @46/50px/f 单帧点火 | 硬刹 ×0.72 ×10f + 蛇游 16f | 哨音固定提前 36f + Lethal 预测线渐强；锁线 14f 后瞄点冻结 | 穿刺零转向；接触伤害仅穿刺帧(CanHitPlayer)；刺间 26f 恢复拍 |
| 潮涌立柱 (P1) | 定身抬首 40f + 地面泡沫预告 | 7/8 根水柱**自随机一侧波浪推进**依次喷发(间隔 8f；36f 红色警戒柱→20f 喷起→26f 保持) | 20f 收束 | 每根独立红色警戒柱 + 推进方向可读"潮从哪边来" | 落点 FindGroundY 贴地；柱距 170px 留缝；伤害仅全喷起时 |
| 浪墙层涌 (P2 签名/P3 强化) | 退至上游 60f 举戟；全场水珠倒吸(负空间预警) | 3 波(P3 4 波)整面浪墙 @9/10px/f，波间 100/90f；出浪后座+挥戟斩拍 | 随末波结束 | 浪体前沿 Lethal 亮线；缺口 Safe 翠玉描边(着色器) | 缺口半宽 150/130px 随机换位；成型 26f 无伤害；缺口碰撞豁免留 0.8 容差 |
| 水龙卷投掷 (P2) | 甩尾大摆幅蓄势 30f ×2 | 掷出行走水龙卷(落点 30f 红标→成柱→2.6px/f 平移) | 24f | 落点红标横线 + 螺旋粒子 | 无追踪可跳越；同场 ≤2 根；成柱前无伤害 |
| 龙息水柱 (P2/P3) | 蓄力 60f：身体后漂(counter-motion)、流光 ∝√charge 且 72% 静默截断、震屏 ∝charge³ | 水束扫射 90/100f，转速恒定 0.020/0.022 rad/f | 26f | Lethal 路径线；后 1/3 蓄力角度锁死 | 恒速扫射可绕行；扫向顺玩家动向(服务器定+同步) |
| 深渊漩涡 (P3 签名) | 升空 40f(早退) + 落涡成型 60f | 定点巨涡 300f(拉力 4s 渐强至 0.35) + 切向穿刺 ×3(锁线 22f+反吸 6f+刺 12f@48) | 崩解 70f | ArenaRunic 红环=碰撞边界；全屏向心折射; 螺旋吸入粒子 | 涡不追踪；拉力可对抗；穿刺线 Lethal 渐强 |
| 终潮天倾 (P3 压箱底) | 升空 50f + 半场 Lethal 幕布 60f(加速读秒鼓点 ×5) | 半场天倾巨浪 30px/f 坠落；反向再一次 | 贯场穿刺(锁线 30f→54px/f 16f)+28f | 幕布=危险半场整面呼吸红 + 分界亮线(着色器 warn 模式) | 标记 60f 足够换场；两次间 40f 窗口；幕布期零伤害 |
| 狂龙连刺 (P3) | 每刺锁线 22f + 反吸 10f | 刺 9f @52px/f ×3；第三刺终点浪爆(环形 10/12 潮矢初速 7.5 自加速 + TidalWave 环) | 刺间 26f | 哨音 + Lethal 预测线 | 刺间恢复拍；浪爆弹幕慢速起步 |

巡曳连接拍：蛇形缓游至侧上方 430px，偶发 1 颗慢速可读泡（DragonBubble），到位提前选招/56f 保底。

## 5. 三大演出脚本

**入场「东海升朝」(~190f)**：fake-Z 6→0 三次方冲镜 35f（远景龙影冲向镜头）→ **60f 纯静止威压**（只有水珠滴落）→ 举戟后摆 20f（QuadInOut 至 -0.9rad，渐强震屏）→ 戟落 6f（1-(1-t)⁸ 斩拍）：冲击环视觉 + shake 12 + 潮涌泛光 + 两侧封路龙卷升起 → 回摆入巡曳。前 150f 无敌。

**相变一「没顶」(65%，~320f 可玩)**：清弹(留龙卷) → 受创后仰 30f（反冲 13px/f + 白闪音效）→ 冲天离场 50f（fake-Z 升至 3.5，沿途甩水瀑）→ 空场 190f：水位急涨至 0.4 屏（专属水位线着色器），第 10/60f 各一面慢速穿越浪墙（缺口 150px，一左一右，850px 外 ~113f 抵达）→ 破水回场 46f（fake-Z 归零 + shake 12 + 潮涌泛光 + 冲击环）。全程 `dontTakeDamage`，浪墙伤害真实。

**相变二「定海·潮止」(30%，~180f)**：清弹 → 急停定身 20f → **潮止 70f**：水位/折射/底色全部退干（海停了），无声，龙王缓缓举戟 50f → 戟落：**impact frame（黑白高对比 15f，一场唯一）** + shake 16 + 水位弹至 0.5 + 深渊底色 + 龙眼转红 → 赤目凝视 60f → 入 P3。

**死亡「潮退归海」(~350f，CheckDead 接管)**：清弹（含龙卷，随 owner Death 状态消散且免伤）、伤害归零 → 失控抽搐 40f（每 8f 随机速度脉冲 + 受击音）→ 螺旋上升 150f（半径 50→280、转速渐快、DissolveBurn 从尾部推进 0.1→0.55、加速鼓点 9 连音高递增、沿途漏水）→ **顶点定格 20f**（velocity=0，泛光塌缩，静默）→ 水爆 26f：shake 19（一场最大）+ 泛光拉满 + impact 0.55 + 140 粒水花 → 泡沫沉降 110f（水位/底色退潮归零，泡沫上浮）→ `life=0; checkDead()` 真死（掉落/downed 照常）。

## 6. 视觉技术方案

新建专属着色器（全部 ps_3_0，AoGuang 前缀，`AoGuangHelper` 静态缓存 `Asset<Effect>`，不注册 ACMShaders）：

1. **AoGuangWaterSerpent.fx** — 龙躯水流 ribbon（TriangleStrip 像素着色器）：沿 NPC.oldPos(34 点) 的 TriangleStrip 水带；双层反向流动噪声 + 泡沫边缘 + 芯部亮色 + 行波脉冲；`uGlow` 速度门控（穿刺时全亮 —— 高速时 34 帧轨迹拉出 ~1500px 长龙，低速时盘卷在本体周围）。Additive，噪声 s0。
2. **AoGuangTidalWall.fx** — 浪墙屏幕空间 decal：SDF 整面巨浪（前沿噪声浪尖 + 浪体深浅水流 + 浪头泡沫 + 前沿 Lethal 亮线 + 缺口 Safe 描边 + 半场遮罩 + warn 幕布模式），供 TsunamiWall / AoGuangSkyDeluge / 相变一浪墙复用；坐标换算缩放感知（对齐 WorldDecalParams 约定）。
3. **AoGuangAbyssalSea.fx** — 专属全屏后处理（s0=screenTarget, s1=噪声）：水下折射 + RGB 色散 + **屏幕水位线**（噪声波动海面、水下深水渐变/焦散/水面亮线）+ 向心吸入（深渊漩涡）+ **uImpact 黑白高对比 impact frame**。走 `ACMShaders.RequestFullscreenSlot()` 名额契约，任一强度 >0.01 才申请。

复用共享件：DissolveBurn（死亡溶解）、BeamGrad（龙息光芯，弹幕原有）、RadialBloom + ElementalScreenTint（SubmersionScreenSystem 保留）、ArenaRunic（漩涡红环）、共享噪声 `ACMShaders.NoiseTexture`、原版 SandnadoHostile 龙卷贴图。

本体绘制：fake-Z（scale=1/(Z+1)，透明度随 Z 衰减）、速度门控残影（pierceGlow>0.2 才画）、姿态覆盖角（举戟/斩拍演出）、龙眼阶段变色（蓝→白→红）。

## 7. 性能与多人预算

- ribbon spine ≤ 34 点、细分 ≤2（TrailQuality 配置降级，Off 不画）；封路龙卷 162→40 段×2 层；行走龙卷 28 段；AbyssalVortex 118→24 层；粒子每帧每源 ≤ 数个。
- 全屏后处理仅 AoGuangAbyssalSea 一个，走名额契约；强度全零早退；尊重 `MythologyConfig.FullscreenShadersEnabled`（浪墙 decal 同样受控且有 CPU 回退绘制）。
- 选招/瞄点/扫向/半场方向全部服务器决定：`chargeTarget/chargeCount/wallDir/sweepDir/breathAngle` 走 SendExtraAI；状态经 ai[0..3]；`netUpdate` 于每次锁定时置位；瞄点在锁定前由各端用已同步的 target 确定性推导。弹幕生成全部 `Main.netMode != MultiplayerClient` 判定。
- 漩涡拉玩家削至峰值 0.35 且 240f 渐强；接触伤害走 `CanHitPlayer`=穿刺帧。
- 视觉标量（水位/溶解/泛光/impact）纯本地，不进同步流。

## 8. 实施清单

- [x] `AoGuang.cs`：状态枚举重组、同步字段收敛、CheckDead 死亡接管（掉落/downed 不动）
- [x] `AoGuang.AI.cs`：主循环、洗牌袋、巡曳、入场/相变一/相变二/死亡脚本、清弹与浪墙生成工具、距离栓绳
- [x] `AoGuang.Phase1.cs`：潮弓三连 / 穿刺巡游 / 潮涌立柱(波浪推进) / FindGroundY
- [x] `AoGuang.Phase2.cs`：浪墙层涌 / 水龙卷投掷 / 龙息水柱
- [x] `AoGuang.Phase3.cs`：深渊漩涡 / 终潮天倾 / 狂龙连刺
- [x] `AoGuang.Drawing.cs`：龙躯 ribbon、fake-Z、速度门控残影、DissolveBurn 溶解、AbyssalSea 后处理
- [x] `AoGuangHelper.cs`：专属着色器缓存 + 浪墙 decal 助手（既有 public 成员保留）
- [x] `AoGuangProjectiles*.cs`：重做 TsunamiWall（整面浪墙+缺口）、WaterSpike（延时+贴地+警戒柱）、AbyssalVortex（拉力渐强+红环对齐）、BarrierWaterTornado（段数削减+死亡消散免伤）、DragonWaterBolt/TridentProjectile（初速渐升）；未被新编排引用的 public 类（WaterVortex/HomingWaterOrb/GiantWhirlpool/DragonMinion/DragonClawSlash/FallingWaterSpear）保留不删；TidalWave/DragonBubble 仍在编排中使用
- [x] 新增 `AoGuangProjectiles5.cs`：AoGuangWaterspout（行走水龙卷）、AoGuangSkyDeluge（天倾巨浪，自带 warn 幕布）
- [x] `AoGuangSubmersionScreenSystem.cs`：保留（V2 期共享氛围层，最终实现经 Publish 引用）
- [x] 3 个新 .fx 按名编译退出码 0
- [x] ReadLints 清零 + 隔离 csc 全仓语义编译（敖广目录 0 错误）
- [x] hjson 敖广键区：新增 2 键（zh/en 同步），无死键
