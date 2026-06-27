# 洪荒模组 · 着色器 & VFX 工具箱与规范书 Shader & VFX Toolkit & Standards

> **文档性质：** Boss 二次迭代（视觉/演出）的**唯一权威技术规格**（Master VFX Spec）
> **版本：** v1.0 · 2026-06-27
> **适用：** `docs/BOSS_REDO_V2/` 下四路区域 Boss 设计与后续实现
> **前置阅读：** `docs/PROGRESSION_DESIGN_SPEC.md`（可玩性/进度），本文只管**视觉与演出**
> **代码勘察基准（本文所有 API 均来自仓库实测）：**
> `Effects/*.fx` · `Effects/CompileFX.ps1` · `Celestias/Boss/AncestralDragonSouls/AncestralDragonSky.cs` · `Celestias/Boss/FourSacredBeasts/Xuanwus/Xuanwu.cs` · `Celestias/Boss/Dazhengs/DazhengArenaBarrier.cs` · `Celestias/PillarofTheHeavenes/HeavenlyEffect.cs` · `ACMUtils.cs` · `ACMMod.cs` · `IACMLoader.cs`

---

## 0. 摘要 TL;DR

本模组**已经具备完整的 HLSL 自定义着色器能力**，且仓库内已有 9 个 `.fx` 源、可用的离线编译脚本、以及三类成熟的运行期套路：

1. **全屏程序化天幕**（`ModSceneEffect` + `CustomSky` + `SkyManager`，shader 在 `CustomSky.Draw` 里全屏绘制）；
2. **全屏后处理**（在 NPC 的 `PostDraw` 里 `sb.Draw(Main.screenTarget, ...)` 套自定义 effect，做扭曲/焦散/色散）；
3. **屏幕空间 overlay**（在 `ModProjectile.PreDraw` 里全屏画噪声纹理 + 圆形 SDF，做竞技场结界）。

外加 **CPU 程序化 FBM 噪声纹理生成**（`GenerateTileableFBM`）、**TriangleStrip 带状拖尾**（`ACMUtils.BuildRibbonStrip` + `gd.DrawUserPrimitives`）两条不依赖 `.fx` 的低成本路线。

> **结论：** 二次迭代**无需引入新库**即可达成高质量演出；优先复用本文 §B 的「已实现原语」，仅在 §D「愿望清单」列出的少数 set-piece 才需要新写 `.fx`。

---

## A. 着色器构建/加载流水线 Shader Build & Load Pipeline

### A.1 现有资产清单 Existing Assets

`Effects/` 目录（源 `.fx` + 编译产物 `.fxc`/`.xnb`）：

| 着色器 | 类型 | 运行期用途 | 入口/Pass |
|--------|------|-----------|-----------|
| `AncestralDragonSky.fx` | 全屏程序化天幕 | 祖龙残魂天空（fbm 云海 + 龙鳞光轮 + Voronoi 星辰） | `technique AncestralSky / P0`，`ps_3_0` |
| `XuanwuFrostDistortion.fx` | 全屏后处理 | 玄武冰霜 UV 扭曲 + 色散 + 边缘霜冻 | `Technique1 / FrostDistortionPass`，`ps_3_0` |
| `XuanwuCaustics.fx` | 全屏后处理 | 水面焦散光斑叠加 | `ps_3_0` |
| `XuanwuIceField.fx` / `XuanwuIcePillar.fx` | 屏幕/世界 overlay | 玄武冰原/冰柱 | — |
| `XuanwuVenomAura.fx` | overlay | 玄武毒雾光环 | — |
| `XuanwuTrailRibbon.fx` | 带状拖尾 | 蛇身/弹幕 ribbon | — |
| `DazhengArenaCircle.fx` | 屏幕空间圆环 SDF | 大椿竞技场结界（藤蔓/根须/呼吸） | `Technique1 / ArenaCirclePass`，`ps_3_0` |
| `BloodSeaAtmosphere.fx` | 全屏氛围 | 血海生物群系大气 | — |

> 共用约定：**双采样器** `uImage0 : register(s0)`（场景或占位）+ `uNoise : register(s1)`（可平铺噪声）；标准 uniform 命名 `uTime / uIntensity / uCenter / uAspect / uPhase / uResolution`。**新 shader 必须沿用这套命名**，以便 C# 侧统一封装。

### A.2 编译：CompileFX.ps1

脚本路径：`Effects/CompileFX.ps1`（注意脚本头注释写的是 `Assets/Effects`，实际目录为 `Effects/`，脚本用 `$PSScriptRoot` 自动定位，无影响）。

- **编译器：** 硬编码 `C:\Users\Hommeng\Documents\My Games\Terraria\tModLoader\FXC\fxc.exe`（DirectX `fxc.exe`）。
- **目标 profile：** `fxc /nologo /T fx_2_0 /Fo <name>.fxc <name>.fx` → 产出 **`.fxc`**（effect bytecode）。
- **退出码：** `0` 全部成功（含无需编译）；`1` 有失败/缺失 —— **agent 可据此判定编译结果**。

