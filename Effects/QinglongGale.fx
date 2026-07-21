// ============================================================
// 青龙风幕流线 — 全屏 overlay (不读 screenTarget, 不占全屏后处理名额)
// 方向性拉伸噪声流条: 沿风向低频拉长 + 横向高频切细 = 风幕丝线
// 用于: 风域天罚 / 雷暴天气 / 死亡「化雨」期间叠加
// 经 ACMShaders.DrawFullscreenOverlay(Additive) 绘制, s0 为占位像素不采样,
// 噪声全程序化 (与 ElementalScreenTint 同方案, 自包含无外部依赖)
// ============================================================

sampler uImage0 : register(s0); // 占位, 不采样

float  uTime;     // 累计时间(秒)
float  uIntensity;// 整体强度 0~1 (<0.01 直接透明)
float  uAspect;   // 宽高比 width/height
float  uAngle;    // 风向 (弧度, 屏幕空间; 0=向右)
float  uDensity;  // 流线横向密度 (建议 4~9)
float4 uColor;    // 风幕色 (翠青/雨灰)
float  uSpeed;    // 流动速度倍率

// 程序化噪声 (自包含)
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

float4 GalePS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 auv = float2(uv.x * uAspect, uv.y);
    float2 dir = float2(cos(uAngle), sin(uAngle));
    float2 nrm = float2(-dir.y, dir.x);
    float along = dot(auv, dir);
    float across = dot(auv, nrm);
    float t = uTime * uSpeed;

    // 主流条: 沿风向低频(拉长) × 横向高频(切细)
    float band1 = valueNoise(float2(along * 1.6 - t * 1.0, across * uDensity));
    float band2 = valueNoise(float2(along * 3.1 - t * 1.9, across * uDensity * 1.9 + 13.7));
    float streak = smoothstep(0.52, 0.95, band1 * 0.6 + band2 * 0.5);

    // 高速细丝层 (更快更细, 强化速度感)
    float wisp = valueNoise(float2(along * 6.0 - t * 3.4, across * uDensity * 3.2 + 47.0));
    wisp = smoothstep(0.68, 0.98, wisp) * 0.7;

    // 屏幕中心让位 (保玩家可读性)
    float2 vc = uv - 0.5;
    vc.x *= uAspect;
    float centerFade = lerp(0.35, 1.0, smoothstep(0.05, 0.42, length(vc)));

    float s = (streak + wisp) * centerFade * uIntensity;
    return float4(uColor.rgb * s, s);
}

technique Technique1
{
    pass GalePass
    {
        PixelShader = compile ps_3_0 GalePS();
    }
}
