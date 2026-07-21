// ============================================================
// 黑熊精·黑风 — 全屏后处理 (黑风山妖风)
// 场景 UV 沿风向扭曲 + 各向异性黑风带 + 压暗/暗角 + 袈裟金尘
//   uIntensity: 黑风强度 (P2 常驻 ~0.35, 怒嚎蓄力至 1.0)
//   uGold:      金光收服强度 (死亡演出用; 黑风退去金尘漫天)
//   uWindDir:   风向 (归一化)
// s0=screenTarget, s1=可平铺噪声 (RGB 三通道)
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标
sampler uNoise  : register(s1); // 可平铺噪声

float  uTime;      // 秒
float  uIntensity; // 黑风强度 0~1
float  uGold;      // 金光强度 0~1
float2 uWindDir;   // 风向 (归一化)
float  uAspect;    // 宽高比 width/height

static const float3 InkBlack   = float3(0.02, 0.015, 0.04);  // 墨黑
static const float3 WindViolet = float3(0.16, 0.10, 0.24);   // 妖风暗紫
static const float3 KasayaGold = float3(1.00, 0.82, 0.42);   // 袈裟金

float4 DarkWindPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float total = max(uIntensity, uGold);
    if (total < 0.01)
        return tex2D(uImage0, coords);

    // —— 风向坐标系: t=沿风, b=垂直风 (aspect 校正保证斜向风不变形) ——
    float2 dir  = normalize(uWindDir + float2(0.0001, 0));
    float2 perp = float2(-dir.y, dir.x);
    float2 p    = float2((coords.x - 0.5) * uAspect, coords.y - 0.5);
    float  t    = dot(p, dir);
    float  b    = dot(p, perp);

    // —— 黑风带: 沿风拉长的两层噪声条纹, 不同速度交叠 ——
    float2 sUV1 = float2(t * 0.9 - uTime * 0.55, b * 5.0);
    float2 sUV2 = float2(t * 1.4 - uTime * 0.95, b * 8.0 + 0.37);
    float s1 = tex2D(uNoise, sUV1).r;
    float s2 = tex2D(uNoise, sUV2).g;
    float streak = s1 * 0.6 + s2 * 0.4;
    float band = smoothstep(0.42, 0.78, streak);        // 风带主体
    float wisp = smoothstep(0.68, 0.92, streak);        // 风带亮丝

    // —— UV 扭曲: 场景沿风向被吹歪 (风带处更强) ——
    float2 warp = dir * (streak - 0.5) * (0.006 + 0.016 * band) * uIntensity;
    warp += perp * (s2 - 0.5) * 0.004 * uIntensity;
    float4 scene = tex2D(uImage0, clamp(coords + warp, 0.001, 0.999));

    // —— 压暗 + 风带墨染 ——
    float darken = uIntensity * (0.16 + band * 0.30);
    scene.rgb = lerp(scene.rgb, InkBlack, saturate(darken));
    scene.rgb = lerp(scene.rgb, WindViolet, band * uIntensity * 0.22);

    // 风带边缘亮丝 (妖气流光, 淡紫)
    scene.rgb += WindViolet * 2.2 * wisp * uIntensity * 0.16;

    // —— 暗角 (黑风压境) ——
    float2 vc = coords - 0.5;
    float vig = saturate(dot(vc, vc) * 1.6);
    scene.rgb = lerp(scene.rgb, InkBlack, vig * uIntensity * 0.45);

    // —— 袈裟金 (收服/金光节拍): 金尘上飘 + 整体暖金染 ——
    if (uGold > 0.01)
    {
        float2 gUV = float2(coords.x * 2.2 + tex2D(uNoise, coords * 1.3).b * 0.2,
                            coords.y * 2.2 + uTime * 0.16);
        float dust = tex2D(uNoise, gUV).r;
        float mote = smoothstep(0.80, 0.94, dust);
        float twinkle = 0.6 + 0.4 * sin(uTime * 5.0 + dust * 40.0);
        scene.rgb += KasayaGold * mote * twinkle * uGold * 0.85;
        // 暖金整体罩色 (轻)
        scene.rgb = lerp(scene.rgb, scene.rgb * KasayaGold * 1.25, uGold * 0.30);
        // 中心柔和金晕
        float glow = saturate(1.0 - length(p) * 1.7);
        scene.rgb += KasayaGold * glow * glow * uGold * 0.22;
    }

    return scene;
}

technique Technique1
{
    pass DarkWindPass
    {
        PixelShader = compile ps_3_0 DarkWindPS();
    }
}
