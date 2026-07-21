// ============================================================
// 定海神针·真 — 程序化巨针柱体 (如意金箍棒大招专用)
// 金红双色渐变 + 白热芯 + 滚动紧箍环纹 + 尖端收束 + 落地脉冲/消散
// 载体: 竖直拉伸 quad (u = 横向, v = 纵向, v=1 为针尖/地面端), s0 = SoftGlow (横向柔边)
// ============================================================

sampler uTexture : register(s0); // SoftGlow — 取 y=0.5 行作横向高斯柔边

float  uTime;       // 动画时间 (秒)
float  uIntensity;  // 整体强度 0~1
float  uScroll;     // 环纹纵向滚动相位 (下坠时滚动, 落地骤停)
float  uImpact;     // 落地脉冲 0~1 (白闪 + 增亮)
float  uFade;       // 消散 0~1 (自上而下裁掉)
float4 uColorGold;  // 箍金
float4 uColorRed;   // 杆红

float4 PillarDropPS(float4 vertColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.005)
        return float4(0, 0, 0, 0);

    // 尖端收束: v>0.86 起宽度向针尖收拢
    float taper = 1.0 - smoothstep(0.86, 1.0, uv.y) * 0.72;
    float x = (uv.x - 0.5) / max(taper, 0.05);
    float ax = saturate(abs(x) * 2.0); // 0 = 轴心, 1 = 边缘

    // 径向剖面: 白热芯 → 金 → 红边
    float core = 1.0 - smoothstep(0.0, 0.30, ax);
    float body = 1.0 - smoothstep(0.55, 0.98, ax);
    float3 col = lerp(uColorGold.rgb, uColorRed.rgb, smoothstep(0.18, 0.85, ax));
    col = lerp(float3(1.0, 0.97, 0.88), col, smoothstep(0.0, 0.32, ax));

    // 紧箍环纹: 纵向等距环, 下坠时随 uScroll 滚动
    float rv = frac(uv.y * 9.0 + uScroll);
    float ring = 1.0 - smoothstep(0.05, 0.14, abs(rv - 0.5));
    col = lerp(col, uColorGold.rgb * 1.25, ring * 0.5 * body);
    float shade = 1.0 - ring * 0.22 * (1.0 - core); // 环处杆身微暗 (立体感)

    // 顶端渐隐 (针自天而降, 顶部无硬边)
    float capFade = smoothstep(0.0, 0.10, uv.y);

    // 消散: 自上而下裁掉 + 收细
    float dissolve = 1.0 - smoothstep(uFade * 1.1 - 0.1, uFade * 1.1, uv.y * 0.999);
    if (uFade > 0.001)
        dissolve = smoothstep(uFade - 0.15, uFade + 0.05, uv.y); // 上段先消
    float alive = lerp(1.0, dissolve, step(0.001, uFade));

    // 落地脉冲: 整针增亮 + 白闪, 靠近针尖更强
    float impactBoost = uImpact * (0.6 + 0.8 * smoothstep(0.5, 1.0, uv.y));
    col = lerp(col, float3(1.0, 0.99, 0.94), saturate(impactBoost * 0.7));

    // 横向柔边 (SoftGlow y=0.5 行为高斯) + 呼吸
    float soft = tex2D(uTexture, float2(saturate(uv.x), 0.5)).r;
    float breath = 0.94 + 0.06 * sin(uTime * 5.0 + uv.y * 4.0);

    float alpha = (body * 0.75 + core * 0.65) * shade * capFade * alive * breath;
    alpha *= (1.0 + impactBoost * 0.9) * uIntensity;
    alpha *= lerp(soft, 1.0, 0.35);
    alpha = saturate(alpha) * vertColor.a;

    return float4(col * alpha * vertColor.rgb, alpha); // 预乘输出, Additive 友好
}

technique Technique1
{
    pass PillarDropPass
    {
        PixelShader = compile ps_3_0 PillarDropPS();
    }
}
