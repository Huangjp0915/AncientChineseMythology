// ============================================================
// 八卦阵盘专属法阵着色器 (召唤法杖系列旗舰) — 程序化 SDF 八卦盘
//   双旋环 + 八卦爻纹 (先天卦序逐位点亮) + 阴阳双点 + 噪声微光
//   载体: BaGuaSigilProj 以加性 quad 绘制 (非全屏后处理, 不占全屏名额)
//   s0 = 共享可平铺噪声 (直接作为精灵贴图绘制, uv 跨 quad 0~1)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;            // 动画时间 (秒)
float  uIntensity;       // 全局强度 0~1 (展开/收拢淡入淡出)
float  uProgress;        // 展开进度 0~1: 驱动逐卦点亮 (一乾二兑三离四震五巽六坎七艮八坤)
float  uActive;          // 阵法激活态 0~1 (提亮 + 内环增强)
float  uSpin;            // 盘面自转角 (CPU 驱动, 激活态转更快)
float4 uColorPrimary;    // 主色 (金)
float4 uColorSecondary;  // 辅色 (玄青)

// 先天卦序按扇区低位起编码 (顶部起顺时针): 乾7 兑3 离5 震1 巽6 坎2 艮4 坤0
// K = Σ code_i * 8^i = 1139551 (< 2^24, float 精确)
static const float TRIGRAM_CODE = 1139551.0;

float4 BaGuaArrayPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 p = (coords - 0.5) * 2.0;   // -1 ~ 1, 外沿 r≈0.94
    float  r = length(p);
    if (r > 1.08)
        return float4(0, 0, 0, 0);

    float ang = atan2(p.y, p.x);

    // —— 噪声微光 (三通道 FBM, 慢速漂移) ——
    float2 nUV = coords * 1.6 + float2(uTime * 0.03, -uTime * 0.02);
    float  n   = tex2D(uImage0, nUV).r * 0.6 + tex2D(uImage0, coords * 3.1 - uTime * 0.015).g * 0.4;

    // —— 外边界环 (随盘正转的符线) ——
    float outerRing = smoothstep(0.055, 0.012, abs(r - 0.94));
    float angNorm   = ang / 6.28318 + 0.5;
    float runes     = tex2D(uImage0, float2(angNorm * 3.0 + uSpin * 0.12, 0.37)).b;
    outerRing *= 0.55 + 0.45 * smoothstep(0.35, 0.75, runes);

    // —— 内环 + 反转刻度 ——
    float innerRing = smoothstep(0.035, 0.008, abs(r - 0.52));
    float ticks = pow(abs(sin(ang * 16.0 + uSpin * 2.2)), 24.0);
    ticks *= smoothstep(0.50, 0.46, abs(r - 0.47) + 0.41);   // r ∈ ~[0.38, 0.56] 带
    ticks *= smoothstep(0.56, 0.50, r) * smoothstep(0.36, 0.42, r);
    innerRing = max(innerRing, ticks * (0.35 + 0.45 * uActive));

    // —— 八卦爻纹带 (r 0.62~0.84, 随盘自转) ——
    float a0 = ang + 1.5708 + uSpin;                        // 顶部为 0, 顺时针增
    float sectorPos = frac((a0 + 0.3927) / 6.28318) * 8.0;  // +半扇区使 0 号扇区居顶
    float idx = floor(sectorPos);
    float la  = sectorPos - idx - 0.5;                      // 扇区内切向坐标 -0.5~0.5

    // 卦码提取 (3 bit: b2=上爻最外, b0=下爻朝心)
    float digit = fmod(floor(TRIGRAM_CODE / exp2(3.0 * idx)), 8.0);
    float b2 = step(4.0, digit);
    float rem = digit - b2 * 4.0;
    float b1 = step(2.0, rem);
    float b0 = rem - b1 * 2.0;

    // 切向: 实爻整条 / 虚爻中断
    float absLa  = abs(la);
    float solid  = 1.0 - smoothstep(0.27, 0.33, absLa);
    float yinGap = smoothstep(0.055, 0.115, absLa);

    // 径向三爻带
    float bar0 = smoothstep(0.607, 0.625, r) * (1.0 - smoothstep(0.665, 0.683, r));
    float bar1 = smoothstep(0.687, 0.705, r) * (1.0 - smoothstep(0.745, 0.763, r));
    float bar2 = smoothstep(0.767, 0.785, r) * (1.0 - smoothstep(0.825, 0.843, r));

    float trigram = solid * (bar0 * lerp(yinGap, 1.0, b0)
                           + bar1 * lerp(yinGap, 1.0, b1)
                           + bar2 * lerp(yinGap, 1.0, b2));

    // 逐卦点亮 (一乾二兑三离…), 点亮瞬间白闪 bump
    float lit   = saturate(uProgress * 8.0 - idx);
    float flash = lit * (1.0 - lit) * 4.0;
    trigram *= 0.06 + 0.94 * lit;

    // —— 扇区分隔辐条 (内环 → 爻带) ——
    float spokes = pow(abs(sin(4.0 * a0)), 48.0);
    spokes *= smoothstep(0.50, 0.54, r) * (1.0 - smoothstep(0.58, 0.62, r)) * 0.55;

    // —— 中央阴阳双点互绕 + 心口柔光 ——
    float orbAng = -uSpin * 3.0;
    float2 pA = float2(cos(orbAng), sin(orbAng)) * 0.17;
    float2 pB = -pA;
    float dotA = pow(saturate(1.0 - length(p - pA) / 0.11), 2.0);
    float dotB = pow(saturate(1.0 - length(p - pB) / 0.11), 2.0);
    float core = pow(saturate(1.0 - r / 0.30), 2.5) * (0.18 + 0.22 * uActive);

    // —— 合成 ——
    float breath = 0.94 + 0.06 * sin(uTime * 2.1);
    float shimmer = 0.85 + 0.30 * n;

    float3 goldish = lerp(uColorSecondary.rgb, uColorPrimary.rgb, 0.78);
    float3 col = uColorPrimary.rgb * outerRing
               + uColorSecondary.rgb * (innerRing + spokes)
               + goldish * trigram
               + float3(1.0, 1.0, 1.0) * trigram * flash * 0.8
               + uColorPrimary.rgb * (dotA + core * 0.6)
               + uColorSecondary.rgb * dotB;

    float aSum = (outerRing + innerRing + spokes + trigram * (1.0 + flash) + dotA + dotB + core)
               * breath * shimmer;
    float bright = (0.78 + 0.42 * uActive) * uIntensity;

    // 加性预乘输出 (alpha 通道不用)
    return float4(col * saturate(aSum) * bright, 0.0) * sampleColor.a;
}

technique Technique1
{
    pass BaGuaArrayPass
    {
        PixelShader = compile ps_3_0 BaGuaArrayPS();
    }
}
