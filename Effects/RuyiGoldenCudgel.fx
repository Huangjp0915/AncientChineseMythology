// ============================================================
// 如意金箍棒 — 棍身附魔着色器 (喂棍贴图, SpriteBatch Immediate)
// 金红双色流光 + 紧箍环纹按如意值逐环点亮 + 满值微光噪 + 爆发白闪
// 消费方: RuyiJinguBang (常驻, 强度随如意值), TrueRuyiStick (低强度档, 辅色幽蓝)
// ============================================================

sampler uTexture : register(s0); // 棍贴图 (对角朝向)

float  uTime;        // 动画时间 (秒)
float  uIntensity;   // 整体强度 0~1 (0 = 原贴图)
float  uCharge;      // 如意值 0~1 (紧箍环由柄向尖逐环点亮)
float  uFlash;       // 爆发帧白闪 0~1
float2 uAxis;        // UV 空间沿棍轴指向棍尖的单位向量 (翻转时由 C# 换向)
float4 uColorGold;   // 主色 (箍金)
float4 uColorRed;    // 辅色 (杆红 / 真·如意传幽蓝)

// 廉价 hash — 满值微光噪
float hash21(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

float4 GoldenCudgelPS(float4 vertColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 base = tex2D(uTexture, uv) * vertColor;
    if (uIntensity < 0.005)
        return base;

    // 沿棍轴坐标 s: 0 = 柄, 1 = 尖
    float s = saturate(dot(uv - 0.5, uAxis) + 0.5);

    // —— 紧箍环纹: 7 环, 每环中心一圈细纹, 随 uCharge 由柄向尖点亮 ——
    const float bandCount = 7.0;
    float b = s * bandCount;
    float bandIdx = floor(b);
    float f = frac(b);
    float ring = 1.0 - smoothstep(0.10, 0.24, abs(f - 0.5));
    float lit = step((bandIdx + 0.5) / bandCount, uCharge + 0.001);
    // 正在充能的当前环呼吸闪烁
    float current = step(abs(bandIdx + 0.5 - uCharge * bandCount), 0.5);
    float ringGlow = ring * (0.18 + 0.82 * lit + current * 0.35 * (0.5 + 0.5 * sin(uTime * 10.0)));

    // —— 金红双色流光: 沿轴缓慢流动 ——
    float w = 0.5 + 0.5 * sin(s * 8.0 - uTime * 3.2);
    float3 flowCol = lerp(uColorRed.rgb, uColorGold.rgb, w);

    // —— 满值微光噪: 棍身细碎金火星 ——
    float sparkle = 0.0;
    if (uCharge > 0.95)
    {
        float n = hash21(floor(uv * 24.0) + floor(uTime * 8.0));
        sparkle = step(0.93, n) * 0.8;
    }

    // 合成 (只作用于贴图不透明处)
    float3 enhanced = base.rgb;
    enhanced += flowCol * (0.10 + 0.28 * uCharge) * base.a;          // 整体流光罩色
    enhanced += uColorGold.rgb * ringGlow * (0.35 + 0.65 * uCharge) * base.a; // 紧箍环
    enhanced += float3(1.0, 0.95, 0.8) * sparkle * base.a;

    // 爆发白闪
    enhanced = lerp(enhanced, float3(1.0, 0.98, 0.9), saturate(uFlash) * base.a);

    float3 outRgb = lerp(base.rgb, enhanced, uIntensity);
    return float4(outRgb, base.a);
}

technique Technique1
{
    pass GoldenCudgelPass
    {
        PixelShader = compile ps_3_0 GoldenCudgelPS();
    }
}
