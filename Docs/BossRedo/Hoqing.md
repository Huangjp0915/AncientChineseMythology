# 后卿 Hoqing —— Boss 重做设计文档（V3）

> 单元：`NPCs/Boss/Hoqings/`（Hoqing.cs / HoqingProjectiles.cs / HoqingScreenSystem.cs / HoqingSky.cs）
> 题材：四大尸祖之一「后卿」。黄帝麾下战死之将，怨魄不散化为僵尸之祖，司瘟疫与尸火，
> 引领「万鬼夜行」。本地化印证：Boss 名"后卿"、掉落"劫骇"（唤鬼火之器）、弹幕"鬼火/尸坑/脓潭/尸链"、
> Debuff"衰朽"，BGM 为地府主题。

## 1. 现状诊断

以 choreography skill 七大本能 + 失败模式清单为透镜：

1. **死亡演出完全缺失**（三大节拍最重伤）：`life<=0` 直接抛 4 块 gore。没有任何死亡编排。
2. **入场演出太弱**：Intro 只是 lerp 到玩家头顶 + 一声吼 + 一次震屏；无"从阴间显形"的仪式感，
   无静止凝视节拍（menace is stillness）。
3. **动作全程 lerp、无速度对比**（本能 #1/#2）：幕一游走 lerp(0.05)，冲撞无反向蓄势、收招是
   `*0.97` 软刹（应硬刹 ×0.65~0.7）；幕二 Boss 全程悬浮成"炮塔"，本体零威胁；幕三到祭坛用
   lerp(0.25) 滑过去——没有一次"launch is a set"的爆发。
4. **幕三蓄力死站 120 帧**：`velocity = Zero` 干等自己的计时器（PACING §9 自查直接命中）；
   收束粒子全程匀速——没有 72% 静默（charge grammar）。
5. **疫风间隙预告撒谎**（可读性 bug）：预告阶段 `gapX` 算完没用，画的是一排均匀 dust；
   缺口在释放帧才随机决定。玩家死了没法叙述死因。
6. **脓雨潭预告与释放坐标脱钩**：预告画在预告帧的玩家位置，释放取释放帧的玩家位置。
7. **帧动画越界**：`targetFrame = 4` 但合法帧 0~3，`FindFrame` 里 frame 能爬到 4，
   `GetRectangle(main, 4, 4)` 读出界。
8. **HoqingSky 名存实亡**：`HanbaSkySun/HanbaSkyColorBar` 两字段从未赋值（编译警告 CS0649），
   冥月与雾层从不显示，天空只剩一层纯色。
9. **已有的 V2 资产是好底子**：ScreenSystem（ArenaRunic 祭坛 ×4 + BeamGrad 经络 + RadialBloom
   蓄力辉光）、GenericWarp·fog 限视、GhostFire 的 DissolveBurn 现形/崩解——保留并扩展。

**重做力度判定**：表现层下半身（ScreenSystem/弹幕溶解）已达 V2，但 AI 编排、三大演出节拍、
天空是 V1 水平 → AI 编排全面重写 + 入场/死亡演出新建 + 天空程序化重做 + 3 个新专属着色器。

## 2. 设计主题与幻想感

**「万鬼夜行的送葬仪式，玩家是被送的那一个。」**

后卿不是猛兽，是仪式的主持者：仪仗（幽火列队）→ 开道（冲撞）→ 施疫（弹幕图案）→
升坛（四祭坛）→ 开启**鬼门**放万鬼过境。死亡时鬼门反噬，把仪式之主自己收走——
「引渡万鬼者，终为鬼所引」。

三个视觉记忆点：
- **鬼门**：病绿/幽紫的竖椭圆裂隙（新着色器），幕三大招与死亡演出的核心意象；
- **冥月**：天空程序化病绿冥月，幕三"睁眼"渗血环（血月标量）；
- **魂焰**：全弹幕统一鬼绿（持续危害）/ 腐橙（致命爆发）双色语言 + 程序化火焰柱。

## 3. 阶段结构与血量断点

