// ============================================================
// 朱雀日冕盘着色器 — 屏幕空间 decal (不读 screenTarget, 不占全屏名额)
// 旋转冕环 ×2 + 耀斑尖刺 ×2 组反向旋转 + 日面沸腾核心 + 爆燃冲击环。
// uCharge 内部按 charge³ 生长 (MOTION §6: 隐形起步, 惊人收尾)。
// 用于: 赤日审判蓄力 / 入场日轮开屏 / 涅槃爆燃。
// 经 ACMShaders.DrawScreenSpaceDecal(Standalone) 以 Additive 绘制。
// ============================================================

sampler uImage0 : register(s0); // 载体 (共享噪声, 颜色不直接用)
sampler uNoise  : register(s1); // 共享可平铺噪声

float uTime;       // 动画时间 (秒)
float2 uCenter;    // 归一化屏幕坐标 (0~1)
float uRadius;     // 满蓄半径 (屏幕高度比例)
float uAspect;     // 屏幕宽高比
float uIntensity;  // 整体强度 0~1
float uCharge;     // 蓄力 0..1 (内部立方生长; 收缩闪烁由 CPU 侧调制后传入)
float uBurst;      // 爆燃 0..1 (冲击环外扩 + 白热闪)
float4 uColorHot;  // 核心色 (金白)
float4 uColorEdge; // 冕缘色 (赤橙)

float4 SolarFlarePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.004)
        return float4(0, 0, 0, 0);

    float2 pos = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff = pos - center;
    float dist = length(diff);

    // charge³ 生长 + 爆燃外扩
    float g = uCharge * uCharge * uCharge;
    float R = uRadius * (0.16 + 0.84 * g) * (1.0 + uBurst * 1.4);
    float nd = dist / max(R, 0.0001);
    if (nd > 3.2)
        return float4(0, 0, 0, 0);

    float ang = atan2(diff.y, diff.x);
    float angNorm = ang * 0.15915 + 0.5; // /2π → 0~1

    // ==========================================
    //  日面沸腾核心 — 极坐标噪声颗粒
    // ==========================================
    float2 suv = float2(angNorm * 3.0 + uTime * 0.02, nd * 1.4 - uTime * 0.06);
    float gran = tex2D(uNoise, suv).r * 0.6 + tex2D(uNoise, suv * 2.3 + 0.37).g * 0.4;
    float core = (1.0 - smoothstep(0.12, 0.95, nd)) * (0.62 + gran * 0.55);

    // ==========================================
    //  旋转冕环 ×2 (正反向旋转的角向调制)
    // ==========================================
    float ring1 = exp(-abs(nd - 1.00) * 9.0)  * (0.72 + 0.28 * sin(ang * 6.0 + uTime * 1.3));
    float ring2 = exp(-abs(nd - 0.72) * 12.0) * (0.68 + 0.32 * sin(-ang * 9.0 + uTime * 2.1));

    // ==========================================
    //  耀斑尖刺 — 角向余弦高次锐化, 两组反向旋转
    // ==========================================
    float sp1 = pow(abs(cos(ang * 5.0 + uTime * 0.45)), 18.0);
    float sp2 = pow(abs(cos(ang * 8.0 - uTime * 0.70 + 1.7)), 26.0);
    float spikeMask = smoothstep(2.6, 1.0, nd) * smoothstep(0.55, 1.0, nd);
    float spikes = (sp1 * 0.85 + sp2 * 0.60) * spikeMask;

    // ==========================================
    //  爆燃冲击环 — 随 uBurst 向外冲出的亮环
    // ==========================================
    float burstRing = 0.0;
    if (uBurst > 0.003)
        burstRing = exp(-abs(nd - (1.0 + uBurst * 1.5)) * 7.0) * uBurst * 1.7;

    float lum = core * 1.15 + ring1 * 0.9 + ring2 * 0.7 + spikes + burstRing;
    lum *= uIntensity;

    // 色带: 冕缘 → 核心金白; 爆燃推白热
    float3 col = lerp(uColorEdge.rgb, uColorHot.rgb, saturate(core + burstRing * 0.8));
    col = lerp(col, float3(1.0, 1.0, 1.0), saturate(uBurst * core * 0.9));

    return float4(col * lum, saturate(lum)) * sampleColor;
}

technique Technique1
{
    pass SolarFlarePass
    {
        PixelShader = compile ps_3_0 SolarFlarePS();
    }
}