```powershell
# 重编译单个（推荐：改完 .fx 后按名字强制覆盖）
pwsh Effects/CompileFX.ps1 AzureDragonAurora
# 多个 / 通配
pwsh Effects/CompileFX.ps1 XuanwuFrostDistortion XuanwuCaustics
pwsh Effects/CompileFX.ps1 Xuanwu*
# 仅补齐缺失产物（向后兼容旧批处理）
pwsh Effects/CompileFX.ps1
# 全量强制重编译
pwsh Effects/CompileFX.ps1 -All
```

> **注意事项**
> - 给定名字时为**强制重编译**（先删旧 `.fxc` 再编，避免失败时残留旧产物被误判成功）。
> - 非交互（输出被 agent 捕获）时**绝不阻塞**；真实终端会暂停等回车。
> - 仓库现存 `DazhengArenaCircle.xnb` 是历史产物；新 shader 统一走 `.fxc` 即可，`ModContent.Request<Effect>` 按**源相对路径去扩展名**解析，能找到对应编译产物。

### A.3 加载 + 应用：运行期最小骨架 Runtime Skeleton

所有 effect 通过 **`ModContent.Request<Effect>(路径, AssetRequestMode.ImmediateLoad)`** 取得，路径形如 `"AncientChineseMythology/Effects/<Name>"`（无扩展名）。**惰性缓存到 static 字段**，切勿每帧重新 Request。

```csharp
// —— 通用全屏后处理骨架（提炼自 Xuanwu.cs DrawShaderEffects）——
private static Asset<Effect> _fxRef;            // static 缓存
private static Texture2D _noiseTex;             // 程序化噪声(可平铺), static 缓存

private static Effect GetFx() =>
    (_fxRef ??= ModContent.Request<Effect>(
        "AncientChineseMythology/Effects/MyBossWarp",
        AssetRequestMode.ImmediateLoad)).Value;

// 在 NPC.PostDraw / 或专门的绘制 System 中调用（绝不在服务端）
private void DrawWarp(SpriteBatch sb, float intensity) {
    if (Main.dedServ || intensity <= 0.01f) return;
    Effect fx = GetFx();
    if (fx == null) return;

    Vector2 centerUV = (NPC.Center - Main.screenPosition)
                     / new Vector2(Main.screenWidth, Main.screenHeight);
    float aspect = (float)Main.screenWidth / Main.screenHeight;

    fx.Parameters["uTime"]?.SetValue(GlobalTimeSeconds);   // 用秒, 不要用帧号
    fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
    fx.Parameters["uCenter"]?.SetValue(centerUV);
    fx.Parameters["uAspect"]?.SetValue(aspect);

    // 第二采样器(噪声)走 device 槽位 1
    var gd = Main.graphics.GraphicsDevice;
    gd.Textures[1] = _noiseTex;
    gd.SamplerStates[1] = SamplerState.LinearWrap;

    sb.End();
    sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
             DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
    sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);   // 把整屏喂给 shader
    sb.End();
    // 恢复项目默认状态(PointClamp + GameViewMatrix)
    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
             DepthStencilState.None, RasterizerState.CullNone, null,
             Main.GameViewMatrix.TransformationMatrix);
}
```

> 关键差异：
> - **全屏后处理**喂 `Main.screenTarget`（已渲染场景），shader 里 `tex2D(uImage0, coords)` 即原画面，做扭曲/色散/染色。
> - **天幕/overlay**喂一张占位贴图（`ACMAsset.BlankStar`）并画满 `new Rectangle(0,0, W*2, H*2)`，shader 完全程序化生成颜色（见 `AncestralDragonSky.Draw`）。
> - 用 `Immediate` 模式才能让单次 `sb.Draw` 自动 Apply effect 的 Pass。

### A.4 天幕骨架：ModSceneEffect + CustomSky + SkyManager

这是本模组**最成熟、最该复用**的全屏演出框架（祖龙、玄武、百目、阴天子…都用它）：

```csharp
// 1) 场景触发器：决定何时挂起天幕滤镜
internal class MyBossSceneEffect : ModSceneEffect {
    public override int Music => -1;
    public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
    public override bool IsSceneEffectActive(Player p)
        => NPC.AnyNPCs(ModContent.NPCType<MyBossHead>());
    public override void SpecialVisuals(Player p, bool isActive) {
        if (p.Alives()) p.ManageSpecialBiomeVisuals(MyBossSky.SkyName, isActive);
    }
}

// 2) 天幕本体：实现 CustomSky + IACMLoader（自动注册, 见 ACMMod 的 VaultUtils.GetDerivedInstances）
public class MyBossSky : CustomSky, IACMLoader {
    public const string SkyName = "ACM:MyBossSky";
    void IACMLoader.LoadData() {
        SkyManager.Instance[SkyName] = this;
        // 复用 vanilla 屏幕滤镜压暗/染色, 让 shader 天幕更突出
        Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
            .UseColor(0f, 0f, 0f).UseOpacity(0.35f), EffectPriority.High);
    }
    public override void Draw(SpriteBatch sb, float minDepth, float maxDepth) {
        if (!(maxDepth >= 0 && minDepth < 0)) return; // 仅最远背景层
        // ... A.3 全屏 shader 绘制 ...
    }
    // Activate/Deactivate/Update/IsActive/Reset 见 AncestralDragonSky.cs
}
```

