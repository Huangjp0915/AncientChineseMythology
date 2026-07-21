// ============================================================
// 白虎·爪裂撕痕着色器 — quad/带状 TriangleStrip PS
// 三道平行耙痕：锯齿噪声缘 + 银白核 + uMode 0=红色预告 / 1=银白释放
// uProgress 控制撕裂揭示进度(痕从头向尾撕开, 前沿亮)
// 顶点由 C# BuildRibbonStrip 提供 (uv.x=沿长 0~1, uv.y=横宽 0~1)
// 喂可平铺噪声(s0)
// ============================================================

sampler uNoise : register(s0); // 可平铺噪声 (RGB三通道独立)

float uTime;      // 动画时间(秒)
float uIntensity; // 整体强度 0~1
float uProgress;  // 撕裂揭示进度 0~1 (1=全长可见)
float uMode;      // 0=红色预告(非致命亮度) 1=银白释放
float uLenScale;  // 长度纹理密度补偿 (≈世界长度/300, 保持锯齿密度一致)

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 ClawRendPS(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;
    float lenScale = max(uLenScale, 0.25);

    // 三道平行耙痕: 横向分 3 带, 每带独立扰动
    float laneId = min(floor(uv.y * 3.0), 2.0); // 0/1/2 (uv.y=1 边缘钳到第 2 带)
    float lane = frac(uv.y * 3.0);              // 带内坐标 0~1

    // 锯齿噪声缘: 沿长度扰动痕心位置与宽度 (每道痕相位不同)
    float n1 = tex2D(uNoise, float2(uv.x * 2.1 * lenScale + laneId * 0.37, laneId * 0.29 + 0.13)).r;
    float n2 = tex2D(uNoise, float2(uv.x * 4.6 * lenScale - laneId * 0.53, 0.71 - laneId * 0.17)).g;
    float n3 = tex2D(uNoise, float2(uv.x * 9.0 * lenScale + laneId * 1.31, lane * 1.7 + 0.41)).b;

    float center = 0.5 + (n1 - 0.5) * 0.36;
    float d = abs(lane - center) * 2.0;     // 0=痕心 1=带缘

    // 端部收尖 + 宽度沿长度粗细起伏(耙痕的撕扯感)
    float taper = smoothstep(0.0, 0.10, uv.x) * smoothstep(1.0, 0.84, uv.x);
    float widthMod = taper * (0.50 + 0.50 * n2);
    float stroke = saturate(1.0 - d / max(widthMod, 0.02));

    // 揭示进度: 痕从头(uv.x=0)向尾撕开, 揭示前沿有亮点
    float reveal = 1.0 - smoothstep(uProgress - 0.04, uProgress + 0.02, uv.x);
    float front = smoothstep(uProgress - 0.12, uProgress - 0.02, uv.x) * reveal;

    // 高频锯齿粗糙度
    float rough = 0.72 + 0.28 * n3;

    float core = pow(stroke, 3.0) * rough;  // 银白核(窄)
    float body = pow(stroke, 1.4) * rough;  // 痕身(宽)

    float3 col;
    float alpha;
    if (uMode < 0.5) {
        // —— 红色预告: 半透红痕 + 呼吸脉冲, 亮度克制 ——
        float pulse = 0.72 + 0.28 * sin(uTime * 13.0 + uv.x * 7.0 + laneId * 2.1);
        float3 lethal = float3(0.98, 0.16, 0.22);
        col = lethal * (body * 0.9 + core * 0.7) * pulse;
        alpha = body * 0.62 * reveal;
    }
    else {
        // —— 银白释放: 亮银缘 + 白热核 + 撕裂前沿爆亮 ——
        float3 edgeSilver = float3(0.70, 0.77, 0.90);
        float3 coreWhite = float3(1.0, 1.0, 1.0);
        col = edgeSilver * body + coreWhite * core * 1.7;
        col += coreWhite * front * 2.4;
        alpha = saturate(body * 0.85 + core) * reveal;
    }

    col *= input.color.rgb;
    alpha *= input.color.a * uIntensity;
    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass ClawRendPass
    {
        PixelShader = compile ps_3_0 ClawRendPS();
    }
}
