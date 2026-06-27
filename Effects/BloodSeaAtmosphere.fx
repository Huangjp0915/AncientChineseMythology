// ============================================================
// 地下血海盆地氛围着色器 — 全屏程序化叠加
// 血色渐变体积雾 + 焦散涟漪 + 向下血光体积光 + 漂浮血粒 + 暗角
// 完全程序化, 无外部噪声贴图依赖
// 以预乘 Alpha (BlendState.AlphaBlend) 叠加: rgb 为预乘色, a 为覆盖度
//   result = premul + dst * (1 - a)
//   暗色染色经 (1-a) 压暗背景并叠加血色, 亮部(体积光/焦散/血粒)以加法发光
// ============================================================

sampler uImage0 : register(s0); // 占位(满屏白像素), 不做采样

float  uTime;        // 累计时间(秒)
float  uIntensity;   // 整体强度 0~1 (进出盆地淡入淡出)
float2 uResolution;  // 屏幕分辨率(像素)
float2 uScreenPos;   // 摄像机世界坐标(像素, 已取模) 用于雾气世界锚定视差
float  uSubmerged;   // 玩家是否浸没于血水 0~1 (增强焦散)

// ========================================
//  程序化噪声
// ========================================
float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
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
float fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 5; i++)
    {
        v += valueNoise(p) * a;
        p = p * 2.02 + float2(1.3, 2.7);
        a *= 0.5;
    }
    return v;
}

// ========================================
//  主像素着色
// ========================================
float4 BloodAtmoPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.001)
        return float4(0, 0, 0, 0);

    float aspect = uResolution.x / max(uResolution.y, 1.0);
    float2 auv = float2(uv.x * aspect, uv.y);           // 保持宽高比的UV
    float2 wOff = uScreenPos / max(uResolution.y, 1.0); // 世界锚定偏移(视差)
    float t = uTime;

    // uv.y: 0=顶部 1=底部
    float depth = saturate(uv.y * 1.1);

    // ==========================================
    //  基础血色渐变 — 越靠近湖底越深越浓
    // ==========================================
    float3 tintTop = float3(0.26, 0.02, 0.04);
    float3 tintBot = float3(0.52, 0.03, 0.05);
    float3 baseTint = lerp(tintTop, tintBot, depth);

    // ==========================================
    //  漂浮血雾 — 双层域漂移 fbm
    // ==========================================
    float2 fuvA = (auv + wOff * 0.6) * 2.2 + float2(t * 0.020, -t * 0.010);
    float2 fuvB = (auv + wOff * 0.9) * 3.7 + float2(-t * 0.035, t * 0.018);
    float fog = saturate(fbm(fuvA) * 0.6 + fbm(fuvB) * 0.5);
    fog = smoothstep(0.33, 0.95, fog);
    fog *= lerp(0.5, 1.15, depth);                      // 低处更浓
    float3 fogCol = lerp(float3(0.34, 0.02, 0.04), float3(0.68, 0.06, 0.07), depth);

    // ==========================================
    //  焦散涟漪 — 浸没时及湖底更强
    // ==========================================
    float2 cuv = (auv + wOff * 0.5) * 6.0;
    float c1 = sin(cuv.x * 1.7 + t * 1.3) + sin(cuv.y * 2.1 - t * 1.1);
    float c2 = sin((cuv.x + cuv.y) * 1.3 + t * 0.9) + sin((cuv.x - cuv.y) * 1.9 - t * 1.4);
    float caustic = pow(saturate((c1 + c2) * 0.25 + 0.5), 3.0);
    caustic *= lerp(0.12, 1.0, uSubmerged) * lerp(0.25, 1.1, depth);
    float3 causCol = float3(1.0, 0.30, 0.22);

    // ==========================================
    //  向下血光体积光 — 自顶部渗下的竖向光束
    // ==========================================
    float rayN = fbm(float2(auv.x * 3.0 + wOff.x * 0.8 + t * 0.05, t * 0.02));
    float rays = pow(saturate(rayN), 2.0);
    rays *= smoothstep(0.95, 0.0, uv.y);                // 顶部强, 向下衰减
    rays *= 0.6 + 0.4 * sin(t * 0.7 + auv.x * 5.0);
    float3 rayCol = float3(0.9, 0.18, 0.16);

    // ==========================================
    //  漂浮血粒 — 缓慢上升的微光点
    // ==========================================
    float motes = 0.0;
    [unroll] for (int m = 0; m < 3; m++)
    {
        float fm = (float)m;
        float2 g = float2(auv.x * 8.0 + fm * 13.1,
                          auv.y * 8.0 - t * (0.25 + 0.08 * fm) + fm * 7.3) + wOff * 0.7;
        float2 ci = floor(g);
        float2 cf = frac(g) - 0.5;
        float h = hash21(ci + fm * 19.0);
        motes += step(0.90, h) * exp(-dot(cf, cf) * 22.0);
    }
    motes = saturate(motes);
    float3 moteCol = float3(1.0, 0.5, 0.4);

    // ==========================================
    //  暗角 — 聚焦视线, 边缘压暗
    // ==========================================
    float2 vc = uv - 0.5;
    vc.x *= aspect;
    float vig = 1.0 - saturate(dot(vc, vc) * 1.15);
    vig = lerp(0.35, 1.0, vig);

    // ==========================================
    //  合成(预乘 Alpha)
    // ==========================================
    float coverage = saturate(0.30 + fog * 0.45);

    float3 premul = (baseTint * 0.7 + fogCol * fog) * coverage; // 染色(随覆盖)
    premul += rays * rayCol * 0.35;                             // 加法: 体积光
    premul += caustic * causCol * 0.30;                         // 加法: 焦散
    premul += motes * moteCol * 0.60;                           // 加法: 血粒
    premul *= vig;                                              // 整体暗角

    premul *= uIntensity;
    float a = saturate(coverage * uIntensity);

    return float4(max(premul, 0.0), a);
}

technique BloodSeaAtmo
{
    pass P0
    {
        PixelShader = compile ps_3_0 BloodAtmoPS();
    }
}
