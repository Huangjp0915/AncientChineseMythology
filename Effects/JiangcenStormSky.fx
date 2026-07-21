// ============================================================
// 将臣专属 — 雷暴天幕着色器 (全屏程序化, 对位 AncestralDragonSky 用法)
// 玄墨→暗血红渐变底 + 双层域扭曲雷暴云 + 云内放电 (cell 随机短亮)
// + 程序化远景闪电折线 (随机触发, 双通道) + 尸主血晕 (uBossUV)
// uPhase: 0=常态雷暴 1=雷狱相 (更暗更密, 放电更频)
// uFlash: 大节拍亮拍; uDeath: 死亡演出压暗 (天罚前的死寂)
// 完全程序化, 无外部噪声贴图依赖
// ============================================================

sampler uImage0 : register(s0); // 占位, 不使用

float  uTime;      // 累计时间(秒)
float  uIntensity; // 天幕可见度 0~1
float  uPhase;     // 0=常态 1=雷狱相
float  uAspect;    // 屏幕宽高比
float2 uBossUV;    // Boss 中心屏幕归一化坐标
float  uFlash;     // 大节拍亮拍 0~1.5 (雷牢合拢/军鼓/天罚)
float  uDeath;     // 死亡压暗 0~1

// 色板: 尸夜玄墨 × 暗血 × 雷青
static const float3 SkyTopA   = float3(0.020, 0.018, 0.048); // 常态顶: 玄墨蓝
static const float3 SkyBotA   = float3(0.150, 0.028, 0.045); // 常态地平: 暗血红
static const float3 SkyTopB   = float3(0.006, 0.010, 0.028); // 雷狱顶: 更深
static const float3 SkyBotB   = float3(0.055, 0.050, 0.115); // 雷狱地平: 电紫蓝
static const float3 CloudDark = float3(0.050, 0.055, 0.085); // 云体暗部
static const float3 CloudEdge = float3(0.300, 0.340, 0.440); // 云缘亮部
static const float3 Lightning = float3(0.700, 0.900, 1.000); // 雷青
static const float3 BloodRed  = float3(0.720, 0.095, 0.130); // 尸主血晕

float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float2 hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
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

float fbm4(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++)
    {
        v += valueNoise(p) * a;
        p = p * 2.07 + float2(1.7, 3.1);
        a *= 0.5;
    }
    return v;
}

// 轻量域扭曲: 一层 q 偏移 (雷云涡卷感, 控制指令数)
float warpedFbm(float2 p, float t)
{
    float2 q = float2(fbm4(p + float2(0.0, t * 0.05)),
                      fbm4(p + float2(5.2, t * 0.04 + 1.3)));
    return fbm4(p + 3.2 * q);
}

// 一道程序化远景闪电折线: 时间片触发, 折线路径由 valueNoise 弯折
// rate=触发节奏(片/秒), gate=本片出现概率阈值
float DistantBolt(float2 aspectUV, float2 uv, float rate, float gate, float seedSalt)
{
    float slice = floor(uTime * rate);
    float ft    = frac(uTime * rate);
    float seed  = hash21(float2(slice, seedSalt));
    if (seed < gate)
        return 0.0;

    // 落点 x + 沿 y 的折线弯曲 (低频大弯 + 高频细折)
    float bx = lerp(0.12, 0.88, hash21(float2(slice, seedSalt + 4.7))) * uAspect;
    float wobble = (valueNoise(float2(uv.y * 7.0, slice * 7.7 + seedSalt)) - 0.5) * 0.16
                 + (valueNoise(float2(uv.y * 30.0, slice * 3.3 + seedSalt)) - 0.5) * 0.05;
    float dx = abs(aspectUV.x - (bx + wobble));

    float core = exp(-dx * 320.0);
    float glow = exp(-dx * 36.0) * 0.35;

    // 快速衰减包络 + 帧内抖闪 (attack 快 decay 快 → "咔嚓"感)
    float env = pow(saturate(1.0 - ft * 1.6), 2.0) * (0.72 + 0.28 * sin(ft * 70.0 + seed * 20.0));
    // 只出现在上半天幕, 顶端最亮
    float vert = smoothstep(0.88, 0.10, uv.y);

    return (core + glow) * env * vert;
}

