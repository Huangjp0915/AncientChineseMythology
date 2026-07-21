# 刀剑系列（Swords）重做设计文档

> 管辖：`Items/Weapons/Swords/` 下 BoneSword / BronzeSword / CrimsonbronzeSword / GanJiangSword / XuanTieSword / YuChangSword / BlackBearSword，
> 及 `Projectiles/` 下 GanJiangSwordProj(_2) / YuChangSword(Bean/Skill)Projectile / CrimsonbronzeSwordProj1 / BlackBearSwordProj1（grep 已验证唯一消费方为本系列）。
> 新增着色器：`Effects/GanJiangTwinArc.fx`、`Effects/GanJiangUnity.fx`（旗舰专属，ps_3_0）。

---

## 1. 现状诊断（八透镜逐件）

| 武器 | 诊断要点 |
|---|---|
| 骨剑 BoneSword | 超快挥速（useTime 6）是唯一身份；无三段感、无决策点、无演出峰值。偶发骨白 burst 是仅有反馈。→ 补强。 |
| 青铜剑 BronzeSword | 毒 + **1% 无差别秒杀（对 Boss 也生效，`target.life=0` 直杀）**——不可读、不公平、平衡毒瘤。无峰值节奏。→ 机制重做（秒杀可读化为斩杀线处决）。 |
| 赤铜剑 Crimsonbronze | 右键蓄力有骨架但：伤害倍率纯随机 ×2~×8（不可读）；蓄力无进度广播；`Main.MouseWorld/Main.mouseRight` 直接进 AI（多人不安全）；HoldItem 污染 Item 状态。→ 蓄力等级化重做。 |
| 干将剑 GanJiang | 系列最高阶（Purple/84），但 `CanUseItem`+`Shoot`+`HoldItem` 三处发射逻辑互相打架（第三击双剑同发实为 bug 感）；挥砍全程 SmoothStep 匀速无爆发；右键莫邪瞬移挂机无演出；Swing/Spin 交替与"每三击"叙事不符。→ 旗舰全面重做。 |
| 玄铁剑 XuanTie | "重剑无锋"却是匀速原版 Swing 的铁阔剑换皮 + 流血；毫无重量感。→ 手持弹幕重做挥舞曲线。 |
| 鱼肠剑 YuChang | crit 80 突刺身份成立；但突刺匀速无加速度；剑豆用金币粒子与"寒锋"脱节；右键只是直线弹幕（20s 冷却过长），毫无"刺客背刺"幻想；配色错用宝石紫。→ 机制视觉重做。 |
| 黑熊剑 BlackBear | **useTime 150 / useAnimation 45 不同步**（挥完僵直 1.75s，手感极差）；弹幕直接用 **BlackBear Boss 头贴图**从屏幕边缘飞入，身份混乱；无本地化呼应黑熊精。→ 全面重做。 |

性能卫生共性问题：GanJiang 双弹幕每帧 `new List<ColoredVertex>` + 每帧 `ModContent.Request` 拖尾纹理；YuChang 每帧 `Request` 贴图。一并修复（静态缓存）。

## 2. 系列主题与幻想感——"名剑谱"

每把剑对应一则中国铸剑/名剑神话，机制身份彼此鲜明：

- **骨剑/青铜剑**：上古粗朴（大荒骨器、商周青铜）——低阶朴素但反馈扎实，"每一击都有回响"。
- **玄铁剑**：重剑无锋，大巧不工——慢、重、崩地，挥一剑要有开山的错觉。
- **赤铜剑**：火淬猩红——按住右键是"入炉"，松手是"出炉"，蓄得越久刃越白热。
- **鱼肠剑**：专诸刺王僚，鱼腹藏锋——出鞘无声、百刺归一、背刺处决的刺客幻想。
- **黑熊剑（勇士金剑）**：黑风山石中剑——黑风裹掌、蜂蜜蜜渍，呼应黑熊精（配色取自 Docs/BossRedo/BlackBear.md：墨黑暗风 + 琥珀蜜金，不引用 Boss 代理资产）。
- **干将剑（旗舰）**：干将莫邪雌雄双剑——**"双剑合璧"**：干将赤金在手，莫邪青蓝为影；每一斩有影随，十鸣起合璧。

## 3. 逐件机制设计

