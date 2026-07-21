// ============================================================
// 赢勾·冥刃条带着色器 — TriangleStrip 刃迹/刃晕/巨刃辉带
// 芯部过曝 + 边缘辉光 + 沿长流动噪声 + 锯齿明灭 + 热度(主题色→致命红)混合
// 顶点由 ACMUtils.BuildRibbonStrip 提供 (uv.x=沿长 0头1尾, uv.y=横宽 0~1)
// 仅 PS, 顶点变换沿用 SpriteBatch 外部矩阵 (同 BeamGrad/XuanwuTrailRibbon 约定)
// s0 = 刃光纹理(SwordSlashTexture/噪声均可), s1 = 可平铺流动噪声
// ============================================================

sampler uImage0 : register(s0); // 刃光纹理
sampler uNoise  : register(s1); // 可平铺噪声 (RGB 三通道独立)

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 整体强度 0~1 (兼作淡入淡出)
float4 uColorCore;   // 芯色 (rgb, a=芯部不透明度权重)
float4 uColorEdge;   // 缘色 (rgb, a=缘部不透明度权重)
float  uHeat;        // 0~1 热度: 芯部向白热/致命色偏移 (蓄势→出鞘用)
float  uFlowSpeed;   // 流动速度
float  uFlowScale;   // 流动纹理尺度
float  uCoreSharp;   // 芯部收窄锐度 (1~5)
float  uTaper;       // 尾端衰减幂 (1=线性, 2~3=快收)

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 PS_BladeRibbon(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;

    // 横向: 0=中心线(刃芯) 1=两侧边缘
    float edgeDist = abs(uv.y - 0.5) * 2.0;
    float coreProfile = pow(saturate(1.0 - edgeDist), max(uCoreSharp, 0.001));
    float bodyProfile = saturate(1.0 - edgeDist * edgeDist);

    // 沿长流动: 双八度噪声制造"刃气拉丝"
    float2 f1 = float2(uv.x * max(uFlowScale, 0.001) - uTime * uFlowSpeed, uv.y * 0.35);
    float2 f2 = float2(uv.x * max(uFlowScale, 0.001) * 2.3 - uTime * uFlowSpeed * 1.6, uv.y * 0.2 + 0.37);
    float flow = tex2D(uNoise, f1).r * 0.65 + tex2D(uNoise, f2).g * 0.35;

    // 刃光纹理叠底 (横向拉丝感)
    float slash = tex2D(uImage0, float2(uv.x * 0.9 - uTime * uFlowSpeed * 0.5, uv.y)).r;

    // 锯齿明灭: 沿长高频行波, 呼应锯齿冥刃
    float serr = 0.82 + 0.28 * sin(uv.x * 34.0 - uTime * 10.0 + flow * 5.0);

    // 尾端衰减 (uv.x=0 刃头, 1 尾), 头部略收口
    float fade = pow(saturate(1.0 - uv.x), max(uTaper, 0.2));
    fade *= smoothstep(0.0, 0.03, uv.x) * 0.35 + 0.65;

    // 芯↔缘渐变, 热度把芯部推向白热
    float3 col = lerp(uColorEdge.rgb, uColorCore.rgb, coreProfile);
    float3 hot = lerp(uColorCore.rgb, float3(1.0, 0.92, 0.85), 0.75);
    col = lerp(col, hot, saturate(uHeat) * coreProfile);
    col *= (0.55 + 0.75 * flow) * serr;
    col += uColorCore.rgb * coreProfile * coreProfile * (0.5 + uHeat * 0.9); // 芯部加法过曝
    col *= 0.75 + 0.5 * slash;

    float alpha = bodyProfile * fade * uIntensity;
    alpha *= lerp(uColorEdge.a, uColorCore.a, coreProfile);

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass BladeRibbonPass
    {
        PixelShader = compile ps_3_0 PS_BladeRibbon();
    }
}
