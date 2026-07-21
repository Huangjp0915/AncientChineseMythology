# TribulationCloud（劫云 / 雷劫）重做设计文档 — Boss 重做工程 V3

> 单元范围：`NPCs/Boss/TribulationCloud/`（TribulationCloudBase / Black / Purple / Red、TribulationLightningStrike、TribulationSweep、TribulationScreenSystem）
> 新增着色器：`Effects/TribulationBolt.fx`、`Effects/TribulationCloudDeck.fx`
> 性质判定：**事件型天灾 Boss**（渡劫生存仪式，非 DPS Boss）——云体完全免伤，撑过 N 记天雷 = 突破大境界，玩家死亡 = 小境界跌落。与修真系统深度联动（MythologySidebar 触发 → TribulationSpawnSystem 延迟生成 → TribulationWeather 雨幕/天空）。**重做力度：保留事件本质与全部修真结算语义，全面重做演出编排、落雷全链视觉与多人安全性。**

## 1. 现状诊断

以 choreography skill 七大本能 + 失败模式清单为透镜：

1. **无入场节拍（本能#4/#7 缺失）**：云体在玩家头顶 -800 直接 pop-in。侧边栏虽有 5 秒"酝酿"延迟与降雨，但云本身没有任何"聚拢成形"的过程；`Main.npcFrameCount=1`，全场只有 ±0.04 rad 的正弦摇摆——一张静态贴图悬浮 2 分钟。
2. **落雷无重量（本能#1/#2/#3 全反）**：预警 = ArenaRunic 法阵 + 少量 dust，落雷 = `DrawBeam` 一根笔直光带 + 震屏 5。没有先导电弧、没有"一拍死寂"、没有轰落瞬间的白闪与云体反冲、没有焦土余烬。雷劫最核心的"天威一击"读起来像一根激光。
3. **节奏是平坡而非波（PACING §1/§9 自检不过）**：三波仅仅是 interval 110/80/60 的变速，每一记雷的**形态完全相同**（黑档 12 记一模一样的点雷）。"Does each attack end in a different screen-state?" —— 不满足；玩家第 3 记之后就无事可期待。
4. **紫霄"移动安全区"名不副实**：缝隙 Y 由 `identity%3` 定死，从不移动——玩家站好缝位后整段横扫零操作。且三档威胁递进（黑→赤→紫）只体现在文案里，视觉上无差异。
5. **结算演出寒酸**：成功 = 60 帧金柱；失败 = 一行文字 + 音效；两种情况云体都是 `NPC.active=false` 瞬间消失。修真模组最重要的"突破时刻"没有仪式感。
6. **多人安全隐患（技术底线 §5 违规）**：`TotalStrikes` 在 `SetDefaults` 各端各自 roll；`attackTimer/strikesDone/lastWave` 全是私有字段（不经 `npc.ai[]` 同步，客户端预测会漂移）；**弹幕生成没有 `Main.netMode != MultiplayerClient` 判定**（客户端 AI 也会执行 `NewProjectile`，多人下重复生成）。
7. 小项：血条对免伤 Boss 无意义（满血 200 万看着像 bug）；`TribulationScreenSystem` 染屏强度恒定爬升，与落雷节拍无联动。

## 2. 设计主题与幻想感

**"天，是一位审判者。"** 渡劫不是打 Boss，而是被天空审视。玩家的体验目标：

- **压迫**：头顶是一整片翻滚的、内部有电光游走的活云盖（不是一朵贴图云），全屏随考验进度越压越暗。
- **仪式**：每一记天雷严格遵循雷的"呼吸"——**云内充能（电光游走渐密）→ 先导垂落（细弱的阶梯状电弧探向落点）→ 一拍死寂（万籁俱寂，光全部熄灭）→ 轰落（超粗折线主雷柱 + 分叉 + 全屏白闪一帧 + 大震屏 + 云体反冲上弹）→ 焦土余烬（电离残光 + 升烟）**。这是 MOTION.md §6 充能语法的极致版，也是真实雷电物理（stepped leader → return stroke）的戏剧化。
- **递进**：黑（玄雷，教拍子）→ 赤（赤雷，佯攻博弈）→ 紫（紫霄，移动安全区），三档威胁清晰递进；每档内部再按三波换形态，终雷永远是本场最重的一击。
- **审判落幕**：成功 = 云层从中裂开、金色天光灌顶（雷劫变洗礼）；失败 = 雷声渐远、乌云冷酷散去（天不为死者停留）。

## 3. 阶段结构与"血量"断点

事件型 Boss 无血量阶段，改用**落雷进度**驱动；血条被征用为"剩余考验"进度条（`NPC.life = lifeMax × 剩余比例`，下限夹 1 防止意外死亡触发 OnKill 二次结算）。

状态机走 `npc.ai[]`（多人安全）：`ai[0]`=状态，`ai[1]`=计时器，`ai[2]`=已落雷数，`ai[3]`=总雷数（服务器首帧 roll 后 netUpdate 同步）。

