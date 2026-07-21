// ============================================================
// 尸骸·魂火 — 程序化鬼火 (骨白内核 + 鬼绿外焰 + 顶部拉丝 + 闪烁)
// 用于: 头颅眼焰(状态广播: 蓄力变亮/破绽熄灭)、魂灯球本体、手部脱体掌心焰
// 载体 ACMAsset.SoftGlow (径向渐变), Additive 批内绘制
// 完全自包含程序化噪声, 无外部噪声依赖
// ============================================================

sampler uTexture : register(s0); // SoftGlow 径向渐变

float uTime;       // 秒
float uIntensity;  // 0~1 (眼焰亮度即状态进度条)
float uSeed;       // 每实例相位错开
float4 uColorCore; // 内核 (骨白)
float4 uColorEdge; // 外焰 (鬼绿)

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

float4 SoulFlamePS(float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.004)
        return float4(0, 0, 0, 0);

    float2 p = uv - 0.5;

    // 上半部(焰舌方向)被噪声向上拉丝: 采样点向下偏 = 形状向上扯
    float lick = valueNoise(float2(p.x * 9.0 + uSeed * 17.0, p.y * 5.0 - uTime * 2.6));
    float stretch = (p.y < 0.0) ? (lick - 0.5) * 0.55 * saturate(-p.y * 4.0) : 0.0;
    float2 q = float2(p.x * 1.25, p.y + stretch);
    float r = length(q) * 2.0;

    // 径向基底 (SoftGlow) + 扰动半径
    float baseGlow = tex2D(uTexture, q + 0.5).r;
    float n = valueNoise(float2(q.x * 7.0 + uSeed * 31.0, q.y * 7.0 - uTime * 3.2));
    float body = saturate(baseGlow * (0.75 + 0.5 * n));

    // 内核骨白 → 外焰鬼绿 → 边缘消散
    float core = smoothstep(0.55, 0.0, r);
    float edge = smoothstep(1.05, 0.35, r);

    // 闪烁 (双频叠加, 魂火不安定)
    float flicker = 0.82 + 0.13 * sin(uTime * 12.7 + uSeed * 7.1)
                         + 0.05 * sin(uTime * 29.3 + uSeed * 3.7);

    float3 col = uColorCore.rgb * core * 1.15 + uColorEdge.rgb * (edge - core * 0.55);
    float a = body * edge * uIntensity * flicker;
    return float4(col * a, 0.0); // Additive: 输出预乘色, alpha 置 0
}

technique SoulFlame
{
    pass P0
    {
        PixelShader = compile ps_3_0 SoulFlamePS();
    }
}
