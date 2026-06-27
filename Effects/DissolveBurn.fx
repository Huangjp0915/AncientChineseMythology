// ============================================================
// 通用溶解/灼烧着色器 — Boss 贴图单 pass
// 噪声阈值 clip + 发光灼烧边, 用于召唤显形 / 死亡崩解 / 分身生成消散
// 喂 Boss 贴图(s0) + 可平铺噪声(s1)
// uThreshold 0->1 推进: 0=完整显示, 1=完全消散
// ============================================================

sampler uImage0 : register(s0); // Boss/部件贴图
sampler uNoise  : register(s1); // 可平铺噪声 (RGB三通道独立)

float  uTime;          // 动画时间 (秒)
float  uIntensity;     // 整体可见度 0~1 (master, 0=直接透明)
float  uThreshold;     // 溶解进度 0~1
float  uEdgeWidth;     // 灼烧边宽度 (建议 0.04~0.15)
float  uNoiseScale;    // 噪声密度 (建议 1~4)
float4 uEdgeColor;     // 灼烧边颜色 (rgb=色, a=强度)
float2 uDirection;     // 溶解方向梯度 (0,0=均匀溶解)
float  uSweepStrength; // 方向梯度强度 (0=纯噪声)

float4 DissolveBurnPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float4 baseColor = tex2D(uImage0, coords) * sampleColor;

    // 噪声 + 方向梯度合成溶解场
    float2 nUV = coords * max(uNoiseScale, 0.001) + float2(uTime * 0.02, -uTime * 0.015);
    float n = tex2D(uNoise, nUV).r;

    float sweep = dot(coords - 0.5, uDirection) * uSweepStrength;
    float field = saturate(n + sweep);

    // 有效阈值: 随 threshold 推进吃掉低值像素
    float t = uThreshold;

    // 完全消散区 — 丢弃
    clip(field - t);

    // 灼烧边: 紧贴当前溶解边缘的窄带发光
    float edge = (1.0 - smoothstep(t, t + uEdgeWidth, field));
    edge = saturate(edge) * step(0.0001, t); // threshold=0 时不亮边

    float3 col = baseColor.rgb;
    // 边缘向灼烧色过渡 + 加法发光
    col = lerp(col, uEdgeColor.rgb, edge * uEdgeColor.a);
    col += uEdgeColor.rgb * edge * edge * uEdgeColor.a * 1.5;

    float alpha = baseColor.a * uIntensity;

    return float4(col, alpha);
}

technique Technique1
{
    pass DissolveBurnPass
    {
        PixelShader = compile ps_3_0 DissolveBurnPS();
    }
}
