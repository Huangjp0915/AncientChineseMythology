# 将臣 Jiangcen —— Boss 重做 V3 设计文档

> 单元：`NPCs/Boss/Jiangcens/`（Jiangcen / JiangcenHammer / 弹幕组 / JiangcenSky / JiangcenThunderPrisonSystem）
> 神话身份：四大僵尸始祖之首（旱魃・后卿・赢勾・**将臣**），犼魂所化、尸族之祖。模组设定为"雷鸣锤"之主 ——
> 一位死了千年仍在点兵布阵的**僵尸大将**。掉落雷鸣锤，镇守"将臣"进度位（RealmGate 四尸判定之一）。

---

## 1. 现状诊断

以 choreography skill 七大本能 + 失败模式清单过一遍现有代码（V2 中期水准）：

1. **动作失重、无速度对比（本能 #1/#2/#3 全违）**：本体所有招式都是 `Center += (hover - Center) * 0.06~0.08` 的弹性悬停，全程无一次真冲刺、无反向蓄势、无硬刹；「僵尸跳」用 `Vector2.Lerp(Center, storePos, 0.28f)` 16 帧滑移到位，读作瞬移而非跳跃（失败模式："teleports feel cheap"）。
2. **本体从不出手**：所有攻击都是"悬停在头顶放弹幕"，每招开始/结束的屏幕状态几乎相同（PACING 自检 "each attack ends in a different screen-state" 全部不过）；六柄环绕锤是唯一亮点，但与本体动作零联动。
3. **雷狱=数值墙**：50% 转阶段演出只有 150 帧站桩+dust；雷牢合拢没有 impact 拍；Phase2 相对 Phase1 仅多一招链电。低血量无任何保留内容（"hold content hostage" 违反——开场即全亮）。
4. **无死亡演出**：`OnKill` 直接掉落，尸祖之死没有任何节拍；入场只有 fade-in+一声吼。简报要求的三大演出节拍缺二。
5. **公平性红线**：`GeneralsOrder` 直接 `AddBuff(BuffID.Frozen, 90)` 冻结玩家操作再放镜像锤突袭——预警变成剥夺操作；本体接触伤害全程开启（悬停贴脸也掉血），伤害窗与视觉不对齐。
6. **节奏是平的**：每招间统一回 Reposition 悬停 36 帧，wave 无起伏；攻击循环固定数组无递进；`JiangcenSky` 是纯色矩形叠加，与雷暴题材无关联。
7. **多人隐患**：`storePos`（跳跃落点）等本体私有字段不同步，客户端模拟会漂移。

已有可保留的底子：TelegraphColors 规范预警、ThunderPrisonSystem（tint/牢笼/泛光三层，非 screenTarget）、链电/落雷的 warn-active 帧对齐判定、雷锤回旋两段躲。

**重做力度判定：全面重做** AI 编排 + 三大演出 + 专属着色器；雷狱概念保留并升格为高光演出。

---

## 2. 设计主题与幻想感

**主题词：尸将军・军令・天罚雷狱。**

将臣不追杀你——他**点名**你。全套攻击语言统一成"军阵指令"：

- **点将**（六锤依次受命猛砸）、**布阵**（尸坟唤将）、**将令**（雷印点名跟随锁定）、**天罚**（雷狱、万雷点将）。
- 移动语言 = **僵尸跳**：蹲伏压缩 → 弹射 → 空中僵直定格 → 直线下砸。他不飞行追击，他"跳"，每次落地是一次山崩。
- 世界反馈 = **雷暴天幕**：入场即压城，雷狱阶段天地成牢；终章雷狱失稳，玩家在崩塌的牢中完成斩首。

配色语言（遵守 TelegraphColors 契约）：

| 色 | 用途 |
|---|---|
| 血红 Lethal (250,40,56) | 仅致命伤害预警（落点/猛砸径向线/雷印锁定/锤魂突袭线） |
| 尸暗红 (150~190,30~45) | 尸气氛围（坟、尸手、本体残影） |
| 雷青 Lightning (180,230,255) | 雷电本体与非致命预警（锚点/边界/氛围） |
| 军金 Gold | 将令符印、点将播报（"将"的身份色，用量最少） |

