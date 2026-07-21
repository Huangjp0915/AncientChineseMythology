// ============================================================
// 敖钦熔火柱着色器 — 程序化熔岩 / 蒸汽喷柱
// 上涌 FBM 焰体 + 边缘焰舌撕裂 + 白热芯 + 生长前沿
// uMode: 0=熔火(红橙金)  1=沸海蒸汽(白金)  2=死亡金白巨柱
// UV 约定: x=横向 0~1, y=0 顶端 → 1 底部基座
// 完全自包含程序化噪声, 不依赖外部贴图 (s0 仅作占位)
// ============================================================

sampler uTexture : register(s0); // 占位, 不采样

float uTime;      // 动画时间(秒)
float uGrowth;    // 生长进度 0~1 (自底向上显露)
float uFade;      // 存续度 1=实体 → 0=噪声侵蚀消散
float uIntensity; // 整体亮度 0~1
float uSeed;      // 每柱相位差(0~10 任意)
float uMode;      // 0=熔火 1=蒸汽 2=金白
float uWidth;     // 宽度系数(默认1)

static const float3 FireDeep   = float3(0.55, 0.08, 0.03);
static const float3 FireMid    = float3(1.00, 0.42, 0.08);
static const float3 FireGold   = float3(1.00, 0.82, 0.30);
static const float3 HotWhite   = float3(1.00, 0.97, 0.88);
static const float3 SteamDim   = float3(0.62, 0.66, 0.72);
static const float3 SteamLit   = float3(0.98, 0.99, 1.00);
static const float3 DeathGold  = float3(1.00, 0.90, 0.55);

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
    v += valueNoise(p * 2.17 + 1.9) * 0.30;
    v += valueNoise(p * 4.31 + 4.2) * 0.15;
    return v;
}

// ---------- 像素着色器 ----------
float4 PillarPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float steamMask = saturate(1.0 - abs(uMode - 1.0)); // 1=蒸汽档
    float deathMask = saturate(1.0 - abs(uMode - 2.0)); // 1=金白档

    // 上涌焰体噪声: y 随时间下移采样 = 火焰向上翻涌; 蒸汽更缓更团
    float rise = lerp(1.55, 0.95, steamMask);
    float freqX = lerp(3.2, 2.1, steamMask);
    float2 np = float2(uv.x * freqX + uSeed * 3.7, uv.y * 2.3 + uTime * rise + uSeed * 7.1);
    float body = fbm3(np);

    // 第二层细节(细焰丝/汽缕)
    float wisp = fbm3(np * 2.6 + float2(0.0, uTime * 0.9));

    // 柱形: 底部收口, 顶部外扩(羽流形) + 焰舌边缘扰动
    float cx = uv.x - 0.5;
    float halfW = lerp(0.36, 0.23, uv.y) * uWidth;
    float tongue = (body - 0.5) * lerp(0.20, 0.30, steamMask) * (0.35 + 0.65 * (1.0 - uv.y));
    float d = abs(cx) - (halfW + tongue);
    float shape = smoothstep(0.02, -0.10, d);

    // 生长: 自底(y=1)向上显露, 前沿加亮
    float growCut = 1.0 - uGrowth;
    float growMask = smoothstep(growCut - 0.02, growCut + 0.10, uv.y);
    float front = smoothstep(growCut + 0.16, growCut + 0.02, uv.y)
                * smoothstep(growCut - 0.05, growCut + 0.02, uv.y)
                * step(0.01, uGrowth) * (1.0 - step(0.995, uGrowth));

    // 消散: uFade 降低时噪声自边缘侵蚀
    float erode = smoothstep(1.05 - uFade, 1.25 - uFade, body + 0.25);

    float mask = shape * growMask * erode;
    if (mask < 0.01)
        return float4(0, 0, 0, 0);

    // 芯-边分布: 中心白热, 边缘深色
    float core = 1.0 - saturate(abs(cx) / max(halfW + tongue, 0.001));
    core = pow(core, 1.6);

    // 底部基座更炽热
    float baseHeat = smoothstep(0.45, 1.0, uv.y);

    // ---------- 颜色合成 ----------
    // 熔火档
    float3 fire = FireDeep;
    fire = lerp(fire, FireMid, saturate(core * 1.2 + body * 0.25));
    fire = lerp(fire, FireGold, saturate(core * core * (0.55 + baseHeat * 0.45)));
    fire = lerp(fire, HotWhite, saturate(pow(core, 3.0) * (0.35 + baseHeat * 0.65)));
    fire += FireGold * wisp * 0.22;

    // 蒸汽档
    float3 steam = lerp(SteamDim, SteamLit, saturate(core * 0.8 + wisp * 0.35));
    steam += SteamLit * pow(core, 3.0) * 0.25;

    // 金白死亡档
    float3 gold = lerp(FireGold, DeathGold, core);
    gold = lerp(gold, HotWhite, saturate(pow(core, 2.0) * 0.9));
    gold += HotWhite * wisp * 0.25;

    float3 col = lerp(fire, steam, steamMask);
    col = lerp(col, gold, deathMask);

    // 生长前沿爆亮
    col += lerp(HotWhite, SteamLit, steamMask) * front * 1.4;

    // 侵蚀边缘炽亮(消散时烧边)
    float burnEdge = smoothstep(1.25 - uFade, 1.05 - uFade, body + 0.25)
                   * smoothstep(1.00 - uFade, 1.12 - uFade, body + 0.25);
    col += FireGold * burnEdge * (1.0 - steamMask) * 1.5;

    // ---------- Alpha ----------
    float alpha = mask * uIntensity;
    alpha *= lerp(0.45, 1.0, core);            // 边缘半透
    alpha *= lerp(1.0, 0.72, steamMask);       // 蒸汽整体更透
    alpha *= uFade * 0.35 + 0.65;

    return float4(saturate(col) * saturate(alpha), saturate(alpha));
}

technique Technique1
{
    pass PillarPass
    {
        PixelShader = compile ps_3_0 PillarPS();
    }
}
