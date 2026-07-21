# 万魂幡 SoulBanners 系列重做设计文档

> 系统级旗舰单品：收魂 → 幡旗蓄能 → 亡魂军团。全系 8 个文件 + 2 个专属 ps_3_0 着色器。

## 1. 现状诊断（八条透镜 + 两条附加）

- **SoulBanner.cs（物品）**：成长系统身份独特，但资源循环缺"释放"决策点——灵魂只是被动倍率，攒满后无事可做；右键在 minion 已在场时是纯无效操作（浪费一个交互位）。
- **SoulBannerHeldProj.cs（左键手持）**：四段（举幡/祭幡/引魂/收魂）结构好，但前摇无 reel-back 蓄势、爆发用 QuadOut（二次）不够"斩钉截铁"（MOTION.md：strike 应 poly(8+)）；幡旗本体只是一张贴图加多层 tint——**作为"幡"的布面飘动身份完全缺失**；引魂漩涡全靠 dust 堆叠（每帧 30+ dust、全 NPC 扫描×3 套粒子），无 shader 层。
- **SoulBannerMinion.cs（右键悬浮幡）**：三阶段吸魂仪式成立，但视觉同样全 dust；伤害用手动 StrikeNPC（可行）；无布面；与大招无联动。
- **SoulBannerGlobalNPC.cs（收魂钩子）**：**多人完全失效的 bug**——`OnKill` 只在服务器/单人执行，代码却 `if (server) return`，联机下永远收不到魂；`IsHoldingSoulBanner` 全弹幕数组 O(1000) 扫描（应 `ownedProjectileCounts` O(1)）。
- **SoulBannerPlayer.cs（成长数据）**：Tier 表与收益曲线成熟，保留；无 UI 脉冲通信字段；无大招资源接口。
- **SoulBannerUI.cs / UISystem.cs**：面板信息全但纯静态——魂数变化无脉冲、满魂无呼吸光、大招就绪无提示（附加透镜：UI 反馈不即时）。
- **SoulBannerMinionBuff.cs**：功能正常，描述需随新机制更新。
- **命中反馈栈**：已接 ACMWeaponBurst.Soul + 小震屏，但音效无 pitch 分层；击杀收魂只有 CombatText，无"灵魂归幡"的飞线演出（附加透镜：收集环节感知弱）。

结论：机制骨架保留（四段祭幡 / 三阶段仪式 / Tier 成长），**全面重做视觉层与资源循环闭环**，修复多人。

## 2. 系列主题与幻想感

引魂幡（招魂幡）——中国丧葬民俗中为亡者引路的白幡，在此化为法器：玩家是"掌幡人"，以幡收束枉死之魂为己用。应给玩家的体验：

1. **一面活的旗** ——幡后拖着一条由亡魂织成的灵绸，随挥动猛甩、驻留时波动，灵魂越多绸面亡魂面孔越清晰（布面 = 成长的可视化）。
2. **收魂的仪式感** ——每次击杀，一缕灵魂沿弧线飞归幡中（灵体飞线）；引魂驻留时幡尖张开真正的漩涡（shader）而非一团粒子。
3. **万魂齐哭** ——攒魂到阈值后主动引爆：悬浮幡聚魂→一拍静默→全屏哭嚎，亡魂军团扑向敌人。"存魂保被动 / 放魂打爆发"的核心决策点。

配色语言：消费 `ACMWeaponBurst.Soul(28)`（幽紫 180,120,255 / 210,165,255 / 95,40,165）为主调，大招收束时混入 `AbyssPurple(11)` 深渊紫（150,110,240 / 60,30,110）压暗定调。

## 3. 逐件机制设计

### 3.1 左键 · 祭幡（保留四段，重调手感曲线）

| 段 | 帧数(基准) | 曲线 | 新增 |
|---|---|---|---|
| 举幡 Raise | 12 | SineInOut 提起 + 末 3 帧 `pow(t,8)` 反向 reel-back 12px | 蓄势"猛吸一口气" |
| 祭幡 Thrust | 6 | **poly(10) ease-out**（首 2 帧走完 ~85% 行程） | 出击帧 recoil（幡整体回挫 6px 衰减）；残影仅在 strike act 门控开启 |
| 引魂 Channel | 20 × 成长(≤1.8) | 呼吸驻留 | 幡尖 **SoulBannerVortex** 漩涡 shader；吸魂弧 ribbon 保留；dust 减量 |
| 收魂 Retract | 8 | QuadIn 抽回 | SoulBurst 加 DrawShockwaveRing |

- 伤害判定不变（Raise 无伤害；杆身线段 / 漩涡圆域），视觉与判定严格对齐。
- 布面灵绸：9 节点弹簧链（阻尼 0.86 / 重力 / 锚点速度反甩），挥动时的次级运动天然产生；渲染走 `BuildRibbonStrip` + **SoulBannerCloth** shader。
- 音效分层：Thrust = Item71(高频质感) + Item1(低频挥动, Pitch −0.4)；命中 pitch 随机 ±0.15。