> **IACMLoader 装配（来自 `ACMMod.cs`）：** `Load()` 调 `VaultUtils.GetDerivedInstances<IACMLoader>()` 自动实例化全部实现类并执行 `LoadData()`；`PostSetupContent()` 执行 `SetupData()`，客户端再执行 `LoadAsset()`。**任何天幕/资源类只要实现 `IACMLoader` 就自动接入生命周期，无需手动注册。**

### A.5 CPU 程序化噪声（无贴图依赖）

`Xuanwu.cs` / `DazhengArenaBarrier.cs` 都内置 `GenerateTileableFBM(size, octaves, seed)` → 生成**可无缝平铺的三通道 FBM 噪声** `Texture2D`（R/G/B 各一套独立噪声，shader 中分通道采样得有机纹路）。**应抽到公共工具**（建议 `ACMUtils.GenerateTileableNoise`）供二次迭代复用，避免每个 Boss 各抄一份。

### A.6 共享演出 API（`ACMShaders` 助手）— Wave-2 必用 ★

> **来源：** Wave-1（玄武 / 百目无常 / 旱魃 / 敖闰）四路一致反馈——旧 API 只暴露裸 `Effect`，逼每个 Boss 手抄 `BuildRibbonStrip + 设全部 uniform + End/Begin/Apply/DrawUserPrimitives + 恢复批` 一大坨样板。下列助手已把 Wave-1 验证过的套路固化进 `ACMShaders`，**Wave-2 的 beam/decal/全屏类演出一律先用助手，不要再手抄。**

**绘制类（自管批次）：**

```csharp
// 1) 光束直带 (从旱魃 HanbaLaser.DrawBeamGrad 提升)。须在已有活动批阶段调用 (如 ModProjectile.PreDraw)。
//    内部: BuildRibbonStrip(两端点退化直线带) → 设 BeamGrad uniform → End→Begin(Immediate,Additive,LinearWrap,GameViewMatrix)
//          → 绑共享噪声到 s0/s1 → Apply → DrawUserPrimitives(TriangleStrip) → RestoreDefaultBatch。
//    顶点契约: 位置 = 世界坐标 - Main.screenPosition (屏幕像素), 配 Main.GameViewMatrix.TransformationMatrix; uv.x=沿长, uv.y=横宽。
ACMShaders.DrawBeam(Vector2 worldStart, Vector2 worldEnd, float halfWidth, Color core, Color edge, float intensity,
                    float flowSpeed = 1.4f, float flowScale = 2.0f, float coreSharp = 2.2f, float coreGlow = -1f);
//    coreGlow<0 → 取 core.A/255 (沿用旧 alpha 行为); 见下方 §A.6 uCoreGlow 说明。

// 2) 世界点加性径向泛光 (从旱魃 DrawRadialBloomOverlay 提升)。内部自动 RequestFullscreenSlot() (占本帧唯一全屏名额)。
//    须在已有活动批阶段调用 (PreDraw): 内部 End 当前批 → 画 overlay → RestoreDefaultBatch。radius=屏幕高度比例。
ACMShaders.DrawRadialBloomAt(Vector2 worldCenter, float radius, float intensity, Color color,
                             float rayCount = 10f, float falloff = 2.5f);

// 3) 自管批次的屏幕空间地纹/法阵/牢笼 (补 DrawScreenSpaceDecal/DrawFullscreenOverlay 之间的空档)。
//    DrawScreenSpaceDecal 假定"有活动批"(End→Begin→End→恢复); 本方法自行 Begin/End, 供 PostDrawTiles / 无活动批阶段调用。
//    绘制前设好 effect 参数 (可配合 SetCommonParams)。
ACMShaders.DrawScreenSpaceDecalStandalone(Effect fx, BlendState blend = null);
```

> **三类 decal 绘制助手的选择**（别再纠结）：
> | 调用阶段 | 是否已有活动批 | 用哪个 |
> |----------|----------------|--------|
> | `ModProjectile.PreDraw` / `ModNPC.PreDraw` / 全屏后处理插入 | 有 | `DrawScreenSpaceDecal`（地纹）/ `ApplyScreenPostProcess`（喂 screenTarget）|
> | `ModSystem.PostDrawTiles` / 自定义无批阶段 | 无 | `DrawScreenSpaceDecalStandalone`（地纹）/ `DrawFullscreenOverlay`（占位像素）|

**坐标 / 参数助手（抽掉每 Boss 重抄的样板）：**

