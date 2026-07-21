# 杂项散件武器 MiscScatter —— 重做设计文档

> 管辖: `Items/Weapons/NiuMa/NetherChainBlade.cs`、`Items/Weapons/NiuMa/SoulHookWhip.cs`、
> `Items/Weapons/Bows/BlackBearBow.cs` + `Projectiles/BlackBearBowProj1.cs`、
> `Items/Weapons/Staffs/Pufferfish.cs` + `Projectiles/PufferfishProj1.cs`、`Items/Weapons/CoffinNail.cs`
> 五件散件无系列贯穿机制, 目标是**给每件立住独一无二的机制身份**, 各自有小型峰值时刻。

---

## 1. 现状诊断 (八条透镜逐件)

### 1.1 冥链刃 NetherChainBlade (牛头马面掉落, 近战 58, Pink)
- **一眼身份**: 原版 ChainKnife 贴图 + 原版链贴图, 视觉上是"链刀换皮"; 双钩机制存在但不可见。
- **三段感**: 出手匀速 (shootSpeed 14 + ×0.985 缓减), 回收 lerp, 无 set-launch/hard-brake, 无甩链重量。
- **命中反馈栈**: 只有 dust + 单音效, 无 Burst/震屏。
- **致命机制缺陷**: 双钩状态 (`HookA/HookB`) 存在弹幕 ai 上, 而弹幕全程 ~1 秒即回收销毁 → 钩链脉冲几乎没有存活时间, 机制形同虚设; 链间脉冲用 `SimpleStrikeNPC` (不吃玩家伤害加成)。
- **轨迹/演出峰值/一致性**: 设了 TrailCache 但没画拖尾; 无大招时刻; 与勾魂索 (Wraith 尘) 配色呼应弱。
- 判定: **全面重做**。

### 1.2 勾魂索 SoulHookWhip (牛头马面掉落, 召唤鞭 52, Pink)
- **一眼身份**: 原版 ThornWhip 贴图 + `DrawWhip_WhipBland`, 荆棘鞭换皮; "勾向玩家"有想法但不可读。
- **机制缺陷**: debuff 持续拉拽的归属判定用 `MinionAttackTargetNPC == npc.whoAmI` 反查 (hacky, 多人下不可靠); `SoulHookWhipDebuff` 标了 `IsATagBuff` 却没有任何 tag 伤害数值 (残缺实现); 把敌人持续拉向召唤师本身对召唤职业是负收益。
- **轨迹**: `DrawSoulChain` 在鞭根与鞭梢间画一条**直线**链贴图, 完全无视鞭形曲线, 视觉是 bug 级。
- **命中反馈/峰值**: dust + 音效, 无 Burst, 无节奏高点。
- 判定: **全面重做**。

### 1.3 金金弓 BlackBearBow (黑熊精掉落, 远程 20, Green)
- **一眼身份**: 射出的"箭"用的是 **BlackBear Boss 头像贴图**旋转乱飞 — 荒谬。
- **数值 bug 群**: 弹幕 DamageType 是 **Melee** (远程弓吃不到远程加成); `OnSpawn` 里 `damage += rand(50%~100%)` 在各端各自 roll (多人不同步); Shoot 里 `ConsumeItem` 手动再耗一次箭 (tML 已自动消耗 → **双倍耗箭**); useTime 15 / useAnimation 25 不一致。
- **三段感/机制深度**: 匀速平射, 箭种被无视 (任何箭都变同一弹幕), 无决策。
- **较好的底子**: 已接入 Bronze Burst 与双层拖尾 (近期有人补的), 但配色未跟随 Boss 重做后的"暗风墨紫 + 蜂蜜琥珀"语言。
- 判定: **全面重做** (机制 + 视觉 + bug 清理)。

