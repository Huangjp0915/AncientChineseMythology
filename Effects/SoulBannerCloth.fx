// ============================================================
// 万魂幡·灵绸布面 — TriangleStrip ribbon 单 pass (SoulBanner 专属)
// 幽紫布底渐变 + 双八度滚动织纹 + 流动符纹亮带 + 尾端噪声破边
// + uGrowth 鬼影面孔环纹显现 + uFlash 大招白闪
// 顶点由 BuildRibbonStrip 提供 (uv.x=0 锚端→1 尾端, uv.y=横宽 0~1)
// 仅 PS, 变换走外部矩阵 (同 BeamGrad); 建议 Additive 混合
// s1 = 共享可平铺噪声
// ============================================================

sampler uImage0 : register(s0); // 占位载体 (不采样, 保留槽位)
sampler uNoise  : register(s1); // 可平铺噪声

float  uTime;      // 动画时间(秒)
float  uIntensity; // 整体强度 0~1
float4 uColorDeep; // 布底深紫
float4 uColorLit;  // 亮缘幽紫
float  uGrowth;    // 0~1 成长比例 (鬼影面孔显现度)
float  uFlash;     // 0~1 大招白闪
float  uSeed;      // 实例随机种子 (错开噪声相位)

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 SoulBannerClothPS(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;

    // ── 织纹: 双八度噪声沿布长滚动 (灵绸在"流") ──
    float n1 = tex2D(uNoise, float2(uv.x * 1.5 - uTime * 0.35 + uSeed, uv.y * 0.8 + uSeed)).r;
    float n2 = tex2D(uNoise, float2(uv.x * 3.6 - uTime * 0.62, uv.y * 2.2 + uSeed * 2.0)).g;
    float weave = n1 * 0.65 + n2 * 0.35;

    // ── 横向剖面: 中央实、边缘收 ──
    float edgeDist = abs(uv.y - 0.5) * 2.0;
    float body = saturate(1.0 - edgeDist * edgeDist);

    // ── 尾端噪声破边: 越靠尾端越被噪声撕碎, 残留幽魂丝缕 ──
    float erode = smoothstep(0.12, 0.5, weave - max(uv.x - 0.5, 0.0) * 1.35);

    // ── 流动符纹亮带: 沿布长行进的锐利亮波 ──
    float rune = pow(saturate(sin(uv.x * 12.566 - uTime * 3.2 + uSeed * 6.0) * 0.5 + 0.5), 6.0);
    rune *= smoothstep(0.95, 0.35, edgeDist);

    // ── 边缘亮丝 (布幡镶边) ──
    float hem = smoothstep(0.22, 0.03, abs(edgeDist - 0.78));

    // ── 鬼影面孔: 低频噪声环纹, 随成长显现 ──
    float g = tex2D(uNoise, float2(uv.x * 0.85 - uTime * 0.07 + uSeed, uv.y * 0.55 + uSeed * 3.0)).b;
    float ghost = (smoothstep(0.55, 0.72, g) - smoothstep(0.78, 0.92, g)) * uGrowth;

    // ── 组色 ──
    float3 col = lerp(uColorDeep.rgb, uColorLit.rgb, saturate(weave * 0.55 + rune * 0.85));
    col += uColorLit.rgb * hem * 0.65;
    col += uColorLit.rgb * ghost * 1.25;
    col = lerp(col, float3(1.0, 1.0, 1.0), saturate(uFlash));

    float alpha = body * erode * (0.5 + 0.5 * weave);
    alpha = saturate(alpha + ghost * 0.35 + uFlash * body * 0.6);
    alpha *= uIntensity * lerp(uColorDeep.a, uColorLit.a, weave);

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(col * uIntensity, saturate(alpha));
}

technique Technique1
{
    pass SoulBannerClothPass
    {
        PixelShader = compile ps_3_0 SoulBannerClothPS();
    }
}
