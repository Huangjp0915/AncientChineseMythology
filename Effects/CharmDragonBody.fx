// ============================================================
// 龙符咒 — 雷龙真身条带着色器 (系列旗舰专属)
// TriangleStrip 条带 (BuildRibbonStrip 顶点契约: uv.x=沿身 0头~1尾, uv.y=横宽 0~1)
// 鳞纹交错弧行 + 脊线亮芯 + 沿身爬行电弧 + 头部辉光 + 尾部收细
// uPulse = 雷龙劫过载 (白热 + 电弧增频)
// s0/s1 = 共享噪声; 仅 PS, 变换走外部矩阵 (BeamGrad 同约定)
// ============================================================

sampler uNoise0 : register(s0);
sampler uNoise1 : register(s1);

float  uTime;       // 动画时间(秒)
float  uIntensity;  // 整体强度 0~1
float4 uColorCore;  // 芯色 (金白, a=芯部权重)
float4 uColorEdge;  // 缘色 (雷紫/靛, a=边缘权重)
float  uEnergy;     // 电弧密度 0~1
float  uPulse;      // 过载 0~1 (雷龙劫)

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0;
};

float4 DragonPS(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;
    float edgeDist = abs(uv.y - 0.5) * 2.0;        // 0=脊线 1=体缘

    // 体廓: 芯亮缘暗
    float body = saturate(1.0 - edgeDist);
    float spine = pow(body, 3.5);

    // 鳞纹: 两相位交错弧行, 沿身向尾滚动
    float rows = sin(uv.y * 9.42);
    float scaleA = sin(uv.x * 42.0 - uTime * 6.0 + rows * 1.8);
    float scaleB = sin(uv.x * 42.0 + 3.14 - uTime * 6.0 - rows * 1.8);
    float scales = pow(saturate(max(scaleA, scaleB)), 2.0) * (0.35 + 0.25 * uEnergy);

    // 爬行电弧: 阈值化流动噪声细丝 + 行波闪烁
    float2 arcUV = float2(uv.x * 3.0 - uTime * (1.8 + uPulse * 2.0), uv.y * 0.8 + uTime * 0.3);
    float an = tex2D(uNoise0, arcUV).r;
    float arc = smoothstep(0.62, 0.72, an) * (1.0 - smoothstep(0.78, 0.90, an));
    arc *= (0.5 + 0.5 * sin(uv.x * 30.0 - uTime * 20.0)) * saturate(uEnergy + uPulse * 1.3);

    // 腹背流纹 (慢速能量流)
    float2 flowUV = float2(uv.x * 2.2 - uTime * 1.1, uv.y);
    float flow = 0.75 + 0.5 * tex2D(uNoise1, flowUV).g;

    // 头部辉光 (uv.x=0 端) 与首尾收口
    float head = pow(saturate(1.0 - uv.x * 2.6), 2.0) * 1.4;
    float ends = smoothstep(0.0, 0.03, uv.x) * smoothstep(1.0, 0.82, uv.x);

    float3 col = lerp(uColorEdge.rgb, uColorCore.rgb, saturate(spine + scales * 0.6));
    col *= flow;
    col += float3(0.85, 0.95, 1.0) * arc * 1.2;    // 电弧青白
    col += uColorCore.rgb * head;
    col = lerp(col, float3(1.0, 0.98, 0.90), saturate(uPulse) * spine * 0.5);

    float alpha = body * ends * uIntensity;
    alpha *= lerp(uColorEdge.a, uColorCore.a, spine);
    alpha = saturate(alpha * (0.75 + scales * 0.35 + arc * 0.6));

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col * alpha), saturate(alpha)); // 预乘, Additive 友好
}

technique Technique1
{
    pass DragonPass
    {
        PixelShader = compile ps_3_0 DragonPS();
    }
}
