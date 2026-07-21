# 召唤法杖系列重做设计文档 (SummoningStaffs)

> 管辖件：八卦阵盘 BaGuaZhenpan / 黑熊法杖 BlackBearStaff / 承影剑 ChengYing(Reins) / 冥鸦法杖 MingCrowStaff
> 弹幕：BaGuaSigilProj / BlackBearStaffProj1 / BlackBearStaffProj2 / MingCrowMinion / ChengYingHitbox

## 1. 现状诊断（八条透镜 + 召唤三加项）

### 1.1 八卦阵盘 BaGuaZhenpan（太上老君出售，LightPurple）
- **一眼身份**：机制身份独特（8 槽布阵系统，功能在 `Players/BaGuaPlayer.cs`），但场上表达为零——"阵图"只是一张 64px 贴图贴在胸口以 0.03 rad/f 慢转。
- **三段感 / 召唤仪式**：HoldUp 无音效（`UseSound` 未设）、无展开动画、无收拢动画，阵法激活/切换零反馈。
- **演出峰值**：无。系列旗舰位空缺。
- **多人卫生**：`UseItem` 里 `BaGuaUISystem.Toggle` 与 `NewProjectile` 均未做本地玩家判定——其他客户端会被别人施法弹出 UI（bug）。
- 结论：**全面重做视觉与仪式**（阵法系统本体在他人文件，不动）。

### 1.2 黑熊法杖 BlackBearStaff（黑熊精 1/10 掉落，Green）
- **一眼身份**：召唤物贴图 = 法杖物品贴图本身悬浮在头顶——完全没有"黑熊精幼灵"身份；zh 名"金皇冠"与 en 名"Black Bear Staff"互相矛盾。
- **攻击可读性**：每 60f 从自身同点同速喷 4-6 个重力熊头（彼此完全重叠成一团），方向取"玩家→目标"而非"熊→目标"。
- **待机灵动感**：位置每帧硬赋值锁死在玩家头顶 + 正弦浮动，呆板。
- **机制深度**：无决策点；硬编码上限 1 只。
- **性能/多人**：`AttackShooting` 里 `NewProjectile` 无 owner 判定且 owner 传 `Main.myPlayer`（MP 各端各生成一份）；`MinionSacrificable` 写在 SetDefaults、`projFrames/TrailingMode/TrailCacheLength` 每帧写在 PreDraw；右键锁敌判断写反（`between < 2000f` 时把锁定目标丢弃，锁敌完全失效）。
- 已有近期补的溶解显形 + Bronze 命中 burst，保留语言并升级。结论：**全面重做**。

### 1.3 承影剑 ChengYing（Pink，御剑坐骑 + 接触判定盒）
- **一眼身份**：御剑飞行概念极佳（《列子·汤问》承影：“昧爽之交……淡淡焉若有物存”），但剑体是静态贴图，无"若有似无"的影剑语言。
- **命中反馈栈**：判定盒 `hide=true` 纯逻辑，命中零反馈；伤害恒 60 与速度无关（骑着不动贴脸磨血与全速冲撞一个价）。
- **判定公平**：判定盒偏移 `-24*direction`（向后），冲撞时剑尖反而打不到。
- **轨迹可读性**：无拖尾无残影。
- 坐骑本体（`Mounts/ChengYingMount.cs`）非本代理管辖——一切演出通过判定盒弹幕与物品实现。结论：**判定盒重做为演出载体**。

### 1.4 冥鸦法杖 MingCrowStaff（冥鸦 1/100 / 血鸦 1/50 掉落，Orange）
- **一眼身份**：有专属飞行/攻击贴图（5 帧×2 套）+ 幽蓝拖尾 + 显形烟（近期补过），底子最好。
- **攻击可读性**：匀速 12f lerp 追踪贴脸怼，无俯冲弧线、无前摇收招。
- **伤害正确性**：近身 `StrikeNPC` 直接扣血（绕过召唤加成/免疫框架，且 AI 在 MP 各端都跑 = **双重/多重伤害 bug**），与 friendly 接触伤害叠加。
- **待机灵动感**：绕玩家转圈但所有鸦 `ai[0]` 相位未初始化——多只全部重叠在同一点。
- **性能**：SetDefaults 里 `Request<Texture2D>().Value` 立即加载存实例字段（服务器路径不安全、每次生成重复取值）。
- 结论：**AI 与反馈全面重做，保留贴图资产**。

