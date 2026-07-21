// ============================================================
// NetherDragonRibbon.fx — 幽冥龙·冥焰披风顶点条带
// 沿全部体节 BuildRibbonStrip 铺设 (uv.x=头→尾 0~1, uv.y=横宽 0~1)
// 鬼绿→幽紫沿身渐变 + 双层反向流动焰舌 + 魂点浮升 + 鞭波亮斑
// uEnrage: 暴怒泛红; uBreak: 死亡逐节熄灭波前(尾→头, 已熄段透明)
// 绘制: Immediate + Additive + GameViewMatrix (同 ACMShaders.DrawBeam 契约)
// ============================================================

sampler uTexture : register(s0); // 载体(绑共享噪声)
sampler uNoise   : register(s1); // 共享三通道 FBM 噪声

float  uTime;         // 秒
float  uIntensity;    // 总强度 0~1
float  uFlowSpeed;    // 焰舌沿身流速
float  uWave;         // 鞭波中心位置 (0~1 沿身; <0 = 无波)
float  uEnrage;       // 暴怒泛红 0~1
float  uBreak;        // 死亡熄灭波前 0~1 (uv.x > 1-uBreak 的尾段已熄)
float4 uColorHead;    // 头端色 (鬼绿亮)
float4 uColorTail;    // 尾端色 (幽紫暗)
float4 uColorWave;    // 鞭波亮斑色

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 PS_NetherRibbon(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;
    float edgeDist = abs(uv.y - 0.5) * 2.0; // 0=脊线 1=边缘

    // 双层反向流动: 焰体沿身滚动
    float flowA = tex2D(uNoise, float2(uv.x * 2.6 - uTime * uFlowSpeed, uv.y * 0.9)).r;
    float flowB = tex2D(uNoise, float2(uv.x * 5.2 + uTime * uFlowSpeed * 0.55, uv.y * 1.6 + 0.43)).g;
    float flow = flowA * 0.62 + flowB * 0.52;

    // 焰舌: 边缘随噪声舔出 (幽火不齐整的撕裂缘)
    float lick = tex2D(uNoise, float2(uv.x * 7.0 - uTime * uFlowSpeed * 1.4, 0.31)).b;
    float edgeCut = smoothstep(0.95 + lick * 0.25 - 0.35, 0.35, edgeDist);

    // 魂点: 高频噪声阈值化成浮升鬼火点
    float souls = tex2D(uNoise, float2(uv.x * 11.0 - uTime * 0.5, uv.y * 2.4 - uTime * 0.35)).b;
    souls = smoothstep(0.82, 0.97, souls);

    // 鞭波亮斑 (冲刺/受击的行进波, CPU 推 uWave)
    float wave = 0.0;
    if (uWave >= 0.0)
    {
        float d = abs(uv.x - uWave);
        wave = saturate(1.0 - d / 0.12);
        wave *= wave;
    }

    float body = saturate(1.0 - edgeDist);
    float core = pow(body, 2.2);

    // 头亮尾暗渐变
    float3 axial = lerp(uColorHead.rgb, uColorTail.rgb, smoothstep(0.05, 0.85, uv.x));
    float3 col = axial * (0.45 + flow * 0.85);
    col += uColorHead.rgb * souls * body * 0.8;
    col = lerp(col, uColorWave.rgb, wave * 0.8);
    col += uColorWave.rgb * wave * core * 0.7;

    // 暴怒泛红: 焰体压向赤红, 流速视觉更急
    if (uEnrage > 0.001)
    {
        float rage = tex2D(uNoise, float2(uv.x * 4.0 - uTime * uFlowSpeed * 2.2, uv.y)).r;
        col = lerp(col, float3(0.95, 0.20, 0.16) * (0.6 + rage * 0.8), uEnrage * 0.6);
    }

    // 死亡熄灭波前: 尾→头逐节吞黑, 波前一线白热
    float ends = smoothstep(0.0, 0.025, uv.x) * (1.0 - smoothstep(0.75, 1.0, uv.x) * 0.8);
    if (uBreak > 0.001)
    {
        float front = 1.0 - uBreak;          // 波前位置 (从尾 1.0 向头 0.0 推进)
        float dead = smoothstep(front + 0.02, front + 0.10, uv.x);
        float frontGlow = exp(-abs(uv.x - front) * 26.0);
        col = lerp(col, float3(0.0, 0.0, 0.0), dead);
        col += float3(1.0, 0.9, 0.75) * frontGlow * 1.4;
        ends *= 1.0 - dead * 0.95;
    }

    float alpha = body * edgeCut * ends * uIntensity;
    alpha *= lerp(uColorTail.a, uColorHead.a, core);
    alpha = saturate(alpha + wave * body * 0.3 * uIntensity);

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass NetherRibbonPass
    {
        PixelShader = compile ps_3_0 PS_NetherRibbon();
    }
}
