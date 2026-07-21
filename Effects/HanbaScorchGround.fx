// ============================================================
// 旱魃·干裂焦土贴花 — 屏幕空间世界锚定 decal (类 ArenaRunic 用法)
// 双层噪声等值线交织成龟裂网 + 裂缝焰光脉动 + 生长前沿亮环
// 载体 = 共享可平铺噪声(s0), 满屏绘制; 不读 screenTarget, 不占全屏名额
// 坐标约定: uCenter/uRadius 走 ACMShaders.WorldDecalParams (缩放感知)
// 输出预乘 Alpha, 配 BlendState.AlphaBlend
// ============================================================

sampler uNoiseTex : register(s0); // 可平铺三通道 FBM 噪声

float  uTime;       // 动画时间 (秒)
float2 uCenter;     // 中心归一化屏幕坐标 0~1
float  uRadius;     // 半径 (屏幕高度比例); 环带模式=环半径
float  uIntensity;  // 整体强度 0~1
float  uAspect;     // 宽高比 width/height
float  uProgress;   // 裂纹自中心生长进度 0~1 (1=完全展开)
float  uRingMode;   // 0=实心焦土场 1=环带 (干渴汲取三环)
float  uRingWidth;  // 环带半宽 (相对半径, 建议 0.10~0.25)
float4 uColorEmber; // 裂缝焰光色 (rgb; a=焰光权重)
float4 uColorAsh;   // 焦黑灰烬底色 (rgb; a=底色覆盖权重)

float4 ScorchGroundPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 local  = (pos - center) / max(uRadius, 0.001); // 以半径为单位的本地坐标 (随世界中心锚定)
    float  d      = length(local);

    // 远处早退
    if (d > 2.4)
        return float4(0, 0, 0, 0);

    // —— 龟裂网: 两层不同尺度噪声的等值线 (level-set 细线), max 交织成裂纹网络 ——
    float n1 = tex2D(uNoiseTex, local * 0.62 + float2(13.1, 7.7)).r;
    float n2 = tex2D(uNoiseTex, local * 1.45 + float2(71.3, 33.9)).g;
    float c1 = 1.0 - smoothstep(0.0, 0.085, abs(n1 - 0.5));
    float c2 = 1.0 - smoothstep(0.0, 0.060, abs(n2 - 0.52));
    float crack = max(c1, c2 * 0.8);

    // —— 生长前沿: 裂纹自中心向外推进, 前沿处一圈亮光 ——
    float grow  = saturate(uProgress) * 1.08;
    float front = smoothstep(grow, grow - 0.16, d);              // 前沿内侧=1
    float frontGlow = smoothstep(0.09, 0.0, abs(d - grow)) * (uProgress < 0.995 ? 1.0 : 0.0);

    // —— 区域掩码: 实心场 / 环带 ——
    float mask;
    if (uRingMode > 0.5)
    {
        float hw = max(uRingWidth, 0.02);
        mask = 1.0 - smoothstep(hw * 0.55, hw, abs(d - 1.0));
    }
    else
    {
        mask = smoothstep(1.02, 0.80, d);
    }
    mask *= front;

    // —— 裂缝焰光脉动 (随噪声相位错开, 呼吸感) ——
    float pulse = 0.68 + 0.32 * sin(uTime * 2.6 + n1 * 12.0 + d * 4.5);
    float ember = crack * pulse * uColorEmber.a;

    // —— 焦黑底: 噪声斑驳的灰烬色 ——
    float charCover = mask * uColorAsh.a * (0.38 + n2 * 0.42);

    float3 col = uColorAsh.rgb * charCover;
    col += uColorEmber.rgb * (ember * mask + frontGlow * 0.95);

    float alpha = saturate(charCover + ember * mask * 0.85 + frontGlow * 0.8) * uIntensity;
    return float4(col * uIntensity, alpha);
}

technique Technique1
{
    pass ScorchGroundPass
    {
        PixelShader = compile ps_3_0 ScorchGroundPS();
    }
}
