// ============================================================
// 神木系列专属 — 年轮法阵 (屏幕空间 decal, 喂共享噪声 s0)
// 同心年轮(内密外疏) + 角向枝纹 + 木心微光; uGrow 驱动"自内向外生长"
// 生长前沿带亮边 — 系列"生根→绽放"机制语言的视觉锚点
// 经 ACMShaders.DrawScreenSpaceDecal 满屏绘制 (AlphaBlend, 预乘输出)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;           // 动画时间(秒)
float2 uCenter;         // 中心 (归一化屏幕 UV)
float  uRadius;         // 半径 (屏幕高度比例)
float  uIntensity;      // 整体强度 0~1
float  uAspect;         // 宽高比 width/height
float4 uColorPrimary;   // 深翠主色
float4 uColorSecondary; // 年轮金绿亮色
float  uGrow;           // 生长进度 0~1 (法阵从心长到满径)
float  uSpin;           // 整体角偏移(弧度)

float4 GrowthRingPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01 || uGrow <= 0.001)
        return float4(0, 0, 0, 0);

    float2 pos      = float2(coords.x * uAspect, coords.y);
    float2 center   = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff     = pos - center;
    float  dist     = length(diff);
    float  normDist = dist / max(uRadius, 0.001);

    if (normDist > 1.18)
        return float4(0, 0, 0, 0);

    float angle = atan2(diff.y, diff.x) + uSpin;

    // 噪声: 年轮的天然不规则 (角向低频 + 全局慢滚)
    float n1 = tex2D(uImage0, float2(angle * 0.6366 + uTime * 0.015, normDist * 0.8)).r;
    float n2 = tex2D(uImage0, coords * 2.5 + float2(uTime * 0.02, -uTime * 0.015)).g;
    float dN = normDist + (n1 - 0.5) * 0.10 + (n2 - 0.5) * 0.05;

    // —— 生长遮罩: 只显示已长出的部分, 生长前沿亮边 ——
    float growMask = smoothstep(uGrow + 0.02, uGrow - 0.06, dN);
    float frontier = smoothstep(uGrow - 0.12, uGrow - 0.015, dN)
                   * smoothstep(uGrow + 0.035, uGrow - 0.005, dN);

    // —— 同心年轮: 相位按 sqrt 展开 → 内密外疏 ——
    float ringPhase = sqrt(saturate(dN)) * 9.0;
    float rings = pow(abs(cos(ringPhase * 3.14159)), 10.0) * 0.85;

    // 外沿主环
    float edge = smoothstep(0.86, 0.96, dN) * (1.0 - smoothstep(0.99, 1.10, dN));

    // —— 角向枝纹: 细辐条, 噪声断续成"枝" ——
    float branch = pow(abs(sin(angle * 4.0 + dN * 2.4)), 28.0);
    branch *= smoothstep(1.0, 0.15, dN) * smoothstep(0.42, 0.70, n1 + 0.25);

    // —— 木心微光 ——
    float heart = smoothstep(0.30, 0.0, dN) * 0.35;

    float shape = max(max(rings, edge * 1.1), max(branch * 0.8, heart)) * growMask
                + frontier * 1.15;

    float pulse = 0.88 + 0.12 * sin(uTime * 2.4 + dN * 6.0);
    float alpha = saturate(shape * pulse * uIntensity);

    float3 col = lerp(uColorPrimary.rgb, uColorSecondary.rgb,
                      saturate(frontier + rings * 0.45 + edge * 0.55));

    // 预乘输出 (AlphaBlend 批); alpha 略降让底图透出, 高光靠预乘色溢出
    return float4(col * alpha, alpha * 0.85);
}

technique Technique1
{
    pass GrowthRingPass
    {
        PixelShader = compile ps_3_0 GrowthRingPS();
    }
}