```csharp
// 4) 缩放感知 世界圆/方 → 屏幕UV (从 DazhengArenaBarrier/Hanba 抽取, 已补齐 Wave-1 遗漏的 zoom 项)。
//    放大镜/缩放下中心与半径仍对齐世界。worldRadius: 圆=半径, 方=半边长。
ACMShaders.WorldDecalParams(Vector2 worldCenter, float worldRadius,
                            out Vector2 uvCenter, out float radiusFrac, out float aspect);

// 5) 一次性设最常见四个共享 uniform: uTime(秒)/uCenter(世界→屏幕UV)/uAspect/uIntensity (省 ~4 行/调用点)。
//    时间统一用 Main.GlobalTimeWrappedHourly; 需自定义时间/中心的高级调用改用各自 Parameters。
ACMShaders.SetCommonParams(Effect fx, Vector2 worldCenter, float intensity);
```

> ⚠️ `WorldDecalParams` **含 zoom**（`Main.GameViewMatrix.Zoom.X`），是 Wave-1 手抄版常漏的关键项（漏了在缩放视角下中心/半径错位）。`SetCommonParams` 的 `uCenter` 走**不含 zoom**的简单换算（适配多数全屏 overlay）；需要 zoom 对齐的世界 decal 请用 `WorldDecalParams` 取 `uvCenter` 再单独 `SetValue`，**不要**叠用 `SetCommonParams` 覆盖。

**着色器扩展（本轮新增 uniform）：**

- **`BeamGrad.fx` → `float uCoreGlow`**：芯部加法过曝辉度，**专用**，取代旧版借用 `uColorCore.a` 的做法（`uColorCore.a` 现仅参与不透明度权重 lerp）。`DrawBeam` 自动传值（默认 `core.A/255` → 与旧视觉一致）。HLSL 未设时默认 0，故老调用若直接设 `Effect` 须显式给 `uCoreGlow`。
- **`ArenaRunic.fx` → `float uShape`**：`0=圆形`(默认，欧氏距离) / `1=矩形/方形`(chebyshev `max(|dx|,|dy|)` 距离，等距线为正方形)。**方形竞技场**（如旱魃 800 半笼）设 `uShape=1` 边界才正确成方；圆形/牢笼 (`uMode`) 模式不受影响。

**Wave-1 验证回归：** 旱魃 `HanbaLaser` 激光 → `DrawBeam`；旱魃大招泛光 → `DrawRadialBloomAt`；旱魃鬼域牢笼 → `WorldDecalParams + uShape=1`；敖闰霜冻法阵 → `DrawScreenSpaceDecalStandalone`。视觉保持（牢笼由圆改方为 §item7 刻意修正）。

### A.7 PaletteLUT 阴阳分屏 split-math（百目无常 报告）

`PaletteLUT.fx` 的 `uSplit=1` 阴阳分屏，C# 侧换算（百目·黑白无常验证）：

```csharp
// uSplitDir: 归一化的"屏幕方向"(分屏法线), 直接给屏幕空间单位向量即可 (如 Vector2.UnitX 或斜向)。
fx.Parameters["uSplitDir"]?.SetValue(splitDirScreen.SafeNormalize(Vector2.UnitX));
// uSplitPos: 中线沿法线的投影位置。shader 内 mid = uSplitPos * (1+aspect)*0.5, 故反推:
//   想让缝隙落在屏幕投影值 proj 处 → uSplitPos = proj / ((1 + aspect) * 0.5)
float aspect = (float)Main.screenWidth / Main.screenHeight;
fx.Parameters["uSplitPos"]?.SetValue(projAlongDir / ((1f + aspect) * 0.5f));
```

> shader 内 `proj = dot(float2(coords.x*aspect, coords.y), normalize(uSplitDir))`，`seam = proj - mid`。把屏幕坐标的目标投影 `projAlongDir` 反算成 `uSplitPos` 即可让缝隙精确落位（居中分屏取 `projAlongDir ≈ (1+aspect)*0.5*0.5`）。

---

## B. 可复用 VFX / 着色器原语目录 Reusable VFX Primitives

> 图例：**[已实现]** 仓库已有可直接抄；**[组合]** 用现有原语拼；**[新 .fx]** 需新写着色器。
> 成本：⚡极低（CPU 顶点/dust）· ⚡⚡中（单 pass shader）· ⚡⚡⚡高（全屏后处理/RT）。

### B.1 加法 TriangleStrip 拖尾 Additive Primitive Trail — [已实现] ⚡

- **是什么：** 由中心线点列生成 `TriangleStrip` 带状拖尾，可贴 `SwordTrail*`/`SoftGlow`/`GlaciateWave` 纹理，配 `BlendState.Additive`。
- **何时用：** 武器挥砍残影、蛇形 Boss 身段、冲刺尾迹、弹幕轨迹、冲击环。
- **实现：** `ACMUtils.BuildRibbonStrip(positions, widthFunc, colorFunc, uvScroll, subdivisions)` 返回 `ColoredVertex[]`（内部 CatmullRom 平滑 + 法线挤出）→ `gd.Textures[0]=tex; gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length-2)`。须在 `sb.Begin(..., Additive, ..., Main.GameViewMatrix.TransformationMatrix)` 内绘制。参考 `RuyiStickSpearProjectile.cs`、`Xuanwu.DrawSnakeBody`、`AoshunProjectiles.cs`。
- **成本：** ⚡ 纯 CPU 顶点，无 `.fx`。**首选拖尾方案。**
- **二次迭代建议：** 统一「外宽暗 ribbon + 内窄亮 ribbon」双层套路（玄武蛇身/暴雪枪已用），形成全 Boss 一致的拖尾质感。

