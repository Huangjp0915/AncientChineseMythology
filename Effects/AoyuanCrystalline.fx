// ============================================================
// 敖闰冰晶棱镜着色器 — 全屏后处理 (s0=screenTarget, s1=共享噪声)
// Voronoi 冰晶棱面折射 + 棱面色散 + 时滞去饱和 + 冲击帧 + 边缘结霜
// 与玄武 FrostDistortion(径向裂纹+雾)刻意区分: 敖闰是"镜面棱角/时间冻结"语言
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标
sampler uNoise  : register(s1); // 可平铺噪声 (RGB 三通道独立)

float  uTime;      // 动画时间(秒)
float2 uCenter;    // Boss 归一化屏幕坐标 (0~1)
float  uIntensity; // 棱面折射强度 0~1
float  uAspect;    // 屏幕宽高比 width/height
float  uStill;     // 时滞(时间冻结)去饱和 0~1
float  uFlash;     // 冲击帧黑白高对比 0~1 (全场一次)
float  uFrost;     // 屏幕边缘结霜 0~1

static const float3 FrostTint = float3(0.55, 0.78, 0.95);
static const float3 IceWhite  = float3(0.87, 0.94, 1.00);
static const float3 DeepIce   = float3(0.10, 0.20, 0.42);

float2 hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float4 CrystallinePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(uImage0, coords);
    if (uIntensity < 0.004 && uStill < 0.004 && uFlash < 0.004 && uFrost < 0.004)
        return scene;

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float  dist   = length(pos - center);

    // ==========================================
    //  Voronoi 冰晶棱面 (F1/F2 距离 + 晶胞ID)
    // ==========================================
    float2 vUV  = pos * 7.0;
    float2 cell = floor(vUV);
    float2 f    = frac(vUV);
    float  f1 = 8.0;
    float  f2 = 8.0;
    float2 bestId = float2(0.0, 0.0);
    for (int gy = -1; gy <= 1; gy++)
    {
        for (int gx = -1; gx <= 1; gx++)
        {
            float2 nb  = float2(gx, gy);
            float2 rnd = hash22(cell + nb);
            float2 pt  = nb + 0.5 + (rnd - 0.5) * 0.75 - f;
            float  d   = dot(pt, pt);
            if (d < f1) { f2 = f1; f1 = d; bestId = rnd; }
            else if (d < f2) { f2 = d; }
        }
    }
    f1 = sqrt(f1);
    f2 = sqrt(f2);
    float border = 1.0 - smoothstep(0.0, 0.09, f2 - f1); // 1=棱线上

    // 棱面作用范围: 折射强度扩大半径; 时滞下全屏棱面化
    float reach = 0.35 + uIntensity * 1.4 + uStill * 2.5;
    float facetMask = smoothstep(reach, reach * 0.25, dist);
    facetMask = max(facetMask, uStill * 0.85);

    // ==========================================
    //  棱面折射 + 沿棱面方向色散
    // ==========================================
    float2 facetDir = normalize(bestId - 0.5 + float2(0.0001, 0.0002));
    float  facetAmp = (uIntensity * 0.022 + uStill * 0.010) * facetMask;
    float2 ofs = facetDir * facetAmp;
    ofs.x /= uAspect;
    float2 ruv = clamp(coords + ofs, 0.002, 0.998);

    float3 refr;
    refr.g = tex2D(uImage0, ruv).g;
    float2 chroma = facetDir * facetAmp * 0.55;
    chroma.x /= uAspect;
    refr.r = tex2D(uImage0, clamp(ruv + chroma, 0.002, 0.998)).r;
    refr.b = tex2D(uImage0, clamp(ruv - chroma, 0.002, 0.998)).b;

    float3 col = lerp(scene.rgb, refr, facetMask * saturate(uIntensity * 2.0 + uStill));

    // 棱线冰光 + 晶胞稀疏闪烁
    float lineGlint = border * facetMask * (uIntensity * 0.55 + uStill * 0.30);
    float twinkle = pow(abs(sin(uTime * 1.7 + bestId.x * 37.0 + bestId.y * 61.0)), 24.0);
    lineGlint += twinkle * facetMask * uIntensity * 0.35;
    col += IceWhite * lineGlint;

    // ==========================================
    //  时滞: 去饱和冷调 + 轻暗角 (时间仿佛冻结)
    // ==========================================
    float lum = dot(col, float3(0.30, 0.59, 0.11));
    float3 stillCol = lum * float3(0.80, 0.90, 1.06);
    col = lerp(col, stillCol, uStill * 0.85);
    float2 edge = abs(coords - 0.5) * 2.0;
    float edgeD = max(edge.x, edge.y);
    col *= 1.0 - smoothstep(0.55, 1.25, edgeD) * uStill * 0.35;

    // ==========================================
    //  屏幕边缘结霜 (细晶纹路)
    // ==========================================
    if (uFrost > 0.004)
    {
        float frostBand = smoothstep(0.55, 1.0, edgeD);
        float n = tex2D(uNoise, coords * 5.0 + float2(uTime * 0.010, -uTime * 0.008)).r;
        float pattern = smoothstep(0.35, 0.75, n);
        col = lerp(col, lerp(DeepIce, FrostTint, n), frostBand * pattern * uFrost * 0.55);
        float sparkle = smoothstep(0.47, 0.50, n) * smoothstep(0.53, 0.50, n);
        col += IceWhite * sparkle * frostBand * uFrost * 0.5;
    }

    // ==========================================
    //  冲击帧: 黑白高对比 (死亡碎裂唯一一次)
    // ==========================================
    if (uFlash > 0.004)
    {
        float l2 = dot(col, float3(0.30, 0.59, 0.11));
        float bw = smoothstep(0.42, 0.58, l2);
        float3 flashCol = lerp(float3(0.02, 0.03, 0.06), float3(0.96, 0.99, 1.00), bw);
        col = lerp(col, flashCol, saturate(uFlash));
    }

    return float4(col, scene.a);
}

technique Technique1
{
    pass CrystallinePass
    {
        PixelShader = compile ps_3_0 CrystallinePS();
    }
}
