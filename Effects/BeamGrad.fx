// ============================================================
// 光束梯度/流动着色器 — TriangleStrip 直带 (图元绘制)
// 核心白热 + 边缘渐变 + 沿长度流动 UV + 能量脉冲
// 旱魃 HanbaLaser 抽象的通用原语; 供雷柱/链电/审判射线/金柱等复用
// 顶点由 BuildRibbonStrip 退化为直线带提供 (uv.x=沿长, uv.y=横宽 0~1)
// 仅 PS, 变换走外部矩阵 (同 XuanwuTrailRibbon)
// 可选纹理(s0) + 可平铺噪声(s1, 流动)
// ============================================================

sampler uImage0 : register(s0); // 可选芯纹理(LightShot等); 不依赖也可
sampler uNoise  : register(s1); // 流动噪声

float  uTime;        // 动画时间(秒)
float  uIntensity;   // 整体强度 0~1
float4 uColorCore;   // 核心色 (rgb, a=芯部不透明度权重)
float4 uColorEdge;   // 边缘色 (rgb, a=边缘不透明度权重)
float  uCoreGlow;    // 芯部加法过曝辉度 (专用; 取代旧版借用 uColorCore.a; 未设=0)
float  uFlowSpeed;   // 流动速度
float  uFlowScale;   // 流动纹理尺度
float  uCoreSharp;   // 核心收窄锐度 (建议 1~4)
float  uUseTexture;  // 0=纯程序色 1=乘芯纹理

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 PS_BeamGrad(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;

    // 横向到中心线距离 0=芯 1=边
    float edgeDist = abs(uv.y - 0.5) * 2.0;

    // 芯->边渐变
    float coreProfile = pow(saturate(1.0 - edgeDist), max(uCoreSharp, 0.001));
    float bodyProfile = saturate(1.0 - edgeDist);

    // 沿长度流动调制
    float2 flowUV = float2(uv.x * max(uFlowScale, 0.001) - uTime * uFlowSpeed, uv.y);
    float flow = tex2D(uNoise, flowUV).r;
    flow = 0.7 + 0.6 * flow;

    // 行波脉冲
    float pulse = 1.0 + 0.25 * sin(uv.x * 18.0 - uTime * 8.0);

    float3 col = lerp(uColorEdge.rgb, uColorCore.rgb, coreProfile);
    col *= flow * pulse;
    col += uColorCore.rgb * coreProfile * coreProfile * uCoreGlow; // 芯部加法过曝 (专用 uCoreGlow, 不再借 alpha)

    // 端点收口(首尾淡出)
    float ends = smoothstep(0.0, 0.06, uv.x) * smoothstep(1.0, 0.94, uv.x);

    float alpha = bodyProfile * ends * uIntensity;
    alpha *= lerp(uColorEdge.a, uColorCore.a, coreProfile);

    if (uUseTexture > 0.5)
    {
        float4 tex = tex2D(uImage0, float2(uv.x - uTime * uFlowSpeed, uv.y));
        col *= tex.rgb + 0.2;
        alpha *= tex.a + 0.2;
    }

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass BeamGradPass
    {
        PixelShader = compile ps_3_0 PS_BeamGrad();
    }
}
