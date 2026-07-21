// ============================================================
// 白虎·音波环着色器 — 环形 TriangleStrip PS
// 同心波纹调制 + 色散微光 + uGapAngle/uGapHalf 旋转安全缺口
// 顶点由 C# 生成圆环带 (参考 Xuanwu.DrawShockwaveRing):
//   uv.x = 角向 0~1 (角度/2π), uv.y = 径向 0(外缘)~1(内缘)
// 喂可平铺噪声(s0)
// ============================================================

sampler uNoise : register(s0); // 可平铺噪声 (RGB三通道独立)

float uTime;      // 动画时间(秒)
float uIntensity; // 整体强度 0~1
float uGapAngle;  // 缺口中心角(弧度, 世界角)
float uGapHalf;   // 缺口半宽(弧度); <=0 表示无缺口
float uRadius;    // 当前环半径(世界像素, 用于波纹密度补偿)

float4 SonicRingPS(float4 vertColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    // 环带横截面: uv.y 0=外缘 1=内缘, 0.5=带心
    float d = abs(uv.y - 0.5) * 2.0;
    float band = saturate(1.0 - d);

    // 角度(世界弧度): uv.x*2π 与 uGapAngle 同一约定(由 C# 顶点生成保证)
    float ang = uv.x * 6.28318530;

    // —— 旋转缺口: 缺口内音波静默(可穿) ——
    float gapMask = 1.0;
    if (uGapHalf > 0.001) {
        float diff = abs(frac((ang - uGapAngle) / 6.28318530 + 0.5) - 0.5) * 6.28318530;
        // 缺口边缘 12% 半宽的软过渡, 便于读出"门框"
        gapMask = smoothstep(uGapHalf * 0.88, uGapHalf * 1.12, diff);
    }

    // —— 同心波纹调制: 带内多条细波纹沿径向行进(声波的年轮) ——
    float ripple = 0.5 + 0.5 * sin(d * 14.0 - uTime * 9.0);
    ripple = pow(ripple, 2.0);

    // 角向声压不均匀(噪声驱动, 随时间滚动)
    float n = tex2D(uNoise, float2(uv.x * 3.0 + uTime * 0.05, uTime * 0.11)).r;
    float variance = 0.78 + 0.44 * n;

    // 前缘更亮(外缘 = 波前)
    float leading = smoothstep(1.0, 0.35, uv.y);

    // —— 色散微光: 内外缘轻微分色(声致折射的彩边) ——
    float3 silverCore = float3(0.92, 0.95, 1.0);
    float3 warmEdge = float3(1.0, 0.88, 0.66);  // 外缘暖金
    float3 coolEdge = float3(0.55, 0.75, 1.0);  // 内缘冷蓝
    float outerFrac = smoothstep(0.5, 0.0, uv.y);
    float innerFrac = smoothstep(0.5, 1.0, uv.y);
    float3 col = silverCore + warmEdge * outerFrac * 0.35 + coolEdge * innerFrac * 0.30;

    // 合成亮度: 带形 × 波纹 × 声压 × 前缘
    float lum = band * (0.55 + 0.45 * ripple) * variance * (0.6 + 0.4 * leading);

    // 带心白热
    lum += pow(band, 6.0) * 0.85;

    // 缺口边框微光: 缺口两侧亮一条窄边(读出"这里是门")
    float frame = 0.0;
    if (uGapHalf > 0.001) {
        float diff = abs(frac((ang - uGapAngle) / 6.28318530 + 0.5) - 0.5) * 6.28318530;
        frame = (1.0 - smoothstep(uGapHalf * 1.05, uGapHalf * 1.45, diff)) * step(uGapHalf * 0.95, diff);
    }

    float alpha = saturate(lum * gapMask + frame * band * 0.9);
    col += float3(0.55, 0.95, 0.75) * frame * band * 0.6; // 门框透安全翠玉色

    col *= vertColor.rgb;
    alpha *= vertColor.a * uIntensity;
    // Additive(SourceAlpha) 混合: 贡献 = rgb×a, 故返回真实 alpha(与 BeamGrad 同约定)
    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass SonicRingPass
    {
        PixelShader = compile ps_3_0 SonicRingPS();
    }
}
