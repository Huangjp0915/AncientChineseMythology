// ============================================================
// 怨灵鬼相着色器 SpectreVeil — 本体/残影/分身统一 sprite pass
// 喂 Boss/魂体贴图(s0) + 共享可平铺噪声(s1)
// 五件事一个 pass:
//   1. 魂缕上飘扭曲 — 底部布条随噪声流动 (幽魂无脚, 下摆是烟)
//   2. 虚相 uVeil    — 0=实体 1=鬼相: 灰化泛青 + 降透明 + 轮辉增强
//   3. 聚散 uDissolve — 噪声 clip + 灼边, 顶部最后消散 (聚散成形/死亡崩解)
//   4. 内焰 uFlame   — 噪声舔焰自发光 (蓄力时增强)
//   5. 拖影 uDashBlur — 沿 uDashDir 三次采样方向模糊 (冲刺速度感)
// 输出遵循预乘 Alpha (Terraria 贴图约定), 供 AlphaBlend 批使用
// ============================================================

sampler uImage0 : register(s0); // 本体/魂体贴图
sampler uNoise  : register(s1); // 可平铺 FBM 噪声 (RGB 三通道独立)

float  uTime;       // 动画时间 (秒)
float  uVeil;       // 虚相度 0=实 1=虚
float  uDissolve;   // 聚散进度 0=成形 1=全散
float  uOpacity;    // 主不透明度 (残影逐级递减)
float  uWisp;       // 魂缕扭曲幅度 (建议 0.3~1)
float  uNoiseScale; // 噪声密度 (建议 1.5~3)
float  uFlame;      // 内焰强度 (0~1.5, 蓄力时抬)
float2 uDashDir;    // UV 空间冲刺方向 (拖影朝反向拉)
float  uDashBlur;   // 拖影强度 0~1
float4 uTint;       // 主题染色 (rgb=色, a=染色强度)
float4 uEdgeColor;  // 灼边/轮辉色 (rgb=色, a=强度)

float4 SpectreVeilPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uOpacity < 0.01)
        return float4(0, 0, 0, 0);

    // ---------- 1. 魂缕上飘扭曲 ----------
    // 底部权重: 下摆布条扭得凶, 头部基本稳定 (可读性: 脸是锚点)
    float bottomW = smoothstep(0.25, 1.0, coords.y);
    float2 n1UV = coords * uNoiseScale + float2(uTime * 0.05, -uTime * 0.13);
    float2 n2UV = coords * uNoiseScale * 1.7 + float2(-uTime * 0.04, -uTime * 0.09);
    float n1 = tex2D(uNoise, n1UV).r;
    float n2 = tex2D(uNoise, n2UV).g;

    float amp = uWisp * (0.012 + 0.05 * bottomW) * (1.0 + uVeil * 0.8);
    // 正 y 偏移 = 采样下方 → 可见内容向上流 (魂烟上飘)
    float2 wispUV = coords + float2((n1 - 0.5) * 2.0, (n2 - 0.5) * 2.0 + 0.6 * bottomW) * amp;

    float4 base = tex2D(uImage0, wispUV);

    // ---------- 5. 冲刺拖影 (三次采样, 权重随 uDashBlur) ----------
    float4 b1 = tex2D(uImage0, wispUV - uDashDir * 0.055);
    float4 b2 = tex2D(uImage0, wispUV - uDashDir * 0.11);
    base = base * (1.0 - 0.42 * uDashBlur) + (b1 * 0.30 + b2 * 0.16) * uDashBlur;

    // ---------- 3. 聚散溶解 (顶部存活最久 → 从下摆散成魂缕) ----------
    float2 dUV = coords * (uNoiseScale * 1.4) + float2(uTime * 0.02, -uTime * 0.04);
    float dn = tex2D(uNoise, dUV).b;
    float field = saturate(dn + (0.5 - coords.y) * 0.55 + 0.05);
    clip(field - uDissolve);

    float edge = (1.0 - smoothstep(uDissolve, uDissolve + 0.14, field)) * step(0.001, uDissolve);

    // ---------- 2. 虚相褪色 (预乘域内直接混) ----------
    float grey = dot(base.rgb, float3(0.30, 0.59, 0.11));
    float3 col = lerp(base.rgb, grey * float3(0.55, 0.95, 0.90), uVeil * 0.85);
    float alphaMul = lerp(1.0, 0.38, uVeil);

    // ---------- 主题染色 (怨念青→黄 / 狂怒红, 往染色亮度靠) ----------
    col = lerp(col, grey * uTint.rgb * 1.7, uTint.a);

    // ---------- 4. 内焰舔焰 (sprite 内部自发光, 用 alpha 遮罩) ----------
    float2 fUV = coords * uNoiseScale * 2.3 + float2(uTime * 0.07, -uTime * 0.22);
    float fn = tex2D(uNoise, fUV).r;
    float flame = smoothstep(0.55, 0.95, fn) * base.a;

    // ---------- 轮辉: 半透明羽边发光 (鬼相/灼边时增强) ----------
    float rim = smoothstep(0.02, 0.30, base.a) * (1.0 - smoothstep(0.30, 0.85, base.a));

    float3 emissive = uTint.rgb * flame * uFlame
                    + uEdgeColor.rgb * rim * uEdgeColor.a * (0.45 + uVeil * 0.9)
                    + uEdgeColor.rgb * edge * uEdgeColor.a * 1.6;

    float alpha = base.a * alphaMul * uOpacity * sampleColor.a;
    float3 outCol = (col * alphaMul + emissive) * uOpacity * sampleColor.a;

    return float4(outCol, alpha);
}

technique Technique1
{
    pass SpectreVeilPass
    {
        PixelShader = compile ps_3_0 SpectreVeilPS();
    }
}
