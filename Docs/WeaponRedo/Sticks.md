# 如意棍系列 (Sticks) 重做设计文档

> 系列文件：`Items/Weapons/Sticks/*`、`Projectiles/*StickSpearProjectile*.cs`
> 主题着色器：`Effects/RuyiGoldenCudgel.fx`、`Effects/RuyiPillarDrop.fx`（系列前缀 Ruyi*）
> 状态：已实施

## 1. 现状诊断（逐件要点）

以共享简报 §3.1 八条透镜逐件检查：

- **WoodenStick 木棍**：ExampleMod 剑挥模板换皮；SmoothStep 匀速挥舞无三段感；右键"26px/帧冲刺+无敌帧"超模且 `player.velocity`/`player.immune` 直改、多端不安全；无命中反馈。
- **IronStick 铁棍**：同一模板第二份复制；右键"按住悬停再 36px/帧冲刺 + 2x 伤害 + 无敌"，`Main.mouseRight` 在 AI 里读（多人错乱）。
- **GoldenStick 金棍**：模板第三份 + Thrust 连突（曲线可用但匀速）；右键掷棍跟随鼠标、松开**瞬移玩家**（Teleport）——机制跑题、`Main.MouseWorld` 全端读取。
- **GemStick 宝石棍**：模板第四份 + SpinThrust(2x)；右键弹幕吸附鼠标 + 左键点击瞬移玩家；六种 dust 随机乱撒无统一语言。
- **RuyiStick 如意棍**：模板第五份 + WideSwing(2x, 反弹弹幕——直接把 hostile 改 friendly，多人危险)；右键定海神针（上举锁定玩家坐标、`static bool isA` 跨实例通讯、鼠标状态 AI 读取）——想法最好、执行最差的一件。
- **TrueRuyiStick 真·如意棍**：**纯占位**。vanilla 贴图 + Swing 白板，无弹幕无特效无机制。
- **RuyiJinguBang 如意金箍棒**：**纯占位**。SilverBroadsword 贴图 + Swing 白板。系列旗舰是系列最烂一件。
- **共性性能债**：PreDraw 每帧 `ModContent.Request`、每帧 new List 顶点、`ProjectileID.Sets` 在 PreDraw 里改写、五份重复的手写条带代码。

结论：除定海神针落点已接 WeaponVFX 屏震外全部低于地基水准 → **全面重做**（机制+视觉+多人安全），保留全部类名/配方/进度位。

## 2. 系列主题与幻想感

**"如意"二字：随心，伸缩。** 齐天大圣的金箍棒"要大就大、要小就小"，本系列是从一根凡木到定海神针的全进度升级线，每一阶教玩家"伸缩"的一个新维度：

| 件 | 伸缩课程 | 神话对应 |
|---|---|---|
| 木棍 | **戳**——棍会伸长刺出 | 有缘人得的凡木 |
| 铁棍 | **撑**——拄棍撑地借力跃起 | 千斤之铁，学会借力 |
| 金棍 | **掷**——离手仍如臂使指，飞出复归 | 金光符箓之器 |
| 宝石棍 | **绽**——每击绽出棱光碎片 | 七宝镶嵌 |
| 如意棍 | **变**——巨大化横扫 + 蓄力定海神针砸落 | 初闻"定海"之名 |
| 真·如意棍 | **分**——双头回环 + 更快的定海神针 | 猴骚味渐浓 |
| 如意金箍棒 | **定**——如意值蓄满，天降定海神针 | 东海镇海之宝 |

系列贯穿的微观签名：**每次挥棍的爆发帧，棍都会"如意"地过冲伸长一截**（低阶 +10%，旗舰 +25%）——3 秒内就能感知的系列身份。

配色语言：木褐 → 钢蓝 → 金辉(`ACMWeaponBurst.Gold`=22) → 多彩(`Gem`=23) → 致命红(`Fatal`=24) → 幽蓝×金（真·如意）→ **金红双色 + 紧箍纹**（金箍棒）。

## 3. 逐件机制设计

全系列左键统一为**连段挥舞**（共享基类 `StickComboSwingBase`，见 §5），采用 MOTION.md 的 96 帧挥砍解剖比例：**前摇 ~42%（quadratic 回拉），爆发 ~16%（poly(14~18) ease-out + 过冲伸长），收招 ~42%（quintic 回落）**；伤害窗口严格对齐爆发段起点至收招前段（前摇无判定）。命中反馈栈：方向性 dust + 屏震(普通段 ≤2 / 重段 3~4) + `SoundID.Item1` 音高随段位递升 ±随机 + 中高阶主题 Burst。