float4 StormSkyPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.001)
        return float4(0, 0, 0, 0);

    float2 aspectUV = float2(uv.x * uAspect, uv.y);
    float t = uTime;
    float phase = saturate(uPhase);
    float dim = saturate(uDeath);

    // ===== 基础渐变: 玄墨顶 → 血红地平 (雷狱相转电紫) =====
    float3 topCol = lerp(SkyTopA, SkyTopB, phase);
    float3 botCol = lerp(SkyBotA, SkyBotB, phase);
    // 死亡演出: 地平线烧成更深的血色
    botCol = lerp(botCol, BloodRed * 0.35, dim * 0.8);
    float3 col = lerp(topCol, botCol, smoothstep(0.15, 1.0, uv.y));

    // ===== 双层雷暴云 (上 65% 天幕) =====
    float speed = 1.0 + phase * 0.8;
    float2 cUV1 = aspectUV * 1.5 + float2(t * 0.020 * speed, -t * 0.006);
    float cloud1 = warpedFbm(cUV1, t * 0.5);
    float dens1 = smoothstep(0.34, 0.72, cloud1);

    float2 cUV2 = aspectUV * 2.9 + float2(-t * 0.032 * speed, t * 0.010);
    float cloud2 = fbm4(cUV2);
    float dens2 = smoothstep(0.42, 0.78, cloud2);

    float cloudMask = smoothstep(0.75, 0.25, uv.y); // 云压在上半
    dens1 *= cloudMask;
    dens2 *= cloudMask;

    // 云体: 暗部压底, 云缘提亮 (dens 梯度近似 → 用两档 smoothstep 差)
    float rim1 = smoothstep(0.34, 0.50, cloud1) - smoothstep(0.50, 0.72, cloud1);
    float3 cloudCol = lerp(CloudDark, CloudEdge, saturate(rim1 * 1.4)) ;
    cloudCol = lerp(cloudCol, cloudCol * float3(1.0, 0.6, 0.55), (1.0 - phase) * 0.35); // 常态云染血
    col = lerp(col, cloudCol * (1.0 - dim * 0.55), dens1 * 0.85);
    col = lerp(col, CloudDark * 0.8 * (1.0 - dim * 0.6), dens2 * 0.55);

    // ===== 云内放电: cell 随机短亮 (雷在云里滚) =====
    float2 cellId = floor(aspectUV * float2(3.0, 2.2));
    float cellSlice = floor(t * 2.6);
    float cellSeed = hash21(cellId + cellSlice * 17.3);
    float fire = step(0.935 - phase * 0.05 - uFlash * 0.1, cellSeed); // 雷狱/亮拍时更频
    if (fire > 0.5)
    {
        float ft = frac(t * 2.6);
        float2 cpos = (cellId + hash22(cellId + cellSlice)) / float2(3.0, 2.2);
        float2 crel = aspectUV - cpos;
        float cglow = exp(-dot(crel, crel) * 26.0);
        float cenv = pow(saturate(1.0 - ft * 1.8), 2.0);
        // 放电只照亮有云处 → 云成了灯罩
        col += Lightning * cglow * cenv * dens1 * (0.55 + phase * 0.35) * (1.0 - dim);
    }

    // ===== 远景闪电折线 (双通道: 常态稀疏 + 雷狱加密) =====
    float bolt = DistantBolt(aspectUV, uv, 0.43, 0.62, 3.7);
    bolt += DistantBolt(aspectUV, uv, 0.71, 0.78 - phase * 0.25, 11.9) * (0.4 + phase * 0.6);
    col += Lightning * bolt * (0.55 + phase * 0.30) * (1.0 - dim * 0.85);
    // 闪电瞬间给全天一层薄的环境照明 (云被照亮)
    col += Lightning * bolt * dens1 * 0.35;

    // ===== 尸主血晕: Boss 位置的暗红脉动 (低语式存在感) =====
    float2 rel = uv - uBossUV;
    rel.x *= uAspect;
    float bdist = length(rel);
    float breath = 0.75 + 0.25 * sin(t * 1.7);
    float haloS = exp(-bdist * 2.4) * breath * (0.16 + phase * 0.10 + dim * 0.30);
    col += BloodRed * haloS;

    // ===== 大节拍亮拍: 全天提亮 + 以 Boss 为中心的辉爆 =====
    if (uFlash > 0.001)
    {
        float fburst = exp(-bdist * 2.0) * uFlash;
        col += (Lightning * 0.75 + float3(0.25, 0.25, 0.28)) * (fburst * 1.1 + uFlash * 0.22);
    }

    // ===== 暗角聚焦 =====
    float2 vc = uv - 0.5;
    vc.x *= uAspect;
    float vig = lerp(0.62, 1.0, 1.0 - saturate(dot(vc, vc) * 0.85));
    col *= vig;

    // 死亡整体压暗 (天罚前的死寂由 uFlash 打破)
    col *= 1.0 - dim * 0.45;

    float alpha = uIntensity;
    return float4(saturate(col) * alpha, alpha);
}

technique Technique1
{
    pass StormSkyPass
    {
        PixelShader = compile ps_3_0 StormSkyPS();
    }
}
