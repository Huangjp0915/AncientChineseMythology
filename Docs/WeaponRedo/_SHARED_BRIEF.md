# 武器系列重做工程 —— 共享作战简报

> 本文件由协调者维护，供各武器系列重做子代理开工前完整阅读并严格遵守。
> 多个子代理正在**同一仓库并行工作**，且**另有一批 Boss 重做代理同时在跑**，第 4 节并行纪律为硬性约束。

## 0. 使命

对本模组（tModLoader Mod `AncientChineseMythology` / Primordial，古中国神话题材）的每个武器系列执行一轮完整重做：
**现状分析 → 设计文档 → 实施 → 编译验证 → 汇报**。

目标是"顶级演出效果"：每件武器有清晰的机制身份、有重量感的使用手感、分层的命中反馈、可读的轨迹语言；系列旗舰有专属 ps_3_0 着色器与"大招时刻"。预算与时间不设限，深度优先，但演出永远服务于可玩性（玩家手持武器，特效不得遮蔽战场）。

## 1. 开工前必读（按顺序）

1. `C:\Users\Hommeng\.cursor\skills\boss-fight-choreography\SKILL.md` 及同目录 `MOTION.md` —— 前摇/爆发/收招波形、冲击反馈、次级运动等运动设计方法论（对武器手感同样适用）。
2. 本简报全文。
3. `Helpers/WeaponVFX.cs` 与 `Helpers/ACMWeaponBurst.cs` 的文件头注释（共享武器 VFX 地基的复用约定）。
4. 你负责系列的全部代码 + 两个本地化文件中属于该系列的条目（`Localization/zh-Hans_Mods.AncientChineseMythology.hjson` 与 `en-US` 版）。
5. 若实现会用到 InnoVault API（PRT 粒子、BaseHeldProj 手持弹幕、ItemUseAnimation 等）：先读 `C:\Users\Hommeng\.cursor\skills\innovault-consumer\SKILL.md`（路由文件），再按需读其指向的子文档。

## 2. 仓库速览

- 引擎：tModLoader（net8.0），引用库 InnoVault（`build.txt: modReferences = InnoVault`），根命名空间 `AncientChineseMythology`。
- 共享基础设施（**只可调用，不可修改**）：
  - `Helpers/WeaponVFX.cs` —— 武器 VFX 地基：`GetEffect`（按名缓存着色器）、`AddScreenShake`（预算化震屏）、`DrawRibbonTrail`/`DrawProjectileTrail`（双层顶点拖尾）、`DrawRadialBloom`（占名额径向泛光，满则自动退化）、`DrawGlowBurst`（廉价柔光）、`DrawShockwaveRing`（顶点冲击环）、`ApplyDissolveBurn`（贴图溶解）、`ApplyPaletteTint`（全屏染色，强度≤0.15）。
  - `Helpers/ACMWeaponBurst.cs` —— 更新阶段（OnHitNPC 等）安全接入 shader 级命中演出的一次性弹幕；主题常量已按系列登记（Bronze/Crimson/Gold/Gem/Fatal/DivineWood/ArrogantSylvan/Profane/Soul/Fox/FoxCharm/Scorch/Bone/Shadow/Water/SoulFire/AbyssPurple/GhostGreen/FengduVoid/NetherGrudge/LethalRed/EastSeaWater/ClockworkGold/GoldDragon/AncestralSoul/HeavenlyPillar 等）。**只消费勿改**；确需新主题时在你自己的代码里定义配色，不动此文件。
  - `Effects/ACMShaders.cs` —— 共享着色器中心：`DissolveBurn`、`GenericWarp`、`ElementalScreenTint`、`PaletteLUT`、`ArenaRunic`、`BeamGrad`（顶点条带光束）、`RadialBloom`、`ReflectWard`；共享噪声 `ACMShaders.NoiseTexture`。
  - **全屏后处理名额契约**：任何全屏后处理必须 `ACMShaders.RequestFullscreenSlot()` 取得名额（每帧同屏 ≤ 1），强度 < 0.01 直接 return，尊重 `MythologyConfig` 开关。经 `WeaponVFX.DrawRadialBloom`/`ApplyPaletteTint` 调用则内部已处理。
  - `ACMAsset.cs` —— 通用遮罩纹理（BlankStar / GlaciateWave / LightShot / Smoke / SoftGlow / Sparkle / EmberShards / SlashBurst / LightningBranch / ElectricArcSheet）。
  - `ACMUtils.cs` —— 工具函数（`BuildRibbonStrip`、`GenerateTileableNoise`、`AddScreenShake` 等）。
  - `Effects/TelegraphColors.cs` —— 预警配色规范；`Systems/ACMScreenShakeSystem.cs` —— 震屏预算（取 max 不累加）。
