// ============================================================
// AzureDragonRibbon.fx — 苍龙龙身流光条带 (招牌着色器)
// 沿整条龙身节点铺 TriangleStrip (uv.x=头0→尾1, uv.y=横宽0~1)
// 特性:
//   · 双向鳞纹流动 — 两套噪声反向滚动叠乘, 龙身像一条导电的河
//   · 头→尾行波脉冲 — 能量沿身体传递的呼吸感
//   · uChargePos/uChargeGlow — 「龙身放电」电荷扫描带 (从尾推进到头)
//   · uStrikeBoost — 穿刺瞬间全身过曝 (速度门控, 常态为 0)
// 顶点契约与 BeamGrad 相同: 屏幕像素坐标 + GameViewMatrix, 仅 PS
// s0/s1 均绑共享可平铺噪声 (ACMShaders.NoiseTexture)
// ============================================================

sampler uImage0 : register(s0); // 噪声 (鳞纹主纹理)
sampler uNoise  : register(s1); // 噪声 (流动扰动)

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 整体强度 0~1
float4 uColorCore;   // 核心色 (a=芯部不透明度权重)
float4 uColorEdge;   // 边缘色 (a=边缘不透明度权重)
float  uCoreSharp;   // 核心收窄锐度 (1~4)
float  uFlowSpeed;   // 鳞纹流动速度
float  uScaleFreq;   // 鳞纹沿身频率
float  uChargePos;   // 电荷扫描位置 (0=头 1=尾; <0 关闭)
float  uChargeGlow;  // 电荷带辉度 0~2
float  uStrikeBoost; // 穿刺过曝 0~1

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 PS_DragonRibbon(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;

    // 横向: 0=中心线 1=边缘
    float edgeDist = abs(uv.y - 0.5) * 2.0;
    float coreProfile = pow(saturate(1.0 - edgeDist), max(uCoreSharp, 0.001));
    float bodyProfile = saturate(1.0 - edgeDist * edgeDist);

    // 双向鳞纹: 两套噪声沿身反向滚动, 相乘出"鳞片间流窜的电光"
    float freq = max(uScaleFreq, 0.001);
    float2 uvA = float2(uv.x * freq - uTime * uFlowSpeed,        uv.y * 0.6 + 0.13);
    float2 uvB = float2(uv.x * freq * 0.53 + uTime * uFlowSpeed * 0.71, uv.y * 0.4 + 0.57);
    float nA = tex2D(uImage0, uvA).r;
    float nB = tex2D(uNoise,  uvB).g;
    float scales = saturate(nA * 0.65 + nB * 0.55);
    scales = 0.55 + 0.9 * scales * scales;

    // 头→尾行波脉冲 (能量呼吸)
    float pulse = 1.0 + 0.22 * sin(uv.x * 22.0 - uTime * 7.0);

    // 电荷扫描带: uChargePos 处一条白热亮带 (放电 set-piece 用)
    float charge = 0.0;
    if (uChargePos >= 0.0)
    {
        float band = 1.0 - saturate(abs(uv.x - uChargePos) / 0.16);
        charge = band * band * uChargeGlow;
    }

    float3 col = lerp(uColorEdge.rgb, uColorCore.rgb, coreProfile);
    col *= scales * pulse;
    // 电荷带与穿刺过曝都推向白热
    col += (uColorCore.rgb * 0.6 + 0.4) * charge;
    col += uColorCore.rgb * coreProfile * uStrikeBoost * 1.6;

    // 首尾收口: 头部略开(0.03), 尾端长收(0.85→1)
    float ends = smoothstep(0.0, 0.03, uv.x) * smoothstep(1.0, 0.85, uv.x);

    float alpha = bodyProfile * ends * uIntensity;
    alpha *= lerp(uColorEdge.a, uColorCore.a, coreProfile);
    alpha = saturate(alpha + charge * 0.35 * ends);

    // 顶点色承载沿身淡出与整体染色
    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass DragonRibbonPass
    {
        PixelShader = compile ps_3_0 PS_DragonRibbon();
    }
}
