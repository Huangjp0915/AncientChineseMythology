// ============================================================
// 敖钦热浪蜃景着色器 — 专属全屏后处理 (走 RequestFullscreenSlot 名额契约)
// 垂直对流扭曲(热空气上升蜃景) + RGB 色散 + 余烬飘浮亮点
//  + 径向冲击波环(uVent, 咆哮/泄压/死亡冲击) + 焦黑暗角
// 喂 Main.screenTarget(s0) + 共享可平铺噪声(s1)
// 与 GenericWarp(heat) 的差异: 对流方向性(向上涌)、余烬粒子层、vent 冲击环
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标
sampler uNoise  : register(s1); // 共享可平铺噪声

float  uTime;       // 动画时间(秒)
float2 uCenter;     // 热源中心 归一化屏幕坐标
float  uIntensity;  // 对流扭曲强度 0~1
float  uAspect;     // 宽高比 width/height
float  uEmber;      // 余烬飘浮密度 0~1
float  uVent;       // 冲击波环进度 0~1 (0=无)
float2 uVentCenter; // 冲击波中心 归一化屏幕坐标
float4 uTint;       // 主题染色 (rgb=暖色, a=覆盖强度)

static const float3 EmberGlow = float3(1.0, 0.62, 0.22);

float4 HeatHazePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    bool ventOn = uVent > 0.001 && uVent < 0.999;
    if (uIntensity < 0.01 && !ventOn && uEmber < 0.01)
        return tex2D(uImage0, coords);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float  dist   = length(pos - center);

    // 距热源衰减(半径按屏幕高度 ~1.1 归一), 底部略强(热浪自下而上)
    float falloff = smoothstep(1.6, 0.15, dist) * (0.65 + coords.y * 0.35);

    // ---------- 垂直对流扭曲 ----------
    // 两层噪声沿 y 向上滚动 = 上升热流蜃景
    float n1 = tex2D(uNoise, float2(coords.x * 3.1, coords.y * 2.2 + uTime * 0.55)).r;
    float n2 = tex2D(uNoise, float2(coords.x * 5.3 + 0.37, coords.y * 3.7 + uTime * 0.90)).g;
    float conv = (n1 * 0.65 + n2 * 0.35) - 0.5;

    float strength = uIntensity * falloff * 0.024;
    float2 uvOffset = float2(conv * 0.35, conv) * strength; // 主位移在竖直向

    // ---------- 冲击波环 ----------
    float ventPush = 0.0;
    float ventRim = 0.0;
    if (ventOn)
    {
        float2 vpos = float2(uVentCenter.x * uAspect, uVentCenter.y);
        float2 vdiff = pos - vpos;
        float vdist = length(vdiff);
        float ringR = uVent * 1.35;                       // 环半径随进度扩张
        float band = exp(-pow((vdist - ringR) * 9.0, 2.0)); // 高斯环带
        float ventAmp = (1.0 - uVent);                      // 扩张同时衰减
        float2 vdir = vdiff / max(vdist, 0.001);
        float2 push = vdir * band * ventAmp * 0.030;
        push.x /= uAspect;
        uvOffset += push;
        ventPush = band * ventAmp;
        ventRim = band * ventAmp;
    }

    uvOffset.x /= uAspect;
    float2 distortedUV = clamp(coords + uvOffset, 0.001, 0.999);
    float4 scene = tex2D(uImage0, distortedUV);

    // ---------- RGB 色散(热对流处轻微) ----------
    float chroma = (uIntensity * falloff * 0.35 + ventPush * 0.8) * 0.010;
    float2 cOff = float2(chroma / uAspect, 0.0);
    scene.r = lerp(scene.r, tex2D(uImage0, distortedUV + cOff).r, 0.75);
    scene.b = lerp(scene.b, tex2D(uImage0, distortedUV - cOff).b, 0.75);

    // ---------- 余烬飘浮亮点 ----------
    if (uEmber > 0.01)
    {
        // 噪声高值阈截为稀疏亮点, 向上飘 + 微横漂
        float2 euv1 = float2(coords.x * 7.0 + uTime * 0.03, coords.y * 4.5 + uTime * 0.16);
        float2 euv2 = float2(coords.x * 11.0 - uTime * 0.05, coords.y * 7.5 + uTime * 0.26);
        float e1 = tex2D(uNoise, euv1).b;
        float e2 = tex2D(uNoise, euv2).r;
        float spark = smoothstep(0.965, 1.0, e1 * 0.55 + e2 * 0.50);
        float flicker = 0.65 + 0.35 * sin(uTime * 9.0 + coords.x * 40.0 + coords.y * 60.0);
        scene.rgb += EmberGlow * spark * flicker * uEmber * falloff * 2.2;
    }

    // ---------- 暖色罩染 + 焦黑暗角 ----------
    float tintCover = falloff * uTint.a * uIntensity;
    scene.rgb = lerp(scene.rgb, scene.rgb * (1.0 - tintCover * 0.5) + uTint.rgb * tintCover * 0.55, saturate(tintCover * 1.2));

    float2 vc = coords - 0.5;
    vc.x *= uAspect;
    float vig = saturate(dot(vc, vc) * 1.05);
    scene.rgb *= 1.0 - vig * uIntensity * 0.30;

    // 冲击环亮边
    scene.rgb += EmberGlow * ventRim * 0.55;

    return scene;
}

technique Technique1
{
    pass HeatHazePass
    {
        PixelShader = compile ps_3_0 HeatHazePS();
    }
}