| 阶段 | 血量 | 规则 |
|---|---|---|
| 幕零·阴门显形 | 入场 | i 帧演出 ~150f：鬼火收束 → DissolveBurn 显形 → 静止凝视 → 怒吼开战 |
| 幕一·幽火仪仗 | 100%→70% | 手写循环表：仪仗布坑 → 三连折线冲 → 仪仗布坑 → 幽火齐射 |
| 过渡 | 70% | i 帧 ~110f，清弹，冥月月晕扩大 |
| 幕二·疫疠扩散 | 70%→30% | 轮替枢纽 + 4 招随机不重复（脓雨/尸链/疫风走廊/魂灯环阵），每 2 招插 1 次鬼影横掠连接器 |
| 过渡 | 30% | i 帧 ~130f，清弹，arenaCenter 锁定、四祭坛依次点亮、冥月渗血 |
| 幕三·万鬼夜行 | 30%→0 | 祭坛洗牌循环 ×4（瞬掠→绕坛蓄力→释放）→ 大招「鬼门开」→ 再循环；≤15% 大招强化 |
| 死亡·鬼门收葬 | CheckDead 拦截 | ~250f 脚本：抽搐外泄 → 鬼门撕开拖拽 → 15f 全静默 → 白闪爆发 gore → 门合拢真死 |

## 4. 招式编排表

时长均为 60fps 帧。伤害系数基于 `NPC.damage`(60)。

### 幕一

**A1 仪仗布坑 Procession（~200f，压力：低，节奏：呼吸）**
- 侧上方 spring-damp 悬停游走（k=2,c=5，有惯性非 lerp）；每 70f 于玩家附近播 1 个尸坑
  （CorpsePit 自预警 75f 尸绿呼吸圈，GhostGreen）。仆从（仪仗队 6 只：枪兵×3/爆兵×2/疫医×1）
  按各自节奏施压。
- 公平阀门：尸坑预警 75f，激活期 150f，伤害窗口 = 激活期。

**A2 三连折线冲 TripleCharge（3×~70f，压力：高，节奏：爆发）**
- 每轮：蓄 42f（先移动至玩家侧向 480px；末 12f `pow(t,6)` 反向抽离 90px——late-snap 反蓄；
  BeamGrad 冲刺线预告 Lethal 红，36f 前置警示音）→ 冲 1f 设定 `v = dir*46`（launch is a set，
  shake 7 + 残影）→ 冲行 12f 零转向 → 刹 ×0.68/f 硬刹（slam into position）→ 18f 重锁窗口。
- 公平阀门：接触伤害仅当 `|v|>22`；冲刺距离栓绳（超过 1300px 提前进入刹车）；
  三轮后必回 A1（呼吸拍）。

**A3 幽火齐射 LanternVolley（~170f，压力：中，节奏：仪式）**
- Boss 停驻抬手（蓄力帧），40f 粒子收束（密度 ∝ sqrt(t)，72% 处硬切静默）；
  50f 起：枪兵每只间隔 8f 各射 3 发制导幽火、爆兵抛湮灭火球、Boss 扇形 7 发（速度分层 8/10/12）。
- 公平阀门：幽火出膛前 20% 速度渐升至 100%（60f wind-up）；扇形速度慢（≤12）。

循环表：`A1 → A2 → A1 → A3 → 回头`。仆从被清空时只在 A1 起始补齐（有 32f DissolveBurn 现形
预告，现形期无伤害）。

### 幕二（悬浮枢纽 Hover 45f → 招式 → 每 2 招插入 C 连接器）

**B1 脓雨落潭 SputumRain（~170f）**
- 预告 50f：5 个 SoftGlow 呼吸圈**锚定玩家当前帧位置**（跟随绘制，如实告知"落在你附近"）；
- 50f：高空生成 5 颗下坠脓球（8~14f 落地），落地即成 SputumPool（自带 20f windup，命中叠"衰朽"）。
- 公平阀门：潭伤害窗口 = windup 之后；圈与落点同源（释放帧玩家位置 ± 固定偏移）。

**B2 尸链复生 CorpseChain（~150f）**
- 预告 30f：Boss 到玩家方向的 BeamGrad 细línea（0.35 亮度渐升）；50f 掷链（19px/f）。
  链落地且仆从 <4 时复生一只幽火。
