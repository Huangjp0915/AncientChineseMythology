// ============================================================
// 觉醒冥龙·魂焰着色器 — 鬼绿芯→觉醒紫缘的程序化冥火
// 双形态: uRound=0 火舌(泪滴包络, 沿 uFlameDir 流动)
//         uRound=1 魂雾场(径向翻涌, 领域地效)
// 完全自包含程序化噪声; 以 MagicPixel/任意占位纹理为载体,
// 建议 Additive 批量绘制 (由 AwakeningNetherScreenSystem 统一开合批)
// ============================================================

sampler uTexture : register(s0); // 占位载体(内容不参与运算)

float  uTime;       // 动画时间(秒)
float  uIntensity;  // 整体强度 0~1
float2 uFlameDir;   // 火舌流向(单位向量, 四边形局部空间)
float  uSeed;       // 实例随机种子
float  uRound;      // 0=火舌 1=径向魂雾场
float4 uCoreColor;  // 焰芯色(鬼绿)
float4 uEdgeColor;  // 焰缘色(觉醒紫)

// ---------------- 程序化噪声 ----------------
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
    v += valueNoise(p) * 0.50;
    v += valueNoise(p * 2.13 + 1.71) * 0.30;
    v += valueNoise(p * 4.27 + 3.19) * 0.20;
    return v;
}

float4 SoulflamePS(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.005)
        return float4(0, 0, 0, 0);

    float2 c = uv - 0.5;

    // 局部坐标: p.x 沿火舌流向, p.y 垂直
    float2 axisX = normalize(uFlameDir + float2(0.0001, 0));
    float2 axisY = float2(-axisX.y, axisX.x);
    float2 p = float2(dot(c, axisX), dot(c, axisY));

    // —— 火舌包络: 尾宽头尖的泪滴 ——
    float lon = saturate(p.x + 0.5);          // 0=尾 1=舌尖
    float lat = abs(p.y);
    float halfWidth = 0.36 * (1.0 - lon * lon) + 0.02;

    // 两层湍流, 沿流向平移(火焰向舌尖奔涌)
    float n1 = fbm3(float2(lon * 3.0 - uTime * 1.7 + uSeed * 13.7, p.y * 4.5 + uSeed * 7.1));
    float n2 = fbm3(float2(lon * 6.5 - uTime * 2.6 + uSeed * 5.3, p.y * 9.0 - uSeed * 3.9));
    float turb = (n1 - 0.5) * 0.16 + (n2 - 0.5) * 0.07;

    // 湍流越靠舌尖越撕裂
    float dTear = (lat + turb * (0.35 + lon)) / halfWidth;

    // —— 魂雾场包络: 径向翻涌 ——
    float radial = length(c) * 2.0;
    float swirl = fbm3(float2(atan2(c.y, c.x) * 0.95 + uTime * 0.22 + uSeed * 9.3,
                              radial * 2.4 - uTime * 0.5));
    float dField = radial / (0.82 + (swirl - 0.5) * 0.42);

    float d = lerp(dTear, dField, saturate(uRound));

    // 焰体与破碎边缘
    float flame = saturate(1.0 - d);
    float mask = smoothstep(0.12, 0.60, flame + (n1 - 0.5) * 0.30);
    if (mask < 0.004)
        return float4(0, 0, 0, 0);

    // 芯部: 越贴脊线越亮, 舌尾更热; 雾场模式芯部收敛于中心
    float core = pow(saturate(1.0 - d), 3.0) * lerp(1.0 - lon * 0.55, 1.0, uRound);

    float3 col = lerp(uEdgeColor.rgb, uCoreColor.rgb, saturate(core * 1.35));
    col += uCoreColor.rgb * pow(core, 2.5) * 0.85;   // 过曝焰心
    col += uEdgeColor.rgb * (n2 - 0.5) * 0.18;       // 缘部游焰

    float alpha = mask * uIntensity * (0.55 + core * 0.45);
    return float4(col * alpha, alpha);
}

technique Soulflame
{
    pass P0
    {
        PixelShader = compile ps_3_0 SoulflamePS();
    }
}
