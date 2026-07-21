// ============================================================
// 黑熊精·蜜潭 — 屏幕空间地面 decal (扁椭圆琥珀蜜液)
// 噪声扰边椭圆 SDF + 内部焦散流光 + 上浮气泡 + 边缘亮圈
//   uCenter/uRadius/uAspect: 世界→屏幕UV 由 ACMShaders.WorldDecalParams 换算
//   uFlatten: 纵向压扁比 (地面透视, 建议 0.20~0.30)
//   uIntensity: 0~1 (生成淡入/消散淡出)
// s0=可平铺噪声 (经 DrawScreenSpaceDecal 满屏绘制)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道)

float  uTime;      // 秒
float2 uCenter;    // 中心归一化屏幕坐标
float  uRadius;    // 横向半宽 (屏幕高度比例)
float  uIntensity; // 整体强度 0~1
float  uAspect;    // 宽高比
float  uFlatten;   // 纵向压扁比

static const float3 HoneyDeep  = float3(0.36, 0.19, 0.03);   // 深琥珀
static const float3 HoneyAmber = float3(0.85, 0.52, 0.10);   // 琥珀金
static const float3 HoneyGlow  = float3(1.00, 0.83, 0.38);   // 蜜光高光

float4 HoneyPoolPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;

    // 扁椭圆距离 (纵向除以压扁比 → 等距线为扁椭圆)
    float flat = max(uFlatten, 0.05);
    float dist = length(float2(diff.x, diff.y / flat));
    float normDist = dist / max(uRadius, 0.001);

    if (normDist > 1.45)
        return float4(0, 0, 0, 0);

    // —— 噪声扰边: 边缘蠕动的粘液轮廓 ——
    float angle = atan2(diff.y, diff.x);
    float2 eUV = float2(angle * 0.9 + uTime * 0.05, normDist * 0.8);
    float edgeN = tex2D(uImage0, eUV).r;
    float dN = normDist + (edgeN - 0.5) * 0.16;

    // 主体填充 (软边)
    float body = 1.0 - smoothstep(0.86, 1.02, dN);
    if (body < 0.003)
        return float4(0, 0, 0, 0);

    // —— 内部焦散流光: 两层慢速交叠噪声, 相乘后锐化成蜜浪 ——
    float2 cUV1 = float2(diff.x * 5.0 + uTime * 0.05, diff.y * 14.0 - uTime * 0.03);
    float2 cUV2 = float2(diff.x * 8.0 - uTime * 0.04, diff.y * 20.0 + 0.5);
    float c1 = tex2D(uImage0, cUV1).g;
    float c2 = tex2D(uImage0, cUV2).b;
    float caustic = smoothstep(0.12, 0.42, c1 * c2);

    // —— 上浮气泡: 网格伪随机点, 相位上移 + 接近液面变大爆开 ——
    float2 bUV = float2(diff.x * 9.0, diff.y * 22.0 + uTime * 0.10);
    float bub = tex2D(uImage0, bUV).r;
    float bubble = smoothstep(0.82, 0.93, bub) * (0.5 + 0.5 * sin(uTime * 3.0 + bub * 25.0));

    // —— 边缘亮圈 (粘稠表面张力高光) ——
    float rim = smoothstep(0.60, 0.96, dN) * body;
    float rimPulse = 0.85 + 0.15 * sin(uTime * 2.2 + angle * 2.0);

    // —— 合成 ——
    float3 col = lerp(HoneyDeep, HoneyAmber, caustic * 0.85 + 0.15);
    col += HoneyGlow * bubble * 0.5;
    col = lerp(col, HoneyGlow, rim * rimPulse * 0.55);

    // 呼吸: 整体缓慢明暗 (活物一样的蜜)
    float breath = 0.9 + 0.1 * sin(uTime * 1.4 + uCenter.x * 20.0);

    float alpha = saturate(body * (0.42 + caustic * 0.22 + rim * 0.30) * breath * uIntensity);
    // 预乘 Alpha 输出 (配合 XNA BlendState.AlphaBlend: One / InverseSourceAlpha)
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass HoneyPoolPass
    {
        PixelShader = compile ps_3_0 HoneyPoolPS();
    }
}
