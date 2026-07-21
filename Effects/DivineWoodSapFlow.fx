// ============================================================
// 神木系列专属 — 翠脉流光 (贴图空间; s0=武器贴图, s1=共享噪声)
// 沿贴图流动的树液光脉 + 轮廓翠光 rim
// 旗舰神木巨刃刀身常态低强度 / 挥砍与蓄力时增亮
// 用法: Immediate + AlphaBlend 批绘制武器贴图, 本 shader 作 effect
// ============================================================

sampler uImage0 : register(s0); // 武器贴图
sampler uImage1 : register(s1); // 可平铺噪声

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 整体强度 0~1
float4 uVeinColor;   // 树液脉络色 (a=权重)
float4 uRimColor;    // 边缘翠光色 (a=权重)
float2 uTexel;       // 1/贴图尺寸 (rim 邻域步长)
float  uFlowSpeed;   // 树液流速
float  uNoiseScale;  // 脉络密度

float4 SapFlowPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 src  = tex2D(uImage0, coords);
    float4 base = src * sampleColor;
    if (uIntensity < 0.01 || src.a < 0.02)
        return base;

    // 两层反向滚动噪声相乘取窄带 → 流动树液脉
    float n1 = tex2D(uImage1, coords * uNoiseScale
               + float2(uTime * uFlowSpeed * 0.25, -uTime * uFlowSpeed)).r;
    float n2 = tex2D(uImage1, coords * uNoiseScale * 1.9
               + float2(-uTime * uFlowSpeed * 0.4, -uTime * uFlowSpeed * 0.7)).g;
    float vein   = smoothstep(0.58, 0.80, n1 * 0.62 + n2 * 0.38);
    float pulseV = 0.7 + 0.3 * sin(uTime * 3.2 + coords.y * 9.0);

    // 轮廓 rim: 邻域 alpha 缺口检测
    float aN = tex2D(uImage0, coords + float2(0.0, -uTexel.y * 2.0)).a;
    float aS = tex2D(uImage0, coords + float2(0.0,  uTexel.y * 2.0)).a;
    float aW = tex2D(uImage0, coords + float2(-uTexel.x * 2.0, 0.0)).a;
    float aE = tex2D(uImage0, coords + float2( uTexel.x * 2.0, 0.0)).a;
    float rim = saturate((1.0 - min(min(aN, aS), min(aW, aE))) * src.a * 1.6);

    float3 col = base.rgb;
    col += uVeinColor.rgb * (vein * pulseV * uVeinColor.a * uIntensity * src.a);
    col += uRimColor.rgb  * (rim * (0.75 + 0.25 * sin(uTime * 2.0)) * uRimColor.a * uIntensity * src.a);
    return float4(col, base.a);
}

technique Technique1
{
    pass SapFlowPass
    {
        PixelShader = compile ps_3_0 SapFlowPS();
    }
}
