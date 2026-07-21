// ============================================================
// 敖广·沧海沉浸 — 专属全屏后处理 (每帧 ≤1, 走名额契约)
// 四合一水系屏幕演出:
//   1) 水下折射: 噪声驱动 UV 扭曲 + 色散, 以 uCenter 为核心径向衰减
//   2) 水位线: 屏幕底部涨潮 (uWaterLevel = 水面高度占屏比, 0=无水),
//      水下部分折射加深 + 焦散微光 + 水面泡沫亮线 (水位叙事)
//   3) 向心吸入: uRadialPull (深渊漩涡 set-piece)
//   4) impact frame: uImpact 黑白高对比定格 (一场唯一的戟落瞬间)
// 喂 Main.screenTarget(s0) + 可平铺噪声(s1)
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标
sampler uNoise  : register(s1); // 可平铺三通道噪声

float  uTime;        // 动画时间 (秒)
float2 uCenter;      // 折射/吸入中心 (归一化屏幕 UV)
float  uIntensity;   // 折射强度 0~1
float  uRadius;      // 折射有效半径 (屏幕高度比例)
float  uAspect;      // 宽高比 width/height
float  uRadialPull;  // 向心吸入 0~1
float  uWaterLevel;  // 水位高度占屏比 0~1 (从屏幕底部起算)
float  uImpact;      // impact frame 黑白定格 0~1
float4 uTint;        // 主题染色 (rgb=色, a=覆盖权重)

float4 AbyssalSeaPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);
    float  normDist = dist / max(uRadius, 0.001);
    float2 radialDir = normalize(diff + 0.0001);

    // ==========================================
    //  水位线: 水面 Y (屏幕 UV, y 向下) + 噪声波动
    // ==========================================
    float surfaceY = 1.0 - uWaterLevel;
    float waveA = tex2D(uNoise, float2(coords.x * 1.8 + uTime * 0.06, uTime * 0.03)).r;
    float waveB = tex2D(uNoise, float2(coords.x * 3.5 - uTime * 0.09, 0.37)).g;
    surfaceY += (waveA - 0.5) * 0.028 + (waveB - 0.5) * 0.012;
    float underwater = (uWaterLevel > 0.003) ? smoothstep(surfaceY - 0.004, surfaceY + 0.02, coords.y) : 0.0;

    // ==========================================
    //  UV 偏移合成: 全局折射 + 水下折射 + 向心吸入
    // ==========================================
    float2 uvOffset = float2(0, 0);

    // —— 全局折射 (围绕 uCenter, 近强远弱) ——
    if (uIntensity > 0.005 && normDist < 2.5) {
        float angle   = atan2(diff.y, diff.x);
        float angNorm = angle / 6.28318 + 0.5;
        float n1 = tex2D(uNoise, float2(angNorm * 4.0 + uTime * 0.05, normDist * 2.0 - uTime * 0.04)).r;
        float n2 = tex2D(uNoise, float2(angNorm * 7.0 - uTime * 0.06, normDist * 3.2 + uTime * 0.03)).g;
        float fbm = n1 * 0.6 + n2 * 0.4;

        float falloff = smoothstep(2.0, 0.0, normDist);
        float strength = uIntensity * 0.05 * falloff;
        float2 tangentDir = float2(-radialDir.y, radialDir.x);
        uvOffset += (radialDir * (fbm - 0.5) * 2.0 + tangentDir * (n2 - 0.5) * 1.2) * strength;
    }

    // —— 向心吸入 (深渊漩涡: 越近吸得越狠, 带切向旋涡) ——
    if (uRadialPull > 0.005) {
        float pullFall = smoothstep(2.2, 0.1, normDist);
        float2 tangentDir = float2(-radialDir.y, radialDir.x);
        uvOffset -= radialDir * uRadialPull * pullFall * 0.075;
        uvOffset += tangentDir * uRadialPull * pullFall * 0.02 * sin(uTime * 2.0);
    }

    // —— 水下折射 (水面以下整体涌动) ——
    if (underwater > 0.001) {
        float2 wUV = coords * 3.0 + float2(uTime * 0.05, -uTime * 0.04);
        float wn = tex2D(uNoise, wUV).b;
        float depth = saturate((coords.y - surfaceY) / max(uWaterLevel, 0.05));
        uvOffset += float2((wn - 0.5) * 0.016, (waveA - 0.5) * 0.010) * underwater * (0.5 + depth);
    }

    uvOffset.x /= uAspect;
    float2 sampleUV = clamp(coords + uvOffset, 0.001, 0.999);
    float4 scene = tex2D(uImage0, sampleUV);

    // —— RGB 色散 (折射/吸入越强越明显) ——
    float chromaAmt = saturate(uIntensity * 0.6 + uRadialPull * 0.5) * smoothstep(2.0, 0.0, normDist);
    if (chromaAmt > 0.01) {
        float2 chOff = radialDir * chromaAmt * 0.010;
        chOff.x /= uAspect;
        scene.r = lerp(scene.r, tex2D(uImage0, clamp(sampleUV + chOff, 0.001, 0.999)).r, 0.8);
        scene.b = lerp(scene.b, tex2D(uImage0, clamp(sampleUV - chOff, 0.001, 0.999)).b, 0.8);
    }

    // ==========================================
    //  水下着色: 深水渐变 + 焦散微光 + 水面亮线
    // ==========================================
    if (underwater > 0.001) {
        float depth = saturate((coords.y - surfaceY) / max(uWaterLevel + 0.15, 0.15));

        // 深水色: 随深度从 uTint 过渡到深渊蓝
        float3 deepCol = lerp(uTint.rgb, float3(0.05, 0.13, 0.28), depth * 0.8);
        scene.rgb = lerp(scene.rgb, scene.rgb * float3(0.62, 0.82, 1.0) + deepCol * 0.30,
                         underwater * (0.35 + depth * 0.4));

        // 焦散: 双层噪声相乘锐化成光斑网
        float2 cUV1 = coords * 2.6 + float2(uTime * 0.07, uTime * 0.05);
        float2 cUV2 = coords * 4.2 - float2(uTime * 0.06, uTime * 0.03);
        float caustic = tex2D(uNoise, cUV1).r * tex2D(uNoise, cUV2).g;
        caustic = smoothstep(0.10, 0.34, caustic);
        scene.rgb += float3(0.35, 0.65, 0.85) * caustic * underwater * (1.0 - depth * 0.6) * 0.22;

        // 水面泡沫亮线
        float surfLine = exp(-abs(coords.y - surfaceY) * 160.0);
        scene.rgb += float3(0.75, 0.92, 1.0) * surfLine * 0.55;
    }

    // —— 主题染色 (折射中心附近) ——
    float tintCover = smoothstep(1.8, 0.1, normDist) * uTint.a * uIntensity;
    scene.rgb = lerp(scene.rgb, uTint.rgb, tintCover * 0.30);

    // ==========================================
    //  impact frame: 黑白高对比定格 (戟落瞬间, 一场唯一)
    // ==========================================
    if (uImpact > 0.01) {
        float luma = dot(scene.rgb, float3(0.299, 0.587, 0.114));
        float mono = smoothstep(0.32, 0.58, luma); // 硬阈值出黑白版画感
        float3 impactCol = lerp(float3(0.02, 0.05, 0.10), float3(0.92, 0.98, 1.0), mono);
        scene.rgb = lerp(scene.rgb, impactCol, saturate(uImpact));
    }

    return scene;
}

technique Technique1
{
    pass AbyssalSeaPass
    {
        PixelShader = compile ps_3_0 AbyssalSeaPS();
    }
}