| 状态 | 时长 | 内容 |
|---|---|---|
| 0 聚云 Gather | 90f | 云体从稀薄聚拢成形（scale 0.25→1，cubed），分子云向心汇聚，染屏 0→0.35 |
| 1 天宣 Decree | 150f | 两轮云内电光游走 + 两声云内横闪（白闪 0.2/0.3 + 远雷 + 震屏 4/6），公告文字；末段 50f 刻意死寂 |
| 2 天罚 Trial | 直到雷数满 | 三波递进落雷（见 §4），波间 55f 喘息，云体蓄力视觉与弹幕联动 |
| 3 审判 Judgement | ~220f | 成功收尾：染屏转金 → 云盖裂开 → 金光灌顶 → 云消散 |
| 4 息怒 Abort | 40f | 失败/目标失效收尾：雷声渐远，云快速消散（不拖死人时间） |

三波断点（按已落雷数 / 总数）：`<1/3` 试探，`<2/3` 紧逼，其余为终局波；最后一记 = 终雷（独立超长演出）。

## 4. 招式编排表

所有落雷共用"**充能→先导→死寂→轰落→余烬**"全链（帧数为单记点雷，60fps）：

| 段 | 帧数 | 预警/表现 | 公平阀门 |
|---|---|---|---|
| 充能 Mark | 45（终雷 90） | 落点红色法阵渐强（TelegraphColors.Lethal，只在真落点冒红）+ 电尘向心汇聚 + 云内 uFlash 脉冲频率随充能上升 | 预警 ≥45f 且落点自锁定后不再追踪 |
| 先导 Leader | 12 | 一条**细弱暗紫折线电弧**从云底快速探向落点（每 3f 换 seed 闪烁），无伤害 | 精确指明落点的"最后通牒" |
| 死寂 Silence | 9 | 法阵骤缩 40%、全部粒子停发、云内光熄灭、无任何声音 | "吸气"= 玩家最后确认窗口；死寂期落点已定格 |
| 轰落 Strike | 1（余辉 14） | TribulationBolt 主雷柱（白热芯 + 主题色辉光 + 2 条分叉）+ 全屏白闪 1-2f + 震屏 10（终雷 16）+ Thunder 近雷 + 云体反冲上弹 + RadialBloom 落点泛光 | 伤害窗口=轰落后 5f，与视觉严格对齐 |
| 余烬 Ember | 40 | 雷柱余辉散开变淡、落点电离残光噼啪、升烟 | 纯演出，无伤害 |

三档形态递进（调度层生成参数，弹幕保持单一职责）：

| 档 | 波1 试探 | 波2 紧逼 | 波3 终局 | 终雷 |
|---|---|---|---|---|
| 玄雷（黑，12 记，教拍子） | 单点雷，间隔 105f | **双联雷**：第二记延迟 26f 后在玩家新站位起充、各自完整预警，间隔 150f | 单点雷加密，间隔 68f | 云内预闪四连加速 → 90f 充能 → 最重一击 |
| 赤雷（红，6-9 记，佯攻） | 假蓄力 28f（非红主题色烟）诱走 → 真雷 32f 追新站位 | 同左，间隔提速 | **双重佯攻**：假 28f→假 28f→真 32f（骗两次） | 无假段——"最后一记天不骗你"，红色 90f 充能 |
| 紫霄（紫，4-7 幕，移动安全区） | 单幕慢扫（预告 55f + 横扫 95f），缝静止 | 缝 **smoothstep 漂移** 150px（最大坡度 ~2.4px/f 可跟随，预告期翠玉幽线标出漂移路径） | **双幕对扫**：两幕从两侧向中心合拢（110f），缝同 Y 联动漂移，相遇即炸散 | 双幕对扫 + 漂移 + 最重预告 80f + 前奏预闪四连 |

预警诚实性契约：红色只出现在**真会造成伤害**的蓄力/落点/雷幕（TelegraphColors.Lethal）；赤雷假段用非红主题色；紫缝隙全程翠玉安全色（TelegraphColors.Safe）高亮。

## 5. 入场 / 换阶段 / 死亡三大演出脚本

- **入场（聚云 90f + 天宣 150f）**：侧边栏触发后天先落雨（既有 TribulationWeather）；云体以 0.25 尺度稀薄出现，分子云粒子从四周向心汇聚（MOTION §6 converging streaks），90f 内 cubed 曲线聚成完整云盖。随后"天宣"：云内电光两轮游走渐密（ElectricArcSheet 随机段 additive 闪现），第 50f / 100f 两声**云内横闪**（不落地，全屏白闪 0.2/0.3 + 远雷低吼 + 震屏），公告"天威临世"；最后 50f 一切光声渐熄——死寂即宣告，第一记雷随后轰落。
- **换阶段（波次换挡）**：每波第一记雷前 55f 喘息 + 云色/染屏强度上探一档 + 一声更低沉的雷鸣；终雷有独立前奏——云内预闪三连加速（间隔 30→18→10f，PACING §6 加速节拍），随后超长充能。
- **死亡（结算，两分支）**：
  - **成功**：最后一记雷余烬未散，染屏由风暴色 60f 内转金 → 云盖着色器 `uBreak` 从中心裂开（50f）→ **金光柱从裂口灌落玩家**（增强版 SuccessFinale：金色"天光"复用雷柱管线但反向语义，120f）+ 金色 RadialBloom + 缓和震屏 6 → 云体 `uDissolve` 消散离场。天雷变天光，是整场唯一一次金色。
  - **失败**：玩家倒地瞬间一声闷雷渐远（pitch 下探），云 40f 内快速消散、染屏立退——死亡后不拖时间（PACING §8）。