### 3.2 右键 · 智能分派（不破坏原语义）

- **悬浮幡不在场** → 召唤悬浮幡（原语义原样保留，含 buff 右键收回）。
- **悬浮幡在场**（原来是无效操作）→ 尝试**万魂齐哭**：
  - 灵魂 ≥ 80：向悬浮幡下达大招指令（消耗当前灵魂 40%，最少 80）；
  - 灵魂 < 80：拒绝使用 + 本地"魂力不足"提示（与原无效行为兼容）。

### 3.3 大招 · 万魂齐哭（全屏名额契约）

三拍结构（悬浮幡为施法台）：

1. **聚魂 40 帧**：幡升至玩家头顶上方 130px 急速旋转；全屏范围灵魂流线向幡收敛（charge-up grammar：收敛流线 + 切向轨道，密度 ∝ sqrt(t) 且 72% 后硬切静默）；`ApplyPaletteTint` 幽紫染屏（强度 ≤0.13，走全屏名额）。
2. **静默 8 帧**：粒子截断，幡收缩至 60%（pre-explosion collapse），音停。
3. **齐哭爆发**：起爆帧 RadialBloom(0.3) + 三重冲击环 + 震屏 9（大招预算内）+ 哭嚎音三层（NPCDeath52 ×2 阶梯 pitch + Zombie 低吼）；从幡中涌出 **8 + 消耗魂/40（上限 24）** 道哭嚎亡魂（SoulWailSpirit）。

亡魂弹幕 SoulWailSpirit：出生扇形爆出（launch is a set：22px/f 直线 9 帧）→ ×0.85 刹车 → 索敌追踪；ribbon 拖尾 + SoftGlow 头部；命中 = 300% 武器伤害 + ACMWeaponBurst.Soul + 微治疗（走 HealMultiplier）；单体穿透 1，2.5s 无目标消散。

### 3.4 收魂飞线（击杀反馈）

击杀结算瞬间（owner 端），生成 SoulWispVFX 纯视觉弹幕：从尸体沿贝塞尔弧线 14~22 帧飞向玩家，双层 ribbon 拖尾，到达时玩家身上柔光一闪 + UI 脉冲字段刷新。同屏上限 12，超出直接跳过（数值结算不受影响）。

### 3.5 悬浮幡（升级）

- 状态机迁移：phase 存 `ai[0]`、timer 存 `ai[1]`（netUpdate 同步，各端演出一致），冷却/计数移 localAI。
- 吸魂阶段：dust 符阵减量，改为对每个被吸目标画 `ACMShaders.DrawBeam` 吸魂光束 + 中心 SoulBannerVortex 小漩涡。
- 新增 UltCharge / UltBurst 阶段（§3.3）。
- 小型布面灵绸（7 节点，同一 cloth 辅助）。

### 3.6 UI（保留 Shift 面板 + 滚轮语义，提升即时反馈）

- **魂数脉冲**：`SoulBannerPlayer.lastGainTimer/lastGainAmount`，增魂后 30 帧内进度条尾端闪光扩散、魂数数字弹跳（scale bump 1.25→1）。
- **满魂呼吸光**：ratio ≥ 1 时边框呼吸紫辉 + 标题镀金。
- **大招就绪行**：魂 ≥ 80 时显示"◈ 万魂齐哭 · 就绪（右键悬浮幡引爆）"金色行；不足时灰色显示还差多少魂。
- 进度条填充改双色渐变 + 满魂流光。

## 4. 系列内梯度

单品系统武器，梯度体现在**成长阶段**上：低成长（<30%）布面暗淡近乎素幡、漩涡半透明；中成长布面符纹亮起；高成长（>70%）布面鬼影面孔隐现、幡体常态辉光；满魂 = 呼吸光 + 大招就绪。大招是唯一的全屏级演出（走名额契约），平时不占全屏资源。

## 5. 视觉技术方案

**复用共享件**：DrawRibbonTrail（吸魂弧/飞线/亡魂拖尾）、DrawShockwaveRing（收魂爆发/大招）、DrawGlowBurst、DrawRadialBloom（满魂脉冲/大招余波）、ApplyPaletteTint（大招定调，≤0.13）、ACMShaders.DrawBeam（吸魂光束）、ACMWeaponBurst.Soul/AbyssPurple（命中演出）、ACMShaders.NoiseTexture（s1 噪声）。

**新建系列专属着色器（均 ps_3_0，前缀 SoulBanner）**：

