// ============================================================
// 树精「疯长」全屏覆盖着色器 — 屏幕边缘 SDF 卷须生长
// 绿意从四边以指状卷须向屏心蔓延: 深度由 uIntensity 驱动
// 叶脉纹理 + 生长尖端亮缘 + 心跳推进脉冲 + 死亡枯化
// 完全自包含程序化噪声 (同 XuanwuIceField 思路), 不依赖外部贴图
// 经 DrawFullscreenOverlay 绘制 (AlphaBlend 预乘输出), 不读 screenTarget,
// 不占全屏后处理名额; 受 MythologyConfig.FullscreenShadersEnabled 降级
// ============================================================

float uTime;        // 动画时间(秒)
float uIntensity;   // 蔓延深度 0~1 (0=无, 1=卷须探入屏幕 ~30%)
float uPulse;       // 心跳脉冲 0~1 (瞬时推进生长前沿, 外部衰减)
float uWither;      // 枯化度 0~1 (死亡演出: 绿→枯灰褐)
float uAspect;      // 宽高比 width/height

static const float3 LeafDeep  = float3(0.03, 0.13, 0.04); // 深林绿(基底)
static const float3 LeafMid   = float3(0.10, 0.32, 0.08); // 苔绿(体)
static const float3 VeinGlow  = float3(0.36, 0.85, 0.28); // 叶脉亮绿
static const float3 TipGlow   = float3(0.62, 1.00, 0.42); // 生长尖端

// ========================================
//  程序化噪声
// ========================================
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
    v += valueNoise(p * 2.07 + 1.73) * 0.30;
    v += valueNoise(p * 4.11 + 3.17) * 0.20;
    return v;
}

float4 OvergrowthPS(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    // —— 屏幕边缘距离场 (x 按宽高比缩放 → 四边等速) ——
    float ex = min(uv.x, 1.0 - uv.x) * uAspect;
    float ey = min(uv.y, 1.0 - uv.y);
    float e = min(ex, ey);

    // —— 指状卷须: 沿屏心角向的多八度噪声调制探入深度 ——
    float2 centered = float2((uv.x - 0.5) * uAspect, uv.y - 0.5);
    float ang = atan2(centered.y, centered.x + 0.0001);
    float angNorm = ang / 6.28318 + 0.5;
    // 双尺度手指: 大指(慢摆) + 细梢(快颤)
    float fingerBig  = fbm3(float2(angNorm * 9.0,  uTime * 0.05));
    float fingerFine = fbm3(float2(angNorm * 26.0 + 7.3, uTime * 0.11));
    float finger = fingerBig * 0.72 + fingerFine * 0.28;
    finger = pow(saturate(finger), 1.5);

    // 探入深度: 基础缘带 + 手指伸长 + 心跳推进
    float depth = uIntensity * (0.055 + 0.26 * finger) + uPulse * 0.045;

    // 覆盖遮罩与生长前沿
    float mask = smoothstep(depth, depth - 0.07, e);
    if (mask < 0.003)
        return float4(0, 0, 0, 0);
    float tip = smoothstep(depth, depth - 0.018, e) * (1.0 - smoothstep(depth - 0.030, depth - 0.012, e));

    // —— 内部叶脉纹: 折叠噪声细线 + 缓慢生长流动 ——
    float2 leafUV = float2(uv.x * uAspect, uv.y);
    float n = fbm3(leafUV * 7.0 + float2(uTime * 0.015, -uTime * 0.02));
    float vein = 1.0 - smoothstep(0.0, 0.09, abs(n - 0.5));
    float body = fbm3(leafUV * 3.2 - float2(uTime * 0.01, uTime * 0.008));

    // —— 颜色合成 ——
    float3 col = lerp(LeafDeep, LeafMid, body);
    col += VeinGlow * vein * 0.35;
    col += TipGlow * tip * (0.8 + uPulse * 0.8);

    // 心跳时整体轻微增亮
    col *= 1.0 + uPulse * 0.25;

    // —— 枯化: 绿→枯灰褐 (死亡演出退潮用) ——
    float grey = dot(col, float3(0.299, 0.587, 0.114));
    float3 withered = lerp(float3(grey, grey, grey), float3(0.24, 0.18, 0.10), 0.6);
    col = lerp(col, withered, saturate(uWither));

    // —— alpha: 体覆盖 + 叶脉/尖端提亮; 越深(靠边)越实 ——
    float depthIn = saturate((depth - e) / max(depth, 0.001));
    float alpha = mask * (0.42 + depthIn * 0.38) * (0.75 + body * 0.25);
    alpha += tip * 0.30;
    alpha = saturate(alpha) * saturate(uIntensity * 1.4);

    // 预乘 alpha 输出 (AlphaBlend)
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass OvergrowthPass
    {
        PixelShader = compile ps_3_0 OvergrowthPS();
    }
}