### 1.4 河豚鱼 Pufferfish (老爹商店 100 铂金, 魔法 1111, Red, 彩蛋件)
- **一眼身份**: 举起河豚喷"河豚头+身+尾三段贴图"激光 — 幽默气质是资产, 要保留。
- **死代码**: 弹幕 AI 里整段 `player.channel` 分支永远不执行 (Item.channel=false), 激光实为 60 帧定时弹, 连发时同屏 2-3 根重叠。
- **机制深度**: 零决策; `usesLocalNPCImmunity` 未设 (依赖全局 immune, 多激光重叠时伤害节流互相干扰)。
- **演出峰值**: 仅有一次成形 bloom; "鼓胀→炸刺"的河豚幻想完全没做。
- 判定: **全面重做机制** (channel 蓄力鼓胀), 保留三段贴图资产与幽默人设。
### 1.5 棺材钉 CoffinNail (赢勾残片合成, 近战 420, Red)
- **一眼身份**: "钉子"却是 10.5 强度制导 + 命中弹跳 — 钉的果断感全无; 物品 Melee / 弹幕 **Ranged** 定位分裂 (吃不到近战加成)。
- **机制深度**: 无叠层无起爆, "阴钉封棺"幻想没立住; 命中一次 57 颗 dust 的粗暴堆料。
- **三段感/峰值**: Swing 匀速; 无大招时刻; 残留死代码 (`CreateScreenShake`/`hasHitTarget`/`trailCounter` 无人调用)。
- **较好的底子**: 已接入 Fatal Burst + 血色 ribbon 拖尾。
- 判定: **全面重做** (钉入叠层 + 七星封棺起爆)。

## 2. 系列主题与幻想感

五件散件各立门户, 但共享一个设计信条: **散件也要有可被一句话复述的机制身份 + 一个小型峰值时刻**。

| 武器 | 幻想 | 一句话身份 | 峰值时刻 |
|---|---|---|---|
| 冥链刃 | 牛头差役的**锁** — 锁魂对撞 | 锁一个、链两个、第三击对撞 | 锁魂对撞 (三击循环) |
| 勾魂索 | 马面差役的**勾** — 三痕收魂 | 三鞭刻痕、第四鞭收魂 | 收魂拉拽 (四鞭循环) |
| 金金弓 | 黑熊精的黑风与蜂蜜 | 黑风驱矢、五矢一蜜 | 蜂蜜重矢 (五射循环) |
| 河豚鱼 | 可爱但危险的鼓胀河豚 | 喷水越久豚越鼓, 鼓满打喷嚏炸刺 | 喷嚏爆刺 (蓄力循环) |
| 棺材钉 | 北斗七星钉封尸棺 | 七钉封棺, 满钉起爆 | 七星封棺 (七钉循环) |

**牛马搭档呼应** (神话原型: 地府拘魂差役牛头执枷锁、马面执勾索): 冥链刃走**青蓝冥焰** (`ACMWeaponBurst.NetherGrudge`, 低沉音效 pitch 偏负), 勾魂索走**幽紫魂色** (`ACMWeaponBurst.AbyssPurple`, 尖锐音效 pitch 偏正), 两件共用同一条"魂链"着色器语言 — 一锁一勾, 双色成对。

**黑熊精呼应**: 只读 `NPCs/Boss/BlackBear/BlackBearSky.cs` 的配色常量 (墨黑 8,6,14 / 风紫 52,36,78 / 袈裟金 255,209,107), 弓的黑风箭用墨紫风尾、蜜矢用琥珀金 — 与 Boss 重做后的"黑风 + 蜂蜜"场地语言同族。严禁写入 Boss 文件、不引用 Boss 代理的 .fx。

**棺材钉文化梗**: 民俗"钉棺"用七根钉 (北斗七星钉/子孙钉), 镇尸防诈。第七根钉落下 = 封棺 = 起爆, 峰值有仪式感。

## 3. 逐件机制设计

### 3.1 冥链刃 — 锁魂对撞 (三击循环)

- **投掷手感** (前摇-爆发-收招): 出手 1 帧 set 28px/f (爆发), 飞行 7 帧全速后 ×0.86/f hard-brake 至 ~5px/f ("链到尽头"的顿挫), 悬停旋转加速 6 帧 (钩魂窗口), 然后回收 — 回收速度 12→30px/f 二次方渐增 (收链越拉越快), 到手帧: 柔光闪 + Grab 咔声 + 震屏 1。链条绘制带**悬垂下坠** (速度低时下垂、绷紧时拉直) — 锁链物理感。
- **锁魂循环** (机制状态迁移到 GlobalNPC + 从属弹幕, 修复"状态随弹幕死亡"缺陷):
  1. 命中敌人 A → A 获得**魂锁标记** (10s, 身上有幽蓝链环绕视觉);
  2. 命中另一敌人 B (A 仍带标记) → 生成**魂链** (从属弹幕, 8s): A-B 间持续锁链, 每 40 帧一次链脉冲 (30% 武器伤, 走正常弹幕命中管线, 吃加成), 脉冲行波沿链跑 + 两端微互拽;
  3. 再次投掷命中 A 或 B (链端点) → **锁魂对撞**: 伤害 ×2.2, 两端点被猛力互拽 (按 kbResist 缩放), 链中点 NetherGrudge Burst ×1.9 + 震屏 3, 链销毁, 循环重开。
