// ============================================================
// 大椿Boss限制圈着色器 — 动态自然纹路屏障
// 使用多层噪声采样实现有机藤蔓纹理效果
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声纹理 (RGB三通道独立噪声)

float uTime;              // 动画时间 (秒)
float2 uCenter;           // 圆心归一化屏幕坐标 (0~1)
float uRadius;            // 半径 (屏幕高度的比例)
float uIntensity;         // 整体强度 (0~1, 用于淡入淡出)
float uAspect;            // 屏幕宽高比 (width / height)
float4 uColorPrimary;    // 主色调 (深林绿)
float4 uColorSecondary;  // 辅色调 (古金色)

float4 ArenaCirclePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    // 宽高比校正 — 保证圆形不变椭圆
    float2 pos = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);

    float dist = distance(pos, center);

    // 呼吸动画 — 半径微弱脉动
    float breath = sin(uTime * 1.5) * 0.005 * uRadius;
    float effRadius = uRadius + breath;
    float normDist = dist / effRadius;

    // 距离过远/过近 → 直接透明（优化跳过大部分屏幕像素）
    if (normDist > 1.6 || normDist < 0.4)
        return float4(0, 0, 0, 0);

    // ==========================================
    //  多八度噪声 (FBM) — 有机位移
    // ==========================================
    float2 n1UV = coords * 3.0 + float2(uTime * 0.04, uTime * 0.03);
    float2 n2UV = coords * 5.5 + float2(-uTime * 0.05, uTime * 0.04);
    float2 n3UV = coords * 1.8 + float2(uTime * 0.02, -uTime * 0.03);

    float n1 = tex2D(uImage0, n1UV).r;
    float n2 = tex2D(uImage0, n2UV).g;
    float n3 = tex2D(uImage0, n3UV).b;

    float fbm = n1 * 0.5 + n2 * 0.3 + n3 * 0.2;

    // 边缘扰动
    float warp = (fbm - 0.5) * 0.13;
    float dN = normDist + warp;

    // ==========================================
    //  主圆环
    // ==========================================
    float th = 0.06;
    float ringIn  = smoothstep(1.0 - th * 2.5, 1.0 - th * 0.3, dN);
    float ringOut = 1.0 - smoothstep(1.0 + th * 0.3, 1.0 + th * 2.5, dN);
    float ring = ringIn * ringOut;

    // ==========================================
    //  极角坐标 (用于环绕纹路)
    // ==========================================
    float2 diff = pos - center;
    float angle = atan2(diff.y, diff.x);
    float angNorm = angle / 6.28318 + 0.5;

    // ==========================================
    //  藤蔓层A — 沿圆周缓慢流动
    // ==========================================
    float2 v1UV = float2(angNorm * 8.0 + uTime * 0.06, normDist * 3.0 - uTime * 0.04);
    float vine1 = tex2D(uImage0, v1UV).r;

    float2 v2UV = float2(angNorm * 12.0 - uTime * 0.05, normDist * 4.0 + uTime * 0.03);
    float vine2 = tex2D(uImage0, v2UV).g;

    float vineBlend = vine1 * 0.6 + vine2 * 0.4;
    float vineShape = smoothstep(0.40, 0.78, vineBlend);

    float vineMask = smoothstep(1.0 + th * 4.5, 1.0, dN)
                   * smoothstep(1.0 - th * 4.5, 1.0, dN);
    vineShape *= vineMask;

    // ==========================================
    //  根须 — 向内延伸
    // ==========================================
    float2 rUV = float2(angNorm * 6.0 + uTime * 0.02, normDist * 2.5 + uTime * 0.05);
    float rootNoise = tex2D(uImage0, rUV).r;
    float roots = smoothstep(0.55, 0.88, rootNoise);
    float rootMask = smoothstep(0.62, 0.92, normDist)
                   * (1.0 - smoothstep(1.0, 1.08, normDist));
    roots *= rootMask * 0.4;

    // ==========================================
    //  外延藤蔓尾 — 向外飘散
    // ==========================================
    float2 oUV = float2(angNorm * 5.0 - uTime * 0.03, normDist * 2.0 - uTime * 0.04);
    float outerVine = tex2D(uImage0, oUV).b;
    float outerShape = smoothstep(0.52, 0.85, outerVine);
    float outerVineMask = smoothstep(1.35, 1.05, normDist) * smoothstep(1.0, 1.02, normDist);
    outerShape *= outerVineMask * 0.28;

    // ==========================================
    //  微光闪点 — 高频噪声的亮斑
    // ==========================================
    float2 sparkleUV = float2(angNorm * 22.0 + uTime * 0.12, normDist * 12.0);
    float sparkle = tex2D(uImage0, sparkleUV).r;
    sparkle = smoothstep(0.86, 0.96, sparkle) * ring * 0.35;

    // ==========================================
    //  合并形状
    // ==========================================
    float shape = max(ring, max(vineShape * 0.55, max(roots, outerShape)));
    shape = max(shape, sparkle);

    // ==========================================
    //  着色
    // ==========================================
    float colorMix = smoothstep(0.38, 0.72, fbm);
    float4 baseColor = lerp(uColorPrimary, uColorSecondary, colorMix);

    // 藤蔓区域微偏绿
    baseColor.rgb += vineShape * vineMask * float3(-0.03, 0.07, -0.02);
    // 闪点偏亮金
    baseColor.rgb += sparkle * float3(0.15, 0.12, 0.0);

    // ==========================================
    //  脉冲呼吸动画
    // ==========================================
    float pulse = sin(uTime * 2.2 + angle * 3.0) * 0.1 + 0.9;

    // ==========================================
    //  内侧预警辉光
    // ==========================================
    float edgeWarn = smoothstep(0.78, 1.0, normDist) * 0.07;

    // ==========================================
    //  外侧边界辉光
    // ==========================================
    float outerGlow = saturate(1.0 - smoothstep(1.0, 1.22, normDist))
                    * smoothstep(1.0, 1.015, normDist) * 0.10;

    // ==========================================
    //  最终合成
    // ==========================================
    float alpha = saturate((shape * pulse + edgeWarn + outerGlow) * uIntensity);

    return float4(baseColor.rgb, alpha);
}

technique Technique1
{
    pass ArenaCirclePass
    {
        PixelShader = compile ps_3_0 ArenaCirclePS();
    }
}
