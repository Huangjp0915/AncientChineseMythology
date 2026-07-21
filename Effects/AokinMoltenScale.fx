// ============================================================
// 敖钦熔鳞 — 龙身贴图单 pass (s0=身体贴图, s1=共享噪声)
// 沿噪声的熔岩裂纹自发光: uHeat(余烬温度)控制亮度与脉动频率,
// uRage(逆鳞狂暴)熔纹泛白, uDeath(死亡演出)逐段焦黑熄灭 + 溶解余烬边
// SpriteBatch Immediate 模式下逐段设置 uSegPhase/uDeath 后绘制
// ============================================================

sampler uImage0 : register(s0); // 身体贴图
sampler uNoise  : register(s1); // 共享可平铺噪声

float uTime;        // 动画时间 (秒)
float uHeat;        // 0~1 余烬温度 → 熔纹亮度/脉动
float uRage;        // 0~1 狂暴泛白
float uDeath;       // 0~1 该段死亡熄灭/溶解进度
float uSegPhase;    // 每段噪声相位偏移 (段序 * 常数)
float4 uGlowColor;  // 熔纹主色 (熔橙)
float4 uGlowColor2; // 熔纹深色 (龙焰红)

float4 MoltenScalePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 tex = tex2D(uImage0, coords);
    if (tex.a < 0.01)
        return tex * sampleColor;

    // 熔岩裂纹: 慢漂噪声窄带 (双 smoothstep 锐化成线)
    float n = tex2D(uNoise, coords * 1.6 + float2(uSegPhase, uSegPhase * 0.37)
                            + float2(uTime * 0.03, -uTime * 0.02)).r;
    float crack = smoothstep(0.44, 0.50, n) * smoothstep(0.62, 0.54, n);

    // 温度越高, 裂纹呼吸越快越亮
    float pulse = 0.75 + 0.25 * sin(uTime * (2.0 + uHeat * 4.0) + uSegPhase * 7.0);
    float emissive = crack * (0.25 + uHeat * 1.05) * pulse;

    float3 glow = lerp(uGlowColor2.rgb, uGlowColor.rgb, saturate(uHeat + crack * 0.4));
    // 狂暴: 熔纹泛白 + 全身微透白光
    glow = lerp(glow, float3(1.0, 0.96, 0.85), uRage * 0.75);
    emissive += uRage * 0.18;

    // 贴图受光照(sampleColor), 熔纹自发光不受光照压暗
    float3 col = tex.rgb * sampleColor.rgb + glow * emissive * tex.a;
    float alpha = tex.a;

    // 死亡熄灭: 向焦黑压暗 + 噪声溶解(边缘余烬亮线)
    if (uDeath > 0.001)
    {
        float dn = tex2D(uNoise, coords * 2.3 + uSegPhase).g;
        float dissolve = saturate(uDeath * 1.15);
        float edge = smoothstep(dissolve - 0.12, dissolve, dn)
                   * (1.0 - smoothstep(dissolve, dissolve + 0.03, dn));
        col = lerp(col, float3(0.06, 0.03, 0.02), saturate(uDeath * 1.2));
        col += uGlowColor.rgb * edge * 2.2 * (1.0 - uDeath * 0.6);
        // 溶解镂空 (uDeath 前段仅压暗不镂空, 后段烧穿)
        float cut = 1.0 - smoothstep(dissolve, dissolve + 0.02, dn) * step(0.35, uDeath);
        alpha *= cut;
        col *= cut;
    }

    return float4(col, alpha) * float4(1, 1, 1, sampleColor.a);
}

technique Technique1
{
    pass MoltenScalePass
    {
        PixelShader = compile ps_3_0 MoltenScalePS();
    }
}
