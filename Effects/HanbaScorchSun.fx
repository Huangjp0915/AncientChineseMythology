// ============================================================
// 旱魃·焦日日轮 — 程序化太阳盘 (加性 overlay)
// 白热核心 + limb darkening + 环向噪声焰舌 + 日冕射线 + 柔光晕
// 用于: HanbaSky 天幕焦日 / 焚天坠日巨日本体 / 灼柱蓄力日核
// 载体 = 共享噪声(s0)满屏绘制, 建议 BlendState.Additive; 不占全屏名额
// 环向采样用 (cos,sin) 圆环走可平铺噪声 → 无接缝
// ============================================================

sampler uNoiseTex : register(s0); // 可平铺三通道 FBM 噪声

float  uTime;       // 动画时间 (秒)
float2 uCenter;     // 日心归一化屏幕坐标 0~1
float  uRadius;     // 日盘半径 (屏幕高度比例)
float  uIntensity;  // 整体强度 0~1
float  uAspect;     // 宽高比 width/height
float  uFlare;      // 爆发度 0~1 (蚀日/坠日时拉高: 焰舌变长, 射线增辉)
float  uAsh;        // 灰蚀度 0~1 (死亡演出: 日轮熄灭成灰)
float4 uColorCore;  // 核心白热色
float4 uColorEdge;  // 边缘焰色

float4 ScorchSunPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  d      = length(diff) / max(uRadius, 0.001);

    if (d > 3.6)
        return float4(0, 0, 0, 0);

    float ang = atan2(diff.y, diff.x);
    float2 cir = float2(cos(ang), sin(ang));

    // —— 焰舌: 环向噪声扰动盘缘半径 (圆环采样无接缝) ——
    float n1 = tex2D(uNoiseTex, cir * 0.35 + uTime * 0.030).r;
    float n2 = tex2D(uNoiseTex, cir * 0.80 - uTime * 0.022).g;
    float lick  = n1 * 0.65 + n2 * 0.35;
    float edgeR = 1.0 + (lick - 0.35) * (0.28 + uFlare * 0.42);

    // —— 日盘 + 白热核心 (limb darkening: 核心亮, 盘缘转焰色) ——
    float disc = smoothstep(edgeR, edgeR - 0.20, d);
    float core = smoothstep(0.95, 0.12, d);

    // —— 盘面日斑: 低频噪声斑驳压暗 ——
    float spots = tex2D(uNoiseTex, diff / max(uRadius, 0.001) * 0.42 + uTime * 0.008).b;

    // —— 日冕射线 (盘外) + 柔光晕 ——
    float outside = smoothstep(0.92, 1.15, d);
    float rays = pow(abs(sin(ang * 9.0 + uTime * 0.55 + n1 * 2.2)), 6.0)
               * outside * smoothstep(2.9, 1.05, d);
    float halo = exp2(-d * 2.1);

    float3 col = uColorEdge.rgb * disc;
    col = lerp(col, uColorCore.rgb, core * disc);
    col *= 1.0 - spots * 0.24 * disc;
    col += uColorEdge.rgb * rays * (0.30 + uFlare * 0.55);
    col += uColorCore.rgb * halo * (0.22 + uFlare * 0.55);

    // 爆发增辉 → 灰蚀熄灭 (去饱和 + 减亮)
    col *= 1.0 + uFlare * 0.8;
    float grey = dot(col, float3(0.333, 0.333, 0.333));
    col = lerp(col, grey.xxx * 0.45, saturate(uAsh));
    col *= uIntensity * (1.0 - saturate(uAsh) * 0.72);

    return float4(col, 0.0);
}

technique Technique1
{
    pass ScorchSunPass
    {
        PixelShader = compile ps_3_0 ScorchSunPS();
    }
}
