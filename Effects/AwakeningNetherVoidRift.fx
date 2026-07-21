// ============================================================
// 觉醒冥龙·虚空裂隙着色器 — 吸积盘奇点/次元裂隙门
// 对数螺旋吸积臂 + 事件视界暗核 + 视界缘辉
// uProgress 驱动旋开/闭合; uLethal 把吸积辉光推向致命红
// 输出预乘 Alpha (暗核靠 a 压暗场景), 以 AlphaBlend 绘制
// 完全自包含程序化噪声
// ============================================================

sampler uTexture : register(s0); // 占位载体(内容不参与运算)

float  uTime;       // 动画时间(秒)
float  uIntensity;  // 整体强度 0~1
float  uProgress;   // 旋开进度 0(闭合)~1(全开)
float  uSpin;       // 附加旋转相位(实例区分/反旋)
float  uLethal;     // 0=主题紫 1=致命红
float4 uColorGlow;  // 吸积臂辉光色(觉醒紫)
float4 uColorEdge;  // 视界缘色(鬼绿/幽蓝)

// ---------------- 程序化噪声 ----------------
float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm3(float2 p)
{
    float v = 0.0;
    v += valueNoise(p) * 0.50;
    v += valueNoise(p * 2.13 + 1.71) * 0.30;
    v += valueNoise(p * 4.27 + 3.19) * 0.20;
    return v;
}

float4 VoidRiftPS(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float open = saturate(uProgress);
    if (uIntensity < 0.005 || open < 0.01)
        return float4(0, 0, 0, 0);

    float2 c = uv - 0.5;
    float r = length(c) * 2.0;               // 0中心~1四边形边缘
    float discR = r / max(open * 0.92, 0.001);
    if (discR > 1.25)
        return float4(0, 0, 0, 0);

    float ang = atan2(c.y, c.x);

    // —— 对数螺旋吸积臂(3 臂), 向内卷入 ——
    float spiral = sin(ang * 3.0 + log(max(discR, 0.04)) * 5.5 - uTime * 2.3 - uSpin);
    float arms = smoothstep(0.15, 0.92, spiral);
    arms *= smoothstep(1.08, 0.55, discR) * smoothstep(0.10, 0.34, discR);

    // 噪声撕碎吸积臂
    float n = fbm3(float2(ang * 1.15 + uSpin * 0.7, discR * 3.2 - uTime * 0.45));
    arms *= 0.45 + n * 0.85;

    // —— 事件视界: 暗核 + 缘辉环 ——
    float core = smoothstep(0.30, 0.10, discR);
    float rim = smoothstep(0.36, 0.24, discR) * smoothstep(0.09, 0.21, discR);
    rim *= 0.8 + 0.35 * sin(uTime * 3.1 + ang * 2.0 + uSpin);

    // 外缘散逸微光
    float halo = smoothstep(1.22, 0.85, discR) * smoothstep(1.05, 0.75, discR) * 0.35;

    float fade = smoothstep(1.22, 0.80, discR);

    float3 glow = lerp(uColorGlow.rgb, float3(0.98, 0.16, 0.22), saturate(uLethal));
    float3 col = glow * arms * 0.85 * fade
               + uColorEdge.rgb * rim * 1.15
               + glow * rim * 1.35
               + glow * halo;
    col *= 1.0 - core * 0.92;   // 光被视界吞没

    // 预乘 Alpha: 暗核占据(压暗场景), 辉光半透
    float alpha = max(core * 0.94, max(arms * 0.55 * fade, rim * 0.85));
    alpha = saturate(alpha + halo * 0.5) * uIntensity;

    return float4(col * uIntensity, alpha);
}

technique VoidRift
{
    pass P0
    {
        PixelShader = compile ps_3_0 VoidRiftPS();
    }
}