- 公平阀门：单发直线弹、预告线明确。

**B3 疫风走廊 PlagueCorridor（~280f）**
- 进入招式帧服务器决定缺口列 `corridorGap`（**同步**，两波各自决定）；
- 预告 60f：15 列中除缺口±1 外，每列顶部魂焰汇聚（下落尘 + 柱顶光核），
  缺口列画 **Safe 翠玉色升柱脉冲**（明确安全缝，修复撒谎预告）；
- 60f 释放第一波：非缺口列生成魂焰柱 HoqingSoulPillar（预警 45f + 激活 50f + 消散 20f，
  SoulFlame 着色器柱体）；
- 128f 服务器决定第二波缺口（移位 ±1~2 列），130~180f 预告，180f 释放（第一波已完全消散，
  同屏柱数 ≤ 12）。
- 公平阀门：柱伤害窗口 = 激活期；缺口宽 3 列（390px）；两波柱体不重叠。

**B4 魂灯环阵 LanternRing（~180f，灯自走 176f）**
- 8 盏魂灯（HoqingSoulLantern）在玩家周围 r=430px 环上 DissolveBurn 现形 40f（现形期无伤害无碰撞）；
- 3 波（40/74/108f）：奇偶灯按波次交替射径向（朝环心）与切向弹，波间可从灯间隙径向逃出；
- 灯毕自动熄灭。
- 公平阀门：弹速 ≤8.5；灯本身不造成接触伤害；波间隔可穿。

**C 鬼影横掠 PhantomSweep 连接器（~130f）**
- DissolveBurn 消隐 20f（原地留残影）→ 瞬移至玩家水平一侧 ~880px（屏缘）→
  40f 现形预告（鬼火凝聚 + 前置警示音 + BeamGrad 水平预告线）→ 46px/f 水平横掠（零转向），
  沿途播 3 枚定点漂浮鬼火 → 到另一侧硬刹 ×0.7。
- 公平阀门：消隐期无伤害无碰撞；现形预告 40f；横掠高度锁定在预告线上（可跳/可蹲避）；
  掠后若距玩家 >1400px 立刻回收（teleport-loop 消灭"飞回来"死时间）。

### 幕三（祭坛序列每轮洗牌同步；fan=腐橙致命 / ring=鬼绿）

**D1 瞬掠上坛 AltarRush（≤34f）**
- 8f 反向抽离（pow(t,6)）→ 1f 设定 `v = dir*52` 冲向祭坛 → 过冲 ~40px 弹性回落。
  再无 lerp 滑行。

**D2 绕坛蓄力 AltarChannel（100f）**
- Boss 绕祭坛 r=60px 慢速盘旋（不死站）；收束粒子密度 ∝ sqrt(t)，**76f 处硬切全静默**；
  祭坛 RadialBloom 增亮 + ArenaRunic 提亮（沿用 ScreenSystem）；释放方向 60f 处锁定（同步）
  并以 dust 弧线标出扇形边缘；近身 360px 每 20f 叠"衰朽"（保留）。
- 公平阀门：蓄力全程可被看见的颜色语言（腐橙=扇形朝你，鬼绿=全向脉冲）；释放方向提前 40f 定死。

**D3 释放 AltarRelease（~48f）**
- 1f：扇形 = 13 发三速分层（9/11.5/14，±35°）；全向 = 双圈 24 发错位（速度 6.5/9）。shake 8。
- 47f 收招漂移 → 下一祭坛（序列洗牌，不再 0→1→2→3 机械轮）。

**D4 大招·鬼门开 GhostGate（~400f，四坛毕后触发一次）**
- 0~30f：冲回 arenaCenter 上空；四经络光带全亮汇入中心（ScreenSystem 标量）；
- 30~120f：**鬼门撕开**（GhostGate decal 0→1，90f Execution 级预告；rumble = t²·4；
  75% 处收束粒子静默）；
- 120~300f：门全开——每 30f 涌 1 串蛇形波（12 颗首尾相接，朝玩家方向 ±14°，正弦幅 110px，
  速度 9，纵向留缝）共 6 波；每 60f 门喷 1 圈 16 发慢速环（速度 4，背景压力）；
  Boss 于门前缓移压阵（fogWarp 满强度）；