---

## 3. 阶段结构与血量断点

| 阶段 | 血量 | 内容 |
|---|---|---|
| Intro | 入场 | 雷暴压城 → 天雷显形 → 静立亮目 → 六锤点兵 → 长啸开战（~215f） |
| Phase1 | 100%→50% | 地面军阵战：僵尸跳 / 点将砸(4锤) / 雷锤回旋 / 尸坟唤将 / 将令雷印 |
| Transition | @50% 一次性 | 「雷狱降临」演出：清弹收锤 → 升空缠电 → 三声军鼓钉雷矛 → 静默一拍 → 雷牢合拢 impact（~265f，无敌） |
| Phase2 | 50%→18% | 雷狱规则战：边界劈雷常驻 + 链电电网(4锚) + 六锤连环(6锤) + 双雷印 + 强化跳 |
| 终章解锁 | <18% | 招池插入**万雷点将**（走廊落雷+安全缝），雷牢失稳视觉，连接拍缩短 |
| Death | HP 归零 | CheckDead 拦截：坠地 → 六锤逐一失能坠落 → 挣扎 → 天罚预兆 → 静默 → 六雷轰顶(白屏 impact) → 溶解崩解 → 真死掉落（~320f） |

攻击循环用**手写节奏数组**（压制↔布阵↔爆发交替，PACING §2）；玩家距离 >1500px 强制僵尸跳追击（位置博弈不变）；Phase2 全部悬停目标点用雷牢半径栓绳。

---

## 4. 招式编排表

时长均为 60fps 帧。预警三要素（形状+颜色+渐强）齐备；红仅出现在真伤害源。

### 4.1 僵尸跳 JiangshiHop（重做：真跳跃，三连，第三跳"帅跳"）
| 段 | 帧数 | 内容 |
|---|---|---|
| 蹲伏 | 40 (帅跳 48) | 刹停+压扁(scaleY→0.82)+落点 Lethal 环锁定（提前量 `target.velocity*18`）；末 8 帧 pow(t,6) 反向下沉 26px（late-snap） |
| 腾空 | 22 | 一帧 set 起跳速度（ease-out 到顶点），后仰 -0.12rad，speed-gated 残影 |
| 空中定格 | 10 | velocity≈0，僵直姿态转为前倾对准落点（menace 拍） |
| 下砸 | ~13 | 直线 poly(3) 加速至 ~46px/f 冲向锁定落点，**仅此段+落地 6f 开接触伤害** |
| 落地 | 22 | shake 11、双层扇形震波弹（内 9 外 7 交错，首 18f 速度 80%→100% 渐升）、尘柱(SlashBurst)、Pulse |
公平阀门：空中不追踪；落点环从蹲伏起 ≥40f 可读；三跳间 22f 恢复拍。P2：帅跳落地追加十字四向短震波。

### 4.2 点将猛砸 HammerSlam（升级：本体联动+锤重量）
- 本体每"点"一柄锤：抬臂顿一下（rotation 短促上抬 snap 回）+ 指令音。
- 单锤流程：受命悬停变红 90f（P2 70f）→ 径向 Lethal 红线随蓄力渐亮渐宽 → 末 12f 沿径向**反向后拉** pow(t,8)·90px → 1 帧 set 42px/f 径向猛砸（shake 8+Pulse）→ 34f 飞行衰减 → **嵌墙停顿 14f**（重量感）→ 拔回轨道。
- 触发节奏：P1 4 柄每 40f 一柄（空间交错序 0,3,1,4）；P2 6 柄每 30f；终章两两同时（3 组）。
- 伤害窗=猛砸飞行段（state2），其余为 0。

### 4.3 雷锤回旋 ThunderHammerThrow（升级：拉弓+接锤反冲）
- 前摇 46f：本体向玩家反方向 drift-back（charge²·180px）+ 双锤间电弧拉线；**末 6f 静默**（粒子熄灭）。
- 掷出 1 帧：2 柄（P2 3 柄）19px/f 扇形，本体反冲 -dir·7px，shake 8。
- 去程 42f 减速 → 回程转向加速 22px/f（去/回两段躲）；P2 锤带电弧视觉。
- 接锤：回到本体 70px 内销毁 + 本体小反冲 + 火花。超时 150f 保底出口。

