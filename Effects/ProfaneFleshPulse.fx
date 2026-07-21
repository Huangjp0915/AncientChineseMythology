// ============================================================
// 亵渎系列 · 血肉蠕动膜着色器 (武器线专属)
// 单 quad 绘制: 域扭曲肌纤维 + 蔓状血管 + 苍白膜边 + 心跳收缩
// uMode 0 = 方向波 (uv.x=行进方向, 刀波/冲击带)
// uMode 1 = 径向膜 (爆炸/摘取/烹煮膨胀球)
// 喂共享可平铺噪声 (s0), Additive 混合 (输出预乘 rgb, a 不参与)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (ACMShaders.NoiseTexture)

float  uTime;        // 动画秒
float  uPulse;       // 心跳 0~1 (ProfaneCommon.Heartbeat)
float  uIntensity;   // 整体强度 0~1
float  uMode;        // 0=方向波 1=径向膜
float  uSeed;        // 每实例随机相位
float  uVeinBoost;   // 血管增亮 0~1 (锁定/满蓄时提升)
float4 uColorDark;   // 外层暗血 (92,8,24)
float4 uColorBright; // 内亮血 (248,64,96)
float4 uColorPale;   // 肌腱苍白膜边 (235,190,170)

// 蔓状血管: 两次域扭曲后的 ridged 噪声, 取窄阈值成枝状
// (u 向乘数取整数, 保证径向模式 along 跨度=6 时环向接缝连续)
float VeinMask(float2 p)
{
    float2 warp = tex2D(uImage0, p * 1.0 + float2(uSeed, uTime * 0.05)).rg - 0.5;
    float n = tex2D(uImage0, p * 2.0 + warp * 0.35 + float2(0, uTime * 0.03)).b;
    float ridge = 1.0 - abs(n * 2.0 - 1.0);        // ridged
    return smoothstep(0.80, 0.97, ridge);          // 只留最细的脊 → 血管
}

float4 FleshPulsePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    // 心跳收缩: 搏动瞬间纤维横向绷紧、亮度上冲
    float squeeze = 1.0 + uPulse * 0.18;

    float along;   // 纤维走向坐标 (肌纤维沿此向拉长)
    float across;  // 横截坐标 0(中心)~1(边缘)
    float edgeFade;

    if (uMode < 0.5) {
        // —— 方向波: x=行进, y=横截 ——
        along = coords.x;
        across = abs(coords.y - 0.5) * 2.0 * squeeze;
        // 头部圆润尾部拖散
        edgeFade = smoothstep(1.0, 0.55, coords.x) * smoothstep(0.0, 0.12, coords.x);
    }
    else {
        // —— 径向膜: 极坐标, 纤维沿切向环绕 ——
        float2 p = (coords - 0.5) * 2.0;
        float r = length(p);
        if (r > 1.0)
            return float4(0, 0, 0, 0);
        float ang = atan2(p.y, p.x);
        along = ang * 0.954930;            // /pi*3 → u 跨度=6 整数, 平铺接缝连续
        across = r * squeeze;
        edgeFade = 1.0;
    }

    // 肌纤维: 沿 along 拉长的各向异性噪声, 两层错速叠加成蠕动
    float fiber1 = tex2D(uImage0, float2(along * 1.5 - uTime * 0.22 + uSeed, across * 3.0)).r;
    float fiber2 = tex2D(uImage0, float2(along * 3.0 + uTime * 0.13 + uSeed * 2.3, across * 5.0 + 0.37)).g;
    float fiber = fiber1 * 0.65 + fiber2 * 0.35;
    fiber = 0.45 + 0.9 * fiber;

    // 血管层 (行进空间采样, 随心跳增亮; u 向保持 along 原跨度确保平铺)
    float vein = VeinMask(float2(along + uSeed * 5.0, across * 1.4));
    float veinGlow = vein * (0.55 + 0.75 * uPulse + uVeinBoost);

    // 膜体: 中心亮血→边缘暗血, 心跳时整体上冲
    float body = saturate(1.0 - across);
    float3 col = lerp(uColorDark.rgb, uColorBright.rgb, pow(body, 1.6) * fiber);
    col *= (0.75 + 0.45 * uPulse);
    col += uColorBright.rgb * veinGlow * body;

    // 苍白膜边: across≈1 处一圈肌腱白 (身体恐怖的"膜"轮廓)
    float rim = smoothstep(0.72, 0.95, across) * smoothstep(1.0, 0.97, across);
    col += uColorPale.rgb * rim * (0.5 + 0.4 * uPulse);

    // 整体 alpha: 膜内实、边缘断
    float alpha = saturate(body * 1.3) * edgeFade * uIntensity;
    alpha *= smoothstep(1.0, 0.88, across);

    return float4(col * alpha * sampleColor.a, 0);
}

technique Technique1
{
    pass FleshPulsePass
    {
        PixelShader = compile ps_3_0 FleshPulsePS();
    }
}
