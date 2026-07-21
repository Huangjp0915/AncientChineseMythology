# Dazheng（大峥/大椿·季节之神）对抗式审查报告

> 审查代理：Boss 重做工程 V3 · 对抗式审查
> 审查对象：`Celestias/Boss/Dazhengs/` 全部代码 + `Effects/DazhengArenaCircle.fx(.fxc)` + `Effects/DazhengLifeburst.fx(.fxc)` + 两份 hjson 键区
> 对照文档：`Docs/BossRedo/Dazheng.md`；方法论：boss-fight-choreography SKILL/MOTION/PACING + `_REVIEW_BRIEF.md` 猎杀清单
> 纪律：全程只读（除本报告），未跑 build / CompileFX；着色器参数以 .fx 源 + .fxc 二进制字符串比对验证。

## 一、审查范围与方法

逐行阅读以下文件（共 14 个 .cs + 2 个 .fx + 共享件抽查）：

- `Dazheng.cs`（1813 行，主状态机/四季/换阶段/死亡演出/绘制）
- `DazhengRootSpear.cs`、`DazhengHealThread.cs`、`DazhengGoldenPhantom.cs`、`DazhengLeaf.cs`、`DazhengVine.cs`
- `DazhengDecoyTree.cs`、`DazhengSeasonAnchor.cs`、`DazhengRootField.cs`、`DazhengArenaBarrier.cs`
- `DazhengSeasonScreenSystem.cs`（含 `DazhengSeasons` 色板）、`DazhengSky.cs`
- `Items/TheNaturalAxe.cs`、`Items/ArrogantDivineSylvan.cs`
- `Effects/DazhengArenaCircle.fx`、`Effects/DazhengLifeburst.fx`；并以二进制字符串探针确认 `.fxc` 内含 `uCrack/uFlash/uColorCore/uThickness/uProgress`
- 共享件抽查：`ACMShaders.cs`（DrawBeam / WorldDecalParams / RequestFullscreenSlot / ApplyScreenPostProcess / RestoreDefaultBatch）、`TelegraphColors.cs`、`ACMScreenShakeSystem`（确认震屏为取 max 不叠加）、`PaletteLUT/ArenaRunic/ElementalScreenTint` 的 uniform 名比对
- hjson：zh-Hans / en-US 中 `NPCs.Dazheng.*`（季语×4、SeasonsDisorder、ForestRequiem）与 `Projectiles.Dazheng*` 键区
- 产物核查：`Effects/DazhengArenaCircle.xnb` 已确认从磁盘删除（git 状态 `D`），`.fxc`（04:29）新于 `.fx`（03:31/03:32），无同名资产冲突

单元特定热点逐项核查结果见 §五。

## 二、P0（崩溃/软锁/desync/不可躲致死）

### P0-1 根须场安全岛用 `Main.GameUpdateCount` 推导位置——多人下服务器与客户端相位错开，玩家站在"画出来的安全岛"上仍被持续判死

- 位置：`DazhengRootField.cs:56-62`（`IslandPos` 以 `Main.GameUpdateCount` 计算轨道角）；伤害判定 `DazhengRootField.cs:125-140`（服务器权威 `ApplyRootDamage` → `IsSafe`）；绘制 `DazhengRootField.cs:181-188`（客户端用自己的 `GameUpdateCount` 画岛）。
- 现象：`Main.GameUpdateCount` 是每端各自从进程启动累加的本地计数器，**没有任何网络同步**。服务器已运行的帧数与客户端本地帧数相差任意值，三座安全岛以 0.0035 rad/f 旋转，两端相位差可达任意角度。客户端把岛画在 A 处，服务器按自己的 B 处判定——玩家站在可见的安全岛内仍每 26t 吃 60/90/120 点无预警伤害（冬季 + 春 P2 常态开启），且完全无法通过操作回避。
- 为什么是问题：审查简报 P0 定义「desync / 不可躲致死」双双命中；且 `DazhengRootField.cs:15` 的注释声称"由 Main.GameUpdateCount 确定性推导 (server/client 一致, 无需额外同步)"——**声明为假**。单机下两者同源所以看不出来，一进多人就露馅。
- 建议修法：改用同步量作时间基（如投射物自身 `ai` 计时每帧自增并靠 spawn 同步、或 `Main.time`），或由服务器把轨道相位写进 `Projectile.ai[]`/`localAI` 并 netUpdate。

