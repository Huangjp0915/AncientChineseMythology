// ============================================================
// 元素屏幕染色着色器 — 全屏氛围 overlay
// 程序化体积雾 + 上下渐变 + 暗角, 颜色/强度参数化
// 六龙元素底色(潮汐/热浪/风暴/金芒/太初) + 地府氛围共用; 传色即可
// 以预乘 Alpha (BlendState.AlphaBlend) 叠加: rgb 预乘, a 覆盖度
// 喂占位白像素(s0, 不采样), 完全程序化
// ============================================================

sampler uImage0 : register(s0); // 占位, 不采样

float  uTime;        // 累计时间(秒)
float  uIntensity;   // 整体强度 0~1
float  uAspect;      // 宽高比 width/height
float4 uTint;        // 主色 (rgb=色, a=基础覆盖度 0~1)
float4 uTint2;       // 次色(地平线/低处), a 未用
float  uVignette;    // 暗角强度 0~1
float  uFogScale;    // 雾密度尺度 (建议 1.5~4)

// 程序化噪声
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
float fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++)
    {
        v += valueNoise(p) * a;
        p = p * 2.02 + float2(1.3, 2.7);
        a *= 0.5;
    }
    return v;
}

float4 ElementalTintPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 auv = float2(uv.x * uAspect, uv.y);
    float t = uTime;

    // 上->下主次色渐变
    float depth = saturate(uv.y);
    float3 baseTint = lerp(uTint.rgb, uTint2.rgb, depth);

    // 双层域漂移雾
    float scale = max(uFogScale, 0.001);
    float2 fuvA = auv * scale + float2(t * 0.02, -t * 0.012);
    float2 fuvB = auv * scale * 1.7 + float2(-t * 0.03, t * 0.018);
    float fog = saturate(fbm(fuvA) * 0.6 + fbm(fuvB) * 0.5);
    fog = smoothstep(0.30, 0.92, fog);

    // 暗角
    float2 vc = uv - 0.5;
    vc.x *= uAspect;
    float vig = 1.0 - saturate(dot(vc, vc) * 1.1);
    vig = lerp(1.0 - uVignette, 1.0, vig);

    float coverage = saturate(uTint.a + fog * 0.35);
    float3 premul = baseTint * coverage;
    premul *= vig;
    premul *= uIntensity;

    float a = saturate(coverage * uIntensity);
    return float4(max(premul, 0.0), a);
}

technique Technique1
{
    pass ElementalTintPass
    {
        PixelShader = compile ps_3_0 ElementalTintPS();
    }
}
