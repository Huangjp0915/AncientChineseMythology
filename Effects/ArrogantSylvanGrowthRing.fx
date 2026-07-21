// ============================================================
// 傲世神木·年轮绽放环 — 屏幕空间 decal (Additive, 不占全屏名额)
// 同心年轮细环 + 藤脉辐条 + 生长前沿白热 + 金边翠芯径向配色
// 使用点: 烙印绽放 / 天崩下劈定标 / 母树种主爆 / 鞭咬新星 / 世界树之矢
// 载体: ACMShaders.DrawScreenSpaceDecal (喂共享可平铺噪声 s0)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;        // 动画时间(秒)
float2 uCenter;      // 中心归一化屏幕坐标 0~1
float  uRadius;      // 最大半径 (屏幕高度比例)
float  uIntensity;   // 整体强度 0~1
float  uAspect;      // 宽高比 width/height
float  uProgress;    // 生长进度 0~1 (前沿位置)
float4 uColorGold;   // 金 (外沿/神威)
float4 uColorJade;   // 翠 (内芯/生命)
float  uRingFreq;    // 年轮密度 (建议 7~14)

float4 GrowthRingPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);
    float  nd     = dist / max(uRadius, 0.0001);

    // 早退: 环外像素直接丢弃
    if (nd > 1.25)
        return float4(0, 0, 0, 0);

    float angle   = atan2(diff.y, diff.x);
    float angNorm = angle / 6.28318 + 0.5;

    // 角向噪声扰动: 让年轮呈有机木纹而非机械同心圆
    float n = tex2D(uImage0, float2(angNorm * 3.0 + uTime * 0.02, nd * 1.7)).r;
    float ndW = nd + (n - 0.5) * 0.10;

    float front = max(uProgress, 0.02);
    // 只点亮生长前沿以内
    float inside = 1.0 - smoothstep(front, front + 0.06, ndW);
    if (inside <= 0.002)
        return float4(0, 0, 0, 0);

    // 同心年轮细环 (随生长向外掠过)
    float rings = 0.5 + 0.5 * sin((ndW * uRingFreq - uProgress * 3.0) * 6.28318);
    rings = pow(rings, 7.0);

    // 径向藤脉辐条 (被噪声扭出植物感)
    float veins = pow(abs(sin(angle * 7.0 + (n - 0.5) * 2.6 + ndW * 3.5)), 14.0);
    veins *= smoothstep(1.0, 0.35, ndW) * 0.8;

    // 生长前沿白热线
    float frontGlow = exp(-abs(ndW - front) * 26.0);

    // 中心核心辉光
    float core = exp(-ndW * 4.5) * 0.55;

    float shape = saturate(rings * 0.75 + veins + core) * inside;

    // 金边翠芯: 芯部翠, 靠近前沿转金
    float3 col = lerp(uColorJade.rgb, uColorGold.rgb, smoothstep(0.25, 0.95, ndW / front));
    col += float3(1.0, 0.98, 0.9) * frontGlow * 1.1;

    float pulseA = 0.9 + 0.1 * sin(uTime * 5.0 + angle * 2.0);
    float alpha = saturate((shape * pulseA + frontGlow) * uIntensity);
    return float4(col, alpha);
}

technique Technique1
{
    pass GrowthRingPass
    {
        PixelShader = compile ps_3_0 GrowthRingPS();
    }
}