### 3.1 骨剑（补强）
- 左键：保持 useTime 6 极速。命中计数，**第 8 击"碎骨"**：该击伤害 ×2，骨白 burst 1.3 + 骨屑喷溅 + 屏震 1.5、音高下沉。计数存 ModItem 字段（本地手感层）。
- 决策点：换目标不清零计数 → 鼓励贴脸连打。
- 前摇-爆发：原版 Swing 保留（低阶朴素）；碎骨击瞬间 `player.velocity` 微后坐 0.5px 体现反作用。

### 3.2 青铜剑（机制重做）
- 移除 1% 无差别秒杀。新 **"断金"**：命中生命 <12% 的非 Boss 敌人 → 处决即杀（金绿爆发 1.4 + 清脆双层音）；对 Boss/低于线的目标改为 +15% 伤害（斩杀线增伤，ModifyHitNPC）。
- 毒保留。挥砍淬绿：青铜暖金尘 + 偶发绿磷光。
- 决策点：残血收割节奏（主动找残血目标补刀）。

### 3.3 赤铜剑（右键重做 + MP 安全）
- 左键不变（点燃 + Crimson burst）。
- 右键蓄力等级化：45f→L1（×2.2）、105f→L2（×3.6 + 1 颗熔火溅珠 ×0.8）、165f→L3（×5.5 + 3 颗熔火溅珠）。每升一级：刃身白热闪 + 升调音 + 小冲击环（可读广播）；松手挥出对应等级火焰剑气（体积/拖尾随级放大）。
- 蓄力期间轻微手臂抖动（±1.2px 高频）+ 火尘密度 ∝ 等级。
- MP 修复：蓄力计时/释放走 owner 端判定；`Main.mouseRight` 仅 owner 读取；方向经 `Projectile.velocity` 传递。

### 3.4 玄铁剑（手持弹幕重做）
- 改为手持弹幕 `XuanTieHeldSlash`（写在 XuanTieSword.cs 内，原生 held proj，不新占 Projectiles/）。
- **三段循环**：横斩 → 回斩 → 过顶崩地斩。每段波形：前摇 ~40%（quadratic 后摆 −0.9rad，剑身微颤）→ **爆发 ~15%（poly(12) ease-out 一帧到位感）** → 收招 ~45%（quintic 回正）。
- 崩地斩（段3）：斩底触发 **崩地冲击**——生成短命地裂弹幕（宽 120px 地面判定 ×0.8 伤害）、双层冲击环 + 碎石尘 + 屏震 3 + 低频音。
- 命中反馈：XuanTieBleed 沿用；命中屏震 1.5、暗红 burst；重剑击退 6.5。
- 数值：useTime 20→段驱动（26/26/34 帧），伤害 13→段伤 16/16/26；论证见 §6。

### 3.5 鱼肠剑（刺客重做）
- 左键突刺：保留 Rapier 骨架与 crit 80 身份；突刺曲线改 **快出慢收**（poly(6) 出鞘）；**每第 4 刺"透骨刺"**：伤害 ×1.5、玩家向光标短滑步 60px（owner 端）、寒银白闪。剑豆改"鱼影飞刃"视觉（寒银冷青拖尾，仍用 YuChangSwordBean 贴图）。
- 右键 **"穿心·背刺"**（冷却 8s，原 20s）：600px 内有敌 → 玩家瞬身至目标**背侧**（带出鞘残影+消音），0.25s 内三段连刺（各 ×1.0）；目标为非 Boss 且 HP<15% → 直接处决（LethalRed 爆发 + 心跳静默半拍）。无目标 → 向光标 240px 幽影突进 + 单段 ×2 穿刺。
- 配色语言：寒银白青（225,240,255 / 120,180,220），处决一抹致命红。

### 3.6 黑熊剑（全面重做）
- 修同步：useTime=useAnimation=60（重剑一秒一挥），伤害 47→42，击退 15 保留。
- 左键挥砍：墨绿黑风尘 + 琥珀蜜光点；**每第 3 击"黑风熊掌"**：从玩家背后黑风中扑出一道熊掌剑气（BlackBearSwordProj1 重做：改用自身剑气贴图 + 墨绿黑风拖尾 + 琥珀核心，×1.5 单次命中），不再从屏幕边缘飞入、不再用 Boss 头贴图。
- 命中叠 **"蜜渍"**（新 debuff 写在 BlackBearSword.cs：受到伤害 +8%，4s，GlobalNPC 挂钩）——先涂蜜再重击的决策点。
- 熊掌命中：琥珀爆发 1.4 + 屏震 2.5。

