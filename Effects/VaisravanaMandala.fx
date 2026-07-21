// ============================================================
// 毗沙门天 · 佛纹坛城法阵 — 屏幕空间 SDF decal (s0=共享噪声)
// 层次(内→外): 中心光核 → 法轮八辐+轮毂 → 内梵纹环 → 莲瓣圈 → 外双环+符点
// uReveal: 0~1 由内向外逐圈点亮(蓄力语法); uSpin 基础相位
// 经 ACMShaders.DrawScreenSpaceDecalStandalone 满屏绘制 (AlphaBlend, 预乘输出)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;            // 动画时间(秒)
float2 uCenter;          // 中心归一化屏幕坐标 0~1
float  uRadius;          // 半径 (屏幕高度比例)
float  uIntensity;       // 整体强度 0~1
float  uAspect;          // 宽高比 width/height
float4 uColorPrimary;    // 琉璃金(内)
float4 uColorSecondary;  // 暖橙金(外)
float  uReveal;          // 0~1 逐圈点亮进度
float  uSpin;            // 基础旋转相位(弧度)

// 圆环带: 中心 center 半宽 halfWidth 软边 soft
float RingBand(float d, float center, float halfWidth, float soft)
{
    return smoothstep(center - halfWidth - soft, center - halfWidth, d)
         * (1.0 - smoothstep(center + halfWidth, center + halfWidth + soft, d));
}

float4 VaisravanaMandalaPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);

    float breath   = 1.0 + sin(uTime * 1.7) * 0.008;
    float normDist = dist / max(uRadius * breath, 0.001);

    if (normDist > 1.30)
        return float4(0, 0, 0, 0);

    float angle = atan2(diff.y, diff.x);

    // 多八度噪声 (梵纹随机性来源 + 边缘扰动)
    float n1  = tex2D(uImage0, coords * 3.0 + float2(uTime * 0.03, -uTime * 0.02)).r;
    float n2  = tex2D(uImage0, coords * 6.0 + float2(-uTime * 0.04, uTime * 0.03)).g;
    float fbm = n1 * 0.6 + n2 * 0.4;
    float dN  = normDist + (fbm - 0.5) * 0.045;

    // —— 逐圈点亮门 (由内向外) ——
    float gCore  = smoothstep(0.02, 0.14, uReveal);
    float gWheel = smoothstep(0.18, 0.34, uReveal);
    float gRune  = smoothstep(0.36, 0.55, uReveal);
    float gPetal = smoothstep(0.55, 0.76, uReveal);
    float gOuter = smoothstep(0.74, 0.96, uReveal);

    // 1) 中心光核
    float core = pow(saturate(1.0 - normDist / 0.16), 2.2) * gCore;

    // 2) 法轮八辐 (缓旋) + 轮毂环
    float spokes = pow(abs(cos(angle * 4.0 + uSpin + uTime * 0.25)), 24.0);
    spokes *= smoothstep(0.66, 0.50, dN) * smoothstep(0.06, 0.16, dN) * gWheel;
    float hub = RingBand(dN, 0.30, 0.012, 0.02) * gWheel;

    // 3) 内梵纹环 (反向旋转的噪声符纹带)
    float runeUVx = angle / 6.28318 * 10.0 - uTime * 0.05 - uSpin * 0.5;
    float rune = tex2D(uImage0, float2(runeUVx, dN * 5.0)).b;
    rune = smoothstep(0.52, 0.78, rune);
    rune *= RingBand(dN, 0.46, 0.055, 0.03) * gRune;

    // 4) 莲瓣圈 (花瓣尖端半径起伏 + 描边)
    float petalWave = abs(cos(angle * 6.0 - uSpin * 0.7));
    float petalEdge = 0.74 + petalWave * 0.085;
    float petalFill = smoothstep(petalEdge, petalEdge - 0.10, dN)
                    * smoothstep(0.56, 0.64, dN);
    float petalRim  = RingBand(dN, petalEdge, 0.012, 0.018) * 1.4;
    float petals = (petalFill * 0.30 + petalRim) * gPetal;

    // 5) 外双环 + 符点
    float ringA = RingBand(dN, 0.95, 0.014, 0.020);
    float ringB = RingBand(dN, 1.03, 0.008, 0.015);
    float dotUVx = angle / 6.28318 * 24.0 + uSpin;
    float dots = tex2D(uImage0, float2(dotUVx, 0.35)).r;
    dots = smoothstep(0.62, 0.90, dots) * RingBand(dN, 0.99, 0.03, 0.02) * 0.8;
    float outer = (ringA + ringB * 0.7 + dots) * gOuter;

    float pulse = 0.9 + 0.1 * sin(uTime * 2.4 + normDist * 6.0);
    float shape = (core + spokes * 0.75 + hub + rune * 0.9 + petals + outer) * pulse;

    float3 col = lerp(uColorPrimary.rgb, uColorSecondary.rgb,
                      saturate(normDist * 0.9 + fbm * 0.25 - 0.15));
    col += core * 0.35; // 中心偏白过曝

    float alpha = saturate(shape * uIntensity);
    return float4(col * alpha, alpha); // 预乘输出
}

technique Technique1
{
    pass VaisravanaMandalaPass
    {
        PixelShader = compile ps_3_0 VaisravanaMandalaPS();
    }
}
