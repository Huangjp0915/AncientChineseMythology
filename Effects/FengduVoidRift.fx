// ============================================================
// 酆都虚空裂口 — 屏幕空间 decal (酆都武器系列专属)
// 中心纯黑虚空吞底 + 角向噪声撕裂边(黑紫电弧) + 外圈向心吸积流线
//   uMode: 0=圆形奇点裂口 (奇点弹引力阱/判官轮判决/刀漩涡/矛裂口/弓混元门)
//          1=竖立门形     (罗生门门体: 竖椭圆门框 + 门内噩梦下涌纹)
// 载体同 ArenaRunic: 满屏绘制可平铺噪声(s0), SDF 裁剪早退出
// 由 FengduVFX.DrawVoidRift 调用 (同帧 ≤2 张, C# 侧限流)
// ============================================================

sampler uImage0 : register(s0); // 可平铺三通道噪声

float  uTime;        // 动画时间(秒)
float2 uCenter;      // 中心归一化屏幕坐标 0~1
float  uRadius;      // 半径 (屏幕高度比例)
float  uIntensity;   // 整体强度 0~1
float  uAspect;      // 宽高比 width/height
float  uTear;        // 撕裂度 0~1 (边缘锯齿幅度 + 电弧密度)
float  uMode;        // 0=圆形奇点 1=竖门
float4 uColorEdge;   // 撕裂边色 (黑紫电弧)
float4 uColorGlow;   // 吸积辉色 (外圈流线/门内涌纹)
float  uSeed;        // 相位种子 (多实例错开)

float4 FengduVoidRiftPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;

    // 门形: x 压缩成竖椭圆 (竖长为 uRadius, 横宽 ~0.62x)
    float2 sd = diff;
    if (uMode > 0.5)
        sd.x /= 0.62;
    float dist = length(sd);
    float normDist = dist / max(uRadius, 0.001);

    // 大片早退 (性能)
    if (normDist > 1.75)
        return float4(0, 0, 0, 0);

    float angle   = atan2(diff.y, diff.x);
    float angNorm = angle / 6.28318 + 0.5;

    // 撕裂边: 角向噪声扰动等距线 → 锯齿裂口
    float tearN = tex2D(uImage0, float2(angNorm * 2.0 + uSeed, normDist * 0.6 - uTime * 0.07)).r;
    float dN = normDist + (tearN - 0.5) * (0.08 + 0.30 * uTear);

    // 中心虚空吞底 (AlphaBlend 下画近黑 → 压暗场景)
    float voidCore = 1.0 - smoothstep(0.55, 0.95, dN);

    // 撕裂边带
    float edgeBand = smoothstep(0.72, 0.96, dN) * (1.0 - smoothstep(1.02, 1.24, dN));

    // 边缘电弧闪烁 (密度随 uTear)
    float arcN = tex2D(uImage0, float2(angNorm * 3.4 - uTime * 0.23, dN * 3.0 + uSeed)).g;
    float arcs = smoothstep(0.72 - 0.20 * uTear, 0.94, arcN) * edgeBand;

    // 外圈向心吸积流线 (uv.y 随时间增 → 视觉向内流)
    float streamN = tex2D(uImage0, float2(angNorm * 3.0 + uSeed, dN * 1.6 + uTime * 0.38)).b;
    float stream = smoothstep(0.58, 0.95, streamN)
                 * smoothstep(1.55, 1.02, dN) * smoothstep(0.90, 1.05, dN);

    // 门内噩梦下涌纹 (仅门形): 门内纵向下涌的暗纹
    float doorFlow = 0.0;
    if (uMode > 0.5)
    {
        float df = tex2D(uImage0, float2(coords.x * 3.0 + uSeed, coords.y * 2.2 - uTime * 0.13)).r;
        doorFlow = smoothstep(0.48, 0.85, df) * voidCore;
    }

    // 呼吸脉冲
    float pulse = 0.9 + 0.1 * sin(uTime * 2.6 + uSeed * 6.28318);

    float3 col = float3(0.020, 0.006, 0.045) * voidCore
               + uColorEdge.rgb * (edgeBand * 0.85 + arcs * 1.35)
               + uColorGlow.rgb * (stream * 0.70 + doorFlow * 0.55);

    float alpha = saturate(voidCore * 0.94 + (edgeBand * 0.8 + arcs) * uColorEdge.a + stream * 0.55 * uColorGlow.a);
    alpha *= uIntensity * pulse;

    return float4(col, alpha);
}

technique Technique1
{
    pass FengduVoidRiftPass
    {
        PixelShader = compile ps_3_0 FengduVoidRiftPS();
    }
}
