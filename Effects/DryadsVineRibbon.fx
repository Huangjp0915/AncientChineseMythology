// ============================================================
// 树精藤鞭/根须柱条带着色器 — TriangleStrip 直带 (图元绘制)
// 树皮纵纹 + 汁液流光 + 边缘暗沿 + 鞭梢高亮 + 枯萎褪色
// 顶点由 BuildRibbonStrip 提供 (uv.x=沿长 0=根 1=梢, uv.y=横宽 0~1)
// 仅 PS, 变换走外部矩阵 (同 XuanwuTrailRibbon); s0 = 共享可平铺噪声
// 建议 NonPremultiplied 混合 (藤体需遮挡背景, 非加性)
// ============================================================

sampler uNoise : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;       // 动画时间(秒)
float  uIntensity;  // 整体强度 0~1 (兼作生长/淡出)
float4 uColorBark;  // 树皮基色 (深绿褐, a=边缘不透明度)
float4 uColorCore;  // 芯部亮色 (翠绿, a=芯部不透明度)
float4 uColorGlow;  // 汁液流光/鞭梢高亮色
float  uTipGlow;    // 鞭梢高亮强度 0~1 (抽击瞬间打满, 平时~0.15)
float  uWither;     // 枯萎度 0~1 (凋落/死亡时向枯褐去饱和)
float  uFlowSpeed;  // 汁液流速 (建议 0.6~1.6)
float  uBarkScale;  // 树皮纹密度 (建议 3~8)

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 VineRibbonPS(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;
    float edgeDist = abs(uv.y - 0.5) * 2.0; // 0=中心线 1=边缘

    // —— 树皮纵纹: 沿长拉伸的条状噪声 → 纤维感 ——
    float bark  = tex2D(uNoise, float2(uv.x * uBarkScale, uv.y * 0.6 + uv.x * 0.15)).r;
    float fiber = tex2D(uNoise, float2(uv.x * uBarkScale * 2.7 + 3.1, uv.y * 1.2)).g;
    float barkField = bark * 0.65 + fiber * 0.35;

    // —— 汁液流光: 根→梢滚动的亮脉 ——
    float sap = tex2D(uNoise, float2(uv.x * 1.8 - uTime * uFlowSpeed, uv.y * 0.35 + 0.5)).b;
    float sapPulse = smoothstep(0.55, 0.85, sap);

    // —— 横剖面: 皮→芯 ——
    float coreProfile = pow(saturate(1.0 - edgeDist), 1.8);
    float3 col = lerp(uColorBark.rgb, uColorCore.rgb, coreProfile * (0.55 + barkField * 0.45));
    col += uColorGlow.rgb * sapPulse * coreProfile * 0.8;

    // —— 鞭梢高亮: 末段 30% 亮起 (抽击速度可读性) ——
    float tipMask = smoothstep(0.70, 1.0, uv.x);
    col += uColorGlow.rgb * tipMask * uTipGlow * 1.6;

    // —— 边缘暗沿: 保证剪影可读 ——
    float rim = smoothstep(0.55, 1.0, edgeDist);
    col = lerp(col, col * 0.35, rim);

    // —— 枯萎: 去饱和 + 转枯褐 ——
    float grey = dot(col, float3(0.299, 0.587, 0.114));
    float3 withered = lerp(float3(grey, grey, grey), float3(0.35, 0.25, 0.13), 0.55);
    col = lerp(col, withered, saturate(uWither));

    // —— alpha: 实心带; 树皮噪声在边缘啃出有机轮廓 ——
    float alpha = smoothstep(1.0, 0.78 - barkField * 0.18, edgeDist);
    alpha *= lerp(uColorBark.a, uColorCore.a, coreProfile);
    alpha *= uIntensity;

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass VineRibbonPass
    {
        PixelShader = compile ps_3_0 VineRibbonPS();
    }
}
