// ============================================================
// 树精毒孢区着色器 — 屏幕空间贴地椭圆域 (декal)
// 漂浮孢子点场(三层升腾) + 边界孢膜辉光 + 底部冒泡 + 烧除焦蚀
// 由 ACMShaders.DrawScreenSpaceDecalStandalone 满屏噪声载体驱动:
//   s0 = 共享可平铺噪声; uCenter/uRadius 走屏幕 UV (WorldDecalParams 换算)
// uFlatten 把判定域压成贴地扁椭圆 (高/宽比, 建议 0.3~0.45)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;            // 动画时间(秒)
float2 uCenter;          // 中心归一化屏幕坐标 0~1
float  uRadius;          // 横向半径 (屏幕高度比例)
float  uIntensity;       // 整体强度 0~1
float  uAspect;          // 宽高比 width/height
float4 uColorPrimary;    // 孢子亮色 (毒绿)
float4 uColorSecondary;  // 雾底暗色 (沉绿)
float  uBurn;            // 烧除进度 0~1 (火烧反制: 域从噪声孔洞处焦蚀退散)
float  uFlatten;         // 纵向压扁比 (椭圆 y/x)

float4 SporeFieldPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01 || uBurn >= 0.99)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    diff.y /= max(uFlatten, 0.05); // 压扁 → 贴地椭圆
    float normDist = length(diff) / max(uRadius, 0.001);

    if (normDist > 1.35)
        return float4(0, 0, 0, 0);

    // —— 三层升腾孢子点: 阈值化噪声点, 各层速度/密度不同 ——
    float2 base = coords * float2(uAspect, 1.0);
    float s1 = tex2D(uImage0, base * 5.0  + float2(uTime * 0.010, -uTime * 0.035)).g;
    float s2 = tex2D(uImage0, base * 9.0  + float2(-uTime * 0.014, -uTime * 0.055)).b;
    float s3 = tex2D(uImage0, base * 14.0 + float2(uTime * 0.020, -uTime * 0.080)).r;
    float motes = smoothstep(0.72, 0.88, s1) * 0.9
                + smoothstep(0.76, 0.90, s2) * 0.7
                + smoothstep(0.80, 0.92, s3) * 0.5;

    // —— 底雾: 低频 FBM, 向上淡出 ——
    float fogN = tex2D(uImage0, base * 2.2 + float2(uTime * 0.02, -uTime * 0.012)).r * 0.6
               + tex2D(uImage0, base * 4.1 - float2(uTime * 0.015, uTime * 0.02)).g * 0.4;
    float fog = fogN * (1.0 - smoothstep(0.0, 1.05, normDist));

    // —— 边界孢膜: 噪声扰动的呼吸环 ——
    float warp = (fogN - 0.5) * 0.16;
    float dN = normDist + warp;
    float breath = 1.0 + sin(uTime * 2.2) * 0.03;
    float membrane = smoothstep(0.80 * breath, 0.98 * breath, dN)
                   * (1.0 - smoothstep(1.00 * breath, 1.18 * breath, dN));

    // —— 底部冒泡: 域内下半部的缓慢亮斑 ——
    float bubble = smoothstep(0.65, 0.95, tex2D(uImage0, base * 3.3 + float2(0.0, -uTime * 0.02)).b);
    bubble *= smoothstep(-0.2, 0.6, diff.y / max(uRadius, 0.001)); // 只在下半

    // —— 烧除焦蚀: 噪声孔洞从 uBurn 推进处撕开, 边缘橙红余烬 ——
    float burnField = fogN;
    float burnEdge = 0.0;
    if (uBurn > 0.001)
    {
        float t = uBurn * 1.15;
        if (burnField < t)
        {
            // 已烧穿区: 贴近烧蚀线残留余烬亮边
            burnEdge = smoothstep(t - 0.10, t, burnField);
            if (burnEdge < 0.01)
                return float4(0, 0, 0, 0);
        }
    }

    // —— 合成 ——
    float interior = 1.0 - smoothstep(0.85, 1.1, normDist);
    float3 col = uColorSecondary.rgb * fog * 0.9;
    col = lerp(col, uColorPrimary.rgb, saturate(motes) * 0.85);
    col += uColorPrimary.rgb * membrane * 0.8;
    col += uColorPrimary.rgb * bubble * 0.25;
    col += float3(0.95, 0.45, 0.12) * burnEdge * 1.5; // 余烬边

    float alpha = (fog * 0.30 + motes * 0.40 + membrane * 0.55 + bubble * 0.12) * interior;
    alpha += burnEdge * 0.4;
    alpha *= uIntensity;

    // 烧穿区透明化
    if (uBurn > 0.001 && burnField < uBurn * 1.15)
        alpha *= burnEdge;

    return float4(saturate(col), saturate(alpha)) * sampleColor.a;
}

technique Technique1
{
    pass SporeFieldPass
    {
        PixelShader = compile ps_3_0 SporeFieldPS();
    }
}
