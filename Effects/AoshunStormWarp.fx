// ============================================================
// 敖顺风暴扭曲着色器 — 全屏后处理（走 RequestFullscreenSlot 名额）
// 风场卷动 UV 扭曲 + 程序化斜向雨幕 + 雷闪白 + 风暴之眼平静区抠除
// 眼内（uEyeRadius 生效时）无雨无扭曲——"平静的眼"既是叙事也是可读性装置
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标 (Main.screenTarget)
sampler uNoise  : register(s1); // 共享可平铺三通道 FBM 噪声

float uTime;        // 动画时间 (秒)
float uIntensity;   // 主强度 0~1
float uAspect;      // 屏幕宽高比 width/height
float2 uWindDir;    // 归一化风向 (屏幕空间, x右 y下)
float uWind;        // 风场扭曲强度 0~1
float uRain;        // 雨幕密度 0~1
float uFlash;       // 雷闪提亮 0~1
float2 uEyeCenter;  // 风暴之眼归一化屏幕中心
float uEyeRadius;   // 眼半径 (占屏幕高度比例); <=0 表示无眼

static const float3 StormTint = float3(0.60, 0.66, 0.88); // 墨蓝雨色分级
static const float3 RainColor = float3(0.70, 0.82, 1.00); // 雨丝青白
static const float3 FlashCol  = float3(0.84, 0.92, 1.00); // 雷闪白

// 眼内平静遮罩: 1=眼内(无风无雨) 0=眼外风暴
float EyeCalm(float2 coords)
{
    if (uEyeRadius <= 0.001)
        return 0.0;
    float2 pos = float2(coords.x * uAspect, coords.y);
    float2 c = float2(uEyeCenter.x * uAspect, uEyeCenter.y);
    return smoothstep(uEyeRadius, uEyeRadius * 0.80, length(pos - c));
}

float4 StormWarpPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float calm = EyeCalm(coords);
    float storm = uIntensity * (1.0 - calm);

    // ==========================================
    //  风场卷动 — 双层沿风向流动的 FBM 扰动
    // ==========================================
    float2 tang = float2(-uWindDir.y, uWindDir.x);
    float n1 = tex2D(uNoise, coords * 2.6 + uWindDir * uTime * 0.16).r;
    float n2 = tex2D(uNoise, coords * 5.1 - uWindDir * uTime * 0.24 + 0.37).g;
    float2 warp = uWindDir * (n1 - 0.5) * 2.0 + tang * (n2 - 0.5);
    float2 uv = coords + warp * 0.012 * uWind * storm;
    uv = clamp(uv, 0.001, 0.999);

    float4 scene = tex2D(uImage0, uv);

    // 轻微色散 — 沿风向 RGB 偏移
    float chroma = 0.004 * uWind * storm;
    float2 chromaOff = uWindDir * chroma;
    scene.r = lerp(scene.r, tex2D(uImage0, clamp(uv + chromaOff, 0.001, 0.999)).r, storm * 0.6);
    scene.b = lerp(scene.b, tex2D(uImage0, clamp(uv - chromaOff, 0.001, 0.999)).b, storm * 0.6);

    // ==========================================
    //  斜向雨幕 — 两层不同速度/密度的拉长噪声条纹
    // ==========================================
    float2 rd = normalize(float2(uWindDir.x * 0.55 + 0.0001, 1.0)); // 雨落向: 主竖直+风向倾斜
    float2 rp = float2(-rd.y, rd.x);
    float2 p = float2(coords.x * uAspect, coords.y);
    float along = dot(p, rd);
    float across = dot(p, rp);

    // 远层: 细密慢速
    float r1 = tex2D(uNoise, float2(across * 9.0, along * 0.6 - uTime * 1.9)).b;
    float streak1 = smoothstep(0.66, 0.85, r1);
    // 近层: 粗亮快速
    float r2 = tex2D(uNoise, float2(across * 15.0 + 0.53, along * 0.9 - uTime * 3.2)).r;
    float streak2 = smoothstep(0.72, 0.90, r2);

    float rain = (streak1 * 0.45 + streak2 * 0.38) * uRain * storm;
    scene.rgb += RainColor * rain * (1.0 + uFlash * 1.6);

    // ==========================================
    //  雷闪白 — 带云层纹理的整屏提亮
    // ==========================================
    float flashTex = 0.4 + 0.6 * tex2D(uNoise, coords * 1.4 + float2(uTime * 0.05, 0.0)).r;
    scene.rgb += FlashCol * uFlash * flashTex * uIntensity * 0.55;

    // ==========================================
    //  雨天分级 — 轻度去饱和 + 墨蓝染色 (眼内减半)
    // ==========================================
    float lum = dot(scene.rgb, float3(0.30, 0.59, 0.11));
    float3 graded = lerp(scene.rgb, lum.xxx * StormTint * 1.12, 0.30);
    scene.rgb = lerp(scene.rgb, graded, uIntensity * (1.0 - calm * 0.65) * uRain);

    return scene;
}

technique Technique1
{
    pass StormWarpPass
    {
        PixelShader = compile ps_3_0 StormWarpPS();
    }
}