## 6. 视觉技术方案

| 部件 | 技术 | 新建/复用 |
|---|---|---|
| 云盖本体 | **TribulationCloudDeck.fx**（ps_3_0）：fbm 域扭曲云形 + 底缘撕裂 + `uFlash/uFlashX` 云内电光散射 + `uBreak` 裂开 + `uDissolve` 消散；世界空间宽幅 quad（~1760×480px），三档主题色进云色 | 新建 |
| 主雷柱/先导 | **TribulationBolt.fx**（ps_3_0）：程序化折线闪电（分段 hash 折线主干 + 二级细折线 + 2 条分叉），`uSeed` 每记异形、`uLife` 余辉衰减（芯收窄、辉光散开）；先导=细弱暗紫参数版 | 新建 |
| 云内电光游走 | ACMAsset.ElectricArcSheet 随机段 + LightningBranch，additive 闪现，频率 ∝ 充能 | 复用 |
| 落点法阵 | ACMShaders.ArenaRunic（既有 DrawRunic 封装保留） | 复用 |
| 落点泛光 | ACMShaders.DrawRadialBloomAt（内部走全屏名额契约） | 复用 |
| 雷幕（紫） | ACMShaders.DrawBeam 做幕底 + TribulationBolt 竖直细弧×2 叠加游走 | 复用+新建 |
| 全屏白闪 | TribulationScreenSystem 新增 Flash 通道：PostDrawTiles 画白 quad，×0.72/f 指数衰减，强度上限 0.6（光敏保护），不读 screenTarget、不占全屏名额 | 增强 |
| 风暴压暗 | TribulationScreenSystem 既有 ElementalScreenTint 染屏，强度随进度 0.32→0.6，结算期转金 | 增强 |
| 云体反冲 | 轰落帧云绘制偏移 +recoil（×0.85/f 衰减）——雷的后坐力（本能#3） | 新建（纯绘制字段） |

着色器均以 Tribulation 前缀命名、ps_3_0 编译、在弹幕/NPC 自己代码内 `ModContent.Request<Effect>` 静态缓存（Xuanwu 写法），**不注册进 ACMShaders**。

## 7. 性能与多人预算

- **绘制**：云盖每帧 1 次着色器 quad（PreDraw 内 End/Begin 各一次）；电弧 sheet ≤3 张 sprite/帧；主雷柱同屏 ≤3 根 quad；白闪/染屏各 1 个全屏 quad（无 RT、不读 screenTarget）；RadialBloom 走名额契约每帧 ≤1。无每帧 new 纹理/Effect（全部静态缓存）。
- **粒子**：聚拢期 ≤6/帧，蓄力 ≤4/帧/落点，轰落瞬间一次性 ≤30，余烬 ≤2/帧。
- **多人**：`TotalStrikes` 服务器 roll 进 `ai[3]`；状态切换 `netUpdate=true`；**弹幕仅服务器生成**；赤雷锁位以弹幕 `Projectile.Center` 为落点权威并 netUpdate；落点伤害保留"各客户端判定本地玩家"模式（`Main.LocalPlayer` 只出现在客户端路径）；所有绘制/白闪/震屏均 `Main.dedServ` 守卫。目标失效时进 Abort 态保底出口（状态机无死路）。

## 8. 实施清单

1. `Effects/TribulationBolt.fx`、`Effects/TribulationCloudDeck.fx` 新建 + `CompileFX.ps1 TribulationBolt TribulationCloudDeck` 编译过。
2. `TribulationCloudBase.cs`：ai[] 状态机（聚云/天宣/天罚/审判/息怒）、三波调度与三档形态参数、服务器权威生成、进度条血量、云盖绘制（Deck 着色器 + 贴图核心层 + 电弧游走层 + 反冲）、修真结算钩子原样保留。
3. `TribulationLightningStrike.cs`：充能→先导→死寂→轰落→余烬全链重写；Feint/DoubleFeint/Final/SuccessFinale 参数化；成功金光升级为"天光灌顶"。
4. `TribulationSweep.cs`：缝漂移、双幕对扫、幕体电弧化、预警配色契约保持。
5. `TribulationScreenSystem.cs`：新增 Flash 白闪通道与结算金色染屏支持。
6. 三色子类：三档参数（雷数/波形态/主题色）+ 三色 DisplayName。
7. ReadLints 全部改动文件清零；最后一步双语 hjson 键区小步更新（三色云名）并回读验证。
