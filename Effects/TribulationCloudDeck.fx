// ============================================================
// 劫云云盖着色器 — 翻滚活云 + 云内电光散射 (TribulationCloud 专属)
// fbm 域扭曲云形, 底缘噪声撕裂, 顶/侧羽化;
// uFlash/uFlashX: 云内电光在指定横位散射照亮云体(充能/预闪);
// uBreak: 结算时云盖从中心裂开(锯齿裂口 + 裂缘辉光);
// uDissolve: 消散离场(噪声阈值溶解, 边缘发亮)
// 世界空间宽幅 quad, UV: x=横向 0~1, y=0 顶 1 底
// 预乘 Alpha (BlendState.AlphaBlend); 完全程序化, s0 占位不采样
// ============================================================

sampler uImage0 : register(s0); // 占位, 不采样

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 整体不透明度 0~1 (聚云进度兼淡入)
float  uSeed;        // 本场随机种子
float4 uColor;       // 风暴主题色 (云体中调)
float4 uColorDark;   // 云底深色 (压顶的暗)
float  uFlash;       // 云内电光强度 0~1
float  uFlashX;      // 电光横位 0~1
float4 uFlashColor;  // 电光色 (青白 / 终雷可偏主题色)
float  uBreak;       // 云盖裂开进度 0~1 (成功结算)
float4 uBreakColor;  // 裂口天光色 (金)
float  uDissolve;    // 消散进度 0~1

float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++)
    {
        v += valueNoise(p) * a;
        p = p * 2.03 + float2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

float4 CloudDeckPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    // 云盖横宽 ~3.7:1, 噪声域按比例拉伸避免云团被压扁
    float2 p = float2(uv.x * 3.7, uv.y) + uSeed * 17.0;

    // 域扭曲: 云的翻滚 (两层反向漂移)
    float2 warp;
    warp.x = fbm(p * 1.6 + float2(uTime * 0.05, 0.0));
    warp.y = fbm(p * 1.6 + float2(-uTime * 0.04, 3.1));
    float2 q = p + (warp - 0.5) * 0.9;
    float den = fbm(q * 1.15 + float2(uTime * 0.03, uTime * 0.012));

    // 外形包络: 左右羽化 + 顶部薄 + 底缘噪声撕裂
    float edgeX = smoothstep(0.0, 0.16, uv.x) * smoothstep(1.0, 0.84, uv.x);
    float tear = fbm(float2(p.x * 1.35, uSeed * 3.0) + float2(uTime * 0.02, 0.0));
    float bottom = smoothstep(1.0, 0.62 + tear * 0.22, uv.y);
    float top = smoothstep(0.0, 0.28, uv.y);
    float shape = edgeX * bottom * top;

    float density = smoothstep(0.28, 0.72, den) * shape;

    // —— 结算裂口: 从中心向两侧撕开 ——
    float gapHalf = uBreak * 0.34;
    float crack = abs(uv.x - 0.5) + (fbm(float2(uv.y * 3.0, uSeed * 7.0 + uv.x * 5.0)) - 0.5) * 0.10;
    float gapMask = smoothstep(gapHalf - 0.03, gapHalf + 0.05, crack); // 0=裂口内
    // 裂缘天光 (只在裂开时出现)
    float rim = exp(-abs(crack - gapHalf) * 26.0) * step(0.001, uBreak);
    density *= gapMask;

    // —— 消散: 噪声阈值溶解, 边缘短暂发亮 ——
    float disNoise = fbm(p * 2.1 + uSeed * 5.0);
    float disMask = smoothstep(uDissolve - 0.12, uDissolve + 0.02, disNoise + 0.02);
    float disEdge = exp(-abs(disNoise - uDissolve) * 18.0) * step(0.02, uDissolve) * step(uDissolve, 0.96);
    density *= disMask;

    if (density < 0.01 && rim < 0.01)
        return float4(0, 0, 0, 0);

    // —— 体积明暗: 深处暗、边缘亮; 云底最深 ——
    float inner = fbm(q * 2.3 + 4.7);
    float3 col = lerp(uColorDark.rgb * 0.55, uColor.rgb * 0.85, inner * 0.8 + 0.1);
    col = lerp(col, uColorDark.rgb * 0.35, uv.y * 0.65); // 底部压暗

    // —— 云内电光散射: 距 uFlashX 的横向衰减 + 噪声透光, 照亮云体 ——
    float fd = abs(uv.x - uFlashX);
    float scatter = exp(-fd * fd * 22.0) * uFlash;
    float translucence = smoothstep(0.2, 0.9, inner); // 云薄处透光更强
    col += uFlashColor.rgb * scatter * (0.45 + translucence * 1.15);
    // 电光也提亮云底撕裂缘 (被内部照亮的轮廓)
    float rimLight = (1.0 - bottom) * edgeX * scatter;
    col += uFlashColor.rgb * rimLight * 0.8;

    // 裂口天光
    col += uBreakColor.rgb * rim * (1.2 + 0.3 * sin(uTime * 3.0));
    // 消散边缘余光
    col += uColor.rgb * disEdge * 0.8;

    float a = saturate(density * (0.82 + scatter * 0.18) + rim * 0.55);
    a *= uIntensity;

    // 预乘输出
    return float4(saturate(col) * a, a);
}

technique Technique1
{
    pass CloudDeckPass
    {
        PixelShader = compile ps_3_0 CloudDeckPS();
    }
}
