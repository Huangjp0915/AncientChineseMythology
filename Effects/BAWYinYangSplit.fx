// ============================================================
// 黑白无常·阴阳勾魂分屏 — 全屏后处理 (BAW 专属)
// 沿两使连线的垂直平分线把屏幕剖成阴阳两域:
//   阴域(黑无常侧): 压暗降饱和 + 幽蓝紫染 + 漂浮魂点
//   阳域(白无常侧): 提亮 + 暖白染 + 浮光魂点
// 分界为水墨噪声波动缝 + 太极双色描边呼吸
// 相对共享 PaletteLUT 分屏: 缝有形态、两域有"活的"魂火颗粒
// 喂 Main.screenTarget(s0), 共享噪声(s1); 走全屏名额契约
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标
sampler uNoise  : register(s1); // 可平铺噪声 (RGB 三通道独立)

float  uTime;      // 动画时间(秒)
float  uIntensity; // 整体强度 0~1
float  uAspect;    // 宽高比 width/height
float2 uSplitDir;  // 分屏法线方向(归一化, 指向阳域)
float  uSplitPos;  // 分屏中线位置(沿法线投影 0~1)
float4 uYinColor;  // 阴域染色 (黑无常: 幽蓝紫)
float4 uYangColor; // 阳域染色 (白无常: 暖白)

static const float3 LUM = float3(0.299, 0.587, 0.114);

float4 BAWYinYangSplitPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(uImage0, coords);
    if (uIntensity < 0.01)
        return scene;

    // 宽高比校正空间
    float2 p = float2(coords.x * uAspect, coords.y);
    float2 dir = normalize(uSplitDir + 0.0001);
    float2 tan = float2(-dir.y, dir.x);

    float proj = dot(p, dir);           // 沿法线坐标
    float t    = dot(p, tan);           // 沿缝坐标
    float mid  = uSplitPos * (1.0 + uAspect) * 0.5;

    // 水墨波动缝: 沿缝方向采样低频噪声扰动分界位置
    float wob1 = tex2D(uNoise, float2(t * 0.55 + uTime * 0.045, uTime * 0.03)).r - 0.5;
    float wob2 = tex2D(uNoise, float2(t * 1.7 - uTime * 0.06, 0.37 + uTime * 0.02)).g - 0.5;
    float seam = proj - mid + (wob1 * 0.055 + wob2 * 0.018) * uIntensity;

    float side = smoothstep(-0.018, 0.018, seam); // 0=阴 1=阳

    float lum = dot(scene.rgb, LUM);

    // —— 阴域: 去饱和压暗 + 幽蓝紫, 漂浮魂点 (暗底亮斑) ——
    float3 yin = lerp(lum.xxx, scene.rgb, 0.35) * (uYinColor.rgb * 1.55 + 0.08);
    float2 moteUV = p * 2.6 + float2(uTime * 0.015, -uTime * 0.05); // 魂点缓慢上飘
    float mote = tex2D(uNoise, moteUV).b;
    float yinMote = smoothstep(0.74, 0.95, mote);
    yin += uYinColor.rgb * yinMote * 0.35;

    // —— 阳域: 提亮 + 暖白, 浮光魂点 ——
    float3 yang = scene.rgb * (uYangColor.rgb * 1.35) + uYangColor.rgb * 0.10;
    float2 lightUV = p * 2.1 + float2(-uTime * 0.02, -uTime * 0.035);
    float glimmer = tex2D(uNoise, lightUV).g;
    float yangMote = smoothstep(0.78, 0.97, glimmer);
    yang += uYangColor.rgb * yangMote * 0.28;

    float3 splitCol = lerp(yin, yang, side);

    // —— 太极缝: 双色描边 (各自渗入对侧) + 呼吸 ——
    float breath = 0.75 + 0.25 * sin(uTime * 2.3 + t * 5.0);
    float glowN = exp(-seam * seam * 1500.0);           // 窄亮缝
    float glowW = exp(-seam * seam * 220.0);            // 宽柔晕
    // 阴侧缝缘描阳色、阳侧缝缘描阴色 — 阴阳互衔
    float3 seamCol = lerp(uYangColor.rgb, uYinColor.rgb, side);
    splitCol += seamCol * glowN * breath * 0.85;
    splitCol += (uYinColor.rgb + uYangColor.rgb) * 0.5 * glowW * 0.18;

    float3 col = lerp(scene.rgb, splitCol, uIntensity);
    return float4(saturate(col), scene.a);
}

technique Technique1
{
    pass BAWYinYangSplitPass
    {
        PixelShader = compile ps_3_0 BAWYinYangSplitPS();
    }
}