- **决策点**: 链存在时 — 让脉冲多跑几轮 (持续伤害+控制) vs 立刻第三击引爆 (爆发); 也可以故意把链留着打第三个敌人。
- 帧数: 出手全速 7f / brake 8f / 悬停 6f / 回收 ~22f, 全循环 ~43f。

### 3.2 勾魂索 — 三痕收魂 (四鞭循环)

- **魂痕**: 鞭命中叠 1 层魂痕 (上限 3, 8s 衰减, GlobalNPC 承载, 敌人头顶显示 1~3 道幽紫抓痕);
- **收魂**: 对已满 3 痕的敌人再鞭 → 该鞭伤害 ×1.8, 清空魂痕, 敌人被猛拽向玩家 (保留原"勾向玩家"的时刻感, 但从常驻改为峰值瞬间, kbResist 缩放), 一缕魂魄从敌人飞回玩家, 到达时给玩家 **拘魂 buff** (+8% 召唤伤害, 4s) + AbyssPurple Burst ×1.6;
- **修复 tag**: 魂痕作为正经鞭 tag — 召唤物命中带痕敌人 +4 固定伤害 (`ModifyHitByProjectile`), 移除原来不可靠的 `MinionAttackTargetNPC` 持续拉拽与 `SoulHookWhipDebuff` 的 lifeRegen (债务清理: 该 buff 类保留为轻量冥焰 DoT 视觉债务兼容, 不再承担拉拽);
- **鞭 crack 时刻**: 鞭到最远点帧 (ai[0] ≈ timeToFlyOut/2) 播 crack 音 + 鞭梢柔光闪 — 原版鞭动画自带 anticipation, 补上爆发帧反馈;
- **绘制**: 保留 `DrawWhip_WhipBland` 底层 + 沿鞭控制点整条幽紫双层 ribbon + 鞭梢"勾"光 (LightShot); 删除直线假链。

### 3.3 金金弓 — 黑风蜜矢 (五射循环)

- **bug 清理**: 弹幕改 Ranged; 删双重耗箭; 删不同步随机伤害/击退; useTime = useAnimation = 22;
- **黑风箭** (普通射击): 任何箭化为黑风箭 — 墨紫风尾双层拖尾 + 微弱风导 (0.6° /f 转向上限, 只在 30 帧后启用, 保持可读的直线感), 命中 Shadow 主题 Burst 小规模;
- **蜂蜜重矢** (每第 5 射, item 端计数, TidecallersDecree 同款模式): 大型琥珀箭 (scale 1.35, 伤害 ×1.7), 无风导重弹道 (轻微下坠), 命中: 迟缓 3s + 炸出 3 颗蜂蜜溅滴 (抛物线小弹幕, 40% 伤害) + 琥珀 Burst ×1.6 + 震屏 2; 蜜矢就绪时持弓手上滴落金蜜微粒 (纯视觉);
- **音高分层**: 黑风箭 Item5 pitch 随机; 蜜矢 Item5 pitch -0.25 + Item97 蜂鸣叠层。

### 3.4 河豚鱼 — 鼓胀喷嚏 (蓄力循环)

- **channel 高压水**: Item.channel = true; 按住持续喷高压水激光 (保留头/身/尾三段贴图), 每 20 帧扣 3 魔; 伤害 tick 走 `usesLocalNPCImmunity` cooldown 10 (对齐旧基线 6 tick/s, 同屏仅 1 根激光);
- **鼓胀**: 引导中河豚头 scale 1→1.55 (Y 轴胀得更多, 憋气感), 蓄力越满抖动频率越高 (±3% scale jitter), 尾鳍摆动加速, 激光宽度轻微同步变粗 (纯视觉);
- **喷嚏爆刺** (峰值): 蓄满 150 帧自动触发, 或松手且蓄力 ≥40% 触发 — "阿嚏!": 朝向锥形 5 根 + 全向 8 根水刺 (55% 伤害, 快速短命), Water Burst ×1.8 + 大水花 + 震屏 3 + 玩家被轻轻向后推 3px/f (幽默后坐); 触发后河豚缩回, 继续按住则无缝开始下一轮;
- 松手且蓄力 <40%: 只打个小水花 (可爱地漏气);
- **决策点**: 维持激光单体 DPS vs 攒满喷嚏拿 AoE 爆发; 走位需要时提前松手止损。

