// ============================================================
// 傲世神木·风暴环带 — 屏幕空间 decal (Additive, 不占全屏名额)
// 极坐标旋转藤叶流 + 环带 mask + 金边翠芯; 环带即伤害判定的可视化
// 使用点: 落叶风暴旋镖螺旋风暴环 / 山海典叶暴漩涡
// 载体: ACMShaders.DrawScreenSpaceDecal (喂共享可平铺噪声 s0)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;        // 动画时间(秒)
float2 uCenter;      // 环带中心归一化屏幕坐标
float  uRadius;      // 环带中心半径 (屏幕高度比例)
float  uIntensity;   // 整体强度 0~1
float  uAspect;      // 宽高比
float  uBandHalf;    // 环带半宽 (屏幕高度比例)
float  uSpin;        // 旋转速度 (正=顺时针视觉)
float  uPulse;       // 0~1 脉冲增亮 (环带脉冲释放叶片的提示)
float4 uColorGold;   // 金 (环带边沿)
float4 uColorJade;   // 翠 (环带芯部)

float4 VortexPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);

    // 到环带中心线的法向距离, 归一化 (0=带芯, 1=带边)
    float bn = abs(dist - uRadius) / max(uBandHalf, 0.0001);
    if (bn > 2.2)
        return float4(0, 0, 0, 0);

    float angle   = atan2(diff.y, diff.x);
    float angNorm = angle / 6.28318;
    float rn      = dist / max(uRadius, 0.0001);

    // 旋转藤叶流: 两层切向拉长条纹, 不同速度制造湍流
    float flow1 = tex2D(uImage0, float2(angNorm * 5.0 - uTime * uSpin + rn * 0.8, rn * 2.0)).r;
    float flow2 = tex2D(uImage0, float2(angNorm * 9.0 - uTime * uSpin * 1.6 + 0.37, rn * 3.0)).g;
    float leaves = smoothstep(0.40, 0.85, flow1 * 0.62 + flow2 * 0.48);

    // 环带 mask (芯亮边淡)
    float bandMask = 1.0 - smoothstep(0.45, 1.05, bn);

    // 内外沿金线 (环带边界清晰可读 = 判定边界)
    float edgeLine = pow(saturate(1.0 - abs(bn - 1.0) * 2.6), 3.0) * 0.9;

    // 旋转辐条 (卖旋转速度)
    float spokes = pow(abs(sin(angle * 3.0 - uTime * uSpin * 3.5)), 10.0) * bandMask * 0.35;

    float pulseBoost = 1.0 + uPulse * 1.2;
    float shape = saturate(leaves * bandMask + edgeLine + spokes) * pulseBoost;

    // 金边翠芯: 带芯翠, 带边金
    float3 col = lerp(uColorJade.rgb, uColorGold.rgb, smoothstep(0.35, 1.0, bn));
    col += uColorGold.rgb * edgeLine * 0.5;
    col += float3(1.0, 0.98, 0.9) * uPulse * bandMask * 0.35;

    float alpha = saturate(shape * uIntensity);
    return float4(col, alpha);
}

technique Technique1
{
    pass VortexPass
    {
        PixelShader = compile ps_3_0 VortexPS();
    }
}
