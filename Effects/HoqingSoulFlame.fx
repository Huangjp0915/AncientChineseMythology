// ============================================================
// 后卿·魂焰 — 程序化尸火 (柱体 / 火苗两用)
// 双层差频向上流动噪声 + 腐橙芯→鬼绿缘 + fbm 撕裂边
// 载体: 任意白色贴图 (MagicPixel) 拉伸绘制, 噪声喂 s1
//   uPillar: 1 = 柱模式(横向对称衰减, 两端收口)  0 = 火苗模式(向上收窄)
//   uWarn:   1 = 预警半透鬼影模式 (无实体伤害期的柱预告)
// ============================================================

sampler uImage0 : register(s0); // 载体贴图 (白像素, 只取 alpha 占位)
sampler uNoise  : register(s1); // 可平铺噪声 (RGB 三通道独立)

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 整体强度 0~1
float4 uColorCore;   // 焰芯色 (腐橙白)
float4 uColorOuter;  // 焰缘色 (鬼绿)
float  uFlow;        // 流速 (建议 0.6~1.6)
float  uNoiseScale;  // 噪声尺度 (建议 1.5~4)
float  uPillar;      // 1=柱模式 0=火苗
float  uWarn;        // 1=预警半透模式
float  uSeed;        // 实例随机种子 (uv 偏移)

float4 SoulFlamePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = coords;

    // —— 双层差频火焰噪声: 向上流动 (uv.y 减 → 视觉上升) ——
    float2 fUV = float2(uv.x * uNoiseScale * 0.6 + uSeed, uv.y * uNoiseScale - uTime * uFlow);
    float n1 = tex2D(uNoise, fUV).r;
    float n2 = tex2D(uNoise, fUV * 1.9 + float2(0.31, -uTime * uFlow * 0.35)).g;
    float flame = n1 * 0.6 + n2 * 0.4;

    float profile;
    if (uPillar > 0.5)
    {
        // 柱模式: 横向对称衰减 + 噪声撕裂边, 上下端收口
        float px = abs(uv.x - 0.5) * 2.0;
        px += (flame - 0.5) * 0.38;
        profile = 1.0 - smoothstep(0.42, 1.0, px);
        profile *= smoothstep(0.0, 0.06, uv.y) * smoothstep(1.0, 0.94, uv.y);
    }
    else
    {
        // 火苗模式: 底宽顶尖, 尖部随流动摆动
        float width = lerp(0.16, 0.52, uv.y);
        float px = abs(uv.x - 0.5 + (flame - 0.5) * 0.20 * (1.0 - uv.y));
        profile = 1.0 - smoothstep(width * 0.35, width, px);
        profile *= smoothstep(0.0, 0.14, uv.y) * smoothstep(1.0, 0.90, uv.y);
    }

    // 焰体: 轮廓 × 噪声阈值 (撕裂的火舌)
    float body = profile * smoothstep(0.16, 0.72, flame + profile * 0.32);

    // 焰芯: 亮度平方提取
    float core = pow(saturate(body * 1.4), 2.6);

    float3 col = lerp(uColorOuter.rgb, uColorCore.rgb, core);
    float alpha = saturate(body * (0.72 + 0.28 * core));

    // 预警半透鬼影: 低透明呼吸 + 偏冷色
    if (uWarn > 0.5)
    {
        alpha *= 0.26 + 0.10 * sin(uTime * 6.0 + uSeed * 12.0);
        col = lerp(col, uColorOuter.rgb, 0.55);
    }

    alpha *= uIntensity;
    // 预乘输出 + 顶点色调制
    return float4(col * alpha, alpha) * sampleColor.a;
}

technique Technique1
{
    pass SoulFlamePass
    {
        PixelShader = compile ps_3_0 SoulFlamePS();
    }
}