### 4.4 尸坟唤将 CorpseRain（升级：军鼓+波浪时序）
- 前摇：本体双锤敲击 2 声军鼓（间隔 24f，各 shake 3）→ 5 座尸坟标记**依次**点亮（每 6f 一座，近→远）。
- 62f 后尸手**波浪式**抓出：第 i 座延迟 i·9f（ripple，不再齐射）。P2 尸手更高（400px）更快。
- 尸手自带 26f 暖红预告（保留），总时长 ~120f。

### 4.5 将令雷印 GeneralsOrder（公平性重做：冻结 → 点名）
- **删除玩家冻结**。将臣高举令旗（脚下金色小法阵闪现 + 音效）。
- 生成雷印 `JiangcenSealMark`：跟随玩家脚下 36f（金圈半透）→ **锁定** 26f（定格转红收束，滴答音）→ 静默 6f → 雷印处轰落雷柱 22f（伤害窗=视觉）。
- P1 1 枚跟随本人；P2 2 枚：第二枚跟随玩家关于场心的**水平镜像点**（与锤魂镜像语言统一——"你的影子也被点名"）。
- 同时 2 柄镜像锤魂（保留：点对称+水平镜像，突袭前 16f Lethal 线）。
- 玩家对抗方式=持续移动甩印+侧闪锤魂；全程可操作。总时长 ~190f。

### 4.6 雷狱链电 ChainLightning（P2，升级：4 锚菱形+错拍点亮）
- 4 锚点菱形（绕玩家 r=470）：锚点雷青收束圈 45f → 依次"钉锚"脉冲（每 8f 一个，shake 2）。
- 4 条边**错拍**：边 i 于 `i·12f` 后进入 36f 预告 → 26f 激活；随后第二轮点亮两条对角线（× 形）。玩家从熄灭的边隙穿行。
- 复用 `JiangcenChainArc`，新增 ai[2]=起始延迟。总时长 ~240f。

### 4.7 万雷点将 ThunderRollCall（新招，<18% 解锁的终章大招）
- 将臣飞至雷牢中心上空高举双锤，六锤加速环转；金字播报"万雷点将"。
- 前摇 70f：天幕持续增亮、rumble=charge²·4、雷牢失稳闪烁。
- 雷牢直径分 7 列走廊落雷（列宽 110px，复用加宽版 `JiangcenLightningStrike`）：
  - 波次1 奇数列（44f 预告→静默→齐落）→ 18f 呼吸 → 波次2 偶数列 → 18f 呼吸 →
  - 波次3 全列齐落，仅留玩家最近一列为**安全缝**（Safe 色光带提前 60f 标出并锁定）。
- 收招 24f 静默散热。总时长 ~350f。一场战斗只该有一个"几乎全屏"的时刻——是这里。

### 4.8 雷狱边界劈雷（P2 被动，保留+视觉升级）
- 出界（>雷牢半径）每 55f 头顶落雷（42f 预告，保留）；边界墙换专属电弧着色器，失稳阶段闪断。

连接拍（connector）：每招之间 24f（终章 14f）"僵直漂浮"姿态——双臂前伸的僵尸定格+周身细电弧，轮廓与所有攻击姿态不同（PACING §3 punctuation）。

---

## 5. 入场 / 换阶段 / 死亡三大演出脚本

