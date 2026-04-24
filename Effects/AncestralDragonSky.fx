// ============================================================
// 祖龙残魂天空着色器 — 全屏程序化天幕
// 多层域扭曲fbm云海 + 极坐标龙鳞光轮 + Voronoi星辰
// 随Boss阶段切换色调: 玄青 -> 紫芒 -> 赤金
// 完全程序化, 无外部噪声贴图依赖
// ============================================================

sampler uImage0 : register(s0); // 仅占位, 不使用(天空是底层背景)

float  uTime;           // 累计时间(秒)
float  uIntensity;      // 天幕可见度 0~1
float  uPhase;          // 阶段进度 0=常态 0.4=二阶段 0.75=三阶段 1=暴怒
float  uAspect;         // 屏幕宽高比 width/height
float2 uResolution;     // 屏幕分辨率(像素)
float2 uBossUV;         // Boss中心屏幕归一化坐标 0~1
float  uPulse;          // Boss心跳脉冲相位(外部递增)

// 阶段色板
static const float3 SkyTop_A   = float3(0.04, 0.06, 0.14); // 一阶段顶端: 深玄青
static const float3 SkyMid_A   = float3(0.10, 0.18, 0.32);
static const float3 SkyBot_A   = float3(0.42, 0.58, 0.72); // 一阶段地平线: 青白
static const float3 SkyTop_B   = float3(0.12, 0.05, 0.22); // 二阶段: 暗紫
static const float3 SkyMid_B   = float3(0.35, 0.12, 0.45);
static const float3 SkyBot_B   = float3(0.75, 0.55, 0.85);
static const float3 SkyTop_C   = float3(0.28, 0.04, 0.10); // 三阶段: 赤血金
static const float3 SkyMid_C   = float3(0.72, 0.15, 0.10);
static const float3 SkyBot_C   = float3(1.00, 0.70, 0.30);

static const float3 CloudLight = float3(0.92, 0.95, 1.00);
static const float3 DragonGold = float3(1.00, 0.85, 0.40);
static const float3 DragonCyan = float3(0.55, 0.90, 1.00);
static const float3 DragonRed  = float3(1.00, 0.40, 0.25);

// ========================================
//  程序化噪声
// ========================================
float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float2 hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// 五倍频 fbm
float fbm5(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 5; i++)
    {
        v += valueNoise(p) * a;
        p = p * 2.03 + float2(1.7, 3.1);
        a *= 0.5;
    }
    return v;
}

// 域扭曲fbm: 用低频fbm偏移UV后再采样, 形成涡流云气
float warpedFbm(float2 p, float t)
{
    float2 q = float2(fbm5(p + float2(0.0, t * 0.08)),
                      fbm5(p + float2(5.2, t * 0.06 + 1.3)));
    float2 r = float2(fbm5(p + 4.0 * q + float2(1.7, 9.2) + t * 0.12),
                      fbm5(p + 4.0 * q + float2(8.3, 2.8) - t * 0.10));
    return fbm5(p + 4.0 * r);
}

// Voronoi用于星辰分布
float starField(float2 p)
{
    float2 n = floor(p);
    float2 f = frac(p);
    float brightness = 0.0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 g = float2((float)x, (float)y);
            float2 o = hash22(n + g);
            float2 pt = g + o - f;
            float d = dot(pt, pt);
            // 只有少数cell生成星星
            float seed = hash21(n + g);
            float starMask = step(0.92, seed);
            float star = starMask * exp(-d * 140.0);
            brightness += star;
        }
    }
    return saturate(brightness);
}

// ========================================
//  阶段色板采样
// ========================================
float3 SamplePalette_Top(float t)
{
    float3 c1 = lerp(SkyTop_A, SkyTop_B, saturate(t / 0.5));
    float3 c2 = lerp(SkyTop_B, SkyTop_C, saturate((t - 0.5) / 0.5));
    return lerp(c1, c2, step(0.5, t));
}

float3 SamplePalette_Mid(float t)
{
    float3 c1 = lerp(SkyMid_A, SkyMid_B, saturate(t / 0.5));
    float3 c2 = lerp(SkyMid_B, SkyMid_C, saturate((t - 0.5) / 0.5));
    return lerp(c1, c2, step(0.5, t));
}

float3 SamplePalette_Bot(float t)
{
    float3 c1 = lerp(SkyBot_A, SkyBot_B, saturate(t / 0.5));
    float3 c2 = lerp(SkyBot_B, SkyBot_C, saturate((t - 0.5) / 0.5));
    return lerp(c1, c2, step(0.5, t));
}

float3 SampleAccent(float t)
{
    float3 c1 = lerp(DragonCyan, DragonGold * 0.85 + DragonCyan * 0.15, saturate(t / 0.5));
    float3 c2 = lerp(DragonGold, DragonRed, saturate((t - 0.5) / 0.5));
    return lerp(c1, c2, step(0.5, t));
}