## 三、P1（不公平设计/明显表现错误/声明与实现不符/性能黑洞）

### P1-1 根须场致命判定无外半径——可见根须纹只覆盖半径 ~556px，伤害却覆盖全场（预警形状 ≠ 伤害形状）

- 位置：判定 `DazhengRootField.cs:125-139`（遍历所有玩家，仅豁免安全岛，**没有任何距离上限**）；视觉 `DazhengRootField.cs:155`（ArenaRunic 贴花半径 = OrbitRadius+IslandRadius×0.6 ≈ 556px）与 `:111-121`（警示尘最远 ~640px）。
- 现象：冬季/春 P2 期间，站在竞技场边缘（结界半径 1200~1500px）或高空——脚下与身边**没有任何根须视觉**——照样每 26t 吃 60~120 伤害，死亡文案"被大椿的根须缠绕吞噬"。视觉上安全的区域实际致命。
- 为什么是问题：猎杀清单 A「不诚实：预警形状≠伤害形状、判定盒远大于可见视觉」。
- 建议修法：`ApplyRootDamage` 里加 `dist < OrbitRadius + IslandRadius * 0.6f` 的外半径上限，或把贴花/尘埃扩到与判定一致的范围。

### P1-2 `Dazheng.DrawBehind` 把 NPC 索引塞进**弹幕**绘制缓存——入场/归土期间会把同槽位的无关弹幕重复画一份

- 位置：`Dazheng.cs:312-317`（`Main.instance.DrawCacheProjsBehindNPCsAndTiles.Add(index)`，`index` 是 NPC whoAmI）。
- 现象：`DrawCacheProjsBehindNPCsAndTiles` 是弹幕缓存，绘制阶段按 `Main.projectile[index]` 取用。入场升起 210t + 归土下沉 210t 期间每帧执行：若弹幕槽 N（N=Boss whoAmI，通常是低槽位，战斗中大概率被玩家弹幕占用）恰好活跃，该弹幕会在"贴图后层"被**额外重复绘制一次**（空中区域无瓦片遮挡，肉眼可见双影）。而 Boss 自身藏入地形实际由 `NPC.behindTiles`（`Dazheng.cs:412`）完成，此 override 纯属错误 API 用法且冗余。
- 为什么是问题：猎杀清单 B「双重绘制（两套系统画同一实体）」变体 + 绘制层 API 误用；同样写法在 `Dryades/Dryads.cs:365` 也出现（属其他单元，仅提示模式性风险）。
- 建议修法：直接删除该 override（behindTiles 已覆盖需求），或改用 NPC 专用缓存（如 `DrawCacheNPCsBehindNonSolidTiles`）。

### P1-3 多个核心反馈节拍嵌在服务器权威分支里——多人下"最帅的主动反制"全部哑火，冬季饱食爆发甚至会在客户端"假爆"

