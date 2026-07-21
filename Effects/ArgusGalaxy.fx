// ============================================================
// Argus 旋涡星系球 — SpinningGalacticOrbs 单批绘制
// 对数螺旋双臂 + 星点闪烁 + 核心辉光 + 吸积缘环
// 顶点色: rgb = 色调(紫/蓝交替), a = 成形进度兼主透明度
// quad 旋转提供每球相位差; Additive 绘制
// ============================================================

sampler uTexture : register(s0); // SoftGlow 径向底

float uTime; // 动画时间(秒)

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

float4 GalaxyPS(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (sampleColor.a < 0.01)
        return float4(0, 0, 0, 0);

    float2 c = uv - 0.5;
    float r = length(c) * 2.2;
    if (r > 1.1)
        return float4(0, 0, 0, 0);

    float ang = atan2(c.y, c.x + 0.0001);

    // 对数螺旋双臂
    float spiral = ang * 2.0 + log(max(r, 0.03)) * 4.2 - uTime * 2.2;
    float arms = pow(0.5 + 0.5 * cos(spiral), 2.4);
    float disk = saturate(1.0 - r);
    disk *= disk;

    // 臂上星点闪烁
    float2 sUV = c * 16.0;
    float star = pow(valueNoise(sUV), 10.0);
    star *= 0.6 + 0.4 * sin(uTime * 5.0 + hash21(floor(sUV)) * 6.28);

    // 核心辉光 + 吸积缘环
    float core = exp(-r * 5.5);
    float ring = 1.0 - smoothstep(0.0, 0.10, abs(r - 0.82));

    float3 tint = sampleColor.rgb;
    float3 col = tint * arms * disk * 1.1;
    col += lerp(tint, float3(1.0, 1.0, 1.0), 0.8) * core * 1.3;
    col += float3(1.0, 1.0, 1.0) * star * disk * arms * 2.0;
    col += tint * ring * 0.45;

    float glowBase = tex2D(uTexture, uv).r;
    col += tint * glowBase * 0.15;

    float charge = sampleColor.a;
    float alpha = saturate(arms * disk * 0.9 + core + ring * 0.4) * charge;

    return float4(col * alpha * charge, alpha);
}

technique Technique1
{
    pass GalaxyPass
    {
        PixelShader = compile ps_3_0 GalaxyPS();
    }
}
