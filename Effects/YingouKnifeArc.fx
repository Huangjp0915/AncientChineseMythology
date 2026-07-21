// ============================================================
// 鬼牙·冷光斩线 — 武器 YingouKnife 专属 (ps_3_0)
// uProgress 单参驱动完整波形: 预警细线 (末段掺红) → poly 爆宽白闪 → 噪声撕裂残光
// 顶点: BuildRibbonStrip 两点直带图元 (uv.x=沿线 0~1, uv.y=横向 0~1), Additive
// s1 = 平铺噪声
// ============================================================

sampler uImage0 : register(s0); // 占位
sampler uNoise  : register(s1); // 平铺噪声

float  uTime;
float  uIntensity;  // 0~1 整体强度
float  uProgress;   // 0~1 生命周期 (0~0.38 预警 / 0.38~0.52 爆发 / 0.52~1 残光)
float4 uColorCore;  // 冷白刃芯
float4 uColorEdge;  // 幽紫外缘
float4 uColorWarn;  // 预警终段红

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0;
};

float4 KnifeArcPS(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;
    float p = saturate(uProgress);

    // 波形分段
    float warm  = smoothstep(0.22, 0.38, p);              // 预警末段红量
    float snap  = 1.0 - pow(1.0 - saturate((p - 0.38) / 0.14), 6.0); // poly(6) 爆宽
    float decay = saturate((p - 0.52) / 0.48);             // 残光衰减

    // 宽度包络: 细预警线 → 全宽 → 回落
    float widthF = lerp(0.055, 1.0, snap) * (1.0 - 0.62 * decay);

    float edgeDist = abs(uv.y - 0.5) * 2.0;   // 0=芯 1=带边
    float d = edgeDist / max(widthF, 0.001);
    if (d > 1.0)
        return float4(0, 0, 0, 0);

    // 冷芒锯齿: 沿线滚动噪声, 爆发期变粗粝
    float saw = tex2D(uNoise, float2(uv.x * 5.0 - uTime * 1.7, uv.y * 0.7 + 0.13)).r;

    float coreProfile = pow(saturate(1.0 - d), 3.0);
    float body = saturate(1.0 - d) * (0.72 + 0.55 * saw);

    // 残光撕裂: 噪声阈值蚕食
    float tear = tex2D(uNoise, float2(uv.x * 3.0 + 7.31, uv.y + uTime * 0.13)).g;
    body *= saturate(1.0 - decay * 1.7 * (1.0 - tear));

    // 预警呼吸脉动 (爆发后停止)
    float puls = 1.0 + 0.30 * sin(uTime * 22.0) * (1.0 - snap);

    // 颜色: 芯冷白 (预警末段转红, 爆发即回冷白), 边幽紫
    float3 coreCol = lerp(uColorCore.rgb, uColorWarn.rgb, warm * (1.0 - snap));
    float3 col = lerp(uColorEdge.rgb, coreCol, coreProfile);
    col += coreCol * coreProfile * coreProfile * snap * 0.9; // 爆发帧芯部过曝

    // 端点收口
    float ends = smoothstep(0.0, 0.05, uv.x) * smoothstep(1.0, 0.95, uv.x);

    // 亮度包络: 预警半亮 → 爆发全亮 → 残光衰减
    float bright = lerp(0.42, 1.0, snap) * (1.0 - 0.75 * decay) * puls;

    float alpha = body * ends * bright * uIntensity;
    alpha *= lerp(uColorEdge.a, uColorCore.a, coreProfile); // a 通道为不透明度权重 (ToVector4 已 0~1)

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col * bright), saturate(alpha));
}

technique Technique1
{
    pass KnifeArcPass
    {
        PixelShader = compile ps_3_0 KnifeArcPS();
    }
}
