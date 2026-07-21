// ============================================================
// 阴天子·酆都法庭结界着色器 — 屏幕空间 SDF (参数约定同 ArenaRunic)
// 双层反向旋转"判词环" + 六座界碑锚点 + 内圈锁链纹 + 收缩形变
// uCollapse 0->1: 镇魂狱收缩压迫(结界向内形变并转赤)
// 喂可平铺噪声(s0), 由 DrawScreenSpaceDecalStandalone 全屏绘制
// ============================================================

sampler uNoise : register(s0); // 可平铺噪声(RGB三通道独立)

float  uTime;            // 动画时间(秒)
float2 uCenter;          // 中心归一化屏幕坐标 0~1
float  uRadius;          // 半径(屏幕高度比例)
float  uIntensity;       // 整体强度 0~1
float  uAspect;          // 宽高比 width/height
float  uCollapse;        // 收缩压迫 0~1 (转赤 + 内吸形变)
float  uFlash;           // 大节拍闪光 0~1 (落成/破封瞬间)
float4 uColorPrimary;    // 主色(帝金)
float4 uColorSecondary;  // 辅色(冥紫)

float4 CourtPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);

    // 呼吸 + 收缩时的紧缩颤动
    float breath = sin(uTime * 1.3) * 0.004 * uRadius
                 - uCollapse * uRadius * (0.015 + 0.008 * sin(uTime * 9.0));
    float effRadius = max(uRadius + breath, 0.001);
    float normDist = dist / effRadius;

    if (normDist > 1.45 || normDist < 0.30)
        return float4(0, 0, 0, 0);

    float angle = atan2(diff.y, diff.x);
    float angN = angle / 6.28318 + 0.5;

    // 噪声形变(有机呼吸感)
    float nWarp = tex2D(uNoise, float2(angN * 3.0 + uTime * 0.03, normDist * 2.0)).b;
    float dN = normDist + (nWarp - 0.5) * 0.06;

    // —— 主界环(双层) ——
    float th = 0.045;
    float ringA = smoothstep(th, th * 0.25, abs(dN - 1.0));
    float ringB = smoothstep(th * 0.7, th * 0.15, abs(dN - 0.905)) * 0.7;

    // —— 判词环: 双层角向字符带, 反向旋转 ——
    // 外带(顺时针, 金)
    float scriptA = tex2D(uNoise, float2(angN * 9.0 + uTime * 0.045, 0.15)).r;
    float glyphA = step(0.55, scriptA) * step(0.25, frac(angN * 72.0));
    float bandA = smoothstep(0.035, 0.012, abs(dN - 0.955)) * glyphA;
    // 内带(逆时针, 紫)
    float scriptB = tex2D(uNoise, float2(angN * 7.0 - uTime * 0.035 + 0.4, 0.62)).g;
    float glyphB = step(0.58, scriptB) * step(0.3, frac(angN * 54.0 + 0.5));
    float bandB = smoothstep(0.030, 0.010, abs(dN - 1.052)) * glyphB;

    // —— 六座界碑锚点: 固定六向亮斑(幡旗立足处) ——
    float pillar = pow(abs(cos(angle * 3.0)), 48.0);
    float pillarGlow = pillar * smoothstep(0.10, 0.02, abs(dN - 1.0));

    // —— 内圈锁链纹: 同心弧纹缓慢内旋 ——
    float chain = 0.5 + 0.5 * sin(dN * 34.0 - uTime * 0.7);
    chain = pow(chain, 9.0) * smoothstep(0.95, 0.62, dN) * smoothstep(0.34, 0.52, dN) * 0.30;

    // —— 界外警戒辉光 ——
    float outerGlow = smoothstep(1.30, 1.02, dN) * smoothstep(1.0, 1.03, dN) * 0.16;
    // 收缩时地面弥漫压迫红
    float fill = smoothstep(1.0, 0.3, dN) * uCollapse * 0.10;

    float shape = max(max(ringA, ringB), max(max(bandA * 0.85, bandB * 0.7), max(pillarGlow, max(chain, max(outerGlow, fill)))));

    // —— 配色: 主金辅紫; 收缩时整体转赤 ——
    float mixT = smoothstep(0.9, 1.06, dN);
    float3 col = lerp(uColorSecondary.rgb, uColorPrimary.rgb, mixT);
    col = lerp(col, uColorPrimary.rgb, bandA);
    col = lerp(col, uColorSecondary.rgb, bandB * 0.8);
    col += uColorPrimary.rgb * pillarGlow * 0.9;
    col = lerp(col, float3(0.85, 0.12, 0.14), uCollapse * 0.6);

    // 大节拍闪光: 全环短暂过曝
    col += float3(1.0, 0.96, 0.85) * uFlash * shape * 1.8;

    float pulse = 0.88 + 0.12 * sin(uTime * 2.0 + angle * 2.0);
    float alpha = saturate(shape * pulse * uIntensity);
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass CourtPass
    {
        PixelShader = compile ps_3_0 CourtPS();
    }
}
