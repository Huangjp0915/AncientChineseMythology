// ============================================================
// 径向泛光着色器 — 加性径向 bloom + 阈值光晕
// 蓄力/爆发/重生/夺宝/处决配方通用; 以加法叠加于场景上方
// 全屏绘制占位白像素(s0), 完全程序化生成光晕
// 建议 BlendState.Additive 绘制
// ============================================================

sampler uImage0 : register(s0); // 占位, 不采样

float  uTime;        // 动画时间(秒)
float2 uCenter;      // 中心归一化屏幕坐标 0~1
float  uIntensity;   // 整体强度 0~1
float  uRadius;      // 泛光半径 (屏幕高度比例)
float  uAspect;      // 宽高比 width/height
float4 uColor;       // 泛光色 (rgb, a=核心强度)
float  uRayCount;    // 光芒条数 (0=纯圆晕)
float  uFalloff;     // 衰减锐度 (建议 1.5~4)

float4 RadialBloomPS(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);
    float  normDist = dist / max(uRadius, 0.001);

    if (normDist > 1.8)
        return float4(0, 0, 0, 0);

    // 径向衰减核心
    float core = pow(saturate(1.0 - normDist), max(uFalloff, 0.001));

    // 内核过曝
    float hotspot = pow(saturate(1.0 - normDist * 2.0), 4.0);

    // 旋转光芒
    float rays = 1.0;
    if (uRayCount > 0.5)
    {
        float angle = atan2(diff.y, diff.x);
        rays = abs(cos(angle * uRayCount * 0.5 + uTime * 0.6));
        rays = pow(rays, 4.0);
        rays = lerp(0.6, 1.0, rays); // 不至于完全暗
    }

    // 呼吸脉冲
    float breath = 0.85 + 0.15 * sin(uTime * 3.0);

    float glow = (core * rays + hotspot) * breath;
    float3 col = uColor.rgb * glow * uColor.a;

    float alpha = saturate(glow * uIntensity);
    // 加性: 输出预乘色, alpha 也带上以兼容 AlphaBlend
    return float4(col * uIntensity, alpha);
}

technique Technique1
{
    pass RadialBloomPass
    {
        PixelShader = compile ps_3_0 RadialBloomPS();
    }
}
