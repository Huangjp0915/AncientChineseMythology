// ============================================================
// 天庭观察者 · 扫描/凝视光锥 — 屏幕空间 SDF (噪声载体全屏绘制)
// 角向锥体 + 内部径向扫描纹 + 边缘亮线; uLock 驱动"搜索→锁定"
// 变化: 收窄 / 换色 / 扫描纹加速; uFlash 为锁定瞬间的内收脉冲环。
// 色彩语言: 搜索=冷钢监视蓝, 锁定=权柄金 (纯红只留给审判射线)。
// 喂可平铺噪声贴图(s0)。
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB三通道独立)

float  uTime;        // 动画时间(秒)
float2 uCenter;      // 锥顶点归一化屏幕坐标 0~1
float  uAspect;      // 宽高比 width/height
float  uIntensity;   // 整体强度 0~1
float  uDir;         // 锥中心方向(弧度, 屏幕空间)
float  uHalfAngle;   // 搜索态锥半角(弧度)
float  uLength;      // 锥长 (屏幕高度比例)
float  uLock;        // 0=搜索 1=锁定 (收窄+换色+加速)
float  uFlash;       // 锁定瞬间脉冲 0~1 (1=刚锁定, 衰减至0)
float4 uColorSearch; // 搜索色 (冷钢蓝)
float4 uColorLock;   // 锁定色 (权柄金)

float4 ScanConePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  r      = length(diff);
    float  rN     = r / max(uLength, 0.001);

    // 锥长外整体早退 (省噪声采样)
    if (rN > 1.12)
        return float4(0, 0, 0, 0);

    // 与锥心方向的角差 (atan2 组合回绕, 稳健)
    float ang  = atan2(diff.y, diff.x);
    float dAng = ang - uDir;
    dAng = atan2(sin(dAng), cos(dAng));

    // 锁定时锥体收窄 45%
    float halfA = max(uHalfAngle * (1.0 - 0.45 * uLock), 0.02);
    float angT  = abs(dAng) / halfA; // 0=锥心 1=锥缘

    if (angT > 1.25)
        return float4(0, 0, 0, 0);

    // —— 遮罩: 锥内主体 / 长度衰减 / 顶点收口 ——
    float inside   = smoothstep(1.0, 0.82, angT);
    float lenFade  = smoothstep(1.05, 0.70, rN);
    float nearFade = smoothstep(0.0, 0.055, rN);

    // —— 内部径向扫描纹: 行进条带, 锁定时加速 ——
    float bandSpeed = 2.2 + 5.0 * uLock;
    float bands = 0.5 + 0.5 * sin(rN * 34.0 - uTime * bandSpeed * 3.0);
    bands = pow(bands, 3.0) * 0.45;

    // —— 沿锥向噪声流 (探照灯尘埃感) ——
    float2 nUV = float2(rN * 2.3 - uTime * 0.55, dAng * 1.7 + uTime * 0.07);
    float n = tex2D(uImage0, nUV).r;
    float streak = smoothstep(0.35, 0.85, n) * 0.30;

    // —— 锥缘亮线 (读数边界) ——
    float edgeLine = smoothstep(0.10, 0.0, abs(angT - 0.90)) * lenFade;

    // —— 锥心细线 (锁定后弹道预告读数) ——
    float coreLine = smoothstep(0.10, 0.0, angT) * 0.55 * uLock;

    // —— 锁定瞬间: 一圈脉冲环从锥口向顶点收拢 ——
    float ringPos = 1.0 - uFlash;      // uFlash 1→0 时环由外向内行进
    float ring = smoothstep(0.07, 0.0, abs(rN - ringPos)) * uFlash * inside;

    float3 col = lerp(uColorSearch.rgb, uColorLock.rgb, uLock);
    float body = inside * (0.22 + bands + streak) + edgeLine * 0.85 + coreLine + ring * 1.2;

    // 搜索态呼吸 (锁定后恒亮更具威胁)
    float breath = lerp(0.82 + 0.18 * sin(uTime * 4.0), 1.0, uLock);

    float alpha = saturate(body * lenFade * nearFade * breath * uIntensity);
    col += uColorLock.rgb * ring * 0.8; // 脉冲环偏金过曝

    // 与 ArenaRunic 同约定: 直通 alpha 输出, 由 AlphaBlend 批混合
    return float4(saturate(col), alpha);
}

technique Technique1
{
    pass ScanConePass
    {
        PixelShader = compile ps_3_0 ScanConePS();
    }
}
