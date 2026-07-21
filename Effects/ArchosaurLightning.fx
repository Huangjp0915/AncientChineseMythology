// ============================================================
// 祖龙残魂 · 程序化闪电条带 — TriangleStrip 直带 (图元绘制, 仅 PS)
// 折线主干完全在带内程序化: 三八度噪声游走 + 端点锚定 + 分叉亮斑 + 形态重抽
// C# 侧只提供两端点直带 (uv.x=沿长 0~1, uv.y=横宽 0~1), 顶点契约同 BeamGrad
// 用途: 尾雷/次生雷柱/贯天雷柱/死亡巨雷/天空闪电/链电强化
// s0/s1 均绑共享可平铺噪声
// ============================================================

sampler uImage0 : register(s0); // 噪声 (占位, 与 s1 同源)
sampler uNoise  : register(s1); // 可平铺噪声 (RGB 三通道独立)

float  uTime;       // 动画时间(秒)
float  uIntensity;  // 整体强度 0~1
float4 uColorCore;  // 雷芯色 (a=芯部权重)
float4 uColorEdge;  // 辉光边色 (a=边缘权重)
float  uSeed;       // 形态种子 (每道雷相异)
float  uJagAmp;     // 折线振幅 0~1 (0.55 为标准闪电)
float  uFlicker;    // 高频闪烁强度 0~1

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 LightningPS(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;

    // 形态每 1/16s 重抽一次 (真实电弧的跳变感)
    float s = uSeed + floor(uTime * 16.0) * 0.1731;

    // 三八度带内游走: 低频摇摆 + 中频折线 + 高频锯齿
    float n1 = tex2D(uNoise, float2(uv.x * 0.70 + s,        frac(s * 7.31))).r;
    float n2 = tex2D(uNoise, float2(uv.x * 2.30 + s * 1.7,  frac(s * 3.77))).g;
    float n3 = tex2D(uNoise, float2(uv.x * 6.10 - s * 2.3,  frac(s * 5.13))).b;
    float wander = (n1 - 0.5) * 0.55 + (n2 - 0.5) * 0.33 + (n3 - 0.5) * 0.22;

    // 两端锚定回中心线 (雷从端点精确出入)
    float anchor = smoothstep(0.0, 0.10, uv.x) * smoothstep(1.0, 0.90, uv.x);
    float cx = 0.5 + wander * uJagAmp * anchor;

    float d = abs(uv.y - cx) * 2.0; // 0=雷芯 1=带边

    // 白热细芯 + 宽辉光
    float core = pow(saturate(1.0 - d * 8.0), 1.4);
    float glow = pow(saturate(1.0 - d), 2.6);

    // 分叉亮斑: 沿主干随机位置向外的发丝感
    float branchN = tex2D(uNoise, float2(uv.x * 9.0 + s * 3.1, frac(s * 1.93))).r;
    float branch = smoothstep(0.70, 0.95, branchN) * pow(saturate(1.0 - d), 1.2) * 0.65;

    // 高频闪烁 (供长驻雷柱呼吸; 一次性雷设低)
    float fl = 1.0 - uFlicker * 0.55 * (0.5 + 0.5 * sin(uTime * 62.0 + uv.x * 24.0 + uSeed * 39.0));

    // 端点收口
    float ends = smoothstep(0.0, 0.03, uv.x) * smoothstep(1.0, 0.97, uv.x);

    float3 col = uColorEdge.rgb * (glow + branch) + uColorCore.rgb * core * 1.7;
    float alpha = saturate(glow * uColorEdge.a + core * uColorCore.a + branch * 0.5);
    alpha *= uIntensity * fl * ends * input.color.a;
    col *= uIntensity * fl * input.color.rgb;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass LightningPass
    {
        PixelShader = compile ps_3_0 LightningPS();
    }
}