### 3.7 干将剑（旗舰全面重做）——"雌雄双剑·合鸣"
- **左键三连段**（held proj 重写）：
  1. 段1 正斩（覆角 2.4rad）；2. 段2 逆斩（覆角 2.4rad，更快 12%）；3. 段3 **双剑十字旋斩**（干将实体 + 莫邪虚影同时反向旋斩，×1.3）。
  - 波形：前摇 35%（quadratic 后摆，剑尖聚赤金粒子）→ **爆发 18%（poly(9) ease-out）** → 收招 47%；爆发帧屏震 1、挥砍音 pitch 随段递升。
  - **影随斩**：每段斩出 6 帧后，莫邪虚剑（青蓝半透明镜像，溶解显形）沿镜像弧线自动补斩（×0.35 独立命中）——GanJiangSwordProj_2 重写。
  - 挥砍弧光：扇环 mesh + `GanJiangTwinArc.fx`（赤金/青蓝双主题，噪声撕裂缘，随挥砍进度扫描淡出）。
- **剑鸣共鸣**：任意命中 +1 鸣（上限 10，存 ModPlayer）；剑柄光点环 + 满鸣时剑身呼吸金光提示。
- **右键**：
  - 满 10 鸣 → **"合鸣·雌雄合璧"**：前摇 25f（双剑交叉高举、汇聚流光，最后 6f 静默收束）→ 爆发：光标方向放出赤金×青蓝**交叉双巨剑气**（各 ×2.5，穿透），交点展开 `GanJiangUnity.fx` 阴阳双鱼盘（世界 decal，20f 开→22f 持→18f 收），起爆 10f PaletteTint ≤0.12 定调 + 屏震 8。
  - 未满鸣 → 莫邪单出：虚剑飞向光标最近敌自动斩一轮（×0.8，保留原右键 DNA，8f 内可读显形）。
- 性能修复：拖尾纹理/Effect 静态缓存；arc mesh 顶点数组复用（`stackalloc`/成员数组）；虚剑同屏 ≤2。

## 4. 系列内梯度

| 档 | 武器 | 演出预算 |
|---|---|---|
| 低 | 骨剑 / 青铜剑 / 玄铁剑 | 共享原语（dust、GlowBurst、ShockwaveRing、ACMWeaponBurst），无专属 shader |
| 中 | 赤铜剑 / 鱼肠剑 / 黑熊剑 | 共享原语 + DrawBeam/RibbonTrail + 处决/蓄力峰值时刻 |
| 旗舰 | 干将剑 | 2 个专属 ps_3_0 shader + 影随斩常态演出 + 合鸣全屏级大招（走名额契约） |

## 5. 视觉技术方案

- 复用：`WeaponVFX.DrawProjectileTrail/DrawRibbonTrail/DrawGlowBurst/DrawShockwaveRing/AddScreenShake/ApplyDissolveBurn/ApplyPaletteTint`、`ACMShaders.DrawBeam`、`ACMWeaponBurst`（Bone 32 / Bronze 20 / Crimson 21 / XuanTieBleed 2 / Gem 23 / LethalRed 15 / Shadow 33）、`ACMAsset`（SoftGlow/GlaciateWave/SlashBurst/Sparkle）、既有 SwordTrail551/553 贴图。
- 新建（仅旗舰）：
  - `GanJiangTwinArc.fx`：扇环斩击弧光。uv.x=沿弧、uv.y=径向；uProgress 扫描头 + 残光衰减、径向芯/缘双色、噪声撕裂外缘、端头收口。
  - `GanJiangUnity.fx`：阴阳双鱼合鸣盘。极坐标双半盘（赤金/青蓝）旋转、双鱼眼、白热界线、噪声呼吸、uIntensity 开合。
- 全屏后处理仅两处且皆走名额契约：合鸣起爆 PaletteTint（≤0.12、10 帧）；鱼肠处决 RadialBloom 经 ACMWeaponBurst 既有路径。

## 6. 平衡与定位（获取途径/进度位不变）