## 2. 系列主题与幻想感

**"阴阳有序，万灵听令"——指挥官幻想。** 玩家不是自己出拳的人，而是布阵者与驭灵人：
- **八卦阵盘** = 阵法幻想（布阵开坛，卦象逐位点亮，方位有序）；
- **黑熊法杖** = 驭兽（黑熊精麾下幼灵，黑风扑击 + 金冠怒威 + 蜜意，呼应黑熊精 Boss 的 黑风/蜂蜜/金冠 三件套）；
- **冥鸦法杖** = 驭群（阴间鸦群此起彼伏地俯冲，羽爆与缠绕）；
- **承影剑** = 御剑（若有似无的影剑，速度即锋芒）。

召唤线手感核心三件套，每件都要有：**召唤仪式**（法阵/溶解显形）、**攻击导向反馈**（前摇→俯冲→收招波形 + 命中 burst）、**待机呼吸感**（弹簧跟随 + 相位错开）。

配色语言（消费 `ACMWeaponBurst` 常量，不改共享件）：八卦=金白×玄青（Gold 22）；黑熊=玄黑×金冠×蜜琥珀（Gold 22 / Bronze 20）；冥鸦=幽蓝×黑羽（Shadow 33）；承影=淡青白影（Shadow 33 + 白芯，"灵体感"半透明+发光边全系列统一）。

## 3. 逐件机制设计

### 3.1 八卦阵盘（系列演出旗舰）
- **左键**：施阵——获得 BaGuaBuff 并在身后展开**程序化八卦法阵**（新 shader `BaGuaArray.fx`）：
  - 展开仪式 48f：双环从中心弹性生长（elastic overshoot），八卦按**先天卦序 一乾二兑三离四震五巽六坎七艮八坤** 逐位点亮，完成帧冲击环 + 柔光 + 震屏 2；
  - 常驻：身后加性法阵盘（半径 ~96px，透明度克制 ≤0.55），外环正转/内环反转，中央阴阳双点互绕；
  - **阵法激活态**：读 `BaGuaPlayer.CurrentName`（只读公共字段）——有阵法生效时法阵提亮转速加快；**阵法切换瞬间重燃闪光 + 音效**（阵法系统本体不动，只做表达层）；
  - Buff 消失：14f 收拢消散。
- **右键**：开布阵 UI（保留），补 UI 开合音；本地玩家判定修复。
- 决策点：本就存在于布阵系统（8 槽摆放），本次给它"场上可见状态机"。

### 3.2 黑熊法杖 → 召唤「黑熊幼灵」
- **召唤物形象**：复用黑熊精 Boss 精灵图（只读 `Textures/NPCs/Boss/BlackBear/idle_344`(4帧)/`run_332`(6帧)），scale 0.22 幽灵化着色（暗蓝灰身 + 头顶金冠 SoftGlow 亮点），溶解显形（金边）。
- **待机**：弹簧跟随玩家侧后方（落后半拍的秒差感）+ 呼吸浮动 + idle 4 帧慢放。
- **攻击循环（前摇→爆发→收招）**：
  1. 前摇 18f：向后蓄势漂移 + 黑风粒子汇聚（可读预警）；
  2. 猛扑 10f：一帧 set 速度 30px/f（launch is a set）+ 每帧 ×1.02，黑风金边拖尾，run 帧快放；
  3. 到点/命中：生成**熊掌震击**（BlackBearStaffProj2 重做——熊首虚影盖印 + 冲击环 AoE，单次判定），Gold burst + 震屏 2 + 落掌音；反冲弹回（×-0.25 recoil）；
  4. 收招 22f：×0.9 衰减飘回悬浮位。
  - 接触伤害仅在猛扑段生效（`CanDamage` 门控），localNPCHitCooldown 20。
- **大招时刻**：每第 4 次拍击 =「金冠怒击」——前摇多 8f 金光定格（pre-silence），拍击 scale×1.5、伤害 ×1.5、蜜琥珀飞溅 + 低吼（复用 `Sounds/BlackBear/BlackBear_Roar`，低音量）+ 震屏 3。
- 修复：右键锁敌判断反转 bug、MP 生成 owner 判定、静态设置归位。上限保持 1 只（"独宠大师兄"身份），minionSlots 0.5 不变。

