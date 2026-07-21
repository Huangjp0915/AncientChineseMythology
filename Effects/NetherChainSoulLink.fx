// ============================================================
// 冥链魂锁条带着色器 — 牛头马面散件双件共用 (冥链刃 NetherChainBlade / 勾魂索 SoulHookWhip)
// 链节明暗周期 + 魂火流动噪声 + 勾魂行波亮斑 (uPulsePos 沿链 0~1)
// 顶点由 ACMUtils.BuildRibbonStrip 提供 (uv.x=沿长 0起点→1终点, uv.y=横宽 0~1)
// 仅 PS, 顶点变换沿用 SpriteBatch 外部矩阵 (同 BeamGrad/YingouBladeRibbon 约定)
// s0 = 可平铺噪声, s1 = 可平铺噪声 (与 BeamGrad 同绑法, 双槽同图即可)
// ============================================================

sampler uImage0 : register(s0); // 噪声 A
sampler uNoise  : register(s1); // 噪声 B (流动)

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 整体强度 0~1 (兼作淡入淡出)
float4 uColorCore;   // 芯色 (rgb, a=芯部不透明度权重) — 冥链刃青蓝 / 勾魂索幽紫
float4 uColorEdge;   // 缘色 (rgb, a=缘部不透明度权重)
float  uLinkCount;   // 链节数 (沿长明暗周期数)
float  uPulsePos;    // 勾魂行波位置 0~1 (<0 = 关闭)
float  uPulseGlow;   // 行波亮度 0~2
float  uFlowSpeed;   // 魂火流速
float  uEndFade;     // 两端收口宽度 (0.02~0.2)

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 PS_SoulChain(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;

    // 横向剖面: 0=链芯 1=边缘
    float edgeDist = abs(uv.y - 0.5) * 2.0;
    float coreProfile = pow(saturate(1.0 - edgeDist), 2.6);
    float bodyProfile = saturate(1.0 - edgeDist * edgeDist);

    // 链节明暗: 沿长周期, 节中心亮、节间隙暗 (锁链分节感)
    float linkWave = abs(sin(uv.x * max(uLinkCount, 1.0) * 3.14159));
    float link = 0.52 + 0.48 * pow(linkWave, 1.6);

    // 魂火流动: 双八度反向滚动 (幽魂拉丝)
    float2 f1 = float2(uv.x * 2.4 - uTime * uFlowSpeed, uv.y * 0.4 + 0.13);
    float2 f2 = float2(uv.x * 5.1 + uTime * uFlowSpeed * 0.6, uv.y * 0.25 + 0.61);
    float flow = tex2D(uNoise, f1).r * 0.62 + tex2D(uImage0, f2).g * 0.38;

    // 勾魂行波: 沿链跑动的白热亮斑
    float pulse = 0.0;
    if (uPulsePos >= 0.0) {
        float d = uv.x - uPulsePos;
        pulse = exp(-d * d * 240.0) * uPulseGlow;
    }

    // 两端收口 (起点接手/鞭根, 终点接刃/鞭梢, 各留一点余亮)
    float fade = smoothstep(0.0, max(uEndFade, 0.01), uv.x)
               * smoothstep(1.0, 1.0 - max(uEndFade, 0.01), uv.x);
    fade = fade * 0.85 + 0.15;

    // 芯↔缘渐变 + 链节调制 + 魂火
    float3 col = lerp(uColorEdge.rgb, uColorCore.rgb, coreProfile);
    col *= (0.55 + 0.75 * flow) * link;
    col += uColorCore.rgb * coreProfile * coreProfile * 0.55;          // 芯部过曝
    col += float3(0.85, 0.95, 1.0) * pulse * coreProfile;              // 行波白热

    float alpha = bodyProfile * fade * uIntensity;
    alpha *= lerp(uColorEdge.a, uColorCore.a, coreProfile);
    alpha += pulse * 0.35 * bodyProfile * uIntensity;

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass SoulChainPass
    {
        PixelShader = compile ps_3_0 PS_SoulChain();
    }
}
