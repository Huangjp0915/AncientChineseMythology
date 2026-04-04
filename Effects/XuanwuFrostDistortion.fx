// ============================================================
// 玄武冰霜扭曲着色器 — 全屏后处理
// 基于多层噪声的UV偏移模拟冰晶折射扭曲
// 从Boss中心向外辐射冰裂纹 + 屏幕边缘霜冻覆盖
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标 (原始屏幕画面)
sampler uNoise  : register(s1); // 可平铺噪声纹理 (RGB三通道独立)

float uTime;           // 动画时间 (秒)
float2 uCenter;        // Boss归一化屏幕坐标 (0~1)
float uIntensity;      // 整体强度 (0~1)
float uFrostRadius;    // 冰霜扭曲有效半径 (屏幕高度比例)
float uCrystalScale;   // 冰晶纹路密度 (建议2~8)
float uAspect;         // 屏幕宽高比 (width / height)

// 颜色常量
static const float3 FrostTint  = float3(0.55, 0.78, 0.95); // 冰蓝色调
static const float3 DeepFrost  = float3(0.15, 0.25, 0.45); // 深冰蓝
static const float3 IceWhite   = float3(0.85, 0.92, 1.0);  // 冰白高光

float4 FrostDistortionPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    // ==========================================
    //  宽高比校正坐标
    // ==========================================
    float2 pos = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff = pos - center;
    float dist = length(diff);

    // 归一化距离(0=Boss中心, 1=有效半径边缘)
    float normDist = dist / max(uFrostRadius, 0.001);

    // 超出范围太远直接返回原色
    if (normDist > 2.5 || uIntensity < 0.001)
        return tex2D(uImage0, coords);

    // ==========================================
    //  极坐标 (用于径向冰裂纹)
    // ==========================================
    float angle = atan2(diff.y, diff.x);
    float angNorm = angle / 6.28318 + 0.5; // 0~1

    // ==========================================
    //  多层噪声FBM — 冰裂纹扰动
    // ==========================================
    float2 n1UV = float2(angNorm * uCrystalScale * 3.0 + uTime * 0.03,
                          normDist * 2.0 - uTime * 0.02);
    float2 n2UV = float2(angNorm * uCrystalScale * 5.0 - uTime * 0.04,
                          normDist * 3.5 + uTime * 0.025);
    float2 n3UV = coords * uCrystalScale * 1.5 + float2(uTime * 0.015, -uTime * 0.02);

    float n1 = tex2D(uNoise, n1UV).r;
    float n2 = tex2D(uNoise, n2UV).g;
    float n3 = tex2D(uNoise, n3UV).b;

    float fbm = n1 * 0.5 + n2 * 0.3 + n3 * 0.2;

    // ==========================================
    //  冰裂纹分支 — 极坐标高频噪声
    // ==========================================
    float2 crackUV = float2(angNorm * uCrystalScale * 8.0, normDist * 4.0 + uTime * 0.05);
    float crackNoise = tex2D(uNoise, crackUV).r;

    // 锐化为裂纹线条
    float crack = smoothstep(0.42, 0.48, crackNoise) * smoothstep(0.58, 0.52, crackNoise);
    crack *= smoothstep(1.8, 0.3, normDist); // 近处裂纹更密
    crack *= uIntensity;

    // ==========================================
    //  UV扭曲 — 冰晶折射
    // ==========================================
    // 扭曲方向：沿径向+切向各有偏移
    float2 radialDir = normalize(diff + 0.0001);
    float2 tangentDir = float2(-radialDir.y, radialDir.x);

    // 扭曲强度随距离衰减(近处强，远处弱)
    float distortFalloff = smoothstep(2.0, 0.0, normDist);
    float distortStrength = uIntensity * 0.035 * distortFalloff;

    // 噪声驱动的扭曲偏移
    float radialOffset = (fbm - 0.5) * 2.0;
    float tangentOffset = (n2 - 0.5) * 2.0;

    // 冰裂纹处额外扭曲
    float crackDistort = crack * 0.8;

    float2 uvOffset = (radialDir * (radialOffset + crackDistort) +
                       tangentDir * tangentOffset * 0.5) * distortStrength;

    // 宽高比反校正回UV空间
    uvOffset.x /= uAspect;

    float2 distortedUV = coords + uvOffset;

    // UV安全夹紧
    distortedUV = clamp(distortedUV, 0.001, 0.999);

    // ==========================================
    //  采样扭曲后的场景
    // ==========================================
    float4 sceneColor = tex2D(uImage0, distortedUV);

    // ==========================================
    //  色散效果 — RGB轻微偏移
    // ==========================================
    float chromaStr = uIntensity * 0.008 * distortFalloff;
    float2 chromaOffset = radialDir * chromaStr;
    chromaOffset.x /= uAspect;

    float rChannel = tex2D(uImage0, distortedUV + chromaOffset).r;
    float bChannel = tex2D(uImage0, distortedUV - chromaOffset).b;
    sceneColor.r = lerp(sceneColor.r, rChannel, uIntensity * 0.5);
    sceneColor.b = lerp(sceneColor.b, bChannel, uIntensity * 0.5);

    // ==========================================
    //  冰霜覆盖层 — 越近Boss越白蓝
    // ==========================================
    float frostCover = smoothstep(1.5, 0.2, normDist) * uIntensity * 0.25;
    float3 frostColor = lerp(FrostTint, IceWhite, smoothstep(0.8, 0.1, normDist));
    sceneColor.rgb = lerp(sceneColor.rgb, frostColor, frostCover);

    // ==========================================
    //  冰裂纹亮线叠加
    // ==========================================
    float3 crackColor = lerp(FrostTint, IceWhite, crack);
    sceneColor.rgb += crackColor * crack * 0.6 * uIntensity;

    // ==========================================
    //  屏幕边缘霜冻 — 四角渐变
    // ==========================================
    float2 edgeDist = abs(coords - 0.5) * 2.0; // 0=中心, 1=边缘
    float edgeFrost = max(edgeDist.x, edgeDist.y);
    edgeFrost = smoothstep(0.55, 1.0, edgeFrost);

    // 边缘霜冻纹路 — 小尺度噪声
    float2 edgeNoiseUV = coords * 6.0 + float2(uTime * 0.01, uTime * 0.008);
    float edgeNoise = tex2D(uNoise, edgeNoiseUV).r;
    float edgePattern = smoothstep(0.3, 0.7, edgeNoise);

    float edgeCover = edgeFrost * edgePattern * uIntensity * 0.45;
    float3 edgeColor = lerp(DeepFrost, FrostTint, edgeNoise);
    sceneColor.rgb = lerp(sceneColor.rgb, edgeColor, edgeCover);

    // 边缘霜冻白线
    float edgeLine = smoothstep(0.46, 0.50, edgeNoise) * smoothstep(0.54, 0.50, edgeNoise);
    sceneColor.rgb += IceWhite * edgeLine * edgeFrost * uIntensity * 0.3;

    // ==========================================
    //  呼吸脉冲 — 整体微弱明暗交替
    // ==========================================
    float breath = sin(uTime * 1.5) * 0.02 + 1.0;
    sceneColor.rgb *= lerp(1.0, breath, uIntensity * 0.3);

    return sceneColor;
}

technique Technique1
{
    pass FrostDistortionPass
    {
        PixelShader = compile ps_3_0 FrostDistortionPS();
    }
}
