// ============================================================
// 敖广·龙躯水流 — 顶点 ribbon (TriangleStrip) 像素着色器
// 沿 Boss 历史路径的活水龙躯: 双层反向流动 + 泡沫边缘 + 深浅渐变
// uv.x = 沿长 (0=龙首), uv.y = 横宽 (0/1=两侧边缘)
// 喂可平铺噪声(s0); 建议 Additive 绘制, 顶点色 A 已含长度衰减权重
// ============================================================

sampler uNoise : register(s0); // 可平铺三通道噪声

float  uTime;       // 动画时间 (秒)
float  uGlow;       // 能量档 0~1 (速度门控: 穿刺时拉满)
float  uFlowSpeed;  // 主流速 (建议 0.8~2.0)
float  uFoamWidth;  // 泡沫边缘宽度 (0~0.5)
float4 uDeepColor;  // 深水色 (体色暗部)
float4 uCoreColor;  // 芯部亮色
float4 uFoamColor;  // 泡沫/高光色

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV
};

float4 WaterSerpentPS(VSOutput input) : COLOR0
{
    float2 uv = input.texCoord.xy;
    float4 vert = input.color;

    // —— 双层反向水流: 两套噪声沿长度反向滚动, 相乘出活水丝缕 ——
    float2 flowA = float2(uv.x * 2.6 - uTime * uFlowSpeed, uv.y * 0.8 + uTime * 0.07);
    float2 flowB = float2(uv.x * 4.2 + uTime * uFlowSpeed * 0.6, uv.y * 1.3 - uTime * 0.05);
    float nA = tex2D(uNoise, flowA).r;
    float nB = tex2D(uNoise, flowB).g;
    float streak = saturate(nA * 0.65 + nB * 0.55);

    // —— 截面形状: 中心线亮芯, 向两侧衰减 ——
    float edgeDist = abs(uv.y - 0.5) * 2.0;          // 0=中心 1=边缘
    float body = 1.0 - smoothstep(0.55, 1.0, edgeDist);
    float core = pow(saturate(1.0 - edgeDist), 2.2 + uGlow * 1.5);

    // —— 泡沫边缘: 贴边窄带被噪声打碎成沫 ——
    float foamBand = smoothstep(1.0 - uFoamWidth, 1.0, edgeDist);
    float foam = foamBand * smoothstep(0.45, 0.75, nB);

    // —— 长度衰减: 龙首满亮, 尾梢收细透明 (平滑三次) ——
    float fade = saturate(1.0 - uv.x);
    fade = fade * fade * (3.0 - 2.0 * fade);

    // —— 行波脉冲: 沿龙躯游走的能量波 (速度档越高越急) ——
    float pulse = 1.0 + sin(uv.x * 9.0 - uTime * (3.0 + uGlow * 5.0)) * (0.12 + uGlow * 0.22);

    // —— 合成: 深水体 → 芯部亮色 → 泡沫高光 ——
    float3 col = uDeepColor.rgb * body * (0.55 + streak * 0.7);
    col = lerp(col, uCoreColor.rgb, core * (0.5 + streak * 0.5));
    col += uFoamColor.rgb * foam * (0.6 + uGlow * 0.6);
    col *= pulse * (0.75 + uGlow * 0.7);

    float alpha = body * fade * vert.a;
    col *= vert.rgb;

    // Additive 输出: 颜色即能量, alpha 仅作权重
    return float4(col * alpha, 0.0);
}

technique Technique1
{
    pass WaterSerpentPass
    {
        PixelShader = compile ps_3_0 WaterSerpentPS();
    }
}