### B.2 程序化全屏天幕 Procedural Sky — [已实现] ⚡⚡

- **是什么：** `ps_3_0` 全程序化天空（fbm 域扭曲云海 + 极坐标光轮 + Voronoi 星辰 + 三段阶段色板）。
- **何时用：** 每个**正式区域大 Boss**的专属天空；随血量切阶段色调（祖龙：玄青→紫芒→赤金）。
- **实现：** 复制 `AncestralDragonSky.fx/.cs`，替换色板与 accent 形状（光轮瓣数、环纹频率）。`uPhase` 由 `boss.life/lifeMax` 驱动并 `Lerp` 平滑。
- **成本：** ⚡⚡ 单 pass、全屏一次 draw；fbm 5 octave，1080p 可接受。
- **依赖：** 新 `.fx`（但是**改色板而非重写**，工作量小）。

### B.3 全屏热浪/折射扭曲 Screen Distortion / Heat-Haze — [已实现] ⚡⚡⚡

- **是什么：** 采样 `Main.screenTarget`，用噪声驱动 UV 偏移 + RGB 色散，从 Boss 中心径向衰减。
- **何时用：** 火焰/爆炸热浪、冰晶折射、虚空塌陷、强力技能蓄力的空间扭曲。
- **实现：** 抄 `XuanwuFrostDistortion.fx`（已含径向/切向扭曲、色散、边缘覆盖、呼吸脉冲），调 `FrostTint`→对应主题色即可得「火浪」「毒雾」「雷场」变体。C# 侧见 §A.3 + `Xuanwu.DrawShaderEffects`（喂 `Main.screenTarget`，噪声走槽位 1）。
- **成本：** ⚡⚡⚡ 全屏每像素多次 `tex2D`，且打断主 SpriteBatch（End/Begin）。**强度<0.01 直接 return**；同屏**最多 1 个**生效（见 §C 守则）。
- **依赖：** 复用同一 `.fx`，仅换 tint/参数。

### B.4 色散/瞬移闪光 Chromatic / Teleport Flash — [组合] ⚡

- **是什么：** 瞬移/受击/相变瞬间的高亮闪 + 短促 RGB 错位。
- **何时用：** Boss 瞬移、弹反、阶段切换的「定格」一帧。
- **实现：**
  - 轻量版：`Xuanwu.DrawPhaseFlash`——`ACMAsset.SoftGlow` 加法大 scale 一闪（`phaseFlash` 衰减）。⚡
  - 重量版：复用 B.3 扭曲 shader 的色散分量，`uIntensity` 做一次 0→1→0 的尖脉冲。⚡⚡⚡
- **成本：** 轻量版几乎免费，**优先**。
- **依赖：** 无 / 复用现有 `.fx`。

### B.5 竞技场地面/结界 Decal Arena Ground / Barrier — [已实现] ⚡⚡

- **是什么：** 屏幕空间圆形 SDF + 噪声纹路（藤蔓/根须/符文/呼吸脉动），界外推力 + 伤害。
- **何时用：** 限制战场的大 Boss（大椿已用）；地面法阵/封印圈/熔岩裂纹地贴。
- **实现：** `DazhengArenaBarrier.cs`（`ModProjectile`，`Projectile.hide` + `DrawBehind` 画在 NPC 之后）+ `DazhengArenaCircle.fx`（极坐标环 + 多层噪声）。把世界半径换算到屏幕 UV：`screenPos=(worldOffset-halfScreen)*zoom+halfScreen`，`radiusUV=screenRadius/screenHeight`。
- **成本：** ⚡⚡ shader 内 `normDist>1.6 || <0.4` 早退，只算圆环带像素。
- **依赖：** 复用 `DazhengArenaCircle.fx`（改 `uColorPrimary/uColorSecondary` + 纹路频率即可得不同主题结界）。**注意逻辑/伤害须 server 权威，绘制 client-only。**

### B.6 全屏入场/相变滤镜 Full-Screen Intro / Phase Filter — [已实现] ⚡

- **是什么：** 通过 `Filters.Scene[name]` 挂 vanilla `ScreenShaderData("FilterMiniTower")` 做整屏染色/压暗，配合天幕淡入。
- **何时用：** Boss 入场镜头、进入二/三阶段的全屏「变色定调」。
- **实现：** 在天幕 `LoadData()` 里 `Filters.Scene[name]=new Filter(new ScreenShaderData("FilterMiniTower").UseColor(r,g,b).UseOpacity(o), EffectPriority.High)`；激活由 `ModSceneEffect.IsSceneEffectActive` + `ManageSpecialBiomeVisuals` 控制。相变时改 `UseColor`/`UseOpacity` 或切到另一个 filter。
- **成本：** ⚡ vanilla 内置 pass，开销极低。
- **依赖：** 无 `.fx`（用 vanilla 滤镜）。需要程序化图案时升级为 B.2/B.3。

