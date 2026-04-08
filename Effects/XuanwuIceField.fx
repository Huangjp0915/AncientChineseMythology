// ============================================================
// 玄武冰域着色器 — 局部区域冰冻效果
// 用于冰锥Mode 2(玄冰锚)的定域冰冻可视化
// Voronoi冰晶网格 + 极坐标裂纹 + 六重对称脉冲
// 完全自包含: 程序化噪声，无需外部噪声贴图
// ============================================================

sampler uTexture : register(s0); // 基础纹理(SoftGlow提供径向渐变)

float uTime;         // 动画时间(秒)
float uProgress;     // 冰域展开进度 0~1
float uIntensity;    // 整体强度 0~1
float uPulse;        // 脉冲相位(外部递增)

static const float3 IceCyan  = float3(0.42, 0.84, 0.98);
static const float3 IceBlue  = float3(0.16, 0.42, 0.90);
static const float3 IceWhite = float3(0.93, 0.97, 1.00);
static const float3 DeepIce  = float3(0.08, 0.18, 0.40);

// ========================================
//  程序化噪声(无需贴图依赖)
// ========================================
float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float2 hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
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

float fbm3(float2 p)
{
    float v = 0.0;
    v += valueNoise(p) * 0.50;
    v += valueNoise(p * 2.07 + 1.73) * 0.30;
    v += valueNoise(p * 4.11 + 3.17) * 0.20;
    return v;
}

// Voronoi距离场: 冰晶网格
float voronoi(float2 p)
{
    float2 n = floor(p);
    float2 f = frac(p);
    float minDist = 1.0;

    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 neighbor = float2((float)x, (float)y);
            float2 cellPt = hash22(n + neighbor);
            //微弱动画: 晶胞缓慢呼吸
            cellPt = 0.5 + 0.4 * sin(uTime * 0.4 + 6.28318 * cellPt);
            float2 diff = neighbor + cellPt - f;
            float d = dot(diff, diff);
            if (d < minDist) minDist = d;
        }
    }
    return sqrt(minDist);
}

float4 IceFieldPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 centered = uv - 0.5;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);

    // 圆形范围裁剪
    float maxR = 0.48 * uProgress;
    float normDist = dist / max(maxR, 0.001);

    if (normDist > 1.15 || uIntensity < 0.001 || uProgress < 0.01)
        return float4(0, 0, 0, 0);

    // SoftGlow基础衰减
    float baseTex = tex2D(uTexture, uv).r;

    // ==========================================
    //  Voronoi冰晶网格
    // ==========================================
    float2 voroUV = centered * 9.0;
    float voro = voronoi(voroUV);
    float cellEdge = smoothstep(0.04, 0.10, voro);
    float cellLine = 1.0 - cellEdge; // 1=在边缘线上

    // 二级Voronoi: 更细密的裂纹
    float voro2 = voronoi(centered * 18.0 + 5.0);
    float finecrack = 1.0 - smoothstep(0.03, 0.08, voro2);
    finecrack *= smoothstep(0.7, 0.3, normDist); // 靠近中心更密

    // ==========================================
    //  极坐标冰裂纹
    // ==========================================
    float angNorm = angle / 6.28318 + 0.5;
    float crackNoise = fbm3(float2(angNorm * 6.0 + uTime * 0.08, normDist * 10.0));
    float radialCrack = smoothstep(0.44, 0.48, crackNoise) * smoothstep(0.56, 0.52, crackNoise);
    radialCrack *= smoothstep(1.1, 0.15, normDist);

    // ==========================================
    //  六重对称脉冲
    // ==========================================
    float sixAngle = angle * 3.0; // 六重对称
    float hexPulse = pow(abs(cos(sixAngle)), 12.0); // 尖锐六棱辉光
    hexPulse *= smoothstep(1.0, 0.3, normDist);
    float hexBreathe = hexPulse * (0.8 + sin(uPulse + normDist * 5.0) * 0.2);

    // ==========================================
    //  径向渐变与扩展动画
    // ==========================================
    float radial = 1.0 - smoothstep(0.0, 1.0, normDist);
    float expandWave = smoothstep(uProgress - 0.15, uProgress, normDist * uProgress)
                     * smoothstep(uProgress + 0.05, uProgress, normDist * uProgress);

    // ==========================================
    //  边缘发光环
    // ==========================================
    float edgeRing = smoothstep(0.90, 0.95, normDist) * smoothstep(1.10, 1.00, normDist);

    // ==========================================
    //  呼吸脉冲
    // ==========================================
    float breath = 1.0 + sin(uPulse) * 0.15;

    // ==========================================
    //  颜色合成
    // ==========================================
    float3 col = lerp(DeepIce, IceBlue, radial * 0.7);
    col = lerp(col, IceCyan, cellLine * 0.8 + finecrack * 0.3);
    col = lerp(col, IceWhite, radialCrack * 0.7);
    col += IceCyan * hexBreathe * 0.35;
    col += IceWhite * edgeRing * 2.0 * uIntensity;
    col += IceCyan * expandWave * 0.4;

    // ==========================================
    //  Alpha合成
    // ==========================================
    float alpha = baseTex;
    alpha *= smoothstep(1.1, 0.80, normDist); // 边缘柔化
    alpha *= uIntensity * breath;
    alpha *= 0.30 + cellLine * 0.25 + radialCrack * 0.2 + radial * 0.25;

    // 冰晶折射色散: 沿径向微弱RGB偏移
    float3 refractCol = col;
    float chromaShift = 0.008 * uIntensity * (1.0 - normDist);
    refractCol.r = lerp(col.r, col.r * 1.15, chromaShift * 10.0);
    refractCol.b = lerp(col.b, col.b * 1.2, chromaShift * 15.0);

    return float4(saturate(refractCol) * saturate(alpha), saturate(alpha));
}

technique IceField
{
    pass P0
    {
        PixelShader = compile ps_3_0 IceFieldPS();
    }
}