| 武器 | 原 DPS 估算（单目标） | 新 DPS 估算 | 变化 | 论证 |
|---|---|---|---|---|
| 骨剑 | 30/s | ~33.8/s | +12.5% | 第 8 击 ×2 等效，±15% 内 |
| 青铜剑 | 48/s + 1%全额秒杀期望 | 48/s + 斩杀线 | ≈0 | 秒杀可读化：RNG 期望改为确定性收割，对 Boss 由"1%直杀"（异常）改为 <12% 增伤 15%（削幅度、补公平） |
| 赤铜剑 | 右键期望 ×3.37 → ~139/s | L1~L3 110~146/s | −20%~+5% | 移除随机上限 ×8；L3 溅珠补偿，期望使用档 L2/L3 ≈ 120~146 |
| 玄铁剑 | 39/s | 40~47/s | +3%~+20% | 循环 86f 输出 58 + 条件地裂；超 15% 部分以"更慢更重、容错更低"的重剑定位补偿（White 级同期基准 36/s） |
| 鱼肠剑 | 左 150/s、右 5/s(20s CD) | 左 ~156/s、右 13/s | +5% 内 | 透骨刺 ×1.5 每 4 刺 ≈ +12.5% 左键、右键冷却 20→8s 但倍率 3→1×3 段 |
| 黑熊剑 | 实测 ~55-62/s（弹幕多段递减 2~3 hit） | 42×1.25 + 掌 63/3s ≈ 63/s | ≈+5% | useTime 150/45 不同步为明显失误，修复后以 47→42 与"每 3 击一掌"控幅 |
| 干将剑 | 理论 ~400/s（三处发射叠加+挂机莫邪） | ~450/s | +12% | 三段均值 ×1.1 + 影随 ×0.35 + 大招摊销 ~84/s，收敛进 ±15%；移除挂机挂发（HoldItem 常驻发射）反而降低无操作收益 |

职业定位（近战）、配方、掉落、稀有度全部不变；鱼肠 1% 钓鱼获取保留。

## 7. 性能与多人预算

- 所有 Effect/Texture 静态缓存或走 `WeaponVFX.GetEffect`/`ACMAsset`，杜绝每帧 Request。
- arc mesh 顶点：成员数组复用，段数 ≤ 24×2 顶点；拖尾受 `MythologyConfig.Trail` 降级（走 WeaponVFX 内建）。
- 屏震预算：命中 ≤2 / 崩地·熊掌 2.5~3 / 合鸣 8。
- 弹幕生成全部 owner 端（Shoot 钩子或 `Projectile.owner == Main.myPlayer` 判定）；`Main.MouseWorld` 只在 owner 路径/OnSpawn（owner 生成端）读取；影随/大招状态经 `Projectile.ai[]` 传递；共鸣层存 ModPlayer。
- 视觉弹幕（burst/decal）damage=0 短命；虚剑同屏 ≤2、熔火溅珠 ≤3。

## 8. 实施清单

1. `Items/Weapons/Swords/BoneSword.cs` — 碎骨第 8 击。
2. `Items/Weapons/Swords/BronzeSword.cs` — 断金斩杀线。
3. `Items/Weapons/Swords/CrimsonbronzeSword.cs` + `Projectiles/CrimsonbronzeSwordProj1.cs` — 蓄力等级化 + MP 安全 + 熔火溅珠。
4. `Items/Weapons/Swords/XuanTieSword.cs` — 内置 `XuanTieHeldSlash` 三段重挥 + 崩地斩。
5. `Items/Weapons/Swords/YuChangSword.cs` + `Projectiles/YuChangSword*/YuChangSkillProjectile.cs` — 透骨刺 + 穿心背刺 + 寒银配色。
6. `Items/Weapons/Swords/BlackBearSword.cs` + `Projectiles/BlackBearSwordProj1.cs` — 同步修复 + 黑风熊掌 + 蜜渍。
7. `Items/Weapons/Swords/GanJiangSword.cs` + `Projectiles/GanJiangSwordProj.cs` + `GanJiangSwordProj_2.cs` — 旗舰三连段 + 影随 + 合鸣。
8. `Effects/GanJiangTwinArc.fx`、`Effects/GanJiangUnity.fx` — 编写并按名编译（退出码 0）。
9. ReadLints 清零 → 最后统一更新两个 hjson 键区（Tooltip 与新弹幕/Buff 名，代码内 GetOrRegister 优先）。
