// ============================================================
// 敖钦冲击火环 — 屏幕空间 SDF 环带 (满屏噪声载体, 仅 PS)
// 用途:
//   uMode=0 炼狱茧泄压火环: 致命橙红环带 + 白热前缘 + 翠玉安全缺口
//   uMode=1 蒸汽冲击波(无伤): 白橙提亮降饱和 (相变/逆鳞爆气演出)
// 建议 Additive 绘制; uCenter/uRadius 经 ACMShaders.WorldDecalParams 换算
// ============================================================

sampler uImage0 : register(s0); // 满屏绘制载体 = 共享噪声

float uTime;       // 动画时间 (秒)
float2 uCenter;    // 环心 (屏幕 UV)
float uRadius;     // 半径 (屏幕高度比例)
float uBand;       // 环带半宽 (屏幕高度比例)
float uGapAngle;   // 缺口中心角 (弧度)
float uGapHalf;    // 缺口半宽 (弧度, <=0 表示无缺口)
float uIntensity;  // 总强度 0~1
float uAspect;     // 宽高比 width/height
float uMode;       // 0=炼狱火环 1=蒸汽冲击
float4 uColorCore; // 前缘/核心色 (白热)
float4 uColorEdge; // 环带主体色 (橙红)
float4 uColorSafe; // 缺口安全色 (翠玉)

float4 ShockRingPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 pos = float2(coords.x * uAspect, coords.y);
    float2 c = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff = pos - c;
    float dist = length(diff);

    // 粗剔除: 远离环带直接透明
    if (abs(dist - uRadius) > uBand * 3.0 || uIntensity < 0.001)
        return float4(0, 0, 0, 0);

    float ang = atan2(diff.y, diff.x);

    // 角向火焰噪声: 环沿角度起伏 + 随时间外涌
    float n = tex2D(uImage0, float2(ang * 0.955 + uTime * 0.08,
                                    dist * 2.2 - uTime * 0.55)).r;
    float ripple = (n - 0.5) * uBand * 1.2;

    float d = abs(dist - uRadius + ripple);
    float band = smoothstep(uBand, uBand * 0.22, d);

    // 白热前缘线 (环带外侧)
    float front = smoothstep(uBand * 0.4, 0.0,
                             abs(dist - (uRadius + uBand * 0.45) + ripple * 0.5));

    float energy = band * 0.8 + front * 0.95;
    if (energy <= 0.004)
        return float4(0, 0, 0, 0);

    // 缺口: 角向环形 wrap 距离
    float safeMask = 0.0;
    if (uGapHalf > 0.001)
    {
        float adiff = abs(frac((ang - uGapAngle) / 6.28318 + 0.5) - 0.5) * 6.28318;
        safeMask = smoothstep(uGapHalf, uGapHalf * 0.7, adiff);
    }

    float3 fire = lerp(uColorEdge.rgb, uColorCore.rgb, saturate(band * band * 0.6 + front));

    // 缺口内火焰熄灭 → 翠玉安全芒 (弱亮度, 不与红冲突)
    float3 col = lerp(fire, uColorSafe.rgb, safeMask);
    float alpha = energy * uIntensity * lerp(1.0, 0.4, safeMask);

    if (uMode > 0.5)
    {
        // 蒸汽: 提白降饱和, 无伤观感
        col = lerp(col, float3(1.0, 0.97, 0.9), 0.5);
        alpha *= 0.8;
    }

    return float4(col * alpha, 0.0);
}

technique Technique1
{
    pass ShockRingPass
    {
        PixelShader = compile ps_3_0 ShockRingPS();
    }
}
