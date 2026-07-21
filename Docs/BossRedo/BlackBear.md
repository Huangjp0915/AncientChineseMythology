# 黑熊精 BlackBear —— Boss 重做设计文档 (V3)

> 单元: `NPCs/Boss/BlackBear/` | 题材: 《西游记》黑风山黑熊精 (黑风大王) | 定位: 早期地面型 Boss (丛林地表)
> 核心课题: **重量感** —— 笨重巨兽的压迫感, 同时保持早期 Boss 的公平难度。

---

## 1. 现状诊断

以 choreography skill 七大本能与失败模式清单为透镜:

1. **FSM 完全不可同步 (技术底线违规)**: `currentState / stateTimer / attackCooldown / lungeDirX / furyTriggered / isDying` 全部是私有字段, 从不走 `npc.ai[]` 也无 `SendExtraAI`, 多人下客户端各演各的。
2. **零入场 / 零死亡演出**: 召唤后 Boss 直接杵在原地; 死亡是"站着播 6 帧动画 ×2 → 掉落", 无任何世界层反馈 (PACING §6 三大节拍缺其二)。
3. **动作失重 (本能 #1/#2/#3 全违反)**:
   - 追击是恒定 lerp 到 11 px/f 的匀速跑, 无脚步落地反馈;
   - 熊抱冲撞是 35 帧常值 `velocity.X = 18f`, launch 不是 set、结束无 hard brake, 冲完像"关掉引擎的车";
   - 挥击前摇只有 `FaceTarget`, 身体无任何后仰/counter-motion; 全程无 recoil、无二次运动;
   - 动画与判定不对齐: 攻击贴图 10 帧 ×6f=60f, 而 Attack_1 总长仅 52f (播不完), BearHug 120f (循环播两遍)。
4. **弹幕质量差**: Attack_2 扑投用 **32×32 的 Boss 头像图标**随机大小/旋转乱洒 (穿墙、无预警、timeLeft 360); Proj4 是无引用死代码; Proj3 光环凭空瞬移到玩家身边 (teleports feel cheap)。
5. **脱战白送掉落 bug**: `UpdateDespawnSight` 失去视野 10s 后 `NPC.life=0 → checkDead`, 会被 `HitEffect` 拦截进死亡动画并**正常执行 `NPCLoot()`** —— 躲起来数 10 秒白拿掉落。
6. **P2 "换规则"太薄**: 蜂蜜滴落每 55f 掉 1~2 颗 + 6 球光环, "蜂蜜"没有形成任何场地机制; 狂怒只是换贴图+描边。
7. **无专属着色器/天空**, 视觉全部是共享件的最低限度用法 → 按简报 §3.1 判定: **全面重做**。

保留的好底子: King Slime 式"仅激活帧接触伤害"契约、TelegraphColors 红线/地纹预警、平台下穿与卡墙起跳的地面物理、蜂蜜液体下沉处理、fury 双套贴图。

## 2. 设计主题与幻想感

**"黑风山大王" —— 三个意象叠成一头熊:**

1. **重 (熊的体魄)**: 每一步扬尘、每次落地震屏、冲撞碾裂大地。玩家闭眼听震动都该知道熊在哪。
2. **黑风 (妖风)**: 原著黑风大王驾黑风而行。入场乘黑风砸落, P2 黑风漫天 (专属 Sky + 全屏风扭曲), 大招"黑风怒嚎"。
3. **金 (袈裟因缘)**: 它偷的是锦襕袈裟, 结局是被观音收服皈依。金冠光环 (P2)、死亡不是"死"而是**被金光收服** —— 金色是它的宿命色。

色彩语言: 黑风(墨黑/暗紫) + 尘土(土黄) + 袈裟金(TelegraphColors.Gold) + 致命红(TelegraphColors.Lethal, 只给真伤害预警)。蜂蜜场地机制保留并做厚 (琥珀金, 与袈裟金同族)。

玩家体验一句话: **"一头躲不开视线的重型卡车, 在越来越黑的风里, 学会读它的每一次起手。"**

## 3. 阶段结构与血量断点

| 阶段 | 血量 | 规则 |
|---|---|---|
| Intro | — | 入场演出 ~168f, 全程无敌零伤害 |
| P1 | 100%~50% | 地面追击 + 5 招基础池 (shuffle-bag 防重复) |
| Enrage 节拍 | 50% 触发一次 | 清弹 + 咆哮相变演出 90f (i-frame), Sky 切黑风漫天 |
| P2 | 50%~25% | 招式升级 + 新增黑风连环冲 / 蜜雨咆哮 / 金冠光环; 攻速节奏加快 (冷却 90→66) |
| P2 后期 | <25% | 解锁一场一次大招"黑风怒嚎", 大招后 90f 疲劳输出窗口 |
| Dying | HP→0 | 收服演出 ~210f: 踉跄 → 金缚 → 静默 → 金光爆发 → 消散 |

## 4. 招式编排表

所有攻击遵守: 接触伤害仅激活帧开启; 预警必有"形状+颜色+渐强"; 每个状态有超时保底出口; 状态切换 `netUpdate`。

### P1 基础池 (按距离过滤的 shuffle-bag, 不与上一招重复)

| 招式 | 前摇 | 爆发 | 收招 | 预警 | 公平阀门 |
|---|---|---|---|---|---|
| **重踏挥击 Swipe** (近距) | 28f 后仰蓄力 (rotation 后倾 + 前爪离地尘) | 6f 猛砸 (poly ease, 身体前冲 4px/f) + 地面波贴图 + 屏震5 | 20f 前倾回正 | 后仰姿态本身 + 蓄力尘 | 激活帧仅 28~42f; 近身才选 |
| **蜜蜡投掷 Toss** (中远) | 30f 上仰蓄力 | 1f 甩头掷出 3~5 颗抛物线蜜蜡弹 (recoil -3px/f) | 24f | 上仰 + 弹体飞行全程可见, 落点弧线可预判 | 弹幕 tileCollide, 不穿墙; 落地只留小蜜潭 |
| **熊抱冲撞 BearHug** (中距) | 50f 张臂 + 刨地后退 (pow(t,3) 后移 26px) | 1f set 22px/f 直线冲 (×1.012/f 微加速), 最多 34f | 命中距离/撞墙即 hard brake ×0.6/f + 滑行尘 | 红色冲撞线 (DrawBeam) 渐强 + 刨地尘向后喷 | 方向蓄力期锁定; 冲程 ≤ 750px 栓绳; 撞墙自伤震屏反馈 |
| **立地震地 Slam** (远距) | 108f 站立蓄力 (汇聚粒子 ∝√t, 72% 后静默) | 小跳 10f → 落地 GroundShock×4 双向 + 地面波 + 屏震9 | 30f | 红色地纹圈渐强 + 蓄力泛光 + 汇聚尘 | 蓄力站桩 = 远程输出窗口; 冲击波贴地, 跳跃可躲 |
| **跃扑 Pounce** (中距) | 30f 蹲伏 (scale.Y 压 0.88) | 弹道跳向玩家预测点 (lead ×12f), 空中激活 | 落地 squash 回弹 + 尘环 + 屏震6 + 12f 硬直 | 蹲伏剪影变化 + 落点地影 | 落点预测只取起跳瞬间, 空中不追踪; 落地硬直可惩罚 |

### P2 新增 / 升级

| 招式 | 前摇 | 爆发 | 收招 | 预警 | 公平阀门 |
|---|---|---|---|---|---|
| **黑风连环冲 FuryRush** | 每段 24f 转身蓄力 | 3 连冲 (each 14f @26px/f), 段间甩 2 道黑风爪痕 (Proj4 弧线风刃) | 末段后 40f 大喘气输出窗口 | 每段红线重新画 + 黑风聚拢 | 段间 24f 读方向; 风刃 ≤2/段; 三段封顶 |
| **蜜雨咆哮 HoneyRoar** | 36f 仰天蓄力 | 咆哮 → 8~10 颗蜜雨 (落点地影预警 45f) 分两波 | 30f | 每颗蜜雨独立地影渐强 | Boss 咆哮全程站桩; 蜜潭同屏 ≤6 上限 |
| **金冠光环 Halo** (复发) | 光环从 Boss 头顶**飞行抵达**玩家周围 (25f, traveled 非瞬移) | 环绕 150f (6 颗留缝) → 45f 向内收拢 | — | 金色环绕期无害→变红收拢 | 淡入+飞行期无伤害; 收拢前变色 20f |
| **黑风怒嚎 TempestHowl** (<25%, 一场一次) | 90f: 黑风向体内汇聚 + 屏幕渐暗 + rumble 渐强 | 12f 全静默 → 爆发: 双波环形风刃 + 全屏黑风横扫 + 屏震12 | 90f 疲劳喘气 (最大输出窗口) | 屏幕变暗本身即全局预警 + 汇聚粒子 | 风刃环有固定缺口; 疲劳期是给玩家的奖励 |

### 追击 (连接组织)

- 追击速度目标 11.5px/f, 但**起步/刹车有惯性坡** (加速度 0.35/f), 转身要 8f 缓冲——重量在起停里;
- 每第 14 帧落一次脚: 脚步尘 + 距离衰减微震 (≤1.2);
- 追击 45f 后必然进入选招 (无冷场); 玩家距离 >1100px 时偏置选择 BearHug/Pounce 拉近 (防风筝拉扯)。

## 5. 三大演出脚本

### 入场 (Intro, ~168f)
1. 0~48f: 黑风前兆——目标玩家四周黑风粒子横扫渐密, Sky intensity 快速升到 0.5, rumble 渐强 (≤3), 天色压暗;
2. 48f: 熊从玩家侧上方 (X±420, Y-560) 以 26px/f 砸落 (真实位移, 非瞬移);
3. 着陆帧: 屏震 10 + 地面波贴图 + 双向尘暴环 + 黑风散开 (全屏风扭曲脉冲 0.8);
4. 着陆后 54f **完全静止** (menace is stillness), 最后 24f 缓缓抬头;
5. 咆哮 (Roar 音效 + RadialBloom 金橙 + 头顶金冠闪) → 入战。全程 `dontTakeDamage`、`damage=0`。

### 换阶段 (Enrage, 90f, 50% 一次)
1. 触发帧: **清空全部己方弹幕**, 屏震 8, 咆哮音效;
2. 0~50f: 跪伏低头, 黑风从屏幕四周向体内汇聚 (converging streaks, 密度 ∝ √t, 后 1/4 静默);
3. 50f: 起身怒嚎——黑风爆发环 + Sky 切到 P2 满强度 + 全屏风扭曲脉冲 1.0 + 屏震 10 + 金冠常亮;
4. 90f 结束, `attackCooldown=45` (transition 后首招延迟, 防telefrag)。全程 i-frame。

### 死亡 (Dying, ~210f, "收服"而非"死亡")
1. HP→0 拦截: 清弹, 无敌, 停止一切攻击;
2. 0~60f: 踉跄——交错后退小步 + 每步尘土 + 黑风从身体丝丝逸散 (妖气散去), Sky 黑风快速退潮;
3. 60~120f: 三道金色光带自天垂落缠绕 (DrawBeam 金色细带), 熊挣扎 (±0.06 rad 抖动), 金光渐亮;
4. 120~132f: **12f 全静默** (光带定格, 粒子停止);
5. 132f: 金光爆发——RadialBloom 满强度 + 全屏金风脉冲 + 屏震 14 (一场唯一的最大震) + 白闪;
6. 132~200f: 身体在金光中上浮消散 (alpha 渐隐 + 金尘上升), Sky 转金色余晖;
7. 200f: 服务器端走标准 `NPC.checkDead()` → OnKill 设 downed + 掉落。同时修复脱战逻辑: 失去目标改用 `EncourageDespawn` 渐隐离场, **不掉落**。

## 6. 视觉技术方案

| 项 | 方案 | 新建/复用 |
|---|---|---|
| 黑风全屏氛围 | `BlackBearDarkWind.fx` (ps_3_0): screenTarget 沿风向 UV 扭曲 + 噪声黑风带 + 暗角 + 金尘颗粒; `uIntensity/uGold/uWindDir` | **新建**, 由 Boss `PostDraw` 直接申请 `RequestFullscreenSlot` 名额并套用 (Xuanwu 同款; 强度经 windDraw/goldDraw 平滑) |
| 蜜潭地面 decal | `BlackBearHoneyPool.fx` (ps_3_0): 椭圆 SDF + 噪声扰动边缘 + 内部焦散流光 + 气泡 + 边缘亮圈, 琥珀金 | **新建**, 蜜潭弹幕经 `DrawScreenSpaceDecal` 绘制 |
| 黑风山天空 | `BlackBearSky` (CustomSky + SceneEffect, IACMLoader 注册): 压暗底色 + 高速横扫黑风云 (Smoke) + 少量金尘 + 暗角; 强度随阶段/演出 | **新建** (文件在本 Boss 文件夹内) |
| 冲撞预警线 | `ACMShaders.DrawBeam` 红线 | 复用 |
| 震地地纹圈 | `ACMShaders.ArenaRunic` decal | 复用 |
| 爆发泛光 | `ACMShaders.DrawRadialBloomAt` | 复用 (内部自带名额契约) |
| 地面冲击波 | 现有 874px 波纹贴图 (Proj1) + GroundShock dust 岩浪 | 复用升级 |
| 屏震 | `ACMScreenShakeSystem.Add` (max 不累加) | 复用 |
| 身体重量感 | NPC.rotation 前倾/后仰 (±0.12 rad) + scale squash/stretch + 帧号按 stateTimer 精确映射 (frame-by-progress) | 纯绘制 |

着色器全部以 `BlackBear` 前缀命名, 在本 Boss 代码内 `ModContent.Request<Effect>` 静态缓存 (Xuanwu 写法), 不注册 ACMShaders。不新增任何贴图/音频。

## 7. 性能与多人预算

- **多人安全**: FSM 迁移到 `npc.ai[0]=状态 / ai[1]=状态计时 / ai[2]=通用参数(冲撞方向·子段计数) / ai[3]=攻击冷却`; `furyTriggered/tempestUsed/introDone` 等标志走 `SendExtraAI/ReceiveExtraAI` (Xuanwu 同款); 所有状态切换 `netUpdate=true`; 弹幕仅服务器生成; `Main.LocalPlayer`/绘制字段只在客户端路径。
- **性能**: 着色器/噪声静态缓存一次; 蜜潭同屏 ≤6 (超限杀最旧), 蜜雨每波 ≤10, 风刃每段 ≤2; dust 全部节流 (`% 2~4`); Sky 粒子用固定数组复用 (Xuanwu 模式); 全屏后处理同帧 ≤1 (名额契约) 且 `MythologyConfig.FullscreenShadersEnabled` 关闭时静默降级; 热路径无 LINQ/每帧 new。
- **进度兼容**: 掉落表、`DownedBossSystem.downedBlackBear`、`SpawnChance` 自然生成、JiaSha 召唤条件全部保留; downed 标记改在 `OnKill` 设置 (Boss Checklist 兼容更稳)。

## 8. 实施清单

1. `BlackBear.cs` — 全面重写: ai[] FSM / shuffle-bag 选招 / 5+4 招编排 / 三大演出 / 重量感绘制 (rotation+squash+精确帧映射) / 脱战不掉落修复;
2. `BlackBear_Proj1.cs` — 地面波视觉弹清理 (保留类名与贴图用法);
3. `BlackBear_Proj2.cs` — 重做为蜜蜡抛物弹 (tileCollide、拖尾、落地小蜜潭);
4. `BlackBear_Proj3.cs` — 金冠光环升级 (traveled 入场、收拢变色预警);
5. `BlackBear_Proj4.cs` — 死代码改造为黑风爪痕风刃 (P2 连环冲使用);
6. `BlackBearGroundShock.cs` — 贴地波升级 (岩浪 dust + 光带渐灭);
7. `BlackBearHoneyDrip.cs` — 蜜雨升级 (落地生成蜜潭);
8. 新建 `BlackBearHoneyPool.cs` — 蜜潭场地弹幕 (0 伤害 + 迟缓 debuff, decal 着色器绘制);
9. 新建 `BlackBearSky.cs` — 黑风山天空 + SceneEffect (IACMLoader 注册, 演出节拍经 PublishStorm 推送);
10. 黑风全屏后处理并入 `BlackBear.PostDraw` (名额契约, 未单建 System — 少一个共享生命周期件);
11. 新建 `Effects/BlackBearDarkWind.fx` / `Effects/BlackBearHoneyPool.fx` 并按名编译 (退出码 0);
12. hjson (最后一步): 弹幕键改为语义名 + 新增 `BlackBearHoneyPool.DisplayName` (zh-Hans + en-US 同步, 已回读验证)。
