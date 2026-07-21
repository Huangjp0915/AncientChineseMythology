// ============================================================
// 生肖符箓 — 符纸 / 朱印 双模着色器 (系列九件共用)
// uMode 0: 黄纸朱砂符 — 纸底+噪声毛边+焦边, 程序化符头三横/中脊/敕令纹,
//          uStroke 朱砂走线揭示 (笔画按书写序写就), uSpread 展开, uBurn 燃尽
// uMode 1: 朱印起爆 — 方印双框+印文格+径向裂纹+白闪
// 载体: s0 = 共享噪声 (整图绘制, UV 0~1); LinearWrap 采样
// 输出预乘 alpha (纸走 AlphaBlend, 印走 Additive 均可)
// ============================================================

sampler uNoise : register(s0);

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 整体强度 0~1
float  uMode;        // 0=符纸 1=朱印
float  uStroke;      // 笔画走线进度 0~1
float  uBurn;        // 燃尽进度 0~1 (边缘向内)
float  uSpread;      // 展开进度 0~1 (由上向下)
float  uFlash;       // 白闪 (朱印起爆帧)
float  uCharmId;     // 生肖编号 0~8 (笔画布局哈希)
float4 uPaperColor;  // 纸底色 (黄纸)
float4 uInkColor;    // 朱砂/印泥色

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    return frac(p * (p + p));
}

// 圆头线段笔画: p 到线段 ab 距离 → 0~1 亮度
float stroke(float2 p, float2 a, float2 b, float w)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-5));
    float d = length(pa - ba * h);
    return 1.0 - smoothstep(w * 0.55, w, d);
}

// 书写序揭示: 该笔占走线区间 [ord0, ord1]
float reveal(float ord0, float ord1)
{
    return smoothstep(ord0, ord1, uStroke);
}

