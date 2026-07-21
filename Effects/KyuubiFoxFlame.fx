// ============================================================
// 九尾狐 · 灵魂狐火着色器 — 精灵量子火焰
// 载体: 以共享噪声贴图为 s0 的四边形 (SpriteBatch Immediate 绘制)
// 泪滴 SDF 火形 + FBM 沿轴流动 + 双色温渐变 + 逐实例相位
// 局部约定: 火焰底部在 uv(0.5, 0.88), 尖端朝 uv.y=0 (绘制时用 rotation 对准世界方向)
// 用于: 尾尖狐火 / 狐火弹核心 / 天灯灯笼
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;       // 动画时间(秒)
float  uIntensity;  // 整体强度 0~1
float4 uColorCore;  // 芯色 (近白高温)
float4 uColorEdge;  // 边色 (主题色: 金焰/紫红)
float  uSeed;       // 逐实例相位 (0~10, 打破同步感)
float  uTall;       // 火舌高瘦度 0.6(圆团)~1.6(细长)

float4 FoxFlamePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    // 局部火焰坐标: h=0 底部 → h=1 尖端, x 横向偏离
    float h = saturate((0.88 - coords.y) / 0.78);
    float x = coords.x - 0.5;

    // FBM 沿轴上行流动 (双通道错频, 尖端扰动更大)
    float2 flowUV1 = float2(coords.x * 1.6 + uSeed * 0.37, coords.y * 2.2 - uTime * 1.15 + uSeed);
    float2 flowUV2 = float2(coords.x * 3.1 - uSeed * 0.61, coords.y * 3.9 - uTime * 1.8 + uSeed * 2.3);
    float n1 = tex2D(uImage0, flowUV1).r;
    float n2 = tex2D(uImage0, flowUV2).g;
    float fbm = n1 * 0.65 + n2 * 0.35;

    // 尖端摇曳: 噪声驱动的横向弯曲 (根部稳、尖端摆)
    float sway = (fbm - 0.5) * 0.34 * h * h;
    float xs = x - sway;

    // 泪滴宽度轮廓: 根部圆润 → 尖端收窄
    float width = 0.30 * sqrt(max(h * (1.0 - h) * (1.0 - h * 0.55), 0.0)) + 0.015;
    width /= max(uTall, 0.35);

    // 径向火体 (噪声侵蚀边缘)
    float body = 1.0 - smoothstep(width * 0.35, width, abs(xs));
    body *= smoothstep(0.0, 0.12, h) * (1.0 - smoothstep(0.82, 1.0, h));
    body *= saturate(0.55 + fbm * 0.9);

    if (body < 0.01)
        return float4(0, 0, 0, 0);

    // 双色温: 芯白热 → 边缘主题色 (核心随 fbm 呼吸)
    float core = pow(saturate(body), 3.0) * saturate(1.4 - h);
    float3 col = lerp(uColorEdge.rgb, uColorCore.rgb, saturate(core * 1.3));

    // 逐实例闪烁 (轻微, 不破坏可读性)
    float flicker = 0.88 + 0.12 * sin(uTime * 9.0 + uSeed * 5.1);

    float a = saturate(body * flicker) * uIntensity;
    return float4(col * a, a) * sampleColor;
}

technique Technique1
{
    pass FoxFlamePass
    {
        PixelShader = compile ps_3_0 FoxFlamePS();
    }
}
