// ============================================================
// 青龙程序化分叉闪电 — 屏幕空间 decal (参考 ArenaRunic 调用方式)
// 主干 SDF 折线(噪声域扭曲) + 二级分支(噪声门控鬼影) + 闪频抖动 + 端点冲击辉光
// 用于: P2 相变劈雷 / 雷柱释放帧增强 / 死亡演出递进落雷 / 天幕闪电
// s0 = 共享可平铺噪声 (ACMShaders.NoiseTexture, RGB 三通道独立)
// 以 Additive 混合满屏绘制; uStart/uEnd 为归一化屏幕 UV
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB)

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 总强度 0~1
float2 uStart;       // 起点 UV (0~1)
float2 uEnd;         // 终点 UV (0~1)
float  uAspect;      // 宽高比 width/height
float4 uColor;       // 主色 (青白雷)
float  uSeed;        // 每道闪电种子 (换形状)
float  uBranch;      // 分支强度 0~1
float  uFlash;       // 环境泛亮 0~1 (全屏微光)
float  uThickness;   // 主干粗细 (UV 高度比, 建议 0.004~0.012)

// 像素到「噪声扭曲折线」的距离场
// amp=扭曲幅度, freqMul=折线频率倍率, tQ=时间量化重掷项
float BoltDist(float2 p, float2 a, float2 b, float seed, float amp, float freqMul, float tQ)
{
    float2 ab = b - a;
    float abLen = max(length(ab), 1e-4);
    float2 dir = ab / abLen;
    float2 nrm = float2(-dir.y, dir.x);
    float2 ap = p - a;
    float t = saturate(dot(ap, dir) / abLen);
    float dPerp = dot(ap, nrm);

    // 端点钉扎包络: 起终点收拢, 中段自由扭曲
    float env = pow(sin(t * 3.14159), 0.6);

    // 三八度噪声折线 (tQ 随时间重掷 → 闪电形状抖动)
    float n1 = tex2D(uImage0, float2(t * 1.7 * freqMul + seed * 3.71 + tQ,       seed * 0.37)).r - 0.5;
    float n2 = tex2D(uImage0, float2(t * 4.3 * freqMul + seed * 7.13 - tQ * 1.7, seed * 0.71 + 0.33)).g - 0.5;
    float n3 = tex2D(uImage0, float2(t * 9.1 * freqMul + seed * 11.7 + tQ * 2.3, seed * 1.13 + 0.67)).b - 0.5;
    float wob = (n1 * 0.62 + n2 * 0.27 + n3 * 0.11) * amp * env;

    return abs(dPerp - wob);
}

float4 LightningPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 p = float2(coords.x * uAspect, coords.y);
    float2 a = float2(uStart.x * uAspect, uStart.y);
    float2 b = float2(uEnd.x * uAspect, uEnd.y);

    // 每 1/24 秒重掷折线形状 (电弧跳变感)
    float tQ = floor(uTime * 24.0) * 0.377;

    float th = max(uThickness, 0.001);

    // 主干: 反比辉光核 + 宽柔光
    float dMain = BoltDist(p, a, b, uSeed, 0.10, 1.0, tQ);
    float core = pow(th / (dMain + th), 2.6);
    float glow = pow(th * 6.0 / (dMain + th * 6.0), 2.0) * 0.35;

    // 二级分支: 两条更细更抖的鬼影折线, 沿主干被噪声分段门控
    float2 ab = b - a;
    float t0 = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-5));
    float gate1 = step(0.45, tex2D(uImage0, float2(t0 * 2.0 + uSeed,  uSeed * 0.5 + tQ * 0.10)).r);
    float gate2 = step(0.55, tex2D(uImage0, float2(t0 * 3.0 - uSeed,  uSeed * 0.9 - tQ * 0.13)).g);
    float thB = th * 0.55;
    float dBr1 = BoltDist(p, a, b, uSeed + 4.7, 0.22, 2.3, tQ);
    float dBr2 = BoltDist(p, a, b, uSeed + 9.2, 0.30, 3.1, tQ + 0.19);
    float br1 = pow(thB / (dBr1 + thB), 2.6) * gate1;
    float br2 = pow(thB / (dBr2 + thB), 2.6) * gate2;
    float branches = (br1 + br2 * 0.8) * uBranch;

    // 闪频抖动
    float flick = 0.78 + 0.22 * sin(uTime * 87.0 + uSeed * 21.0);

    // 终点冲击辉光 (落点最亮)
    float impact = pow(saturate(1.0 - length(p - b) / 0.14), 2.4) * 0.8;

    float strength = (core + glow + branches + impact) * flick * uIntensity;

    // 主色 + 芯部提白
    float3 rgb = uColor.rgb * strength + float3(1.0, 1.0, 1.0) * (core + branches) * 0.55 * uIntensity;
    // 环境泛亮 (uFlash: 雷击瞬间整屏微光)
    rgb += uColor.rgb * uFlash * 0.10;

    return float4(rgb, saturate(strength));
}

technique Technique1
{
    pass LightningPass
    {
        PixelShader = compile ps_3_0 LightningPS();
    }
}
