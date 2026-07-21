// ============================================================
// CelestialDragonRibbon.fx — 天御金龙·龙身金辉流光顶点条带
// 沿全部体节 BuildRibbonStrip 铺设 (uv.x=头→尾 0~1, uv.y=横宽 0~1)
// 双层反向流动噪声金辉 + 鳞光点闪 + 充能波行进亮斑 + 死亡白热裂纹
// 绘制: Immediate + Additive + GameViewMatrix (同 ACMShaders.DrawBeam 顶点契约)
// ============================================================

sampler uTexture : register(s0); // 载体纹理(绑共享噪声即可)
sampler uNoise   : register(s1); // 共享三通道 FBM 噪声

float  uTime;         // 秒
float  uIntensity;    // 总强度 0~1
float  uFlowSpeed;    // 沿身流速
float  uChargeWave;   // 充能波中心位置 (0~1 沿身; <0 = 无波)
float  uChargeWidth;  // 充能波半宽 (0~1)
float  uBreak;        // 死亡白热化/金纹裂亮 0~1
float4 uColorCore;    // 脊线金核 (a=脊线不透明度)
float4 uColorEdge;    // 边缘暖橙 (a=边缘不透明度)
float4 uColorCharge;  // 充能波白金

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 PS_DragonRibbon(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;
    float edgeDist = abs(uv.y - 0.5) * 2.0; // 0=脊线 1=边缘

    // 双层反向流动 → 金辉沿身滚动
    float flowA = tex2D(uNoise, float2(uv.x * 3.0 - uTime * uFlowSpeed, uv.y * 0.8)).r;
    float flowB = tex2D(uNoise, float2(uv.x * 6.0 + uTime * uFlowSpeed * 0.6, uv.y * 1.7 + 0.37)).g;
    float flow = flowA * 0.65 + flowB * 0.55;

    // 鳞光: 高频噪声阈值化成点状闪烁
    float sparkle = tex2D(uNoise, float2(uv.x * 14.0 - uTime * 0.9, uv.y * 3.0)).b;
    sparkle = smoothstep(0.80, 0.97, sparkle);

    // 充能波: 波心附近抬亮 (头→尾行进由 CPU 推 uChargeWave)
    float wave = 0.0;
    if (uChargeWave >= 0.0)
    {
        float d = abs(uv.x - uChargeWave);
        wave = saturate(1.0 - d / max(uChargeWidth, 0.001));
        wave *= wave;
    }

    float body = saturate(1.0 - edgeDist);
    float core = pow(body, 2.5);

    float3 col = lerp(uColorEdge.rgb, uColorCore.rgb, core) * (0.55 + flow * 0.75);
    col += uColorCore.rgb * sparkle * body * 0.9;
    col = lerp(col, uColorCharge.rgb, wave * 0.85);
    col += uColorCharge.rgb * wave * core * 0.8;

    // 死亡白热: 整体拉白 + 噪声裂纹迸亮
    if (uBreak > 0.001)
    {
        float crack = tex2D(uNoise, float2(uv.x * 9.0 + uTime * 0.05, uv.y * 2.0)).r;
        crack = smoothstep(0.62 - uBreak * 0.28, 0.78, crack) * uBreak;
        col = lerp(col, float3(1.0, 0.98, 0.9), uBreak * 0.55);
        col += float3(1.0, 0.95, 0.8) * crack * 1.2;
    }

    // 首端小收口, 尾端拖长淡出
    float ends = smoothstep(0.0, 0.03, uv.x) * (1.0 - smoothstep(0.72, 1.0, uv.x) * 0.85);

    float alpha = body * ends * uIntensity;
    alpha *= lerp(uColorEdge.a, uColorCore.a, core);
    alpha = saturate(alpha + wave * body * 0.35 * uIntensity + uBreak * body * ends * 0.3);

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass DragonRibbonPass
    {
        PixelShader = compile ps_3_0 PS_DragonRibbon();
    }
}