### B.7 自定义天空（粒子版） Custom Sky (Particle) — [已实现] ⚡

- **是什么：** 不写 shader 的 `CustomSky`，靠 `Dust` + 贴图（`Smoke`/`LightShot`/`BlankStar`）软件粒子堆出祥云/光柱/花瓣/光球。
- **何时用：** 氛围向、性能敏感、或美术希望手绘质感的区域（天柱仙气区已用）。
- **实现：** `HeavenlyEffect.cs`（`HeavenlySky : CustomSky` + `HeavenlyWorldDrawSystem` + `ModPlayer` 三层）。粒子用内置对象池（Reset/Activate/Update）。
- **成本：** ⚡ 但粒子数要设上限（祥云 80 / 粒子 40 / 花瓣 30）。
- **依赖：** 无。**低端/演出降级时的天然 fallback。**

### B.8 冲击波/折射环 Shockwave Rings — [已实现] ⚡

- **是什么：** `TriangleStrip` 双环（内外半径随时间扩张、alpha 衰减），可叠加 B.3 做空间折射。
- **何时用：** 落地砸击、爆炸、能量释放、招式命中点。
- **实现：** `Xuanwu.DrawShockwaveRing`（48 段环，inner/outer 顶点，加法）。半径 `shockwaveRadius` 随时间线性扩张，`shockwaveAlpha` 衰减。
- **成本：** ⚡ 纯顶点。
- **依赖：** 无（要真实折射再叠 B.3）。

### B.9 光束/激光 Beam / Laser — [组合] ⚡~⚡⚡

- **是什么：** 沿方向的 `TriangleStrip` 长条（芯白 + 外晕），首尾用 `SoftGlow` 收口；可加流动 UV。
- **何时用：** 持续光束、扫射激光、连线攻击（阴天子/百目/苍龙均适用）。
- **实现：** `BuildRibbonStrip` 退化为直线带（两端点）+ 流动 `uvScroll`；芯部 `LightShot`、外晕 `SoftGlow`，端点画 `SoftGlow` 圆。参考 `AoshunProjectiles.cs` 的多种带状构造。
- **成本：** ⚡ 顶点；要发光辉度叠 additive 即可。
- **依赖：** 无 / 可选 `XuanwuTrailRibbon.fx` 风格流动 shader。

### B.10 溶解/灼烧 Dissolve / Burn — [新 .fx] ⚡⚡

- **是什么：** 用噪声阈值做 alpha 裁切，沿裁切边产生发光「灼烧边」，实现 Boss 召唤/死亡/部件消融。
- **何时用：** 入场材质化显形、死亡崩解、护盾破碎、分身生成/消失。
- **现状：** 仓库**暂无**专用 dissolve shader（B.4 闪光是替代的廉价方案）。
- **实现：** 新写单 pass `ps_3_0`：`clip(noise - threshold)`，`edge = smoothstep(threshold, threshold+w, noise)` 上色发光；`threshold` 由 C# 0→1 推进。可直接复用 `GenerateTileableFBM` 噪声 + §A.3 骨架（喂 Boss 贴图而非 screenTarget）。
- **成本：** ⚡⚡ 仅作用于 Boss 贴图区域。
- **依赖：** **需要新 `.fx`**（见 §D）。

> **原语 ↔ 资产对照：** 现成贴图（`ACMAsset`）`BlankStar / GlaciateWave / LightShot / Smoke / SoftGlow / Sparkle / EmberShards / SlashBurst / LightningBranch / ElectricArcSheet`；占位 `VaultAsset.placeholder2`。拖尾贴图 `Textures/Projectiles/SwordTrail55 / 551 / 553`。**新增 Boss 优先从这些里选，缺了再画。**

---

## C. 演出规范与守则 Standards / Conventions

> 目标：四路 Boss 由不同实现者并行开发，仍要**整体观感统一**、**不互相打架**、**不炸帧**。

### C.1 预警色彩语言 Telegraph Color Language（强制）

红色=即将造成伤害的**致命**预警；其余为主题色，不与红冲突：

| 含义 | 颜色 | 用法 |
|------|------|------|
| **致命攻击预警**（落点/激光路径/冲刺线） | 纯红 `#FF2838`（`new Color(250,40,56)`，已用于如意棒拖尾） | 闪烁/充能渐强，命中前 0.3–0.6s 必须可读 |
| 安全/治疗/神圣（天庭） | 金白 `#FFFAD0` / 翠玉 `#DCFFE6`（`HeavenlyEffect.heavenlyColors`） | 天庭线氛围与正反馈 |
| 地府/阴 | 幽蓝紫 / 鬼绿 | 地府线氛围；DoT 标记 |
| 冰/水（玄武四海） | 冰蓝 `#8CC7F2` / 深冰 `#264073`（`FrostTint/DeepFrost`） | 冰系预警用「冰白高光」做边 |
| 雷 | 青白电弧（`LightningBranch`/`ElectricArcSheet`） | 高频闪 |
| 阶段升级 | 主题色 → 提亮/转暖（祖龙 玄青→紫→赤金） | 见 B.2 阶段色板 |