- 位置与逐点现象（联机 = 专用服务器 + 客户端时）：
  - `Dazheng.cs:1306-1319`：斩线判定与 `healBroken` 仅存在于服务器（实例字段**不同步**），斩线成功的音效/震屏（`:1315-1318`，嵌套在 `!= MultiplayerClient` 分支内）与踉跄冲量只有单机能触发；客户端只能看到丝线回抽动画（靠 ai[2] 同步，这条是通的）。
  - `Dazheng.cs:1326-1357`：客户端本地 `healBroken` 恒为 false → **即使玩家已挣断丝线**，客户端仍完整播放 45t 冰尘收束预警 + healEnd 帧的爆发音效/震屏 9/冰白 Lifeburst 环（`:1352-1356`），而服务器根本没生成那 12 根冰藤——"挣断即可完全免除本招"的承诺在画面上被反向违背（假预警+假爆发，狼来了会教坏玩家）。
  - `Dazheng.cs:1214-1226`：秋季诱饵解谜成功的大破绽演出（金环/音效/震屏 10/踉跄）在 `killedRecently` 分支内，而 `DecoyKilled` 静态旗只在服务器 OnKill 时置位 → 多人下**所有端**都看不到成功节拍，只剩破绽窗口上升沿的一声 Item4（`:527-530`）。
  - `DazhengDecoyTree.cs:100-107`：击杀消散金爆（`SpawnDissipate(true)`）在 OnKill 内且 `!= Server` 守卫 → 多人下诱饵被打爆时**无声无光地消失**（对比：超时消散路径 `:72-78` 走客户端 AI 模拟，反而有演出）。
  - `Dazheng.cs:877-887`：门控通过的怒吼/震屏 12/seasonFlash 全在 `!= MultiplayerClient` 块内 → 多人无感；锚点强制切季（`:641-658` → `AdvanceSeason` 仅服务器调用）同理丢失 `:597-599` 的切季音效/震屏（自然轮转切季由客户端模拟 AI 补回，不受影响）。
- 为什么是问题：猎杀清单 B「多人（代码可判部分）」+ 设计文档 §1.9 声称已修"破绽窗口音效走服务器权威路径，多人端听不到"——同一类错误在本轮新增代码里又复制了五处；其中"假饱食爆发"属于主动误导玩家的表现错误。
- 建议修法：把 `healBroken` 写进同步量（如 `SubState`/新 ai 槽或 SendExtraAI），客户端据此闸断假预警；演出类反馈改为客户端对同步状态的上升沿检测（项目内 `defenseDownTimer` 的处理就是正确范本）；诱饵消散改在 `HitEffect`（life<=0 分支）或客户端对 NPC 消失的检测里播放。

## 四、P2（打磨项）