### 3.1 木棍（2 段 + 右键戳刺）
- 左键：横扫 → 回扫（1.0x / 1.1x），动画 20f。
- 右键**如意戳**：前摇 7f 棍缩回（0.75x）→ 爆发 4f 伸长至 1.6x 直线刺出（轴向线段判定，1.4x 伤害、1.5x 击退）→ 收招 9f。替换原危险冲刺。
- 演出：木褐双层拖尾（角速度门控）+ 命中木屑 dust + 屏震 1.5。无 Burst 无 shader（低阶朴素）。

### 3.2 铁棍（3 段 + 右键撑杆跃）
- 左键：横扫 → 回扫 → **重抡**（过顶大回环 1.35x 伤害，前摇多 30%，落点 6 帧冲击环 + 屏震 3 + `DD2_MonkStaffGroundImpact` 低频叠层）。
- 右键**撑杆跃**：棍向斜下撑地 8f，将玩家以固定初速（水平 ±9 / 垂直 -10.5）向鼠标方向抛起，**无无敌帧**（删除原免伤滥用）；CD 2.5s。位移只在 owner 端写 `player.velocity`。
- 演出：钢蓝拖尾 + 撑地银尘 + 小冲击环。

### 3.3 金棍（3 段 + 每 4 次金光三连突 + 右键掷棍）
- 左键：三段同铁棍；**攻击计数满 4 次后下一击变"金光三连突"**：三次 8f 快速刺击（0.65x/0.65x/1.5x），第三突带 `Gold` Burst + 屏震 2.5。
- 右键**掷棍如意**：金棍旋转掷出（初速 22，飞向鼠标点或 480px 上限），在目标点**悬停化金光柱 30f**（持续判定 0.6x/10f），然后自动回旋归手；掷出瞬间玩家反向后坐 2px/f。同时只允许一根在外。替换原"瞬移玩家"。
- 演出：金辉拖尾、悬停期金色柔光脉冲、回收金尘。

### 3.4 宝石棍（3 段 + 命中绽碎片 + 右键棱光回旋）
- 左键：三段；每次命中沿击飞方向绽出 **2 枚棱光碎片**（六色轮转，0.25x，穿透 1，短寿命细拖尾）。
- 右键**棱光回旋**：以玩家为轴、棍伸长 1.6x 的双圈旋转斩（40f，角速度慢→快→慢），每 45° 切向甩出 1 枚棱光碎片（共 8 枚），期间移速 ×0.85（贴脸 AOE 的决策代价）。替换原瞬移。
- 演出：hue 流转双层拖尾（保留原色相语言）+ `Gem` Burst；碎片颜色即判定。

### 3.5 如意棍（3 段 + 每 5 次巨大化横扫 + 右键定海神针）
- 左键：三段；**每 5 次攻击触发"如意巨大化"横扫**：scale 1.9x、范围 1.25x、2x 伤害（继承原 WideSwing 数值），扫击路径上**击落**敌方弹幕（直接销毁 + 红闪，每次上限 6 枚——替换原"反转 friendly"的多人危险实现）。
- 右键**定海神针**（重做）：按住蓄力（`player.channel`）：棍上举渐伸（1→2.2x）+ 蓄力抖动，0.5s/1.0s/1.5s 三级（末 25% 静默收束）；玩家**可移动**（×0.8，不再锁定坐标）；松开：向鼠标落点天降定海巨针（`RuyiStickSpearProjectile_3` 重做），poly(3) 加速砸落、插地 0.8s、落地 2x~3.5x 伤害（按蓄力级）+ 屏震 5~9 + `Fatal` Burst 1.2~2.0x + 双环冲击波 + 碎石尘。删除 `static isA` 通讯与 0.2x 免伤补偿。
- 演出：致命红拖尾（保留）+ 落点预警红光。

### 3.6 真·如意棍（从占位全面重做）
- 持械弹幕体系全套继承（弹幕类写入 `TrueRuyiStick.cs`）：三段连段（220 伤害语义→240，见 §6）+ **每 5 次"双头回环"**：棍两端均有判定的 360° 大回环（1.8x，双向击退）。
- 右键定海神针：复用如意棍 `_2/_3`，通过 `ai` 传规格（蓄力每级 0.4s，更快；针体 +25%，落地追加两根 240px 侧针）。
- 配色：幽蓝×金（材料为四魂）；持械绘制复用旗舰 `RuyiGoldenCudgel` 着色器低强度档（辅色传幽蓝，无新增着色器成本）。
- 物品贴图从 vanilla 占位改指模组已有 `Textures/Projectiles/RuyiStickSpearProjectile`（不新增贴图文件）。