### 5.1 入场（Intro，~215f，无敌+零伤害）
| 帧 | 事件 |
|---|---|
| 0 | 锁定 ArenaCenter=玩家位置；天幕快速压暗（雷暴云翻滚）；本体隐形悬于高空 |
| 0~70 | 远景闪电 2~3 道（天幕亮拍）+ 远雷声，dust 前兆 |
| 70 | **一道粗天雷轰在落点**：白闪 0.35+shake 9 → 本体在雷柱中显形（DissolveBurn 1→0，30f，雷青灼边），砸地站定 |
| 70~150 | **静立不动**（menace is stillness）：红目辉光渐亮+周身细电弧，只有低鸣 |
| 150~198 | **六锤点兵**：每 8f 一柄从本体脚下拔出飞入环绕轨道（各一声金属+电火花） |
| 198~215 | 仰天长啸：shake 10+Pulse 0.7+天幕大亮拍 → 进 Phase1 |

### 5.2 雷狱降临（Transition @50%，~265f，无敌+清弹）
| 帧 | 事件 |
|---|---|
| 0 | 清全部敌意弹幕；六锤收拢绕身快转；锁定雷牢中心 |
| 0~60 | 升空至场心上方，雷电缠身渐强，天幕转入雷狱相（更暗、放电更频） |
| 60~180 | **三声军鼓**（每 40f 一声，shake 5/7/9 递进+天幕亮拍递增）；每声鼓后雷牢边界**钉入 4 根雷矛**（12 根 30° 均布，视觉柱贯穿天地） |
| 180~210 | **静默收拢**：全部粒子/电弧熄灭（inhale before the scream） |
| 210 | **雷牢合拢 impact**：牢体 0→1 瞬亮+白闪 0.8+shake 14+长啸，边界链电 12 段依次跑圈点亮 |
| 210~265 | 落回战位，恢复可伤害 → Phase2（首招延迟 30f 给缓冲） |

### 5.3 死亡（CheckDead 拦截 → Death 阶段，~320f）
| 帧 | 事件 |
|---|---|
| 0 | life=1 无敌；清弹；停将令；六锤失去动力**逐一坠落**（第 i 柄延迟 i·10f，重力坠地闷响+熄灭） |
| 0~50 | 本体失能坠地（重力），落地弹跳一次，电弧失控狂闪 |
| 50~140 | **挣扎**：两次缓慢撑起又跪落（呼吸感）；雷牢失稳闪断衰减；天幕转暗红 |
| 140~200 | **天罚预兆**：6 个预告圈在本体上收束，天幕亮度爬升，rumble=t²·5 |
| 200~212 | **静默 12f**（全体熄灭） |
| 212 | **六道天雷同时轰顶**：全屏白 impact 拍（whiteFlash 1.0，~12f）+shake 16+巨响 |
| 212~300 | 本体 DissolveBurn 溶解（0→1，雷青灼边+灰烬上升），雷牢同步崩解，天幕退场 |
| 300 | 服务器 life=0 → checkDead 放行 → OnKill/掉落照常（downed 标记不回退） |

---

## 6. 视觉技术方案

### 6.1 新建专属着色器（均 ps_3_0，不注册进 ACMShaders，本地静态缓存）
1. **`Effects/JiangcenLightningArc.fx`** —— 顶点条带电弧（TriangleStrip，与 BeamGrad 同顶点契约）：
   噪声域扭曲中心线（折线抖动）+ 高次幂白热芯 + 雷青辉光 halo + 高频阈值化分叉亮斑 + uSeed 每条弧独立形状。
   用于：链电激活段 / 雷牢墙 / 锤间电弧 / 本体缠电 / 雷印落雷增亮 / 死亡失控电弧。
   由 `JiangcenVFX.DrawArc / DrawArcBatch`（一次开合批画多条）驱动。
2. **`Effects/JiangcenStormSky.fx`** —— 程序化雷暴天幕（对位 AncestralDragonSky 的用法）：
   玄黑→血红垂直渐变底 + 双层域扭曲 fbm 雷云 + **云内放电**（cell hash 随机短亮，雷青照明）+ 程序化远景闪电折线（随机触发）+ uFlash 外部亮拍 + uBossUV 尸主血晕 + uPhase（0 常态 / 1 雷狱相）。
   由重写的 `JiangcenSky` 全屏绘制；`FullscreenShadersEnabled=false` 时回退到现有纯色叠加。