float4 CharmPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.005)
        return float4(0, 0, 0, 0);

    float2 c = uv - 0.5;
    float idh = hash11(uCharmId * 7.31 + 1.7);

    // ================= 朱印模式 =================
    if (uMode > 0.5)
    {
        float dSq = max(abs(c.x), abs(c.y)) / 0.40;   // 方形 (chebyshev) 距离
        float n = tex2D(uNoise, uv * 2.0 + idh).r;

        // 印框: 外粗内细双线
        float frame   = smoothstep(0.86, 0.92, dSq) * (1.0 - smoothstep(0.98, 1.04, dSq));
        float frameIn = smoothstep(0.70, 0.74, dSq) * (1.0 - smoothstep(0.76, 0.82, dSq));

        // 印文: 2x2 格伪篆字 (双向拉伸噪声阈值成横竖笔画块)
        float2 cell = floor((c + 0.30) / 0.30);
        float2 cuv  = frac((c + 0.30) / 0.30);
        float g1 = tex2D(uNoise, cell * 0.37 + cuv * float2(0.55, 0.16) + uCharmId * 0.13).r;
        float g2 = tex2D(uNoise, cell * 0.71 + cuv * float2(0.14, 0.52) + uCharmId * 0.29).g;
        float glyph = step(0.52, max(g1, g2)) * step(dSq, 0.62)
                    * step(0.06, cuv.x) * step(cuv.x, 0.94)
                    * step(0.06, cuv.y) * step(cuv.y, 0.94);

        // 径向裂纹 (盖印飞溅, 长短随扇区哈希)
        float ang = atan2(c.y, c.x);
        float rad = length(c) / 0.5;
        float sharp = 24.0 - 18.0 * hash11(floor(ang * 4.0) + uCharmId);
        float crack = pow(abs(sin(ang * 7.0 + uCharmId)), sharp)
                    * smoothstep(1.5, 0.85, rad) * smoothstep(0.55, 0.75, rad);

        float shape = max(frame, max(frameIn * 0.6, max(glyph * 0.95, crack * 0.8)));
        float3 col = uInkColor.rgb * (0.75 + 0.25 * n);
        col = lerp(col, float3(1.0, 0.97, 0.9), saturate(uFlash) * 0.85);
        shape += uFlash * pow(saturate(1.0 - rad), 1.6) * 1.6;

        float alphaS = saturate(shape) * uIntensity;
        return float4(col * alphaS, alphaS);
    }

    // ================= 符纸模式 =================
    // 竖长条纸形 + 噪声毛边
    float2 e = abs(c) - float2(0.30, 0.455);
    float edge = max(e.x, e.y);                       // <0 = 纸内
    float tear = (tex2D(uNoise, uv * 3.4 + uCharmId * 0.61).r - 0.5) * 0.035;
    edge += tear;
    if (edge > 0.0)
        return float4(0, 0, 0, 0);

    // 展开: 由上向下揭示
    float unroll = uv.y - uSpread * 1.25;
    if (unroll > 0.0)
        return float4(0, 0, 0, 0);
    float unrollFront = smoothstep(-0.06, 0.0, unroll) * step(uSpread, 0.98);

    // 燃尽: 噪声场阈值推进, 边缘先烧
    float bn = tex2D(uNoise, uv * 2.6 + uCharmId * 0.37).g;
    float burnField = bn * 0.55 - edge * 2.2;
    float s = burnField - uBurn * 1.85;
    if (s < -0.10)
        return float4(0, 0, 0, 0);
    float body = smoothstep(-0.10, 0.02, s);
    float emberGlow = (1.0 - smoothstep(0.0, 0.16, s)) * step(0.005, uBurn);

    // 纸底: 黄纸 + 纵向纤维纹
    float grain = tex2D(uNoise, uv * float2(2.0, 6.0)).b;
    float3 col = uPaperColor.rgb * (0.88 + 0.17 * grain);
    // 焦边
    float scorch = smoothstep(-0.05, 0.0, edge);
    col = lerp(col, float3(0.24, 0.13, 0.07), scorch * 0.8);

    // —— 朱砂笔画 (纸空间, 按书写序揭示) ——
    float2 p = c;
    float wob = (tex2D(uNoise, float2(uv.y * 1.7, uCharmId * 0.5)).r - 0.5) * 0.03;
    float ink = 0.0;

    // 符头: 三清三横 (0~0.25)
    ink = max(ink, stroke(p, float2(-0.17, -0.360), float2(0.17, -0.365), 0.030) * reveal(0.00, 0.08));
    ink = max(ink, stroke(p, float2(-0.14, -0.300), float2(0.14, -0.295), 0.026) * reveal(0.08, 0.16));
    ink = max(ink, stroke(p, float2(-0.11, -0.240), float2(0.11, -0.245), 0.024) * reveal(0.16, 0.25));

    // 中脊主竖 (0.25~0.55), 手写摆动
    ink = max(ink, stroke(p - float2(wob, 0.0), float2(0.0, -0.20), float2(0.0, 0.16), 0.034) * reveal(0.25, 0.55));

    // 敕令纹: 生肖各异的横斜短画 (0.55~0.85)
    float y0 = -0.10 + hash11(uCharmId + 3.1) * 0.08;
    float y1 =  0.02 + hash11(uCharmId + 5.7) * 0.07;
    float x0 =  0.05 + hash11(uCharmId + 9.2) * 0.10;
    ink = max(ink, stroke(p, float2(-x0, y0), float2(x0, y0 + 0.03), 0.024) * reveal(0.55, 0.65));
    ink = max(ink, stroke(p, float2(x0 * 0.8, y1), float2(-x0 * 0.8, y1 + 0.05), 0.022) * reveal(0.65, 0.75));
    ink = max(ink, stroke(p, float2(-0.06, 0.10 + wob), float2(0.06, 0.16), 0.024) * reveal(0.75, 0.85));

    // 底部收笔两斜 (0.85~1.0)
    ink = max(ink, stroke(p, float2(0.0, 0.22), float2(-0.10, 0.36), 0.028) * reveal(0.85, 0.93));
    ink = max(ink, stroke(p, float2(0.0, 0.22), float2( 0.10, 0.36), 0.028) * reveal(0.93, 1.00));

    // 笔尖行进辉光 (正在写的位置)
    float tipY = lerp(-0.40, 0.36, uStroke);
    float tip = saturate(1.0 - abs(c.y - tipY) * 8.0) * step(uStroke, 0.995) * step(0.005, uStroke);

    col = lerp(col, uInkColor.rgb, saturate(ink) * 0.92);
    col += uInkColor.rgb * tip * 0.45;
    col = lerp(col, float3(1.0, 0.55, 0.15), emberGlow * 0.85);
    col += float3(1.0, 0.70, 0.30) * emberGlow * 0.5;
    col += float3(1.0, 0.90, 0.60) * unrollFront * 0.6;

    float alpha = uIntensity * body;
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass CharmPass
    {
        PixelShader = compile ps_3_0 CharmPS();
    }
}
