// ============================================================
// 后卿·疠雾冥月 — 程序化天幕层 (HoqingSky 专用)
// 双层差速 fbm 疠雾(下浓上稀) + 冥月(病绿核/光晕/月面暗斑)
// uMoonBlood: 幕三"冥月渗血" — 月染红 + 外血环
// uFlash: 死亡演出天空白闪
// 载体: 以共享可平铺噪声满屏绘制 (s0), AlphaBlend 预乘输出
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 整体强度 0~1
float  uAspect;      // 宽高比 width/height
float2 uMoonPos;     // 冥月中心 归一化屏幕UV
float  uMoonRadius;  // 冥月半径 (屏幕高度比例)
float  uMoonBlood;   // 0~1 渗血程度 (幕三)
float  uFlash;       // 0~1 白闪 (死亡爆点)
float4 uColorMistA;  // 疠雾深色 (腐暗绿)
float4 uColorMistB;  // 疠雾浅色 (病黄绿)
float4 uColorMoon;   // 月光色 (尸绿)

float4 PlagueMiasmaPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.005)
        return float4(0, 0, 0, 0);

    // —— 疠雾: 双层差速流动 ——
    float2 m1 = coords * float2(2.6, 1.9) + float2(uTime * 0.012, -uTime * 0.004);
    float2 m2 = coords * float2(4.8, 3.4) + float2(-uTime * 0.020, uTime * 0.008);
    float a1 = tex2D(uImage0, m1).r;
    float a2 = tex2D(uImage0, m2).g;
    float mist = a1 * 0.62 + a2 * 0.38;

    float bottomBias = lerp(0.30, 1.0, coords.y); // 下浓上稀
    float mistAlpha = smoothstep(0.30, 0.85, mist) * bottomBias;
    float3 mistCol = lerp(uColorMistA.rgb, uColorMistB.rgb, smoothstep(0.40, 0.78, mist));

    // —— 冥月 ——
    float2 pos = float2(coords.x * uAspect, coords.y);
    float2 mc  = float2(uMoonPos.x * uAspect, uMoonPos.y);
    float md = length(pos - mc) / max(uMoonRadius, 0.001);

    float moonCore = 1.0 - smoothstep(0.72, 1.0, md);
    // 月面暗斑 (低频噪声)
    float blot = tex2D(uImage0, (pos - mc) * (0.55 / max(uMoonRadius, 0.001)) * 0.35 + 7.7).b;
    moonCore *= lerp(0.72, 1.05, blot);

    float halo = pow(saturate(1.0 - md * 0.40), 3.2) * 0.55;

    // 渗血: 月体染红 + 外缘血环
    float3 moonCol = lerp(uColorMoon.rgb, float3(0.85, 0.32, 0.28), saturate(uMoonBlood) * 0.5);
    float ring = smoothstep(1.02, 1.10, md) * (1.0 - smoothstep(1.12, 1.34, md));
    ring *= 0.75 + 0.25 * sin(uTime * 2.4 + md * 20.0);
    float bloodA = ring * saturate(uMoonBlood);
    float3 bloodCol = float3(0.78, 0.10, 0.14);

    // —— 合成 ——
    float3 col = mistCol * mistAlpha * 0.60
               + moonCol * (moonCore * 0.95 + halo * 0.65)
               + bloodCol * bloodA;
    float alpha = saturate(mistAlpha * 0.50 + moonCore * 0.9 + halo * 0.55 + bloodA);

    // 白闪
    col = lerp(col, float3(1.0, 1.0, 1.0), saturate(uFlash) * 0.9);
    alpha = saturate(alpha + saturate(uFlash) * 0.8);

    alpha *= uIntensity;
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass PlagueMiasmaPass
    {
        PixelShader = compile ps_3_0 PlagueMiasmaPS();
    }
}