- 质量标杆（参考不抄袭）：`Celestias/Boss/AoGuangs/Items/`（潮涌龙杖等：ribbon 龙身 + 主题 Burst + 每五发大招节奏）、`NPCs/Boss/KyuubiKitsunes/Items/DakkiBook.cs`（染屏大招）。
- 着色器编译：`powershell -ExecutionPolicy Bypass -File Effects\CompileFX.ps1 <Shader名1> <Shader名2>`（仓库根目录运行，退出码 0 才算过）。像素着色器一律 **ps_3_0**（technique 里 `compile ps_3_0 XxxPS();`）。

## 3. 流程要求（逐步执行，不可跳过、不可调换）

### 3.1 现状分析

通读你系列的全部代码（物品 / 弹幕 / 手持弹幕 / Buff / UI），以下面八条"武器手感透镜"逐件诊断：

1. **一眼身份**：拿起武器 3 秒内能感知其独特机制吗？还是换皮数值棒？
2. **三段感**：使用动画有没有前摇→爆发→收招的重量曲线，还是匀速挥舞？
3. **命中反馈栈**：命中是否有 柔光闪/冲击环/震屏/音高分层 的复合反馈？
4. **轨迹可读性**：弹幕与挥砍是否有清晰拖尾与统一颜色语言？
5. **机制深度**：有无蓄力/连段/替代攻击/资源循环等主动决策点？
6. **演出峰值**：有没有"大招时刻"（每 N 次/蓄满/处决）提供节奏高点？
7. **系列一致性**：系列内配色/形状语言是否统一、是否呼应其来源（材料/Boss/神话原型）？
8. **性能卫生**：每帧分配、粒子上限、着色器缓存、多人安全。

判断重做力度：若某件明显已是近期高水准（已用 WeaponVFX/ACMWeaponBurst 且机制完整），做**提升补强**；否则全面重做机制与视觉。

### 3.2 设计文档（先文档、后代码）

- 写入 `Docs/WeaponRedo/<系列名>.md`，简体中文。
- 必含章节：
  1. 现状诊断（逐件要点）
  2. 系列主题与幻想感（这个系列"应该给玩家什么体验"，神话原型引用）
  3. 逐件机制设计（左键/右键或蓄力/大招时刻；前摇-爆发-收招帧数；决策点）
  4. 系列内梯度（低阶件朴素、旗舰件豪华的演出递进）
  5. 视觉技术方案（哪些复用 WeaponVFX/共享着色器，哪些新建系列专属 ps_3_0 着色器）
  6. 平衡与定位（保持获取途径与进度位不变；伤害/攻速调整须给出 DPS 对比论证）
  7. 性能与多人预算
  8. 实施清单

### 3.3 实施

- 按文档落地，允许大改你系列文件内的一切代码。
- 手感优先：使用 anticipation/burst/recovery 波形（挥砍角速度曲线、蓄力抖动、后坐）、速度对比、命中反馈栈（`ACMWeaponBurst.Spawn` + `WeaponVFX.AddScreenShake` 预算内 + 音高随机）。
- 演出梯度：低阶件用共享原语（拖尾/柔光/冲击环）即可；系列旗舰配 1-2 个专属着色器与全屏级"大招时刻"（走名额契约）。**不要给每件小武器都堆专属 shader**——一致性与性能优先。
- 近战大件鼓励改造为手持弹幕（InnoVault `BaseHeldProj` 或原生 held projectile）实现自定义挥舞曲线；改造前读 InnoVault skill。
- 音效：分层复用 `SoundID`（叠加低频冲击 + 高频质感，Pitch 随机 ±0.1~0.2），不新增音频文件。
- 新 Buff 类直接写在你系列自己的 .cs 文件里，不动共享 `Buffs/` 既有文件。

### 3.4 验证

- 着色器：**按名字**编译自己的着色器，退出码 0 才算过；失败必须修复后重验。
- C#：**禁止运行 dotnet build**（多个并行代理会文件锁冲突且相互误报）。改用 ReadLints 工具对你**改过/新建的每个 .cs 文件**做诊断，属于你系列的错误必须清零；其他文件夹报出的错误与你无关，不要去修。
- 权威全量构建由协调者在整批代理完成后统一执行；若构建发现你名下错误，你会被唤醒修复。

### 3.5 汇报（最终回复格式，简体中文）

1. 现状诊断要点（逐件一句话）
2. 设计核心决策（系列主题 / 代表性新机制 3-5 条）
3. 实施变更清单（文件级）
4. 新增/修改的着色器及编译结果
5. ReadLints 验证结果
6. 平衡影响说明 + 遗留风险与建议（如有）

## 4. 并行纪律（多代理同仓协作，硬性约束）

