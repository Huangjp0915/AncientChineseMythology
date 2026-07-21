// ============================================================
// 劫雷主雷柱着色器 — 程序化折线闪电 (TribulationCloud 专属)
// 分段 hash 折线主干(两级频率) + 2 条斜向分叉 + 白热芯/主题辉光
// uSeed 每记异形; uLife 余辉衰减(芯收窄变暗、辉光散开);
// uWidthScale/uBranch/uFlicker 参数化出"先导电弧"细弱闪烁形态
// 竖直窄长 quad, UV: x=横向 0~1, y=0 顶(云底) 1 底(落点)
// 建议 Additive 绘制; 完全程序化, s0 占位不采样
// ============================================================

sampler uImage0 : register(s0); // 占位, 不采样

float  uTime;       // 动画时间(秒)
float  uIntensity;  // 整体强度 0~1
float  uSeed;       // 折线随机种子 (每记落雷更换)
float  uLife;       // 余辉进度 0=轰落峰值 1=完全熄灭
float4 uColor;      // 主题辉光色 (rgb)
float  uWidthScale; // 宽度系数 (主雷=1, 先导~0.35)
float  uBranch;     // 分叉可见度 0~1 (先导=0)
float  uFlicker;    // 高频闪烁幅度 0~1 (先导用, 主雷=0)

static const float3 CoreWhite = float3(1.0, 0.99, 0.93);

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

// 折线中心偏移: y 分段, 段点 hash 位移, 段间平滑插值 (分段常数->折线)
float boltPath(float y, float seed, float segs, float amp)
{
    float fy = y * segs + seed * 57.31;
    float i = floor(fy);
    float f = frac(fy);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash11(i) - 0.5;
    float b = hash11(i + 1.0) - 0.5;
    return lerp(a, b, f) * amp;
}

float4 BoltPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float x = uv.x;
    float y = uv.y;

    // 端点收敛包络: 顶端(云底出口)与底端(落点)必须精确对齐 quad 中线
    float env = 4.0 * y * (1.0 - y);

    // 主干 = 低频大折线 + 高频细节折线
    float cxLow = boltPath(y, uSeed, 8.0, 0.30);
    float cx = 0.5 + (cxLow + boltPath(y, uSeed + 3.7, 26.0, 0.10)) * env;

    float fade = saturate(1.0 - uLife);

    // 白热芯: 高斯剖面; 余辉期收窄变暗
    float d = abs(x - cx);
    float coreW = 0.014 * uWidthScale * (1.0 - 0.55 * uLife) + 0.0015;
    float core = exp(-d * d / (coreW * coreW));

    // 主题辉光: 指数剖面; 余辉期反而散开(电离残光)
    float glowW = 0.085 * uWidthScale * (1.0 + 1.6 * uLife) + 0.005;
    float glow = exp(-d / glowW);

    // —— 两条斜向分叉 (从主干中途岔出, 越走越淡) ——
    float branch = 0.0;
    float branchGlow = 0.0;
    for (int k = 0; k < 2; k++)
    {
        float fk = (float)k;
        float startY = 0.20 + hash11(uSeed * 7.1 + fk * 13.7) * 0.38;
        float side = (hash11(uSeed * 3.3 + fk * 5.9) > 0.5) ? 1.0 : -1.0;
        float dy = y - startY;
        float on = step(0.0, dy);
        // 分叉起点挂在主干低频路径上
        float baseX = 0.5 + boltPath(startY, uSeed, 8.0, 0.30) * 4.0 * startY * (1.0 - startY);
        float bx = baseX + side * dy * 0.55
                 + boltPath(y, uSeed + 11.0 + fk * 4.2, 20.0, 0.09) * saturate(dy * 8.0);
        float bd = abs(x - bx);
        float bw = coreW * 0.62;
        float bfade = exp(-dy * 5.0) * on * saturate(dy * 30.0);
        branch += exp(-bd * bd / (bw * bw)) * bfade;
        branchGlow += exp(-bd / (glowW * 0.55)) * bfade;
    }
    // 分叉比主干先熄灭
    float branchAlive = saturate(1.0 - uLife * 2.0) * uBranch;

    // 落点亮斑 (雷插进地面的白热点)
    float hit = exp(-((1.0 - y) * (1.0 - y)) / 0.0025) * exp(-(x - 0.5) * (x - 0.5) / 0.02);

    // 顶端淡入 (从云底探出)
    float top = smoothstep(0.0, 0.05, y);

    // 高频闪烁 (先导电弧的犹疑感)
    float flick = 1.0 - uFlicker * (0.5 + 0.5 * sin(uTime * 55.0 + y * 26.0 + uSeed * 40.0)) * 0.65;

    float3 col = CoreWhite * (core * 1.5 + branch * 0.9 * branchAlive) * fade;
    col += uColor.rgb * (glow * 0.9 + branchGlow * 0.45 * branchAlive) * fade;
    col += CoreWhite * hit * fade * 2.2;
    col *= flick * uIntensity * top;

    float a = saturate(core + glow * 0.55 + branch * branchAlive) * fade * uIntensity * top;
    return float4(saturate(col), a);
}

technique Technique1
{
    pass BoltPass
    {
        PixelShader = compile ps_3_0 BoltPS();
    }
}