> 规则：**预警必有「形状 + 颜色 + 渐强时间」三要素**；红色只留给真正的伤害源，避免「狼来了」。

### C.2 屏幕震动预算 Screen-Shake Budget（强制）

为避免叠加成「地震」，全程统一预算（建议封装 `ACMUtils.AddScreenShake(amount)`，取 max 而非累加）：

| 事件 | 强度（像素峰值） | 时长 |
|------|------------------|------|
| 小命中/格挡 | ≤ 2 | ≤ 6 帧 |
| 落地/普通爆炸 | 4–6 | 8–12 帧 |
| 相变/大招释放 | 8–12 | 12–20 帧 |
| 入场/死亡定格 | ≤ 16（一次性） | ≤ 30 帧 |

- 同帧多源**取最大值**，不累加；衰减用 `*=0.9` 指数。
- 提供 `MythologyConfig.ScreenShakeScale`（0–1）供玩家缩放，0 时完全关闭。

### C.3 入场 / 相变 / 死亡演出节拍 Cinematic Beats（建议统一）

| 节拍 | 时长 | 视觉栈 |
|------|------|--------|
| **入场 Intro** | 1.5–2.5s | 天幕淡入(B.2/B.7) + `Filters.Scene` 染色(B.6) + 溶解显形(B.10) + 一次性大闪(B.4) + 入场震动 |
| **相变 Phase** | 0.6–1.2s | 全屏闪(B.4) + 天幕 `uPhase` 切色 + 冲击环(B.8) + 中度震动；短暂无敌避免被秒过场 |
| **死亡 Death** | 1.5–3s | 慢动作(可选) + 多段溶解/崩解(B.10) + 渐隐天幕 + 最终大冲击波(B.8) |

> 天幕的 `intensity` 用 `Lerp` 淡入(~0.012/帧)淡出(~0.02/帧)；相变 `phase` 用 `Lerp(...,0.02)` 平滑（见 `AncestralDragonSky.Update`）。

### C.4 性能护栏 Performance Guardrails（强制）

1. **Effect/纹理只 Request 一次**：缓存到 `static Asset<Effect>` / `static Texture2D`；**禁止**每帧 `ModContent.Request<...>(ImmediateLoad)`（`ImmediateLoad` 会卡顿）。现有代码用 `??=` 惰性缓存，照抄。
2. **全屏后处理同屏 ≤ 1 个生效**：B.3 这类喂 `Main.screenTarget` 的 shader 很贵且会打断主 SpriteBatch；多 Boss 同屏时按优先级只跑一个。`uIntensity < 0.01` 立即 `return`。助手 `DrawRadialBloomAt` / `ApplyScreenPostProcess`（配 `RequestFullscreenSlot`）已内建该名额仲裁。
   - **关于"叠两层"的少数合法情形（如玄武冰霜扭曲 + 水面焦散）：** 预算指的是**喂 `Main.screenTarget` 的全屏后处理 (`RequestFullscreenSlot` 名额)**——它每帧仍 ≤1。`ElementalScreenTint` / `RadialBloom` 这类**不读 screenTarget、只画占位像素**的 overlay 不占该名额（`DrawFullscreenOverlay`），可作为廉价的"装饰性第二层"叠加。**不为此新增任何多层后处理机制**：真要同时跑两个 screenTarget pass 的 Boss 必须**自行仲裁**（如玄武 `DrawShaderEffects` 在拿到唯一名额后，自己决定一帧内串跑 frost+caustics，且二者各自做 End→Begin→恢复批配对）——这是该 Boss 自管的内部选择，**不打破全局名额契约**。
3. **shader 内尽早 early-out**：用 `normDist` / 距离阈值跳过大片像素（`DazhengArenaCircle` `>1.6||<0.4` 返回透明；`FrostDistortion` `>2.5` 返回原色）。
4. **复用 RenderTarget / 噪声纹理**：噪声纹理 static 生成一次（`EnsureNoiseTexture` + `IsDisposed` 校验）；`Unload()` 里 `Dispose` + 置 null。**不要**每帧 `new Texture2D`/`new RenderTarget2D`。
5. **SpriteBatch 配对**：每次 `sb.End()`→自定义 `Begin`→画→`End`→**必须**恢复项目默认 `Begin(Deferred, AlphaBlend, PointClamp, ..., Main.GameViewMatrix.TransformationMatrix)`。漏恢复会污染后续所有绘制。
6. **顶点拖尾设上限**：`oldRot`/`oldPos` 历史点数固定（如 12），`subdivisions` 默认 3；过密会拖帧。
7. **服务端零绘制**：所有绘制函数 `if (Main.dedServ) return;`；伤害/推力逻辑放 `Main.netMode != Server`/server 权威分支（见 `DazhengArenaBarrier`）。

