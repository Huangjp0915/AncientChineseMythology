// ============================================================
// 天柱系列 · 断穹天裂 — 顶点直带着色器 (BuildRibbonStrip 契约)
// 中心白热裂缝(噪声撕裂锯齿边) + 金色灼边 + 天青外晕 + 天光纵向流动
// uv.x = 沿长 (0=冲击点/底部, 1=顶端), uv.y = 横宽 0~1
// s0/s1 = 共享可平铺噪声 (与 BeamGrad 同绑定约定), Additive 绘制
// ============================================================

sampler uImage0 : register(s0);
sampler uNoise  : register(s1);

float  uTime;      // 动画时间(秒)
float  uIntensity; // 整体强度 0~1 (淡入淡出)
float  uProgress;  // 裂隙开度 0~1 (0=闭合缝线, 1=全开)
float4 uColorCore; // 裂缝芯色 (白金, a=权重)
float4 uColorEdge; // 灼边色 (金, a=权重)
float4 uColorHaze; // 外晕色 (天青, a=权重)
float  uSeed;      // 每道裂隙随机相位

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 PillarSkyRiftPS(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;
    float edgeDist = abs(uv.y - 0.5) * 2.0; // 0=中心线 1=带边

    // 裂缝半宽沿长度被双八度噪声撕裂 (锯齿裂口)
    float jag  = tex2D(uNoise, float2(uv.x * 2.6 + uSeed, 0.37 + uSeed * 0.13)).r;
    float jag2 = tex2D(uNoise, float2(uv.x * 7.0 - uSeed, 0.71)).g;
    float crackW = uProgress * (0.16 + 0.38 * jag + 0.14 * jag2);

    // 天光自上而下流动
    float flow = tex2D(uNoise, float2(uv.y * 1.5 + uSeed, uv.x * 2.0 + uTime * 1.9)).b;
    flow = 0.75 + 0.5 * flow;

    // 白热裂芯 / 金灼边 / 青外晕 三层剖面
    float core = smoothstep(crackW, crackW * 0.25, edgeDist);
    float edge = smoothstep(crackW * 2.6, crackW * 0.9, edgeDist) * (1.0 - core * 0.6);
    float haze = smoothstep(1.0, crackW * 1.6, edgeDist) * 0.30;

    // 底部(冲击点)增辉, 顶端渐隐
    float baseFlare = 1.0 + 1.2 * smoothstep(0.18, 0.0, uv.x);
    float topFade = smoothstep(1.0, 0.72, uv.x);

    // 裂缝内高频电闪 (行波)
    float arc = pow(abs(sin(uv.x * 34.0 - uTime * 22.0 + jag * 6.0)), 24.0);
    core += arc * core * 0.8;

    float3 col = uColorCore.rgb * core * (1.2 + 0.8 * flow)
               + uColorEdge.rgb * edge * flow
               + uColorHaze.rgb * haze;
    col *= baseFlare;

    float alpha = core * uColorCore.a + edge * uColorEdge.a + haze * uColorHaze.a;
    alpha *= topFade * uIntensity;

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass PillarSkyRiftPass
    {
        PixelShader = compile ps_3_0 PillarSkyRiftPS();
    }
}
