// ============================================================
// CelestialDragonPearl.fx — 天御金龙·龙珠 (程序化球体)
// 画在 SoftGlow 等全幅 quad 上 (uv 0~1), Immediate + Additive
// fresnel 边缘光 + 内部旋涡噪声 (随充能加速) + 白热化 + 塌缩颤闪
// ============================================================

sampler uTexture : register(s0); // 占位 quad 纹理 (仅取 uv, 不采样亦可)
sampler uNoise   : register(s1); // 共享三通道噪声

float  uTime;      // 秒
float  uIntensity; // 总强度 0~1 (含塌缩颤闪, CPU 端乘好)
float  uCharge;    // 充能 0~1 (驱动内部转速/白热)
float4 uColorCore; // 珠心金白
float4 uColorRim;  // 边缘暖金

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float2 texCoord : TEXCOORD0;
};

float4 PS_Pearl(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 d = (input.texCoord.xy - 0.5) * 2.0;
    float r = length(d);
    if (r > 1.0)
        return float4(0, 0, 0, 0);

    // 内部旋涡: 极坐标噪声, 充能越高转得越快
    float ang = atan2(d.y, d.x) / 6.2831853 + 0.5;
    float spin = uTime * (0.10 + uCharge * 0.55);
    float swirl = tex2D(uNoise, float2(ang + spin, r * 0.8 - uTime * (0.08 + uCharge * 0.5))).r;
    float swirl2 = tex2D(uNoise, float2(ang * 2.0 - spin * 0.7, r * 1.6 + 0.31)).g;
    swirl = swirl * 0.7 + swirl2 * 0.5;

    // 球体明暗: 核心亮 + fresnel 边缘环
    float core = 1.0 - smoothstep(0.0, 0.78, r);
    float rim = smoothstep(0.55, 0.95, r) * (1.0 - smoothstep(0.95, 1.0, r));

    float heat = uCharge * uCharge;
    float3 col = lerp(uColorRim.rgb, uColorCore.rgb, core);
    col += uColorCore.rgb * swirl * (0.35 + heat * 0.85);
    col += float3(1.0, 1.0, 0.95) * rim * (0.55 + heat * 0.9);
    col = lerp(col, float3(1.0, 0.99, 0.94), heat * core * 0.7); // 满充白热

    float alpha = saturate(core + rim * 0.9 + swirl * 0.25 * (1.0 - r));
    alpha *= 1.0 - smoothstep(0.92, 1.0, r); // 球缘裁切
    alpha *= uIntensity;

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass PearlPass
    {
        PixelShader = compile ps_3_0 PS_Pearl();
    }
}
