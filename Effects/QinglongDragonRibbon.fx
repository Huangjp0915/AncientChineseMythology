// ============================================================
// 青龙蛇身条带着色器 — TriangleStrip 带状 PS
// 流动鳞纹(噪声 UV 滚动) + 背鳍缘光 + 头亮尾暗渐变 + uElectrify 通电脉冲
// uv.x = 沿身进度 0(头)~1(尾), uv.y = 横宽 0~1 (0=背侧边, 1=腹侧边)
// s0 = 共享可平铺噪声 (ACMShaders.NoiseTexture, RGB 三通道独立)
// 以 Additive 混合绘制, 输出 rgb 已按覆盖度加权
// ============================================================

sampler uTexture : register(s0); // 可平铺噪声 (RGB)

float  uTime;        // 动画时间(秒)
float  uElectrify;   // 通电脉冲强度 0~1 (相变劈雷后龙身带电)
float  uLayer;       // 0=外鳞层 1=内亮芯层
float  uFade;        // 全身覆盖度 (死亡「化雨」渐隐)
float4 uColorScale;  // 鳞纹主色 (翠青)
float4 uColorDeep;   // 鳞隙深色 (墨绿)
float4 uColorFin;    // 背鳍缘光色 (雷黄/青白)

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;    // rgb=节点色调(天气染色), a=节点覆盖度
    float3 texCoord : TEXCOORD0; // xy=UV, z=1(未用)
};

float4 DragonRibbonPS(VSOutput input) : COLOR0
{
    float2 uv = input.texCoord.xy;
    float4 vert = input.color;

    // 头亮尾暗渐变
    float headGrad = lerp(1.25, 0.42, smoothstep(0.0, 1.0, uv.x));

    // 到中线的距离 (0=中线 1=边缘)
    float edgeDist = abs(uv.y - 0.5) * 2.0;

    // —— 内亮芯层: 纯柔光核 + 通电白闪 ——
    if (uLayer > 0.5)
    {
        float core = pow(saturate(1.0 - edgeDist), 2.2);
        float pulse = 1.0 + sin(uv.x * 9.0 - uTime * 6.0) * 0.18;
        float3 coreRGB = (uColorScale.rgb * 0.4 + float3(0.75, 1.0, 0.9) * 0.6) * core * pulse * headGrad;
        // 通电: 高频白色行波
        coreRGB += float3(1.0, 1.0, 1.0) * uElectrify * core * (0.55 + 0.45 * sin(uv.x * 34.0 - uTime * 26.0));
        float coreA = core * vert.a * uFade;
        return float4(coreRGB * vert.rgb * coreA, coreA);
    }

    // —— 外鳞层 ——
    // 流动鳞纹: 双层噪声反向滚动相乘, 锐化成鳞点
    float n1 = tex2D(uTexture, float2(uv.x * 3.2 - uTime * 0.35, uv.y * 0.9 + uTime * 0.03)).r;
    float n2 = tex2D(uTexture, float2(uv.x * 6.5 + uTime * 0.22, uv.y * 1.7 - uTime * 0.05)).g;
    float scales = smoothstep(0.30, 0.75, n1 * 0.62 + n2 * 0.48);

    // 沿身体节条带 (鳞列节律感, 相位受噪声扰动避免机械)
    float band = 0.5 + 0.5 * sin(uv.x * 88.0 - uTime * 7.0 + n1 * 2.4);
    scales *= 0.72 + 0.28 * band;

    float3 rgb = lerp(uColorDeep.rgb, uColorScale.rgb, scales) * headGrad;

    // 背鳍缘光 (背侧边亮鳍) + 腹侧微光
    float fin = pow(saturate(1.0 - uv.y * 2.6), 3.0);
    float belly = pow(saturate((uv.y - 0.62) * 2.6), 3.0) * 0.35;
    rgb += uColorFin.rgb * (fin * (0.8 + 0.2 * sin(uv.x * 20.0 - uTime * 9.0)) + belly);

    // 通电脉冲: 白色行波 + 噪声弧光斑
    float arc = tex2D(uTexture, float2(uv.x * 4.0 - uTime * 2.2, uv.y * 0.6 + uTime * 0.4)).b;
    float elec = uElectrify * (0.5 + 0.5 * sin(uv.x * 42.0 - uTime * 30.0));
    rgb += float3(0.9, 0.98, 1.0) * (elec * 0.75 + smoothstep(0.70, 0.95, arc) * uElectrify);

    // 中线增亮 → 边缘衰减 (身体圆柱感)
    float body = saturate(1.05 - edgeDist * edgeDist * 0.9);
    float aOut = body * vert.a * uFade;
    return float4(rgb * vert.rgb * aOut, aOut);
}

technique Technique1
{
    pass DragonRibbonPass
    {
        PixelShader = compile ps_3_0 DragonRibbonPS();
    }
}
