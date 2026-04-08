// ============================================================
// 玄武蛇毒气场着色器 — 有机质感拖尾渲染
// 用于毒牙弹幕的TriangleStrip ribbon拖尾
// 区别于TrailRibbon: 噪声溶解边缘 + 色相漂移 + 液态流动
// 完全自包含: 程序化噪声，无需外部噪声贴图
// ============================================================

sampler uTexture : register(s0); // 拖尾纹理(SoftGlow/LightShot)

float uTime;          // 全局时间(秒)
float uDissolveEdge;  // 溶解边缘过渡宽度(建议0.05~0.2)
float uAlphaFade;     // 尾端长度衰减(0~1)
float uFlowSpeed;     // 沿拖尾方向的UV流动速度
float uHueShift;      // 色相偏移速率
float4 uBaseColor;    // 基础毒色(RGBA, A=整体Alpha权重)
float4 uCoreColor;    // 核心亮色(RGBA, A=核心线混合权重)
float uDripStrength;  // 滴落触须效果强度(0~1)

// ========================================
//  程序化噪声
// ========================================
float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm2(float2 p)
{
    return valueNoise(p) * 0.60 + valueNoise(p * 2.13 + 1.53) * 0.40;
}

// 色相旋转(Rodrigues公式)
float3 hueRotate(float3 col, float shift)
{
    float cosA = cos(shift);
    float sinA = sin(shift);
    float3 k = float3(0.57735, 0.57735, 0.57735);
    return col * cosA + cross(k, col) * sinA + k * dot(k, col) * (1.0 - cosA);
}

struct VSOutput
{
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0;
};

float4 PS_VenomAura(VSOutput input) : COLOR0
{
    float2 uv = input.texCoord.xy;
    float4 vertColor = input.color;

    // ==========================================
    //  流动UV采样
    // ==========================================
    float2 flowUV = float2(uv.x + uTime * uFlowSpeed, uv.y);
    float4 baseTex = tex2D(uTexture, flowUV);

    // 边缘距离: 0=中心线, 1=两侧边缘
    float edgeDist = abs(uv.y - 0.5) * 2.0;

    // ==========================================
    //  噪声溶解 — 有机侵蚀边缘
    // ==========================================
    // 双频噪声叠加创造复杂溶解纹路
    float n1 = fbm2(float2(uv.x * 4.0 + uTime * 0.35, uv.y * 3.0 + uTime * 0.22));
    float n2 = valueNoise(float2(uv.x * 8.0 - uTime * 0.5, uv.y * 6.0 - uTime * 0.18));
    float noiseMask = n1 * 0.6 + n2 * 0.4;

    // 溶解阈值: 越靠近边缘越容易被侵蚀
    float dissolveThreshold = lerp(0.22, 0.88, edgeDist);
    float dissolved = smoothstep(dissolveThreshold - uDissolveEdge,
                                 dissolveThreshold + uDissolveEdge, noiseMask);

    // 溶解边缘发光: 在溶解过渡带上产生亮线
    float dissolveGlow = smoothstep(dissolveThreshold - uDissolveEdge * 2.5,
                                    dissolveThreshold - uDissolveEdge * 0.5, noiseMask)
                       * (1.0 - dissolved);

    // ==========================================
    //  长度衰减(Hermite平滑)
    // ==========================================
    float lengthFade = saturate(1.0 - uv.x * uAlphaFade);
    lengthFade = lengthFade * lengthFade * (3.0 - 2.0 * lengthFade);

    // ==========================================
    //  滴落触须 — 底部边缘的垂直噪声
    // ==========================================
    float dripNoise = valueNoise(float2(uv.x * 5.0 + 0.5, uTime * 0.28));
    float drip = smoothstep(0.35, 0.72, dripNoise);
    drip *= smoothstep(0.35, 0.85, edgeDist); // 仅边缘处
    drip *= (uv.y > 0.5 ? 1.0 : 0.25); // 主要从下方滴落
    drip *= uDripStrength;

    // ==========================================
    //  色相漂移 — 绿↔紫渐变
    // ==========================================
    float hueOffset = sin(uv.x * 5.0 + uTime * uHueShift) * 0.45
                    + cos(uv.y * 3.0 + uTime * uHueShift * 0.7) * 0.15;
    float3 venomCol = hueRotate(uBaseColor.rgb, hueOffset);

    // ==========================================
    //  核心线增亮
    // ==========================================
    float coreIntensity = 1.0 - edgeDist;
    coreIntensity = coreIntensity * coreIntensity; // 二次衰减

    // ==========================================
    //  内部翻涌动画
    // ==========================================
    float churn = valueNoise(float2(uv.x * 3.5 + uTime * 0.55, uv.y * 2.5 + uTime * 0.3));
    float churnBright = churn * 0.25 * coreIntensity;

    // 气泡纹: 小尺度点状噪声锐化
    float bubble = valueNoise(float2(uv.x * 12.0 + uTime * 0.8, uv.y * 10.0 + uTime * 0.4));
    bubble = smoothstep(0.55, 0.65, bubble) * coreIntensity * 0.3;

    // ==========================================
    //  脉动波
    // ==========================================
    float wave = sin(uv.x * 8.0 - uTime * 5.5) * 0.10 * lengthFade;

    // ==========================================
    //  合成
    // ==========================================
    float3 finalCol = venomCol * vertColor.rgb * baseTex.rgb;
    finalCol = lerp(finalCol, uCoreColor.rgb, coreIntensity * uCoreColor.a);
    finalCol += dissolveGlow * float3(0.85, 1.0, 0.25) * 0.55; // 溶解亮边
    finalCol += churnBright;
    finalCol += bubble;
    finalCol *= (1.0 + wave);

    float alpha = lengthFade * vertColor.a * baseTex.a;
    alpha *= (1.0 - dissolved * 0.75); // 不完全透明
    alpha += drip * 0.22 * lengthFade;
    alpha *= uBaseColor.a;

    return float4(saturate(finalCol), saturate(alpha));
}

technique VenomAura
{
    pass P0
    {
        PixelShader = compile ps_3_0 PS_VenomAura();
    }
}