### 6.2 复用共享件
- `ArenaRunic`(uMode=1) 雷牢罩 + `ElementalScreenTint` 雷暴压暗 + `RadialBloom` 事件泛光（ThunderPrisonSystem 现有三层，保留）。
- `DissolveBurn`：入场显形 / 死亡崩解。
- `ACMAsset.LightningBranch`：落雷柱/雷印柱贴图；`ElectricArcSheet`：本体/锤缠电随机帧段；`SlashBurst`：落地尘柱与尸手；`SoftGlow`：预告圈与辉光。
- `ACMShaders.DrawBeam`：猛砸径向红线、锤魂突袭线（保留）。

### 6.3 ThunderPrisonSystem 强化（接口新增，不动共享文件）
- `Publish(...)` 增加 `instability`（终章/死亡失稳：牢体闪断+色偏红）与白闪通道 `FlashWhite(strength)`（全屏白 impact 拍，≤0.9，12f 指数衰减；AlphaBlend 纯色盖，不读 screenTarget，不占全屏名额）。

### 6.4 本体绘制升级（PreDraw）
- squash & stretch（蹲伏压扁/腾空拉长/落地回弹）；速度门控残影（>14px/f 才显）；红目辉光；蓄力/雷狱期 ElectricArcSheet 缠电；旋转随速度倾斜。

---

## 7. 性能与多人预算

- **多人安全**：跳跃落点 `jumpTarget`、雷牢中心等经 `SendExtraAI/ReceiveExtraAI` 同步，状态切换一律 `netUpdate=true`；弹幕/召唤仅服务器（`VaultUtils.isClient` 判定保留）；将令雷印按 `Projectile.Center` 权威同步；`Main.LocalPlayer` 只出现在绘制/Sky/ScreenSystem。
- **性能**：着色器/噪声全部静态缓存（Xuanwu 模式）；电弧条带每帧 ≤24 条、每条 ≤12 段；天幕 1 次全屏 draw；dust 每帧 ≤~40；无热路径 LINQ/每帧 new 资源；全部视觉受 `MythologyConfig` 降级（Trail=Off 关电弧，FullscreenShadersEnabled=false 关天幕 shader 与屏幕系统）。
- **震屏预算**：走 ACMScreenShakeSystem（取 max）；≥14 只出现在雷牢合拢(14)与死亡轰顶(16) 两拍。
- **掉落/进度不回退**：`ModifyNPCLoot`（妖气碎片+雷鸣锤）、`OnKill` downed 标记、RedwoodCoffin 召唤、BossChecklist 集成、`JiangcenSky.LoadInstance()` 注册全部保留原签名。

---

## 8. 实施清单

1. `Effects/JiangcenLightningArc.fx` + `Effects/JiangcenStormSky.fx`（新建，CompileFX 按名编译）
2. `NPCs/Boss/Jiangcens/JiangcenVFX.cs`（新建：着色器缓存 + 电弧/柱绘制助手）
3. `NPCs/Boss/Jiangcens/Jiangcen.cs`（重写：状态机含 Death、8 招编排、三大演出、绘制升级、JiangcenSky 重写为 shader 天幕）
4. `NPCs/Boss/Jiangcens/JiangcenHammer.cs`（从 Jiangcen.cs 拆出+重做：轨道惯性、反拉猛砸、嵌墙停顿、收拢/坠落状态、缠电）
5. `NPCs/Boss/Jiangcens/JiangcenProjectiles.cs`（从 Jiangcen.cs 拆出+重做：TelegraphMark(+安全缝样式)、ShockBolt、CorpseHand、ThrownHammer、HammerGhost、LightningStrike(+走廊变体)、ChainArc(+延迟)、**新增 JiangcenSealMark 将令雷印**）
6. `NPCs/Boss/Jiangcens/JiangcenThunderPrisonSystem.cs`（强化：失稳+白闪通道）
7. 双 hjson（最后一步）：`Jiangcen` 键区扩块（ThunderPrison/RollCall 播报）+ `JiangcenSealMark.DisplayName`

所有既有 public/internal 类型名不改名不删除；命名空间 `AncientChineseMythology.NPCs.Boss.Jiangcens` 不变。
