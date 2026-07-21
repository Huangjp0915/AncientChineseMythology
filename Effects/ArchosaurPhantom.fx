// ============================================================
// 祖龙残魂 · 幻影显形 — 分身头部贴图着色 (SpriteBatch Immediate 单 pass)
// 去饱和灰蓝化 + 扫描线静电撕裂 + 边缘噪声溶解(出生/吸收) + 透明脉动
// s0 = NPC 贴图 (tML 预乘), s1 = 共享可平铺噪声; 输出预乘 Alpha
// ============================================================

sampler uImage0 : register(s0); // NPC 贴图 (预乘)
sampler uNoise  : register(s1); // 可平铺噪声

float  uTime;      // 动画时间(秒)
float  uSeed;      // 实例种子
float  uDissolve;  // 溶解 0=完全实体 1=完全消散 (出生 1→0, 吸收 0→1)
float  uGlitch;    // 扫描线撕裂强度 0~1
float  uOpacity;   // 整体透明 0~1
float4 uTint;      // 幻影主色 (rgb)

float4 PhantomPS(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    // —— 扫描线撕裂: uv.y 量化 12 条带, 少数带随机横移 (静电噪点) ——
    float band = floor(uv.y * 12.0);
    float gn = tex2D(uNoise, float2(band * 0.093 + floor(uTime * 22.0) * 0.117 + uSeed, 0.35)).r;
    float shift = (gn - 0.5) * 0.10 * uGlitch * step(0.60, gn);
    float2 suv = saturate(uv + float2(shift, 0.0));

    float4 tex = tex2D(uImage0, suv);

    // —— 灰化 → 幻影灰蓝 (输入为预乘, 灰度自带 alpha 权重) ——
    float grey = dot(tex.rgb, float3(0.35, 0.50, 0.15));
    float3 ghost = uTint.rgb * (grey * 1.35 + 0.10 * tex.a);
    float3 col = lerp(tex.rgb, ghost, 0.80);

    // —— 边缘噪声溶解 + 溶解沿亮边 ——
    float dn = tex2D(uNoise, uv * 1.9 + float2(uSeed, uSeed * 1.7)).g;
    float keep = smoothstep(uDissolve - 0.05, uDissolve + 0.07, dn);
    float rim  = smoothstep(uDissolve - 0.22, uDissolve - 0.02, dn) * (1.0 - keep) * tex.a;

    // —— 透明脉动 (魂体呼吸) ——
    float pulse = 0.86 + 0.14 * sin(uTime * 9.0 + uSeed * 20.0);

    float mul = keep * uOpacity * pulse;
    float alpha = saturate(tex.a * mul + rim * 0.55);
    float3 outCol = col * mul + (uTint.rgb * 1.3 + float3(0.35, 0.45, 0.60)) * rim * 0.85;

    return float4(saturate(outCol) * sampleColor.rgb, alpha * sampleColor.a);
}

technique Technique1
{
    pass PhantomPass
    {
        PixelShader = compile ps_3_0 PhantomPS();
    }
}
