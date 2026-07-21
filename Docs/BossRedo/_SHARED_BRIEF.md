# Boss 重做工程 V3 —— 共享作战简报

> 本文件由协调者维护，供各 Boss 重做子代理开工前完整阅读并严格遵守。
> 多个子代理正在**同一仓库并行工作**，第 4 节并行纪律为硬性约束。

## 0. 使命

对本模组（tModLoader Mod `AncientChineseMythology` / Primordial，古中国神话题材）的每个 Boss 执行一轮完整重做：
**现状分析 → 设计文档 → 实施 → 编译验证 → 汇报**。

目标是"顶级演出效果"：电影化的入场/换阶段/死亡三大节拍，有重量感与速度感的动作编排，高级着色器与高级绘制加持的视觉，同时保持可读性与公平性。预算与时间不设限，深度优先。

## 1. 开工前必读（按顺序）

1. `C:\Users\Hommeng\.cursor\skills\boss-fight-choreography\SKILL.md` 及同目录的 `MOTION.md`、`PACING.md` —— 编排设计核心方法论（七大本能、失败模式、节奏工程、多 Boss 分工、演出配方）。
2. 本简报全文。
3. 你负责的 Boss 文件夹全部代码 + 两个本地化文件中属于该 Boss 的条目（`Localization/zh-Hans_Mods.AncientChineseMythology.hjson` 与 `en-US` 版）。
4. 若实现会用到 InnoVault API（PRT 粒子、UIHandle、BaseHeldProj、TileProcessor 等）：先读 `C:\Users\Hommeng\.cursor\skills\innovault-consumer\SKILL.md`（路由文件），再按需读其指向的子文档。

## 2. 仓库速览

- 引擎：tModLoader（net8.0），引用库 InnoVault（`build.txt: modReferences = InnoVault`），根命名空间 `AncientChineseMythology`。
- Boss 三大区：`NPCs/Boss/*`（地表/主世界）、`Underworlds/Boss/*`（阴间）、`Celestias/Boss/*`（天界）。
- 共享基础设施：
  - `Effects/ACMShaders.cs` —— V2 共享着色器注册中心：`DissolveBurn`（溶解/灼烧）、`GenericWarp`（热浪/寒雾/裂隙等全屏扭曲）、`ElementalScreenTint`（全屏染色）、`PaletteLUT`（调色重映射）、`ArenaRunic`（法阵/牢笼 SDF）、`BeamGrad`（顶点条带光束）、`RadialBloom`（径向泛光）、`ReflectWard`（折射护盾）；共享噪声 `ACMShaders.NoiseTexture`（256² 三通道 FBM）。
  - **全屏后处理名额契约**：调用 `ACMShaders.RequestFullscreenSlot()`，每帧同屏 ≤ 1 个全屏后处理，强度 < 0.01 直接 return，且受 `MythologyConfig.FullscreenShadersEnabled` 配置约束。
  - `ACMAsset.cs` —— 通用遮罩纹理（BlankStar / GlaciateWave / LightShot / Smoke / SoftGlow / Sparkle / EmberShards / SlashBurst / LightningBranch / ElectricArcSheet），字段注释里有尺寸与用法说明。
  - `ACMUtils.cs` —— 工具函数（含 `GenerateTileableNoise`）。
  - `Effects/TelegraphColors.cs` —— 预警配色规范。
  - `ScreenShakePlayer.cs` —— 屏幕震动。
  - `IACMLoader` —— LoadData/SetupData/LoadAsset/UnLoadData 生命周期接口，`ACMMod` 自动收集。
- 质量标杆（近期已重做，作为参考而非抄袭）：`Celestias/Boss/FourSacredBeasts/Xuanwus/`（玄武：多专属着色器、焦散/冰场/毒雾演出）与 `Celestias/Boss/Dazhengs/`（大峥：场地结界着色器）。
- 部分 Boss 有专属 `XxxSky` / `XxxScreenSystem`（天空+滤镜、屏幕特效系统），Sky 多在 `ACMMod.Load()` 里 `LoadInstance()` 注册。

## 3. 流程要求（逐步执行，不可跳过、不可调换）

### 3.1 现状分析

- 通读你 Boss 的全部代码（AI / 弹幕 / 绘制 / ScreenSystem / Sky / 掉落 / 召唤物 / Items 子文件夹）。
- 以 choreography skill 的"七大本能"与"失败模式清单"为透镜，列出问题清单：哪里僵硬、失重、无聊、不可读、不公平、演出缺失、状态机死路。
- 判断重做力度：若该 Boss 明显已是近期高水准（已有专属着色器与完整三大演出节拍），做**提升补强**而非推倒重来；否则全面重做 AI 编排与视觉。

### 3.2 设计文档（先文档、后代码）

- 写入 `Docs/BossRedo/<Boss名>.md`，简体中文。
- 必含章节：
  1. 现状诊断
  2. 设计主题与幻想感（这个神话角色"应该给玩家什么体验"）
  3. 阶段结构与血量断点
  4. 招式编排表（每招：前摇/爆发/收招时长帧数、预警方式、公平阀门）
  5. 入场 / 换阶段 / 死亡三大演出脚本
  6. 视觉技术方案（哪些着色器/绘制技术，哪些新建、哪些复用共享件）
  7. 性能与多人预算
  8. 实施清单

### 3.3 实施

- 按文档落地，允许大改你名下文件夹内的一切代码。
- 动作编排使用 skill 中的 anticipation/burst/recovery 波形、速度对比、公平阀门、递进节奏。
- 入场/换阶段/死亡三大演出节拍必须齐备；世界层反馈（天空、滤镜、震屏、顿帧）按主题接入。
- 鼓励大胆使用：全屏后处理（走名额契约）、顶点条带（参考 BeamGrad 用法）、RenderTarget、程序化噪声、粒子编排、Sky/Filter、屏幕震动与顿帧。

