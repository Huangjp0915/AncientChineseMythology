// ============================================================
// 阴天子·鬼门着色器 — 竖直门洞 + 深渊漩涡 + 金符门缘
// uOpen 0->1 开阖: 0=细缝亮线, 1=全开门洞
// 喂可平铺噪声(s0); quad UV 全幅, 门体居中竖直
// ============================================================

sampler uNoise : register(s0); // 可平铺噪声

float  uTime;            // 动画时间(秒)
float  uOpen;            // 开阖进度 0~1
float  uIntensity;       // 整体可见度 0~1
float4 uColorPrimary;    // 深渊主色(冥紫黑)
float4 uColorSecondary;  // 门缘符文色(帝金)
float  uSeed;            // 每扇门相位种子

float4 GatePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 c = coords - 0.5;

    // —— 门洞椭圆 SDF: 开阖只影响横向半宽 ——
    float halfH = 0.44;
    float halfW = max(uOpen * 0.30, 0.004);
    float d = length(float2(c.x / halfW, c.y / halfH));

    // 完全在门外(含符环余量)则丢弃
    if (d > 1.55)
        return float4(0, 0, 0, 0);

    // —— 内部深渊漩涡: 极坐标卷动噪声, 向心吸入 ——
    float angle = atan2(c.y, c.x);
    float angN = angle / 6.28318 + 0.5;
    float2 swirlUV = float2(angN * 2.0 + d * 1.4 - uTime * 0.22 + uSeed,
                            d * 0.9 - uTime * 0.38);
    float n1 = tex2D(uNoise, swirlUV).r;
    float n2 = tex2D(uNoise, swirlUV * 2.3 + 0.37).b;
    float swirl = n1 * 0.65 + n2 * 0.35;

    float inside = smoothstep(1.0, 0.92, d);

    // 深渊底色: 越接近中心越黑(深不见底)
    float3 abyss = uColorPrimary.rgb * (0.20 + d * 0.5);
    // 魂流条纹: 高亮丝缕被吸向中心
    float streak = pow(abs(swirl), 4.0);
    abyss += float3(0.32, 0.75, 0.85) * streak * (0.35 + d * 0.5);
    // 中心幽芒微光
    abyss += uColorPrimary.rgb * smoothstep(0.5, 0.0, d) * 0.35;

    // —— 门缘: 金色轮辉 ——
    float rim = smoothstep(1.10, 0.99, d) * smoothstep(0.90, 0.985, d);
    // 门缘外圈符环: 角向字符块闪烁
    float glyph = tex2D(uNoise, float2(angN * 6.0 + uSeed, 0.21 + uTime * 0.02)).g;
    float runeRing = step(0.58, glyph)
                   * smoothstep(1.34, 1.12, d) * smoothstep(1.04, 1.13, d);
    float runeBlink = 0.6 + 0.4 * sin(uTime * 3.1 + angle * 2.0 + uSeed * 3.0);

    // —— 初开细缝: 未开时是一道亮缝 ——
    float slit = smoothstep(0.020, 0.0, abs(c.x)) * smoothstep(0.46, 0.40, abs(c.y));
    float slitGlow = slit * saturate(1.0 - uOpen * 1.6);

    float3 col = abyss * inside;
    col += uColorSecondary.rgb * rim * (1.1 + 0.25 * sin(uTime * 4.0 + uSeed));
    col += uColorSecondary.rgb * runeRing * runeBlink * 0.8;
    col += float3(1.0, 0.95, 0.8) * slitGlow * 1.6;

    float alpha = saturate(inside * 0.92 + rim * 0.9 + runeRing * 0.6 + slitGlow) * uIntensity;
    return float4(col * uIntensity, alpha);
}

technique Technique1
{
    pass GatePass
    {
        PixelShader = compile ps_3_0 GatePS();
    }
}
