// ============================================================
// 天庭监察者 — 监察扫描线全屏后处理 (OverseerScanline)
// "你正在被天庭的机械法眼审计": CRT 细扫描线 + 周期下扫亮带(带内折射)
// + 屏幕两缘数据流字符雨 + 故障 glitch(RGB 撕裂/行错位, 窥视假预告与死亡演出复用)
// + 审判红化收紧(uLockdown) + 开机自上而下点亮(uBoot, 入场演出复用)
// 喂 Main.screenTarget(s0) + 共享可平铺噪声(s1); 走 RequestFullscreenSlot 名额契约
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标
sampler uNoise  : register(s1); // 可平铺噪声 (RGB 三通道独立)

float  uTime;       // 动画时间(秒)
float  uIntensity;  // 总强度 0~1 (扫描线/数据雨/亮带)
float  uGlitch;     // 故障强度 0~1
float  uLockdown;   // 审判红化 0~1
float  uBoot;       // 开机进度 0~1 (>=1 为已完成, 无遮蔽)
float  uAspect;     // 宽高比 width/height
float2 uFocus;      // 焦点(本体)屏幕 UV — 数据聚焦亮斑

// 监察金 / 玉色 / 冷监视蓝
static const float3 ScanGold = float3(1.00, 0.84, 0.47);
static const float3 ScanJade = float3(0.43, 0.86, 0.67);
static const float3 ScanBlue = float3(0.35, 0.59, 0.84);
static const float3 LockRed  = float3(0.86, 0.10, 0.14);

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    return frac(p * (p + p));
}

float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float4 OverseerScanlinePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float boot = saturate(uBoot);
    bool booting = boot < 0.999;

    // ---------- 故障: 行错位 + RGB 撕裂 ----------
    float2 uv = coords;
    float chroma = 0.0;
    if (uGlitch > 0.01)
    {
        float row = floor(coords.y * 52.0);
        float tick = floor(uTime * 22.0);
        float rn = hash21(float2(row, tick));
        // 少数行大幅错位, 多数行不动 → 读感是"信号损坏"而非糊
        float rowSel = step(1.0 - uGlitch * 0.30, rn);
        uv.x += rowSel * (rn - 0.5) * 0.10 * uGlitch;
        chroma = 0.0038 * uGlitch;
    }

    float3 scene;
    scene.r = tex2D(uImage0, uv + float2(chroma, 0)).r;
    scene.g = tex2D(uImage0, uv).g;
    scene.b = tex2D(uImage0, uv - float2(chroma, 0)).b;

    float k = saturate(uIntensity);
    if (k < 0.01 && uGlitch < 0.01 && uLockdown < 0.01 && !booting)
        return float4(scene, 1.0);

    float3 col = scene;

    // ---------- CRT 细扫描线 (很淡, 只给"被监视的介质感") ----------
    float fine = 0.5 + 0.5 * sin(coords.y * 942.0);
    col *= 1.0 - fine * 0.05 * k;

    // ---------- 周期下扫亮带 (带内 1~2px 折射) ----------
    float bandPos = frac(uTime * 0.115);
    float bd = coords.y - bandPos;
    float band = exp(-bd * bd * 5200.0);
    if (band > 0.003)
    {
        float2 ruv = uv;
        ruv.x += band * 0.0035 * sin(coords.y * 260.0 + uTime * 8.0);
        float3 refr = tex2D(uImage0, ruv).rgb;
        col = lerp(col, refr, band * 0.8);
        float3 bandTint = lerp(ScanBlue, ScanGold, saturate(uLockdown + k * 0.4));
        col += bandTint * band * 0.22 * k;
    }

    // ---------- 屏幕两缘数据流字符雨 ----------
    float edgeL = smoothstep(0.085, 0.012, coords.x);
    float edgeR = smoothstep(0.915, 0.988, coords.x);
    float edge = max(edgeL, edgeR);
    if (edge > 0.01)
    {
        float2 cell = float2(floor(coords.x * 150.0), floor(coords.y * 84.0));
        float colKey = hash11(cell.x * 7.13);
        float fall = floor(uTime * (5.0 + colKey * 11.0));
        float g = tex2D(uNoise, float2(cell.x / 150.0, (cell.y + fall) / 84.0) * 1.7).g;
        float glyph = step(0.74, g) * (0.35 + 0.65 * frac(g * 9.0));
        // 列头亮尾暗: 用另一路噪声做每列亮度包络
        float head = tex2D(uNoise, float2(colKey, (coords.y + fall * 0.012)) * 0.9).r;
        float3 rainCol = lerp(ScanJade, ScanGold, colKey);
        rainCol = lerp(rainCol, LockRed, uLockdown * 0.75);
        col += rainCol * glyph * head * edge * 0.34 * k;
    }

    // ---------- 焦点数据晕 (本体位置微弱金晕, 提示"演算中枢") ----------
    float2 fpos = float2(coords.x * uAspect, coords.y);
    float2 fcen = float2(uFocus.x * uAspect, uFocus.y);
    float fd = length(fpos - fcen);
    float focusGlow = exp(-fd * fd * 26.0) * 0.10 * k;
    col += ScanGold * focusGlow;

    // ---------- 审判红化收紧 ----------
    if (uLockdown > 0.01)
    {
        float2 cpos = float2((coords.x - 0.5) * uAspect, coords.y - 0.5);
        float r = length(cpos);
        float redEdge = smoothstep(0.30, 0.78, r) * uLockdown;
        float flick = 0.85 + 0.15 * sin(uTime * 24.0);
        col = lerp(col, LockRed * (0.32 + scene.r * 0.5), redEdge * 0.55 * flick);
    }

    // ---------- 开机自上而下点亮 ----------
    if (booting)
    {
        float line0 = boot * 1.06;
        float below = smoothstep(line0, line0 + 0.015, coords.y); // 1 = 尚未点亮
        // 未点亮区: 近黑 + 微静态噪声
        float stat = hash21(float2(floor(coords.x * 320.0), floor(coords.y * 180.0) + floor(uTime * 30.0)));
        float3 offCol = float3(0.012, 0.016, 0.02) + stat * 0.035 * float3(0.6, 0.8, 1.0);
        col = lerp(col, offCol, below);
        // 点亮行: 亮带
        float ignite = exp(-(coords.y - line0) * (coords.y - line0) * 9000.0);
        col += lerp(ScanBlue, ScanGold, boot) * ignite * 0.9;
    }

    return float4(col, 1.0);
}

technique Technique1
{
    pass OverseerScanlinePass
    {
        PixelShader = compile ps_3_0 OverseerScanlinePS();
    }
}
