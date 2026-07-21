// ============================================================
// 幽冥妖狐 · 魂焰 (NetherKitsuneSoulflame) — 程序化撕裂鬼火 sprite
// 噪声上卷 + 撕裂边缘的狐火: 白青芯 + 冥蓝缘, uGhost 切鬼绿 (P3/怨火),
// 死亡演出经 uCoreColor/uEdgeColor 传暖金做"回光返照"。
// 供尾尖火 / 怨火地灾 / 入场九火 / 死亡九火通用。
// s0 = 共享噪声 (载体即噪声, quad uv 0~1, 焰尖朝 quad 顶部)
// ============================================================

sampler uNoiseTex : register(s0);

float uTime;
float uSeed;      // 每朵焰相位
float uIntensity; // 0~1
float uGhost;     // 0=冥蓝 1=鬼绿
float uStretch;   // 纵向拉伸 (1=标准)
float4 uCoreColor;
float4 uEdgeColor;

float4 SoulflamePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    // 局部坐标: x 居中, y: 0=焰尖(上) 1=焰根(下)
    float2 p = float2(coords.x * 2.0 - 1.0, coords.y);

    // 上卷噪声 (向上滚动) + 次级扰动
    float2 nuv = float2(coords.x * 1.6 + uSeed, coords.y * 1.1 * uStretch - uTime * 1.35 + uSeed * 7.0);
    float n = tex2D(uNoiseTex, nuv * 0.5).r;
    float n2 = tex2D(uNoiseTex, nuv * 1.1 + 0.31).g;

    // 顶部扰动大 (焰舌撕开), 根部稳定
    p.x += (n - 0.5) * 0.9 * (1.0 - coords.y);

    // 焰体: 根部宽圆, 向上收细
    float width = lerp(0.18, 0.62, smoothstep(0.05, 0.75, coords.y));
    float body = length(float2(p.x / width, (coords.y - 0.72) * 1.5));
    float flame = 1.0 - smoothstep(0.35, 1.0, body + (n2 - 0.5) * 0.55);
    flame = saturate(flame) * saturate(uIntensity);

    // 撕裂边: 噪声阈值撕开焰缘
    flame *= smoothstep(0.12, 0.40, n + flame * 0.5);

    float core = smoothstep(0.45, 0.95, flame);
    float3 edgeC = lerp(uEdgeColor.rgb, float3(0.28, 0.85, 0.50), uGhost);
    float3 coreC = lerp(uCoreColor.rgb, float3(0.75, 1.00, 0.85), uGhost * 0.7);
    float3 col = lerp(edgeC, coreC, core) * flame;

    return float4(col * 1.6, 0.0) * sampleColor.a; // 加性
}

technique Technique1
{
    pass SoulflamePass
    {
        PixelShader = compile ps_3_0 SoulflamePS();
    }
}
