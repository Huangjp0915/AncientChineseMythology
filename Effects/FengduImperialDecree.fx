// ============================================================
// 酆都帝诏卷轴带 — TriangleStrip 直带图元 (酆都武器系列专属)
// 黑紫缎底 + 两侧帝金边 + 中央方印符文列 + 展开前沿金亮线
// 顶点契约同 BeamGrad: BuildRibbonStrip 两点直带, uv.x=沿长(0=卷首) uv.y=横宽 0~1
// 仅 PS, 顶点变换复用 SpriteBatch VS (GameViewMatrix); 由 FengduVFX.DrawDecreeBand 调用
// 喂可平铺噪声(s0)
// ============================================================

sampler uImage0 : register(s0); // 可平铺三通道噪声 (符文源)

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 整体强度 0~1
float  uUnroll;      // 展开进度 0~1 (uv.x > uUnroll 的部分未展开不可见)
float4 uColorSilk;   // 缎底色 (黑紫, a=底不透明权重)
float4 uColorTrim;   // 金边色
float4 uColorGlyph;  // 符文色
float  uGlyphFreq;   // 符文列频率 (沿长格数, 建议 8~16)
float  uSeed;        // 符文随机种子

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 FengduDecreePS(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;

    // 展开裁剪: 前沿之外不可见
    if (uv.x > uUnroll + 0.02)
        return float4(0, 0, 0, 0);

    // 已展开区域权重 + 展开前沿金亮线 (0→1→0 bump)
    float unrolled  = smoothstep(uUnroll + 0.015, uUnroll - 0.05, uv.x);
    float frontGlow = smoothstep(uUnroll - 0.07, uUnroll - 0.01, uv.x)
                    * smoothstep(uUnroll + 0.02, uUnroll - 0.005, uv.x);

    float edgeDist = abs(uv.y - 0.5) * 2.0; // 0=中轴 1=边

    // 缎底 (边缘收口)
    float silk = 1.0 - smoothstep(0.86, 1.0, edgeDist);

    // 两侧金边条 (~78%-94% 处)
    float trim = smoothstep(0.74, 0.84, edgeDist) * (1.0 - smoothstep(0.92, 1.0, edgeDist));

    // 卷首端头收口 (避免生硬切边)
    float capIn = smoothstep(0.0, 0.05, uv.x);

    // 中央方印符文列: 网格化 + 噪声阈值 → 玺印块
    float glyphZone = 1.0 - smoothstep(0.42, 0.58, edgeDist);
    float gy = uv.x * max(uGlyphFreq, 0.001);
    float2 cell = frac(float2(uv.y * 3.0, gy));
    float cellMask = step(0.18, cell.x) * step(cell.x, 0.82)
                   * step(0.14, cell.y) * step(cell.y, 0.86);
    float2 cellId = float2(floor(uv.y * 3.0), floor(gy));
    float g = tex2D(uImage0, cellId * 0.173 + float2(uSeed, uSeed * 0.7)).r;
    float glyph = step(0.52, g) * cellMask * glyphZone;

    // 符文微光呼吸 + 沿带流光
    float glyphPulse = 0.75 + 0.25 * sin(uTime * 3.0 + gy * 2.4 + uSeed * 6.28318);
    float sheenN = tex2D(uImage0, float2(uv.x * 1.4 - uTime * 0.35, uv.y * 0.5)).b;
    float sheen = smoothstep(0.62, 0.95, sheenN) * silk * 0.22;

    float3 col = uColorSilk.rgb * silk
               + uColorTrim.rgb * (trim * 1.25 + frontGlow * 1.7 + sheen)
               + uColorGlyph.rgb * glyph * glyphPulse;

    float alpha = (silk * uColorSilk.a + trim * 0.95 + glyph * 0.9 + frontGlow) * capIn * unrolled;
    alpha = saturate(alpha) * uIntensity;

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(col, saturate(alpha));
}

technique Technique1
{
    pass FengduDecreePass
    {
        PixelShader = compile ps_3_0 FengduDecreePS();
    }
}