1. **门控期声明的"根刺轻压力"未实现** — `Docs/BossRedo/Dazheng.md` §3 表格声明 Gate 阶段"补根刺轻压力"；`Dazheng.cs:848-888`（RunGate）只有每 50t 一根藤蔓（V2 原样），根刺在门控期从未出现。声明 vs 实现不符（轻度）。
2. **锚点击毁奖励存在两处被静默吞掉的时机** — ① 秋季诱饵窗口内打碎"秋"锚点：`ConsumeAnchorBreak`（`Dazheng.cs:641-658`）因 `forced == season` 不切季，只开 180t 破绽，但此时 `NPC.dontTakeDamage=true`（`:1153`），奖励等于零且玩家无从知晓；② 换阶段演出 160t 期间打碎锚点：`RunSeasonCombat` 不执行，事件 6 帧过期（`:650`），既不切季也无破绽。建议：①击毁当季锚点时给替代奖励或提前结束诱饵窗口；②转换结束后补消费或转换期锚点无敌。
3. **换阶段"清弹"公平阀不含环境杀** — `RunPhaseTransition2`（`Dazheng.cs:1405-1415`）清了全部 hostile 弹幕，但根须场 damage=0 不在清单内且 `UpdateRootField` 只在 `AdvanceSeason` 调用：冬/春 P2 进入 160t 无敌演出期间，全场致命根须照常判定（`DazhengRootField.cs:88-94`）。演出期玩家仍在被环境秒磨，与"清弹→呼吸拍"的声明相悖。建议转换起手把根须场 `ai[1]` 归零（自带 0.04 lerp 淡出即是公平阀）。
4. **结界裂纹在转换开始即渐入，而非声明的"82t 爆发帧 0→0.35 跳变"** — `Dazheng.BarrierCrack`（`Dazheng.cs:105-118`）在进入 PhaseTransition_2（didPhase2Transition=true）后立即返回 ≥0.35，屏障以 0.045 lerp 跟随（`DazhengArenaBarrier.cs:136`），至 82t 爆发帧裂纹已基本浮现，uFlash 变成"事后闪"。轻微的声明 vs 实现错拍，观感尚可。建议在属性里加 `PhaseTimer >= Transition2BurstTick` 门控。
5. **NPC.damage 的运行时修改不入网络同步** — P2 接触伤害 ×1.25（`Dazheng.cs:1496`）与死亡期 damage=0（`:1524`）依赖各客户端本地 AI 复算：中途加入的玩家永远拿不到 ×1.25；本地 PhaseTimer 与服务器不严格对齐时存在单帧漏乘/重复乘的可能（`didPhase2Transition` 防重入只保护相位，不保护该行本身）。接触伤害语义下影响有限。建议把倍率并入 SendExtraAI 或改为按 `didPhase2Transition` 推导的只读属性。
6. **斩线判定目标与丝线视觉目标可能漂移** — 挣断检测盯 `Main.player[NPC.target]`（`Dazheng.cs:1303-1308`，每帧 TargetClosest 可换人），丝线视觉锚定生成时的 `ai[1]`（`DazhengHealThread.cs:27-28`）。多人下可能出现"B 冲刺挣断了拴在 A 身上的线"。建议判定改读丝线的 `TargetIndex`。
7. **DazhengVine 绘制热路径批次churn** — 每根藤蔓每帧 3~4 对 `End/Begin`（`DazhengVine.cs:82-135`）+ 每帧 2 次 `List<ColoredVertex>` 分配与 `ToArray`（`:140-181`）；春季迷宫墙持久化后同屏可达 15~30 根藤蔓 → 每帧 ~60-120 次批翻转 + 堆分配。V2 遗留、非本轮引入，桌面端能扛但属于清单 B「逐节 Begin/End / 热路径每帧 new List」。建议顶点带合并成单批或缓存数组。
8. **`DazhengHealThread` 斩线时目标玩家已退出的兜底** — `DazhengHealThread.cs:61-64` 若 `TargetIndex` 玩家已离线，`snapAnchor` 取到失效坐标（可能是 (0,0)），回抽鞭甩会从世界原点抽回。极低概率，加一个 `t.active` 兜底即可。

## 五、单元特定热点核查结论（无问题项，留档）