### 3.5 棺材钉 — 七星封棺 (七钉循环)

- **直投手感**: 删制导删弹跳; extraUpdates 2 + shootSpeed 16 → ~48px/f 快直投 (钉的果断), 出手低频 whoosh + 高频金属双层音;
- **钉入叠层**: 命中敌人 → 钉**留在敌人身上** (stuck 弹幕, offset 存 velocity 字段, Daybreak 同款), 每根钉每 45 帧 tick 12% 武器伤 (走命中管线), 10s 后脱落; 每根钉入音高递增 (-0.30 + n×0.08 — "钉、钉、钉"上行音阶, 听觉读条);
- **七星封棺** (峰值): 同一敌人身上第 7 根钉钉入 → 封棺: 45 帧演出 (棺形封印 decal 从上向下合盖 + 北斗七星逐颗点亮 + 敌人减速 60%), 然后**起爆**: 350% 武器伤 (160px 圆内全额), LethalRed 级 Burst ×2.2 + 冲击环 + 震屏 5, 所有钉飞散; 该敌人 2s 内不可再被钉 (防循环锁死);
- **定位修复**: 弹幕 DamageType 改 Melee (与物品一致)。

## 4. 系列内梯度

散件无系列递进, 按**进度位分配演出预算**:

- **金金弓** (早期, Green): 最朴素 — 全共享原语 (拖尾/柔光/Burst), 零专属 shader;
- **冥链刃 / 勾魂索** (肉山后, Pink): 中档 — 共享原语 + 共用 1 个魂链条带 shader (两件分摊成本, 双件成对的视觉记忆点);
- **河豚鱼** (彩蛋, Red): 中档 — 贴图资产复用 + 共享原语堆演出, 幽默靠动画曲线不靠 shader;
- **棺材钉** (后期, Red): 旗舰 — 专属封棺 decal shader + 最完整的峰值仪式 (合盖→点星→起爆)。

## 5. 视觉技术方案

| 项 | 方案 | 新建/复用 |
|---|---|---|
| 魂链条带 (冥链刃玩家↔刃 / A↔B 魂链 / 收魂拉拽) | `NetherChainSoulLink.fx` (ps_3_0, TriangleStrip 条带): 链节明暗周期 + 魂火流动噪声 + 勾魂行波亮斑 (uPulse); 顶点走 `ACMUtils.BuildRibbonStrip`, BeamGrad 同约定 | **新建** (牛马双件共用) |
| 封棺印记 | `CoffinNailSeal.fx` (ps_3_0, 屏幕空间 decal): 中式棺形 SDF (上宽下窄) + 合盖扫线 (uProgress) + 北斗七星逐颗点亮 + 咒纹噪声 + 起爆白闪; 经 `ACMShaders.DrawScreenSpaceDecalStandalone` (Additive) 绘制, `WorldDecalParams` 换算 | **新建** (旗舰件专属) |
| 链条实体感 | 原版 Chains[0] 贴图沿悬垂曲线分节铺设 (每节独立旋转) + shader 条带叠加 | 复用 |
| 各弹幕拖尾 | `WeaponVFX.DrawProjectileTrail` / `DrawRibbonTrail` 双层 | 复用 |
| 命中反馈 | `ACMWeaponBurst.Spawn` (NetherGrudge/AbyssPurple/Shadow/Water/Fatal + Bone 白) + `WeaponVFX.AddScreenShake` 预算内 | 复用 |
| 爆发环/柔光 | `WeaponVFX.DrawShockwaveRing` / `DrawGlowBurst` / `DrawRadialBloom` (自带名额退化) | 复用 |
| 魂痕/魂锁标记 | GlobalNPC PostDraw 程序化 (Sparkle/LightShot 遮罩) | 复用 |
| 河豚鼓胀 | 纯动画曲线 (scale XY 不对称 + jitter 频率随蓄力) + 三段贴图 | 复用 |

不新增任何贴图; 新弹幕用原版贴图 (箭/链) 或 `InnoVault/Assets/placeholder` + 程序化绘制。无全屏后处理常驻 (Burst 内部的 RadialBloom 走名额契约自动退化)。

## 6. 平衡与定位

获取途径 / 稀有度 / 职业定位全部不变 (弓修复为真 Ranged、棺钉弹幕修复为 Melee 属于**定位修复**而非变更)。