### 3.7 如意金箍棒（旗舰）
- 左键**四段连段**：横扫 → 回扫 → 双头回环 → **如意巨大化砸落**（scale 2.4x 过顶砸向鼠标侧地面，1.9x 伤害，落点冲击环 + 屏震 4 + `Fatal` Burst 1.4x）。
- 资源循环**如意值**（存 `RuyiStickPlayer`，0~100）：命中 +4、暴击 +7、第四段命中 +10，非持有时衰减；棍身紧箍纹随如意值逐环点亮（VFX 即状态广播）。
- 右键**定海神针·真**（如意值满才可用）：30f 前摇（棍上指、金红光收束、末 8f 静默）→ 天降**全屏高定海神针**（`RuyiPillarDrop` 着色器绘制针体：金红双色 + 滚动紧箍环纹 + 白热芯）砸落鼠标 X 位置 → 落地：屏震 12、径向泛光 + 金红染屏 0.12（全屏名额契约内，<0.6s）、直径 ~340px 8x 伤害、左右两道奔行冲击波。释放后如意值清零。
- 被动演出：持械时棍身常驻 `RuyiGoldenCudgel` 着色器（金红双色、流动紧箍环、强度随如意值 0.35→1.0）；满值时手部金红火花。
- 物品贴图同改指 `RuyiStickSpearProjectile`，库存图标满值发光（PostDrawInInventory 不做，保持克制）。

## 4. 系列内梯度

| 件 | 拖尾 | 命中 Burst | 冲击环 | 屏震上限 | 着色器 | 全屏演出 |
|---|---|---|---|---|---|---|
| 木棍 | 细单色 | — | — | 2 | — | — |
| 铁棍 | 中双层 | — | 重段小环 | 3 | — | — |
| 金棍 | 金辉 | Gold | 三连突 | 3 | — | — |
| 宝石棍 | hue 流转 | Gem | 回旋起手 | 3 | — | — |
| 如意棍 | 致命红 | Fatal | 定海落地 | 9 | — | — |
| 真·如意棍 | 幽蓝金 | Fatal+Soul 色 | 双头回环+定海 | 9 | 复用 RuyiGoldenCudgel(低档) | — |
| 金箍棒 | 金红双层 | Fatal/Gold 分层 | 四段+神针 | 12 | RuyiGoldenCudgel + RuyiPillarDrop | 径向泛光+染屏 0.12(名额内) |

## 5. 视觉技术方案

- **共享基类 `StickComboSwingBase`**（写入 `Projectiles/WoodenStickSpearProjectile.cs`，系列内部复用，不动共享件）：三段波形、爆发帧过冲伸长、棍尖轨迹环形缓冲 + `WeaponVFX.DrawRibbonTrail` 双层拖尾（角速度门控）、命中反馈栈、贴图 45° 对角绘制、线段判定、多人同步（角度入 `ai[1]`）。删除五份手写顶点条带与每帧 `ModContent.Request`。
- **复用共享地基**：`WeaponVFX.DrawRibbonTrail / DrawGlowBurst / DrawShockwaveRing / DrawRadialBloom / ApplyPaletteTint / AddScreenShake`、`ACMWeaponBurst.Spawn`（Gold/Gem/Fatal）、`ACMAsset.SoftGlow/GlaciateWave/SlashBurst`、`TelegraphColors.Lethal`。
- **新建系列专属 ps_3_0 着色器（仅旗舰档消费）**：
  - `RuyiGoldenCudgel.fx`：喂棍贴图；沿棍轴（贴图对角线方向）的紧箍环纹按 `uCharge` 逐环点亮 + 金红双色流光 + 边缘泛光 + `uFlash` 白闪。金箍棒常驻（强度随如意值），真·如意棍低强度档（辅色幽蓝）。
  - `RuyiPillarDrop.fx`：程序化定海神针柱（喂 SoftGlow 柔边）；针体金红渐变、白热芯、滚动紧箍环纹、`uImpact` 落地脉冲、`uProgress` 下落/消散控制。仅金箍棒大招使用。
- 音效全部分层复用 `SoundID`（Item1 挥/DD2_MonkStaffSwing 重段/DD2_MonkStaffGroundImpact 落地低频/Item14 神针撞击/MaxMana 蓄力级提示），Pitch 随段位与随机 ±0.1~0.2。

## 6. 平衡与定位

