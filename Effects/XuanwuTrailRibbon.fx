// XuanwuTrailRibbon.fx — 顶点拖尾ribbon着色器
// 用于冰锥/毒牙弹幕的TriangleStrip拖尾渲染
// 功能: UV流动动画 + 边缘辉光 + 色调偏移 + 长度衰减 + 能量脉冲

sampler uTexture : register(s0); // 拖尾纹理(LightShot/GlaciateWave等)

float uTime;         // 全局时间，用于UV滚动和脉冲
float uGlowWidth;    // 边缘辉光宽度(0~0.5)
float uAlphaFade;    // 尾端衰减强度(0=不衰减，1=完全衰减)
float uScrollSpeed;  // UV横向滚动速度
float uPulseRate;    // 能量脉冲频率
float uPulseStrength;// 能量脉冲强度(叠加亮度)
float4 uGlowColor;  // 边缘辉光颜色(RGBA，A=辉光强度)
float4 uCoreColor;   // 核心色调偏移(与原色混合)

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0; // xy=UV, z=1(unused)
};

float4 PS_TrailRibbon(VSOutput input) : COLOR0 {
    float2 uv = input.texCoord.xy;

    // UV流动: 沿拖尾长度方向滚动
    float2 scrollUV = float2(uv.x + uTime * uScrollSpeed, uv.y);

    // 基础纹理采样
    float4 baseTex = tex2D(uTexture, scrollUV);

    // 顶点色(来自ColoredVertex，包含位置相关的颜色信息)
    float4 vertColor = input.color;

    // 长度衰减: uv.x越大=越远=越旧，逐渐透明
    float lengthFade = saturate(1.0 - uv.x * uAlphaFade);
    // 用平滑曲线代替线性衰减
    lengthFade = lengthFade * lengthFade * (3.0 - 2.0 * lengthFade);

    // 边缘辉光: 基于到中心线的距离(v=0和v=1是两侧边缘)
    float edgeDist = abs(uv.y - 0.5) * 2.0; // 0=中心, 1=边缘
    float edgeFactor = smoothstep(1.0 - uGlowWidth, 1.0, edgeDist);
    float4 glowContrib = uGlowColor * edgeFactor * uGlowColor.a;

    // 核心增亮: 中心线附近更亮
    float coreBright = 1.0 + (1.0 - edgeDist) * 0.4;

    // 能量脉冲: 沿长度方向的行波
    float pulse = 1.0 + sin(uv.x * 12.0 - uTime * uPulseRate) * uPulseStrength * lengthFade;

    // 色调混合: 顶点色 × 纹理 × 核心色调
    float4 color = baseTex * vertColor;
    color.rgb = lerp(color.rgb, uCoreColor.rgb, uCoreColor.a * (1.0 - edgeDist));

    // 合成
    color.rgb *= coreBright * pulse;
    color.rgb += glowContrib.rgb * lengthFade;
    color.a *= lengthFade * vertColor.a;

    // 最终钳制
    color.rgb = saturate(color.rgb);
    return color;
}

technique TrailRibbon {
    pass P0 {
        PixelShader = compile ps_3_0 PS_TrailRibbon();
    }
}
