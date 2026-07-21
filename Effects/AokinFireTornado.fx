// ============================================================
// 敖钦火龙卷着色器 — 程序化炎旋柱
// 双螺旋条纹旋转 + FBM 边缘撕裂 + 中轴金芯 + 点燃闪
// 一次 quad 绘制替代旧版 162 段贴图叠绘 (封路龙卷 / 炎龙卷舞共用)
// UV 约定: x=横向 0~1, y=0 顶端 → 1 底部
// 完全自包含程序化噪声 (s0 仅占位)
// ============================================================

sampler uTexture : register(s0); // 占位, 不采样

float uTime;      // 动画时间(秒)
float uIntensity; // 整体强度 0~1 (兼作淡入淡出)
float uIgnite;    // 点燃闪 0~1 (相变点燃时脉冲)
float uSeed;      // 每龙卷相位差
float uSpin;      // 旋转速度系数(默认1)

static const float3 EmberDark = float3(0.38, 0.06, 0.03);
static const float3 FlameRed  = float3(0.92, 0.22, 0.08);
static const float3 FlameOrg  = float3(1.00, 0.50, 0.10);
static const float3 FlameGold = float3(1.00, 0.84, 0.34);
static const float3 FlashWht  = float3(1.00, 0.96, 0.86);

// ---------- 程序化噪声 ----------
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

float fbm3(float2 p)
{
    float v = 0.0;
    v += valueNoise(p) * 0.55;
    v += valueNoise(p * 2.13 + 1.7) * 0.30;
    v += valueNoise(p * 4.29 + 3.9) * 0.15;
    return v;
}

// ---------- 像素着色器 ----------
float4 TornadoPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float t = uTime * max(uSpin, 0.001) + uSeed * 11.3;

    // 漏斗形: 顶部宽 底部略收, 随高度左右摇摆
    float sway = (fbm3(float2(uv.y * 1.6 + uSeed, t * 0.35)) - 0.5) * 0.16 * (1.0 - uv.y * 0.4);
    float cx = uv.x - 0.5 - sway;
    float halfW = lerp(0.42, 0.24, uv.y);

    // 边缘撕裂
    float rip = (fbm3(float2(uv.x * 2.5 + uSeed * 5.0, uv.y * 3.0 - t * 1.2)) - 0.5) * 0.18;
    float d = abs(cx) - (halfW + rip);
    float shape = smoothstep(0.03, -0.09, d);
    if (shape < 0.01)
        return float4(0, 0, 0, 0);

    // 归一化横向位置(-1~1, 相对当前半宽)
    float nx = cx / max(halfW, 0.001);

    // 双螺旋条纹: 两族相位相反的斜向亮带, 卷动感来自沿 y 的相位滚动
    float spiral1 = sin((uv.y * 9.0 - t * 3.4 + nx * 1.8) * 3.14159);
    float spiral2 = sin((uv.y * 13.0 + t * 4.1 - nx * 2.2) * 3.14159 + 1.7);
    float stripes = saturate(spiral1 * 0.5 + 0.5) * 0.6 + saturate(spiral2 * 0.5 + 0.5) * 0.4;
    stripes = pow(stripes, 2.2);

    // 内部翻腾焰体
    float body = fbm3(float2(nx * 1.4 + uSeed, uv.y * 2.4 - t * 1.5));

    // 圆柱明暗: 中轴亮, 两侧暗(伪体积)
    float axial = 1.0 - abs(nx);
    axial = pow(saturate(axial), 1.4);

    // ---------- 颜色 ----------
    float3 col = EmberDark;
    col = lerp(col, FlameRed, saturate(body * 0.9 + 0.15));
    col = lerp(col, FlameOrg, saturate(stripes * 1.1) * (0.4 + axial * 0.6));
    col = lerp(col, FlameGold, saturate(pow(axial, 2.5) * (0.5 + stripes * 0.6)));

    // 底部基座熔光
    float baseGlow = smoothstep(0.72, 1.0, uv.y);
    col += FlameOrg * baseGlow * 0.5;

    // 顶部散逸变暗变透
    float topFade = smoothstep(0.0, 0.22, uv.y);

    // 点燃闪: 全体提亮 + 白闪
    col = lerp(col, FlashWht, uIgnite * 0.45 * (0.4 + stripes * 0.6));
    col *= 1.0 + uIgnite * 0.8;

    // ---------- Alpha ----------
    float alpha = shape * topFade * uIntensity;
    alpha *= lerp(0.55, 1.0, axial);
    alpha *= 0.55 + body * 0.45;

    return float4(saturate(col) * saturate(alpha), saturate(alpha));
}

technique Technique1
{
    pass TornadoPass
    {
        PixelShader = compile ps_3_0 TornadoPS();
    }
}
