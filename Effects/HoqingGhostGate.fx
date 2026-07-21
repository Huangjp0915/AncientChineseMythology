// ============================================================
// 后卿·鬼门 — 屏幕空间 SDF 竖椭圆裂隙
// 内部深渊涡流(极坐标螺旋噪声向内流动) + 边缘魂焰灼边 + 外围噪声裂纹光丝
// 用法同 ArenaRunic: 以共享可平铺噪声为载体满屏绘制 (AlphaBlend, 不读 screenTarget)
// uOpen 控制开度(缝→门), uFlash 供死亡演出白闪
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;        // 动画时间(秒)
float2 uCenter;      // 门中心 归一化屏幕UV
float  uAspect;      // 宽高比 width/height
float  uIntensity;   // 整体强度 0~1
float  uOpen;        // 开度 0~1 (缝隙 → 全开之门)
float  uHalfHeight;  // 门半高 (屏幕高度比例)
float4 uColorEdge;   // 焰边色 (鬼绿)
float4 uColorDeep;   // 深渊色 (幽紫黑)
float  uFlash;       // 白闪 0~1 (死亡爆点)

float4 GhostGatePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos = float2(coords.x * uAspect, coords.y);
    float2 c   = float2(uCenter.x * uAspect, uCenter.y);
    float2 d   = pos - c;

    float halfH = max(uHalfHeight, 0.001);
    // 开度: 极窄缝隙 → 竖椭圆门
    float halfW = halfH * lerp(0.05, 0.46, saturate(uOpen));

    // 边缘噪声扰动 (撕裂感)
    float2 nUV = coords * 3.0 + float2(uTime * 0.05, -uTime * 0.08);
    float n = tex2D(uImage0, nUV).r;

    float2 q = float2(d.x / halfW, d.y / halfH);
    float r = length(q) + (n - 0.5) * 0.22;

    // 早退: 远离门体
    if (r > 2.3)
        return float4(0, 0, 0, 0);

    float ang = atan2(q.y, q.x);

    // —— 内部深渊涡流: 极坐标螺旋, 向内吸入 ——
    float2 swirlUV = float2(ang / 6.28318 + uTime * 0.07, r * 0.9 - uTime * 0.35);
    float sw1 = tex2D(uImage0, swirlUV).g;
    float sw2 = tex2D(uImage0, swirlUV * 2.3 + float2(0.37, 0.61)).b;
    float swirl = sw1 * 0.65 + sw2 * 0.35;

    float inside = 1.0 - smoothstep(0.82, 1.0, r);
    // 深处近黑 → 涡流辉线浮现
    float3 abyss = lerp(uColorDeep.rgb * 0.18, uColorDeep.rgb * 0.85, swirl);
    float veins = pow(saturate(swirl - 0.42), 2.0) * 2.2 * saturate(r + 0.2);
    abyss += uColorEdge.rgb * veins * 0.8;

    // —— 边缘魂焰灼边 ——
    float edge = smoothstep(0.76, 1.0, r) * (1.0 - smoothstep(1.0, 1.32, r));
    edge *= 0.65 + 0.45 * sin(uTime * 7.0 + ang * 5.0 + n * 9.0);

    // —— 外围裂纹光丝 (只在门外) ——
    float2 crackUV = float2(ang * 1.6 + n * 0.3, r * 0.5 - uTime * 0.06);
    float crack = tex2D(uImage0, crackUV).r;
    crack = smoothstep(0.72, 0.92, crack)
          * smoothstep(1.95, 1.05, r)
          * step(1.0, r);

    float3 col = abyss * inside + uColorEdge.rgb * (edge * 1.25 + crack * 0.7);
    float alpha = saturate(inside * (0.72 + 0.28 * swirl) + edge * 0.9 + crack * 0.55);

    // 白闪 (死亡爆点)
    col = lerp(col, float3(1.0, 1.0, 1.0), saturate(uFlash) * 0.85);
    alpha = saturate(alpha + saturate(uFlash) * 0.35 * inside);

    alpha *= uIntensity;
    // 预乘输出: 深渊内部要能压暗背景
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass GhostGatePass
    {
        PixelShader = compile ps_3_0 GhostGatePS();
    }
}
