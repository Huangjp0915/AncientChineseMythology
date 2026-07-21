// ============================================================
// 毗沙门天 · 镇压天光柱 — 世界矩形 quad (s0=共享噪声)
// uv.x=横向 0~1, uv.y=纵向 0=顶(天/塔) 1=底(地面)
// uTelegraph: 1=细线预告态(高频脉冲) 0=全宽爆发; 中间值平滑过渡
// 供 天光垂落 / 塔光柱镇压 复用。Additive 绘制。
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声

float  uTime;          // 动画时间(秒)
float  uIntensity;     // 总亮度 0~1
float4 uColorCore;     // 核心色(暖白金)
float4 uColorEdge;     // 边缘色(琉璃金)
float  uTelegraph;     // 1=预告细线 0=全宽爆发
float  uFlowSpeed;     // 纵向金纹流速
float  uSeed;          // 每根柱相位差

float4 VaisravanaPillarPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float x  = coords.x * 2.0 - 1.0; // -1~1
    float ax = abs(x);

    // —— 宽度轮廓: 预告=细线脉冲, 爆发=过曝核+宽边 ——
    float telegraphLine = pow(saturate(1.0 - ax), 14.0)
                        * (0.55 + 0.45 * sin(uTime * 9.0 + uSeed * 17.0));
    float coreW = pow(saturate(1.0 - ax), 2.4);
    float edgeW = pow(saturate(1.0 - ax), 1.1);

    // —— 纵向金纹上升流动(两层错频) ——
    float flow  = tex2D(uImage0, float2(coords.x * 0.8 + uSeed, coords.y * 2.4 - uTime * uFlowSpeed)).r;
    float flow2 = tex2D(uImage0, float2(coords.x * 1.7 - uSeed, coords.y * 4.8 - uTime * uFlowSpeed * 1.6)).g;
    float streaks = saturate(flow * 0.65 + flow2 * 0.55);

    // —— 端部收头: 顶部渐入, 底部地座过曝 ——
    float capTop   = smoothstep(0.0, 0.10, coords.y);
    float baseGlow = pow(saturate((coords.y - 0.86) / 0.14), 2.0) * 1.4;
    float capBot   = 1.0 + baseGlow;

    float beam  = coreW * (0.85 + streaks * 0.6) + edgeW * streaks * 0.45;
    float shape = lerp(beam, telegraphLine * (0.8 + streaks * 0.3), saturate(uTelegraph));
    shape *= capTop * capBot;

    float3 col = uColorCore.rgb * shape
               + uColorEdge.rgb * edgeW * 0.35 * (1.0 - saturate(uTelegraph));

    float alpha = saturate(shape * uIntensity);
    return float4(col * uIntensity, alpha);
}

technique Technique1
{
    pass VaisravanaPillarPass
    {
        PixelShader = compile ps_3_0 VaisravanaPillarPS();
    }
}