1. `SoulBannerCloth.fx` —— 灵绸布面：uv.x 沿布长 / uv.y 横宽；幽紫布底渐变 + 双八度滚动噪声织纹 + 流动符纹亮丝 + 尾端噪声破边 + `uGrowth` 控制鬼影面孔斑块显现 + `uFlash` 大招白闪。Additive 绘制在 BuildRibbonStrip 网格上。
2. `SoulBannerVortex.fx` —— 吸魂漩涡：极坐标螺旋臂（atan2 + 径向相位）+ 向心流动噪声 + 内核亮斑 + `uProgress` 从中心向外展开；引魂阶段（半径 ~90px）与大招聚魂（~200px）复用。

获取一律 `WeaponVFX.GetEffect("SoulBannerCloth")`（带缓存），不注册进 ACMShaders。

## 6. 平衡与定位

- 获取途径 / 稀有度 / 职业（召唤系数值、魔法伤害判定的混合原样保留）、Tier 表、成长收益曲线（伤害 +0~200% 等）**全部不动**。
- 左键 DPS：基准伤害 52 / useTime 30 不变，命中逻辑不变 → 常态 DPS 变化 0%。
- 悬浮幡：伤害节奏不变（8 帧/tick AoE）→ 0%。
- 新增大招：资源兑换的间歇爆发，非持续 DPS。消耗 40% 灵魂（≥80）意味着被动伤害倍率立刻下降（满魂 +200% → 放完剩 60% 魂 +120%，降 27% 常态输出），换一次爆发：
  - 前期首个可用点（cap 120，攒 80 魂）：8 魂 × 300% × 52 ≈ 1.2k 总伤，约等于 20 次左键命中，需 80 次击杀积攒——合理。
  - 满魂后期（cap 37500，耗 15000）：24 魂 × 300% × 52 × 3.0(成长) ≈ 11k 总伤——对月后 Boss 血量（10⁵~10⁶）是节奏点而非斩杀。
- 结论：常态 DPS 0 变化，大招 ≤ 每分钟一次量级的资源爆发，符合 ±15% 约束精神。

## 7. 性能与多人预算

- **多人修复（本次重点）**：收魂从 `OnKill`（仅服务器执行，原代码直接 return = 联机全失效）迁移到 `HitEffect` + `npc.life <= 0` 判定，用 `Main.myPlayer == npc.lastInteraction` 守卫——灵魂数据只在 owner 客户端结算与保存，伤害计算本就在 owner 端（ModifyWeaponDamage），**零网络包**即多人安全。DoT 类非直击击杀在联机下可能漏结算（服务器侧击杀不回放 HitEffect），属可接受边角（远优于现状的完全失效）。
- 大招指令走 `Projectile.ai[]` + `netUpdate` 同步；扣魂/生成弹幕仅 owner 端；染屏/震屏/音效均本地表现层。
- 性能：`ownedProjectileCounts` O(1) 替代全弹幕扫描；HitEffect 仅受击帧触发；漩涡 dust 减量 ~50%（shader 承担形体）；cloth 模拟 9 节点纯向量运算；Effect 全部静态缓存（GetEffect）；飞线同屏 ≤12；拖尾受 MythologyConfig.Trail 降级；全屏后处理均走 RequestFullscreenSlot。
- 每帧分配：cloth 节点数组复用成员字段；ribbon 点列为小数组（≤10 元素）按需构建（与仓库现有拖尾同量级）。

## 8. 实施清单

1. `SoulBannerPlayer.cs`：+UltMinSouls/UltReady/TrySpendUltSouls/RegisterGain 脉冲字段（lastGainTimer 等）；收益曲线不动。
2. `SoulBannerGlobalNPC.cs`：OnKill → HitEffect 重写；O(1) 持有判定；击杀结算 + 飞线生成 + Boss tier 解锁。
3. `Effects/SoulBannerCloth.fx` / `SoulBannerVortex.fx`：新建 + 按名编译过（退出码 0）。
4. `SoulBannerHeldProj.cs`：曲线重调（reel-back / poly10 / recoil）+ cloth 灵绸 + 漩涡 shader + 音效分层；文件内新增 SoulBannerClothSim 辅助、SoulWailSpirit、SoulWispVFX。
5. `SoulBanner.cs`：右键智能分派 + 大招下达；Tooltip 动态行加大招状态。
6. `SoulBannerMinion.cs`：状态机 ai 布局迁移 + UltCharge/UltBurst + 吸魂光束 + 小灵绸。
7. `SoulBannerUI.cs`：魂数脉冲 / 满魂呼吸 / 大招就绪行。
8. `SoulBannerUISystem.cs`：交互语义（Shift 显隐/图层插入）验证无需改动，保持原样。
9. 验证：专属着色器按名编译退出码 0；影子工程全量语义编译过滤本系列错误清零（ReadLints 语言服务对新文件未生效，以影子编译为准）。
10. 最后一步：两个 hjson 的 SoulBanner.Tooltip 与 SoulBannerMinionBuff.Description 更新（StrReplace 小步编辑 + 回读验证）。