配方链、获取途径（唐僧赠木棍 → 逐级合成）、稀有度、近战定位全部不变。伤害论证（DPS = 伤害×平均段倍率/单挥帧数，与原实现对比）：

| 件 | 原 | 新 | DPS 变化 | 论证 |
|---|---|---|---|---|
| 木棍 | 8 / 20f | 8 / 20f，段均 1.05x | +5% | 删右键 1.5x 冲刺无敌，补给左键 |
| 铁棍 | 28 / 30f | 28 / 26f，段均 1.15x | +13% | 删右键 2x 冲刺+无敌帧（净减防御收益），左键补偿 |
| 金棍 | 48 / 40f | 48 / 30f，段均 1.12x + 每 4 击三连突 | +12% | 原 40f 一挥远低于同期近战；三连突为主要输出节奏点 |
| 宝石棍 | 68 / 50f | 68 / 30f，段均 1.12x + 0.25x×2 碎片 | +10%（碎片半数命中计） | 原 SpinThrust 2x 删除；50f 一挥严重低于进度位 |
| 如意棍 | 120 / 20f | 120 / 22f，段均 1.27x（含每 5 次 2x） | -3% 左键；定海 2~3.5x（原 5x 削至此） | 原定海 5x + 免伤 0.2x 双超模，一并回收 |
| 真·如意棍 | 200 / 14f 白板 | 240 / 22f，段均 1.28x | -2%（对白板名义值） | 占位件无可比机制；对齐机械后近战梯队 |
| 金箍棒 | 260 / 10f 白板 | 340 / 18f，段均 1.30x + 满值 8x 神针 | -6% 常态 +神针峰值 | 占位件名义 DPS 虚高；神针受 100 如意值门控（≥14 次命中/发） |

全部变化在 ±15% 内；两件占位旗舰以"同进度位近战对标"取值（叶绿梯队参考真近战 ~25 DPS/f 级）。

## 7. 性能与多人预算

- Effect/纹理静态缓存（`WeaponVFX.GetEffect` / `ACMAsset`），杜绝每帧 Request；拖尾顶点走 `BuildRibbonStrip`，受 `MythologyConfig.Trail` 降级；棱光碎片同屏上限（每玩家 12）。
- 全屏后处理仅金箍棒神针落地一处，走 `RequestFullscreenSlot`（`DrawRadialBloom`/`ApplyPaletteTint` 内部处理），强度 0.12 < 0.15，持续 <36f。
- 屏震全走 `WeaponVFX.AddScreenShake` 预算：普通命中 ≤2、重段 3~4、定海 5~9、金箍棒神针 12（一次性大招）。
- 多人：鼠标只在 owner 端读（OnSpawn/Shoot/owner 分支），方向经 `ai[]`+`SendExtraAI` 同步；蓄力用 `player.channel`；玩家位移只在 owner 端写 velocity；删除全部 `player.immune` 滥用与 `static` 跨实例状态；如意值存 `ModPlayer`（仅 owner 消费）。
- 伤害判定与视觉对齐：前摇无判定（`CanDamage`），神针落地判定帧=冲击环出现帧。

## 8. 实施清单

1. `Projectiles/WoodenStickSpearProjectile.cs`：+`StickComboSwingBase` 基类；木棍 2 段。
2. `Projectiles/WoodenStickSpearProjectile_2.cs`：如意戳（`StickThrustBase` 亦在基类文件）。
3. `Projectiles/IronStickSpearProjectile.cs` / `_2.cs`：铁棍 3 段 / 撑杆跃。
4. `Projectiles/GoldenStickSpearProjectile.cs` / `_2.cs`：金棍 3 段+三连突 / 掷棍回旋。
5. `Projectiles/GemStickSpearProjectile.cs` / `_2.cs`：宝石棍 3 段+`GemShardProj` / 棱光回旋。
6. `Projectiles/RuyiStickSpearProjectile.cs` / `_2.cs`：如意棍 3 段+巨大化 / 定海蓄力 `_2`+砸落针 `_3`+`RuyiStickPlayer`（保留类名，重做为系列充能载体）。
7. `Items/Weapons/Sticks/*.cs`：七件物品重写 Shoot/CanUseItem/AltFunction/Tooltip 钩子；TrueRuyiStick、RuyiJinguBang 补全套持械弹幕（类内嵌于各自物品文件）。
8. `Effects/RuyiGoldenCudgel.fx`、`Effects/RuyiPillarDrop.fx`：新建并按名编译（ps_3_0，退出码 0）。
9. hjson（zh-Hans / en-US）：系列键区更新 Tooltip（机制说明），最后一步小步编辑并复验。
