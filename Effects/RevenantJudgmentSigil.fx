// ============================================================
// 亡魂系列·阎罗判决印记 — 屏幕空间贴花 (业秤宣判 / 断业刀处决 / 孽镜断罪)
// 方形官印外框 + 朱笔勾决环(随 uStamp 扫过) + 竖排判词刻痕 + 墨晕收束 + 业火溢边
// 经 ACMShaders.DrawScreenSpaceDecalStandalone 调用 (载体=满屏噪声贴图, Additive)
// s0 = 可平铺噪声 (RGB 三通道独立)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声

float  uTime;            // 动画时间(秒)
float2 uCenter;          // 中心归一化屏幕坐标 0~1
float  uRadius;          // 半径 (屏幕高度比例; 印面半边长)
float  uIntensity;       // 整体强度 0~1
float  uAspect;          // 宽高比 width/height
float4 uColorPrimary;    // 朱批主色 (判决红)
float4 uColorSecondary;  // 业火辅色 (青黄魂火)
float  uStamp;           // 盖印进度 0~1: 0=墨晕弥散落印, ~0.5=勾决环扫完, 1=印痕定格+业火散逸

// 窄带轮廓: 距离 d 到基准 c 的柔和线条
float band(float d, float c, float halfWidth)
{
    return saturate(1.0 - abs(d - c) / max(halfWidth, 0.0005));
}

float4 JudgmentSigilPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    // 屏幕 → 印面局部空间 (aspect 校正; q 约 -1..1 为印面内)
    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;

    // 官印微斜 (盖印总有一点歪, 定格前带一丝旋摆)
    float tilt = 0.07 + (1.0 - uStamp) * 0.06 * sin(uTime * 9.0);
    float cs = cos(tilt), sn = sin(tilt);
    diff = float2(diff.x * cs - diff.y * sn, diff.x * sn + diff.y * cs);

    float2 q = diff / max(uRadius, 0.0005);
    float boxD = max(abs(q.x), abs(q.y));   // chebyshev: 等距线为正方形
    float r    = length(q);
    if (boxD > 1.55)
        return float4(0, 0, 0, 0);

    // 墨晕扭曲: 落印初期边缘洇开, 定格后收干
    float2 nUV1 = q * 0.85 + float2(uTime * 0.05, -uTime * 0.04);
    float2 nUV2 = q * 2.10 - float2(uTime * 0.07, uTime * 0.03);
    float n1 = tex2D(uImage0, nUV1).r;
    float n2 = tex2D(uImage0, nUV2).g;
    float bleed = (1.0 - uStamp) * 0.16 + 0.03;
    float dBox = boxD + (n1 - 0.5) * bleed;
    float dR   = r    + (n2 - 0.5) * bleed;

    // 线条锐度: 落印时糊, 定格后利
    float crisp = lerp(2.2, 1.0, uStamp);

    // —— 1) 方形官印外框 (双线: 粗外描 + 细内描) ——
    float frame  = band(dBox, 0.98, 0.045 * crisp);
    frame        = max(frame, band(dBox, 0.86, 0.022 * crisp) * 0.75);

    // —— 2) 朱笔勾决环: 圆环随 uStamp 从顶端顺时针扫过, 笔锋前端过曝 ——
    float angle   = atan2(q.y, q.x);                  // -pi..pi
    float angNorm = frac(angle / 6.28318 + 0.25);     // 顶端起 0..1
    float sweep   = saturate(uStamp * 2.2);           // 前半程扫完勾决环
    float ringIn  = band(dR, 0.56, 0.055 * crisp);
    float penMask = step(angNorm, sweep);
    float ring    = ringIn * penMask;
    // 笔锋: 扫描前端 0.06 区段的亮头
    float tipGlow = ringIn * saturate(1.0 - abs(angNorm - sweep) / 0.06) * step(sweep, 0.999);
    // 勾尾顿笔: 扫完后在收笔处留一个出头挑钩
    float hook = band(dR, 0.56, 0.10) * saturate(1.0 - abs(angNorm - 0.02) / 0.05) * step(0.999, sweep);

    // —— 3) 竖排判词刻痕: 印面内 3~4 列竖向断续笔画 (噪声离散采样) ——
    float words = 0.0;
    if (boxD < 0.78)
    {
        float col = floor((q.x + 0.78) / 0.42);            // 列号
        float colCenter = col * 0.42 - 0.78 + 0.21;
        float colBand = band(q.x, colCenter, 0.055 * crisp);
        // 沿列纵向的断续笔画 (每列相位不同; 随 uStamp 自上而下书写显现)
        float wn = tex2D(uImage0, float2(col * 0.173 + 0.31, q.y * 1.35 + col * 0.377)).b;
        float strokeOn = smoothstep(0.42, 0.62, wn);
        float writeMask = step(q.y, lerp(-1.0, 1.05, saturate(uStamp * 1.6)));
        words = colBand * strokeOn * writeMask * 0.85;
    }

    // —— 4) 业火溢边: 定格后印框外沿业火舔舐, 向上偏浮 ——
    float flame = 0.0;
    if (uStamp > 0.55 && dBox > 0.9)
    {
        float rise = uTime * 0.55;
        float fn = tex2D(uImage0, float2(angNorm * 3.0 + n1 * 0.4, dBox * 1.4 - rise)).r;
        float flameBand = saturate(1.0 - abs(dBox - 1.12) / 0.38);
        float upBias = saturate(0.45 - q.y * 0.75);        // 上缘更旺
        flame = smoothstep(0.55, 0.9, fn) * flameBand * upBias * saturate((uStamp - 0.55) * 2.8);
    }

    // —— 合成: 朱批为主, 业火为辅; 笔锋/顿笔加法过曝 ——
    float inkShape = max(max(frame, ring), max(words, hook * 1.2));
    float3 col3 = uColorPrimary.rgb * inkShape;
    col3 += uColorPrimary.rgb * (tipGlow * 1.6 + hook * 0.8);            // 笔锋白热
    col3 += float3(1.0, 0.9, 0.8) * tipGlow * 0.7;
    col3 += uColorSecondary.rgb * flame * 0.9;                            // 业火
    // 印面淡淡的朱底 (盖印瞬间最重, 随定格褪去)
    float pad = saturate(1.0 - boxD) * (1.0 - uStamp) * 0.18;
    col3 += uColorPrimary.rgb * pad;

    float alpha = saturate(inkShape + tipGlow + flame * 0.8 + pad);
    // 定格后整体呼吸余韵
    alpha *= 0.9 + 0.1 * sin(uTime * 6.0 + r * 4.0);

    return float4(col3 * uIntensity, alpha * uIntensity);
}

technique Technique1
{
    pass JudgmentSigilPass
    {
        PixelShader = compile ps_3_0 JudgmentSigilPS();
    }
}