- 300~345f：门收拢，Boss 退出压阵；345f 回到 D1（祭坛重新洗牌）。
- ≤15% 强化：蛇形波 8 波（波距 30f→22f）+ 波幅 130px。
- 公平阀门：门弹全部慢速（≤9）、密度靠编排不靠数量；大招期间祭坛蓄力停摆（单一弹源）；
  门位置固定于 arenaCenter 上方（不追人）。

### 死亡·鬼门收葬 DeathThroes（~250f，CheckDead 拦截）

| 帧 | 内容 |
|---|---|
| 0 | 清弹；`life=1` + 无敌；所有仆从崩解；经络/祭坛快速熄灭 |
| 0~50 | 硬刹悬停；抽搐（随机偏移振幅 1→4px 渐升）；尸火向上外泄；低吼 |
| 50~140 | 身后 240px 处鬼门撕开（0→1 / 90f）；Boss 被拖向门（lerp 0.02→0.08 渐强）；速度线粒子流入门 |
| 140~155 | **15f 全静默**：粒子停、抖动停、门脉动冻结（inhale） |
| 155 | 爆点：shake 16 + 天空白闪（gateFlash）+ gore 爆发 + 本体 DissolveBurn 崩解开始 |
| 155~200 | 门吸尽余烬后合拢（1→0 / 45f）；fogWarp 归零 |
| ~205 | `life=0` 真死：OnKill 照常（downed 标记 + 掉落） |

## 5. 三大演出脚本（入场/换阶段见上表，此处补入场细节）

**入场·阴门显形（Intro ~150f，i 帧）**
- 0f：出生即隐形（演出标量 spawnReveal=0），激活天空；低鸣（Item103 pitch-0.6）；
- 0~40f：出生点鬼火尘向心收束 + 小型门缝 decal（GhostGate 开度 ≤0.22）+ rumble 1~2；
- 40~80f：DissolveBurn 1→0 显形，scale 0.85→1.06→1.0（BackOut）；门缝闭合；
- 80~110f：**完全静止凝视玩家**（menace is stillness），只有眼位红尘；88f 起仪仗队 6 只每 5f
  依次现形；
- 110f：怒吼（Roar pitch-0.5）+ shake 12 + 冥月满亮；
- 110~150f：缓推入战位 → 幕一，解除 i 帧。

**换阶段（Transition ~110f / 进幕三 ~130f）**
- 1f：清弹 + ForceRoar + shake 12 + fogWarp 脉冲（瞬冲 0.8 回落）；
- 0~50f：疠气爆涌（外喷转内吸两段）；
- 进幕二：50~90f 冥月月晕扩大（sky 标量 moonPhase 0→0.5）；
- 进幕三：50~110f arenaCenter 锁定 + 四祭坛每 15f 依次点亮（ArenaRunic 淡入 + 每座一声钟）
  + 冥月渗血（moonBlood 0→1）；
- 尾 20f 缓出，解除 i 帧。

## 6. 视觉技术方案

**新建着色器（全部 ps_3_0，Hoqing 前缀，本单元内静态缓存，不注册 ACMShaders）：**

| 文件 | 用途 | 要点 |
|---|---|---|
| `Effects/HoqingGhostGate.fx` | 鬼门（大招 + 死亡 + 入场门缝） | 屏幕空间 SDF 竖椭圆裂隙：内部极坐标涡流噪声向内流动、边缘魂焰灼边、外围噪声裂纹光丝；uOpen 控开度、uFlash 死亡白闪；由 ScreenSystem 以满屏噪声绘制（同 ArenaRunic 用法，不读 screenTarget、不占全屏名额） |
| `Effects/HoqingSoulFlame.fx` | 魂焰柱 / 魂灯火苗 | 程序化火焰：uv.y 差频双层流动噪声、底亮顶散、腐橙芯→鬼绿缘→透明、fbm 撕裂边；uPillar 切柱模式（横向对称衰减 + 预警半透模式 uWarn） |
| `Effects/HoqingPlagueMiasma.fx` | 天空疠雾 + 冥月 | 双层差速 fbm 疠雾（下浓上稀）、冥月光晕（uMoonPos/病绿核+外晕）、uMoonBlood 血环渗出、uFlash 死亡白闪；替代永远为 null 的旧贴图层，Sky.Draw 内 End/Begin 绘制（参考 AncestralDragonSky） |

