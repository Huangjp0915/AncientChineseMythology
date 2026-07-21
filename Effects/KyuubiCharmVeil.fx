// ============================================================
// 九尾狐 · 魅影幻纱着色器 — 贴图鬼影/流光/溶解三合一
// s0 = 目标贴图 (本体/幻影), s1 = 可平铺噪声
// RGB 鬼影错位 (魅惑感) + 噪声流光扫过 + 溶解烧边 (幻影破除/入场显形/死亡消散)
// 用于: 狐影九重幻影 / 二阶段本体薄纱 / 入场逆溶解 / 死亡顺溶解
// ============================================================

sampler uImage0 : register(s0); // 目标贴图
sampler uNoise  : register(s1); // 可平铺噪声 (RGB 三通道独立)

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 整体可见度 0~1
float  uDissolve;    // 溶解进度 0(完整)~1(全消散)
float  uGhost;       // RGB 鬼影错位强度 (UV 单位, 0~0.03)
float4 uVeilColor;   // 纱色 (rgb=色调, a=染色权重)
float4 uEdgeColor;   // 溶解烧边色 (rgb=色, a=强度)
float  uSeed;        // 逐实例相位

float4 CharmVeilPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    // —— 溶解场: 噪声阈值 clip + 发光边 ——
    float2 nUV = coords * 2.3 + float2(uSeed * 0.71, uSeed * 0.37);
    float dissolveNoise = tex2D(uNoise, nUV).r * 0.7 + tex2D(uNoise, nUV * 2.1 + 0.3).g * 0.3;
    float threshold = uDissolve * 1.15;
    float edgeBand = 0.10;
    float survive = smoothstep(threshold - edgeBand, threshold, dissolveNoise + 0.075);
    if (survive < 0.004)
        return float4(0, 0, 0, 0);

    // —— RGB 鬼影错位: 错位方向缓慢旋转, 妖异不安定感 ——
    float ga = uTime * 1.7 + uSeed * 3.9;
    float2 gdir = float2(cos(ga), sin(ga)) * uGhost;
    float4 texC = tex2D(uImage0, coords);
    float  texR = tex2D(uImage0, coords + gdir).r;
    float  texB = tex2D(uImage0, coords - gdir).b;
    float4 col = float4(texR, texC.g, texB, texC.a);

    if (col.a < 0.01 && texC.a < 0.01)
        return float4(0, 0, 0, 0);

    // —— 噪声流光: 斜向扫过的亮带 (幻纱质感) ——
    float2 flowUV = float2(coords.x * 1.2 - uTime * 0.22 + uSeed, coords.y * 2.4 + uTime * 0.31);
    float flow = tex2D(uNoise, flowUV).b;
    float streak = smoothstep(0.55, 0.9, flow) * 0.55;

    // —— 纱色染调: 保亮度混向纱色 ——
    float lum = dot(col.rgb, float3(0.3, 0.59, 0.11));
    float3 veiled = lerp(col.rgb, uVeilColor.rgb * (lum + 0.35), uVeilColor.a);
    veiled += streak * uVeilColor.rgb;

    // —— 溶解烧边: 阈值边界处发光 ——
    float edge = smoothstep(threshold, threshold - edgeBand, dissolveNoise + 0.075)
               * smoothstep(threshold - edgeBand * 2.2, threshold - edgeBand, dissolveNoise + 0.075);
    veiled += uEdgeColor.rgb * edge * uEdgeColor.a * 2.2;

    float a = col.a * survive * uIntensity;
    return float4(veiled * a, a) * sampleColor;
}

technique Technique1
{
    pass CharmVeilPass
    {
        PixelShader = compile ps_3_0 CharmVeilPS();
    }
}
