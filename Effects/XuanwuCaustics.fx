// ============================================================
// 玄武水面焦散着色器 — 全屏叠加后处理
// 双层焦散纹理交叉采样模拟水下光线散射
// 营造深海/寒潮水下战斗氛围
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标 (原始屏幕画面)
sampler uNoise  : register(s1); // 可平铺噪声纹理 (RGB三通道独立)

float uTime;           // 动画时间 (秒)
float uIntensity;      // 整体强度 (0~1)
float uWaveSpeed;      // 波纹流动速度倍率 (建议0.5~2.0)
float uCausticsScale;  // 焦散纹路尺度 (建议1.5~5.0)
float4 uColorTint;     // 焦散色调 (RGBA, A作为混合权重)

// 颜色常量
static const float3 DeepWater   = float3(0.04, 0.08, 0.18);  // 深海蓝
static const float3 CausticBlue = float3(0.25, 0.55, 0.85);  // 焦散蓝
static const float3 CausticCyan = float3(0.40, 0.80, 0.95);  // 焦散青

// ========================================
//  2x2旋转矩阵辅助
// ========================================
float2 Rotate2D(float2 v, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float4 CausticsPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    // 强度为零时直接返回
    if (uIntensity < 0.001)
        return tex2D(uImage0, coords);

    float speed = uWaveSpeed * uTime;

    // ==========================================
    //  焦散层A — 第一组UV空间
    // ==========================================
    float2 uvA = coords * uCausticsScale;
    uvA = Rotate2D(uvA, 0.4 + speed * 0.08);
    uvA += float2(speed * 0.06, speed * 0.04);

    // 双通道采样，取不同频率
    float cA1 = tex2D(uNoise, uvA).r;
    float cA2 = tex2D(uNoise, uvA * 1.7 + float2(0.3, 0.7)).g;

    // 焦散纹路：两个噪声通道相乘后锐化
    float causticA = cA1 * cA2;
    causticA = smoothstep(0.08, 0.35, causticA); // 锐化为网格光斑

    // ==========================================
    //  焦散层B — 第二组UV空间 (不同旋转角度+速度)
    // ==========================================
    float2 uvB = coords * uCausticsScale * 1.2;
    uvB = Rotate2D(uvB, -0.6 + speed * 0.06);
    uvB += float2(-speed * 0.05, speed * 0.07);

    float cB1 = tex2D(uNoise, uvB).g;
    float cB2 = tex2D(uNoise, uvB * 1.5 + float2(0.6, 0.2)).b;

    float causticB = cB1 * cB2;
    causticB = smoothstep(0.10, 0.38, causticB);

    // ==========================================
    //  合并两层焦散 — 取最大值产生丰富交叉图案
    // ==========================================
    float caustic = max(causticA, causticB);

    // 增加微小的叠加变化
    float causticBlend = causticA * 0.3 + causticB * 0.3 + caustic * 0.4;

    // ==========================================
    //  深度模拟 — 屏幕Y坐标影响焦散密度
    //  越往底部焦散越密 (模拟水越深光越散)
    // ==========================================
    float depthFade = lerp(0.7, 1.0, coords.y);
    causticBlend *= depthFade;

    // ==========================================
    //  呼吸波动 — 整体焦散强度缓慢起伏
    // ==========================================
    float breath = sin(uTime * 0.8) * 0.15 + 0.85;
    causticBlend *= breath;

    // ==========================================
    //  焦散着色
    // ==========================================
    // 根据焦散强度在深蓝→浅青之间插值
    float3 causticColor = lerp(CausticBlue, CausticCyan, causticBlend);

    // 应用用户色调
    causticColor = lerp(causticColor, uColorTint.rgb, uColorTint.a * 0.6);

    // ==========================================
    //  水下色调偏移 — 给整个场景加一层深海色
    // ==========================================
    float4 sceneColor = tex2D(uImage0, coords);

    // 基础水下色调叠加 (颜色偏蓝绿)
    float underwaterBlend = uIntensity * 0.12;
    sceneColor.rgb = lerp(sceneColor.rgb, sceneColor.rgb * float3(0.7, 0.85, 1.0) + DeepWater * 0.3, underwaterBlend);

    // ==========================================
    //  焦散光斑叠加 — Additive混合
    // ==========================================
    float causticStrength = causticBlend * uIntensity * 0.35;
    sceneColor.rgb += causticColor * causticStrength;

    // ==========================================
    //  高亮焦散点 — 最亮处添加白色高光
    // ==========================================
    float highlight = smoothstep(0.6, 0.9, causticBlend);
    sceneColor.rgb += float3(0.9, 0.95, 1.0) * highlight * uIntensity * 0.15;

    // ==========================================
    //  水波纹微扭曲 — 轻微UV偏移模拟折射
    // ==========================================
    float2 waveOffset;
    waveOffset.x = sin(coords.y * 15.0 + uTime * 1.5) * 0.001;
    waveOffset.y = cos(coords.x * 12.0 + uTime * 1.2) * 0.0008;
    waveOffset *= uIntensity;

    float4 waveSample = tex2D(uImage0, clamp(coords + waveOffset, 0.001, 0.999));
    sceneColor.rgb = lerp(sceneColor.rgb, waveSample.rgb, uIntensity * 0.3);

    // ==========================================
    //  暗角 — 水下深度感
    // ==========================================
    float2 vc = coords - 0.5;
    float vignette = 1.0 - dot(vc, vc) * 1.2;
    vignette = saturate(vignette);
    float vignetteBlend = lerp(1.0, vignette, uIntensity * 0.25);
    sceneColor.rgb *= vignetteBlend;

    return sceneColor;
}

technique Technique1
{
    pass CausticsPass
    {
        PixelShader = compile ps_3_0 CausticsPS();
    }
}
