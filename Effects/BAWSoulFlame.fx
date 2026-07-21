// ============================================================
// 黑白无常·程序魂火 — 世界空间 quad 单 pass (BAW 专属)
// SDF 泪滴焰形 + 噪声侵蚀边缘 + 内核高亮, 全程序化 (载体贴图仅占位)
// 用途: 双使体表魂焰罩 / 引魂灯充能焰 / 死亡魂焰柱
// 建议 Additive 混合绘制; uv 0~1 覆盖 quad, 焰尖朝 quad 上方
// s1 = 共享可平铺噪声
// ============================================================

sampler uImage0 : register(s0); // 占位载体 (不采样也可, 保留槽位)
sampler uNoise  : register(s1); // 可平铺噪声

float  uTime;      // 动画时间(秒)
float  uIntensity; // 整体强度 0~1
float4 uCoreColor; // 内核色
float4 uEdgeColor; // 边焰色
float  uSeed;      // 实例随机种子 (错开噪声相位)
float  uStretch;   // 纵向拉伸 (1=圆焰, >1 越拉越像柱)

float4 BAWSoulFlamePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    // 中心化坐标: x∈[-1,1], y∈[-1,1] (y=-1 为焰尖上端)
    float2 p = coords * 2.0 - 1.0;
    p.y *= 1.0 / max(uStretch, 0.25); // 纵向拉伸焰体

    // 泪滴形 SDF: 越往上(焰尖) 半径越收窄
    float taper = lerp(1.0, 0.35, saturate(-p.y * 0.5 + 0.5)); // 上窄下宽
    float r = length(float2(p.x / max(taper, 0.15), p.y * 0.9 + 0.12));

    // 噪声侵蚀: 沿焰体向上卷动 (双八度)
    float2 nUV1 = float2(coords.x * 0.9 + uSeed, coords.y * 0.6 - uTime * 0.42 + uSeed);
    float2 nUV2 = float2(coords.x * 2.2 - uSeed, coords.y * 1.4 - uTime * 0.66);
    float n = tex2D(uNoise, nUV1).r * 0.65 + tex2D(uNoise, nUV2).g * 0.35;

    // 焰形: 距离 + 噪声侵蚀 + 焰尖抖动
    float flick = sin(uTime * 7.0 + uSeed * 12.0) * 0.06;
    float shape = smoothstep(0.95, 0.25, r + (n - 0.5) * 0.62 + flick * saturate(-p.y));
    if (shape < 0.003)
        return float4(0, 0, 0, 0);

    // 内核: 更小更亮, 略微下沉
    float core = smoothstep(0.52, 0.06, r * (1.25 - (n - 0.5) * 0.4) + p.y * 0.10);

    float3 col = uEdgeColor.rgb * shape + uCoreColor.rgb * core * 1.35;
    // 焰内明暗流动
    col *= 0.82 + 0.30 * n;

    float alpha = shape * uIntensity * uEdgeColor.a + core * uIntensity * uCoreColor.a;
    return float4(col * uIntensity, saturate(alpha));
}

technique Technique1
{
    pass BAWSoulFlamePass
    {
        PixelShader = compile ps_3_0 BAWSoulFlamePS();
    }
}
