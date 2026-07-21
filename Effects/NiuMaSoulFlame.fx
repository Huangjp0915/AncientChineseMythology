// ============================================================
// 魂火描边 — 牛头马面本体着色 (s0=NPC 贴图, s1=可平铺噪声)
// 轮廓检出 + 体外上涌魂焰 (贴图空间向下探体) + 受击/演出闪白 + 蓄力增辉
// 贴图为 tML 预乘 Alpha; 输出保持预乘约定, 供 AlphaBlend 批直接使用
// ============================================================

sampler uImage0 : register(s0); // NPC 贴图 (预乘)
sampler uImage1 : register(s1); // 可平铺噪声

float  uTime;    // 动画时间(秒)
float4 uTint;    // 魂火主色 (牛头熔红 / 马面幽紫)
float4 uTint2;   // 焰心亮色
float  uFlash;   // 0~1 闪白 (受击/演出)
float  uCharge;  // 0~1 蓄力 (焰长与亮度)
float2 uPixel;   // 1/贴图尺寸
float  uAlpha;   // 整体透明度

float4 NiuMaSoulFlamePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 body = tex2D(uImage0, coords);

    // —— 轮廓: 邻域 alpha 梯度 ——
    float aU = tex2D(uImage0, coords + float2(0.0, -uPixel.y * 2.0)).a;
    float aD = tex2D(uImage0, coords + float2(0.0,  uPixel.y * 2.0)).a;
    float aL = tex2D(uImage0, coords + float2(-uPixel.x * 2.0, 0.0)).a;
    float aR = tex2D(uImage0, coords + float2( uPixel.x * 2.0, 0.0)).a;
    float edge = saturate(body.a * 4.0) * saturate(1.0 - min(min(aU, aD), min(aL, aR)) * 1.2);

    // —— 体外焰域: 向下探体 (焰在贴图空间向上涌) ——
    float flameLen = 6.0 + 10.0 * uCharge;
    float reach = 0.0;
    [unroll]
    for (int i = 1; i <= 8; i++)
    {
        float d = i / 8.0;
        float a2 = tex2D(uImage0, coords + float2(0.0, uPixel.y * flameLen * d)).a;
        reach = max(reach, a2 * (1.0 - d));
    }
    float outside = (1.0 - saturate(body.a * 2.0)) * reach;

    // —— 火焰噪声: 双层上涌流动 ——
    float n1 = tex2D(uImage1, coords * 2.6 + float2(0.0, -uTime * 0.9)).r;
    float n2 = tex2D(uImage1, coords * 5.0 + float2(uTime * 0.15, -uTime * 1.4)).g;
    float flameNoise = saturate(n1 * 0.6 + n2 * 0.55);

    float flame = saturate(edge * 0.9 + outside * 1.4) * flameNoise * (0.6 + 0.9 * uCharge);
    float3 flameCol = lerp(uTint.rgb, uTint2.rgb, saturate(flame * 1.6 - 0.3));

    // 预乘输出: 身体(闪白) + 魂焰加色
    float3 col = body.rgb * (1.0 - uFlash * 0.85) + body.a * uFlash * 0.85;
    col += flameCol * flame * 1.5;
    float alpha = saturate(body.a + flame * 0.75);

    return float4(col * sampleColor.rgb, alpha * sampleColor.a) * uAlpha;
}

technique Technique1
{
    pass NiuMaSoulFlamePass
    {
        PixelShader = compile ps_3_0 NiuMaSoulFlamePS();
    }
}
