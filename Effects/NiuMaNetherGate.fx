// ============================================================
// 鬼门法阵 — 牛头马面专属屏幕空间 SDF 印记
// 阴阳双半环反向旋转 (熔红/幽紫) + 噪声符刻 + 太极旋涡内域
// + 开门亮缝 (uOpen): 入场鬼门 / 链锤落点 / 复生反制圈 / 锁命中枢共用
// 载体: 全屏绘制共享噪声 (s0); 世界定位经 ACMShaders.WorldDecalParams
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;       // 动画时间(秒)
float2 uCenter;     // 中心归一化屏幕坐标 0~1
float  uRadius;     // 半径 (屏幕高度比例)
float  uIntensity;  // 整体强度 0~1
float  uAspect;     // 宽高比 width/height
float4 uColorA;     // 阳半环色 (牛头熔红)
float4 uColorB;     // 阴半环色 (马面幽紫)
float  uOpen;       // 0~1 开门度: 中缝亮起 + 内域涌光
float  uSpin;       // 附加整体旋转 (rad)

float4 NiuMaNetherGatePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);
    float  nd     = dist / max(uRadius, 0.001);

    // 大片外域早退
    if (nd > 1.55)
        return float4(0, 0, 0, 0);

    float angle = atan2(diff.y, diff.x) + uSpin;

    // 多八度噪声扰动
    float n1 = tex2D(uImage0, coords * 3.0 + float2(uTime * 0.05, -uTime * 0.04)).r;
    float n2 = tex2D(uImage0, coords * 6.0 - float2(uTime * 0.06, 0)).g;
    float fbm = n1 * 0.65 + n2 * 0.35;
    float dN = nd + (fbm - 0.5) * 0.10;

    // —— 主环: 阴阳双半环, 各自反向旋转 ——
    float th = 0.055;
    float ring = smoothstep(th * 2.2, th * 0.4, abs(dN - 1.0));

    float spinA = angle - uTime * 0.9;  // 阳半环顺行
    float spinB = angle + uTime * 0.7;  // 阴半环逆行
    float halfA = smoothstep(-0.25, 0.25, sin(spinA));
    float halfB = 1.0 - halfA;

    // 符刻: 沿环角向噪声刻痕 (两半流动方向相反)
    float runeA = tex2D(uImage0, float2(spinA * 1.59155 + uTime * 0.03, 0.25)).r;
    float runeB = tex2D(uImage0, float2(spinB * 1.59155 - uTime * 0.03, 0.75)).b;
    float runes = smoothstep(0.52, 0.82, lerp(runeB, runeA, halfA));
    float ringShape = ring * (0.55 + 0.45 * runes);

    // —— 内对环 (细弱) ——
    float ring2 = smoothstep(0.035, 0.008, abs(dN - 0.62)) * 0.6;

    // —— 太极旋涡内域 (随 uOpen 涌光) ——
    float swirlAng = angle + nd * 4.0 - uTime * 1.4;
    float swirl = 0.5 + 0.5 * sin(swirlAng * 2.0);
    swirl = pow(swirl, 3.0) * smoothstep(1.0, 0.15, nd) * (0.12 + 0.55 * uOpen);

    // —— 开门亮缝: 竖向细缝, uOpen 拉宽变亮 ——
    float slitW = (0.02 + 0.16 * uOpen) * uRadius;
    float slit = smoothstep(slitW, slitW * 0.15, abs(diff.x)) * smoothstep(1.05, 0.75, nd);
    slit *= (0.25 + 0.75 * uOpen);

    // —— 外辉 ——
    float glow = smoothstep(1.45, 1.0, nd) * smoothstep(0.75, 1.0, nd) * 0.18;

    float3 colRing = lerp(uColorB.rgb, uColorA.rgb, halfA);
    float3 col = colRing * (ringShape + glow)
               + lerp(uColorA.rgb, uColorB.rgb, 0.5) * ring2
               + lerp(uColorB.rgb, float3(1.0, 1.0, 1.0), 0.35) * swirl
               + lerp(uColorA.rgb, float3(1.0, 0.95, 0.9), 0.6 * uOpen) * slit;

    float pulse = 0.92 + 0.08 * sin(uTime * 3.1 + nd * 6.0);
    float alpha = saturate(ringShape + ring2 + swirl + slit + glow) * uIntensity;
    return float4(col * pulse, alpha);
}

technique Technique1
{
    pass NiuMaNetherGatePass
    {
        PixelShader = compile ps_3_0 NiuMaNetherGatePS();
    }
}
