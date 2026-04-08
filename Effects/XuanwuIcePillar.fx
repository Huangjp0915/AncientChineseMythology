// ============================================================
// 玄武冰柱着色器 — 从地面刺出的巨大冰晶柱
// 程序化生成冰晶棱柱外形 + 内部折射 + 生长/碎裂动画
// 完全自包含: 无需外部噪声贴图
// ============================================================

sampler uTexture : register(s0); //基础纹理(提供渐变基底)

float uTime;       //动画时间
float uGrowth;     //生长进度 0~1 (0=地下, 1=完全刺出)
float uShatter;    //碎裂进度 0~1 (0=完好, 1=完全碎裂)
float uIntensity;  //整体亮度
float uWidth;      //柱体宽度系数(默认1.0)

static const float3 IceCoreWhite = float3(0.92, 0.97, 1.00);
static const float3 IceCyan      = float3(0.38, 0.78, 0.96);
static const float3 IceDeep      = float3(0.10, 0.24, 0.52);
static const float3 FrostEdge    = float3(0.68, 0.90, 1.00);

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
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
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
    float v = 0.0;
    v += valueNoise(p) * 0.55;
    v += valueNoise(p * 2.13 + 1.7) * 0.30;
    v += valueNoise(p * 4.27 + 3.5) * 0.15;
    return v;
}

// ========================================
//  冰柱外形遮罩
// ========================================
//  UV空间: x=0左 x=1右, y=0顶(尖) y=1底(宽)
//  柱形: 底部宽→顶部尖的棱锥形
float pillarShape(float2 uv)
{
    float cx = uv.x - 0.5; //中心偏移 -0.5~0.5

    //底部宽，顶部尖: 随y减小(向上)收窄
    //y=1(底部)时半宽 = 0.38 * uWidth
    //y=0(顶部)时半宽 = 0.03
    float taper = lerp(0.03, 0.38 * uWidth, uv.y);

    //棱柱感: 用三角函数在侧面制造多个棱面
    float facets = 1.0 + abs(sin(cx * 3.14159 / taper * 2.5)) * 0.08;
    taper *= facets;

    //边缘柔化用smoothstep
    float shapeMask = smoothstep(taper, taper - 0.02, abs(cx));

    return shapeMask;
}

// ========================================
//  像素着色器
// ========================================
float4 IcePillarPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    //生长遮罩: uGrowth控制从底部(y=1)向顶部(y=0)显露
    float growthCut = 1.0 - uGrowth; //growthCut=0时全部可见
    float growthMask = smoothstep(growthCut, growthCut + 0.08, uv.y);

    //碎裂遮罩: 噪声驱动的溶解效果
    float shatterNoise = fbm2(uv * float2(5.0, 12.0) + float2(uTime * 0.5, 0.0));
    float shatterMask = smoothstep(uShatter - 0.05, uShatter + 0.1, shatterNoise);
    //碎裂时从边缘开始
    float edgeDist = 1.0 - abs(uv.x - 0.5) * 2.0; //0=边缘, 1=中心
    shatterMask = lerp(shatterMask, 1.0, saturate((1.0 - edgeDist) * uShatter * 2.0));

    //柱体外形
    float shape = pillarShape(uv);
    float finalMask = shape * growthMask * shatterMask;

    if (finalMask < 0.01) return float4(0, 0, 0, 0);

    //中心距
    float cx = abs(uv.x - 0.5) * 2.0; //0=中心, 1=边缘

    // ========================================
    //  晶体内部结构
    // ========================================
    //垂直条纹结构: 模拟冰柱内部的结晶纹路
    float verticalVeins = abs(sin(uv.x * 18.0 + fbm2(uv * float2(3.0, 8.0)) * 2.0));
    verticalVeins = pow(verticalVeins, 3.0); //锐化

    //水平分层: 冰的沉积层
    float horizontalBands = abs(sin(uv.y * 25.0 + valueNoise(uv * float2(2.0, 6.0)) * 1.5));
    horizontalBands = smoothstep(0.85, 1.0, horizontalBands);

    //内部折射光斑: 随时间缓慢移动的高光
    float refraction = fbm2(uv * float2(6.0, 15.0) + float2(uTime * 0.15, -uTime * 0.08));
    float refractionHighlight = smoothstep(0.62, 0.72, refraction);

    // ========================================
    //  径向渐变(中心亮，边缘暗)
    // ========================================
    float coreGlow = 1.0 - cx * cx; //二次衰减
    coreGlow = pow(abs(coreGlow), 0.6);

    //顶端尖部辉光
    float tipGlow = smoothstep(0.25, 0.0, uv.y) * (1.0 - cx);

    // ========================================
    //  边缘霜冻轮廓光
    // ========================================
    //边缘检测: shape接近阈值时发光
    float taper = lerp(0.03, 0.38 * uWidth, uv.y);
    float edgeGlow = smoothstep(taper - 0.03, taper - 0.01, abs(uv.x - 0.5));
    edgeGlow *= shape; //只在形状内部

    // ========================================
    //  生长前沿特效
    // ========================================
    float growFront = smoothstep(growthCut + 0.12, growthCut + 0.03, uv.y)
                    * smoothstep(growthCut - 0.02, growthCut + 0.03, uv.y);
    growFront *= uGrowth * (1.0 - uShatter);

    // ========================================
    //  颜色合成
    // ========================================
    float3 col = IceDeep;

    //核心亮度
    col = lerp(col, IceCyan, coreGlow * 0.7);
    col = lerp(col, IceCoreWhite, coreGlow * coreGlow * 0.4);

    //结构纹理
    col += IceCyan * verticalVeins * 0.15;
    col += IceCoreWhite * horizontalBands * 0.25;
    col += IceCoreWhite * refractionHighlight * 0.35 * coreGlow;

    //边缘霜光
    col = lerp(col, FrostEdge, edgeGlow * 0.6);

    //顶部辉光
    col += IceCoreWhite * tipGlow * 0.8;

    //生长前沿: 明亮的冰光
    col += IceCoreWhite * growFront * 1.2;

    //碎裂边缘高光: 裂纹处更亮
    float crackEdge = smoothstep(uShatter + 0.08, uShatter, shatterNoise)
                    * smoothstep(uShatter - 0.08, uShatter, shatterNoise);
    col += IceCyan * crackEdge * uShatter * 2.0;

    // ========================================
    //  Alpha合成
    // ========================================
    float alpha = finalMask;
    alpha *= uIntensity;
    //中心更不透明，边缘半透明
    alpha *= lerp(0.5, 1.0, coreGlow);

    //色散: 边缘微弱RGB偏移
    float3 dispCol = col;
    float chromaShift = 0.003 * (1.0 - coreGlow);
    dispCol.r *= 1.0 + chromaShift * 5.0;
    dispCol.b *= 1.0 + chromaShift * 8.0;

    return float4(saturate(dispCol) * saturate(alpha), saturate(alpha));
}

technique IcePillar {
    pass P0 {
        PixelShader = compile ps_3_0 IcePillarPS();
    }
}
