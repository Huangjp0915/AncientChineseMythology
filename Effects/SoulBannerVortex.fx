// ============================================================
// 万魂幡·吸魂漩涡 — 世界空间 quad 单 pass (SoulBanner 专属)
// 极坐标三螺旋臂 + 向心流动噪声 + 内核亮斑; uProgress 从中心展开
// 用途: 左键引魂驻留幡尖漩涡 / 悬浮幡吸魂阵心 / 大招聚魂涡
// 建议 Additive 混合; uv 0~1 覆盖 quad (SpriteBatch.Draw 提供)
// s1 = 共享可平铺噪声
// ============================================================

sampler uImage0 : register(s0); // 占位载体 (不采样, 保留槽位)
sampler uNoise  : register(s1); // 可平铺噪声

float  uTime;      // 动画时间(秒)
float  uIntensity; // 整体强度 0~1
float  uProgress;  // 0~1 漩涡展开进度 (半径归一)
float  uSpin;      // 旋转速度 (rad/s 量级)
float4 uColorCore; // 内核亮色
float4 uColorEdge; // 螺旋臂边色
float  uSeed;      // 实例随机种子

float4 SoulBannerVortexPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 p = coords * 2.0 - 1.0;
    float r = length(p);
    if (r > 1.0)
        return float4(0, 0, 0, 0);

    float reach = max(uProgress, 0.05);
    float rn = r / reach;             // 展开归一半径
    if (rn > 1.0)
        return float4(0, 0, 0, 0);

    float ang = atan2(p.y, p.x);

    // ── 三螺旋臂: 内圈缠得紧、外圈舒展, 随时间向内卷 ──
    float spiral = sin(ang * 3.0 + rn * 9.0 - uTime * uSpin + uSeed * 6.0);
    float arms = smoothstep(0.1, 0.9, spiral * 0.5 + 0.5);

    // ── 向心流动噪声: 沿半径向内滚, 有"被吸进去"的流感 ──
    float flow = tex2D(uNoise, float2(ang * 0.159 + uTime * 0.05 + uSeed,
                                      rn * 1.4 - uTime * 0.55)).r;
    flow = 0.55 + 0.7 * flow;

    // ── 径向羽化 + 内核 ──
    float falloff = smoothstep(1.0, 0.5, rn) * smoothstep(0.02, 0.2, rn);
    float core = smoothstep(0.32, 0.02, rn);

    float armBody = arms * flow * falloff;
    float3 col = uColorEdge.rgb * armBody
               + uColorCore.rgb * (core * 1.45 + armBody * 0.35);

    float alpha = saturate(armBody * 0.85 * uColorEdge.a + core * uColorCore.a);
    alpha *= uIntensity;

    col *= sampleColor.rgb;
    alpha *= sampleColor.a;

    return float4(col * uIntensity, saturate(alpha));
}

technique Technique1
{
    pass SoulBannerVortexPass
    {
        PixelShader = compile ps_3_0 SoulBannerVortexPS();
    }
}