| 武器 | 旧有效 DPS (估) | 新 DPS (估) | Δ | 论证 |
|---|---|---|---|---|
| 冥链刃 | ~66 (58 × ~1.13 掷/s; 双钩形同虚设) | 单体 ~75; 链循环 ~100 | +7% 单体 | 循环收益要求持续命中两个不同目标再回打, 操作成本换群体职能; 脉冲改走命中管线 (原 SimpleStrike 不吃加成) |
| 勾魂索 | 52/鞭 + 残缺 tag | 48/鞭, 四鞭循环均值 ×1.2 ≈ 57.6 + tag 4 | ~+11% (+tag) | 基伤 52→48 抵扣收魂倍率; tag +4 是鞭类标配的补全 (原来标了 IsATagBuff 却无数值) |
| 金金弓 | ~96 (随机 +72.5% 期望, 但多人不同步 + 双倍耗箭) | (28+箭)×2.73/s ≈ 90 + 蜜矢循环 ≈ +10 | ~+4% | 伤害 20→28 吸收"删除随机伤害"的期望损失; useTime 25→22 |
| 河豚鱼 | ~6.7k (1111 × 6 tick/s) | 基线一致 + 喷嚏场景性 +~12% | +0~12% | 彩蛋位; 1111 彩蛋数字保留; 爆刺要求 2.5s 蓄力且刺对单体最多命中 2-3 根 |
| 棺材钉 | ~1008 (420 × 2.4/s, 追踪高命中) | 300 × 2.4/s = 720 + 起爆 350%/7 钉 ≈ +345 + DoT ~30 ≈ 1095 | ~+9% | 基伤 420→300 换取起爆循环; 删除制导后实际命中率下降, 有效 Δ 更小; AoE 半径 160px 为对群补偿 |

全部落在 ±15% 论证带内。

## 7. 性能与多人预算

- **多人安全**: 所有弹幕生成在 owner 端 (`Shoot` / `Projectile.owner == Main.myPlayer`); 伤害倍率走 `ModifyHitNPC` (命中端权威); 钉/魂链状态承载在弹幕 `ai[]`/`velocity` (同步字段) 与 GlobalNPC 计时 (owner 端消费, 视觉各端自行渐灭); 敌人拉拽冲量 kbResist 缩放 (与原实现同风险面); 蜜矢/蓄力计数为 item/弹幕本地状态 (仅 owner 消费)。
- **性能**: shader 经 `WeaponVFX.GetEffect` 静态缓存; 链节 sprite ≤22 节/条; 魂链同 owner 同屏 ≤1 条; 钉每目标 ≤7; 刺每次 ≤13 根; dust 全部节流 (相比旧棺钉单次命中 57 颗大幅缩减); 无每帧 LINQ/分配 (链曲线用复用数组); 拖尾受 `MythologyConfig.Trail` 降级。
- **震屏预算**: 小命中 ≤2 / 蜜矢·爆刺 2-3 / 对撞·封棺起爆 3-5, 均走 `WeaponVFX.AddScreenShake` (取 max 不累加)。

## 8. 实施清单

1. `Effects/NetherChainSoulLink.fx` + `Effects/CoffinNailSeal.fx` — 新建, 按名编译退出码 0;
2. `Items/Weapons/NiuMa/NetherChainBlade.cs` — 投掷手感重做 + 锁魂循环 (GlobalNPC 标记 + 魂链从属弹幕 + 对撞) + shader 链绘制;
3. `Items/Weapons/NiuMa/SoulHookWhip.cs` — 三痕收魂 + tag 修复 + crack 时刻 + ribbon 鞭光 + 魂魄回收弹幕 + 拘魂 buff;
4. `Items/Weapons/Bows/BlackBearBow.cs` + `Projectiles/BlackBearBowProj1.cs` — bug 清理 + 黑风箭/蜂蜜重矢/蜜滴三模式;
5. `Items/Weapons/Staffs/Pufferfish.cs` + `Projectiles/PufferfishProj1.cs` — channel 鼓胀 + 喷嚏爆刺 + 水刺弹幕;
6. `Items/Weapons/CoffinNail.cs` — 直投 + 钉入 stuck + 七星封棺起爆 + decal 绘制;
7. ReadLints 全部改动文件清零;
8. hjson (最后一步, zh-Hans + en-US 同步, 小步 StrReplace + 回读验证): 五件物品 Tooltip 更新, 弹幕/buff 新键尽量走代码 `Language.GetOrRegister`。
