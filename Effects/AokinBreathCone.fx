// ============================================================
// 敖钦赤炎龙息 — 锥形火舌 (TriangleStrip 条带, 仅 PS)
// 顶点契约与 BeamGrad 相同: uv.x=沿长 0~1(口部→尖端), uv.y=横宽 0~1
// 双层反向流动噪声撕裂边缘 + 白热核心 + 尖端碎裂消散
// Additive 绘制: 输出颜色即能量, alpha 通道置 0
// ============================================================

sampler uImage0 : register(s0); // 共享噪声 (s0/s1 同绑, 沿用 DrawBeam 约定)
sampler uNoise  : register(s1); // 共享可平铺噪声

float uTime;        // 动画时间 (秒)
float uIntensity;   // 总强度 0~1 (蓄力淡入 / 收势淡出)
float uFlowSpeed;   // 流动速度 (建议 1.6~2.4)
float uNoiseScale;  // 噪声沿长平铺尺度 (建议 1.5~2.5)
float uCoreSharp;   // 白热核心锐度 (建议 2.5~4)
float4 uColorCore;  // 白热核心色
float4 uColorMid;   // 熔橙中间色
float4 uColorEdge;  // 赤红边缘色

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV, z 未用 (BuildRibbonStrip 顶点契约)
};

float4 BreathConePS(VSOutput input) : COLOR0
{
    float2 coords = input.texCoord.xy;
    float along = coords.x;                    // 0=口部 1=尖端
    float across = abs(coords.y - 0.5) * 2.0;  // 0=中轴 1=边缘

    // 双层流动噪声: 速度差 → 湍流火舌
    float n1 = tex2D(uNoise, float2(along * uNoiseScale - uTime * uFlowSpeed,
                                    coords.y * 1.7 + uTime * 0.31)).r;
    float n2 = tex2D(uNoise, float2(along * uNoiseScale * 1.9 - uTime * uFlowSpeed * 1.6,
                                    coords.y * 3.1 - uTime * 0.47)).g;
    float turb = n1 * 0.65 + n2 * 0.35;

    // 边缘被噪声撕裂: 有效横向边界随湍流抖动, 越远越细碎
    float edgeLimit = 0.95 - (turb - 0.5) * 0.6 - along * 0.1;
    float body = smoothstep(edgeLimit, edgeLimit * 0.42, across);

    // 口部淡入 / 尖端随噪声碎裂消散
    float head = smoothstep(0.0, 0.07, along);
    float tail = 1.0 - smoothstep(0.55 + turb * 0.28, 1.0, along);
    body *= head * tail;

    // 白热核心: 贴近中轴 + 靠近口部最亮
    float core = pow(saturate(1.0 - across * (1.35 + along * 1.3)), uCoreSharp)
               * (1.0 - along * 0.55) * tail;

    float3 col = uColorEdge.rgb;
    col = lerp(col, uColorMid.rgb, saturate(body * 1.15));
    col = lerp(col, uColorCore.rgb, saturate(core * (0.65 + turb * 0.8)));

    float energy = (body * 0.55 + core * 0.95) * uIntensity;
    energy *= input.color.a > 0.001 ? input.color.a : 1.0;
    return float4(col * energy, 0.0);
}

technique Technique1
{
    pass BreathConePass
    {
        PixelShader = compile ps_3_0 BreathConePS();
    }
}