### 3.3 承影剑（判定盒 = 演出与手感载体，坐骑文件不动）
- **速度即锋芒**：接触伤害随骑乘速度缩放 `60 × (0.75 + 0.75 × |v|/12)` → 静止贴脸 45 / 巡航 ~75 / 全速冲撞 90；localNPCHitCooldown 20（原全局 10f 贴脸磨血 → 单次撞击更重、频率更低）。
- **判定修正**：判定盒前移 `+16*direction`（剑尖判定）。
- **若有似无的影剑语言**（速度门控，慢=近乎无形，快=剑影显形——这就是承影的身份）：
  - |v|>6：淡青白双层 ribbon 剑影拖尾，透明度 ∝ 速度；
  - |v|>7：每 5f 落一枚**折影残像**（坐骑剑贴图 + 共享 DissolveBurn，threshold 随时间微颤 →"淡淡焉若有物存"）；
  - |v|>9：剑尖流光 + 风声（音高随速度）。
- **命中反馈栈**：Shadow burst + 白芯柔光 + 火花 dust + 震屏 2；**全速冲撞（|v|>10）= 破影一闪**：burst×1.3 + 震屏 3 + 更锐的音高。
- **上剑仪式**：物品使用时本地生成一次性 `ChengYingSummonGlint`（新类，写在 ChengYing.cs）：剑形从虚空溶解显形 + 垂直白光柱 + 环纹。

### 3.4 冥鸦法杖（鸦群此起彼伏）
- **编队待机**：以 `minionPos` 相位错开绕玩家头顶椭圆盘旋（修复全员重叠 bug），扑翼帧速 ∝ 速度。
- **攻击循环（状态机 ai[0]=状态 ai[1]=计时）**：
  1. 环伺 ~22f（+ minionPos×8 错拍）：在目标上方 130px 弧线盘旋蓄势（前摇，群鸦轮流进入俯冲——phase-offset，屏幕永远有动静）；
  2. 俯冲 ≤16f：一帧 set 21px/f 朝预测点 + 每帧 ×1.035（≤30），幽蓝 ribbon + 鸦羽粒子，风声；
  3. 穿过目标 → 收招 14f：×0.92 刹车 + 上拉弧线，回到环伺。
  - 伤害仅俯冲段生效（`CanDamage` 门控），usesLocalNPCImmunity + localNPCHitCooldown 18。
- **大招时刻**：每第 3 次俯冲 =「缠绕俯冲」——俯冲轨迹变螺旋缠绕（垂直正弦），伤害 ×1.4，命中**鸦羽爆散**（黑羽 + 幽蓝 dust 12-16 粒 + Shadow burst 1.15 + 震屏 2.5）。
- 修复：删除 `StrikeNPC` 直击（双重伤害 bug）；贴图静态缓存出 SetDefaults；显形加 DissolveBurn（幽蓝边，与系列召唤仪式语言统一）。

## 4. 系列内梯度

| 件 | 进度位 | 演出档位 |
|---|---|---|
| 冥鸦法杖 | 前期（鸦怪掉落） | 共享原语：ribbon 拖尾 + burst + dust 羽爆 |
| 黑熊法杖 | 黑熊精后 | 共享原语 + Boss 贴图复用 + 双段音效分层 |
| 承影剑 | Pink 位 | 共享 DissolveBurn 折影语言 + 速度门控层叠 |
| 八卦阵盘 | LightPurple 位（旗舰） | **专属 ps_3_0 着色器 BaGuaArray** + 仪式展开 + 状态机法阵 |

## 5. 视觉技术方案

- **新建着色器（仅 1 个，旗舰专属）**：`Effects/BaGuaArray.fx`（ps_3_0）——SDF 程序化八卦盘：双旋环 + 8 卦爻纹（3 位二进制编码进一个 float 常量，先天卦序）+ 阴阳双点 + 噪声微光；参数 uProgress（展开/逐卦点亮）/uActive（阵法激活提亮）/uColorPrimary/uColorSecondary/uIntensity/uSpin。以 `WeaponVFX.GetEffect("BaGuaArray")` 缓存获取，画在普通加性 quad 上（**不占全屏名额**——非全屏后处理）。
- **复用共享件**：`WeaponVFX.DrawRibbonTrail/DrawProjectileTrail`（熊扑/鸦俯冲/剑影拖尾）、`ApplyDissolveBurn`（全系列召唤显形 + 承影折影残像）、`DrawShockwaveRing/DrawGlowBurst`（拍击/展开）、`ACMWeaponBurst.Spawn`（Gold/Shadow/Bronze 主题）、`WeaponVFX.AddScreenShake`（预算内 ≤3）。
- **贴图**：零新增。黑熊幼灵复用 Boss 精灵图（只读），承影复用坐骑贴图，冥鸦用既有双套贴图，八卦全程序化。
- **音效**：全部复用 `SoundID` + 既有 `Sounds/BlackBear/*.mp3`（低音量分层，Pitch 随机 ±0.1~0.2）。

