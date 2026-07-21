// ============================================================
// 敖顺·风刃着色器 — 弹幕专属（quad UV 空间程序化新月刃）
// 新月 SDF + 沿弧流动噪声 + 边缘亮芯 + 尾侧风痕
// 弹体朝向 +X（由 SpriteBatch 旋转对齐飞行方向）
// ============================================================

sampler uImage0 : register(s0); // 任意载体纹理（仅取 TEXCOORD）
sampler uNoise  : register(s1); // 共享可平铺噪声

float uTime;      // 动画时间 (秒)
float uIntensity; // 整体强度 0~1（生长/淡出复用）
float uSeed;      // 每片风刃相位差
float4 uColorCore; // 亮芯色
float4 uColorEdge; // 边缘/风尾色

float4 WindBladePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.001)
        return float4(0, 0, 0, 0);

    // 局部空间: 中心为原点, 范围 -1~1, +X = 飞行方向
    float2 p = (coords - 0.5) * 2.0;

    // ---------- 新月 SDF: 大圆减去沿 -X 偏移的小圆 ----------
    float dOuter = length(p) - 0.82;                       // 主圆
    float dInner = length(p - float2(-0.34, 0.0)) - 0.78;  // 挖除圆(偏后方) → 开口朝后的月牙
    float crescent = max(dOuter, -dInner);                 // 月牙内部 < 0

    // ---------- 沿弧的流动扰动（让刃锋带风的呼吸） ----------
    float ang = atan2(p.y, p.x); // -pi~pi, 0=+X 刃锋前缘
    float flow = tex2D(uNoise, float2(ang * 0.6 - uTime * 1.7 + uSeed, uSeed * 3.1)).r;
    crescent += (flow - 0.5) * 0.10;

    // ---------- 刃体与亮芯 ----------
    // 刃体: SDF 内部软填充
    float body = smoothstep(0.05, -0.16, crescent);
    // 亮芯: 贴着 SDF 零面的窄带
    float core = smoothstep(0.055, 0.0, abs(crescent));
    core = pow(core, 2.2);

    // 前缘更亮(风刃的锋)：越靠 +X 权重越高
    float front = saturate(p.x * 0.8 + 0.55);
    core *= 0.45 + 0.55 * front;
    body *= 0.35 + 0.65 * front;

    // ---------- 尾侧风痕: 从月牙开口向 -X 拖出的细风线 ----------
    float tailZone = saturate(-p.x - 0.05) * smoothstep(0.75, 0.15, abs(p.y));
    float tailNoise = tex2D(uNoise, float2(p.x * 0.7 - uTime * 2.2 + uSeed, p.y * 4.0 + uSeed)).g;
    float tail = tailZone * smoothstep(0.55, 0.85, tailNoise) * 0.8;

    // ---------- 合成（加性混合输出，A 通道置 0 交由 Additive 处理） ----------
    float3 col = uColorEdge.rgb * (body * 0.55 + tail)
               + uColorCore.rgb * core * 1.25;
    float alpha = saturate(body * 0.7 + core + tail * 0.6) * uIntensity;

    return float4(col * alpha, 0.0);
}

technique Technique1
{
    pass WindBladePass
    {
        PixelShader = compile ps_3_0 WindBladePS();
    }
}
