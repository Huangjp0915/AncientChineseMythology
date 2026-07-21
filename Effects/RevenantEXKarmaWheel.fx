// ============================================================
// 亡魂EX·业焰轮 — 屏幕空间 decal (不占全屏名额)
// 旋转辐条业火法轮: 外缘火舌环 + 旋转辐条 + 内圈符文带 + 中心业核
// 用于: 无间劫刃驻场大招 / 业劫觉醒时玩家身周业焰环
// 喂共享可平铺噪声 (s0); 建议 Additive 绘制 (DrawScreenSpaceDecalStandalone)
// ============================================================

sampler uNoise : register(s0); // 可平铺三通道噪声

float  uTime;            // 动画时间(秒)
float2 uCenter;          // 中心归一化屏幕坐标 0~1
float  uRadius;          // 半径 (屏幕高度比例)
float  uIntensity;       // 整体强度 0~1
float  uAspect;          // 宽高比 width/height
float4 uColorPrimary;    // 业焰亮色 (橙金/魂火)
float4 uColorSecondary;  // 业障暗色 (暗冥紫)
float  uSpin;            // 自旋相位 (弧度, 调用方随时间推进)
float  uSpokes;          // 辐条数 (建议 6~10)

float4 KarmaWheelPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);
    float  normDist = dist / max(uRadius, 0.001);

    // 轮外大片早退
    if (normDist > 1.5)
        return float4(0, 0, 0, 0);

    float angle   = atan2(diff.y, diff.x) + uSpin;
    float angNorm = angle / 6.28318 + 0.5;

    // —— 多八度噪声 (角向火舌 + 径向流动) ——
    float2 n1UV = float2(angNorm * 3.0 + uTime * 0.05, normDist * 1.6 - uTime * 0.24);
    float2 n2UV = float2(angNorm * 6.0 - uTime * 0.08, normDist * 2.8 - uTime * 0.30);
    float2 n3UV = coords * 2.4 + float2(uTime * 0.03, -uTime * 0.05);
    float n1 = tex2D(uNoise, n1UV).r;
    float n2 = tex2D(uNoise, n2UV).g;
    float n3 = tex2D(uNoise, n3UV).b;
    float fbm = n1 * 0.55 + n2 * 0.30 + n3 * 0.15;

    // —— 外缘火舌环: 噪声把环半径向外撕出火舌 ——
    float flameWarp = (fbm - 0.5) * 0.30;
    float dRim = normDist + flameWarp;
    float rim = smoothstep(0.78, 0.97, dRim) * (1.0 - smoothstep(1.02, 1.30, dRim));
    // 火舌尖端更亮
    float tongue = smoothstep(0.55, 0.85, n1) * smoothstep(1.32, 0.95, dRim) * smoothstep(0.85, 1.02, dRim);

    // —— 旋转辐条 (刀轮感, 角向随半径微螺旋) ——
    float spokeAngle = angle * max(uSpokes, 1.0) * 0.5 + normDist * 1.8;
    float spokes = pow(abs(cos(spokeAngle)), 9.0);
    spokes *= smoothstep(0.20, 0.42, normDist) * (1.0 - smoothstep(0.85, 1.02, normDist));

    // —— 内圈符文带 (反向旋转的噪声带) ——
    float2 runeUV = float2(angNorm * 8.0 - uTime * 0.10 - uSpin * 0.5, normDist * 5.0);
    float rune = smoothstep(0.52, 0.82, tex2D(uNoise, runeUV).r);
    rune *= smoothstep(0.30, 0.40, normDist) * (1.0 - smoothstep(0.52, 0.62, normDist));

    // —— 中心业核 (呼吸小辉光) ——
    float corePulse = 0.85 + 0.15 * sin(uTime * 5.0);
    float core = (1.0 - smoothstep(0.0, 0.24, normDist)) * corePulse * 0.8;

    float shape = max(max(rim, tongue * 0.9), max(spokes * 0.75, max(rune * 0.65, core)));

    // 亮部 (火舌芯/辐条尖) 偏主色, 弥漫处偏暗色
    float hot = saturate(tongue + spokes * 0.6 + core);
    float3 col = lerp(uColorSecondary.rgb, uColorPrimary.rgb, saturate(hot + fbm * 0.35));
    // 火舌芯泛白过曝
    col += float3(0.28, 0.22, 0.12) * pow(tongue, 2.0);

    float pulse = 0.92 + 0.08 * sin(uTime * 3.4 + angle * 2.0);
    float alpha = saturate(shape * pulse * uIntensity);
    return float4(col * alpha, alpha); // 预乘输出, Additive/AlphaBlend 皆可
}

technique Technique1
{
    pass KarmaWheelPass
    {
        PixelShader = compile ps_3_0 KarmaWheelPS();
    }
}
