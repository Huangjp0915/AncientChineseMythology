// ============================================================
// 幽冥系列旗舰着色器 — 刑天"无首怒斩"怨气弧形撕裂波
// 屏幕空间 SDF: 沿行进方向张开的弧形波带, 噪声撕裂边 + 双色梯度
// (怨红芯 / 幽蓝外缘) + 前缘白热。由 XingTianWrathWave 弹幕经
// DrawScreenSpaceDecalStandalone 满屏噪声(s0)驱动, 不读 screenTarget,
// 不占全屏后处理名额。
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;        // 动画时间(秒)
float2 uCenter;      // 波心归一化屏幕坐标 0~1 (发波原点, 随弹幕后移)
float  uRadius;      // 当前波前半径 (屏幕高度比例)
float  uIntensity;   // 整体强度 0~1 (生长/消散)
float  uAspect;      // 宽高比 width/height
float  uDirection;   // 行进方向角(弧度, 屏幕空间)
float  uArcWidth;    // 弧半张角(弧度, 建议 0.5~0.9)
float4 uColorCore;   // 芯色 (怨红)
float4 uColorEdge;   // 外缘色 (幽蓝)

float4 UmbralGrudgeWavePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);

    float normDist = dist / max(uRadius, 0.001);
    // 波带只存在于波前附近 (0.55~1.25), 其余大片早退
    if (normDist > 1.35 || normDist < 0.35)
        return float4(0, 0, 0, 0);

    // —— 角向弧形裁剪: 只保留行进方向 ±uArcWidth 的扇区, 弧梢羽化 ——
    float angle = atan2(diff.y, diff.x);
    float dAng  = angle - uDirection;
    // wrap 到 [-pi, pi]
    dAng = dAng - 6.28318 * floor((dAng + 3.14159) / 6.28318);
    float arcMask = 1.0 - smoothstep(uArcWidth * 0.55, uArcWidth, abs(dAng));
    if (arcMask < 0.003)
        return float4(0, 0, 0, 0);

    // —— 多八度噪声撕裂 (角向为主, 径向缓动) ——
    float angNorm = dAng / max(uArcWidth, 0.001); // -1~1 弧内归一
    float2 n1UV = float2(angNorm * 1.6 + uTime * 0.10, normDist * 2.2 - uTime * 0.55);
    float2 n2UV = float2(angNorm * 3.1 - uTime * 0.16, normDist * 4.5 - uTime * 0.9);
    float n1 = tex2D(uImage0, n1UV).r;
    float n2 = tex2D(uImage0, n2UV).g;
    float fbm = n1 * 0.65 + n2 * 0.35;

    // 撕裂扰动波前
    float dN = normDist + (fbm - 0.5) * 0.22;

    // —— 波带剖面: 后缘拖长 (怨气残留), 前缘锐利 ——
    float front = 1.0 - smoothstep(1.0, 1.10, dN);            // 前缘硬切
    float tail  = smoothstep(0.42, 0.95, dN);                 // 后缘长拖
    float band  = front * tail;

    // 撕裂丝: 高频角向细条 (怨气撕开的裂缝感)
    float shred = abs(sin(angNorm * 22.0 + fbm * 9.0 - uTime * 3.5));
    shred = pow(shred, 5.0);
    band *= 0.55 + 0.45 * shred;

    // —— 前缘白热线 (poly 锐峰) ——
    float edgeLine = pow(saturate(1.0 - abs(dN - 1.0) * 9.0), 3.0);

    // —— 双色梯度: 内怨红 → 外幽蓝, 噪声搅动 ——
    float mixT = smoothstep(0.55, 1.05, dN + (fbm - 0.5) * 0.3);
    float3 col = lerp(uColorCore.rgb, uColorEdge.rgb, mixT);
    col += float3(1.0, 0.92, 0.85) * edgeLine * 0.9; // 前缘白热
    col *= 0.85 + 0.3 * fbm;

    float alpha = saturate((band * 0.85 + edgeLine * 0.75) * arcMask * uIntensity);
    return float4(col * alpha, alpha); // 预乘, 配 Additive 使用
}

technique Technique1
{
    pass UmbralGrudgeWavePass
    {
        PixelShader = compile ps_3_0 UmbralGrudgeWavePS();
    }
}
