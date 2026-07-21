// ============================================================
// 毗沙门天 · 金身法相 — 本体贴图单 pass (s0=本体贴图, s1=共享噪声)
// alpha 边缘 rim 金光 + 体内金纹流动 + 受击/爆发白闪 + 死亡龟裂
// (裂缝发金光 → 低于阈值镂空溶解)。SpriteBatch Immediate + 本 effect 绘制。
// 注: tML 贴图为预乘 alpha, 附加光直接加在 rgb 上即为加性辉光。
// ============================================================

sampler uImage0 : register(s0); // 本体贴图
sampler uNoise  : register(s1); // 可平铺噪声

float  uTime;         // 动画时间(秒)
float  uIntensity;    // 常驻金身强度 0~1
float2 uTexel;        // 1/贴图尺寸
float4 uRimColor;     // 金边色
float4 uFlowColor;    // 体内金纹色
float  uFlashWhite;   // 0~1 白闪(受击/爆发)
float  uCrack;        // 0~1 死亡龟裂进度

float4 VaisravanaGoldBodyPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 tex = tex2D(uImage0, coords);
    if (tex.a < 0.01)
        return float4(0, 0, 0, 0);

    // —— rim 检测: 邻域 alpha 最小值越低越接近轮廓 ——
    float aL = tex2D(uImage0, coords + float2(-uTexel.x * 2.0, 0)).a;
    float aR = tex2D(uImage0, coords + float2( uTexel.x * 2.0, 0)).a;
    float aU = tex2D(uImage0, coords + float2(0, -uTexel.y * 2.0)).a;
    float aD = tex2D(uImage0, coords + float2(0,  uTexel.y * 2.0)).a;
    float edge = 1.0 - min(min(aL, aR), min(aU, aD));
    edge = saturate(edge * 1.2) * tex.a;

    // —— 体内金纹上升流动 ——
    float flow = tex2D(uNoise, coords * 2.2 + float2(0, -uTime * 0.16)).r;
    flow = pow(saturate(flow), 3.0);

    float3 col = tex.rgb;
    col += uRimColor.rgb * edge * (0.55 + 0.25 * sin(uTime * 2.1)) * uIntensity;
    col += uFlowColor.rgb * flow * 0.4 * uIntensity * tex.a;

    float alpha = tex.a;

    // —— 死亡龟裂: 噪声阈值裂缝发光 + 低于阈值镂空 ——
    if (uCrack > 0.001)
    {
        float n = tex2D(uNoise, coords * 2.6 + float2(uTime * 0.01, 0)).g;
        float hole = smoothstep(uCrack * 0.62, uCrack * 0.62 - 0.05, n);
        float seam = 1.0 - smoothstep(0.0, 0.09, abs(n - uCrack * 0.72));
        col += uRimColor.rgb * seam * 2.2 * uCrack;
        col  = lerp(col, uRimColor.rgb * 1.6, saturate(seam * uCrack));
        alpha *= 1.0 - hole;
        col   *= 1.0 - hole;
    }

    // —— 白闪(过曝) ——
    col = lerp(col, float3(1.4, 1.35, 1.2) * tex.a, saturate(uFlashWhite));

    return float4(col, alpha) * sampleColor;
}

technique Technique1
{
    pass VaisravanaGoldBodyPass
    {
        PixelShader = compile ps_3_0 VaisravanaGoldBodyPS();
    }
}
