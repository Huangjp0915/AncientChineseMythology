// ============================================================
// 白虎·金属光泽着色器 — 本体重绘 pass (Immediate batch 套 effect 重画本体贴图)
// 各向异性高光沿 uSheenAngle 扫过 + 银色 rim light + uFlash 蓄势闪白
// 喂白虎本体贴图(s0) + 可平铺噪声(s1)
// ============================================================

sampler uImage0 : register(s0); // 白虎本体贴图
sampler uNoise  : register(s1); // 可平铺噪声 (RGB三通道独立)

float  uTime;       // 动画时间(秒)
float  uIntensity;  // 金属质感总强度 0~1 (P2 起淡入)
float  uSheenAngle; // 高光扫掠方向(弧度, 贴图空间)
float  uSheenPos;   // 高光带中心位置 -0.5~1.5 (沿扫掠方向推移)
float  uFlash;      // 蓄势闪白 0~1 (发射前 6 帧=1)
float2 uTexelSize;  // 1/贴图尺寸 (rim 采样步长)
float  uFlip;       // 1=水平翻转绘制(rim 方向补偿), 0=正常

float4 MetalSheenPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 baseTex = tex2D(uImage0, coords);
    if (baseTex.a < 0.01)
        return float4(0, 0, 0, 0);

    float4 col = baseTex * sampleColor;

    // ==========================================
    //  各向异性高光带: 贴图坐标投影到扫掠方向, 窄带亮起并沿 uSheenPos 推移
    // ==========================================
    float2 dir = float2(cos(uSheenAngle), sin(uSheenAngle));
    float2 pc = coords - 0.5;
    if (uFlip > 0.5) pc.x = -pc.x; // 翻转绘制时保持世界方向一致
    float proj = dot(pc, dir) + 0.5;      // 0~1 扫掠坐标

    // 微噪声扰动扫掠坐标 → 毛发上的金属丝感(各向异性)
    float strand = tex2D(uNoise, float2(coords.x * 5.0, coords.y * 5.0)).g;
    proj += (strand - 0.5) * 0.10;

    float sheenDist = abs(proj - uSheenPos);
    float sheen = exp(-sheenDist * sheenDist * 90.0);           // 窄高光带
    float sheenWide = exp(-sheenDist * sheenDist * 14.0) * 0.4; // 宽余晖

    // 高光只亮在贴图较亮处(毛色高光区), 避免糊掉暗部轮廓
    float lumBase = dot(baseTex.rgb, float3(0.299, 0.587, 0.114));
    float sheenMask = smoothstep(0.18, 0.65, lumBase);

    float3 silver = float3(0.86, 0.91, 1.0);
    col.rgb += silver * (sheen * 0.85 + sheenWide) * sheenMask * uIntensity * baseTex.a;

    // ==========================================
    //  银色 rim light: alpha 梯度找轮廓(4 邻域), 上缘偏亮
    // ==========================================
    float2 ts = uTexelSize;
    float aR = tex2D(uImage0, coords + float2(ts.x * 2.0, 0)).a;
    float aL = tex2D(uImage0, coords - float2(ts.x * 2.0, 0)).a;
    float aD = tex2D(uImage0, coords + float2(0, ts.y * 2.0)).a;
    float aU = tex2D(uImage0, coords - float2(0, ts.y * 2.0)).a;
    float2 grad = float2(aR - aL, aD - aU);
    float rim = saturate(length(grad) * 1.6) * baseTex.a;

    // 上缘 rim 更亮(天光方向), 且随时间微微流动
    float topBias = saturate(-grad.y * 2.0) * 0.6 + 0.4;
    float rimFlow = 0.8 + 0.2 * sin(uTime * 2.4 + coords.y * 9.0);
    col.rgb += silver * rim * topBias * rimFlow * 0.7 * uIntensity;

    // ==========================================
    //  蓄势闪白: 全身向银白推 + rim 爆亮
    // ==========================================
    if (uFlash > 0.001) {
        float3 flashWhite = float3(0.97, 0.99, 1.0);
        col.rgb = lerp(col.rgb, flashWhite * baseTex.a, uFlash * 0.82);
        col.rgb += silver * rim * uFlash * 1.6;
    }

    return col;
}

technique Technique1
{
    pass MetalSheenPass
    {
        PixelShader = compile ps_3_0 MetalSheenPS();
    }
}