## 6. 平衡与定位（获取途径/稀有度/职业不变）

- **黑熊法杖**（召唤 25 dmg 不变）：旧 = 60f 周期 4-6 发×25 重叠霰弹（理想 100-150/s，实际弹道散射单体 ~50/s）；新 = ~50-60f 循环：扑击接触 25×1~2 + 拍击 AoE 25×1，第 4 次 ×1.5 → 单体 ~55-70/s，群体拍击 AoE 打 2-3 目标与旧霰弹持平。**±15% 内**，且删除了 MP 多端重复生成的隐性超模。
- **冥鸦法杖**（召唤 12 dmg 不变）：旧实战 ~12-20/s/鸦（贴脸理想值更高但依赖 bug 的 StrikeNPC 多端叠加）；新 = ~50f 循环 1-2 hit ≈ 15-19/s/鸦，第 3 次 ×1.4。修复双重伤害 bug 后名义持平实战值。
- **承影剑**：旧 = 恒 60、10f 全局免疫（贴脸理论 360/s，实际骑乘接触不稳定）；新 = 45~90 随速度、20f 局部免疫（全速理论 270/s，静止贴脸 135/s）——上限下调但反馈/命中率大幅上升，定位仍是"机动工具附带撞击"，非主力 DPS。
- **八卦阵盘**：纯表达层重做，数值零变化（阵法系统在他人文件，未动）。

## 7. 性能与多人预算

- 着色器/贴图全部静态缓存（ImmediateLoad 一次）；Boss 精灵图与 Boss 共享 Asset 缓存。
- 弹幕生成全部 owner 判定（`Main.myPlayer == Projectile.owner` / `player.whoAmI == Main.myPlayer`）；伤害状态存 `Projectile.ai[0..2]`；纯视觉计时用 localAI。
- 拖尾走 `WeaponVFX`（受 `MythologyConfig.Trail` 降级）；八卦法阵为单 quad 绘制（每帧 1 次 End/Begin），屏外跳过；粒子每事件 ≤16 粒；震屏 ≤3。
- UI 开关、音效、残像缓冲全部仅本地客户端。

## 8. 实施清单

1. `Effects/BaGuaArray.fx` 新建 + 按名编译（ps_3_0，退出码 0）。
2. `Projectiles/BaGuaSigilProj.cs`：法阵状态机（展开/常驻/激活态/重燃/收拢）+ shader quad 绘制。
3. `Items/Weapons/SummoningStaffs/BaGuaZhenpan.cs`：音效 + 本地玩家判定修复。
4. `Projectiles/BlackBearStaffProj1.cs`：黑熊幼灵全重做（贴图/弹簧待机/扑击状态机/金冠怒击/修 bug）。
5. `Projectiles/BlackBearStaffProj2.cs`：熊掌震击 AoE 重做。
6. `Items/Weapons/SummoningStaffs/BlackBearStaff.cs`：施法音分层 + 注释清理。
7. `Projectiles/MingCrowMinion.cs`：鸦群状态机（环伺/俯冲/缠绕俯冲/收招）+ 修双重伤害与相位 bug。
8. `Items/Weapons/SummoningStaffs/MingCrowStaff.cs`：微调与注释。
9. `Projectiles/ChengYingHitbox.cs`：速度伤害 + 折影残像 + 命中栈 + 判定修正。
10. `Items/Weapons/SummoningStaffs/ChengYing.cs`：上剑仪式（新 `ChengYingSummonGlint`）+ tooltip。
11. ReadLints 全部清零。
12. 最后一步：两个 hjson 同步（物品名/tooltip/弹幕名/Buff 文案，小步锚点编辑 + 回读验证）。