| 热点 | 结论 |
|---|---|
| `.xnb`→`.fxc` 切换 | **干净**。磁盘上 `DazhengArenaCircle.xnb` 已删（git `D`），`.fxc`（04:29）新于 `.fx`（03:32）；`.fxc` 二进制内含 `uCrack/uFlash` 字符串，确认由新源编译。`.fx` 源中两参数默认 0 时经 `if (uCrack > 0.001)` / `if (uFlash > 0.001)` 完全短路——"默认 0 向后兼容"声明属实。`DazhengLifeburst.fxc` 同验（uColorCore/uThickness/uProgress/LifeburstPass 均在）。C# 侧 9 个 SetValue 参数名与 .fx 声明一一对上。 |
| DeathCinematic 状态机接缝 | **成立**。`CheckDead`（`Dazheng.cs:354-386`）首次拦截：血锁 1 + dontTakeDamage + 清弹/杀随从/杀锚点 → 进入演出；`Phase == DeathCinematic` 再入直接放行真实死亡，无反复触发；演出不依赖目标存活（AI 提前 return，全灭不卡演出）；300t 服务器 `life=0; checkDead()` 正常走掉落与 downed 旗标（`DownedBossSystem.downedDazheng` 存/读齐全）。 |
| 夏·金叶风暴三环共用安全扇区 | **同源成立**。扇区角由服务器掷入 `SubState`（ai[3]，`Dazheng.cs:1082-1085`）并 netUpdate；三环 30 叶的排除判定（`:1106-1123`）与 Safe 光线标线（`DrawNovaSafeSector`，`:1955-1976`）读同一个 `SubState`。环间 0.07 rad 相位差仍在 ±24° 扇区容差内。收束尘 87% 骤停（`:1092`）+ 静默到 68t 释放，静默实际 ~16t（声明 8t，方向更保守，公平）。 |
| 冬·饱食爆发免除机制（服务器逻辑） | **判定正确**（表现层缺陷见 P1-3）。`healBroken` 置位即跳过 healEnd 爆发（`:1339`），预警尘也被 `!healBroken` 闸断（服务器/单机视角）；斩线回抽（`DazhengHealThread.cs:60-79, 102-118`）`hostile=false` 全程无判定，纯视觉——"回抽鞭甩不带判定"属实。 |
| 金身幻影公平阀 | **属实**。转向率封顶 0.045 rad/f（`DazhengGoldenPhantom.cs:22, 69-70`）；速度 6→13 渐升（`:64-65`）；`timeLeft < 120` 熄火：`hostile=false` + 拉直减速 + 暗琥珀冷却淡出（`:55-57, 74-77, 113-115`）——伤害窗口与视觉对齐。 |
| 结界裂纹/死亡停用 | **属实**。裂纹读 `dz.BarrierCrack` 授权值（`DazhengArenaBarrier.cs:136`）；死亡期界外伤害 `!bossDying` 停用（`:162`）、推力 `!bossDying` 停用（`:171-173`）；70t 碎裂（uFlash + 尘环 + alpha 塌缩自灭，`:146-157, 189-201`）。 |
| 春·根刺伤害窗口 | **诚实**。42t 竖直 Lethal 红柱预警（全高，与判定形状一致）→ 破土+持留 36t 内 `hostile` 且线段碰撞随当前高度收缩（`DazhengRootSpear.cs:71, 120-129`）→ 回缩 24t 无判定（可见但无害，公平方向）。位置预警起始帧锁定，不追踪；同屏 ≤6 硬上限（`Dazheng.cs:970-981`）。震屏系统取 max 不叠加，5 根齐出不会炸镜头。 |
| hjson 键区 | **齐全**。zh/en 双份 `NPCs.Dazheng.{DisplayName, SeasonSpring/Summer/Autumn/Winter, SeasonsDisorder, ForestRequiem}`、`DazhengRootSpear.DisplayName` 及全部弹幕/随从键均在正确区块内，无指向已删类的死键。 |
| ReadLints | Dazhengs 目录无诊断（本审查环境下非失明佐证之一，仍以人工读码为准）。 |

编排学层面（SKILL 七本能）：入场/换阶段/死亡三大节拍齐备且死亡演出"寂→碎→归土→静→生"的波形、终爆唯一 shake 17 的层级纪律、季间连接拍 + 风速阀、树身阻尼弹簧摇曳/呼吸/受创冲量——均为真实实现且质量高，无"等自己计时器"的死区（各季压力源连续，连接拍为刻意呼吸）。

## 六、结论：可玩性判定

- **单机：可发布级**。四季框架 + 三大演出完整，公平阀真实存在，热点声明基本兑现；P1-1（隐形致命区）建议尽快修但知晓规则（"站岛上"）后可正常游玩。
- **多人：需修 P0-1 后才可玩**。安全岛判定与画面错位属于不可回避的持续伤害 desync，冬季/春 P2 会直接把联机局打崩；P1-3 一并修复后多人体验才与单机对齐。
- 建议返工范围很小：P0-1 + P1-1 集中在 `DazhengRootField.cs`（~30 行内可修）；P1-2 删 5 行；P1-3 是同一模式的 5 处小改。无需推倒任何系统。