**复用共享件**：DissolveBurn（现形/消隐/崩解）、GenericWarp·fog（幕三+大招+死亡限视，走全屏名额契约）、
ArenaRunic（祭坛 ×4）、BeamGrad（经络、冲刺/横掠/尸链预告线）、RadialBloom（蓄力辉光）、
SoftGlow/Smoke/LightShot（粒子复合）、TelegraphColors（Lethal/Safe/GhostGreen/Flame/NetherViolet）。

**本体绘制升级**：速度门控拖尾（仅 |v|>18 显影）；蓄力时 scale 脉冲 + 边缘泛光；
死亡抽搐偏移 + DissolveBurn 崩解；帧逻辑修复为 0~3 合法域。

## 7. 性能与多人预算

- **全屏后处理**：仅 GenericWarp·fog 一处（Hoqing.PostDraw，走 `RequestFullscreenSlot` 名额 +
  `MythologyConfig` 开关，强度 <0.02 直接 return）。鬼门/雾/柱全部为非 screenTarget 绘制。
- **粒子**：预告类 dust 每帧 ≤30（全局节流：各预告以 %3/%4 间隔喷发）；蓄力收束用 `sqrt` 曲线
  控制生成率并在后段硬切。
- **弹幕上限**：魂焰柱同屏 ≤ 15；魂灯 8；蛇形波每串 12、串间隔 30f；环形 16。
- **BeamGrad 条带**：≤8/帧（4 经络 + 预告线 ≤4）。
- **多人安全**：`corridorGap` / `altarOrder`(4! 排列编码) / `laneDir` / `arenaCenter` /
  `releaseAngle` / 招式选择等决策全部服务器决定 + `SendExtraAI` 同步 + 切换帧 `netUpdate`；
  演出标量（plagueAccum/fogWarp/gateOpen/spawnReveal 等）各端各算；弹幕/NPC 生成全部
  `!VaultUtils.isClient` 判定；`Main.LocalPlayer` 只出现在绘制/Sky 路径。
- **静态缓存**：3 个新 Effect 以 `Asset<Effect>` 静态缓存（Xuanwu 写法），Unload 由字段置空即可
  （惰性 Request，无手动 Dispose 需求）。

## 8. 实施清单

1. `Effects/HoqingGhostGate.fx` / `HoqingSoulFlame.fx` / `HoqingPlagueMiasma.fx` 新建 + 按名编译。
2. `Hoqing.cs` 全重写：新阶段枚举 + 循环表/随机不重复选招 + 三大演出（Intro/Transition/DeathThroes
   + CheckDead 拦截）+ 幕三祭坛洗牌与鬼门大招 + 帧逻辑修复 + 绘制升级 + 同步字段扩展。
3. `HoqingProjectiles.cs`：GhostFire 仪仗编队与齐射仪式；新增 HoqingSoulPillar（魂焰柱）、
   HoqingSoulLantern（魂灯）、HoqingSputumGlob（脓球下坠）、蛇形波模式并入 GhostFireProj(ai 模式)；
   CorpsePit/SputumPool 表现统一；HoqingShadow 改纯视觉。
4. `HoqingScreenSystem.cs`：扩展发布标量（gateCenter/gateOpen/gateFlash/leyBurst），新增鬼门
   decal 绘制层；保留祭坛/经络/蓄力三层。
5. `HoqingSky.cs`：程序化重做（PlagueMiasma 全屏层 + moonPhase/moonBlood/flash 标量），删除
   永远为 null 的旧贴图字段；保留 `LoadInstance` 注册名不变。
6. 验证：CompileFX 按名编译 3 个着色器至退出码 0；ReadLints 对 5 个 .cs 清零。
7. 最后一步：hjson（zh-Hans + en-US）补新弹幕 DisplayName 键，小步 StrReplace + 立即回读验证。