// ========================================
//  主像素着色
// ========================================
float4 SkyPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.001)
        return float4(0, 0, 0, 0);

    // 保持宽高比的UV(用于云气和星辰)
    float2 aspectUV = float2(uv.x * uAspect, uv.y);

    float t = uTime;
    float phase = saturate(uPhase);

    // ==========================================
    //  基础渐变 — 三段色板 (顶/中/地平线)
    // ==========================================
    float3 topCol = SamplePalette_Top(phase);
    float3 midCol = SamplePalette_Mid(phase);
    float3 botCol = SamplePalette_Bot(phase);

    // uv.y=0为顶部, 1为底部
    float upperBlend = smoothstep(0.0, 0.55, uv.y);
    float lowerBlend = smoothstep(0.45, 1.0, uv.y);
    float3 skyCol = lerp(topCol, midCol, upperBlend);
    skyCol = lerp(skyCol, botCol, lowerBlend);

    // ==========================================
    //  星辰层 — 仅在上半部可见, 受云气遮蔽
    // ==========================================
    float2 starUV = aspectUV * 35.0 + float2(t * 0.01, t * 0.005);
    float stars = starField(starUV);
    // 闪烁
    float starSeed = hash21(floor(starUV));
    float twinkle = 0.6 + 0.4 * sin(t * 2.0 + starSeed * 20.0);
    stars *= twinkle;
    // 只在上半部
    stars *= smoothstep(0.65, 0.15, uv.y);
    // 阶段3时星辰被血光吞没
    stars *= lerp(1.0, 0.25, saturate((phase - 0.5) / 0.5));

    // ==========================================
    //  深层背景云海 — 大尺度缓慢漂移
    // ==========================================
    float2 bgCloudUV = aspectUV * 1.4 + float2(t * 0.015, -t * 0.008);
    float bgCloud = warpedFbm(bgCloudUV, t * 0.5);
    bgCloud = smoothstep(0.35, 0.75, bgCloud);
    float3 bgCloudCol = lerp(midCol * 1.2, CloudLight * 0.8, 0.4);
    bgCloudCol = lerp(bgCloudCol, SampleAccent(phase) * 0.6, phase * 0.4);

    // ==========================================
    //  中层流云 — 域扭曲龙息
    // ==========================================
    float speedScale = 1.0 + phase * 1.5;
    float2 midCloudUV = aspectUV * 2.6 + float2(t * 0.04 * speedScale, t * 0.02);
    float midCloudN = warpedFbm(midCloudUV, t * speedScale);
    float midCloud = smoothstep(0.40, 0.70, midCloudN);
    float3 midCloudCol = lerp(CloudLight, SampleAccent(phase), 0.3 + phase * 0.4);

    // ==========================================
    //  前景龙息薄雾 — 更快流动, 叠在底部
    // ==========================================
    float2 fgCloudUV = aspectUV * 4.0 + float2(t * 0.09 * speedScale, -t * 0.04);
    float fgCloudN = warpedFbm(fgCloudUV, t * 1.2 * speedScale);
    float fgCloud = smoothstep(0.50, 0.80, fgCloudN);
    fgCloud *= smoothstep(0.2, 0.8, uv.y); // 仅下半显著
    float3 fgCloudCol = SampleAccent(phase);

    // ==========================================
    //  极坐标龙鳞光轮 — 以Boss为中心的放射
    // ==========================================
    float2 rel = uv - uBossUV;
    rel.x *= uAspect;
    float dist = length(rel);
    float ang = atan2(rel.y, rel.x);

    // 八瓣光轮(八龙)
    float petals = 8.0;
    float halo = pow(abs(cos(ang * petals * 0.5 + t * 0.4)), 6.0);
    halo *= exp(-dist * 1.6);
    halo *= (0.6 + 0.4 * sin(uPulse));

    // 龙鳞环纹: 同心波
    float ring = 0.5 + 0.5 * sin(dist * 28.0 - t * 1.4);
    ring = pow(ring, 8.0);
    ring *= exp(-dist * 2.5);

    float3 dragonAura = SampleAccent(phase);
    float haloStrength = (halo * 0.8 + ring * 0.5) * uIntensity * (0.5 + phase * 1.0);

    // ==========================================
    //  合成颜色
    // ==========================================
    float3 col = skyCol;
    col += stars * float3(0.95, 0.98, 1.05) * 0.9;
    col = lerp(col, bgCloudCol, bgCloud * 0.55);
    col = lerp(col, midCloudCol, midCloud * 0.75);
    col = lerp(col, fgCloudCol, fgCloud * 0.45);
    col += dragonAura * haloStrength;

    // ==========================================
    //  顶部残留光辉(天门)
    // ==========================================
    float gate = smoothstep(0.35, 0.0, uv.y);
    gate *= 0.5 + 0.5 * sin(t * 0.5);
    col += SampleAccent(phase) * gate * 0.15 * uIntensity;

    // ==========================================
    //  整体暗角 — 聚焦视线
    // ==========================================
    float2 vc = uv - 0.5;
    vc.x *= uAspect;
    float vignette = 1.0 - saturate(dot(vc, vc) * 0.9);
    vignette = lerp(0.6, 1.0, vignette);
    col *= vignette;

    // 阶段感色相微调: 暴怒时提亮高光
    col += (phase > 0.7 ? (phase - 0.7) * 0.4 * DragonRed * midCloud : 0.0);

    float alpha = uIntensity;
    return float4(saturate(col) * alpha, alpha);
}

technique AncestralSky
{
    pass P0
    {
        PixelShader = compile ps_3_0 SkyPS();
    }
}
