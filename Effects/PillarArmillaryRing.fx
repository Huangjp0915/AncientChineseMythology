// ============================================================
// 天柱系列 · 璇玑浑天仪领域 — 屏幕空间 decal (加性)
// 三道倾角摆动的椭圆环(赤道/黄道/子午) + 环上刻度珠 + 中心淡金核
// 载体: DrawScreenSpaceDecalStandalone 满屏噪声贴图 (s0)
// uCenter/uRadius 经 WorldDecalParams 换算 (屏幕UV / 屏高比例)
// ============================================================

sampler uImage0 : register(s0); // 共享可平铺噪声

float  uTime;
float2 uCenter;         // 中心归一化屏幕坐标 0~1
float  uRadius;         // 最外环半径 (屏幕高度比例)
float  uIntensity;      // 整体强度 0~1
float  uAspect;         // 宽高比 width/height
float4 uColorPrimary;   // 主色 (祥金)
float4 uColorSecondary; // 辅色 (天青)

// 单环: 旋入环自转系后纵向压扁模拟三维倾角, 返回环带强度与角向坐标
float RingBand(float2 diff, float radius, float spin, float tiltPhase, out float angNorm)
{
    float cs = cos(spin);
    float sn = sin(spin);
    float2 p = float2(diff.x * cs - diff.y * sn, diff.x * sn + diff.y * cs);
    float squash = lerp(0.24, 0.95, 0.5 + 0.5 * sin(tiltPhase));
    p.y /= squash;
    float nd = length(p) / max(radius, 0.0001);
    angNorm = atan2(p.y, p.x) / 6.28318 + 0.5;
    float w = 0.05;
    return smoothstep(w * 2.2, w * 0.4, abs(nd - 1.0));
}

float4 PillarArmillaryRingPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  nd     = length(diff) / max(uRadius, 0.0001);
    if (nd > 1.4)
        return float4(0, 0, 0, 0);

    float n = tex2D(uImage0, coords * 3.0 + float2(uTime * 0.03, -uTime * 0.02)).r;

    float a0, a1, a2;
    float r0 = RingBand(diff, uRadius * 1.00,  uTime * 0.45, uTime * 0.60,       a0);
    float r1 = RingBand(diff, uRadius * 0.80, -uTime * 0.60, uTime * 0.45 + 2.1, a1);
    float r2 = RingBand(diff, uRadius * 0.62,  uTime * 0.80, uTime * 0.75 + 4.2, a2);

    // 环上刻度珠 (角向高频亮点, 随环流转)
    float t0 = pow(abs(sin((a0 * 12.0 + uTime * 0.30) * 3.14159)), 16.0);
    float t1 = pow(abs(sin((a1 * 10.0 - uTime * 0.24) * 3.14159)), 16.0);
    float t2 = pow(abs(sin((a2 *  8.0 + uTime * 0.36) * 3.14159)), 16.0);

    float ring0 = r0 * (0.50 + 0.65 * t0);
    float ring1 = r1 * (0.50 + 0.65 * t1);
    float ring2 = r2 * (0.45 + 0.60 * t2);

    // 中心淡金核 + 领域内极淡弥漫
    float coreG = smoothstep(0.24, 0.0, nd) * 0.22;
    float fill  = smoothstep(1.05, 0.0, nd) * 0.045;

    float3 col = uColorPrimary.rgb   * (ring0 + ring2 * 0.5 + coreG)
               + uColorSecondary.rgb * (ring1 + ring2 * 0.5 + fill);

    float breath = 0.92 + 0.08 * sin(uTime * 2.4);
    float alpha = saturate((ring0 + ring1 + ring2) * 0.85 + coreG + fill);
    alpha *= uIntensity * breath * (0.85 + 0.30 * n);

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass PillarArmillaryRingPass
    {
        PixelShader = compile ps_3_0 PillarArmillaryRingPS();
    }
}