### 3.4 验证

- 着色器编译（在仓库根目录运行，**按名字**编译自己的着色器，退出码 0 才算过）：
  `powershell -ExecutionPolicy Bypass -File Effects\CompileFX.ps1 <Shader名1> <Shader名2>`
- C# 验证：**禁止运行 dotnet build**（多个并行代理会文件锁冲突，且会把彼此的半成品编进来造成误报）。改用 ReadLints 工具对你**改过/新建的每个 .cs 文件**做诊断，属于你文件夹的错误必须清零；其他文件夹报出的错误与你无关，不要去修。
- 权威全量构建由协调者在整批代理完成后统一执行；若构建发现你名下的错误，你会被唤醒修复。
- 修到全绿为止；着色器编译失败必须修复后重验。

### 3.5 汇报（最终回复格式，简体中文）

1. 现状诊断要点（3-6 条）
2. 设计核心决策（主题 / 阶段结构 / 代表性新招 3-5 条）
3. 实施变更清单（文件级）
4. 新增/修改的着色器及编译结果
5. ReadLints 验证结果
6. 遗留风险与建议（如有）

## 4. 并行纪律（多代理同仓协作，硬性约束）

1. **只允许修改**：
   - 你的 Boss 文件夹内的文件；
   - 你新建的 `Effects/<Boss名>*.fx` 及其 `.fxc` 产物；
   - `Docs/BossRedo/<Boss名>.md`；
   - 两个 hjson 中属于你 Boss 的键区；
   - 确有必要时（如注册新 Sky），对共享文件（`ACMMod.cs` 等）做**单处最小编辑**。
2. **禁止**：
   - 修改其他 Boss 的文件夹；
   - 修改 `Effects/ACMShaders.cs` 既有共享着色器、`ACMAsset.cs`、`ACMUtils.cs` 既有函数（可以调用，不可改动）；
   - 重命名/删除任何 public 类型（跨文件夹引用与本地化键都依赖类名）；
   - 运行**无参数**的 `CompileFX.ps1`（会误编译其他代理的半成品着色器）；
   - 无差别全局重构、格式化他人文件。
3. 新着色器一律以你的 Boss 名为前缀（如 `HanbaSandstorm.fx`），像素着色器用 **ps_3_0**（technique 里 `compile ps_3_0 XxxPS();`）；在 Boss 自己的代码内用 `ModContent.Request<Effect>("AncientChineseMythology/Effects/<名>", AssetRequestMode.ImmediateLoad)` 静态缓存（参考 Xuanwu 写法），**不要**注册进 ACMShaders。
4. 全屏后处理必须走 `ACMShaders.RequestFullscreenSlot()` 名额契约，并尊重 `MythologyConfig` 开关。
5. **hjson 并发保护**（两个 hjson 是全体代理共享的高冲突文件）：
   - 把 hjson 编辑放到你**全部代码工作完成后的最后一步**；
   - 编辑前重新 Read 最新内容，用 StrReplace 以你 Boss 独有的键区为锚点做小步编辑；
   - 每次编辑后**立即重新 Read 验证**你的键还在（可能被并行代理的写入覆盖）；若丢失，重新执行你的编辑，直到确认在最新文件中存在；
   - 严禁重写整个文件、严禁 PowerShell echo/Set-Content 输出 hjson；zh-Hans 与 en-US 同步更新。
   - 若新增弹幕/Buff 仅需默认 DisplayName，可优先在代码中用 `Language.GetOrRegister`/局部覆写 `SetStaticDefaults` 方式减少对 hjson 的依赖，实在需要才动 hjson。
6. 不执行任何 git add/commit/push；忽略工作区既有的 `ItemTexturePrompts.generated.json`。
9. **新系统注册**：需要全局生命周期钩子时，优先实现 `IACMLoader`（`ACMMod` 通过反射自动收集，无需改 `ACMMod.cs`）或使用 `ModSystem`；仅当技术上别无他法时才最小编辑 `ACMMod.cs`，且编辑后立即重读验证未覆盖他人改动。
10. 你的任务工作量较大（分析+文档+实施+验证），如内部存在可并行的独立块（如 AI 重写与着色器编写），可自行拆分内部子代理并行推进，但对外交付与并行纪律不变。
7. 美术资产：**不新增任何贴图文件**；视觉全部靠现有贴图 + ACMAsset 遮罩 + 程序化噪声 + 着色器/顶点绘制/粒子达成。音效复用 `SoundID` 与 `Sounds/` 现有资源，不新增音频文件。
8. 代码注释用简体中文，风格与现有代码一致；注释只解释非显而易见的意图与约束。

## 5. 技术底线

- **多人安全**：AI 逻辑只依赖 `npc.ai[]` / `npc.localAI[]` / `npc.target` 等可同步状态，状态切换时 `npc.netUpdate = true`；`Main.LocalPlayer` 与纯视觉状态只出现在绘制/客户端路径；弹幕与召唤仅在服务器端生成（`Main.netMode != NetmodeID.MultiplayerClient` 判定）。
- **性能**：不在 Update/Draw 中每帧 new 纹理/Effect/RenderTarget；粒子与弹幕数量设上限；全屏后处理遵守名额契约；避免每帧 LINQ 分配热路径。
- **掉落/进度**：Boss 的掉落表、召唤方式、downed 标记、Boss 清单兼容等不可回退（可增强，不可弄丢）。
- **公平性**：每招给足前摇与逃逸窗口；换阶段清弹；伤害窗口与视觉严格对齐；防止 Boss 飞出屏幕绕圈（距离栓绳）；每个状态必须有保底出口（完成或超时）。