### C.5 多人 & 低端安全 Multiplayer / Low-End Safety（强制）

- **逻辑与表现分离**：竞技场伤害、相变判定、出生/死亡在 server 决策并同步；shader/dust/震动纯本地。
- **天幕/滤镜按本地玩家就近触发**：`ModSceneEffect.IsSceneEffectActive(player)` 已是 per-player，沿用即可。
- **降级开关（建议新增 `MythologyConfig`）：**
  - `EnableFullscreenShaders`（默认开）：关后 B.3/B.6 退化为纯 dust/粒子(B.7)；
  - `ScreenShakeScale`（0–1）；
  - `TrailQuality`（High/Med/Off → 控制 `subdivisions` 与拖尾段数）。
- **`Main.gameMenu` / 截图模式**下跳过 overlay 绘制（参考 `HeavenlyWorldDrawSystem.PostDrawTiles`）。

---

## D. 着色器愿望清单 Shader Wishlist

映射候选 set-piece → 需新写或扩展的 `.fx`（按性价比排序；多数是「改色板/参数」而非从零）。

| 优先 | 着色器 | 服务 set-piece | 路线 | 工作量 |
|------|--------|----------------|------|--------|
| P0 | `DissolveBurn.fx`（B.10） | 通用：Boss 入场显形 / 死亡崩解 / 分身生成 | **新 `.fx`**（单 pass clip + 灼烧边），复用 `GenerateTileableFBM` | 小 |
| P0 | `GenericHeatWarp.fx` | 把 `XuanwuFrostDistortion` 泛化成主题可换（火/毒/雷/虚空）的统一扭曲 | **重构现有**：暴露 `uTint/uChroma/uWarpScale` 参数 | 小 |
| P1 | `<Boss>Sky.fx` ×N | 各区域大 Boss 专属天幕（地府入侵、阴天子、苍龙真身、四海龙王） | 复制 `AncestralDragonSky.fx` 改色板/光轮形状 | 中（每个小） |
| P1 | `ArenaRunic.fx` | 通用法阵/封印地贴（不同主题结界） | **复用** `DazhengArenaCircle.fx` 换色 + 符文频率 | 小 |
| P2 | `BeamFlow.fx` | 阴天子/百目/苍龙的持续激光（流动 + 内辉） | 扩展 `XuanwuTrailRibbon.fx` 风格，配 B.9 顶点 | 中 |
| P2 | `VoidCollapse.fx` | 虚空/黑洞类大招（径向吸入 + 强色散 + 暗角） | 在 `GenericHeatWarp` 上加径向 UV 收缩 | 中 |
| P3 | `ShockRefraction.fx` | 冲击波环的真实空间折射（叠在 B.8 上） | 全屏后处理，按环半径做窄带 UV 扭曲 | 中（性能敏感，受 C.4#2 约束） |

> **复用优先级提醒：** 二次迭代里**约 80% 的演出**可由 §B 已实现原语 + vanilla `Filters.Scene` 达成；新 `.fx` 集中在 P0 的两个**通用**着色器（溶解 + 泛化扭曲），它们能被四路 Boss 共享，是最高 ROI。

---

## 附录 — 关键文件索引 Key File Index

| 用途 | 参考文件 |
|------|----------|
| 编译脚本 | `Effects/CompileFX.ps1` |
| 全屏程序化天幕（范例） | `Celestias/Boss/AncestralDragonSouls/AncestralDragonSky.cs` + `Effects/AncestralDragonSky.fx` |
| 全屏后处理扭曲/焦散（范例） | `Celestias/Boss/FourSacredBeasts/Xuanwus/Xuanwu.cs`（`DrawShaderEffects`）+ `Effects/XuanwuFrostDistortion.fx`、`XuanwuCaustics.fx` |
| 屏幕空间竞技场结界（范例） | `Celestias/Boss/Dazhengs/DazhengArenaBarrier.cs` + `Effects/DazhengArenaCircle.fx` |
| 粒子版 CustomSky + 滤镜（范例） | `Celestias/PillarofTheHeavenes/HeavenlyEffect.cs` |
| TriangleStrip 拖尾工具 | `ACMUtils.cs`（`BuildRibbonStrip`）；用例 `Projectiles/RuyiStickSpearProjectile.cs`、`Xuanwu.DrawSnakeBody/DrawShockwaveRing` |
| 程序化噪声生成 | `Xuanwu.cs` / `DazhengArenaBarrier.cs` 的 `GenerateTileableFBM`（建议抽公共） |
| IACMLoader 生命周期 | `IACMLoader.cs` + `ACMMod.cs`（`VaultUtils.GetDerivedInstances`） |
| 现成贴图资产 | `ACMAsset.cs` |

---

*Primordial / 洪荒 · Boss Redo V2 · Shader & VFX Toolkit v1.0 · 实现演出前请先读 §C 守则与 §C.4 性能护栏。*