0. **Boss 战役并行警告**：另一批代理正在重做全部 Boss。**绝对禁止**读改冲突区：`NPCs/Boss/**`、`Underworlds/Boss/**`、`Celestias/Boss/**` 下的任何文件（含其 `Items` 子文件夹——那些 Boss 掉落武器归 Boss 代理管）。只读参考其配色可以，严禁写入。
1. **只允许修改**：
   - 任务分派给你的系列文件（含分派清单中列出的 `Projectiles/` 下属于你系列的弹幕文件）；
   - 你新建的 `Effects/<系列名>*.fx` 及其 `.fxc` 产物；
   - `Docs/WeaponRedo/<系列名>.md`；
   - 两个 hjson 中属于你系列的键区。
2. **禁止**：
   - 修改任何共享件：`Effects/ACMShaders.cs`、`ACMAsset.cs`、`ACMUtils.cs`、`Helpers/WeaponVFX.cs`、`Helpers/ACMWeaponBurst.cs`、`Effects/TelegraphColors.cs`、`Systems/*` 既有代码（全部只可调用）；
   - 修改其他系列/其他代理名下的文件；修改 `Effects/` 下不属于你的 `.fx`；
   - 重命名/删除任何 public 类型（配方、掉落表、本地化键都依赖类名）；
   - 运行**无参数**的 `CompileFX.ps1`（会误编译其他代理的半成品着色器）；运行 dotnet build；
   - 无差别全局重构、格式化他人文件；执行任何 git 操作。
3. **共享弹幕文件归属验证**：改 `Projectiles/` 下任何文件前，先 grep 确认其唯一消费方是你的系列；若被 Boss/NPC 或他系列引用，**不要改它**，在你系列文件内新建替代类。
4. 新着色器一律以你的系列名为前缀（如 `RuyiStaffGoldPillar.fx`），ps_3_0；在你系列代码内用 `WeaponVFX.GetEffect("<名>")` 或静态缓存 `ModContent.Request<Effect>` 获取，**不要**注册进 ACMShaders。
5. 全屏后处理必须走名额契约（见 §2），且仅用于大招/处决的短暂定调。
6. **hjson 并发保护**（两个 hjson 是全体代理 + Boss 战役共享的最高冲突文件）：
   - 把 hjson 编辑放到你**全部代码工作完成后的最后一步**；
   - 编辑前重新 Read 最新内容，用 StrReplace 以你系列独有的键区为锚点做小步编辑；
   - 每次编辑后**立即重新 Read 验证**你的键还在（可能被并行代理的写入覆盖）；若丢失，重新执行你的编辑，直到确认在最新文件中存在；
   - 严禁重写整个文件、严禁 PowerShell echo/Set-Content 输出 hjson；zh-Hans 与 en-US 同步更新；
   - 新增弹幕/Buff 优先在代码中用 `Language.GetOrRegister` 或 `SetStaticDefaults` 局部覆写提供名称，减少对 hjson 的依赖，实在需要才动 hjson。
7. 美术资产：**不新增任何贴图文件**；无贴图弹幕用 `Texture => "InnoVault/Assets/placeholder"`（仓库既有惯例）+ 程序化绘制；视觉全靠现有贴图 + ACMAsset 遮罩 + 噪声 + 着色器/顶点/粒子。
8. 代码注释用简体中文，只解释非显而易见的意图与约束。
9. 忽略工作区既有的 `ItemTexturePrompts.generated.json` 与 Boss 战役产生的任何文件变动。
10. 你的任务工作量较大，如内部存在可并行的独立块（如逐件武器、着色器编写），可自行拆分内部子代理并行推进，但对外交付与并行纪律不变。

## 5. 技术底线

- **多人安全**：弹幕生成走 owner 客户端（`Shoot` 钩子天然如此；主动生成判 `player.whoAmI == Main.myPlayer` 或 `Projectile.owner == Main.myPlayer`）；`Main.LocalPlayer`、震屏、染屏等只出现在绘制/本地路径；武器状态存 `Item`/`Projectile.ai[]`/`ModPlayer` 可同步字段，不依赖客户端静态变量承载 gameplay 状态（纯视觉除外）。
- **性能**：不在 Update/Draw 中每帧 Request 纹理/Effect/新建 RenderTarget；粒子与弹幕数量设上限；拖尾受 `MythologyConfig.Trail` 降级；避免热路径每帧 LINQ/大数组分配。
- **平衡/进度**：配方、掉落来源、商店、稀有度、职业定位（近战/远程/魔法/召唤）不可回退；伤害与攻速调整幅度须在文档中论证（默认 DPS 变化 ±15% 内，超出需强理由）。
- **公平/可读**：伤害判定与视觉严格对齐；特效不遮蔽敌人弹幕（武器特效透明度克制、生命周期短）；震屏遵守预算（小命中 ≤2 / 爆炸 4-6 / 大招 8-12）。
