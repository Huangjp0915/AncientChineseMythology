// ============================================================
// CelestialDragonPillar.fx — 天御金龙·天光柱 (垂直光柱)
// 画在竖直拉伸的全幅 quad 上 (uv.x=横 0~1, uv.y=纵 0=顶 1=底)
// 噪声蚀边 + 自上而下流光 + uGrow 推进(光自天顶轰落) + 落点热斑
// 供入场贯天柱 / 天光柱阵 (CelestialSkyPillar) 复用; Additive
// ============================================================

sampler uTexture : register(s0); // 占位 quad 纹理
sampler uNoise   : register(s1); // 共享三通道噪声

float  uTime;       // 秒
float  uIntensity;  // 总强度 0~1
float  uGrow;       // 光柱推进 0~1 (可见区域 uv.y < uGrow; ≥1 = 全长)
float  uWidth;      // 核心宽度占比 0~1 (预警细线→全宽轰落)
float  uFlowSpeed;  // 纵向流速
float4 uColorCore;  // 柱心金白
float4 uColorEdge;  // 边缘暖金

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float2 texCoord : TEXCOORD0;
};

float4 PS_Pillar(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;
    float dx = abs(uv.x - 0.5) * 2.0; // 0=轴线 1=边

    // 噪声蚀边: 光柱边界沿高度起伏呼吸
    float n = tex2D(uNoise, float2(uv.y * 2.2 - uTime * uFlowSpeed * 0.8, uv.x * 1.3)).r;
    float w = max(uWidth * (0.85 + (n - 0.5) * 0.5), 0.02);
    float body = saturate(1.0 - dx / w);
    float core = pow(body, 3.0);

    // 自上而下流光
    float flow = tex2D(uNoise, float2(uv.x * 1.8, uv.y * 2.6 - uTime * uFlowSpeed)).g;

    float3 col = lerp(uColorEdge.rgb, uColorCore.rgb, core) * (0.65 + flow * 0.65);
    col += uColorCore.rgb * core * core * 0.9;

    // 推进锋面: uv.y < uGrow 可见, 锋面处有亮头
    float front = smoothstep(uGrow, uGrow - 0.06, uv.y);
    float frontGlow = saturate(1.0 - abs(uv.y - uGrow) / 0.05) * step(uGrow, 1.0);
    col += uColorCore.rgb * frontGlow * body * 1.2;

    // 落点热斑 (柱触底后底部增亮)
    float baseGlow = smoothstep(0.86, 1.0, uv.y) * step(0.99, uGrow);
    col += uColorCore.rgb * baseGlow * body * 0.8;

    // 顶端淡出 (没入天光)
    float topFade = smoothstep(0.0, 0.10, uv.y);

    float alpha = body * front * topFade * uIntensity;
    alpha *= lerp(uColorEdge.a, uColorCore.a, core);
    alpha = saturate(alpha + (frontGlow + baseGlow) * body * 0.4 * uIntensity);

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass PillarPass
    {
        PixelShader = compile ps_3_0 PS_Pillar();
    }
}
