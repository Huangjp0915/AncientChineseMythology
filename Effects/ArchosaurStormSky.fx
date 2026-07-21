// ============================================================
// 祖龙残魂 · 雷暴天幕 — 全屏氛围 overlay (预乘 Alpha, AlphaBlend)
// 双层云海压暗 + 斜向雨丝 + 天空白闪(uFlash) + 破绽金化(uWindow) + 暗角
// 喂占位白像素(s0, 不采样), s1 绑共享可平铺噪声; 不读 screenTarget,
// 不占全屏后处理名额 (与 ElementalScreenTint 用法同类, 在 PostDrawTiles 自开合批)
// ============================================================

sampler uImage0 : register(s0); // 占位, 不采样
sampler uNoise  : register(s1); // 可平铺噪声 (RGB 三通道独立)

float uTime;       // 累计时间(秒)
float uIntensity;  // 雷暴强度 0~1
float uAspect;     // 宽高比 width/height
float uFlash;      // 天空白闪 0~1 (闪电时刻)
float uWindow;     // 破绽金化 0~1 (输出窗口信号)
float uRain;       // 雨丝强度 0~1
float uVignette;   // 暗角强度 0~1

static const float3 StormTop  = float3(0.055, 0.100, 0.200); // 玄青雷云顶
static const float3 StormLow  = float3(0.030, 0.050, 0.110); // 地平深暗
static const float3 FlashCol  = float3(0.780, 0.870, 1.000); // 白闪青白
static const float3 GoldCol   = float3(1.000, 0.840, 0.470); // 破绽金
static const float3 RainCol   = float3(0.550, 0.680, 0.850); // 雨丝冷白

float4 StormSkyPS(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float total = uIntensity + uFlash;
    if (total < 0.01)
        return float4(0, 0, 0, 0);

    float2 auv = float2(uv.x * uAspect, uv.y);

    // —— 云海: 双层漂移噪声, 只压上半屏 ——
    float2 cuvA = auv * 1.6 + float2(uTime * 0.014, -uTime * 0.004);
    float2 cuvB = auv * 3.1 + float2(-uTime * 0.021, uTime * 0.006);
    float cloud = tex2D(uNoise, cuvA).r * 0.65 + tex2D(uNoise, cuvB).g * 0.45;
    cloud = smoothstep(0.35, 0.95, cloud) * smoothstep(0.85, 0.10, uv.y);

    // —— 雨丝: 斜向拉伸噪声快速下滚 (横向密, 纵向长) ——
    float2 ruv = float2(auv.x * 22.0 + auv.y * 5.0, auv.y * 1.4 - uTime * 2.8);
    float rain = smoothstep(0.80, 0.97, tex2D(uNoise, ruv).b) * uRain;

    // —— 基础压暗层 (预乘) ——
    float baseA = saturate(0.32 + cloud * 0.36) * uIntensity;
    baseA *= lerp(1.0, 0.42, saturate(uWindow));  // 破绽窗口: 减压暗提亮世界
    float3 baseCol = lerp(StormTop, StormLow, saturate(uv.y * 1.25));
    baseCol = lerp(baseCol, GoldCol * 0.38, saturate(uWindow) * 0.62);
    float3 premul = baseCol * (1.0 + cloud * 0.85) * baseA;

    // —— 加性层: 雨丝 / 白闪(云被背光照亮) / 金色微光 ——
    premul += RainCol * rain * 0.30 * uIntensity;
    premul += FlashCol * uFlash * (0.42 + cloud * 0.85);
    premul += GoldCol * saturate(uWindow) * 0.085 * uIntensity;

    // —— 暗角 ——
    float2 vc = uv - 0.5;
    vc.x *= uAspect;
    float vig = 1.0 - saturate(dot(vc, vc) * 1.15);
    vig = lerp(1.0 - uVignette, 1.0, vig);
    premul *= vig;

    float a = saturate(baseA + uFlash * 0.55 + rain * 0.20 * uIntensity);
    return float4(max(premul, 0.0), a);
}

technique Technique1
{
    pass StormSkyPass
    {
        PixelShader = compile ps_3_0 StormSkyPS();
    }
}
