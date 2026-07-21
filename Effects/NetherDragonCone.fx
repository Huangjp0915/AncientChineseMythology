// ============================================================
// NetherDragonCone.fx — 幽冥龙·锥形/扇形危险区预警 屏幕空间贴花
// 吐息锥与魂束扫射扇共用: 两界亮线 + 内域微填充 + 向顶点收拢流纹
// uProgress 推进配色 幽紫(预备)→纯红(致命收口, §6.1 红=致命)
// 载体: 满屏共享噪声(s0), 顶点/方向/张角均世界参数由 CPU 换算
// 建议混合: Additive (预警不遮挡视野)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声

float  uTime;        // 秒
float2 uCenter;      // 锥顶点归一化屏幕坐标 0~1
float  uRadius;      // 锥长 (屏幕高度比例)
float  uIntensity;   // 整体强度 0~1
float  uAspect;      // 宽高比
float  uDir;         // 锥轴朝向 (rad)
float  uSpread;      // 半张角 (rad)
float  uProgress;    // 预警推进 0~1 (配色紫→红 + 边线锐化)
float4 uColorWarm;   // 预备色 (幽蓝紫)
float4 uColorHot;    // 致命色 (纯红)

float4 NetherConePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  distN  = length(diff) / max(uRadius, 0.001);

    if (distN > 1.25 || distN < 0.005)
        return float4(0, 0, 0, 0);

    // 相对锥轴的角偏差
    float ang = atan2(diff.y, diff.x);
    float dAng = ang - uDir;
    dAng = atan2(sin(dAng), cos(dAng)); // wrap 到 [-pi, pi]
    float aN = abs(dAng) / max(uSpread, 0.001); // <1 = 锥内

    if (aN > 1.35)
        return float4(0, 0, 0, 0);

    // 噪声 (极坐标域, 顶点收拢流动: 危险自锥口涌向顶点的反向暗示)
    float2 nUV = float2(dAng * 0.8 + uTime * 0.05, distN * 2.0 - uTime * 0.6);
    float n = tex2D(uImage0, nUV).r;

    // 两界亮线 (锥的边)
    float edgeLine = exp(-abs(aN - 1.0) * (10.0 + uProgress * 14.0));
    // 远端弧线 (锥口)
    float capLine = exp(-abs(distN - 1.0) * (9.0 + uProgress * 12.0)) * step(aN, 1.0);

    // 内域微填充: 靠顶点淡, 靠口浓, 带流纹
    float fill = step(aN, 1.0) * smoothstep(1.15, 0.25, aN)
               * smoothstep(0.02, 0.55, distN) * (0.10 + n * 0.10);
    fill *= 0.5 + uProgress * 0.7;

    // 中轴细线 (瞄准读数)
    float axis = exp(-abs(dAng) * 30.0) * smoothstep(0.02, 0.3, distN) * 0.5;

    // 收口脉冲: 越接近释放闪得越急
    float pulse = 0.75 + 0.25 * sin(uTime * (5.0 + uProgress * 10.0));

    float3 col = lerp(uColorWarm.rgb, uColorHot.rgb, saturate(uProgress * 1.15));
    float shape = saturate(edgeLine + capLine + fill + axis * uProgress);
    float alpha = shape * pulse * uIntensity * (0.45 + uProgress * 0.55);

    return float4(col * alpha, alpha * 0.85);
}

technique Technique1
{
    pass NetherConePass
    {
        PixelShader = compile ps_3_0 NetherConePS();
    }
}
